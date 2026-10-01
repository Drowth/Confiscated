using UnityEngine;
using UnityEngine.InputSystem;

namespace Confiscated
{
    /// <summary>
    /// A Twitch viewer's locker (TwitchNameCards) is a hiding place: step in and the caretaker can neither see nor
    /// catch you for as long as you stay. The view is from inside, through the vents, with the viewer's graffiti on
    /// the back of the door; hold the left mouse button to lean in for a closer, wider-angle look through the slit,
    /// and right-click to step back out. Then that locker needs a while before it'll shut properly again.
    /// </summary>
    public sealed class ViewerLockerHide : Interactable
    {
        public const float ReuseSeconds=20;
        const float MaxPeekYaw=45f,PeekReturnDegreesPerSec=220f,PeekZoomDegreesPerSec=90f,PeekFovScale=.55f;
        public string viewer;
        public Transform locker;
        public GameObject card;
        public static ViewerLockerHide Current {get;private set;}
        public static int TimesHidden {get;private set;}
        public GameObject Inside=>inside;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Current=null;TimesHidden=0;}

        float readyAt,oldNear,oldFov,peekFov,currentFov,peekYaw;
        Quaternion baseRotation;
        bool oldMove,oldLook,oldInput,oldHold,oldBody;
        Vector3 oldCameraPosition;Quaternion oldCameraRotation;
        PlayerInteractor player;FirstPersonController movement;
        GameObject inside;
        Renderer[] lockerRenderers;

        Bounds Body=>locker.GetComponent<Renderer>().bounds;
        Vector3 Front=>-locker.forward; // locker doors face away from the wall (see TwitchNameCards)

        public override string GetPrompt(PlayerInteractor p)
        {
            if(Current==this)return null;
            return Time.time<readyAt?viewer+"'s locker. The door's still rattling.":"F: hide in "+viewer+"'s locker";
        }
        public override bool CanInteract(PlayerInteractor p)
        {
            var game=GameManager.Instance;
            return Current==null&&Time.time>=readyAt&&game!=null&&game.IsPlaying&&!ComicDialogue.IsActive&&locker!=null;
        }
        public override void Interact(PlayerInteractor p)
        {
            if(!CanInteract(p))return;
            player=p;movement=p.GetComponent<FirstPersonController>();
            Current=this;TimesHidden++;
            oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=p.InputLocked;
            movement.MovementLocked=movement.LookLocked=true;p.InputLocked=true;
            oldHold=p.HoldAnchor!=null&&p.HoldAnchor.gameObject.activeSelf;if(p.HoldAnchor!=null)p.HoldAnchor.gameObject.SetActive(false);
            var body=movement.Controller;oldBody=body.enabled;body.enabled=false;
            var b=Body;
            p.transform.position=new Vector3(b.center.x,p.transform.position.y,b.center.z);
            var cam=p.ViewCamera;oldCameraPosition=cam.transform.localPosition;oldCameraRotation=cam.transform.localRotation;oldNear=cam.nearClipPlane;
            oldFov=cam.fieldOfView;currentFov=oldFov;peekFov=oldFov*PeekFovScale;peekYaw=0;
            float depth=Vector3.Scale(b.extents,Abs(Front)).magnitude;
            cam.transform.position=new Vector3(b.center.x,Mathf.Min(b.min.y+1.4f,b.max.y-.25f),b.center.z)-Front*depth*.35f;
            baseRotation=Quaternion.LookRotation(Front);cam.transform.rotation=baseRotation;cam.nearClipPlane=.02f;
            // The real locker (its own placeholder mesh, the actual door/body model underneath, and its handle) is
            // solid -- with it still rendering, the vent in the fake interior just looks straight into the back of it
            // from point-blank range. Same for the viewer's name card, taped on the outside of the door at eye height.
            // Hide them so the vent actually opens onto the corridor; the fake interior covers the rest of the view.
            var hidden=new System.Collections.Generic.List<Renderer>(locker.GetComponentsInChildren<Renderer>());
            if(card!=null)hidden.AddRange(card.GetComponentsInChildren<Renderer>(true));
            // Only the ones actually showing -- the locker's plain red placeholder cube sits switched off under the
            // real model, and switching it back on when you step out paints a flat red slab over the locker.
            hidden.RemoveAll(r=>!r.enabled);
            lockerRenderers=hidden.ToArray();
            foreach(var r in lockerRenderers)r.enabled=false;
            BuildInside(b,depth,cam);
            CaretakerAI.PlayerHidden=true;
            DoorSounds.For(gameObject,DoorSounds.Kind.Generic)?.Play(false);
            HudController.Instance?.SetBark("Hiding in "+viewer+"'s locker... hold LMB to peek, RMB to come out",3);
        }

