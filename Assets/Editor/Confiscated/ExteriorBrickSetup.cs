using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Cosmetic brick on the outside of the school and the garden walls. The walls themselves keep the corridor paint on both
    /// faces; this adds one thin, collider-free skin mesh over every wall face that looks out under the open sky (no ceiling
    /// overhead), plus caps on free-standing garden walls. Unlit, so the dim night-time fill used indoors can't darken it.
    /// The brick texture is generated here as a placeholder; new art can overwrite T_Exterior_Brick.png (GUID kept).
    /// </summary>
    public static class ExteriorBrickSetup
    {
        public const string RootName="Exterior brick";
        const string TexPath="Assets/Art/Textures/T_Exterior_Brick.png",MatPath="Assets/Art/Materials/M_Exterior_Brick.mat",MeshPath="Assets/Art/Meshes/ExteriorBrickSkin.asset";
        // One texture tile covers 1.2 m x 0.6 m: five bricks (225 mm + mortar) by eight courses.
        static readonly Vector2 Tile=new(1.2f,.6f);
        const float Offset=.006f,Step=.5f;
        const string Placeholder="generated placeholder brick",RenderPath="Assets/Art/Materials/M_Exterior_Render.mat";

        [MenuItem("Confiscated/Outdoors/Apply Exterior Brick")]
        public static void ApplyMenu(){Apply();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();}

        public static void Apply()
        {
            var material=Material();
            var old=GameObject.Find(RootName);if(old!=null)Object.DestroyImmediate(old);
            var renderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var ceilings=renderers.Where(r=>r.name=="Ceiling").Select(r=>r.bounds).ToArray();
            bool Outdoors(Vector3 p)=>!ceilings.Any(c=>p.x>c.min.x&&p.x<c.max.x&&p.z>c.min.z&&p.z<c.max.z);
            var verts=new List<Vector3>();var uvs=new List<Vector2>();var norms=new List<Vector3>();var tris=new List<int>();
            int faces=0,caps=0;
            // Openings the skin must leave clear: every window (by its glass, plus frame) and every door (posts to lintel).
            var openings=renderers.Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Confiscated/Pencil Glass").Select(r=>{var g=r.bounds;g.Expand(new Vector3(.14f,.14f,.14f));return g;}).ToList();
            foreach(var door in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))
            {
                var parts=door.GetComponentsInChildren<Renderer>().Where(r=>r.name=="PostL"||r.name=="PostR"||r.name=="Lintel").Select(r=>r.bounds).ToList();
                if(parts.Count==0)continue;var g=parts[0];foreach(var p in parts)g.Encapsulate(p);g.Expand(new Vector3(.02f,0,.02f));openings.Add(g);
            }
            foreach(var wall in renderers.Where(r=>r.name.StartsWith("Wall_")&&r.GetComponentInParent<Canvas>()==null))
            {
                var b=wall.bounds;
                bool alongX=b.size.x>=b.size.z;
                Vector3 along=alongX?Vector3.right:Vector3.forward,across=alongX?Vector3.forward:Vector3.right;
                float length=alongX?b.size.x:b.size.z,half=(alongX?b.size.z:b.size.x)*.5f;
                float start=Vector3.Dot(b.min,along);
                bool bothOut=true;
                foreach(float side in new[]{-1f,1f})
                {
                    Vector3 n=across*side;
                    Vector3 plane=b.center+n*half;
                    // Walk the face in half-metre steps and skin each outdoor run (a face can be half garden, half hall).
                    int steps=Mathf.Max(1,Mathf.CeilToInt(length/Step));float runFrom=-1;bool anyIn=false;
                    for(int i=0;i<=steps;i++)
                    {
                        bool outside=i<steps&&Outdoors(Sample(plane,n,along,start,length,(i+.5f)/steps));
                        if(!outside)anyIn|=i<steps;
                        if(outside&&runFrom<0)runFrom=i*length/steps;
                        if(!outside&&runFrom>=0)
                        {
                            float a=runFrom,z=i*length/steps;
                            // Wrap the wall's own ends so outside corners close up.
                            if(a<=0)a-=half+Offset;if(z>=length)z+=half+Offset;
                            faces+=Skin(verts,uvs,norms,tris,plane+n*Offset,n,along,across,half,start+a,start+z,b.min.y,b.max.y,openings);runFrom=-1;
                        }
                    }
                    bothOut&=!anyIn;
                }
                // Free-standing garden walls: brick along the top too.
                if(bothOut&&b.max.y<2.5f){Cap(verts,uvs,norms,tris,b,along,across,half);caps++;}
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);bool isNew=mesh==null;if(isNew)mesh=new Mesh();
            mesh.Clear();mesh.name="ExteriorBrickSkin";mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);mesh.SetNormals(norms);mesh.SetUVs(0,uvs);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(isNew)AssetDatabase.CreateAsset(mesh,MeshPath);else EditorUtility.SetDirty(mesh);
            var root=new GameObject(RootName);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(root,StaticEditorFlags.BatchingStatic);
            // The entrance's cream facade and pilasters: outdoor-only pieces sharing the indoor trim material, so they took
            // the same darkening. Give just these an unlit cream render.
            var render=AssetDatabase.LoadAssetAtPath<Material>(RenderPath);
            if(render==null){render=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(render,RenderPath);}
            render.SetColor("_BaseColor",new Color(.93f,.89f,.78f));EditorUtility.SetDirty(render);
            foreach(var r in renderers.Where(r=>r.name=="Cream entrance facade"||r.name=="Facade pilaster")){Undo.RecordObject(r,"Exterior render");r.sharedMaterial=render;EditorUtility.SetDirty(r);}
            int daylit=Daylight(renderers,Outdoors);
            AssetDatabase.SaveAssets();
            Debug.Log("Exterior brick: "+daylit+" outdoor props daylit, "+faces+" outdoor wall runs, "+caps+" garden wall caps, "+verts.Count+" verts");
        }

        /// <summary>One outdoor run of wall face, cut into cells around any window or door opening in it.</summary>
        static int Skin(List<Vector3> v,List<Vector2> uv,List<Vector3> nl,List<int> tr,Vector3 plane,Vector3 n,Vector3 along,Vector3 across,float half,float from,float to,float bottom,float top,List<Bounds> openings)
        {
            float planeAcross=Vector3.Dot(plane,across);
            var holes=openings.Where(o=>Mathf.Abs(Vector3.Dot(o.center,across)-planeAcross)<half+Vector3.Dot(o.extents,Abs(across))+.05f)
                .Select(o=>(s0:Vector3.Dot(o.min,along),s1:Vector3.Dot(o.max,along),y0:o.min.y,y1:o.max.y))
                .Where(h=>h.s1>from&&h.s0<to&&h.y1>bottom&&h.y0<top).ToList();
            if(holes.Count==0){Quad(v,uv,nl,tr,plane,n,along,from,to,bottom,top);return 1;}
            var xs=new SortedSet<float>{from,to};var ys=new SortedSet<float>{bottom,top};
            foreach(var h in holes){xs.Add(Mathf.Clamp(h.s0,from,to));xs.Add(Mathf.Clamp(h.s1,from,to));ys.Add(Mathf.Clamp(h.y0,bottom,top));ys.Add(Mathf.Clamp(h.y1,bottom,top));}
            var xa=xs.ToArray();var ya=ys.ToArray();int cells=0;
            for(int i=0;i+1<xa.Length;i++)for(int j=0;j+1<ya.Length;j++)
            {
                float cs=(xa[i]+xa[i+1])*.5f,cy=(ya[j]+ya[j+1])*.5f;
                if(xa[i+1]-xa[i]<.001f||ya[j+1]-ya[j]<.001f||holes.Any(h=>cs>h.s0&&cs<h.s1&&cy>h.y0&&cy<h.y1))continue;
                Quad(v,uv,nl,tr,plane,n,along,xa[i],xa[i+1],ya[j],ya[j+1]);cells++;
            }
            return cells;
        }
        static Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));

        static Vector3 Sample(Vector3 plane,Vector3 n,Vector3 along,float start,float length,float t)
        {var p=plane+n*.3f;p+=along*(start+t*length-Vector3.Dot(p,along));p.y=1;return p;}

        static void Quad(List<Vector3> v,List<Vector2> uv,List<Vector3> nl,List<int> tr,Vector3 plane,Vector3 n,Vector3 along,float from,float to,float bottom,float top)
        {
            // Corners in the order bottom-left, top-left, top-right, bottom-right as seen from outside (clockwise = front).
            Vector3 right=Vector3.Cross(Vector3.up,-n);
            Vector3 P(float s,float y){var p=plane;p+=along*(s-Vector3.Dot(p,along));p.y=y;return p;}
            float l=Vector3.Dot(right,along)>0?from:to,r=Vector3.Dot(right,along)>0?to:from;
            int b=v.Count;
            foreach(var p in new[]{P(l,bottom),P(l,top),P(r,top),P(r,bottom)})
            {v.Add(p);nl.Add(n);uv.Add(new Vector2(Vector3.Dot(p,right)/Tile.x,p.y/Tile.y));}
            tr.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});
        }

        static void Cap(List<Vector3> v,List<Vector2> uv,List<Vector3> nl,List<int> tr,Bounds b,Vector3 along,Vector3 across,float half)
        {
            float y=b.max.y+Offset;Vector3 c=new(b.center.x,y,b.center.z);
            Vector3 ea=along*(Vector3.Dot(b.extents,along)+half),ec=across*(half+Offset);
            // Seen from above: "up" on screen is +across, right is along (clockwise winding faces up).
            Vector3 right=Vector3.Cross(across,Vector3.down);if(Vector3.Dot(right,along)<0)ea=-ea;
            int i=v.Count;
            foreach(var p in new[]{c-ea-ec,c-ea+ec,c+ea+ec,c+ea-ec}){v.Add(p);nl.Add(Vector3.up);uv.Add(new Vector2(Vector3.Dot(p,along)/Tile.x,Vector3.Dot(p,across)/Tile.y));}
            tr.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }

        const string OutdoorDir="Assets/Art/Materials/Outdoor";
        /// <summary>
        /// Outdoor props (bench, planters, canopy posts, doors, signs, notice boards) share lit materials with the inside, which
        /// the dim indoor fill leaves near black outdoors. Give the ones standing outside an outdoor copy: Pencil Surface with its
        /// daylight switch on, or URP Lit glowing its own colour. Dark Mode and blackouts still darken both.
        /// </summary>
        static int Daylight(MeshRenderer[] renderers,System.Func<Vector3,bool> outdoors)
        {
            if(!AssetDatabase.IsValidFolder(OutdoorDir))AssetDatabase.CreateFolder("Assets/Art/Materials","Outdoor");
            var copies=new Dictionary<Material,Material>();int count=0;
            foreach(var r in renderers)
            {
                if(r.name.StartsWith("Wall_")||r.name=="Ceiling"||r.name==RootName||!outdoors(r.bounds.center))continue;
                // Characters, pickups and anything that moves keep their own look.
                if(r.GetComponentInParent<RunPickup>()!=null||r.GetComponentInParent<UnityEngine.AI.NavMeshAgent>()!=null||r.GetComponentInParent<Canvas>()!=null)continue;
                var mats=r.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)
                {
                    var m=mats[i];if(m==null||m.name.EndsWith(" (outdoor)"))continue;
                    bool pencil=m.shader.name=="Confiscated/Pencil Surface",lit=m.shader.name=="Universal Render Pipeline/Lit";
                    if(!pencil&&!lit)continue;
                    if(!copies.TryGetValue(m,out var copy))
                    {
                        string path=OutdoorDir+"/"+m.name+" (outdoor).mat";
                        copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(copy==null){copy=new Material(m){name=m.name+" (outdoor)"};AssetDatabase.CreateAsset(copy,path);}
                        else copy.CopyPropertiesFromMaterial(m);
                        copy.name=m.name+" (outdoor)";
                        if(pencil)copy.SetFloat("_Outdoor",1);
                        else
                        {
                            copy.EnableKeyword("_EMISSION");copy.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                            copy.SetColor("_EmissionColor",copy.GetColor("_BaseColor")*.8f);copy.SetTexture("_EmissionMap",copy.GetTexture("_BaseMap"));
                        }
                        EditorUtility.SetDirty(copy);copies[m]=copy;
                    }
                    mats[i]=copy;changed=true;
                }
                if(changed){Undo.RecordObject(r,"Outdoor daylight");r.sharedMaterials=mats;EditorUtility.SetDirty(r);count++;}
            }
            return count;
        }

        static Material Material()
        {
            // The generated placeholder is rewritten each time (tagged in its importer); real art dropped over the PNG is left alone.
            // Art dropped over the PNG keeps the .meta, so the tag holds a hash of what was generated: any other bytes are art.
            var importer=AssetImporter.GetAtPath(TexPath);
            if(importer==null||importer.userData==Placeholder+":"+Hash128.Compute(File.ReadAllBytes(TexPath)))
            {
                var bytes=BrickTexture().EncodeToPNG();File.WriteAllBytes(TexPath,bytes);AssetDatabase.ImportAsset(TexPath);
                importer=AssetImporter.GetAtPath(TexPath);importer.userData=Placeholder+":"+Hash128.Compute(bytes);importer.SaveAndReimport();
            }
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,MatPath);}
            mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",new Color(.9f,.9f,.9f));
            EditorUtility.SetDirty(mat);return mat;
        }

        /// <summary>Placeholder art: warm red school brick in stretcher bond, light mortar, pencil-style diagonal hatching.</summary>
        static Texture2D BrickTexture()
        {
            const int W=512,H=256,Courses=8,PerRow=5;
            var t=new Texture2D(W,H,TextureFormat.RGB24,true);var rng=new System.Random(1906);
            float bw=W/(float)PerRow,bh=H/(float)Courses,mortar=4;
            var tints=new float[Courses,PerRow+1];for(int r=0;r<Courses;r++)for(int c=0;c<=PerRow;c++)tints[r,c]=(float)rng.NextDouble();
            Color brick=new(.64f,.30f,.22f),dark=new(.50f,.22f,.17f),light=new(.72f,.40f,.29f),mortarCol=new(.80f,.77f,.70f);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int row=y/(int)bh;float offset=row%2==0?0:bw*.5f;
                float bx=(x+offset)%bw,by=y%bh;int col=(int)((x+offset)/bw)%PerRow; // wraps, so the brick across the tile edge is one colour
                bool isMortar=bx<mortar||by<mortar;
                float k=tints[row,col];
                Color c=isMortar?mortarCol:Color.Lerp(Color.Lerp(dark,brick,Mathf.Clamp01(k*2)),light,Mathf.Clamp01(k*2-1));
                // Diagonal pencil strokes, as in the rest of the school's art.
                // Periodic in both directions (10 diagonal waves per width = 5 per height), so tiles meet without a seam.
                float stroke=.5f+.5f*Mathf.Sin(2*Mathf.PI*((x+y)*10f/W+.35f*Mathf.Sin(2*Mathf.PI*(x-y)*2f/W)))*.8f+.2f*(k-.5f);
                float grain=(float)rng.NextDouble();
                c*=.88f+.16f*stroke+.06f*(grain-.5f);
                if(!isMortar&&(bx<mortar+1.5f||by<mortar+1.5f))c*=.8f; // pencilled brick edge
                c.a=1;t.SetPixel(x,y,c);
            }
            t.Apply();return t;
        }
    }
}
