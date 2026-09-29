using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Confiscated
{
    /// <summary>
    /// The ceiling fluorescents are the level's light, but a point light's shadow is six shadow maps, so they can't all cast.
    /// Only the few nearest the player (in view of the camera first) do; their shadows fade in and out as it moves, so the
    /// room you're in always has real furniture and character shadows at a fixed cost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShadowLightBudget : MonoBehaviour
    {
        public int budget=4;
        public float strength=.7f,fadeSeconds=.35f;
        readonly List<Light> pool=new();
        readonly List<Light> ranked=new();
        readonly HashSet<Light> wanted=new();
        float next;

        void Start()
        {
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(l.type!=LightType.Point||l.transform.parent==null||!l.transform.parent.name.StartsWith("P_CeilingLight"))continue;
                pool.Add(l);l.shadows=LightShadows.None;l.shadowStrength=0;l.shadowBias=.04f;l.shadowNormalBias=.3f;l.shadowNearPlane=.1f;
                var data=l.GetComponent<UniversalAdditionalLightData>();if(data==null)data=l.gameObject.AddComponent<UniversalAdditionalLightData>();
                data.additionalLightsShadowResolutionTier=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
            }
        }

        void Update()
        {
            var cam=Camera.main;if(cam==null||pool.Count==0)return;
            if(Time.unscaledTime>=next)
            {
                next=Time.unscaledTime+.2f;
                Vector3 eye=cam.transform.position;
                ranked.Clear();
                foreach(var l in pool)if(l!=null&&l.isActiveAndEnabled&&l.intensity>0&&(l.transform.position-eye).sqrMagnitude<l.range*l.range*1.6f)ranked.Add(l);
                // A light behind a wall throws no shadow you can see: lights with a clear line to the eye come first.
                ranked.Sort((a,b)=>Score(a,eye).CompareTo(Score(b,eye)));
                wanted.Clear();for(int i=0;i<ranked.Count&&wanted.Count<budget;i++)wanted.Add(ranked[i]);
            }
            float step=Time.unscaledDeltaTime*strength/Mathf.Max(.01f,fadeSeconds);
            foreach(var l in pool)
            {
                if(l==null)continue;
                bool want=wanted.Contains(l);
                if(!want&&l.shadows==LightShadows.None)continue;
                if(want&&l.shadows==LightShadows.None)l.shadows=LightShadows.Soft;
                l.shadowStrength=Mathf.MoveTowards(l.shadowStrength,want?strength:0,step);
                if(!want&&l.shadowStrength<=0)l.shadows=LightShadows.None;
            }
        }

        static float Score(Light l,Vector3 eye)
        {
            Vector3 p=l.transform.position+Vector3.down*.15f;float d=(p-eye).sqrMagnitude;
            return Physics.Linecast(eye,p,out var hit,~0,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(l.transform.parent)?d+400:d;
        }
    }
}
