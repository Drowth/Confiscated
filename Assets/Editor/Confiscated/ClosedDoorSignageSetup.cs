using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Doors shut for the whole run read as shut from down the corridor: a taped "PLEASE USE OTHER DOOR" notice whose
    /// arrow points at the nearest usable door, and a classroom chair wedged under the handle. Chained into
    /// SchoolRunSetup.Build after PlaytestFixSetup. Art from the Astra loop batch (Docs/ASSET_REQUESTS_Astra_Loop.md).
    /// </summary>
    public static class ClosedDoorSignageSetup
    {
        public const string GroupName="Closed door signage",ChairModel="ClassroomChair";
        const string Textures="Assets/Art/Textures/",Models="Assets/Art/Models/SchoolProps/",Materials="Assets/Art/Materials/";
        const float NoticeWidth=.34f,NoticeHeight=.51f,NoticeCentreY=1.02f,ChairHeight=.78f,ChairTilt=24f;

        [MenuItem("Confiscated/School Run/Apply Closed Door Signage")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            int n=ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[ClosedDoorSignage] Signed "+n+" closed door faces.");
        }

        /// <summary>Repeatable. Does not save; callers own that. Returns the number of door faces signed.</summary>
        public static int ApplyToScene()
        {
            ImportCutout("T_Notice_UseOtherDoor");ImportCutout("T_Notice_UseOtherDoor_Arrow");
            var notice=Cutout("M_Notice_UseOtherDoor","T_Notice_UseOtherDoor");var arrow=Cutout("M_Notice_UseOtherDoor_Arrow","T_Notice_UseOtherDoor_Arrow");
            var doors=Object.FindObjectsByType<OfficeDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var usable=doors.Where(d=>!d.closedForRun&&!d.mainExit).Select(d=>d.transform.position).ToArray();
            var shut=doors.Where(d=>d.closedForRun&&!d.mainExit).Select(d=>d.hinge.GetComponentsInChildren<Collider>().First(c=>c.name=="Leaf").bounds.center).ToArray();
            int faces=0;
            foreach(var door in doors.Where(d=>d.closedForRun&&!d.mainExit))
            {
                foreach(var old in door.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="School door notice"||t.name==GroupName).ToArray())Object.DestroyImmediate(old.gameObject);
                var leaf=door.hinge.GetComponentsInChildren<Collider>().First(c=>c.name=="Leaf");
                var group=new GameObject(GroupName).transform;group.SetParent(door.transform,false);
                Vector3 centre=leaf.bounds.center;centre.y=0;
                Vector3 normal=leaf.transform.forward;normal.y=0;normal.Normalize();
                float half=Mathf.Abs(Vector3.Dot(leaf.bounds.extents,new Vector3(Mathf.Abs(normal.x),0,Mathf.Abs(normal.z))));
                foreach(float sign in new[]{-1f,1f})
                {
                    Vector3 n=normal*sign;
                    // Only faces a player can stand in front of: some doors back onto the yard or a wall.
                    if(!NavMesh.SamplePosition(centre+n*1.1f,out _,.5f,NavMesh.AllAreas)||!Reachable(centre+n*1.1f,usable,shut))continue;
                    var face=new GameObject(sign<0?"Back face":"Front face").transform;face.SetParent(group,false);
                    face.position=centre+n*(half+.004f);face.rotation=Quaternion.LookRotation(-n,Vector3.up);
                    var paper=Quad("Use other door notice",face,new Vector3(0,NoticeCentreY,0),new Vector2(NoticeWidth,NoticeHeight),notice);
                    int toward=OtherDoorSide(door,doors,centre,n,shut);
                    if(toward!=0)
                    {
                        var a=Quad("Other door arrow",face,new Vector3(0,NoticeCentreY-NoticeHeight*.29f,-.002f),new Vector2(NoticeWidth*.62f,NoticeWidth*.62f),arrow);
                        if(toward<0)a.transform.localRotation=Quaternion.Euler(0,0,180);
                    }
                    Chair(face,n);
                    faces++;
                }
                EditorUtility.SetDirty(door.gameObject);
            }
            return faces;
        }

        /// <summary>
        /// Whether a player can stand here: the edit-time NavMesh still runs through closed doors (they only carve at runtime),
        /// so some usable door must be reachable without squeezing through any of them. The inside of a shut room fails this.
        /// </summary>
        static bool Reachable(Vector3 from,Vector3[] usable,Vector3[] shut)
        {
            if(!NavMesh.SamplePosition(from,out var a,.6f,NavMesh.AllAreas))return false;
            foreach(var door in usable.OrderBy(u=>(u-from).sqrMagnitude).Take(6))
            {
                if(!NavMesh.SamplePosition(door,out var b,1.5f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                if(!ThroughShut(path.corners,shut))return true;
            }
            return false;
        }
        static bool ThroughShut(Vector3[] corners,Vector3[] shut)
        {
            for(int i=1;i<corners.Length;i++)
                for(float t=0;t<=1;t+=.05f)
                {
                    Vector3 p=Vector3.Lerp(corners[i-1],corners[i],t);
                    foreach(var s in shut)if(new Vector2(p.x-s.x,p.z-s.z).sqrMagnitude<.45f*.45f)return true;
                }
            return false;
        }

        /// <summary>+1 / -1: the nearest usable door is to the viewer's right / left; 0 when none is close enough to point at.</summary>
        static int OtherDoorSide(OfficeDoor closed,OfficeDoor[] doors,Vector3 at,Vector3 viewerSide,Vector3[] shut)
        {
            Vector3 eye=at+viewerSide*1.1f;OfficeDoor best=null;float bestLength=22;
            foreach(var d in doors)
            {
                if(d==closed||d.closedForRun||d.mainExit)continue;
                var path=new NavMeshPath();
                if(!NavMesh.SamplePosition(eye,out var a,.6f,NavMesh.AllAreas)||!NavMesh.SamplePosition(d.transform.position,out var b,1.5f,NavMesh.AllAreas))continue;
                if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||ThroughShut(path.corners,shut))continue;
                float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                if(length<bestLength){bestLength=length;best=d;}
            }
            if(best==null)return 0;
            Vector3 right=Vector3.Cross(Vector3.up,-viewerSide);
            float side=Vector3.Dot(best.transform.position-at,right);
            return Mathf.Abs(side)<.5f?0:side>0?1:-1;
        }

        /// <summary>A classroom chair leaning back against the door with its backrest under the handle.</summary>
        static void Chair(Transform face,Vector3 viewerSide)
        {
            var visual=new GameObject("Wedged chair").transform;visual.SetParent(face,false);
            if(!PickupModelSetup.Place(visual,ChairModel,ChairHeight,0,0,Models))return;
            var holder=visual.GetChild(0);
            // Turn the backrest to the door: the tallest part of the model is the backrest.
            Vector3 back=BackrestDirection(holder);float yaw=Vector3.SignedAngle(back,-viewerSide,Vector3.up);
            holder.RotateAround(visual.position,Vector3.up,yaw);
            // Tip it back so the top of the backrest meets the door, then stand it on the floor against the leaf.
            // Fit from the vertices: a tilted chair's bounding box is far from its real outline.
            holder.RotateAround(visual.position,Vector3.Cross(Vector3.up,-viewerSide),ChairTilt);
            var points=Vertices(holder);
            float nearest=points.Min(p=>Vector3.Dot(p-face.position,viewerSide)),lowest=points.Min(p=>p.y);
            holder.position+=viewerSide*(.01f-nearest)+Vector3.up*(face.position.y-lowest);
            // Toward the handle (the leaf's free edge) and clear of the notice.
            holder.position+=face.right*.18f;
            foreach(var r in holder.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.On;
            // Solid, but never between the player's interaction ray and the door (Ignore Raycast is skipped by PlayerInteractor.Resolve).
            var box=visual.gameObject.AddComponent<BoxCollider>();var bounds=Bounds(holder);
            box.center=visual.InverseTransformPoint(bounds.center);box.size=Abs(visual.InverseTransformVector(bounds.size));
            visual.gameObject.layer=PlaytestFixSetup.IgnoreRaycastLayer;
        }
        static Vector3 BackrestDirection(Transform holder)
        {
            var bounds=Bounds(holder);float cut=bounds.min.y+bounds.size.y*.8f;Vector3 sum=Vector3.zero;int count=0;
            foreach(var f in holder.GetComponentsInChildren<MeshFilter>())
            {
                var v=f.sharedMesh.vertices;int step=Mathf.Max(1,v.Length/4000);
                for(int i=0;i<v.Length;i+=step){var w=f.transform.TransformPoint(v[i]);if(w.y>=cut){sum+=w;count++;}}
            }
            Vector3 dir=count>0?sum/count-bounds.center:holder.forward;dir.y=0;
            return dir.sqrMagnitude>1e-4f?dir.normalized:holder.forward;
        }
        static Vector3[] Vertices(Transform root)=>root.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
        static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static Bounds Bounds(Transform root){var rs=root.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}

        static GameObject Quad(string name,Transform parent,Vector3 local,Vector2 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=local;g.transform.localScale=new Vector3(size.x,size.y,1);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return g;
        }
        static void ImportCutout(string texture)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Textures+texture+".png");
            if(importer==null)throw new InvalidOperationException("Missing "+Textures+texture+".png");
            if(importer.alphaIsTransparency&&importer.wrapMode==TextureWrapMode.Clamp)return;
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();
        }
        /// <summary>Matte alpha-clipped paper that follows the corridor lighting.</summary>
        static Material Cutout(string name,string texture)
        {
            string path=Materials+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+texture+".png"));m.SetColor("_BaseColor",Color.white);
            SchoolLightingSetup.ConfigureArtworkMaterial(m,true);return m;
        }
    }
}
