using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>
    /// Installs the roaming school photographer, starting at the central junction. Narrow one-object installer: replaces
    /// only "School photographer". Uses T_Photographer.png when it exists, otherwise draws a placeholder cutout so the
    /// mechanics can be played before the art arrives.
    /// </summary>
    public static class SchoolPhotographerSetup
    {
        const string Root="School photographer",TexturePath="Assets/Art/Textures/T_Photographer.png",MaterialPath="Assets/Art/Materials/M_Photographer.mat";
        static readonly Vector3 Position=new Vector3(-1.6f,0,58.75f);
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat");

        [MenuItem("Confiscated/Chase Feedback/Add School Photographer")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
            var old=GameObject.Find(Root);if(old!=null)Undo.DestroyObjectImmediate(old);
            var root=new GameObject(Root);Undo.RegisterCreatedObjectUndo(root,"Add school photographer");
            if(!NavMesh.SamplePosition(Position,out var start,2f,NavMesh.AllAreas))start.position=Position;
            root.transform.SetPositionAndRotation(start.position,Quaternion.LookRotation(Vector3.left));
            var agent=root.AddComponent<NavMeshAgent>();agent.radius=.3f;agent.height=1.7f;agent.speed=1.4f;agent.obstacleAvoidanceType=ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var him=root.AddComponent<SchoolPhotographer>();
            // Set explicitly: a re-install must not inherit stale serialized values from an older build of him.
            him.spotRange=9f;him.spotHalfAngle=180f;him.warningSeconds=1.1f;him.blindSeconds=4f;him.blindHalfAngle=70f;him.flashStallSeconds=4f;him.cooldownSeconds=12f;him.junctionBias=.7f;him.lingerSeconds=6f;

            // Him: a cutout quad on the Character Cutout shader, bobbing with his own walk.
            var tex=Texture();var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(mat==null){mat=new Material(Shader.Find("Confiscated/Character Cutout"));AssetDatabase.CreateAsset(mat,MaterialPath);}
            mat.SetTexture("_BaseMap",tex);EditorUtility.SetDirty(mat);
            var hit=root.AddComponent<CapsuleCollider>();hit.center=new Vector3(0,.85f,0);hit.height=1.7f;hit.radius=.25f;
            var pivot=new GameObject("Walking pencil pivot").transform;pivot.SetParent(root.transform,false);
            var sway=pivot.gameObject.AddComponent<CutoutMotion>();sway.movementSource=root.transform;sway.walkSpeed=1.4f;sway.walkBounce=.02f;sway.walkSwayDegrees=1.2f;sway.strideLength=.9f;
            var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Photographer cutout";q.transform.SetParent(pivot,false);
            q.transform.localPosition=new Vector3(0,.91f,0);q.transform.localScale=new Vector3(1.233f,1.85f,1);
            Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;

            // The camera held up in front of him, flash on top: where the light comes from.
            var cam=GameObject.CreatePrimitive(PrimitiveType.Cube);cam.name="Camera";cam.transform.SetParent(pivot,false);cam.transform.localPosition=new Vector3(.1f,1.22f,.3f);cam.transform.localScale=new Vector3(.2f,.14f,.22f);
            Object.DestroyImmediate(cam.GetComponent<Collider>());cam.GetComponent<Renderer>().sharedMaterial=Mat("M_Chapter_Ink");
            var lens=GameObject.CreatePrimitive(PrimitiveType.Cylinder);lens.name="Lens";lens.transform.SetParent(cam.transform,false);lens.transform.localPosition=new Vector3(0,0,.6f);lens.transform.localRotation=Quaternion.Euler(90,0,0);lens.transform.localScale=new Vector3(.5f,.25f,.6f);
            Object.DestroyImmediate(lens.GetComponent<Collider>());lens.GetComponent<Renderer>().sharedMaterial=Mat("M_Chapter_Grey");
            var flashBox=GameObject.CreatePrimitive(PrimitiveType.Cube);flashBox.name="Flash";flashBox.transform.SetParent(cam.transform,false);flashBox.transform.localPosition=new Vector3(0,1.2f,0);flashBox.transform.localScale=new Vector3(1.1f,.9f,.35f);
            Object.DestroyImmediate(flashBox.GetComponent<Collider>());flashBox.GetComponent<Renderer>().sharedMaterial=Mat("M_Chapter_Paper");
            var light=new GameObject("Flash light").AddComponent<Light>();light.transform.SetParent(cam.transform,false);light.transform.localPosition=new Vector3(0,1.2f,1);
            light.type=LightType.Point;light.range=9;light.intensity=0;light.color=new Color(1,.97f,.9f);light.shadows=LightShadows.None;him.flashLight=light;

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("School photographer installed at "+start.position+(tex.name.StartsWith("Placeholder")?" with a placeholder cutout (add "+TexturePath+" and re-run)":""));
        }

        /// <summary>The real art if it exists; otherwise a drawn placeholder, written once to Art/Textures so it imports like the real thing.</summary>
        static Texture2D Texture()
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if(tex==null)
            {
                var p=Placeholder();System.IO.File.WriteAllBytes(TexturePath,p.EncodeToPNG());Object.DestroyImmediate(p);AssetDatabase.ImportAsset(TexturePath);
                tex=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if(importer!=null&&(!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None||importer.wrapMode!=TextureWrapMode.Clamp))
            {importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();tex=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);}
            return tex;
        }
        /// <summary>A pencil-ish figure holding a boxy camera: enough to read "photographer" at child height.</summary>
        static Texture2D Placeholder()
        {
            const int W=512,H=768;var t=new Texture2D(W,H,TextureFormat.RGBA32,false){name="Placeholder photographer"};
            var px=new Color32[W*H];var ink=new Color32(18,26,40,255);var cream=new Color32(242,232,205,255);var khaki=new Color32(150,140,95,255);var red=new Color32(170,50,45,255);
            void Blob(float cx,float cy,float rx,float ry,Color32 fill){for(int y=0;y<H;y++)for(int x=0;x<W;x++){float u=(x-cx)/rx,v=(y-cy)/ry;float r=u*u+v*v;if(r<=1){bool edge=r>.78f;px[y*W+x]=edge?ink:fill;}}}
            void Rect(int x0,int y0,int x1,int y1,Color32 fill){for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){bool edge=x<x0+6||x>=x1-6||y<y0+6||y>=y1-6;px[y*W+x]=edge?ink:fill;}}
            Rect(200,20,240,250,ink);Rect(272,20,312,250,ink);      // legs
            Rect(150,10,250,40,ink);Rect(262,10,362,40,ink);        // big shoes
            Rect(170,240,342,520,khaki);                             // waistcoat body
            Rect(190,300,230,330,ink);Rect(282,300,322,330,ink);Rect(190,400,230,430,ink);Rect(282,400,322,430,ink); // pockets
            Rect(244,330,268,500,red);                               // tie
            Blob(256,610,78,88,cream);                               // head
            Rect(190,650,322,672,ink);                               // comb-over
            Blob(230,620,16,10,cream);Blob(282,620,16,10,cream);Rect(214,612,246,616,ink);Rect(266,612,298,616,ink); // glasses
            Rect(222,560,290,572,ink);                               // grin
            Rect(330,430,470,520,ink);Rect(350,520,450,600,cream);Blob(400,475,24,24,cream); // camera + flash box + lens
            Rect(100,440,170,470,khaki);Rect(60,440,110,500,cream); // thumbs-up arm
            t.SetPixels32(px);t.Apply();return t;
        }
    }
}
