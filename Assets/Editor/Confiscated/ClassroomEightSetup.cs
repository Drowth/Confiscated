using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Room 8 remains the resources pickup room, with a small teaching and reading area.</summary>
    public static class ClassroomEightSetup
    {
        const string RootName = "Classroom 8 furnishings";
        const string Output = "D:/Confiscated/Docs/Classroom8";
        static Transform root;
        static readonly string[] Covers = { "M_Chapter_Green", "M_Chapter_ToyRed", "M_Trolley_SketchedTeal", "M_Chapter_Brass" };

        [MenuItem("Confiscated/Classroom 8/Decorate Room")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SchoolLayout.unity")
                throw new InvalidOperationException("Open SchoolLayout in Edit mode first.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Decorate Classroom 8");
            int undo = Undo.GetCurrentGroup();
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            root = new GameObject(RootName).transform;
            var navigation = root.gameObject.AddComponent<NavMeshModifier>();
            navigation.overrideArea = true;
            navigation.area = 1; // Furniture is not a walkable surface.
            foreach (float x in new[] { 5.1f, 11.15f })
                foreach (float z in new[] { 85.1f, 94.6f })
                    Place("Modular/P_CeilingLight", root, new Vector3(x, 0, z));

            // Pair desks around a generous through aisle. Keep both door swing zones empty.
            for (int row = 0; row < 3; row++)
                foreach (float x in new[] { 5.1f, 11.15f })
                {
                    float z = 91.9f + row * 2.05f;
                    var station = Group("Paired pupil desk", root, new Vector3(x, 0, z));
                    Place("Props/P_Desk", station, Vector3.zero, 180);
                    for (int seat = 0; seat < 2; seat++)
                    {
                        float sx = seat == 0 ? -.42f : .42f;
                        Place("Props/P_Chair", station, new Vector3(sx, 0, -.78f), 180);
                        Book(station, new Vector3(sx, .766f, -.04f), row + seat);
                        Box("Pencil", station, new Vector3(sx + .18f, .772f, .04f), new Vector3(.018f, .014f, .19f), "M_Chapter_Brass");
                    }
                    // Solid collision below the desktop; the root modifier excludes its top from navigation.
                    Block(station, new Vector3(0, .38f, 0), new Vector3(1.6f, .76f, .8f));
                }

            var board = Group("Teaching board", root, new Vector3(5.25f, 1.92f, 99.49f));
            Box("Timber frame", board, Vector3.zero, new Vector3(4.4f, 1.55f, .10f), "M_Wood_Desk");
            Box("Green chalkboard", board, new Vector3(0, 0, -.07f), new Vector3(4.18f, 1.33f, .04f), "M_Chapter_Green");
            Label(board, "CLASS 8", new Vector3(0, .39f, -.11f), .32f, 3.8f, Color.white);
            Label(board, "Read  /  Imagine  /  Discover", new Vector3(0, -.04f, -.11f), .23f, 3.8f, Color.white);
            Label(board, "Choose a book. Share an idea.", new Vector3(0, -.39f, -.11f), .19f, 3.7f, Color.white);
            Box("Chalk ledge", board, new Vector3(0, -.80f, -.14f), new Vector3(4.45f, .06f, .22f), "M_Wood_Desk");
            Box("Board rubber", board, new Vector3(1.45f, -.74f, -.18f), new Vector3(.24f, .07f, .10f), "M_Chapter_Ink");
            Place("Hallway/P_Hall_Clock", root, new Vector3(12.45f, 2.32f, 99.49f));

            // Low furniture keeps the entrance sightline and the collectible clearly readable.
            var reading = Group("Reading corner", root, new Vector3(4.6f, 0, 84.4f));
            Box("Woven reading mat border", reading, new Vector3(0, .006f, 0), new Vector3(3.9f, .01f, 3.4f), "M_Trolley_SketchedTeal");
            Box("Woven reading mat inset", reading, new Vector3(0, .013f, 0), new Vector3(3.65f, .008f, 3.15f), "M_Entrance_Mat");
            Place("Hallway/P_Hall_Bench", reading, new Vector3(0, 0, -1.13f), 180);
            var shelf = Group("Reading books", root, new Vector3(2.91f, 0, 84.65f));
            shelf.localRotation = Quaternion.Euler(0, -90, 0);
            Box("Low bookcase back", shelf, new Vector3(0, .52f, .23f), new Vector3(2.5f, 1.04f, .08f), "M_Wood_Desk");
            foreach (float x in new[] { -1.21f, 1.21f })
                Box("Bookcase end", shelf, new Vector3(x, .52f, 0), new Vector3(.08f, 1.04f, .55f), "M_Wood_Desk");
            foreach (float y in new[] { .08f, .53f, 1.04f })
                Box("Bookcase shelf", shelf, new Vector3(0, y, 0), new Vector3(2.5f, .07f, .55f), "M_Wood_Desk");
            for (int level = 0; level < 2; level++) for (int i = 0; i < 15; i++)
            {
                float height = .26f + (i % 3) * .035f;
                Box("Reading book spine", shelf, new Vector3(-1.05f + i * .145f, .135f + level * .45f + height / 2, -.05f), new Vector3(.105f, height, .31f), Covers[(i + level) % 4]);
                Box("Paper spine label", shelf, new Vector3(-1.05f + i * .145f, .21f + level * .45f, -.211f), new Vector3(.071f, .038f, .004f), "M_Chapter_Paper");
            }
            Block(shelf, new Vector3(0, .55f, 0), new Vector3(2.5f, 1.1f, .55f));
            WallDisplay("Reading corner display", new Vector3(2.48f, 2.03f, 84.8f), -90, "M_Run_Nature", "A GOOD BOOK STARTS AN ADVENTURE", .73f);
            WallDisplay("Pupil work display", new Vector3(2.48f, 1.9f, 94.55f), -90, "M_Run_PupilArt", "OUR WORK", 1.08f);
            WallDisplay("Nature study display", new Vector3(13.73f, 1.9f, 94.55f), 90, "M_Run_Nature", "LOOK CLOSER", .83f);

            var supplies = Group("Shared supplies table", root, new Vector3(11.55f, 0, 84.1f));
            Place("Props/P_Desk", supplies, Vector3.zero);
            Block(supplies, new Vector3(0, .38f, 0), new Vector3(1.6f, .76f, .8f));
            for (int i = 0; i < 3; i++) Book(supplies, new Vector3(-.44f, .77f + i * .035f, 0), i);
            Box("Paper tray", supplies, new Vector3(.12f, .78f, .03f), new Vector3(.4f, .07f, .45f), "M_Chapter_Green");
            Box("Drawing paper", supplies, new Vector3(.12f, .824f, .03f), new Vector3(.35f, .035f, .4f), "M_Chapter_Paper");
            Box("Pencil pot", supplies, new Vector3(.55f, .85f, .07f), new Vector3(.16f, .2f, .16f), "M_Trolley_SketchedTeal");
            for (int i = 0; i < 5; i++)
                Box("Colouring pencil", supplies, new Vector3(.5f + i * .025f, 1f + (i % 2) * .02f, .07f), new Vector3(.017f, .22f, .017f), Covers[i % 4]);
            Place("Hallway/P_Hall_RecyclingBin", root, new Vector3(13.25f, 0, 83.9f), 90);
            Place("Hallway/P_Hall_CoatRail", root, new Vector3(11.55f, 1.55f, 81.6f), 180);
            var notice = Group("Supplies notice", root, new Vector3(11.55f, 2.32f, 81.61f));
            notice.localRotation = Quaternion.Euler(0, 180, 0);
            Box("Paper", notice, Vector3.zero, new Vector3(2.1f, .5f, .025f), "M_Chapter_Paper");
            Label(notice, "A PLACE FOR EVERYTHING", new Vector3(0, 0, -.02f), .16f, 1.9f, Color.black);

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Decorate Classroom 8");
            Undo.CollapseUndoOperations(undo);
            DiningHallSetup.Rebake();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Capture();
            Debug.Log("[Classroom8] Decorated resources classroom: 6 double desks, 12 chairs, chalkboard, reading corner, supplies and displays.");
        }

        static Transform Group(string name, Transform parent, Vector3 p)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }
        static void Box(string name, Transform parent, Vector3 p, Vector3 size, string material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
            g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = size;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + material + ".mat");
            if (mat == null) throw new InvalidOperationException("Missing material " + material);
            IllustratedArtSetup.Tiled(g.GetComponent<MeshRenderer>(), mat, "Classroom8", .7f);
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static void Block(Transform t, Vector3 centre, Vector3 size)
        { var c = t.gameObject.AddComponent<BoxCollider>(); c.center = centre; c.size = size; }
        static GameObject Place(string prefab, Transform parent, Vector3 p, float yaw = 0)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing prefab " + prefab);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform); return g;
        }
        static void Book(Transform parent, Vector3 p, int colour)
        {
            Box("Exercise book cover", parent, p, new Vector3(.29f, .025f, .36f), Covers[colour % 4]);
            Box("Exercise book label", parent, p + new Vector3(0, .014f, .055f), new Vector3(.18f, .003f, .10f), "M_Chapter_Paper");
        }
        static void Label(Transform parent, string text, Vector3 p, float height, float maxWidth, Color colour)
        {
            var t = Group(text, parent, p); var mesh = t.gameObject.AddComponent<TextMesh>();
            mesh.font = SchoolTypography.Font; mesh.fontSize = 72; mesh.characterSize = .05f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.text = text; mesh.color = colour;
            var r = t.GetComponent<MeshRenderer>(); r.sharedMaterial = mesh.font.material;
            mesh.font.RequestCharactersInTexture(text, mesh.fontSize);
            var size = r.localBounds.size;
            t.localScale = Vector3.one * Mathf.Min(height / Mathf.Max(.001f, size.y), maxWidth / Mathf.Max(.001f, size.x));
            t.gameObject.AddComponent<WorldLabel>();
        }
        static void WallDisplay(string name, Vector3 p, float yaw, string mat, string heading, float scale)
        {
            var g = Place("SchoolRun/P_ArtDisplay", root, p, yaw); g.name = name; g.transform.localScale = Vector3.one * scale;
            g.transform.Find("Generated pupil artwork").GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + mat + ".mat");
            Label(g.transform, heading, new Vector3(0, 1.03f, -.07f), .18f, 2.8f, Color.black);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform.Find("Generated pupil artwork").GetComponent<MeshRenderer>());
        }

        [MenuItem("Confiscated/Classroom 8/Capture Room")]
        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            var g = new GameObject("Temporary Classroom 8 review camera"); var c = g.AddComponent<Camera>(); c.fieldOfView = 75;
            try
            {
                c.transform.position = new Vector3(8.3f, FirstPersonController.ChildEyeHeight, 83.1f); c.transform.LookAt(new Vector3(8.1f, 1.3f, 95));
                HallwayPropLibrary.Capture(c, 1500, 950, Output + "/01-entrance.png");
                c.transform.position = new Vector3(10.2f, FirstPersonController.ChildEyeHeight, 97.65f); c.transform.LookAt(new Vector3(7.5f, 1.1f, 84.5f));
                HallwayPropLibrary.Capture(c, 1500, 950, Output + "/02-reverse.png");
                c.transform.position = new Vector3(7.25f, FirstPersonController.ChildEyeHeight, 87f); c.transform.LookAt(new Vector3(4.15f, 1.05f, 84.35f));
                HallwayPropLibrary.Capture(c, 1500, 950, Output + "/03-reading-corner.png");
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
