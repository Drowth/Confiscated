using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// A Twitch viewer's locker (TwitchNameCards) is a hiding place: step in for seven seconds and the caretaker can neither
    /// see nor catch you. The view is from inside, through the vents, with the viewer's graffiti on the back of the door.
    /// Then you step back out, and that locker needs a while before it'll shut properly again.
    /// </summary>
    public sealed class ViewerLockerHide : Interactable
    {
        public const float HideSeconds=7,ReuseSeconds=20;
        public string viewer;
        public Transform locker;
        public static ViewerLockerHide Current {get;private set;}
        public static int TimesHidden {get;private set;}
        public float SecondsLeft=>Current==this?Mathf.Max(0,until-Time.time):0;
        public GameObject Inside=>inside;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Current=null;TimesHidden=0;}

        float readyAt,until,oldNear;
        bool oldMove,oldLook,oldInput,oldHold,oldBody;
        Vector3 oldCameraPosition;Quaternion oldCameraRotation;
        PlayerInteractor player;FirstPersonController movement;
        GameObject inside;

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
            Current=this;TimesHidden++;until=Time.time+HideSeconds;
            oldMove=movement.MovementLocked;oldLook=movement.LookLocked;oldInput=p.InputLocked;
            movement.MovementLocked=movement.LookLocked=true;p.InputLocked=true;
            oldHold=p.HoldAnchor!=null&&p.HoldAnchor.gameObject.activeSelf;if(p.HoldAnchor!=null)p.HoldAnchor.gameObject.SetActive(false);
            var body=movement.Controller;oldBody=body.enabled;body.enabled=false;
            var b=Body;
            p.transform.position=new Vector3(b.center.x,p.transform.position.y,b.center.z);
            var cam=p.ViewCamera;oldCameraPosition=cam.transform.localPosition;oldCameraRotation=cam.transform.localRotation;oldNear=cam.nearClipPlane;
            float depth=Vector3.Scale(b.extents,Abs(Front)).magnitude;
            cam.transform.position=new Vector3(b.center.x,Mathf.Min(b.min.y+1.4f,b.max.y-.25f),b.center.z)-Front*depth*.35f;
            cam.transform.rotation=Quaternion.LookRotation(Front);cam.nearClipPlane=.02f;
            BuildInside(b,depth,cam.transform.position.y);
            CaretakerAI.PlayerHidden=true;
            DoorSounds.For(gameObject,DoorSounds.Kind.Generic)?.Play(false);
            HudController.Instance?.SetBark("Hiding in "+viewer+"'s locker...",2);
        }

        void Update()
        {
            if(Current!=this)return;
            var game=GameManager.Instance;
            if(game==null||!game.IsPlaying){Leave(false);return;}
            // Keep the view pinned inside while the rest of the game runs on.
            if(Time.time>=until)Leave(true);
        }
        void OnDisable(){if(Current==this)Leave(GameManager.Instance!=null&&GameManager.Instance.IsPlaying);}

        /// <summary>restorePlayer=false when something else (detention, a capture) has taken over the player.</summary>
        void Leave(bool restorePlayer)
        {
            if(Current!=this)return;
            Current=null;CaretakerAI.PlayerHidden=false;readyAt=Time.time+ReuseSeconds;
            if(inside!=null)Destroy(inside);
            if(player==null)return;
            var cam=player.ViewCamera;cam.transform.localPosition=oldCameraPosition;cam.transform.localRotation=oldCameraRotation;cam.nearClipPlane=oldNear;
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

        void BuildInside(Bounds b,float depth,float eye)
        {
            inside=new GameObject("Inside "+viewer+"'s locker");inside.transform.SetParent(transform,false);
            var steel=new Material(Shader.Find("Universal Render Pipeline/Unlit"));steel.color=new Color(.46f,.47f,.5f);
            var light=new Material(Shader.Find("Universal Render Pipeline/Unlit"));light.color=new Color(.93f,.92f,.86f);
            inside.AddComponent<MaterialCleanup>().materials=new[]{steel,light};
            Vector3 f=Front,r=Vector3.Cross(Vector3.up,f).normalized*-1;
            float w=Vector3.Scale(b.extents,Abs(r)).magnitude*2-.02f,h=b.size.y-.02f,d=depth*2-.02f;
            Vector3 c=b.center;
            Vector3 door=c+f*(depth-.03f);
            // The inside of the door is what you're staring at: vents, and the viewer's scrawl.
            Panel(door,new Vector3(w,h,.01f),f,steel);
            Panel(c-f*(depth-.02f),new Vector3(w,h,.01f),f,steel);
            Panel(c+r*(w*.5f),new Vector3(.01f,h,d),f,steel);
            Panel(c-r*(w*.5f),new Vector3(.01f,h,d),f,steel);
            Panel(new Vector3(c.x,b.max.y-.02f,c.z),new Vector3(w,.01f,d),f,steel);
            Panel(new Vector3(c.x,b.min.y+.02f,c.z),new Vector3(w,.01f,d),f,steel);
            // Vent slits just above eye level: the only light getting in.
            for(int i=0;i<3;i++)Panel(new Vector3(door.x,eye+.075f+i*.035f,door.z)-f*.008f,new Vector3(w*.6f,.01f,.004f),f,light);
            var scrawl=new GameObject(viewer+" woz ere");scrawl.transform.SetParent(inside.transform,false);
            var text=scrawl.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.fontSize=64;text.characterSize=.0042f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.black;text.fontStyle=FontStyle.Bold;
            text.text=viewer+"\nwoz ere";
            scrawl.AddComponent<WorldLabel>();
            // Fit the width of the door, then turn it to face the eye (TextMesh reads from its -Z side) at a scribbled tilt.
            float width=scrawl.GetComponent<Renderer>().bounds.size.x;if(width>w*.7f)text.characterSize*=w*.7f/width;
            scrawl.transform.SetPositionAndRotation(new Vector3(door.x,eye-.035f,door.z)-f*.012f,Quaternion.LookRotation(f)*Quaternion.Euler(0,0,-8));
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
