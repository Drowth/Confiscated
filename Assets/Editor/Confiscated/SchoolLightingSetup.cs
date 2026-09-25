using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Confiscated.EditorTools
{
    // Normal-mode lighting rework: the ceiling fluorescent fixtures (P_CeilingLight) are the level's real light
    // source. Dims ambient and the KeyLight well down, brightens and cools
    // the ceiling fixtures' point light and gives the panels HDR emission, then regenerates the
    // fixture layout (SchoolLayoutBuilder.RefreshLights) so larger rooms get a proper spaced grid instead of one
    // fixture in the centre. Dark Mode is untouched: DarkModeController snapshots whatever these normal values are
    // and restores them, so it automatically picks up the new baseline.
    public static class SchoolLightingSetup
    {
        const string PrefabPath="Assets/Prefabs/Modular/P_CeilingLight.prefab";
        const string MaterialPath="Assets/Art/Materials/M_CeilingLight.mat";
        static readonly Color FixtureColor=new(0.92f,0.97f,1f);
        const float FixtureIntensity=2.4f;
        const float FixtureRange=7.5f;
        // HDR emission makes the fitting itself shine. The existing point lights still illuminate the rooms.
        static readonly Color PanelColor=new(0.96f,0.98f,1f);
        static readonly Color AmbientColor=new(0.16f,0.17f,0.2f);
        const float KeyLightIntensity=0.05f;

        [MenuItem("Confiscated/Lighting/Apply Fluorescent Lighting")]
        public static void ApplyAndSave()
        {
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToScene()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)throw new System.Exception("P_CeilingLight prefab missing");
            var contents=PrefabUtility.LoadPrefabContents(PrefabPath);
            var light=contents.GetComponentInChildren<Light>(true);
            if(light==null){PrefabUtility.UnloadPrefabContents(contents);throw new System.Exception("P_CeilingLight has no Light component");}
            light.color=FixtureColor;light.intensity=FixtureIntensity;light.range=FixtureRange;
            PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(mat==null)throw new System.Exception("M_CeilingLight material missing");
            mat.shader=Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor",PanelColor);
            mat.SetColor("_EmissionColor",PanelColor*5f);mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            mat.SetFloat("_Smoothness",0);mat.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(mat);
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VisualTest_PostProfile.asset");
            if(profile!=null)
            {
                if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
                bloom.threshold.Override(1.5f);bloom.intensity.Override(.15f);bloom.scatter.Override(.5f);bloom.clamp.Override(20f);
                EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(profile);
            }

            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=AmbientColor;
            RenderSettings.ambientIntensity=1;

            var key=GameObject.Find("KeyLight");
            if(key!=null)
            {
                var kl=key.GetComponent<Light>();
                if(kl!=null){kl.intensity=KeyLightIntensity;EditorUtility.SetDirty(kl);}
            }

            SchoolLayoutBuilder.RefreshLights();
            AddFeatureFixtures();
        }

        // Objectives the plan-grid fixtures can't reach (walled off or outside a room's grid) get their own fixture,
        // placed from the scene object so it follows the object if it moves.
        static readonly string[] FeatureFixtures={"Dining property enclosure"};
        const string FeatureGroup="Feature ceiling lights";
        static void AddFeatureFixtures()
        {
            var school=GameObject.Find("School");if(school==null)return;
            var old=school.transform.Find(FeatureGroup);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var group=new GameObject(FeatureGroup).transform;group.SetParent(school.transform,false);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach(var name in FeatureFixtures)
            {
                var target=GameObject.Find(name);if(target==null){Debug.LogWarning("[Lighting] feature fixture target missing: "+name);continue;}
                var go=(GameObject)PrefabUtility.InstantiatePrefab(source,group);go.name="Fixture over "+name;
                var p=target.transform.position;go.transform.position=new Vector3(p.x,0,p.z);go.transform.rotation=Quaternion.Euler(0,target.transform.eulerAngles.y,0);
            }
        }
    }
}
