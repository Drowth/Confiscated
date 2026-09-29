using UnityEngine;
using UnityEngine.InputSystem;
namespace Confiscated
{
    [DisallowMultipleComponent]
    public sealed class PlayerTorch : MonoBehaviour
    {
        PlayerInteractor player;
        PlayerInventory inventory;
        GameObject model;
        Light beam;
        bool on,wasCarried,flat;
        float baseIntensity=16;
        // Day play only (Docs/LibraryMaze.md): a battery like the stamina bar, so the torch cannot hold the library
        // shadow off for ever. Night (Dark Mode) keeps its unlimited torch.
        public const float LightSeconds=20,RechargeSeconds=30,WarnFraction=.2f,RestartFraction=.25f;
        public static bool Limited=>!SchoolGameMode.Dark;
        public float Battery {get;private set;}=1;
        public bool Flat=>flat;
        /// <summary>Tests: skip the wait for a drain or a recharge.</summary>
        public void SetBattery(float value){Battery=Mathf.Clamp01(value);if(Battery>=RestartFraction)flat=false;}
        public bool HasTorch=>inventory!=null&&inventory.HasCarried(InventoryItemKind.Torch);
        public bool IsOn=>beam!=null&&beam.enabled;
        public Light Beam=>beam;
        public void Configure(GameObject prefab)
        {
            if(beam!=null)return;
            player=GetComponent<PlayerInteractor>();inventory=GetComponent<PlayerInventory>();
            var g=new GameObject("Player torch beam");g.transform.SetParent(player.ViewCamera.transform,false);
            g.transform.localPosition=new Vector3(.09f,-.07f,.12f);
            beam=g.AddComponent<Light>();beam.type=LightType.Spot;beam.color=new Color(1,.93f,.76f);
            beam.range=24;beam.intensity=16;beam.spotAngle=56;beam.innerSpotAngle=30;
            beam.shadows=LightShadows.Soft;beam.shadowBias=.02f;beam.shadowNormalBias=.04f;beam.shadowNearPlane=.04f;
            beam.renderMode=LightRenderMode.ForcePixel;beam.enabled=false;
            if(prefab!=null)
            {
                model=Instantiate(prefab,player.ViewCamera.transform);model.name="Held torch";
                model.transform.localPosition=new Vector3(-.23f,-.22f,.40f);model.transform.localRotation=Quaternion.Euler(-3,5,-8);model.SetActive(false);
            }
        }
        public bool Toggle()
        {
            if(GameManager.Instance==null||!GameManager.Instance.IsPlaying||player.InputLocked||ComicDialogue.IsActive||Time.timeScale<=0)return false;
            if(!HasTorch){HudController.Instance?.SetStatus(SchoolGameMode.Dark?"Follow the amber light to your locker. Press F, then move the torch into your satchel.":"Your torch is in your locker. Press F at the locker, then move it into your satchel.",6);return false;}
            if(!on&&flat){HudController.Instance?.SetStatus("Torch battery's flat. Give it a moment to recharge.",3);return false;}
            on=!on;ContextualControlHints.Used(ContextualControlHints.Action.Torch);return true;
        }
        void Update(){if(Keyboard.current!=null&&Keyboard.current.tKey.wasPressedThisFrame)Toggle();}
        void LateUpdate()
        {
            bool carried=HasTorch;
            if(carried&&!wasCarried){on=!Limited;HudController.Instance?.SetStatus(Limited?"Torch collected. Press T to turn it on / off. Its battery drains while it's on.":"Torch collected. Press T to turn it on / off.",7);}
            if(!carried)on=false;wasCarried=carried;
            bool playing=GameManager.Instance!=null&&GameManager.Instance.IsPlaying;
            if(Limited&&playing&&Time.timeScale>0)
            {
                if(on&&carried){Battery=Mathf.Max(0,Battery-Time.deltaTime/LightSeconds);if(Battery<=0){on=false;flat=true;HudController.Instance?.SetStatus("The torch dies.",2.5f);}}
                else Battery=Mathf.Min(1,Battery+Time.deltaTime/RechargeSeconds);
                if(flat&&Battery>=RestartFraction)flat=false;
            }
            else if(!Limited){Battery=1;flat=false;}
            if(beam!=null)
            {
                beam.enabled=playing&&carried&&on;
                // Low battery: the beam browns out and stutters.
                float low=Limited&&Battery<WarnFraction?Battery/WarnFraction:1;
                bool stutter=Limited&&Battery<WarnFraction&&Mathf.PerlinNoise(Time.time*9,0)>.72f;
                // The library's darkness pulls exposure right down; the beam is lifted to match, so it still reads.
                beam.intensity=baseIntensity*Mathf.Lerp(.45f,1,low)*(stutter?.15f:1)*LibraryDarkness.LiftFactor;
            }
            if(model!=null)model.SetActive(playing&&carried&&!player.InputLocked);
        }
        void OnDestroy(){if(beam!=null)Destroy(beam.gameObject);if(model!=null)Destroy(model);}
    }
}
