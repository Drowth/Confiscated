using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Confiscated
{
    /// <summary>Shared, player-view comic close-ups. Unscaled reading time never consumes gameplay deadlines.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class ComicDialogue : MonoBehaviour
    {
        struct Line { public string speaker,text; public Transform actor; public float automaticSeconds; }
        static ComicDialogue instance;
        public static bool IsActive=>instance!=null&&instance.active;
        public static ComicDialogue Instance=>instance;
        public bool IsTyping=>active&&shown<current.text.Length;
        public string Speaker=>current.speaker;
        public string VisibleText=>words!=null?words.text:"";
        public string FullText=>current.text;
        public Transform Actor=>active?current.actor:null;
        public bool IsSpeaking=>IsTyping&&shown>0&&letters>=0;
        readonly Queue<Line> queue=new();
        readonly List<Canvas> hiddenCanvases=new();
        Canvas canvas;
        Text words,nameTag,advance,regularWords,phoneWords,thoughtWords;
        GameObject regularBalloon,phoneNotification,thoughtBalloon;
        PlayerInteractor player;
        FirstPersonController movement;
        Camera view;
        UniversalAdditionalCameraData cameraData;
        Volume volume;
        DepthOfField focus;
        VolumeProfile profile;
        bool active,oldCursor,oldHold,oldPost,oldDepth,oldEvents;
        EventSystem events;
        CursorLockMode cursorMode;
        LayerMask oldVolumeMask;
        float oldTime,oldFov,started,letters;
        int shown,startedFrame;
        Vector3 oldPosition;
        Quaternion oldRotation,fromRotation,targetRotation;
        float fromFov,targetFov;
        Line current;
        AudioSource lineVoice;
        bool hasRecording;
        /// <summary>Who the current line belongs to in the world (resolved from the speaker name), and the recording it is playing, for TalkingMouth.</summary>
        public Transform SpeakerTransform {get;private set;}
        public static bool IsAddressingPlayer(Transform actor) => IsActive &&
            instance.current.speaker != "PHONE" && instance.SpeakerTransform != null &&
            (instance.SpeakerTransform == actor || instance.SpeakerTransform.IsChildOf(actor));

        void FacePlayer()
        {
            if (SpeakerTransform == null || view == null || current.speaker == "PHONE") return;
            Vector3 direction = view.transform.position - SpeakerTransform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
                SpeakerTransform.rotation = Quaternion.LookRotation(direction);
        }
        public AudioSource LineVoice=>lineVoice;
        public bool IsVoicePlaying=>lineVoice!=null&&lineVoice.isPlaying;
        // Written line -> recording in Resources/Audio. A matching line plays its clip;
        // the text must match the string the game speaks exactly, so change both together.
        static readonly Dictionary<string,string> recordedLines=new()
        {
            ["Master Smith. Is that… a phone?"]="ReedPhone",
            ["You know the rules, and this isn't the first time. It will be with the caretaker until the end of term."]="ReedNoPhones",
            ["Well, since you're clearly not busy… newsletters. Tray outside the caretaker's office. And be quick about it."]="ReedNewsletters",
            ["Smith, why aren't you in class? Show me your hall pass."]="CaretakerHallPass",
            ["Mr Reed's delivery? Straight to the tray and back. No other rooms, Smith."]="CaretakerDelivery",
        };

        // A short compulsory conversation uses the same camera/input ownership as other dialogue,
        // but finishes itself. Normal teacher/phone dialogue retains its manual advance behaviour.
        public static bool TrySpeakTimed(Transform actor,string speaker,string text,float seconds)
        {
            if(actor==null||string.IsNullOrWhiteSpace(text)||IsActive)return false;
            if(instance==null)instance=new GameObject("Comic dialogue").AddComponent<ComicDialogue>();
            instance.queue.Enqueue(new Line{actor=actor,speaker=speaker,text=text,automaticSeconds=Mathf.Max(1,seconds)});
            instance.Begin();
            return instance.active;
        }

        public static bool TrySpeak(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return false;
            value=DarkModeDialogue.Resolve(value);
            var parsed=new List<Line>();
            foreach(string paragraph in value.Split('\n'))
            {
                string s=paragraph.Trim();if(s.Length==0)continue;
                int split=s.IndexOf(':');
                string who=split>=0?s.Substring(0,split):"";
                if(who=="Mr Reed"||who=="Caretaker"||who=="Miss D Tenison"||who=="PHONE"||who=="Smith")
                    parsed.Add(new Line{speaker=who,text=s.Substring(split+1).Trim()});
                else if(parsed.Count>0){var line=parsed[parsed.Count-1];line.text+="\n"+s;parsed[parsed.Count-1]=line;}
                else return false;
            }
            if(parsed.Count==0)return false;
            if(instance==null)instance=new GameObject("Comic dialogue").AddComponent<ComicDialogue>();
            foreach(var line in parsed)instance.queue.Enqueue(line);
            if(!instance.active)instance.Begin();
            return true;
        }
        void Begin()
        {
            player=FindFirstObjectByType<PlayerInteractor>();if(player==null){queue.Clear();return;}
            movement=player.GetComponent<FirstPersonController>();view=player.ViewCamera;
            // Up off the floor before the camera pose is saved below (a slip then a pass check left players lying down for good).
            if(movement!=null)movement.EndFall();
            if(canvas==null)Build();
            oldTime=Time.timeScale;oldCursor=Cursor.visible;cursorMode=Cursor.lockState;
            oldPosition=view.transform.localPosition;oldRotation=view.transform.localRotation;oldFov=view.fieldOfView;
            cameraData=view.GetUniversalAdditionalCameraData();oldPost=cameraData.renderPostProcessing;oldDepth=cameraData.requiresDepthTexture;oldVolumeMask=cameraData.volumeLayerMask;
            cameraData.renderPostProcessing=true;cameraData.requiresDepthTexture=true;cameraData.volumeLayerMask=oldVolumeMask.value|1;
            oldHold=player.HoldAnchor!=null&&player.HoldAnchor.gameObject.activeSelf;
            if(player.HoldAnchor!=null)player.HoldAnchor.gameObject.SetActive(false);
            events=EventSystem.current;oldEvents=events!=null&&events.enabled;if(events!=null)events.enabled=false;
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            active=true;canvas.enabled=true;volume.enabled=true;
            Next();
        }
        void Next()
        {
            StopVoice();
            if(queue.Count==0){Finish();return;}
            current=queue.Dequeue();shown=0;letters=0;
            hasRecording=false;
            // Chatterbox owns its timed speech channel and mouth animation.
            var contextual=current.speaker=="Chatterbox"?null:DarkModeDialogue.Recording(current.text.Trim());
            if(lineVoice!=null&&contextual!=null){lineVoice.clip=contextual;lineVoice.Play();hasRecording=true;}
            if(!hasRecording&&lineVoice!=null&&recordedLines.TryGetValue(current.text.Trim(),out var clipName))
            {
                var recording=Resources.Load<AudioClip>("Audio/"+clipName);
                if(recording!=null){lineVoice.clip=recording;lineVoice.Play();hasRecording=true;}
            }
            bool isPhone=current.speaker=="PHONE";
            // Smith is the player: his lines are never said aloud, so they show as a thought cloud.
            bool isThought=current.speaker=="Smith";
            regularBalloon.SetActive(!isPhone&&!isThought);phoneNotification.SetActive(isPhone);thoughtBalloon.SetActive(isThought);
            words=isPhone?phoneWords:isThought?thoughtWords:regularWords;words.text="";nameTag.text=current.speaker.ToUpperInvariant();
            started=Time.unscaledTime;startedFrame=Time.frameCount;fromRotation=view.transform.rotation;fromFov=view.fieldOfView;
            Transform actor=current.actor;var gm=GameManager.Instance;
            if(current.speaker=="Mr Reed")actor=gm?.schoolPeriod?.teacher?.transform;
            else if(current.speaker=="Caretaker")actor=gm?.schoolPeriod?.caretaker?.transform??FindFirstObjectByType<CaretakerAI>()?.transform;
            else if(current.speaker=="Miss D Tenison")actor=gm?.detention?.teacher?.transform;
            else if(current.speaker=="PHONE")actor=gm?.schoolPeriod?.phoneProp?.transform;
            SpeakerTransform=actor;
            FacePlayer();
            if(actor!=null)
            {
                Vector3 point=actor.position+Vector3.up*1.78f;
                if(current.speaker=="PHONE")point=actor.position;
                else foreach(var renderer in actor.GetComponentsInChildren<Renderer>())
                    if(renderer.sharedMaterial!=null&&(renderer.sharedMaterial.shader.name=="Confiscated/Character Cutout"||renderer.sharedMaterial.shader.name=="Confiscated/Chatterbox Cutout"))
                    {point=new Vector3(renderer.bounds.center.x,renderer.bounds.min.y+renderer.bounds.size.y*.86f,renderer.bounds.center.z);break;}
                float distance=Vector3.Distance(view.transform.position,point);
                Vector3 aim=point-Vector3.up*(current.speaker=="PHONE"?0:.07f);
                targetRotation=Quaternion.LookRotation(aim-view.transform.position);
                float span=current.speaker=="PHONE" ? .46f : 1.12f;
                targetFov=Mathf.Clamp(2*Mathf.Atan(span/(2*Mathf.Max(.2f,distance)))*Mathf.Rad2Deg,2,65);
                focus.focusDistance.Override(distance);focus.gaussianStart.Override(distance+.2f);focus.gaussianEnd.Override(distance+1f);focus.active=true;
            }
            else {targetRotation=fromRotation;targetFov=fromFov;focus.active=false;}
            advance.text=current.automaticSeconds>0?"":isPhone?"[OPEN  •  CLICK / SPACE]":"[Click / Space]";
        }
        void Update()
        {
            if(!active)return;
            if(player==null||view==null){Finish();return;}
            float elapsed=Time.unscaledTime-started;
            if(current.automaticSeconds>0&&current.actor==null){Finish();return;}
            if(elapsed>.48f&&shown<current.text.Length)
            {
                letters+=Time.unscaledDeltaTime*34;
                while(letters>=1&&shown<current.text.Length)
                {
                    letters-=1;char letter=current.text[shown++];
                    if(letter=='.'||letter=='?'||letter=='!')letters-=6;
                    else if(letter==',')letters-=3;
                }
                words.text=current.text.Substring(0,shown);
            }
            if(current.automaticSeconds>0)
            {
                if(elapsed>=current.automaticSeconds&&!IsTyping)Next();
                return;
            }
            var kb=Keyboard.current;var mouse=Mouse.current;var pad=Gamepad.current;
            if(Time.frameCount>startedFrame&&elapsed>.2f&&
               ((mouse!=null&&mouse.leftButton.wasPressedThisFrame)||(kb!=null&&(kb.spaceKey.wasPressedThisFrame||kb.enterKey.wasPressedThisFrame||kb.fKey.wasPressedThisFrame))||(pad!=null&&pad.buttonSouth.wasPressedThisFrame)))Advance();
        }
        public void Advance()
        {
            if(!active||current.automaticSeconds>0)return;
            // Skipping the typing keeps a recording playing; moving to the next line (or closing) cuts it.
            if(IsTyping){if(!hasRecording)StopVoice();shown=current.text.Length;words.text=current.text;return;}
            StopVoice();
            Next();
        }
        void LateUpdate()
        {
            if(!active)return;
            FacePlayer();
            // Modal screens can be opened later in the same frame as a spoken line. Suspend their display too.
            foreach(var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(other!=canvas&&other.enabled&&other.renderMode!=RenderMode.WorldSpace){hiddenCanvases.Add(other);other.enabled=false;}
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-started)/.55f));
            view.transform.rotation=Quaternion.Slerp(fromRotation,targetRotation,blend);view.fieldOfView=Mathf.Lerp(fromFov,targetFov,blend);
            if(phoneNotification.activeSelf)
            {
                float pop=Mathf.Clamp01((Time.unscaledTime-started)/.24f);
                phoneNotification.transform.localScale=Vector3.one*Mathf.Lerp(1.12f,1f,Mathf.SmoothStep(0,1,pop));
            }
        }
        void StopVoice(){if(lineVoice!=null)lineVoice.Stop();}
        void Finish()
        {
            StopVoice();
            if(!active)return;active=false;queue.Clear();
            if(canvas!=null)canvas.enabled=false;if(volume!=null)volume.enabled=false;
            foreach(var other in hiddenCanvases)if(other!=null)other.enabled=true;hiddenCanvases.Clear();
            if(view!=null){view.transform.localPosition=oldPosition;view.transform.localRotation=oldRotation;view.fieldOfView=oldFov;}
            if(cameraData!=null){cameraData.renderPostProcessing=oldPost;cameraData.requiresDepthTexture=oldDepth;cameraData.volumeLayerMask=oldVolumeMask;}
            if(events!=null)events.enabled=oldEvents;
            if(player!=null){player.SuppressActionsThisFrame();if(player.HoldAnchor!=null)player.HoldAnchor.gameObject.SetActive(oldHold);}
            // A modal screen can open after dialogue starts. Its live cursor ownership wins over the old snapshot.
            var gm=GameManager.Instance;
            bool modal=gm!=null&&((gm.lockerUI!=null&&gm.lockerUI.IsOpen)||(gm.detention!=null&&gm.detention.MinigameOpen));
            Cursor.lockState=modal?CursorLockMode.None:cursorMode;Cursor.visible=modal||oldCursor;Time.timeScale=oldTime;
        }
        public static void Cancel(){if(instance!=null)instance.Finish();}
        void OnDisable(){Finish();}
        void OnDestroy(){Finish();if(profile!=null)Destroy(profile);if(instance==this)instance=null;}
        void Build()
        {
            lineVoice=SchoolAudio.Create(gameObject,SchoolAudio.Channel.Voice,true);lineVoice.playOnAwake=false;lineVoice.loop=false;lineVoice.spatialBlend=0;lineVoice.ignoreListenerPause=true;

            var g=new GameObject("Comic dialogue canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));g.transform.SetParent(transform,false);
            canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
            var scale=g.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,1000);scale.matchWidthOrHeight=.5f;
            Panel("Top cinematic bar",new Vector2(0,.91f),Vector2.one,Color.black);
            Panel("Bottom cinematic bar",Vector2.zero,new Vector2(1,.09f),Color.black);
            regularBalloon=new GameObject("Spoken dialogue",typeof(RectTransform));regularBalloon.transform.SetParent(canvas.transform,false);
            var regularRect=regularBalloon.GetComponent<RectTransform>();regularRect.anchorMin=Vector2.zero;regularRect.anchorMax=Vector2.one;regularRect.offsetMin=regularRect.offsetMax=Vector2.zero;
            var bubble=Panel("Speech balloon",new Vector2(.10f,.115f),new Vector2(.90f,.32f),new Color(.99f,.985f,.96f),regularBalloon.transform);
            var edge=new GameObject("Pencil balloon outline",typeof(RectTransform),typeof(SketchBorder));edge.transform.SetParent(bubble,false);var er=edge.GetComponent<RectTransform>();er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=er.offsetMax=Vector2.zero;edge.GetComponent<SketchBorder>().color=new Color(.07f,.10f,.16f);edge.GetComponent<SketchBorder>().raycastTarget=false;
            var tail=new GameObject("Balloon tail",typeof(RectTransform),typeof(ComicBalloonTail));tail.transform.SetParent(regularBalloon.transform,false);var tr=tail.GetComponent<RectTransform>();tr.anchorMin=new Vector2(.34f,.318f);tr.anchorMax=new Vector2(.42f,.385f);tr.offsetMin=tr.offsetMax=Vector2.zero;tail.GetComponent<ComicBalloonTail>().raycastTarget=false;
            var tag=Panel("Speaker label",new Vector2(.12f,.325f),new Vector2(.34f,.37f),new Color(.93f,.83f,.55f),regularBalloon.transform);
            nameTag=Label(tag,"Speaker",26,TextAnchor.MiddleCenter,new Vector2(10,2));nameTag.fontStyle=FontStyle.Bold;nameTag.resizeTextForBestFit=true;nameTag.resizeTextMinSize=18;nameTag.resizeTextMaxSize=26;
            regularWords=Label(bubble,"Spoken words",31,TextAnchor.MiddleLeft,new Vector2(34,18));regularWords.supportRichText=false;
            BuildPhoneNotification();
            BuildThought();
            var control=Panel("Advance key",new Vector2(.72f,.01f),new Vector2(.94f,.075f),Color.clear);
            advance=Label(control,"Advance",19,TextAnchor.MiddleRight,Vector2.zero);advance.color=Color.white;
            var vg=new GameObject("Dialogue focus");vg.transform.SetParent(transform,false);volume=vg.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;focus=profile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Gaussian);focus.gaussianMaxRadius.Override(1.5f);focus.highQualitySampling.Override(true);
            volume.enabled=false;canvas.enabled=false;
        }
        void BuildPhoneNotification()
        {
            phoneNotification=new GameObject("Jagged phone notification",typeof(RectTransform));phoneNotification.transform.SetParent(canvas.transform,false);
            var group=phoneNotification.GetComponent<RectTransform>();group.anchorMin=new Vector2(.145f,.13f);group.anchorMax=new Vector2(.855f,.355f);group.offsetMin=group.offsetMax=Vector2.zero;
            group.localRotation=Quaternion.Euler(0,0,-.55f);

            var shadow=new GameObject("Offset ink shadow",typeof(RectTransform),typeof(JaggedComicPanel));shadow.transform.SetParent(group,false);
            var sr=shadow.GetComponent<RectTransform>();sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=new Vector2(9,-11);sr.offsetMax=new Vector2(9,-11);
            var shadowArt=shadow.GetComponent<JaggedComicPanel>();shadowArt.color=new Color(.025f,.035f,.055f,.72f);shadowArt.edgeColor=Color.clear;shadowArt.raycastTarget=false;

            var card=new GameObject("Warm paper burst",typeof(RectTransform),typeof(JaggedComicPanel));card.transform.SetParent(group,false);
            var cr=card.GetComponent<RectTransform>();cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;
            var cardArt=card.GetComponent<JaggedComicPanel>();cardArt.color=new Color(1f,.965f,.79f);cardArt.edgeColor=new Color(.055f,.075f,.11f);cardArt.edgeWidth=7;cardArt.raycastTarget=false;

            var badge=new GameObject("New message badge",typeof(RectTransform),typeof(JaggedComicPanel));badge.transform.SetParent(group,false);
            var br=badge.GetComponent<RectTransform>();br.anchorMin=new Vector2(.035f,.77f);br.anchorMax=new Vector2(.31f,1.08f);br.offsetMin=br.offsetMax=Vector2.zero;br.localRotation=Quaternion.Euler(0,0,1.5f);
            var badgeArt=badge.GetComponent<JaggedComicPanel>();badgeArt.color=new Color(.94f,.69f,.20f);badgeArt.edgeColor=new Color(.055f,.075f,.11f);badgeArt.edgeWidth=5;badgeArt.raycastTarget=false;
            var heading=Label(br,"New message",25,TextAnchor.MiddleCenter,new Vector2(15,3));heading.text="NEW MESSAGE";heading.fontStyle=FontStyle.Bold;

            phoneWords=Label(cr,"Phone message",35,TextAnchor.MiddleLeft,new Vector2(65,24));phoneWords.supportRichText=false;
            var buzz=Label(cr,"Buzz",24,TextAnchor.UpperRight,new Vector2(25,-12));buzz.text="BZZT!";buzz.fontStyle=FontStyle.Bold;buzz.color=new Color(.54f,.12f,.10f);buzz.rectTransform.localRotation=Quaternion.Euler(0,0,5);
            phoneNotification.SetActive(false);
        }
        void BuildThought()
        {
            thoughtBalloon=new GameObject("Thought cloud",typeof(RectTransform));thoughtBalloon.transform.SetParent(canvas.transform,false);
            var group=thoughtBalloon.GetComponent<RectTransform>();group.anchorMin=Vector2.zero;group.anchorMax=Vector2.one;group.offsetMin=group.offsetMax=Vector2.zero;
            var cloud=new GameObject("Cloud",typeof(RectTransform),typeof(ThoughtCloud));cloud.transform.SetParent(group,false);
            var cr=cloud.GetComponent<RectTransform>();cr.anchorMin=new Vector2(.16f,.125f);cr.anchorMax=new Vector2(.84f,.34f);cr.offsetMin=cr.offsetMax=Vector2.zero;
            cloud.GetComponent<ThoughtCloud>().raycastTarget=false;
            // Bubbles trailing off the bottom of the screen, back to the player's own head.
            var trail=new GameObject("Thought bubbles",typeof(RectTransform),typeof(ThoughtCloud));trail.transform.SetParent(group,false);
            var tr=trail.GetComponent<RectTransform>();tr.anchorMin=new Vector2(.15f,.02f);tr.anchorMax=new Vector2(.29f,.15f);tr.offsetMin=tr.offsetMax=Vector2.zero;
            var dots=trail.GetComponent<ThoughtCloud>();dots.trail=true;dots.raycastTarget=false;
            thoughtWords=Label(cr,"Thought words",31,TextAnchor.MiddleCenter,new Vector2(70,30));thoughtWords.supportRichText=false;
            thoughtWords.fontStyle=FontStyle.Italic;thoughtWords.color=new Color(.10f,.13f,.22f);
            thoughtBalloon.SetActive(false);
        }
        RectTransform Panel(string name,Vector2 min,Vector2 max,Color colour,Transform parent=null)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent!=null?parent:canvas.transform,false);var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;g.GetComponent<Image>().color=colour;return r;
        }
        static Text Label(RectTransform parent,string name,int size,TextAnchor alignment,Vector2 padding)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=padding;r.offsetMax=-padding;
            var t=g.GetComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=size;t.color=new Color(.025f,.025f,.025f);t.alignment=alignment;t.raycastTarget=false;return t;
        }
    }

    /// <summary>A deliberately uneven, inked notification card drawn without a texture.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JaggedComicPanel:MaskableGraphic
    {
        public Color edgeColor=new(.055f,.075f,.11f);
        public float edgeWidth=6;
        static readonly Vector2[] Shape={
            new(0,.08f),new(.035f,0),new(.15f,.025f),new(.24f,0),new(.36f,.02f),new(.48f,0),new(.61f,.025f),new(.74f,0),new(.88f,.022f),new(.97f,0),new(1,.10f),
            new(.982f,.25f),new(1,.39f),new(.978f,.53f),new(1,.68f),new(.982f,.83f),new(1,1),new(.87f,.975f),new(.74f,1),new(.61f,.978f),new(.48f,1),new(.35f,.976f),new(.22f,1),new(.09f,.974f),new(0,1),
            new(.02f,.84f),new(0,.68f),new(.022f,.52f),new(0,.36f),new(.02f,.21f)};
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;var center=r.center;int n=Shape.Length;
            vh.AddVert(center,color,Vector2.one*.5f);
            for(int i=0;i<n;i++)
            {
                Vector2 p=new(Mathf.Lerp(r.xMin,r.xMax,Shape[i].x),Mathf.Lerp(r.yMin,r.yMax,Shape[i].y));
                vh.AddVert(p,color,Shape[i]);
            }
            for(int i=0;i<n;i++)vh.AddTriangle(0,i+1,(i+1)%n+1);
            if(edgeColor.a<=0||edgeWidth<=0)return;
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;Vector2 a=new(Mathf.Lerp(r.xMin,r.xMax,Shape[i].x),Mathf.Lerp(r.yMin,r.yMax,Shape[i].y));
                Vector2 b=new(Mathf.Lerp(r.xMin,r.xMax,Shape[j].x),Mathf.Lerp(r.yMin,r.yMax,Shape[j].y));
                Vector2 ai=Vector2.MoveTowards(a,center,edgeWidth),bi=Vector2.MoveTowards(b,center,edgeWidth);int v=vh.currentVertCount;
                vh.AddVert(a,edgeColor,Vector2.zero);vh.AddVert(b,edgeColor,Vector2.zero);vh.AddVert(bi,edgeColor,Vector2.zero);vh.AddVert(ai,edgeColor,Vector2.zero);
                vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v,v+2,v+3);
            }
        }
    }

    /// <summary>A scalloped, inked thought cloud (or, with trail set, the shrinking bubbles that lead to the thinker).
    /// Drawn as overlapping discs: ink discs first, paper discs slightly smaller on top, so the outline dips into each cusp.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThoughtCloud:MaskableGraphic
    {
        public bool trail;
        static readonly Color Ink=new(.07f,.10f,.16f),Paper=new(.99f,.985f,.96f);
        const float Edge=5;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            if(trail)
            {
                // Three bubbles, largest nearest the cloud (top right), smallest off towards the player (bottom left).
                float unit=Mathf.Min(r.width,r.height);
                var bubbles=new[]{(new Vector2(.74f,.74f),.26f),(new Vector2(.42f,.40f),.17f),(new Vector2(.16f,.14f),.10f)};
                foreach(var (at,size) in bubbles){var c=new Vector2(Mathf.Lerp(r.xMin,r.xMax,at.x),Mathf.Lerp(r.yMin,r.yMax,at.y));Disc(vh,c,unit*size,unit*size*.85f,Ink);}
                foreach(var (at,size) in bubbles){var c=new Vector2(Mathf.Lerp(r.xMin,r.xMax,at.x),Mathf.Lerp(r.yMin,r.yMax,at.y));Disc(vh,c,unit*size-Edge,unit*size*.85f-Edge,Paper);}
                return;
            }
            float bump=Mathf.Min(r.height*.36f,72);
            var inner=new Rect(r.xMin+bump,r.yMin+bump,r.width-2*bump,r.height-2*bump);
            var centres=new System.Collections.Generic.List<Vector2>();
            float perimeter=2*(inner.width+inner.height);int count=Mathf.Max(8,Mathf.RoundToInt(perimeter/(bump*1.2f)));
            for(int i=0;i<count;i++)
            {
                float d=perimeter*i/count;Vector2 p;
                if(d<inner.width)p=new(inner.xMin+d,inner.yMin);
                else if((d-=inner.width)<inner.height)p=new(inner.xMax,inner.yMin+d);
                else if((d-=inner.height)<inner.width)p=new(inner.xMax-d,inner.yMax);
                else p=new(inner.xMin,inner.yMax-(d-inner.width));
                centres.Add(p);
            }
            // Uneven bump sizes, so it reads as drawn rather than stamped.
            float Size(int i)=>bump*(i%3==1?.8f:i%3==2?.93f:1);
            for(int i=0;i<centres.Count;i++)Disc(vh,centres[i],Size(i),Size(i),Ink);
            Quad(vh,inner,Ink);
            for(int i=0;i<centres.Count;i++)Disc(vh,centres[i],Size(i)-Edge,Size(i)-Edge,Paper);
            Quad(vh,inner,Paper);
        }
        static void Disc(VertexHelper vh,Vector2 c,float rx,float ry,Color tint)
        {
            const int Segments=28;int start=vh.currentVertCount;vh.AddVert(c,tint,Vector2.zero);
            for(int i=0;i<Segments;i++){float a=i*Mathf.PI*2/Segments;vh.AddVert(c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),tint,Vector2.zero);}
            for(int i=0;i<Segments;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%Segments);
        }
        static void Quad(VertexHelper vh,Rect q,Color tint)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector2(q.xMin,q.yMin),tint,Vector2.zero);vh.AddVert(new Vector2(q.xMax,q.yMin),tint,Vector2.zero);
            vh.AddVert(new Vector2(q.xMax,q.yMax),tint,Vector2.zero);vh.AddVert(new Vector2(q.xMin,q.yMax),tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComicBalloonTail:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Triangle(new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax*.3f,r.yMax),new Color(.07f,.10f,.16f));
            Triangle(new Vector2(r.xMin+5,r.yMin-1),new Vector2(r.xMax-5,r.yMin-1),new Vector2(r.xMax*.3f,r.yMax-7),new Color(.99f,.985f,.96f));
            void Triangle(Vector2 a,Vector2 b,Vector2 c,Color tint){int n=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
        }
    }
}
