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

        AudioSource source;
        bool active;
        float nextRingAt;
        bool warned;
        float ringingUntil;
        float nextNoiseEmit;
        Transform scriptedTarget;
        PlayerInteractor owner;
        bool scripted;
        public AudioSource Emitter=>source;
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
        }
        void LateUpdate()
        {
            if(source==null)return;
            var inventory=owner!=null?owner.GetComponent<PlayerInventory>():null;
            source.transform.position=scriptedTarget!=null?scriptedTarget.position:
                owner!=null&&inventory!=null&&inventory.IsEquipped(InventoryItemKind.Phone)?owner.HoldAnchor.position:transform.TransformPoint(new Vector3(.22f,.95f,-.12f));
        }
        public void PlayScriptedAt(Transform phone)
        {
            Deactivate();scripted=true;scriptedTarget=phone;LateUpdate();
            source.clip=ringClip!=null?ringClip:TempAudio.Ring;source.loop=true;source.Play();
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
