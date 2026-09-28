using System.Collections.Generic;
using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// The final escape's audience. Once all five belongings are back, the grinning heads of the pupils, the dinner lady and Mr Reed
    /// rise behind the sill of every exterior window and watch the player (the cutout shader keeps each one turned to the camera).
    /// Never the caretaker. Built lazily from the hunt artwork, so it needs no installer and survives scene rebuilds.
    /// </summary>
    public sealed class WindowWatchers : MonoBehaviour
    {
        const string WindowPrefix="Garden view window ";
        // Head crop in source pixels (x, top, width, height), world height, head centre height and whether the sheet is magenta keyed.
        sealed class Art{public string texture;public RectInt crop;public float height,y;public bool magenta;public Material material;}
        static readonly Art[] Cast={
            new Art{texture="T_Student_Seated_Views",crop=new RectInt(370,25,250,280),height=.42f,y=1.3f,magenta=true},
            new Art{texture="T_Student_Girl_Seated_Views",crop=new RectInt(355,25,270,275),height=.42f,y=1.31f,magenta=true},
            new Art{texture="T_DinnerLady",crop=new RectInt(370,25,290,370),height=.5f,y=1.45f},
            new Art{texture="T_Mr_Reed",crop=new RectInt(410,25,200,305),height=.52f,y=1.5f}};
        static readonly int[,] Pairs={{0,1},{2,0},{1,3},{3,2}};
        const float PaneOffset=.58f,Outside=.75f,Sink=.45f,RiseSeconds=.8f;
        sealed class Head{public Transform pivot,transform;public Vector3 rest,right;public float delay,phase;}
        readonly List<Head> heads=new List<Head>();Transform root;float shownAt;bool built;
        public bool Showing {get;private set;}
        public int WindowCount {get;private set;}
        public int HeadCount=>heads.Count;
        void Update(){Refresh();if(Showing)Animate(Time.time-shownAt);}
        /// <summary>Shows the heads while the run is live with every belonging recovered; detention or a catch hides them again.</summary>
        public void Refresh()
        {
            var run=SchoolRunController.Instance;var game=GameManager.Instance;
            bool show=run!=null&&game!=null&&game.IsPlaying&&run.RoundStarted&&run.Count>=5;
            if(show&&!built)Build();
            if(show==Showing)return;
            Showing=show;shownAt=Time.time;if(root!=null)root.gameObject.SetActive(show);if(show)Animate(0);
        }
        void Build()
        {
            built=true;root=new GameObject("Window watchers").transform;
            var windows=new List<Transform>();
            foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t.name.StartsWith(WindowPrefix))windows.Add(t);
            windows.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));WindowCount=windows.Count;if(WindowCount==0)return;
            var centre=Vector3.zero;foreach(var w in windows)centre+=w.position;centre/=WindowCount;
            foreach(var art in Cast)art.material=MakeMaterial(art);
            for(int i=0;i<WindowCount;i++)
            {
                var w=windows[i];var outward=w.forward;outward.y=0;outward.Normalize();
                if(Vector3.Dot(outward,w.position-centre)<0)outward=-outward;
                var right=Vector3.Cross(Vector3.up,outward);int pair=i%Pairs.GetLength(0);float side=i%2==0?1:-1;
                for(int slot=0;slot<2;slot++)
                {
                    var art=Cast[Pairs[pair,slot]];if(art.material==null)continue;
                    // The quad must really face the camera (BillboardY), not just be drawn facing it: the cutout shader's
                    // depth pass uses the quad as placed, and a mismatch clips half the face. The tilt lives on the child.
                    var pivot=new GameObject(art.texture+" watcher pivot").transform;pivot.SetParent(root,false);pivot.gameObject.AddComponent<BillboardY>();
                    var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name=art.texture+" watching "+w.name;Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(pivot,false);q.transform.localScale=new Vector3(art.height*art.crop.width/art.crop.height,art.height,1);
                    var r=q.GetComponent<MeshRenderer>();r.sharedMaterial=art.material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                    var rest=new Vector3(w.position.x,art.y,w.position.z)+outward*Outside+right*PaneOffset*side*(slot==0?-1:1);
                    heads.Add(new Head{pivot=pivot,transform=q.transform,rest=rest,right=right,delay=i*.12f+slot*.3f+Random.Range(0,.15f),phase=Random.Range(0,6.3f)});
                }
            }
            root.gameObject.SetActive(false);
        }
        static Material MakeMaterial(Art art)
        {
            var shader=Shader.Find("Confiscated/Character Cutout");var texture=Resources.Load<Texture2D>("Art/Hunt/"+art.texture+"_Hunt");
            if(shader==null||texture==null)return null;
            var m=new Material(shader){name=art.texture+" window watcher"};m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);
            m.SetFloat("_Cutoff",.5f);m.SetFloat("_MagentaKey",art.magenta?1:0);m.SetFloat("_DirectionalViews",1);
            // Front and back share the crop: seen from anywhere, it is the grinning face.
            // Crops are in the source PNG's pixels. The import can be smaller (max-size clamp), so never divide by texture.width:
            // the pupils' sheets are 1536x1024 landscape, the adults' 1024x1536 portrait.
            Vector2 source=art.magenta?new Vector2(1536,1024):new Vector2(1024,1536);
            var rect=new Vector4(art.crop.x/source.x,1-art.crop.yMax/source.y,art.crop.width/source.x,art.crop.height/source.y);
            m.SetVector("_FrontRect",rect);m.SetVector("_BackRect",rect);return m;
        }
        // Each head rises from behind the sill in turn, then sways a little, as if craning to see.
        void Animate(float seconds)
        {
            foreach(var h in heads)
            {
                float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-h.delay)/RiseSeconds));
                float t=Time.time+h.phase;
                h.pivot.position=h.rest+h.right*Mathf.Sin(t*.7f)*.03f+Vector3.up*(Mathf.Sin(t*1.3f)*.015f-(1-rise)*Sink);
                h.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*.9f)*4);
            }
        }
        void OnDestroy()
        {
            if(root!=null)Destroy(root.gameObject);
            foreach(var art in Cast)if(art.material!=null){Destroy(art.material);art.material=null;}
        }
    }
}
