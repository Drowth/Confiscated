using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Confiscated.EditorTools
{
    /// <summary>Reusable pencil materials and per-face drawn edges; geometry and gameplay components are preserved.</summary>
    public static class IllustratedArtSetup
    {
        const string TextureDir = "Assets/Art/Textures/";
        const string MaterialDir = "Assets/Art/Materials/";
        const string MeshDir = "Assets/Art/Meshes/";

        [MenuItem("Confiscated/Apply Illustrated Asset Textures")]
        public static void Apply()
        {
            var edge = ImportTexture("T_Navy_Pencil_Edge", false, true);
            var office = Surface("M_Door_Office", "T_Door_Office_v2", edge, false, .009f,
                new Vector4(.12f,.12f,.22f,.22f), new Vector2(.209f,.44f));
            var fire = Surface("M_Door_Fire", "T_Door_Fire_v2", edge, false, .009f,
                new Vector4(.08f,.09f,.26f,.24f), new Vector2(.468f,.48f));
            var glass = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir+"M_Sketched_WindowGlass.mat") ?? Surface("M_Window_Illustrated", "T_Glass_Pencil", edge, false, .006f,
                new Vector4(.15f,.15f,.3f,.3f), Vector2.one*.3f);
            var board = Surface("M_Noticeboard", "T_Noticeboard_Pencil_v2", edge, false, .008f,
                new Vector4(.43f,.62f,.05f,.23f), new Vector2(.05f,.184f));
            var wood = Surface("M_Wood_Desk", "T_Wood_Pencil_v2", edge, true, .016f);
            var trim = Surface("M_Painted_Trim_Pencil", "T_Painted_Trim_Pencil", edge, true, .020f);

            foreach (string door in new[] { "P_Door_Office", "P_Door_Fire_Double", "P_Door_Classroom" })
            {
                EditPrefab("Modular/" + door, root =>
                {
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.name != "Leaf")
                        {
                            // Sign plates retain their own artwork/material.
                            if (r.name == "PostL" || r.name == "PostR" || r.name == "Lintel")
                                Tiled(r, trim, "Trim", 1f);
                            continue;
                        }
                        if (door == "P_Door_Classroom") continue;
                        bool doubleDoor = door == "P_Door_Fire_Double";
                        int index = r.transform.parent.name == "Hinge1" ? 0 : 1;
                        r.sharedMaterial = doubleDoor ? fire : office;
                        Map(r, doubleDoor ? "FireLeaf" + index : "OfficeLeaf",
                            (p, size, normal) =>
                            {
                                var uv = new Vector2(p.x / size.x, p.y / size.y);
                                if (doubleDoor)
                                {
                                    int half = normal.z > 0 ? 1 - index : index;
                                    uv.x = uv.x * .5f + half * .5f;
                                }
                                return uv;
                            }, true);
                    }
                });
            }

            EditPrefab("Props/P_Window_Escape", root =>
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name == "Glass")
                    {
                        r.sharedMaterial = glass;
                        Map(r, "Glass", (p, size, normal) => new Vector2(p.x / size.x, p.y / size.y), true);
                    }
                    else Tiled(r, trim, "Trim", 1f);
                }
            });
            EditPrefab("Props/P_Noticeboard", root =>
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name == "Board")
                    {
                        r.sharedMaterial = board;
                        Map(r, "Noticeboard", (p, size, normal) => new Vector2(p.x / size.x, p.y / size.y), true);
                    }
                    else Tiled(r, wood, "Wood", 1f);
                }
            });
            foreach (string prop in new[] { "P_Chair", "P_Desk" })
                EditPrefab("Props/" + prop, root =>
                {
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        Tiled(r, wood, "Wood", 1f);
                });

            AssetDatabase.SaveAssets();
            Debug.Log("[Confiscated] Finished pencil surfaces and drawn edges on all six faces; shared wood and cream trim installed.");
        }

        static Texture2D ImportTexture(string name, bool repeat, bool strip = false)
        {
            string path = TextureDir + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Pencil artwork missing", path);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !strip; // The navy strip is used as an ink coverage mask.
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.wrapModeV = repeat || strip ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Surface(string name, string textureName, Texture2D edge, bool repeat, float edgeWidth,
            Vector4 sideRect = default, Vector2 sideSize = default)
        {
            var shader = Shader.Find("Confiscated/Pencil Surface");
            if (shader == null) throw new InvalidOperationException("Pencil Surface shader missing.");
            var texture = ImportTexture(textureName, repeat);
            string path = MaterialDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.shaderKeywords = Array.Empty<string>();
            mat.renderQueue = -1;
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EdgeMap", edge);
            mat.SetColor("_EdgeColor", new Color(.16f,.21f,.36f));
            mat.SetFloat("_EdgeWidth", edgeWidth);
            mat.SetFloat("_EdgeRepeat", .45f);
            mat.SetVector("_SideRect", sideRect);
            mat.SetVector("_SideWorldSize", new Vector4(sideSize.x,sideSize.y,0,0));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EditPrefab(string name, Action<GameObject> edit)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Tiled(MeshRenderer renderer, Material material, string kind, float metresPerTile)
        {
            renderer.sharedMaterial = material;
            Map(renderer, kind, (p, size, normal) => p / metresPerTile, false);
        }

        /// <summary>Only changes UVs and colour metadata. Vertices, triangles, bounds, transforms and colliders stay intact.</summary>
        static void Map(MeshRenderer renderer, string kind, Func<Vector2, Vector2, Vector3, Vector2> project, bool cropSides)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = new Vector2[vertices.Length];
            var metres = new Vector2[vertices.Length];
            var dimensions = new Vector2[vertices.Length];
            var flags = new Color[vertices.Length];
            var bounds = source.bounds;
            var scale = renderer.transform.localScale;
            // Include local shape AND transform scale so built-in cubes and metre-sized custom boxes never share a wrong mesh.
            string key = kind + "_" + Key(bounds.size) + "_" + Key(scale);
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = normals[i];
                var up = Mathf.Abs(n.y) > .5f ? Vector3.forward : Vector3.up;
                var right = Vector3.Cross(up, -n);
                float halfWidth = Mathf.Abs(Vector3.Dot(bounds.extents, right));
                float halfHeight = Mathf.Abs(Vector3.Dot(bounds.extents, up));
                float scaleX = Vector3.Scale(right, scale).magnitude;
                float scaleY = Vector3.Scale(up, scale).magnitude;
                var p = new Vector2((Vector3.Dot(vertices[i]-bounds.center,right)+halfWidth)*scaleX,
                    (Vector3.Dot(vertices[i]-bounds.center,up)+halfHeight)*scaleY);
                var size = new Vector2(halfWidth*2*scaleX,halfHeight*2*scaleY);
                uv[i] = project(p, size, n);
                metres[i] = p;
                dimensions[i] = size;
                flags[i] = new Color(cropSides && Mathf.Abs(n.z) < .5f ? 1f : 0f,0,0,1);
            }
            string path = MeshDir + "Pencil_" + key + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "Pencil_" + key;
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.uv = uv;
            mesh.uv2 = metres;
            mesh.uv3 = dimensions;
            mesh.colors = flags;
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            filter.sharedMesh = mesh;
        }

        static string Key(Vector3 value)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return value.x.ToString("R",ci) + "x" + value.y.ToString("R",ci) + "x" + value.z.ToString("R",ci);
        }
    }
}
