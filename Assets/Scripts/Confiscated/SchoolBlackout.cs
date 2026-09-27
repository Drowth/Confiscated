using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Confiscated
{
    /// <summary>
    /// A temporary power cut in normal mode, as dark as Dark Mode's (same ambient, same unlit-to-lit material copies, same
    /// <c>_SchoolDarkness</c> so characters are lit only by nearby light and the caretaker's eyes glow). Everything is
    /// captured on <see cref="Begin"/> and put back exactly on <see cref="End"/>. The player's own lights (torch) stay on.
    /// </summary>
    public sealed class SchoolBlackout
    {
        readonly List<(Light light,float intensity,bool enabled)> lights=new();
        readonly List<(Renderer renderer,Material[] materials)> surfaces=new();
        readonly List<Material> copies=new();
        readonly List<(Material material,Color emission)> emitters=new();
        Color ambient,background;AmbientMode ambientMode;float ambientIntensity,reflection;bool fog;
        LightmapData[] lightmaps;Camera view;CameraClearFlags clearFlags;
        public bool Active {get;private set;}
        public IReadOnlyList<(Light light,float intensity,bool enabled)> Lights=>lights;

        public void Begin(Transform player,Camera camera)
        {
            if(Active)return;Active=true;
            ambient=RenderSettings.ambientLight;ambientMode=RenderSettings.ambientMode;ambientIntensity=RenderSettings.ambientIntensity;
            reflection=RenderSettings.reflectionIntensity;fog=RenderSettings.fog;lightmaps=LightmapSettings.lightmaps;
            view=camera;if(view!=null){clearFlags=view.clearFlags;background=view.backgroundColor;}
            foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(player==null||!l.transform.IsChildOf(player))lights.Add((l,l.intensity,l.enabled));
            var made=new Dictionary<Material,Material>();
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(r is not MeshRenderer||r.GetComponentInParent<Canvas>()!=null||(player!=null&&r.transform.IsChildOf(player))||r.GetComponentInParent<LibraryShadow>()!=null)continue;
                var originals=r.sharedMaterials;Material[] changed=null;
                for(int i=0;i<originals.Length;i++)
                {
                    var m=originals[i];if(m==null)continue;
                    bool unlit=m.shader.name=="Universal Render Pipeline/Unlit"||m.shader.name=="Confiscated/Pencil Scenery";
                    bool emission=m.HasProperty("_EmissionColor")&&m.GetColor("_EmissionColor").maxColorComponent>0;
                    if(!unlit&&!emission)continue;
                    if(!made.TryGetValue(m,out var copy))
                    {
                        // Same conversion as DarkModeController: flat art obeys the lights while the power is out.
                        copy=new Material(m){name=m.name+" (blackout)"};made.Add(m,copy);copies.Add(copy);
                        if(unlit)
                        {
                            copy.shader=Shader.Find("Universal Render Pipeline/Lit");copy.SetFloat("_Smoothness",0);
                            copy.SetFloat("_Cull",0);copy.SetFloat("_AlphaClip",1);copy.SetFloat("_Cutoff",.35f);
                            copy.EnableKeyword("_ALPHATEST_ON");copy.renderQueue=2450;
                        }
                        if(emission)emitters.Add((copy,copy.GetColor("_EmissionColor")));
                    }
                    changed??=(Material[])originals.Clone();changed[i]=copy;
                }
                if(changed!=null){surfaces.Add((r,originals));r.sharedMaterials=changed;}
            }
            LightmapSettings.lightmaps=System.Array.Empty<LightmapData>();
            if(view!=null){view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.015f,.024f,.045f);}
            SetPower(0);
        }

        /// <summary>0 = Dark Mode darkness, 1 = normal brightness (used for the stutter either side of the outage).</summary>
        public void SetPower(float level)
        {
            if(!Active)return;
            foreach(var e in lights)if(e.light!=null){e.light.intensity=e.intensity*level;e.light.enabled=e.enabled&&level>0;}
            foreach(var e in emitters)if(e.material!=null)e.material.SetColor("_EmissionColor",e.emission*level);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=Color.Lerp(new Color(.009f,.014f,.025f),ambient,level);
            RenderSettings.ambientIntensity=Mathf.Lerp(.02f,ambientIntensity,level);
            RenderSettings.reflectionIntensity=reflection*level;RenderSettings.fog=fog&&level>0;
            Shader.SetGlobalFloat("_SchoolDarkness",1-level);
        }

        public void End()
        {
            if(!Active)return;Active=false;
            foreach(var e in lights)if(e.light!=null){e.light.intensity=e.intensity;e.light.enabled=e.enabled;}
            foreach(var e in surfaces)if(e.renderer!=null)e.renderer.sharedMaterials=e.materials;
            foreach(var m in copies)if(m!=null)Object.Destroy(m);
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;RenderSettings.ambientIntensity=ambientIntensity;
            RenderSettings.reflectionIntensity=reflection;RenderSettings.fog=fog;LightmapSettings.lightmaps=lightmaps;
            if(view!=null){view.clearFlags=clearFlags;view.backgroundColor=background;}
            Shader.SetGlobalFloat("_SchoolDarkness",0);
            lights.Clear();surfaces.Clear();copies.Clear();emitters.Clear();
        }
    }
}
