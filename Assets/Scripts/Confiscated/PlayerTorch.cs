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
        bool on,wasCarried;
        public bool HasTorch=>inventory!=null&&inventory.HasCarried(InventoryItemKind.Torch);
        public bool IsOn=>beam!=null&&beam.enabled;
        public Light Beam=>beam;
        public void Configure(GameObject prefab)
        {
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
            if(!SchoolGameMode.Dark||GameManager.Instance==null||!GameManager.Instance.IsPlaying||player.InputLocked||ComicDialogue.IsActive||Time.timeScale<=0)return false;
            if(!HasTorch){HudController.Instance?.SetStatus("Follow the amber light to your locker. Press F, then move the torch into your satchel.",6);return false;}
            on=!on;return true;
        }
        void Update(){if(Keyboard.current!=null&&Keyboard.current.tKey.wasPressedThisFrame)Toggle();}
        void LateUpdate()
        {
            bool carried=HasTorch;
            if(carried&&!wasCarried){on=true;HudController.Instance?.SetStatus("Torch collected. Press T to turn it on / off.",7);}
            if(!carried)on=false;wasCarried=carried;
            bool playing=SchoolGameMode.Dark&&GameManager.Instance!=null&&GameManager.Instance.IsPlaying;
            if(beam!=null)beam.enabled=playing&&carried&&on;
            if(model!=null)model.SetActive(playing&&carried&&!player.InputLocked);
        }
        void OnDestroy(){if(beam!=null)Destroy(beam.gameObject);if(model!=null)Destroy(model);}
    }
}
