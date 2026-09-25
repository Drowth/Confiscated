using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Keeps the classroom errand inside the west wing (Year 6, west corridor, office, dining hall):
    /// four corridor fire doors plus two lesson-locked room doors. Everything opens when the run begins.
    /// </summary>
    public static class LessonGateSetup
    {
        public const string GroupName = "Lesson gates";
        /// <summary>Room doors that would otherwise lead out of the west wing during the errand.</summary>
        public static readonly string[] LessonDoors = { "Dining east B", "South room B north" };

        public readonly struct Gate
        {
            public readonly string name;
            public readonly float x, y, corridorPixels;
            public readonly Vector3 swing, wing;
            public Gate(string name, float x, float y, float corridorPixels, Vector3 swing, Vector3 wing)
            { this.name = name; this.x = x; this.y = y; this.corridorPixels = corridorPixels; this.swing = swing; this.wing = wing; }
            public Vector3 Position => SchoolPlan.Point(x, y);
        }
        // Plan pixels. North is +Z, east is +X. Leaves swing clear of nearby room doors and junctions.
        public static readonly Gate[] Gates =
        {
            new("West corridor lesson doors", 280, 405, 32, Vector3.forward, Vector3.back),
            // East of the dining entrance: the dining corridor, its puddle and the dinner trolley stay inside the errand.
            // Leaves fold west along the walls; swinging east would narrow the mouth of north link A.
            new("North cross hall lesson doors", 457, 467, 34, Vector3.left, Vector3.left),
            // The art room door is immediately east, so these leaves fold back into the wing.
            new("South cross hall lesson doors", 491, 874.5f, 33, Vector3.left, Vector3.left),
            new("South corridor lesson doors", 510, 1167, 32, Vector3.right, Vector3.left)
        };

        [MenuItem("Confiscated/School Run/Install Lesson Gates (West Wing Errand)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before installing the lesson gates.");
            if (EditorSceneManager.GetActiveScene().path != SchoolLayoutBuilder.ScenePath) throw new InvalidOperationException("Open SchoolLayout first.");
            ApplyToScene();
            // Surround panels are new static walls: rebake and save through the established route.
            SchoolRunSetup.RefreshShelfLayout();
            Debug.Log("[LessonGates] Installed " + Gates.Length + " corridor gates and " + LessonDoors.Length + " lesson-locked doors; navigation rebaked.");
        }

        [MenuItem("Confiscated/School Run/Remove Lesson Gates")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play before removing the lesson gates.");
            Clear();
            SchoolRunSetup.RefreshShelfLayout();
            Debug.Log("[LessonGates] Removed. The whole school is open during the errand again.");
        }

        [MenuItem("Confiscated/School Run/Capture Lesson Gates")]
        public static void Capture()
        {
            var go = new GameObject("Lesson gate review camera"); var camera = go.AddComponent<Camera>(); camera.fieldOfView = 68;
            try
            {
                for (int i = 0; i < Gates.Length; i++)
                {
                    // Child eye height, approaching from inside the wing.
                    camera.transform.position = Gates[i].Position + Gates[i].wing * 4.5f + Vector3.up * 1.25f;
                    camera.transform.LookAt(Gates[i].Position + Vector3.up * 1.25f);
                    HallwayPropLibrary.Capture(camera, 1500, 950, "D:/Confiscated/Docs/LessonGate_" + (i + 1) + ".png");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void Clear()
        {
            var run = GameObject.Find("SchoolRun"); if (run == null) throw new InvalidOperationException("Build the school run first.");
            var old = run.transform.Find(GroupName); if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var door in Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))
            {
                foreach (var block in door.transform.Cast<Transform>().Where(t => t.name == "Lesson doorway collision").ToArray()) Object.DestroyImmediate(block.gameObject);
                if (!door.closedDuringLessons) continue;
                door.closedDuringLessons = false; EditorUtility.SetDirty(door);
            }
        }

        /// <summary>Does not bake. Callers own the navigation rebuild.</summary>
        public static void ApplyToScene()
        {
            Clear();
            var group = new GameObject(GroupName).transform; group.SetParent(GameObject.Find("SchoolRun").transform, false);
            foreach (var gate in Gates) BuildGate(group, gate);
            var doors = Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None);
            foreach (string name in LessonDoors)
            {
                var door = doors.FirstOrDefault(d => d.name == name); if (door == null) throw new Exception("Lesson door missing: " + name);
                if (door.closedForRun || door.runRequiredLevel > 0) throw new Exception("Lesson door already has a run lock: " + name);
                door.closedDuringLessons = true;
                // Child of the door, so the player's look ray still finds the door and its explanation.
                var barrier = Block(door.transform, name.Contains("main") ? 2.6f : 1.5f);
                // The controller lives on an always-active host because it switches the barrier object off.
                var host = new GameObject("Lesson door blocker - " + name).transform; host.SetParent(group, false); host.position = door.transform.position;
                var control = host.gameObject.AddComponent<RunDoorBlocker>(); control.door = door; control.barrier = barrier;
                EditorUtility.SetDirty(door); PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
            Batch3ArtSetup.ApplyCorridorDoors();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void BuildGate(Transform group, Gate gate)
        {
            float corridor = gate.corridorPixels / SchoolPlan.PixelsPerMetre;
            var rotation = Quaternion.LookRotation(gate.swing);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Modular/P_Door_Fire_Double.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, group); go.name = gate.name;
            go.transform.SetPositionAndRotation(gate.Position, rotation);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Same proportions as the school's other double doors.
            const float width = 2.6f, frameHalf = .98f * width / 1.8f, frameTop = 2.08f * 1.1f, ceiling = 3f;
            go.transform.localScale = new Vector3(width / 1.8f, 1.1f, 1);
            var doors = go.AddComponent<LessonCorridorDoors>();
            doors.hinge = go.transform.Find("Hinge1"); doors.secondHinge = go.transform.Find("Hinge2");
            doors.secondHinge.localPosition = new Vector3(.9f, 0, 0);
            doors.secondHinge.Find("Leaf").localPosition = new Vector3(-.45f, 1, 0);
            foreach (var hinge in new[] { doors.hinge, doors.secondHinge }) hinge.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            doors.barrier = Block(go.transform, width + .3f);

            // Fixed surround: closes the corridor either side of, and above, the door frame.
            var surround = new GameObject(gate.name + " surround").transform; surround.SetParent(group, false);
            surround.SetPositionAndRotation(gate.Position, rotation);
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Wall_Corridor.mat");
            float side = corridor * .5f - frameHalf + .04f;
            foreach (float sign in new[] { -1f, 1f })
                Panel("Side panel", surround, new Vector3(sign * (frameHalf + side * .5f), ceiling * .5f, 0), new Vector3(side, ceiling, .15f), wall);
            Panel("Header panel", surround, new Vector3(0, (frameTop + ceiling) * .5f, 0), new Vector3(frameHalf * 2, ceiling - frameTop, .15f), wall);

            // One small notice on the wing face of the first leaf.
            var leaf = doors.hinge.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Leaf");
            var bounds = leaf.localBounds; bool front = Vector3.Dot(gate.wing, leaf.transform.forward) > 0;
            var point = leaf.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .4f, front ? bounds.max.z : bounds.min.z));
            var notice = SchoolRunSetup.Notice(point + gate.wing * .0011f, leaf.transform.eulerAngles.y + (front ? 180 : 0), "LESSONS IN PROGRESS\nNO THROUGH ROUTE");
            notice.name = "School door notice"; notice.localScale = new Vector3(1, 1, .1f); notice.SetParent(leaf.transform, true);
            foreach (var renderer in notice.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Report anything already standing where the doors and their swing will be.
            Physics.SyncTransforms();
            var hits = Physics.OverlapBox(gate.Position + Vector3.up * 1.1f + gate.swing * .6f, new Vector3(corridor * .5f - .15f, .9f, .75f), rotation)
                .Where(c => !c.transform.IsChildOf(group) && !c.name.StartsWith("Wall_") && c.bounds.max.y > .15f && c.bounds.min.y < 2.7f)
                .Select(c => c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name).Distinct().ToArray();
            if (hits.Length > 0) Debug.LogWarning("[LessonGates] " + gate.name + " overlaps: " + string.Join(", ", hits));
        }

        static GameObject Block(Transform parent, float worldWidth)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Lesson doorway collision";
            Object.DestroyImmediate(g.GetComponent<MeshRenderer>()); Object.DestroyImmediate(g.GetComponent<MeshFilter>());
            g.transform.SetParent(parent, false); g.transform.localPosition = new Vector3(0, 1.1f / parent.lossyScale.y, 0);
            var s = parent.lossyScale; g.transform.localScale = new Vector3(worldWidth / s.x, 2.2f / s.y, .18f / s.z);
            g.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var obstacle = g.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.zero; obstacle.size = Vector3.one;
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
            return g;
        }

        /// <summary>Metre-sized mesh with the layout builder's world-space wall mapping, so the dado lines up with the corridor.</summary>
        static void Panel(string name, Transform parent, Vector3 local, Vector3 size, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = local;
            var filter = g.GetComponent<MeshFilter>(); var mesh = Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], size);
                Vector3 w = g.transform.TransformPoint(vertices[i]), n = g.transform.TransformDirection(normals[i]);
                uv[i] = Mathf.Abs(n.y) > .5f ? new Vector2(w.x, w.z) : new Vector2((Mathf.Abs(n.x) > .5f ? w.z : w.x) / 2, w.y / 3);
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string path = "Assets/Art/Meshes/School/" + (parent.name + "_" + name + "_" + parent.childCount).Replace(' ', '_') + ".asset";
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; } else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            filter.sharedMesh = saved; g.GetComponent<BoxCollider>().size = size; g.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
        }
    }
}
