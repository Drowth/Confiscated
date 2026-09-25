using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Confiscated.EditorTools
{
    /// <summary>Edit-mode checks of the baked NavMesh: coverage bounds and a path from the desk to the corridor.</summary>
    public static class NavMeshDiagnostics
    {
        [MenuItem("Confiscated/Diagnostics/NavMesh Path Test")]
        public static void PathTest()
        {
            var tri = NavMesh.CalculateTriangulation();
            var b = new Bounds();
            if (tri.vertices.Length > 0)
            {
                b = new Bounds(tri.vertices[0], Vector3.zero);
                foreach (var v in tri.vertices) b.Encapsulate(v);
            }
            Debug.Log(string.Format("[NavDiag] triangulation: {0} verts, {1} tris, bounds min {2} max {3}",
                tri.vertices.Length, tri.indices.Length / 3, b.min, b.max));

            Report("desk P0", new Vector3(-2.3f, 0f, 0.45f));
            Report("office door P1", new Vector3(-1.2f, 0f, -1.25f));
            Report("corridor head", new Vector3(0f, 0f, 4.4f));
            Report("corridor mid", new Vector3(0f, 0f, 10f));
            Report("corridor far", new Vector3(0f, 0f, 18.5f));

            var path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(new Vector3(-2.3f, 0f, 0.45f), new Vector3(0f, 0f, 4.4f), NavMesh.AllAreas, path);
            Debug.Log(string.Format("[NavDiag] path desk->corridor head: calc={0} status={1} corners={2}", ok, path.status, path.corners.Length));
            for (int i = 0; i < path.corners.Length; i++) Debug.Log("[NavDiag]   corner " + i + ": " + path.corners[i]);
        }

        static void Report(string label, Vector3 p)
        {
            bool hit = NavMesh.SamplePosition(p, out var h, 1.0f, NavMesh.AllAreas);
            Debug.Log(string.Format("[NavDiag] {0} {1}: onNavMesh(1m)={2} nearest={3} dist={4:F2}", label, p, hit, hit ? h.position : Vector3.zero, hit ? h.distance : -1f));
        }
    }
}
