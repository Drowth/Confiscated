using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>Compact escape HUD and restrained, readable pursuit feedback.</summary>
    public sealed class EscapeRunFeedback : MonoBehaviour
    {
        SchoolRunController run;Canvas canvas;Text kit,controls,staminaLabel;SketchStaminaBar stamina;AudioSource ambience,steps,pickupAudio;AudioClip hum,step,sting;
        ThreatVignette vignette;Light[] lights;float[] intensities;float nextStep,danger;bool built;float oldFont;
        bool wasChasing;float spottedAt=-10,flickerAt=-10;int lastCount;PickupChecklist checklist;
        void Start()
        {
            run=GetComponent<SchoolRunController>();lights=FindObjectsByType<Light>(FindObjectsSortMode.None);intensities=new float[lights.Length];for(int i=0;i<lights.Length;i++)intensities[i]=lights[i].intensity;
            hum=Resources.Load<AudioClip>("Audio/SchoolAmbience");step=Tone("Caretaker keys and footsteps",.3f,false);sting=Resources.Load<AudioClip>("Audio/ItemPickup");
            pickupAudio=gameObject.AddComponent<AudioSource>();pickupAudio.playOnAwake=false;pickupAudio.spatialBlend=0;pickupAudio.volume=.65f;
            ambience=gameObject.AddComponent<AudioSource>();ambience.playOnAwake=false;ambience.loop=true;ambience.spatialBlend=0;ambience.clip=hum;ambience.volume=0;if(hum!=null)ambience.Play();
            steps=run.caretaker.gameObject.AddComponent<AudioSource>();steps.spatialBlend=1;steps.rolloffMode=AudioRolloffMode.Linear;steps.minDistance=2;steps.maxDistance=28;steps.playOnAwake=false;
        }
        public void PickupCue(){if(pickupAudio!=null&&sting!=null)pickupAudio.PlayOneShot(sting);}
        void Build()
        {
            var parent=HudController.Instance.GetComponentInParent<Canvas>().transform;
            var go=new GameObject("Escape supplies HUD",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);canvas=go.GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=10;
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var edge=new GameObject("Pursuit edge shade",typeof(RectTransform),typeof(ThreatVignette));edge.transform.SetParent(go.transform,false);var er=edge.GetComponent<RectTransform>();er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=er.offsetMax=Vector2.zero;vignette=edge.GetComponent<ThreatVignette>();vignette.raycastTarget=false;
            kit=Label(go.transform,new Vector2(24,65),new Vector2(750,40),20);
            controls=Label(go.transform,new Vector2(24,18),new Vector2(950,36),18);
            staminaLabel=Label(go.transform,new Vector2(24,104),new Vector2(300,32),24);
            staminaLabel.text="STAMINA";
            var track=new GameObject("Sketch stamina bar",typeof(RectTransform),typeof(SketchStaminaBar));track.transform.SetParent(go.transform,false);
            var r=track.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(24,65);r.sizeDelta=new Vector2(280,34);
            stamina=track.GetComponent<SketchStaminaBar>();stamina.raycastTarget=false;
            kit.rectTransform.anchoredPosition=new Vector2(24,143);
            var list=new GameObject("Belongings checklist",typeof(RectTransform),typeof(PickupChecklist));list.transform.SetParent(go.transform,false);
            var lr=list.GetComponent<RectTransform>();lr.anchorMin=lr.anchorMax=lr.pivot=new Vector2(1,1);lr.anchoredPosition=new Vector2(-24,-24);lr.sizeDelta=new Vector2(440,90);checklist=list.GetComponent<PickupChecklist>();checklist.raycastTarget=false;checklist.SetIcons(run.checklistIcons);
            oldFont=HudController.Instance.objectiveText.fontSize;HudController.Instance.objectiveText.fontSize=26;built=true;
        }
        static Text Label(Transform parent,Vector2 position,Vector2 size,int fontSize){var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=position;r.sizeDelta=size;var t=go.GetComponent<Text>();t.font=SchoolTypography.Font;t.fontSize=fontSize;t.color=new Color(.93f,.91f,.78f);t.raycastTarget=false;return t;}
        void Update()
        {
            if(run==null||GameManager.Instance==null)return;
            bool active=run.RoundStarted&&GameManager.Instance.IsPlaying;
            bool visible=GameManager.Instance.IsPlaying&&!run.period.Player.GetComponent<FirstPersonController>().MovementLocked&&!ComicDialogue.IsActive;
            if(visible&&!built&&HudController.Instance!=null)Build();
            if(canvas!=null)
            {
                canvas.gameObject.SetActive(visible);
                kit.gameObject.SetActive(active);controls.gameObject.SetActive(active);vignette.gameObject.SetActive(active);
                checklist.gameObject.SetActive(run.RecoveryBegun);
                stamina.SetFraction(run.period.Player.GetComponent<FirstPersonController>().SprintFraction);
                staminaLabel.text=stamina.Fraction<=.2f?"STAMINA - LOW":"STAMINA";
            }
            if(active)
            {
                var p=run.period.Player;var decoy=p.GetComponent<ClockworkDecoy>();
                kit.text=(run.HasBoltCutters?"Cutters ready  ":"")+(run.HasStoreKey?"Store key ready":"");
                controls.text="F: interact     Q / E: lean     Tab: bag     Shift: run     Space: look back     F8: effects";
                float distance=Vector3.Distance(p.transform.position,run.caretaker.transform.position);
                danger=Mathf.MoveTowards(danger,run.caretaker.Current==CaretakerAI.State.Chase?1:Mathf.Clamp01(1-distance/16)*.5f,Time.deltaTime);
                vignette.SetStrength(.18f+danger*.25f);var agent=run.caretaker.GetComponent<NavMeshAgent>();
                bool chasing=run.caretaker.Current==CaretakerAI.State.Chase;if(chasing&&!wasChasing)spottedAt=Time.time;wasChasing=chasing;
                vignette.SetAlert(Time.time-spottedAt<1?Mathf.Sin((Time.time-spottedAt)*Mathf.PI):0);
                if(run.Count>lastCount&&run.Count>=4)flickerAt=Time.time;lastCount=run.Count;
                if(run.caretaker.GetComponent<CaretakerGait>()==null&&agent.velocity.sqrMagnitude>.2f&&Time.time>nextStep){nextStep=Time.time+(run.caretaker.Current==CaretakerAI.State.Chase?.43f:.75f);steps.pitch=Random.Range(.92f,1.07f);steps.PlayOneShot(step,.65f);}
            }
            if(ambience!=null)
            {
                bool inSchool=GameManager.Instance.IsPlaying||GameManager.Instance.Current==GameManager.State.Detention;
                float target=inSchool?(ComicDialogue.IsActive?.035f:.12f):0;
                ambience.volume=Mathf.MoveTowards(ambience.volume,target,Time.unscaledDeltaTime*.15f);
            }
            float dim=active?Mathf.Lerp(.9f,.68f,run.Count/5f):1;
            float age=Time.time-flickerAt;float dip=active&&age<.5f?1-.24f*Mathf.Sin(age/.5f*Mathf.PI):1;
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].intensity=Mathf.MoveTowards(lights[i].intensity,intensities[i]*dim*dip,Time.deltaTime*(age<.6f?4:.3f));
        }
        static AudioClip Tone(string name,float seconds,bool loop)
        {
            const int rate=22050;float[] data=new float[Mathf.CeilToInt(seconds*rate)];var rng=new System.Random(83);float noise=0;
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;noise=Mathf.Lerp(noise,(float)rng.NextDouble()*2-1,.25f);data[i]=loop?(Mathf.Sin(2*Mathf.PI*55*t)*.35f+Mathf.Sin(2*Mathf.PI*83*t)*.12f):(Mathf.Sin(2*Mathf.PI*95*t)*.5f*Mathf.Exp(-t*25)+noise*.3f*Mathf.Exp(-t*15)+Mathf.Sin(2*Mathf.PI*1430*t)*.06f*Mathf.Exp(-t*10));}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){for(int i=0;lights!=null&&i<lights.Length;i++)if(lights[i]!=null)lights[i].intensity=intensities[i];if(canvas!=null)Destroy(canvas.gameObject);if(steps!=null)Destroy(steps);if(ambience!=null)Destroy(ambience);if(step!=null)Destroy(step);if(pickupAudio!=null)Destroy(pickupAudio);}
    }
}

