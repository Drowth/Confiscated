using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// The secret isolation ("reflection") room, cut from the south-east corner of the never-opened South room A with its
    /// one door on the south-west corridor wall (Docs/IsolationRoom.md). Narrow on purpose (the level is hand-built): it
    /// only ever destroys its own root, and it deactivates the one corridor wall piece (Wall_35) it replaces with pieces
    /// that leave the (boarded) doorway. The NavMesh is not rebaked; staff can't open the door anyway.
    /// </summary>
    public static class IsolationRoomSetup
    {
        const string RootName="Isolation room (secret)",MeshDir="Assets/Art/Meshes/Isolation/",MatDir="Assets/Art/Materials/";
        // Cell interior (world): west partition inner face .. corridor wall's inner face, south wall .. north partition.
        const float X0=-18.2f,X1=-14.96f,Z0=3.63f,Z1=6.9f,DoorZ=5.3f;
        const float WallX=-14.885f,WallT=.15f,Opening0=4.43f,Opening1=6.17f,Header=2.29f;
        static readonly Color Graphite=new(.16f,.16f,.18f),Chalk=new(.86f,.84f,.76f);
        static Transform root;

        [MenuItem("Confiscated/Secret/Build Isolation Room")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play first.");
            var old=GameObject.Find(RootName);if(old!=null)Object.DestroyImmediate(old);
            var wall=GameObject.Find("School/Walls")?.transform.Find("Wall_35")?.gameObject; // inactive after a first build
            if(wall==null)throw new InvalidOperationException("School/Walls/Wall_35 (the south-west corridor's west wall by South room A) is missing.");
            var template=GameObject.Find("School/Doors/South room A east");
            if(template==null||template.GetComponent<OfficeDoor>()==null)throw new InvalidOperationException("Door template 'South room A east' is missing.");
            Directory.CreateDirectory(MeshDir);
            root=new GameObject(RootName).transform;
            var room=root.gameObject.AddComponent<IsolationRoom>();
            var wallMat=AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Wall_Corridor.mat");
            var cellMat=Tinted("M_Isolation_Wall",wallMat,new Color(.5f,.53f,.54f));

            // ---- Shell: the corridor wall with a doorway, two partitions inside South room A, grey lining inside.
            wall.SetActive(false);
            var shell=Group("Shell",root);
            Solid("Corridor wall south of door",shell,new Vector3(WallX,1.5f,(3.56f+Opening0)/2),new Vector3(WallT,3,Opening0-3.56f),wallMat);
            Solid("Corridor wall north of door",shell,new Vector3(WallX,1.5f,(Opening1+9.23f)/2),new Vector3(WallT,3,9.23f-Opening1),wallMat);
            Solid("Door header",shell,new Vector3(WallX,(Header+3)/2,(Opening0+Opening1)/2),new Vector3(WallT,3-Header,Opening1-Opening0),wallMat);
            Solid("West partition",shell,new Vector3(X0-.075f,1.5f,(3.56f+Z1+.15f)/2),new Vector3(.15f,3,Z1+.15f-3.56f),cellMat);
            Solid("North partition",shell,new Vector3((X0-.15f+X1)/2,1.5f,Z1+.075f),new Vector3(X1-X0+.15f,3,.15f),cellMat);
            Solid("Lining south",shell,new Vector3((X0+X1)/2,1.5f,Z0+.01f),new Vector3(X1-X0,3,.02f),cellMat,false);
            Solid("Lining east south",shell,new Vector3(X1-.01f,1.5f,(Z0+Opening0)/2),new Vector3(.02f,3,Opening0-Z0),cellMat,false);
            Solid("Lining east north",shell,new Vector3(X1-.01f,1.5f,(Opening1+Z1)/2),new Vector3(.02f,3,Z1-Opening1),cellMat,false);
            Solid("Lining east header",shell,new Vector3(X1-.01f,(Header+3)/2,(Opening0+Opening1)/2),new Vector3(.02f,3-Header,Opening1-Opening0),cellMat,false);

            // ---- The way in: the old door frame, the doorway boarded over with plywood and nailed planks. The plywood stops
            // 4 cm short of the floor, so the bulb's light shows underneath (the strip on the corridor floor, below).
            var frame=Object.Instantiate(template,root);frame.name="Boarded doorway frame";
            frame.transform.position=template.transform.position+Vector3.forward*(DoorZ-template.transform.position.z);
            frame.transform.rotation=template.transform.rotation;
            foreach(var t in frame.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Closed door signage"||t.name=="Hinge1"||t.name=="SignPlate").ToArray())
                if(t!=null)Object.DestroyImmediate(t.gameObject);
            Object.DestroyImmediate(frame.GetComponent<OfficeDoor>());
            var boarded=Group("Boarded doorway",root);boarded.position=new Vector3(WallX,0,DoorZ);
            var wood=Tinted("M_Isolation_Planks",AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Wood_Desk.mat"),new Color(.86f,.8f,.7f));
            var ply=Tinted("M_Isolation_Plywood",AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Wood_Desk.mat"),new Color(.5f,.47f,.42f));
            var nail=AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Chapter_Grey.mat");
            var sheet=Plank("Plywood sheet",boarded,new Vector3(WallX,.04f+(2.2f-.04f)/2,DoorZ),new Vector3(.03f,2.2f-.04f,1.52f),0,ply);
            var pulled=new System.Collections.Generic.List<Transform>();
            float[,] rows={{.42f,-5},{.86f,4},{1.3f,-3},{1.74f,6},{2.08f,-2}};
            for(int i=rows.GetLength(0)-1;i>=0;i--)
            {
                var plank=Plank("Plank "+(i+1),boarded,new Vector3(-14.795f,rows[i,0],DoorZ),new Vector3(.025f,.16f,1.9f),rows[i,1],wood);
                foreach(float end in new[]{-.84f,.84f})Nail(plank,end,nail);
                pulled.Add(plank);
            }
            var brace=Plank("Diagonal brace",boarded,new Vector3(-14.768f,1.2f,DoorZ),new Vector3(.025f,.15f,2.35f),38,wood);
            foreach(float end in new[]{-1.05f,1.05f,0f})Nail(brace,end,nail);
            pulled.Insert(0,brace);pulled.Add(sheet);
            var doorway=boarded.gameObject.AddComponent<BoardedDoorway>();doorway.boards=pulled.ToArray();doorway.holdSeconds=1.5f;
            room.boards=doorway;
            // The cell side of the plywood: the same word over and over, scratched with a compass point.
            var scratched=Text(boarded,"sorry sorry sorry sorry\nsorry sorry sorry\nsorry sorry sorry sorry\nsorry sorry",new Vector3(WallX-.018f,.95f,DoorZ),.011f,Graphite);
            scratched.localRotation=Quaternion.Euler(0,90,0);scratched.SetParent(sheet,true);
            var sign=GameObject.Find("School/Details/South room A east sign");
            if(sign!=null)
            {
                var copy=Object.Instantiate(sign,root);copy.name="Reflection room sign";
                copy.transform.position=sign.transform.position+Vector3.forward*(DoorZ-template.transform.position.z);copy.transform.rotation=sign.transform.rotation;
                foreach(var tm in copy.GetComponentsInChildren<TextMesh>(true))tm.text="REFLECTION ROOM";
            }

            // ---- Dressing: one desk facing the bare west wall, the lines, the rules, a clock with no hands, a bare bulb.
            var dress=Group("Dressing",root);
            var place=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Detention place 2");
            if(place!=null)
            {
                var desk=Object.Instantiate(place.gameObject,dress);desk.name="Isolation desk";
                // The place faces +Z (chair behind the desk); turned to face the west wall, desk front .5 m from it.
                desk.transform.SetPositionAndRotation(new Vector3(X0+.55f,0,DoorZ-.2f),Quaternion.Euler(0,-90,0));
            }
            SchoolRunSetupNotice(dress,new Vector3(X0+.012f,2.42f,DoorZ-.2f),-90,"THINK ABOUT WHAT\nYOU HAVE DONE.");
            var lines=string.Join("\n",Enumerable.Repeat("I will not be naughty.",9))+"\nI will not be naughty. I will not\nI will not   I WILL NOT\nlet me out";
            var written=Text(dress,lines,new Vector3(X0+.004f,1.45f,DoorZ-.95f),.0105f,Graphite);written.localRotation=Quaternion.Euler(0,-90,0);written.name="Lines on the wall";
            SchoolRunSetupNotice(dress,new Vector3(-16.1f,1.55f,Z1-.012f),0,"REFLECTION ROOM RULES\n1. Sit still.\n2. Face the wall.\n3. Do not speak.\n4. You will be let out\nwhen you are sorry.");
            // Tally marks scratched low on the south wall, by the door, where a child sitting on the floor would reach.
            var tally=Text(dress,"|||| |||| |||| |||| |||| |||| ||||\n|||| |||| |||| |||| |||| ||\n|||| |||| |||| |||| |",new Vector3(-15.7f,.42f,Z0+.024f),.009f,Graphite);
            tally.localRotation=Quaternion.Euler(0,180,0);tally.name="Tally marks";
            var clock=Group("Clock with no hands",dress);clock.position=new Vector3(-17.3f,2.35f,Z1-.03f);
            Cylinder("Rim",clock,Vector3.zero,new Vector3(.34f,.02f,.34f),AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Chapter_Grey.mat"));
            Cylinder("Face",clock,new Vector3(0,0,-.012f),new Vector3(.3f,.01f,.3f),AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Chapter_Paper.mat"));
            var twelve=Text(clock,"12",new Vector3(0,.1f,-.02f),.006f,Graphite);
            var six=Text(clock,"6",new Vector3(0,-.1f,-.02f),.006f,Graphite);
            // A crumpled page on the floor.
            var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Crumpled lines";ball.transform.SetParent(dress,false);
            ball.transform.position=new Vector3(-15.9f,.06f,4.1f);ball.transform.localScale=new Vector3(.13f,.1f,.12f);ball.transform.rotation=Quaternion.Euler(20,40,10);
            Object.DestroyImmediate(ball.GetComponent<Collider>());IllustratedArtSetup.Tiled(ball.GetComponent<MeshRenderer>(),AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Chapter_Paper.mat"),"isolation",1);

            // ---- The bulb: bare, on a flex, faintly unsteady. Dark Mode leaves it on (IsolationRoom).
            var lamp=Group("Bare bulb",root);lamp.position=new Vector3((X0+X1)/2,2.5f,(Z0+Z1)/2);
            var flex=Cylinder("Flex",lamp,new Vector3(0,.26f,0),new Vector3(.012f,.24f,.012f),AssetDatabase.LoadAssetAtPath<Material>(MatDir+"M_Chapter_Grey.mat"));
            var glass=GameObject.CreatePrimitive(PrimitiveType.Sphere);glass.name="Bulb";glass.transform.SetParent(lamp,false);glass.transform.localScale=Vector3.one*.09f;
            Object.DestroyImmediate(glass.GetComponent<Collider>());glass.GetComponent<MeshRenderer>().sharedMaterial=Unlit("M_Isolation_Bulb",new Color(1,.86f,.55f),false);
            glass.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var bulb=new GameObject("Bulb light").AddComponent<Light>();bulb.transform.SetParent(lamp,false);bulb.transform.localPosition=Vector3.down*.08f;
            bulb.type=LightType.Point;bulb.color=new Color(1,.78f,.5f);bulb.intensity=1.6f;bulb.range=4.2f;bulb.shadows=LightShadows.Soft;
            var flicker=bulb.gameObject.AddComponent<LightFlicker>();flicker.mode=LightFlicker.Mode.Bulb;flicker.baseIntensity=1.6f;flicker.depth=.2f;
            room.bulb=bulb;

            // ---- Light under the boards, on the corridor floor.
            var leak=Group("Light under the door",root);leak.position=new Vector3(WallX+WallT/2,.004f,DoorZ);
            // Soft falloff from overlapping strips, plus the bright slot itself under the plywood.
            Strip(leak,.03f,.45f);Strip(leak,.09f,.22f);Strip(leak,.18f,.12f);Strip(leak,.32f,.06f);
            var slot=GameObject.CreatePrimitive(PrimitiveType.Quad);slot.name="Lit gap";slot.transform.SetParent(leak,false);
            Object.DestroyImmediate(slot.GetComponent<Collider>());
            slot.transform.position=new Vector3(WallX+.02f,.022f,DoorZ);slot.transform.rotation=Quaternion.Euler(0,-90,0);slot.transform.localScale=new Vector3(1.5f,.036f,1);
            slot.GetComponent<MeshRenderer>().sharedMaterial=Unlit("M_Isolation_Bulb",new Color(1,.86f,.55f),false);
            slot.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            room.lightUnderDoor=leak.gameObject;

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject=root.gameObject;
            Debug.Log("[IsolationRoom] Built the secret reflection room in South room A's south-east corner (door on the south-west corridor at z "+DoorZ+"). Wall_35 deactivated and replaced.");
        }

        [MenuItem("Confiscated/Secret/Remove Isolation Room")]
        public static void Remove()
        {
            var old=GameObject.Find(RootName);if(old!=null)Object.DestroyImmediate(old);
            var wall=GameObject.Find("School/Walls")?.transform.Find("Wall_35");if(wall!=null)wall.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        static Transform Group(string name,Transform parent){var g=new GameObject(name).transform;g.SetParent(parent,false);return g;}
        /// <summary>Wall box with the corridor walls' world mapping (u = along/2, v = y/3), saved as a mesh asset.</summary>
        static void Solid(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collide=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=p;
            var filter=g.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);
            var v=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;
            for(int i=0;i<v.Length;i++)
            {
                v[i]=Vector3.Scale(v[i],size);Vector3 w=p+v[i];
                uv[i]=Mathf.Abs(n[i].y)>.5f?new Vector2(w.x,w.z):new Vector2((Mathf.Abs(n[i].x)>.5f?w.z:w.x)/2,w.y/3);
            }
            mesh.vertices=v;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=MeshDir+name.Replace(' ','_')+".asset";mesh.name=Path.GetFileNameWithoutExtension(path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            filter.sharedMesh=saved;g.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(collide)g.GetComponent<BoxCollider>().size=size;else Object.DestroyImmediate(g.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic);
        }
        static GameObject Cylinder(string name,Transform parent,Vector3 local,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=local;
            // Clock parts face the room (-Z); the flex hangs straight down.
            if(name!="Flex")g.transform.localRotation=Quaternion.Euler(90,0,0);
            g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());
            if(mat!=null)IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),mat,"isolation",1);
            return g;
        }
        static Transform Text(Transform parent,string value,Vector3 world,float size,Color colour)
        {
            var g=new GameObject(value.Split('\n')[0]).transform;g.SetParent(parent,false);g.position=world;
            var t=g.gameObject.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=72;t.characterSize=size;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Left;t.text=value;t.color=colour;
            g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;g.gameObject.AddComponent<WorldLabel>();
            g.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        /// <summary>A board: pencil wood box, long along world z, tilted `tilt` degrees about the wall's normal.</summary>
        static Transform Plank(string name,Transform parent,Vector3 at,Vector3 size,float tilt,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
            g.transform.position=at;g.transform.rotation=Quaternion.Euler(tilt,0,0);g.transform.localScale=size;
            IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),mat,"isolation",1);
            return g.transform;
        }
        static void Nail(Transform plank,float along,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Nail";Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(plank,false);g.transform.localPosition=new Vector3(.6f,0,along/plank.localScale.z);
            g.transform.localScale=new Vector3(.4f,.018f/plank.localScale.y,.018f/plank.localScale.z);
            g.GetComponent<MeshRenderer>().sharedMaterial=mat;g.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static void SchoolRunSetupNotice(Transform parent,Vector3 p,float yaw,string text)
        {
            var notice=SchoolRunSetup.Notice(p,yaw,text);notice.SetParent(parent,true);notice.name="Notice - "+text.Split('\n')[0];
        }
        static void Strip(Transform parent,float depth,float alpha)
        {
            var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Glow "+depth;q.transform.SetParent(parent,false);
            q.transform.localPosition=new Vector3(depth/2,0,0);q.transform.localRotation=Quaternion.Euler(90,0,0);q.transform.localScale=new Vector3(depth,Opening1-Opening0-.3f,1);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var r=q.GetComponent<MeshRenderer>();r.sharedMaterial=Unlit("M_Isolation_LightLeak_"+Mathf.RoundToInt(alpha*100),new Color(1,.74f,.38f,alpha),true);
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }
        static Material Tinted(string name,Material source,Color tint)
        {
            string path=MatDir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(source){name=name};AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
            m.SetColor("_BaseColor",tint);EditorUtility.SetDirty(m);return m;
        }
        static Material Unlit(string name,Color colour,bool transparent)
        {
            string path=MatDir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",colour);
            if(transparent)
            {
                m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);
                m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");
            }
            EditorUtility.SetDirty(m);return m;
        }
    }
}
