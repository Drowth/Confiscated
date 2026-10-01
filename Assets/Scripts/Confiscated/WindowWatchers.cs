using System.Collections.Generic;
using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// The final escape's audience. Once all five belongings are back, night falls outside, heavy mist banks up against every
    /// exterior window and the grinning heads of the pupils, the dinner lady and Mr Reed rise out of it behind the sill to watch
    /// the player (the cutout shader keeps each one turned to the camera). The heads are only heads: they sit with their chins
    /// at the sill and the mist hides where the bodies should be. Never the caretaker.
    /// Built lazily from the hunt artwork, so it needs no installer and survives scene rebuilds.
    /// </summary>
    public sealed class WindowWatchers : MonoBehaviour
    {
        const string WindowPrefix="Garden view window ";
        // Head crop in source pixels (x, top, width, height), world height, head centre height and whether the sheet is magenta keyed.
        // Centre heights put the cut-off neck at the bottom of the glass (1.03 m), below the child's eye line.
        sealed class Art{public string texture;public RectInt crop;public float height,y;public bool magenta;public Material material,collar;}
        static readonly Art[] Cast={
            new Art{texture="T_Student_Seated_Views",crop=new RectInt(370,25,250,280),height=.42f,y=1.24f,magenta=true},
            new Art{texture="T_Student_Girl_Seated_Views",crop=new RectInt(355,25,270,275),height=.42f,y=1.25f,magenta=true},
            new Art{texture="T_DinnerLady",crop=new RectInt(370,25,290,370),height=.5f,y=1.28f},
            new Art{texture="T_Mr_Reed",crop=new RectInt(410,25,200,305),height=.52f,y=1.3f}};
        static readonly int[,] Pairs={{0,1},{2,0},{1,3},{3,2}};
        const float PaneOffset=.58f,Outside=.75f,Sink=.45f,RiseSeconds=.8f;
        // Mist sheets per window: metres outside the pane, height, density, ragged top fade. The first two stand in front of
        // the heads, below their mouths, and hide where the bodies should be; the rest bank up behind and swallow the garden.
        static readonly float[,] Mist={{.32f,1.12f,.92f,.2f},{.55f,1.16f,.85f,.2f},{1f,1.9f,.6f,.7f},{1.6f,2.3f,.6f,.8f},{2.6f,2.6f,.7f,.9f}};
        // Each head also wears a collar of mist that turns with it, so the cut-off neck is covered from any angle and
        // distance: it reaches this far up the artwork (neck and collar, not the grin) and down to the sheets.
        const float MistWidth=7,CollarWidth=1.1f,CollarReach=.11f,CollarFade=.06f,CollarBase=.4f,NightSeconds=2.5f;
        static readonly Color MistColour=new Color(.46f,.5f,.58f);
        // Flat daylit art outdoors (brick, render, the porch furniture) has no night of its own: tint it per renderer.
        static readonly Color NightTint=new Color(.16f,.2f,.3f);
        static readonly int NightId=Shader.PropertyToID("_OutdoorNight"),FadeId=Shader.PropertyToID("_Fade"),BaseColorId=Shader.PropertyToID("_BaseColor"),GlassNightId=Shader.PropertyToID("_Night");
        sealed class Head{public Transform pivot,transform;public Vector3 rest,right;public float delay,phase;}
        readonly List<Head> heads=new List<Head>();Transform root;float shownAt;bool built;
        readonly List<Material> mist=new List<Material>();
        readonly List<(Renderer renderer,Color colour)> daylit=new List<(Renderer,Color)>();
        readonly List<Renderer> glass=new List<Renderer>();
        MaterialPropertyBlock block;
        public bool Showing {get;private set;}
        public int WindowCount {get;private set;}
        public int HeadCount=>heads.Count;
        /// <summary>0 = the usual daylight outdoors, 1 = night and mist.</summary>
        public float Night {get;private set;}
        void Update(){Refresh();SetNight(Showing?Mathf.MoveTowards(Night,1,Time.deltaTime/NightSeconds):0);if(Showing)Animate(Time.time-shownAt);}
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
            built=true;root=new GameObject("Window watchers").transform;block=new MaterialPropertyBlock();
            var windows=new List<Transform>();
            foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t.name.StartsWith(WindowPrefix))windows.Add(t);
            windows.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));WindowCount=windows.Count;if(WindowCount==0)return;
            var centre=Vector3.zero;foreach(var w in windows)centre+=w.position;centre/=WindowCount;
            foreach(var art in Cast)art.material=MakeMaterial(art);
            BuildMistMaterials();
            for(int i=0;i<WindowCount;i++)
            {
                var w=windows[i];var outward=w.forward;outward.y=0;outward.Normalize();
                if(Vector3.Dot(outward,w.position-centre)<0)outward=-outward;
                var right=Vector3.Cross(Vector3.up,outward);int pair=i%Pairs.GetLength(0);float side=i%2==0?1:-1;
                for(int layer=0;layer<Mist.GetLength(0)&&layer<mist.Count;layer++)
                {
                    var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Mist "+layer+" outside "+w.name;Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(root,false);q.transform.localScale=new Vector3(MistWidth,Mist[layer,1],1);
                    q.transform.SetPositionAndRotation(new Vector3(w.position.x,Mist[layer,1]*.5f,w.position.z)+outward*Mist[layer,0],Quaternion.LookRotation(outward));
                    var r=q.GetComponent<MeshRenderer>();r.sharedMaterial=mist[layer];r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                }
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
                    if(art.collar==null)continue;
                    // The collar stays put while the head rises through it.
                    float top=art.y-art.height*.5f+CollarReach;
                    var turn=new GameObject(art.texture+" mist collar pivot").transform;turn.SetParent(root,false);turn.position=new Vector3(rest.x,0,rest.z);turn.gameObject.AddComponent<BillboardY>();
                    var c=GameObject.CreatePrimitive(PrimitiveType.Quad);c.name="Mist collar "+w.name;Destroy(c.GetComponent<Collider>());
                    c.transform.SetParent(turn,false);c.transform.localPosition=new Vector3(0,(top+CollarBase)*.5f,-.05f);c.transform.localScale=new Vector3(CollarWidth,top-CollarBase,1);
                    r=c.GetComponent<MeshRenderer>();r.sharedMaterial=art.collar;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                }
            }
            // Named for what they are rather than their shader: a blackout swaps these for lit copies that keep the name.
            foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var m=r.sharedMaterial;if(m==null)continue;
                // Glass that looks outdoors loses its pale pencil sheen; the classroom windows keep theirs.
                if(m.shader.name=="Confiscated/Pencil Glass"){if(r.transform.parent==null||!r.transform.parent.name.StartsWith("Class window"))glass.Add(r);continue;}
                if(!m.HasProperty(BaseColorId)||m.shader.name=="Confiscated/Pencil Surface"||m.name.Contains("Light"))continue;
                if(!m.name.StartsWith("M_Exterior_")&&!m.name.Contains(" (outdoor"))continue;
                r.GetPropertyBlock(block);daylit.Add((r,block.HasColor(BaseColorId)?block.GetColor(BaseColorId):m.GetColor(BaseColorId)));
            }
            root.gameObject.SetActive(false);
        }
        void BuildMistMaterials()
        {
            var shader=Resources.Load<Shader>("Shaders/WindowMist");if(shader==null)return;
            int layers=Mist.GetLength(0);
            for(int i=0;i<layers;i++)
            {
                // Paler up close, darker with depth.
                var m=new Material(shader){name="Window mist "+i};m.SetVector("_Size",new Vector4(MistWidth,Mist[i,1]));m.SetColor("_Color",MistColour*Mathf.Lerp(1,.72f,i/(layers-1f)));
                m.SetFloat("_Density",Mist[i,2]);m.SetFloat("_TopFade",Mist[i,3]);m.SetFloat("_Seed",i*7.3f);m.SetFloat(FadeId,0);mist.Add(m);
            }
            foreach(var art in Cast)
            {
                var m=new Material(shader){name=art.texture+" mist collar"};m.SetVector("_Size",new Vector4(CollarWidth,art.y-art.height*.5f+CollarReach-CollarBase));m.SetColor("_Color",MistColour*.93f);
                m.SetFloat("_Density",1);m.SetFloat("_Wisp",.06f);m.SetFloat("_TopFade",CollarFade);m.SetFloat("_Ragged",.3f);m.SetFloat("_SideFade",.4f);m.SetFloat("_Seed",3.1f);m.SetFloat(FadeId,0);
                art.collar=m;mist.Add(m);
            }
        }
        // Night comes on with the heads; anything that hides them (detention, a catch) brings the day straight back.
        void SetNight(float level)
        {
            if(level==Night)return;Night=level;
            Shader.SetGlobalFloat(NightId,level);
            foreach(var m in mist)m.SetFloat(FadeId,level);
            var tint=Color.Lerp(Color.white,NightTint,level);
            foreach(var d in daylit)if(d.renderer!=null){d.renderer.GetPropertyBlock(block);block.SetColor(BaseColorId,d.colour*tint);d.renderer.SetPropertyBlock(block);}
            foreach(var r in glass)if(r!=null){r.GetPropertyBlock(block);block.SetFloat(GlassNightId,level);r.SetPropertyBlock(block);}
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
        // Each head rises out of the mist behind the sill in turn, then sways a little, as if craning to see.
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
            SetNight(0);
            if(root!=null)Destroy(root.gameObject);
            foreach(var m in mist)Destroy(m);
            foreach(var art in Cast){if(art.material!=null)Destroy(art.material);art.material=art.collar=null;}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShader()=>Shader.SetGlobalFloat("_OutdoorNight",0);
    }
}
