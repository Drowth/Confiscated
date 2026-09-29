using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Confiscated
{
    /// <summary>A pupil hiding in a library-maze alcove. Once Smith spots her she follows, advancing only while unseen,
    /// betrays Smith, then flees.</summary>
    [DisallowMultipleComponent]
    public sealed class CopycatStudent : MonoBehaviour
    {
        const string RevealLine="Now the caretaker knows exactly where you are.";
        const float TriggerDistance=17f,SpotDistance=12f,SpeakDistance=1.75f;
        NavMeshAgent agent;
        SchoolRunController run;
        FirstPersonController movement;
        Renderer artwork;
        Material material;
        MaterialPropertyBlock appearance;
        AudioSource voice,song,giggle;
        float nextGiggle;
        Vector3 lastPosition,fleeTarget;
        float travelled,fleeAt,fleeDeadline;
        bool triggered,speaking,fleeing,completed,watched,hiding;

        public bool Triggered=>triggered;
        public bool Speaking=>speaking;
        public bool Fleeing=>fleeing;
        public bool Completed=>completed;
        public bool FrozenByGaze=>watched&&triggered&&!fleeing;
        public int RevealCount{get;private set;}
        /// <summary>Waiting in a library alcove until the player sees her.</summary>
        public bool Hiding=>hiding&&!triggered;

        public static void Install(SchoolRunController owner)
        {
            if(owner==null||FindFirstObjectByType<CopycatStudent>()!=null)return;
            bool hidden=HidingPlace(out var intended,out var facing);
            if(!hidden){intended=new(-5.2f,0,51.4f);facing=Quaternion.Euler(0,180,0);}
            if(!NavMesh.SamplePosition(intended,out var floor,5f,NavMesh.AllAreas))return;
            var root=new GameObject("Copycat pupil");root.transform.SetPositionAndRotation(floor.position,facing);
            root.SetActive(false);
            var nav=root.AddComponent<NavMeshAgent>();nav.radius=.22f;nav.height=1.55f;nav.baseOffset=0;
            nav.speed=2.75f;nav.acceleration=18;nav.angularSpeed=720;nav.stoppingDistance=1.2f;nav.autoBraking=false;
            var body=root.AddComponent<CapsuleCollider>();body.center=new Vector3(0,.78f,0);body.height=1.55f;body.radius=.22f;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Quad);visual.name="Copycat artwork";visual.transform.SetParent(root.transform,false);
            visual.transform.localPosition=new Vector3(0,.93f,0);visual.transform.localScale=new Vector3(1.12f,1.86f,1);
            Destroy(visual.GetComponent<Collider>());
            var copycat=root.AddComponent<CopycatStudent>();copycat.run=owner;copycat.artwork=visual.GetComponent<MeshRenderer>();copycat.hiding=hidden;
            copycat.artwork.shadowCastingMode=ShadowCastingMode.On;
            root.SetActive(true);
        }

        /// <summary>A random dead end of the library maze (the shadow's alcoves), not the glue's, facing its open side.</summary>
        static bool HidingPlace(out Vector3 position,out Quaternion facing)
        {
            position=default;facing=Quaternion.identity;
            var shadow=FindFirstObjectByType<LibraryShadow>(FindObjectsInactive.Include);
            if(shadow==null||shadow.alcoves==null)return false;
            var glue=FindFirstObjectByType<GluePickup>(FindObjectsInactive.Include);
            var options=new System.Collections.Generic.List<Transform>();
            foreach(var alcove in shadow.alcoves)
                if(alcove!=null&&(glue==null||Vector3.Distance(alcove.position,glue.transform.position)>3))options.Add(alcove);
            if(options.Count==0)return false;
            position=options[Random.Range(0,options.Count)].position;
            float best=-1;
            for(int i=0;i<8;i++)
            {
                Vector3 direction=Quaternion.Euler(0,i*45,0)*Vector3.forward;
                float open=Physics.Raycast(position+Vector3.up,direction,out var hit,8,~0,QueryTriggerInteraction.Ignore)?hit.distance:8;
                // Only open floor inside the library counts: an alcove by an outer wall must not face out through a gap in it.
                float inside=0;while(inside<open&&shadow.Inside(position+direction*(inside+.25f)))inside+=.25f;open=inside;
                if(open>best){best=open;facing=Quaternion.LookRotation(direction);}
            }
            return true;
        }

        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();
            if(run==null)run=SchoolRunController.Instance;
            var texture=Resources.Load<Texture2D>("Art/CopycatWalk");
            var shader=Shader.Find("Confiscated/Character Cutout");
            if(artwork==null)artwork=GetComponentInChildren<MeshRenderer>();
            if(texture!=null&&shader!=null&&artwork!=null)
            {
                material=new Material(shader);material.name="Copycat pupil (runtime)";
                material.SetTexture("_BaseMap",texture);material.SetFloat("_Cutoff",.35f);
                artwork.sharedMaterial=material;
            }
            appearance=new MaterialPropertyBlock();
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=1;
            voice.minDistance=3;voice.maxDistance=45;voice.rolloffMode=AudioRolloffMode.Linear;voice.volume=1;
            voice.clip=Resources.Load<AudioClip>("Audio/CopycatReveal");
            // Hidden in the maze she sings to herself: a lure you hear between the shelves before you see her.
            song=gameObject.AddComponent<AudioSource>();song.playOnAwake=false;song.loop=true;song.spatialBlend=1;song.dopplerLevel=0;
            song.minDistance=2;song.maxDistance=20;song.rolloffMode=AudioRolloffMode.Linear;song.volume=.75f;
            song.clip=Resources.Load<AudioClip>("Audio/CopycatRingAroundTheRosie");
            // Following, she giggles now and then, smeared with echo and reverb so it seems to come from the whole corridor.
            var laugh=new GameObject("Copycat giggle");laugh.transform.SetParent(transform,false);laugh.transform.localPosition=Vector3.up*1.2f;
            giggle=laugh.AddComponent<AudioSource>();giggle.playOnAwake=false;giggle.spatialBlend=.85f;giggle.dopplerLevel=0;
            giggle.minDistance=2.5f;giggle.maxDistance=28;giggle.rolloffMode=AudioRolloffMode.Linear;giggle.volume=.9f;
            giggle.clip=Resources.Load<AudioClip>("Audio/CopycatGiggle");
            var echo=laugh.AddComponent<AudioEchoFilter>();echo.delay=260;echo.decayRatio=.55f;echo.wetMix=.7f;echo.dryMix=.85f;
            var reverb=laugh.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.Hallway;
            lastPosition=transform.position;
        }

        void Update()
        {
            if(completed||run==null||run.period==null||run.period.Player==null)return;
            Sing(Hiding&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying&&run.RoundStarted&&!ComicDialogue.IsActive&&Time.timeScale>0);
            if(GameManager.Instance==null||!GameManager.Instance.IsPlaying||!run.RoundStarted||ComicDialogue.IsActive||Time.timeScale<=0)
            {StopAgent();return;}
            if(movement==null)movement=run.period.Player.GetComponent<FirstPersonController>();
            Transform player=run.period.Player.transform;
            if(fleeing){Flee();Animate();return;}
            if(speaking)
            {
                StopAgent();Face(player.position);
                if(Time.time>=fleeAt)BeginFlee(player.position);
                Animate();return;
            }
            Vector3 delta=player.position-transform.position;delta.y=0;
            if(!triggered)
            {
                StopAgent();
                if(hiding?delta.magnitude<=SpotDistance&&LibraryWindow.InLibrary(player.position)&&PlayerCanSeeMe():
                    delta.magnitude<=TriggerDistance&&Vector3.Dot(transform.forward,delta.normalized)>.25f&&ClearView(player))
                {
                    triggered=true;
                    if(hiding)PlayerThoughts.Instance?.Think("Someone else is in here... hiding in the shelves.");
                }
                Animate();return;
            }
            if(delta.magnitude<=SpeakDistance){Reveal(player);Animate();return;}
            watched=PlayerCanSeeMe();
            if(watched||movement==null||movement.MovementLocked||movement.IsFallen)
            {
                StopAgent();Face(player.position);
            }
            else if(agent.isOnNavMesh)
            {
                Giggle();
                agent.isStopped=false;
                float copiedSpeed=movement.Controller!=null?movement.Controller.velocity.magnitude:0;
                agent.speed=Mathf.Clamp(copiedSpeed+.55f,1.35f,4.8f);
                agent.SetDestination(player.position);
            }
            Animate();
        }

        bool ClearView(Transform player)
        {
            Vector3 from=transform.position+Vector3.up*1.15f,to=player.position+Vector3.up*1.1f;
            if(!Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore))return true;
            return hit.transform==player||hit.transform.IsChildOf(player);
        }

        bool PlayerCanSeeMe()
        {
            var camera=Camera.main;if(camera==null||artwork==null)return false;
            Vector3 point=artwork.bounds.center,screen=camera.WorldToViewportPoint(point);
            if(screen.z<=0||screen.x<-.03f||screen.x>1.03f||screen.y<-.03f||screen.y>1.03f)return false;
            if(Physics.Linecast(camera.transform.position,point,out var hit,~0,QueryTriggerInteraction.Ignore))
                return hit.transform==transform||hit.transform.IsChildOf(transform);
            return true;
        }

        void Reveal(Transform player)
        {
            if(speaking)return;
            speaking=true;watched=false;RevealCount++;StopAgent();Face(player.position);if(giggle!=null)giggle.Stop();
            if(voice.clip!=null)voice.Play();
            float duration=voice.clip!=null?voice.clip.length:5f;
            HudController.Instance?.SetBark("Copycat: "+RevealLine,duration+.5f);
            // This is deliberately school-wide: hiding at the far end cannot stop her betraying Smith.
            NoiseEvents.Emit(transform.position,1000f,"copycat");
            run.caretaker?.HearSchoolWideAlarm(transform.position);
            fleeAt=Time.time+duration;
        }

        void BeginFlee(Vector3 playerPosition)
        {
            speaking=false;fleeing=true;watched=false;fleeDeadline=Time.time+14;
            Vector3 away=transform.position-playerPosition;away.y=0;if(away.sqrMagnitude<.1f)away=transform.forward;away.Normalize();
            fleeTarget=transform.position;
            float best=-1;
            foreach(Vector3 direction in new[]{away,Quaternion.Euler(0,55,0)*away,Quaternion.Euler(0,-55,0)*away,-away})
            {
                if(!NavMesh.SamplePosition(transform.position+direction*24,out var hit,8,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(transform.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float score=Vector3.Distance(hit.position,playerPosition);if(score>best){best=score;fleeTarget=hit.position;}
            }
            if(agent.isOnNavMesh){agent.speed=6.2f;agent.acceleration=25;agent.stoppingDistance=.3f;agent.isStopped=false;agent.SetDestination(fleeTarget);}
        }

        void Flee()
        {
            if(!agent.isOnNavMesh||Time.time>=fleeDeadline||!agent.pathPending&&agent.remainingDistance<=.45f)
            {
                completed=true;fleeing=false;StopAgent();if(artwork!=null)artwork.enabled=false;
                foreach(var collider in GetComponents<Collider>())collider.enabled=false;
            }
        }

        // Pauses rather than stops for a dialogue balloon; spotting her cuts the song dead.
        void Sing(bool on)
        {
            if(song==null||song.clip==null)return;
            if(on){if(!song.isPlaying){if(song.time>0)song.UnPause();else song.Play();}}
            else if(song.isPlaying){if(Hiding)song.Pause();else song.Stop();}
        }
        public bool Singing=>song!=null&&song.isPlaying;
        public int Giggles{get;private set;}
        /// <summary>Every 6-11 s while she creeps after Smith (the first soon after she is spotted), never over her reveal.</summary>
        void Giggle()
        {
            if(giggle==null||giggle.clip==null)return;
            if(nextGiggle==0){nextGiggle=Time.time+Random.Range(1.5f,3f);return;}
            if(Time.time<nextGiggle||giggle.isPlaying)return;
            nextGiggle=Time.time+Random.Range(6f,11f);giggle.pitch=Random.Range(.88f,1.02f);giggle.Play();Giggles++;
        }
        void StopAgent(){if(agent!=null&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}}
        void Face(Vector3 point){Vector3 direction=point-transform.position;direction.y=0;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);}
        void Animate()
        {
            if(artwork==null||appearance==null)return;
            Vector3 moved=transform.position-lastPosition;lastPosition=transform.position;moved.y=0;
            if(moved.magnitude>.001f)travelled+=moved.magnitude;
            int step=Mathf.FloorToInt(travelled/.65f)&1;
            artwork.GetPropertyBlock(appearance);appearance.SetVector("_BaseMap_ST",new Vector4(.5f,1,step*.5f,0));artwork.SetPropertyBlock(appearance);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
