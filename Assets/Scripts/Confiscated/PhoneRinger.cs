using UnityEngine;

namespace Confiscated
{
    /// <summary>
    /// Once the player has the phone it rings on a schedule. A warning (buzz + HUD) comes first so the player
    /// can get clear; the ring itself is a loud noise at the player's position that the caretaker investigates.
    /// </summary>
    public class PhoneRinger : MonoBehaviour
    {
        [SerializeField] float firstRingDelay = 12f;
        [SerializeField] float ringInterval = 22f;
        [SerializeField] float warningLead = 4f;
        [SerializeField] float ringDuration = 3.2f;
        [SerializeField] float ringNoiseRadius = 40f;
        [SerializeField] AudioClip ringClip;
        [SerializeField] AudioClip warnClip;

        AudioSource source,hum;
        // The phone on vibrate: the desk prop shakes in buzzing bursts while it rings, and a buzz sits under the ringtone.
        Transform buzzing;Vector3 restPosition;Quaternion restRotation;
        bool active;
        float nextRingAt;
        bool warned;
        float ringingUntil;
        float nextNoiseEmit;
        Transform scriptedTarget;
        PlayerInteractor owner;
        bool scripted;
        public AudioSource Emitter=>source;
        /// <summary>The desk phone is shaking on vibrate (the opening ring).</summary>
        public bool Vibrating=>buzzing!=null;
        public bool Buzzing=>hum!=null&&hum.isPlaying;
        public AudioClip Ringtone=>ringClip;

        public bool IsRinging => Time.time < ringingUntil;
        public float SecondsToRing => active ? Mathf.Max(0f, nextRingAt - Time.time) : -1f;

        void Awake()
        {
            owner=GetComponent<PlayerInteractor>();
            var old=GetComponent<AudioSource>();if(old!=null){old.Stop();old.playOnAwake=false;}
            var emitter=new GameObject("Phone sound emitter");emitter.transform.SetParent(transform,false);
            source=emitter.AddComponent<AudioSource>();
            source.spatialBlend = 1f;source.minDistance=.8f;source.maxDistance=24;source.dopplerLevel=0;source.volume=.65f;
            source.playOnAwake = false;
            hum=emitter.AddComponent<AudioSource>();hum.spatialBlend=1f;hum.minDistance=.8f;hum.maxDistance=16;hum.dopplerLevel=0;hum.volume=.5f;
            hum.playOnAwake=false;hum.loop=true;hum.clip=TempAudio.Buzz;
        }
        void LateUpdate()
        {
            if(source==null)return;
            var inventory=owner!=null?owner.GetComponent<PlayerInventory>():null;
            source.transform.position=scriptedTarget!=null?scriptedTarget.position:
                owner!=null&&inventory!=null&&inventory.IsEquipped(InventoryItemKind.Phone)?owner.HoldAnchor.position:transform.TransformPoint(new Vector3(.22f,.95f,-.12f));
            bool ringing=source.isPlaying&&source.loop;
            if(ringing&&!hum.isPlaying)hum.Play();else if(!ringing&&hum.isPlaying)hum.Stop();
            if(buzzing==null)return;
            if(!ringing){StopBuzzing();return;}
            // Buzzing bursts (the same rhythm as the buzz sound), unscaled so it keeps going under Reed's dialogue.
            float t=Time.unscaledTime;bool on=(t%.4f)<.26f;
            Vector3 jitter=on?new Vector3(Mathf.PerlinNoise(t*60,0)-.5f,0,Mathf.PerlinNoise(0,t*60)-.5f)*.012f:Vector3.zero;
            buzzing.localPosition=restPosition+jitter;buzzing.localRotation=restRotation*Quaternion.Euler(0,on?(Mathf.PerlinNoise(t*55,3)-.5f)*10:0,0);
        }
        void StopBuzzing(){if(buzzing==null)return;buzzing.localPosition=restPosition;buzzing.localRotation=restRotation;buzzing=null;}
        public void PlayScriptedAt(Transform phone)
        {
            Deactivate();scripted=true;scriptedTarget=phone;
            buzzing=phone;restPosition=phone.localPosition;restRotation=phone.localRotation;
            source.clip=ringClip!=null?ringClip:TempAudio.Ring;source.loop=true;source.Play();LateUpdate();
        }

        public void Activate()
        {
            if (SchoolRunController.Instance != null) { Deactivate(); return; }
            scripted=false;scriptedTarget=null;
            active = true;
            nextRingAt = Time.time + firstRingDelay;
            warned = false;
        }

        public void Deactivate()
        {
            scripted=false;scriptedTarget=null;
            active = false;
            ringingUntil = 0f;
            CancelInvoke(nameof(StopRing));
            if (source != null) { source.Stop(); source.loop = false; }
            if (hum != null) hum.Stop();
            StopBuzzing();
            HudController.Instance?.SetPhoneState(HudController.PhoneState.Hidden, 0f);
        }

        void Update()
        {
            if(scripted)return;
            if (!active || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            var hud = HudController.Instance;
            float t = Time.time;

            if (IsRinging)
            {
                hud?.SetPhoneState(HudController.PhoneState.Ringing, 0f);
                if (t >= nextNoiseEmit)
                {
                    nextNoiseEmit = t + 0.5f;
                    NoiseEvents.Emit(source.transform.position, ringNoiseRadius, "phone");
                }
                return;
            }

            float remaining = nextRingAt - t;
            if (remaining <= 0f)
            {
                ringingUntil = t + ringDuration;
                nextRingAt = t + ringInterval;
                warned = false;
                source.clip = ringClip != null ? ringClip : TempAudio.Ring;
                source.loop = true;
                source.Play();
                Invoke(nameof(StopRing), ringDuration);
                hud?.SetPhoneState(HudController.PhoneState.Ringing, 0f);
                return;
            }

            if (remaining <= warningLead)
            {
                if (!warned)
                {
                    warned = true;
                    source.PlayOneShot(warnClip != null ? warnClip : TempAudio.Warn, 0.8f);
                }
                hud?.SetPhoneState(HudController.PhoneState.AboutToRing, remaining);
            }
            else hud?.SetPhoneState(HudController.PhoneState.Idle, remaining);
        }

        void StopRing()
        {
            source.Stop();
            source.loop = false;
            HudController.Instance?.SetPhoneState(HudController.PhoneState.Idle, nextRingAt - Time.time);
        }
    }
}
