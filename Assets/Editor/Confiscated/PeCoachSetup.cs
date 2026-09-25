using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Confiscated;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    public static class PeCoachSetup
    {
        const string PrefabPath = "Assets/Prefabs/Characters/P_PE_Coach.prefab";
        const string MaterialPath = "Assets/Art/Materials/M_PE_Coach.mat";
        const string Art = "Assets/Art/Textures/Events/";
        static readonly Vector3[] Route = {
            new(8.22f, 0, 79.56f), new(34.11f, 0, 79.56f),
            new(34.11f, 0, 34.33f), new(-5.22f, 0, 34.33f),
            new(-5.22f, 0, 79.56f)
        };

        [MenuItem("Confiscated/School Run/Install PE Coach")]
        public static void Install()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath)
                throw new InvalidOperationException("Open SchoolLayout in Edit mode to install the coach.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        public static PeCoach ApplyToScene()
        {
            var idle = Import(Art + "T_PE_Teacher_v1.png");
            var walk = Import(Art + "T_PE_Teacher_Creepy_Walk_v1.png");
            var shader = Shader.Find("Confiscated/Character Cutout");
            if (shader == null || idle == null || walk == null) throw new InvalidOperationException("Coach artwork or cutout shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader; material.SetTexture("_BaseMap", idle); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cutoff", .35f); material.SetFloat("_MagentaKey", 0);
            material.SetFloat("_DirectionalViews", 0); EditorUtility.SetDirty(material);

            var command = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Coach_Run_Command.mp3");
            var whistle = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/CoachWhistle.wav");
            var steps = Enumerable.Range(1, 5).Select(i =>
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Coach_Run_Step_" + i + ".wav")).ToArray();
            if (command == null || whistle == null || steps.Any(c => c == null))
                throw new InvalidOperationException("Coach command, whistle, or one of the five step recordings is missing.");
            var stray = GameObject.Find("P_PE_Coach");
            if (stray != null) Object.DestroyImmediate(stray);

            var source = new GameObject("P_PE_Coach");
            source.layer = LayerMask.NameToLayer("Enemy");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Cutout"; quad.layer = source.layer;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(source.transform, false);
            quad.transform.localPosition = new Vector3(0, 1.08f, 0);
            quad.transform.localScale = new Vector3(1.48f, 2.16f, 1);
            var renderer = quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.AddComponent<BillboardY>();
            var body = source.AddComponent<CapsuleCollider>(); body.center = new Vector3(0, 1.08f, 0);
            body.radius = .35f; body.height = 2.16f; body.isTrigger = true;
            var agent = source.AddComponent<NavMeshAgent>(); agent.radius = .34f; agent.height = 2.16f;
            agent.speed = 3.3f; agent.acceleration = 12f; agent.angularSpeed = 540f;
            agent.stoppingDistance = .2f; agent.autoBraking = true;
            var coach = source.AddComponent<PeCoach>();
            coach.cutout = renderer; coach.idleArt = idle; coach.walkArt = walk;
            coach.command = command; coach.warningWhistle = whistle; coach.steps = steps;
            coach.patrolSpeed = 6.4f; coach.coachingSpeed = 6.4f;
            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);

            var old = GameObject.Find("PE Coach"); if (old != null) Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            instance.name = "PE Coach"; instance.transform.position = Route[0];
            var live = instance.GetComponent<PeCoach>();
            live.corridors = SchoolPlan.Areas.Where(a => a.zone == 0 && !a.name.Contains("vestibule") &&
                Mathf.Max(a.rect.width, a.rect.height) / SchoolPlan.PixelsPerMetre >= 9f)
                .Select(a => new PeCoach.Corridor {
                    name = a.name,
                    min = new Vector2(SchoolPlan.Point(a.rect.xMin, a.rect.center.y).x,
                        SchoolPlan.Point(a.rect.center.x, a.rect.yMax).z),
                    max = new Vector2(SchoolPlan.Point(a.rect.xMax, a.rect.center.y).x,
                        SchoolPlan.Point(a.rect.center.x, a.rect.yMin).z)
                }).ToArray();
            var route = new GameObject("PE Coach lap").transform; route.SetParent(instance.transform.parent, false);
            live.lap = new Transform[Route.Length];
            for (int i = 0; i < Route.Length; i++)
            {
                var point = new GameObject("Lap point " + (i + 1)).transform;
                point.SetParent(route, false); point.position = Route[i]; live.lap[i] = point;
            }
            EditorUtility.SetDirty(live);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(live);
            return live;
        }

        static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing coach artwork: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

    }
}
