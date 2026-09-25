using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Confiscated;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Grounds every illustrated actor and enables its alpha silhouette in shadow maps.</summary>
    public static class CharacterShadowSetup
    {
        const string MaterialPath = "Assets/Art/Materials/M_CharacterGroundShadow.mat";

        [MenuItem("Confiscated/School Run/Install Character Shadows")]
        public static void Install()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode to install character shadows.");
            int count = ApplyToScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Character Shadows] Updated " + count + " illustrated characters.");
        }

        public static int ApplyToScene()
        {
            var shader = Shader.Find("Confiscated/Character Ground Shadow");
            if (shader == null) throw new InvalidOperationException("Character ground-shadow shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_Color", new Color(.045f, .065f, .105f, .68f));
            EditorUtility.SetDirty(material);

            int count = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var art = renderer.sharedMaterial;
                if (art == null || art.shader == null ||
                    art.shader.name != "Confiscated/Character Cutout" && art.shader.name != "Confiscated/Chatterbox Cutout") continue;

                renderer.shadowCastingMode = ShadowCastingMode.On;
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

                var actor = renderer.transform.parent;
                while (actor.parent != null && (actor.name == "MotionPivot" || actor.name == "VisualFacing" ||
                    actor.name == "Walking pencil pivot")) actor = actor.parent;
                var shadow = actor.Find("Ground shadow");
                if (shadow == null)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Ground shadow";
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.transform.SetParent(actor, false);
                    shadow = quad.transform;
                }
                Vector3 centre = actor.InverseTransformPoint(renderer.transform.position);
                shadow.localPosition = new Vector3(centre.x, .025f, centre.z);
                shadow.localRotation = Quaternion.Euler(90, 0, 0);
                shadow.localScale = new Vector3(Mathf.Max(.85f, renderer.transform.localScale.x * 1.05f), .9f, 1);
                var floor = shadow.GetComponent<MeshRenderer>();
                floor.sharedMaterial = material;
                floor.shadowCastingMode = ShadowCastingMode.Off;
                floor.receiveShadows = false;
                EditorUtility.SetDirty(shadow);
                EditorUtility.SetDirty(floor);
                count++;
            }
            var player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null)
            {
                var shadow = player.transform.Find("Ground shadow");
                if (shadow == null)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Ground shadow";
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.transform.SetParent(player.transform, false);
                    shadow = quad.transform;
                }
                shadow.localPosition = new Vector3(0, .025f, 0);
                shadow.localRotation = Quaternion.Euler(90, 0, 0);
                shadow.localScale = new Vector3(.85f, .65f, 1);
                var floor = shadow.GetComponent<MeshRenderer>();
                floor.sharedMaterial = material;
                floor.shadowCastingMode = ShadowCastingMode.Off;
                floor.receiveShadows = false;

                var body = player.transform.Find("Shadow-only body");
                if (body == null)
                {
                    var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = "Shadow-only body";
                    Object.DestroyImmediate(capsule.GetComponent<Collider>());
                    capsule.transform.SetParent(player.transform, false);
                    body = capsule.transform;
                }
                body.localPosition = new Vector3(0, .7f, 0);
                body.localRotation = Quaternion.identity;
                body.localScale = new Vector3(.45f, .7f, .45f);
                var caster = body.GetComponent<MeshRenderer>();
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                caster.receiveShadows = false;
                EditorUtility.SetDirty(shadow);
                EditorUtility.SetDirty(body);
                EditorUtility.SetDirty(floor);
                EditorUtility.SetDirty(caster);
                count++;
            }
            ConfigureFixtureShadows();
            return count;
        }

        static void ConfigureFixtureShadows()
        {
            var school = GameObject.Find("School");
            if (school == null) throw new InvalidOperationException("School root missing for character shadows.");
            if (school.GetComponent<CharacterShadowLighting>() == null)
                school.AddComponent<CharacterShadowLighting>();

            var fixtures = new List<Light>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Point && light.transform.parent != null &&
                    light.transform.parent.name == "P_CeilingLight") fixtures.Add(light);
            foreach (var light in fixtures)
            {
                light.shadows = LightShadows.None;
                EditorUtility.SetDirty(light);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var art = renderer.sharedMaterial;
                if (art == null || art.shader == null ||
                    art.shader.name != "Confiscated/Character Cutout" && art.shader.name != "Confiscated/Chatterbox Cutout") continue;
                SetNearestFixture(fixtures, renderer.transform.position);
            }
            var player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null) SetNearestFixture(fixtures, player.transform.position + Vector3.up);
        }

        static void SetNearestFixture(List<Light> fixtures, Vector3 position)
        {
            Light best = null;
            float distance = float.MaxValue;
            foreach (var light in fixtures)
            {
                float d = (light.transform.position - position).sqrMagnitude;
                if (d >= distance || d > light.range * light.range) continue;
                best = light; distance = d;
            }
            if (best == null) return;
            best.shadows = LightShadows.Soft;
            best.shadowStrength = .75f;
            EditorUtility.SetDirty(best);
            PrefabUtility.RecordPrefabInstancePropertyModifications(best);
        }
    }
}
