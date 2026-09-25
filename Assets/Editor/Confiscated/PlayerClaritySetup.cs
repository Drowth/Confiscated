using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Repeatable signs, newsletter tray and five matching property boxes.</summary>
    public static class PlayerClaritySetup
    {
        const string RootName="Player guidance";
        static Material yellow,green,paper,ink;
        static Transform root;
        [MenuItem("Confiscated/Playtest/Apply Player Clarity Pass")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play first.");
            ApplyToScene();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene()
        {
            var period=Object.FindFirstObjectByType<SchoolPeriodController>();
            if(period==null)return;
            var old=GameObject.Find(RootName);if(old!=null)Object.DestroyImmediate(old);
            root=new GameObject(RootName).transform;
            yellow=Surface("M_Guidance_Mustard",new Color(.87f,.65f,.18f));
            green=Surface("M_Guidance_Green",new Color(.15f,.28f,.21f));
            paper=Surface("M_Guidance_Paper",new Color(.95f,.91f,.76f));
            ink=Surface("M_Guidance_Ink",new Color(.08f,.12f,.1f));
            ConfiscatedBoxArtSetup.ApplyToScene();
            foreach(var pickup in Object.FindObjectsByType<RunPickup>(FindObjectsSortMode.None))BoxStation(pickup);
            DeliveryTray(period);
            // At the classroom exit, turn right into the office corridor; then follow northward signs.
            Sign("Office turn at Year 6",new Vector3(-35.49f,2.55f,25.89f),270,"OFFICE  >",new Vector2(1.6f,.48f),yellow);
            Sign("Office corridor junction",new Vector3(-33.85f,2.25f,34.8f),0,"OFFICE\nSTRAIGHT AHEAD",new Vector2(1.9f,.63f),yellow);
            Sign("Office corridor midway",new Vector3(-33.85f,2.25f,65),0,"OFFICE\nNEWSLETTER DELIVERY",new Vector2(1.9f,.63f),yellow);
            Sign("Office destination",new Vector3(-35.47f,1.9f,79.3f),270,"OFFICE\nNEWSLETTERS HERE",new Vector2(1.65f,.63f),yellow);
            Sign("Dining return direction",new Vector3(-32.3f,1.95f,77.5f),90,"DINING HALL  >\nOFFICE KEY ON TROLLEY",new Vector2(1.9f,.63f),paper);
            foreach(var door in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))
            {
                string name=door.name=="North room B south"?"RESOURCES":door.name=="East room B west"?"EQUIPMENT":door.name=="Store cupboard"?"STORE":null;
                if(name!=null)Sign(name+" property sign",door.transform.position-door.transform.forward*.12f+Vector3.up*2.12f,door.transform.eulerAngles.y,name+"\nCONFISCATED PROPERTY",new Vector2(1.7f,.55f),paper);
            }
            UpdateDescriptions();
            var surface=Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if(surface!=null&&surface.navMeshData!=null&&AssetDatabase.Contains(surface.navMeshData))DiningHallSetup.Rebake();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        static void BoxStation(RunPickup pickup)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(ConfiscatedBoxArtSetup.VisualPrefabPath);
            if(asset==null)throw new System.InvalidOperationException("Generated box prefab is missing.");
            var old=pickup.transform.Find("Confiscation box");if(old!=null)Object.DestroyImmediate(old.gameObject);
            pickup.transform.rotation=Quaternion.Euler(0,pickup.itemId==1?180:pickup.itemId>=3?90:0,0);
            var shelf=pickup.transform.Find("Property shelf");if(shelf!=null)shelf.localPosition=new Vector3(0,-.08f,0);
            int legIndex=0;
            foreach(Transform child in pickup.transform)if(child.name=="Display stand leg")
            {
                child.localPosition=new Vector3(legIndex<2?-.4f:.4f,-pickup.transform.position.y*.5f-.05f,legIndex%2==0?-.24f:.24f);legIndex++;
            }
            var box=(GameObject)PrefabUtility.InstantiatePrefab(asset,pickup.transform);box.name="Confiscation box";
            box.transform.localPosition=new Vector3(0,-.02f,0);box.transform.localScale=Vector3.one*1.5f;
            var hit=pickup.GetComponent<BoxCollider>();if(hit!=null){hit.center=new Vector3(0,.29f,0);hit.size=new Vector3(.92f,.64f,.69f);}
            // Fit the existing generated belonging inside the box; keep it as the pickup's separate hideable visual.
            var visual=pickup.visual.transform;visual.localPosition=Vector3.zero;
            var renderers=visual.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                Bounds b=new Bounds();bool first=true;
                foreach(var r in renderers)
                {
                    var wb=r.bounds;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    {
                        var p=pickup.transform.InverseTransformPoint(wb.center+Vector3.Scale(wb.extents,new Vector3(x,y,z)));
                        if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
                    }
                }
                float fit=Mathf.Min(1,.70f/Mathf.Max(.001f,b.size.x),.30f/Mathf.Max(.001f,b.size.y),.38f/Mathf.Max(.001f,b.size.z));
                visual.localScale*=fit;
                visual.localPosition=new Vector3(-b.center.x*fit,.10f-b.min.y*fit,-.03f-b.center.z*fit);
            }
            var motion=visual.GetComponent<CollectibleMotion>();if(motion==null)motion=visual.gameObject.AddComponent<CollectibleMotion>();
            motion.spin=false;motion.lift=.035f;motion.bob=.02f;
            EditorUtility.SetDirty(pickup);EditorUtility.SetDirty(visual);EditorUtility.SetDirty(motion);
        }
        static void DeliveryTray(SchoolPeriodController period)
        {
            var tray=period.deliveryTray.transform;
            tray.localScale=Vector3.one;
            // The tray's back/poles were authored assuming a -Z approach; the corridor wall here actually runs along Z, so square it up.
            tray.rotation=Quaternion.Euler(0,270,0);
            var old=tray.Find("Newsletter tray artwork");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var renderer=tray.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=false;
            var hit=tray.GetComponent<BoxCollider>();hit.center=new Vector3(0,.055f,0);hit.size=new Vector3(.94f,.16f,.57f);
            var art=new GameObject("Newsletter tray artwork").transform;art.SetParent(tray,false);
            Part(art,"Yellow tray base",Vector3.zero,new Vector3(.9f,.035f,.55f),yellow);
            foreach(float x in new[]{-.44f,.44f})Part(art,"Tray side",new Vector3(x,.07f,0),new Vector3(.025f,.14f,.55f),yellow);
            Part(art,"Tray back",new Vector3(0,.07f,.265f),new Vector3(.9f,.14f,.025f),yellow);
            Part(art,"Tray front",new Vector3(0,.045f,-.265f),new Vector3(.9f,.09f,.025f),yellow);
            foreach(float x in new[]{-.39f,.39f})Part(art,"Notice support",new Vector3(x,.31f,.30f),new Vector3(.025f,.62f,.025f),green);
            var stack=tray.Find("Delivered papers");if(stack!=null){stack.localPosition=new Vector3(0,.065f,0);stack.localRotation=Quaternion.identity;stack.localScale=new Vector3(.57f,.075f,.37f);}
            Sign("NEWSLETTERS tray label",tray.position+new Vector3(0,.035f,-.288f),0,"NEWSLETTERS",new Vector2(.86f,.12f),paper);
            Sign("Newsletter stand notice",new Vector3(-35.40f,1.40f,79.30f),270,"NEWSLETTERS\nDELIVER HERE",new Vector2(1.05f,.38f),yellow);
            var previous=GameObject.Find("FirstPeriod/Office delivery sign");if(previous!=null)previous.SetActive(false);
            foreach(var text in period.GetComponentsInChildren<TextMesh>(true))if(text.text.StartsWith("OFFICE DELIVERIES"))text.gameObject.SetActive(false);
            EditorUtility.SetDirty(tray);EditorUtility.SetDirty(hit);EditorUtility.SetDirty(renderer);
        }
        static Material Surface(string name,Color color)
        {
            string path="Assets/Art/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Chapter_Green.mat"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;
        }
        static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
            Object.DestroyImmediate(g.GetComponent<Collider>());IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(),material,"guidance",1);
        }
        static void Sign(string name,Vector3 position,float yaw,string words,Vector2 size,Material material)
        {
            var sign=new GameObject(name).transform;sign.SetParent(root,false);sign.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            Part(sign,"Pencil signboard",Vector3.zero,new Vector3(size.x,size.y,.035f),material);
            var go=new GameObject("Lettering");go.transform.SetParent(sign,false);go.transform.localPosition=new Vector3(0,0,-.022f);
            var text=go.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.text=words;text.fontSize=64;text.characterSize=.055f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.04f,.07f,.05f);
            text.font.RequestCharactersInTexture(words,64);go.AddComponent<WorldLabel>();
            var bounds=go.GetComponent<Renderer>().bounds;float width=Mathf.Max(bounds.size.x,bounds.size.z);
            if(width>.001f)text.characterSize*=Mathf.Min((size.x-.10f)/width,(size.y-.025f)/Mathf.Max(.001f,bounds.size.y));
        }
        static void UpdateDescriptions()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:InventoryItemDefinition"))
            {
                var item=AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                item.description=item.kind switch {
                    InventoryItemKind.OfficeKey=>"Carry this to the caretaker's office door and press F to unlock it.",
                    InventoryItemKind.HallPass=>"Mr Reed's signed pass. Press F on the caretaker when he stops you.",
                    InventoryItemKind.Newsletters=>"Follow the OFFICE signs to the yellow NEWSLETTERS tray. Press F to deliver.",
                    InventoryItemKind.Football=>"Press 1 to hold or put away. Left click to throw a noisy distraction.",
                    InventoryItemKind.Phone=>"Your recovered phone. It is quiet and safe when stored in your locker.",
                    _=>item.description};EditorUtility.SetDirty(item);
            }
        }
    }
}
