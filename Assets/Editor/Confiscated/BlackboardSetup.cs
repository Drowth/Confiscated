using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Confiscated.EditorTools
{
    public static class BlackboardSetup
    {
        [MenuItem("Confiscated/Detention/Install Blackboard Cleaning")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            Apply(Object.FindFirstObjectByType<DetentionController>());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        }
        public static void Apply(DetentionController d)
        {
            if(d==null)return;
            foreach(var text in d.GetComponentsInChildren<TextMesh>().Where(t=>t.text=="DETENTION"||t.text.StartsWith("Miss D Tenison\nTake a seat")))Object.DestroyImmediate(text.gameObject);
            foreach(var old in d.GetComponentsInChildren<DetentionBlackboard>())Object.DestroyImmediate(old.gameObject);
            foreach(var old in d.GetComponentsInChildren<RubberCleaningStation>())Object.DestroyImmediate(old.gameObject);
            var props=d.transform.Find("Cleaning task furnishings");if(props!=null)Object.DestroyImmediate(props.gameObject);
            var group=new GameObject("Cleaning task furnishings").transform;group.SetParent(d.transform,false);
            var paper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/S_Inventory_Paper.asset");
            const string woodPath="Assets/Art/UI/S_Blackboard_Wood.asset";
            var wood=AssetDatabase.LoadAssetAtPath<Sprite>(woodPath);
            if(wood==null){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/T_Wood_Pencil_v2.png");wood=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);AssetDatabase.CreateAsset(wood,woodPath);}
            var positions=new[]{new Vector3(0,1.8f,3.574f),new Vector3(3.24f,1.8f,-.7f),new Vector3(0,1.8f,-3.6f)};
            d.blackboards=new DetentionBlackboard[3];
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Blackboard "+(i+1));go.transform.SetParent(d.transform,false);go.transform.localPosition=positions[i];go.transform.localRotation=Quaternion.Euler(0,i*90,0);
                var col=go.AddComponent<BoxCollider>();col.size=new Vector3(2.85f,.87f,.035f);
                var b=go.AddComponent<DetentionBlackboard>();b.detention=d;b.boardNumber=i+1;b.chalkFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/FrederickatheGreat-Regular.ttf");b.boardPaper=paper;b.woodPaper=wood;d.blackboards[i]=b;
                if(i>0){var frame=new GameObject("Board frame "+(i+1)).transform;frame.SetParent(group,false);frame.localPosition=positions[i];frame.localRotation=go.transform.localRotation;Box(frame,"Wood frame",new Vector3(0,0,.07f),new Vector3(3.05f,1.04f,.09f),"M_Wood_Desk");Box(frame,"Green board",new Vector3(0,0,.022f),new Vector3(2.87f,.88f,.025f),"M_Chapter_Green");}
                EditorUtility.SetDirty(b);
            }
            var station=new GameObject("Board rubber cleaning station");station.transform.SetParent(d.transform,false);station.transform.localPosition=new Vector3(-3.02f,1.05f,1.3f);station.transform.localRotation=Quaternion.Euler(0,-90,0);
            station.AddComponent<BoxCollider>().size=new Vector3(1.25f,.25f,.55f);
            Box(station.transform,"Chalk collection tray",Vector3.zero,new Vector3(1.25f,.09f,.55f),"M_Chapter_Grey");
            for(int i=0;i<2;i++){var p=new Vector3(i==0?-.26f:.26f,.09f,0);Box(station.transform,"Dusty felt rubber",p,new Vector3(.25f,.09f,.13f),"M_Chapter_Paper");Box(station.transform,"Wooden rubber grip",p+Vector3.up*.06f,new Vector3(.27f,.045f,.15f),"M_Wood_Desk");}
            var label=new GameObject("Cleaning station label").AddComponent<TextMesh>();label.transform.SetParent(station.transform,false);label.transform.localPosition=new Vector3(0,.32f,-.04f);label.transform.localRotation=Quaternion.Euler(0,180,0);label.text="RUBBER CLEANING";label.anchor=TextAnchor.MiddleCenter;label.fontSize=40;label.characterSize=.027f;label.color=new Color(.12f,.17f,.17f);
            var s=station.AddComponent<RubberCleaningStation>();s.detention=d;s.boardPaper=paper;s.woodPaper=wood;d.cleaningStation=s;d.blackboard=d.blackboards[0];EditorUtility.SetDirty(d);EditorUtility.SetDirty(s);
        }
        static void Box(Transform parent,string name,Vector3 pos,Vector3 size,string material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+material+".mat");IllustratedArtSetup.Tiled(go.GetComponent<MeshRenderer>(),mat,"detention cleaning",1);
        }
    }
}

