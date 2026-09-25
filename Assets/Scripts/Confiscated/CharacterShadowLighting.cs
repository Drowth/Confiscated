using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>Gives each nearby illustrated character one shadow-casting ceiling fixture.</summary>
    public sealed class CharacterShadowLighting : MonoBehaviour
    {
        readonly HashSet<Light> selected = new();
        readonly HashSet<Light> wanted = new();
        Light[] fixtures;
        float nextUpdate;

        void Awake()
        {
            var lights = new List<Light>();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Point && light.transform.parent != null &&
                    light.transform.parent.name == "P_CeilingLight")
                {
                    lights.Add(light);
                    if (light.shadows != LightShadows.None) selected.Add(light);
                }
            fixtures = lights.ToArray();
        }

        void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .3f;
            wanted.Clear();
            var camera = Camera.main;
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!renderer.enabled || renderer.sharedMaterial == null) continue;
                string shader = renderer.sharedMaterial.shader.name;
                if (shader != "Confiscated/Character Cutout" && shader != "Confiscated/Chatterbox Cutout") continue;
                if (camera != null && (renderer.transform.position - camera.transform.position).sqrMagnitude > 28f * 28f)
                    continue;
                AddNearest(renderer.transform.position);
            }
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player != null) AddNearest(player.transform.position + Vector3.up);

            foreach (var light in selected)
                if (!wanted.Contains(light) && light != null) light.shadows = LightShadows.None;
            selected.Clear();
            foreach (var light in wanted)
            {
                if (light.shadows == LightShadows.None) light.shadows = LightShadows.Soft;
                selected.Add(light);
            }
        }

        void AddNearest(Vector3 position)
        {
            Light best = null;
            float distance = float.MaxValue;
            foreach (var light in fixtures)
            {
                if (light == null || !light.isActiveAndEnabled) continue;
                float d = (light.transform.position - position).sqrMagnitude;
                if (d >= distance || d > light.range * light.range) continue;
                best = light;
                distance = d;
            }
            if (best != null) wanted.Add(best);
        }
    }
}