        void Update()
        {
            if(Current!=this)return;
            var game=GameManager.Instance;
            if(game==null||!game.IsPlaying){Leave(false);return;}
            var mouse=Mouse.current;
            if(mouse!=null&&mouse.rightButton.wasPressedThisFrame){Leave(true);return;}
            // No time limit -- stays hidden until the player chooses to step back out. Holding the left button leans
            // in for a closer, wider look through the slit and lets the mouse swing the view left/right to see more
            // of the corridor either side; letting go eases back to the fixed, centred view.
            bool peeking=mouse!=null&&mouse.leftButton.isPressed;
            peekYaw=peeking?Mathf.Clamp(peekYaw+mouse.delta.x.ReadValue()*movement.MouseSensitivity,-MaxPeekYaw,MaxPeekYaw)
                            :Mathf.MoveTowards(peekYaw,0,PeekReturnDegreesPerSec*Time.deltaTime);
            currentFov=Mathf.MoveTowards(currentFov,peeking?peekFov:oldFov,PeekZoomDegreesPerSec*Time.deltaTime);
            var cam=player.ViewCamera;
            cam.transform.rotation=baseRotation*Quaternion.Euler(0,peekYaw,0);
            cam.fieldOfView=currentFov;
        }
        void OnDisable(){if(Current==this)Leave(GameManager.Instance!=null&&GameManager.Instance.IsPlaying);}

        /// <summary>restorePlayer=false when something else (detention, a capture) has taken over the player.</summary>
        void Leave(bool restorePlayer)
        {
            if(Current!=this)return;
            Current=null;CaretakerAI.PlayerHidden=false;readyAt=Time.time+ReuseSeconds;
            if(inside!=null)Destroy(inside);
            if(lockerRenderers!=null)foreach(var r in lockerRenderers)if(r!=null)r.enabled=true;
            lockerRenderers=null;
            if(player==null)return;
            var cam=player.ViewCamera;cam.transform.localPosition=oldCameraPosition;cam.transform.localRotation=oldCameraRotation;cam.nearClipPlane=oldNear;cam.fieldOfView=oldFov;
            if(player.HoldAnchor!=null)player.HoldAnchor.gameObject.SetActive(oldHold);
            if(restorePlayer&&locker!=null)
            {
                var b=Body;float depth=Vector3.Scale(b.extents,Abs(Front)).magnitude;
                player.transform.position=new Vector3(b.center.x,player.transform.position.y,b.center.z)+Front*(depth+.45f);
                player.transform.rotation=Quaternion.LookRotation(Front);movement.ResetLook();
                movement.MovementLocked=oldMove;movement.LookLocked=oldLook;player.InputLocked=oldInput;
                DoorSounds.For(gameObject,DoorSounds.Kind.Generic)?.Play(true);
            }
            movement.Controller.enabled=oldBody||movement.Controller.enabled;
            player.SuppressActionsThisFrame();Physics.SyncTransforms();
        }

