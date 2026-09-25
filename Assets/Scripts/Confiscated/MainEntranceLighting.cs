using UnityEngine;
using UnityEngine.Rendering;

namespace Confiscated
{
    /// <summary>Working wall lamps and overhead fittings for the entrance.</summary>
    public sealed class MainEntranceLighting : MonoBehaviour
    {
        Material glow;

        public static void Install()
        {
            var entrance = GameObject.Find("Main School Entrance");
            if (entrance != null && entrance.GetComponent<MainEntranceLighting>() == null)
                entrance.AddComponent<MainEntranceLighting>();
        }

        void Awake()
        {
            glow = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glow.name = "Entrance lamp glow";
            var color = new Color(1f, .96f, .86f);
            glow.SetColor("_BaseColor", color);
            glow.SetColor("_EmissionColor", color * 5f);
            glow.SetFloat("_Smoothness", 0);
            glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.name == "Entrance lamp glass")
                {
                    renderer.SetPropertyBlock(null);
                    renderer.sharedMaterial = glow;
                }

            AddLight("Left entrance wall light", new Vector3(-1.68f, 2.30f, -4.42f), 2.6f, 6f);
            AddLight("Right entrance wall light", new Vector3(1.68f, 2.30f, -4.42f), 2.6f, 6f);
            AddPanel("Entrance canopy light", new Vector3(0, 2.96f, -5.40f));
            AddPanel("Entrance lobby light", new Vector3(0, 2.65f, -1.80f));
        }

        void AddPanel(string label, Vector3 position)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = label + " fitting";
            panel.transform.SetParent(transform, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = new Vector3(1.15f, .055f, .38f);
            var collider = panel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            AddLight(label, position + Vector3.down * .20f, 3.2f, 7.5f);
        }

        void AddLight(string label, Vector3 position, float intensity, float range)
        {
            var fixture = new GameObject(label);
            fixture.transform.SetParent(transform, false);
            fixture.transform.localPosition = position;
            var light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .96f, .86f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.shadowBias = .025f;
            light.shadowNormalBias = .08f;
            light.shadowNearPlane = .08f;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        void OnDestroy()
        {
            if (glow != null) Destroy(glow);
        }
    }
}
