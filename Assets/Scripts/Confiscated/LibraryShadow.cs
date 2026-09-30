using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>
    /// The shadow in the library maze (Docs/LibraryMaze.md). It drifts from alcove to alcove along the library floor. It
    /// notices a player who moves near it, or who has a torch on, in its line of sight: it stops, turns and shushes. Freeze
    /// (still, torch off) until the shushing ends and it drifts on past; keep moving or keep the light on and it hunts,
    /// faster than a walk, until it catches the player (thrown out of the nearest door; the handheld game goes back in its box) or the
    /// player leaves the library. The caretaker is never told. It never leaves the library's NavMesh area.
    /// </summary>
    public sealed class LibraryShadow : MonoBehaviour
    {
        public enum Phase { Wander, Linger, Notice, Hunt, Gone }
        public Transform[] alcoves;
        [Tooltip("Just inside each library door; exits[i] is just outside the same door.")]
        public Transform[] entrances,exits;
        public Renderer[] silhouette;
        public Renderer[] eyes;
        [Tooltip("Dark smoke that rolls off it and trails behind as it moves.")]
        public ParticleSystem smoke;
        [Tooltip("Rigged figure: Drift while wandering, Hunt (bool \"Hunting\") while it comes for you.")]
        public Animator animator;
        public Bounds room;
        public int areaMask=1<<3;
        public float wanderSpeed=1.1f,huntSpeed=3.3f,noticeSeconds=1.3f,lingerSeconds=3;
        public const float reactionSeconds=.35f;
        public float moveSense=8,lightSense=14,movingSpeed=.35f,catchRadius=.75f,goneSeconds=2.5f;
        public const string CatchLine="GET OUT!";
        /// <summary>The library's own belonging: a catch puts it back in its box on the returns desk.</summary>
        public const int LibraryItem=2;
        public int ReturnedItems {get;private set;}
        public Phase Current {get;private set;}
        public int Catches {get;private set;}
        public int Notices {get;private set;}
        public int PassedBy {get;private set;}
        /// <summary>Tests (and the autopilot) that are about something else in the library hold it still.</summary>
        public bool Paused {get;set;}
        public float PlayerSpeed {get;private set;}
        float phaseEnds,repath;int corner,target;NavMeshPath path;Material skin;AudioSource voice,breath,sting;bool wasRound,catching;
        // Sounds: its breathing (3D loop), "I see you" when it notices you, a wire-scrape sting when it comes into view,
        // and a scream for the catch jumpscare.
        AudioClip seeYou,spotted,scream;float outOfViewSince=-999;bool inView;
        // Separate sightings need time out of view and a global cooldown so shelf-edge glimpses cannot spam the sting.
        public const float JumpscareSeconds=2.05f,NewSightingSeconds=3,SpottedCooldown=12,SpottedRange=14,TorchGraceSeconds=.35f,MovementGraceSeconds=.35f;
        float nextSpottedAt,nextNoticeVoiceAt;
        float litSeconds,movingSeconds,calmSince=-1;
        public int Spotted {get;private set;}
        public bool Catching=>catching;
        [Tooltip("Chance each wander goes to a window's pool of light instead of an alcove, so it can be glimpsed from the corridor.")]
        [Range(0,1)] public float windowChance=.35f;
        public int WindowVisits {get;private set;}
        public bool GoingToWindow {get;private set;}
        Vector3 goal;
        // Eyes are HDR to burn through the library darkness; from outside it they are toned down to match.
        const float EyeLift=4.2f;MaterialPropertyBlock eyeBlock;Color[] eyeColours;
        /// <summary>Tracks the first library entry; the former long entry shush is silent.</summary>
        public static bool Shushed {get;private set;}
        public static void ResetSession()=>Shushed=false;
        Vector3 lastPlayer;bool havePlayer;float alpha;AudioClip catchClip;

        void Start()
        {
            worldHead=gameObject.AddComponent<LibraryShadowWorldHead>();worldHead.Build(this);
            path=new NavMeshPath();
            if(silhouette.Length>0&&silhouette[0]!=null){skin=new Material(silhouette[0].sharedMaterial);foreach(var r in silhouette)r.sharedMaterial=skin;}
            voice=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);voice.playOnAwake=false;voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Linear;
            voice.minDistance=2;voice.maxDistance=20;voice.dopplerLevel=0;
            catchClip=Resources.Load<AudioClip>("Audio/LibraryShadowGetOut");
            seeYou=Resources.Load<AudioClip>("Audio/LibraryShadowISeeYou");spotted=Resources.Load<AudioClip>("Audio/LibraryShadowSpotted");scream=Resources.Load<AudioClip>("Audio/LibraryShadowScream");
            breath=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Effects,true);breath.clip=Resources.Load<AudioClip>("Audio/LibraryShadowBreathLoop");breath.loop=true;breath.playOnAwake=false;
            breath.spatialBlend=1;breath.rolloffMode=AudioRolloffMode.Linear;breath.minDistance=1.5f;breath.maxDistance=14;breath.dopplerLevel=0;breath.volume=0;
            sting=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Effects,true);sting.playOnAwake=false;sting.spatialBlend=0;
            Place(0);Wander();Visual(true);
        }
        void OnDestroy(){if(skin!=null)Destroy(skin);}
        LibraryShadowWorldHead worldHead;

        void Update()
        {
            var run=SchoolRunController.Instance;
            bool round=run!=null&&run.RoundStarted;
            // A new round (or a retry) starts it over in a random alcove.
            if(round&&!wasRound){Place(Random.Range(0,alcoves.Length));Wander();}wasRound=round;
            bool live=round&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&!Paused&&!ComicDialogue.IsActive&&Time.timeScale>0;
            if(!live){havePlayer=false;litSeconds=movingSeconds=0;LostSight();Visual(false);Breathe(false);return;}
            if(catching){LostSight();return;}
            var player=run.period.Player;Vector3 at=player.transform.position;
            // Walking speed from the player's own movement; warps (a throw-out, a retry) are not movement.
            Vector3 step=at-lastPlayer;step.y=0;float speed=havePlayer&&Time.deltaTime>0?step.magnitude/Time.deltaTime:0;
            // A released movement key must count as still immediately; smoothing used to carry a run into the shush.
            PlayerSpeed=speed>12||speed<.08f?0:Mathf.Lerp(PlayerSpeed,speed,Mathf.Clamp01(Time.deltaTime*12));lastPlayer=at;havePlayer=true;
            bool inside=Inside(at);
            // A torch has to stay on it for a moment before it counts -- a quick sweep of the beam past it is forgiven.
            bool lit=inside&&Flat(at-transform.position)<=lightSense&&TorchLightsMe(player)&&Sight(player,at);
            litSeconds=lit?litSeconds+Time.deltaTime:0;
            bool moving=inside&&PlayerSpeed>movingSpeed&&Flat(at-transform.position)<=moveSense&&Sight(player,at);
            movingSeconds=moving?movingSeconds+Time.deltaTime:0;
            if(inside&&!Shushed)Shushed=true;
            // Out of the library: it forgets the player and goes back to drifting.
            if(!inside&&(Current==Phase.Notice||Current==Phase.Hunt))Wander();
            switch(Current)
            {
                case Phase.Wander:
                    if(inside&&Senses(player,at)){Notice(at);break;}
                    if(Follow(goal,wanderSpeed)){Current=Phase.Linger;phaseEnds=Time.time+lingerSeconds*(GoingToWindow?1.5f:1);}
                    break;
                case Phase.Linger:
                    if(inside&&Senses(player,at)){Notice(at);break;}
                    if(Time.time>=phaseEnds)Wander();
                    break;
                case Phase.Notice:
                    Face(at-transform.position);
                    // A moment to react, then any movement or light gives the player away.
                    if(Time.time>=phaseEnds-noticeSeconds+reactionSeconds&&Gives(player,at)){Hunt();break;}
                    if(Time.time>=phaseEnds){PassedBy++;Current=Phase.Wander;repath=0;}
                    break;
                case Phase.Hunt:
                    // Commit to the rush first; sustained stillness and darkness can calm it only afterward.
                    if(Time.time>=phaseEnds&&PlayerSpeed<=movingSpeed&&!Lit)
                    {
                        if(calmSince<0)calmSince=Time.time;
                        if(Time.time-calmSince>=1.6f){PassedBy++;Wander();break;}
                        Face(at-transform.position);break;
                    }
                    calmSince=-1;
                    Follow(at,huntSpeed);
                    break;
                case Phase.Gone:
                    if(Time.time>=phaseEnds)Wander();
                    break;
            }
            if(Current==Phase.Hunt){Vector3 gap=at-transform.position;gap.y=0;if(gap.magnitude<catchRadius){StartCoroutine(Catch(player));return;}}
            // From anywhere, not just inside: seen through a library window from the corridor counts too (the glass is
            // on Ignore Raycast, so it doesn't block the sight line).
            Sighting(player);
            Breathe(Current!=Phase.Gone);
            Visual(false);
        }

        public bool Inside(Vector3 p)=>room.Contains(new Vector3(p.x,room.center.y,p.z));
        bool TorchLightsMe(PlayerInteractor player)
        {
            var torch=player.GetComponent<PlayerTorch>();var beam=torch!=null?torch.Beam:null;
            if(beam==null||!torch.IsOn)return false;
            Vector3 from=beam.transform.position,to=transform.position+Vector3.up*1.45f;
            Vector3 ray=to-from;
            if(ray.sqrMagnitude>beam.range*beam.range||Vector3.Angle(beam.transform.forward,ray)>beam.spotAngle*.5f)return false;
            return !Physics.Linecast(from,to,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||
                hit.transform.IsChildOf(transform)||hit.transform.IsChildOf(player.transform);
        }
        /// <summary>It can see the player, and the player is moving close by or showing a light.</summary>
        public bool Senses(PlayerInteractor player,Vector3 at)
        {
            float d=Flat(at-transform.position);
            if(d>Mathf.Max(moveSense,lightSense)||!Sight(player,at))return false;
            return Lit||movingSeconds>=MovementGraceSeconds&&d<=moveSense;
        }
        /// <summary>While it is shushing: any movement at all, or a light held on it.</summary>
        bool Gives(PlayerInteractor player,Vector3 at)=>(PlayerSpeed>movingSpeed||Lit)&&Sight(player,at);
        bool Lit=>litSeconds>=TorchGraceSeconds;
        bool Sight(PlayerInteractor player,Vector3 at)
        {
            var legs=player.GetComponent<FirstPersonController>();
            Vector3 eye=transform.position+Vector3.up*1.6f,head=at+Vector3.up*(legs!=null?legs.TorsoHeight:1.2f);
            return !Physics.Linecast(eye,head,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(player.transform)||hit.transform.IsChildOf(transform);
        }

        void Wander()
        {
            int next=target;for(int i=0;i<8&&(next==target||alcoves.Length<2);i++){next=Random.Range(0,alcoves.Length);if(alcoves.Length<2)break;}
            target=next;goal=alcoves[target].position;GoingToWindow=false;
            // Now and then it drifts to a window's pool of light and lingers: seen from the corridor, a shape with eyes.
            var windows=LibraryWindow.All;
            if(windows.Count>0&&Random.value<windowChance&&NavMesh.SamplePosition(windows[Random.Range(0,windows.Count)].pool,out var hit,1.5f,areaMask))
            {goal=hit.position;GoingToWindow=true;WindowVisits++;}
            Current=Phase.Wander;calmSince=-1;repath=0;path?.ClearCorners();
        }
        void Notice(Vector3 at)
        {
            Notices++;Current=Phase.Notice;phaseEnds=Time.time+noticeSeconds;Face(at-transform.position);
            if(Time.time>=nextNoticeVoiceAt&&!voice.isPlaying)
            {
                if(seeYou!=null){voice.pitch=1;voice.PlayOneShot(seeYou,1);}else Whisper(1,1);
                nextNoticeVoiceAt=Time.time+Mathf.Max(18,seeYou!=null?seeYou.length+1:0);
            }
        }
        void Hunt(){Current=Phase.Hunt;calmSince=-1;repath=0;phaseEnds=Time.time+2.5f;}
        /// <summary>
        /// The jumpscare, in four beats, the shape every good one shares: a held breath, a rush, the hit, a hard cut.
        /// 1. It is gone, its breathing stops, and a whisper comes from nowhere. 2. It appears dead ahead and rushes the
        /// face in a third of a second, screaming, the lens punching wide. 3. The face fills the view: the picture shakes,
        /// strobes and tears. 4. Black. Then you are out of the nearest door. Camera motion honours the F8 setting.
        /// </summary>
        const float WhisperBeat=.85f,LungeBeat=.32f,HoldBeat=.55f,BlackBeat=.33f,FaceDistance=.32f;
        System.Collections.IEnumerator Catch(PlayerInteractor player)
        {
            catching=true;Current=Phase.Hunt;
            SchoolAudio.DuckForScare(JumpscareSeconds);
            var body=player.GetComponent<CharacterController>();var legs=player.GetComponent<FirstPersonController>();var feel=player.GetComponent<ChaseCamera>();
            if(legs!=null){legs.MovementLocked=true;legs.LookLocked=true;}
            var cam=player.ViewCamera;var eye=cam.transform;
            Vector3 eyePos=eye.localPosition;Quaternion eyeRot=eye.localRotation;float near=cam.nearClipPlane,fov=cam.fieldOfView;
            float motion=feel!=null?feel.intensity:1;
            Vector3 look=eye.forward;look.y=0;if(look.sqrMagnitude<.01f)look=transform.forward;look.Normalize();
            // 1. Nothing. The scare is the contrast, so the library goes quiet first.
            if(breath!=null){breath.Stop();breath.volume=0;}voice.Stop();sting.Stop();sting.volume=1;
            Show(0,1);if(animator!=null&&animator.runtimeAnimatorController!=null)animator.SetBool("Hunting",true);
            var you=Resources.Load<AudioClip>("Audio/LibraryShadowYou");if(you!=null)sting.PlayOneShot(you,1);
            for(float t=0;t<WhisperBeat;t+=Time.unscaledDeltaTime)
            {sting.volume=1-Mathf.Clamp01((t-WhisperBeat+.08f)/.08f);yield return null;}
            // 2. Measured peak at .731 s: start .32 s before it, so the strongest hit lands at face arrival.
            sting.Stop();sting.volume=1;
            if(scream!=null){sting.clip=scream;sting.time=Mathf.Clamp(.731375f-LungeBeat,0,Mathf.Max(0,scream.length-.01f));sting.Play();}else if(catchClip!=null)voice.PlayOneShot(catchClip,1);else TempAudio.PlayAt(TempAudio.Caught,eye.position,.8f);
            // The generated head moves in depth on a private 3D stage. No flat face is used for the catch.
            var face=ScareFaceOverride;
            var overlay=BuildScareOverlay(face,out var backdrop,out var art,out var flash);
            var model=overlay.GetComponent<LibraryShadowScareHead>();
            cam.nearClipPlane=.02f;
            Quaternion level=Quaternion.Inverse(eye.parent.rotation)*Quaternion.LookRotation(look);
            for(float t=0;t<LungeBeat;t+=Time.unscaledDeltaTime)
            {
                float k=Mathf.Pow(Mathf.Clamp01(t/LungeBeat),2.4f);
                Pose(eye,look,Mathf.Lerp(3.5f,FaceDistance,k));Show(1,1+3*k);
                eye.localRotation=Quaternion.Slerp(eyeRot,level,Mathf.Clamp01(t/.12f));
                cam.fieldOfView=fov+motion*Mathf.Sin(k*Mathf.PI)*14;
                if(model!=null)model.Pose(k,0,motion);
                else if(art!=null){art.rectTransform.localScale=Vector3.one*Mathf.Lerp(.1f,1.25f,k);art.color=new Color(1,1,1,Mathf.Clamp01(t/.08f));}
                yield return null;
            }
            // 3. The hit. Face filling the view, eyes burning, the picture rattling and strobing, the tape tearing.
            HudController.Instance?.SetBark(CatchLine,2.5f);feel?.Kick(1);
            if(backdrop!=null)backdrop.enabled=art!=null;
            Vector3 rest=eye.localPosition;Quaternion faceRot=eye.localRotation;
            for(float t=0;t<HoldBeat;t+=Time.unscaledDeltaTime)
            {
                float s=motion*(1-t/HoldBeat*.5f);
                Pose(eye,look,FaceDistance+Mathf.Sin(t*90)*.02f*s);Show(1,4);HuntVhsEffect.Burst=1;
                eye.localPosition=rest+new Vector3(Mathf.Sin(t*173)*.035f,Mathf.Sin(t*211)*.03f,0)*s;
                eye.localRotation=faceRot*Quaternion.Euler(Mathf.Sin(t*151)*2.5f*s,Mathf.Sin(t*137)*2f*s,Mathf.Sin(t*191)*5f*s);
                cam.fieldOfView=fov-6*s;
                if(model!=null)model.Pose(1,t,motion);
                else if(art!=null)
                {
                    // Still creeping closer, and rattling with the camera.
                    art.rectTransform.localScale=Vector3.one*(1.25f+t/HoldBeat*.35f)*(1+Mathf.Sin(t*160)*.02f*s);
                    art.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(t*173)*22*s,-30+Mathf.Sin(t*211)*16*s);
                    art.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*191)*3*s);
                }
                // One brief interruption after the face has registered; independent of refresh rate.
                flash.enabled=motion>.75f&&t>=.16f&&t<.20f;
                yield return null;
            }
            // 4. Cut.
            flash.enabled=true;Show(0,1);
            for(float t=0;t<BlackBeat;t+=Time.unscaledDeltaTime)
            {
                sting.volume=1-Mathf.Clamp01(t/.08f);yield return null;
            }
            sting.Stop();sting.volume=1;voice.Stop();HuntVhsEffect.Burst=0;
            eye.localPosition=eyePos;eye.localRotation=eyeRot;cam.nearClipPlane=near;cam.fieldOfView=feel!=null?feel.BaseFov:fov;
            Destroy(overlay);
            Vector3 at=player.transform.position;int door=0;float best=float.MaxValue;
            for(int i=0;i<exits.Length;i++){float d=Flat(entrances[i].position-at);if(d<best){best=d;door=i;}}
            if(SchoolRunController.Instance!=null&&SchoolRunController.Instance.ReturnToBox(LibraryItem)){ReturnedItems++;HudController.Instance?.SetStatus("The handheld game is back in its CONFISCATED box on the returns desk.",4);}
            // Out of the nearest door, facing away from it. Everything else carried is kept; the caretaker is not told.
            if(body!=null)body.enabled=false;
            Vector3 away=exits[door].position-entrances[door].position;away.y=0;
            player.transform.SetPositionAndRotation(exits[door].position,Quaternion.LookRotation(away.sqrMagnitude>.01f?away:Vector3.forward));
            if(body!=null)body.enabled=true;Physics.SyncTransforms();lastPlayer=player.transform.position;
            if(legs!=null){legs.MovementLocked=false;legs.LookLocked=false;}
            int far=0;best=-1;for(int i=0;i<alcoves.Length;i++){float d=Flat(alcoves[i].position-exits[door].position);if(d>best){best=d;far=i;}}
            Place(far);Current=Phase.Gone;phaseEnds=Time.time+goneSeconds;Catches++;catching=false;
        }
        /// <summary>Root placed so its eyes (2 m up the rig) sit level with the player's, a given distance dead ahead.</summary>
        void Pose(Transform eye,Vector3 look,float distance)=>transform.SetPositionAndRotation(eye.position+look*distance-Vector3.up*2f,Quaternion.LookRotation(-look));
        /// <summary>Direct control of how much of it shows during the scare (Visual is skipped while catching).</summary>
        void Show(float a,float eyeBoost)
        {
            worldHead?.SetVisible(a,true);
            alpha=a;if(skin!=null){var c=skin.GetColor("_BaseColor");c.a=a;skin.SetColor("_BaseColor",c);}
            foreach(var r in silhouette)if(r!=null)r.enabled=a>.01f;
            if(smoke!=null){var e=smoke.emission;e.enabled=a>.01f;e.rateOverTimeMultiplier=160;}
            Eyes(a>.01f,eyeBoost);
        }
        public const string ScareFaceResource="Art/LibraryShadowScareFace";
        /// <summary>Tests can stand in a face without adding the asset.</summary>
        public static Texture ScareFaceOverride;
        /// <summary>Above everything: a black backdrop (on from the hit), the face drawing, and a black flash for the strobe and the cut.</summary>
        static GameObject BuildScareOverlay(Texture face,out UnityEngine.UI.Image backdrop,out UnityEngine.UI.RawImage art,out UnityEngine.UI.Image flash)
        {
            var g=new GameObject("Library shadow jumpscare",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            var c=g.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=32000;
            var scaler=g.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;
            backdrop=Sheet("Backdrop");art=null;
            bool modelled=LibraryShadowScareHead.Available;
            if(modelled||face!=null)
            {
                var a=new GameObject("Face",typeof(RectTransform),typeof(UnityEngine.UI.RawImage));a.transform.SetParent(g.transform,false);
                art=a.GetComponent<UnityEngine.UI.RawImage>();art.texture=face;art.raycastTarget=false;art.color=new Color(1,1,1,0);
                var r=art.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(0,-30);
                if(modelled)g.AddComponent<LibraryShadowScareHead>().Build(art);
                else {r.sizeDelta=new Vector2(1000f*face.width/face.height,1000);r.localScale=Vector3.one*.1f;}
            }
            flash=Sheet("Flash");
            return g;
            UnityEngine.UI.Image Sheet(string name)
            {
                var s=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));s.transform.SetParent(g.transform,false);
                var r=s.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                var image=s.GetComponent<UnityEngine.UI.Image>();image.color=Color.black;image.raycastTarget=false;image.enabled=false;return image;
            }
        }
        /// <summary>A scrape of wire, once, each time it comes into view (on screen, unobstructed, near) -- not looped
        /// while you keep looking, but every separate sighting gets it.</summary>
        void Sighting(PlayerInteractor player)
        {
            if(spotted==null)return;
            if(Current==Phase.Gone){LostSight();return;}
            var cam=player.ViewCamera;Vector3 chest=transform.position+Vector3.up*1.4f;Vector3 v=cam.WorldToViewportPoint(chest);
            bool seen=v.z>0&&v.z<SpottedRange&&v.x>.1f&&v.x<.9f&&v.y>.05f&&v.y<.95f&&
                (!Physics.Linecast(cam.transform.position,chest,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(transform)||hit.transform.IsChildOf(player.transform));
            if(seen&&!inView&&Time.time-outOfViewSince>=NewSightingSeconds&&Time.time>=nextSpottedAt)
            {Spotted++;sting.PlayOneShot(spotted,.8f);nextSpottedAt=Time.time+Mathf.Max(SpottedCooldown,spotted.length+1);}
            if(!seen&&inView)outOfViewSince=Time.time;
            inView=seen;
        }
        /// <summary>Whenever the sighting check isn't running (you left the library, a dialogue, a catch), it's out of
        /// sight -- otherwise the next time you see it would count as the same old sighting and stay silent.</summary>
        void LostSight(){if(inView){inView=false;outOfViewSince=Time.time;}}
        void Breathe(bool on)
        {
            if(breath==null||breath.clip==null)return;
            float want=on?(Current==Phase.Hunt?1f:.7f):0;
            breath.volume=Mathf.MoveTowards(breath.volume,want,Time.deltaTime*2);breath.pitch=Current==Phase.Hunt?1.15f:1;
            if(breath.volume>0&&!breath.isPlaying)breath.Play();else if(breath.volume<=0&&breath.isPlaying)breath.Pause();
        }
        /// <summary>Glides along the library floor toward a point; true once there.</summary>
        bool Follow(Vector3 goal,float speed)
        {
            if(Time.time>=repath)
            {
                repath=Time.time+(Current==Phase.Hunt?.25f:1.5f);corner=1;
                if(!NavMesh.SamplePosition(goal,out var hit,1.5f,areaMask)||!NavMesh.CalculatePath(transform.position,hit.position,areaMask,path))path.ClearCorners();
            }
            if(path.corners.Length<=corner)return Flat(goal-transform.position)<.3f||path.corners.Length==0&&Current!=Phase.Hunt;
            Vector3 next=path.corners[corner];
            transform.position=Vector3.MoveTowards(transform.position,next,speed*Time.deltaTime);
            Face(next-transform.position);
            if((transform.position-next).sqrMagnitude<.0025f)corner++;
            return false;
        }
        void Face(Vector3 heading){heading.y=0;if(heading.sqrMagnitude>.0004f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(heading),Time.deltaTime*6);}
        void Whisper(float pitch,float volume){if(voice==null)return;voice.pitch=pitch;voice.PlayOneShot(TempAudio.Whisper,volume);}
        void Place(int index)
        {
            if(alcoves==null||alcoves.Length==0)return;
            target=Mathf.Clamp(index,0,alcoves.Length-1);transform.SetPositionAndRotation(alcoves[target].position,alcoves[target].rotation);path?.ClearCorners();
        }
        void Visual(bool snap)
        {
            float want=Current==Phase.Gone?0:Current==Phase.Hunt?.95f:Current==Phase.Notice?.85f:.7f;
            alpha=snap?want:Mathf.MoveTowards(alpha,want,Time.deltaTime*(Current==Phase.Gone?6:2));
            worldHead?.SetVisible(alpha,Current==Phase.Hunt);
            if(skin!=null){var c=skin.GetColor("_BaseColor");c.a=alpha;skin.SetColor("_BaseColor",c);}
            foreach(var r in silhouette)if(r!=null)r.enabled=alpha>.01f;
            if(animator!=null&&animator.runtimeAnimatorController!=null)animator.SetBool("Hunting",Current==Phase.Hunt);
            if(smoke!=null)
            {
                var emission=smoke.emission;emission.enabled=Current!=Phase.Gone;
                emission.rateOverTimeMultiplier=Current==Phase.Hunt?120:Current==Phase.Notice?85:60;
            }
            // Eyes stay lit (so it can be spotted in the dark) and blink now and then.
            bool blink=Current!=Phase.Hunt&&Mathf.PerlinNoise(Time.time*1.3f,transform.position.x)>.8f;
            Eyes(Current!=Phase.Gone&&!blink,1);
        }
        void Eyes(bool on,float boost)
        {
            foreach(var e in eyes)if(e!=null)e.enabled=on;
            if(eyeColours==null){eyeColours=new Color[eyes.Length];for(int i=0;i<eyes.Length;i++)eyeColours[i]=eyes[i]!=null?eyes[i].sharedMaterial.GetColor("_BaseColor"):Color.white;eyeBlock=new MaterialPropertyBlock();}
            float tone=Mathf.Pow(2f,(LibraryDarkness.Weight-1)*EyeLift)*boost;
            for(int i=0;i<eyes.Length;i++)if(eyes[i]!=null){eyes[i].GetPropertyBlock(eyeBlock);var c=eyeColours[i]*tone;c.a=eyeColours[i].a;eyeBlock.SetColor("_BaseColor",c);eyes[i].SetPropertyBlock(eyeBlock);}
        }
        static float Flat(Vector3 v){v.y=0;return v.magnitude;}
    }
}