        void BuildInside(Bounds b,float depth,Camera cam)
        {
            inside=new GameObject("Inside "+viewer+"'s locker");inside.transform.SetParent(transform,false);
            // Same dark red as the locker's own paintwork outside (sampled off its texture), not a generic gunmetal.
            var paint=new Material(Shader.Find("Universal Render Pipeline/Unlit"));paint.color=new Color(.42f,.13f,.13f);
            inside.AddComponent<MaterialCleanup>().materials=new[]{paint};
            Vector3 f=Front,r=Vector3.Cross(Vector3.up,f).normalized*-1;
            float w=Vector3.Scale(b.extents,Abs(r)).magnitude*2-.02f,h=b.size.y-.02f,d=depth*2-.02f;
            Vector3 c=b.center,eyePos=cam.transform.position;
            Vector3 door=c+f*(depth-.03f);
            Panel(c-f*(depth-.02f),new Vector3(w,h,.01f),f,paint);
            Panel(c+r*(w*.5f),new Vector3(.01f,h,d),f,paint);
            Panel(c-r*(w*.5f),new Vector3(.01f,h,d),f,paint);
            Panel(new Vector3(c.x,b.max.y-.02f,c.z),new Vector3(w,.01f,d),f,paint);
            Panel(new Vector3(c.x,b.min.y+.02f,c.z),new Vector3(w,.01f,d),f,paint);
            // One wide letterbox vent in the door, sized off what the camera actually sees at the door's distance: a
            // third of the view tall, most of its width, a little above centre -- red frame round a proper look at the
            // corridor. (Hairline slits at this range barely registered on screen.)
            float dist=Vector3.Dot(door-eyePos,f);
            float viewH=2*dist*Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad),viewW=viewH*cam.aspect;
            float openH=viewH*.34f,openW=Mathf.Min(viewW*.84f,w-.06f),openY=eyePos.y+viewH*.13f;
            float oTop=openY+openH*.5f,oBottom=openY-openH*.5f,side=(w-openW)*.5f;
            DoorSegment(door,w,c.y+h*.5f,oTop,f,paint);
            DoorSegment(door,w,oBottom,c.y-h*.5f,f,paint);
            if(side>.001f)foreach(var s in new[]{-1f,1f})
                Panel(new Vector3(door.x,openY,door.z)+r*(s*(openW+side)*.5f),new Vector3(side,openH,.01f),f,paint);
            // The viewer's scrawl goes in the red band under the vent, shrunk to fit it.
            float bandBottom=eyePos.y-viewH*.5f,bandH=oBottom-bandBottom;
            var scrawl=new GameObject(viewer+" woz ere");scrawl.transform.SetParent(inside.transform,false);
            var text=scrawl.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.fontSize=64;text.characterSize=.0042f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.black;text.fontStyle=FontStyle.Bold;
            text.text=viewer+"\nwoz ere";
            scrawl.AddComponent<WorldLabel>();
            // Measured unrotated, then turned to face the eye (TextMesh reads from its -Z side) at a scribbled tilt.
            var size=scrawl.GetComponent<Renderer>().bounds.size;
            text.characterSize*=Mathf.Min(1,openW*.6f/Mathf.Max(size.x,.001f),bandH*.7f/Mathf.Max(size.y,.001f));
            scrawl.transform.SetPositionAndRotation(new Vector3(door.x,(oBottom+bandBottom)*.5f,door.z)-f*.012f,Quaternion.LookRotation(f)*Quaternion.Euler(0,0,-8));
        }
        /// <summary>One solid strip of the door between two door-local heights (yTop above yBottom); skipped if the
        /// slit either side of it has swallowed the whole gap.</summary>
        void DoorSegment(Vector3 doorXZ,float w,float yTop,float yBottom,Vector3 facing,Material m)
        {
            float height=yTop-yBottom;if(height<=.001f)return;
            Panel(new Vector3(doorXZ.x,(yTop+yBottom)*.5f,doorXZ.z),new Vector3(w,height,.01f),facing,m);
        }
        void Panel(Vector3 at,Vector3 size,Vector3 facing,Material m)
        {
            var p=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(inside.transform,false);p.transform.SetPositionAndRotation(at,Quaternion.LookRotation(facing));p.transform.localScale=size;
            var r=p.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));

        sealed class MaterialCleanup:MonoBehaviour{public Material[] materials;void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);}}
    }
}
