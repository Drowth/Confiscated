using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class SchoolBellSetup
    {
        public const string PrefabPath="Assets/Prefabs/Audio/P_SchoolBell.prefab";
        const string ModelPath="Assets/Art/Models/SchoolBell/SM_SchoolBell.fbx";
        [MenuItem("Confiscated/School Bells/Build Reusable Bell")]
        public static void BuildPrefab()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.bakeAxisConversion=true;importer.globalScale=1;importer.useFileScale=true;
            importer.generateSecondaryUV=false;importer.importCameras=false;importer.importLights=false;
            importer.importAnimation=false;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach(var mat in model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
            {
                var shared=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+mat.name+".mat");
                if(shared==null)throw new Exception("Missing pencil material "+mat.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),shared);
            }
            importer.SaveAndReimport();
            var audioImporter=(AudioImporter)AssetImporter.GetAtPath("Assets/Audio/SFX/SchoolBell.mp3");
            audioImporter.forceToMono=true;audioImporter.SaveAndReimport();
            var root=new GameObject("P_SchoolBell");
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),root.transform);
                visual.name="Blender model";visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                var source=root.AddComponent<AudioSource>();
                source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/SchoolBell.mp3");
                source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.volume=.52f;
                source.dopplerLevel=0;source.spread=0;source.minDistance=3;source.maxDistance=38;
                source.rolloffMode=AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(
                    new Keyframe(0,1),new Keyframe(.079f,1),new Keyframe(.263f,.47f),new Keyframe(.526f,.16f),new Keyframe(1,0)));
                var bell=root.AddComponent<SchoolBell>();
                bell.gong=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Gong");
                bell.striker=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Striker");
                var marks=new GameObject("Drawn ringing marks");marks.transform.SetParent(root.transform,false);
                var ink=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Chapter_Ink.mat");
                for(int side=-1;side<=1;side+=2)for(int arc=0;arc<2;arc++)
                {
                    var g=new GameObject("Broken sound stroke");g.transform.SetParent(marks.transform,false);
                    var line=g.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=ink;
                    line.startWidth=.004f;line.endWidth=.002f;line.numCapVertices=2;line.positionCount=7;
                    for(int i=0;i<7;i++){float a=(-.48f+i*.16f);float r=.312f+arc*.043f;line.SetPosition(i,new Vector3(side*(Mathf.Cos(a)*r),.045f+Mathf.Sin(a)*r,-.09f));}
                    line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                bell.ringingMarks=marks;marks.SetActive(false);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,-.05f,-.075f);collider.size=new Vector3(.6f,.78f,.22f);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Confiscated/School Bells/Place School Bells")]
        public static void ApplyToScene()
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode first.");
            var old=Object.FindFirstObjectByType<SchoolBellSystem>();if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            var root=new GameObject("SchoolBells");Undo.RegisterCreatedObjectUndo(root,"Place school bells");
            var system=root.AddComponent<SchoolBellSystem>();
            // Bell front is local -Z. Pixel points sit 11 cm inward of existing wall centres.
            Put("North west corridor",335,254,0);
            Put("North east corridor",765,254,0);
            Put("Office corridor",265,418,-90);
            Put("North cross hall",565,451,0);
            Put("East upper corridor",907,525,90);
            Put("Dining hall",440,804,180);
            Put("West middle corridor",265,698,-90);
            Put("Central crossroads",524,618,-90);
            Put("Detention corridor",717,550,90);
            Put("East classroom corridor",907,810,90);
            Put("Year 6 corridor",375,890,180);
            Put("South classroom approach",665,859,0);
            Put("South west corridor",265,1060,-90);
            Put("South east corridor",907,1060,90);
            Put("South entrance corridor",590,1152,0);
            system.bells=root.GetComponentsInChildren<SchoolBell>();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            void Put(string name,float x,float y,float yaw)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),root.transform);
                go.name="Bell - "+name;go.transform.SetPositionAndRotation(SchoolPlan.Point(x,y,2.54f),Quaternion.Euler(0,yaw,0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
        }
    }
}
