using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
namespace Confiscated
{
    /// <summary>Power fails only after the errand is assigned and the child crosses out of Year 6.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkModeController : MonoBehaviour
    {
        public InventoryItemDefinition torchItem;
        public GameObject torchPrefab;
        public Transform[] windows;
        public bool Started {get;private set;}
        public bool BlackedOut {get;private set;}
        public int FlickerTransitions {get;private set;}
        public IReadOnlyList<Light> WindowLights=>windowLights;
        public Light LockerLight {get;private set;}
        public string TorchHint=>hint!=null?hint.text:"";
        public const string LockerHint="Power's out. Follow the amber light to YOUR LOCKER for the torch. Press F to open it.";
        readonly List<Light> windowLights=new();
        readonly List<(Light light,float intensity,bool enabled)> lights=new();
        readonly List<(Renderer renderer,Material[] materials)> surfaces=new();
        readonly Dictionary<Material,Material> copies=new();
        readonly List<(Material material,Color emission)> emitters=new();
        PlayerInteractor player;
        PlayerInventory inventory;
        PlayerTorch torch;
        SchoolPeriodController period;
        Text hint;
        Transform locker;
        GameObject lockerLamp;
        Material lampMaterial;
        bool begun,captured,retry;
        Color ambient,background;
        AmbientMode ambientMode;
        float ambientIntensity,reflection;
        bool fog;
        CameraClearFlags clearFlags;
        LightmapData[] lightmaps;
        public void Begin()
        {
            if(begun||!SchoolGameMode.Dark)return;begun=true;
            period=GetComponent<GameManager>().schoolPeriod;player=period.Player;inventory=player.GetComponent<PlayerInventory>();
            inventory.StockLocker(torchItem);
            torch=player.GetComponent<PlayerTorch>();if(torch==null)torch=player.gameObject.AddComponent<PlayerTorch>();
            torch.Configure(torchPrefab);
        }
        public void PrepareRetry(){if(begun)retry=true;}
        void Update()
        {
            if(!begun||GameManager.Instance==null)return;
            if(!Started&&GameManager.Instance.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0
                &&(period.IsRoaming||retry)&&!SchoolPeriodController.InClass(player.transform.position))
                StartCoroutine(Blackout());
            if(hint!=null)
            {
                bool stored=inventory.Contains(InventoryContainer.Locker,InventoryItemKind.Torch);
                if(lockerLamp!=null)lockerLamp.SetActive(stored);
                bool show=Started&&GameManager.Instance.IsPlaying&&!ComicDialogue.IsActive&&!GameManager.Instance.lockerUI.IsOpen;
                hint.gameObject.SetActive(show);
                hint.text=torch.HasTorch?"DARK MODE    T: torch "+(torch.IsOn?"ON":"OFF"):LockerDirections();
            }
        }
        IEnumerator Blackout()
        {
            Started=true;CaptureLighting();BuildWindowLights();BuildLockerLight();BuildHint();
            // Deliberate irregular power failures, spaced well apart rather than a rapid strobe.
            float[] durations={.7f,.45f,.85f,.55f,.6f,.9f};
            for(int i=0;i<durations.Length;i++)
            {
                SetPower(i%2==0?.06f:1f);FlickerTransitions++;
                yield return new WaitForSeconds(durations[i]);
            }
            SetPower(0);FlickerTransitions++;BlackedOut=true;
            HudController.Instance?.SetStatus(LockerHint,12);
            DarkModeDialogue.PlayWorldVoice(DarkModeDialogue.PowerOut);
            HudController.Instance?.SetBark(DarkModeDialogue.PowerOut.Caption,12);
        }
        void CaptureLighting()
        {
            captured=true;ambient=RenderSettings.ambientLight;ambientMode=RenderSettings.ambientMode;
            ambientIntensity=RenderSettings.ambientIntensity;reflection=RenderSettings.reflectionIntensity;fog=RenderSettings.fog;
            lightmaps=LightmapSettings.lightmaps;LightmapSettings.lightmaps=System.Array.Empty<LightmapData>();
            clearFlags=player.ViewCamera.clearFlags;background=player.ViewCamera.backgroundColor;
            player.ViewCamera.clearFlags=CameraClearFlags.SolidColor;player.ViewCamera.backgroundColor=new Color(.015f,.024f,.045f);
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(!l.transform.IsChildOf(player.transform))lights.Add((l,l.intensity,l.enabled));
            // The original illustrated props stay identical in normal mode. Runtime copies make formerly
            // unlit props and emissive fittings obey the outage without modifying any shared art assets.
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(r is not MeshRenderer||r.GetComponentInParent<Canvas>()!=null||r.transform.IsChildOf(player.transform))continue;
                var originals=r.sharedMaterials;var changed=(Material[])originals.Clone();bool replace=false;
                for(int i=0;i<changed.Length;i++)
                {
                    var m=originals[i];if(m==null)continue;
                    bool unlit=m.shader.name=="Universal Render Pipeline/Unlit"||m.shader.name=="Confiscated/Pencil Scenery";
                    bool emission=m.HasProperty("_EmissionColor")&&m.GetColor("_EmissionColor").maxColorComponent>0;
                    if(!unlit&&!emission)continue;
                    if(!copies.TryGetValue(m,out var copy))
                    {
                        copy=new Material(m);copy.name=m.name+" (dark mode)";copies.Add(m,copy);
                        if(unlit)
                        {
                            copy.shader=Shader.Find("Universal Render Pipeline/Lit");copy.SetFloat("_Smoothness",0);
                            copy.SetFloat("_Cull",0);copy.SetFloat("_AlphaClip",1);copy.SetFloat("_Cutoff",.35f);
                            copy.EnableKeyword("_ALPHATEST_ON");copy.renderQueue=2450;
                        }
                        if(emission)emitters.Add((copy,copy.GetColor("_EmissionColor")));
                    }
                    changed[i]=copy;replace=true;
                }
                if(replace){surfaces.Add((r,originals));r.sharedMaterials=changed;}
            }
        }
        void BuildWindowLights()
        {
            if(windows==null)return;
            foreach(var w in windows)
            {
                if(w==null)continue;
                Vector3 p=w.position;
                Vector3 inward=p.x<-35?Vector3.right:p.x>35?Vector3.left:p.z<1?Vector3.forward:Vector3.back;
                var g=new GameObject("Moonlight through "+w.name);g.transform.SetParent(transform,false);
                g.transform.position=p+inward*.24f;
                g.transform.rotation=Quaternion.LookRotation(inward+Vector3.down*.30f);
                var l=g.AddComponent<Light>();l.type=LightType.Spot;l.color=new Color(.54f,.69f,1);
                l.intensity=4;l.range=14;l.spotAngle=100;l.innerSpotAngle=65;
                l.shadows=LightShadows.Soft;l.shadowStrength=1;l.shadowBias=.025f;l.shadowNormalBias=.08f;l.shadowNearPlane=.08f;
                l.shadowCustomResolution=256;
                l.renderMode=LightRenderMode.ForcePixel;windowLights.Add(l);
            }
        }
        void SetPower(float level)
        {
            foreach(var entry in lights)if(entry.light!=null){entry.light.intensity=entry.intensity*level;entry.light.enabled=entry.enabled&&level>0;}
            foreach(var e in emitters)e.material.SetColor("_EmissionColor",e.emission*level);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=Color.Lerp(new Color(.009f,.014f,.025f),ambient,level);
            RenderSettings.ambientIntensity=Mathf.Lerp(.02f,ambientIntensity,level);
            RenderSettings.reflectionIntensity=reflection*level;RenderSettings.fog=false;
            Shader.SetGlobalFloat("_SchoolDarkness",1-level);
        }
        void BuildLockerLight()
        {
            var target=FindFirstObjectByType<PlayerLocker>();if(target==null)return;locker=target.transform;
            // Created after the mains-light snapshot: this small battery lamp survives the outage.
            lockerLamp=new GameObject("Locker battery light");lockerLamp.transform.SetParent(transform,false);
            lockerLamp.transform.SetPositionAndRotation(locker.position+Vector3.up*1.98f-locker.forward*.28f,locker.rotation);
            lampMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lampMaterial.SetColor("_BaseColor",new Color(1,.65f,.18f));
            var lens=GameObject.CreatePrimitive(PrimitiveType.Cube);lens.name="Amber locator lens";lens.transform.SetParent(lockerLamp.transform,false);
            lens.transform.localScale=new Vector3(.32f,.09f,.07f);Destroy(lens.GetComponent<Collider>());
            var renderer=lens.GetComponent<MeshRenderer>();renderer.sharedMaterial=lampMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var glow=new GameObject("Battery light spill");glow.transform.SetParent(lockerLamp.transform,false);glow.transform.localPosition=new Vector3(0,-.15f,-.2f);
            LockerLight=glow.AddComponent<Light>();LockerLight.type=LightType.Point;LockerLight.color=new Color(1,.65f,.24f);
            LockerLight.range=7;LockerLight.intensity=4;LockerLight.shadows=LightShadows.Soft;
            LockerLight.shadowBias=.025f;LockerLight.shadowNormalBias=.04f;LockerLight.shadowNearPlane=.08f;
            LockerLight.renderMode=LightRenderMode.ForcePixel;
        }
        string LockerDirections()
        {
            if(locker==null)return "TORCH IN YOUR LOCKER\nCorridor beyond Year 6  |  F: open locker";
            Vector3 delta=locker.position-player.transform.position;delta.y=0;
            if(delta.magnitude<2)return "YOUR LOCKER — TORCH INSIDE\nLook at the locker and press F";
            Vector3 forward=player.ViewCamera.transform.forward;forward.y=0;
            float angle=Vector3.SignedAngle(forward,delta,Vector3.up);
            string direction=Mathf.Abs(angle)>115?"TURN AROUND":angle>25?"TURN RIGHT":angle< -25?"TURN LEFT":"AHEAD";
            return "TORCH  |  "+Mathf.CeilToInt(delta.magnitude)+" m  |  "+direction+"\nFind the amber-lit locker";
        }
        void BuildHint()
        {
            var hud=HudController.Instance;if(hud==null)return;
            var go=new GameObject("Dark mode torch reminder",typeof(RectTransform));go.transform.SetParent(hud.GetComponentInParent<Canvas>().transform,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-30,-130);r.sizeDelta=new Vector2(470,76);
            hint=go.AddComponent<Text>();hint.font=SchoolTypography.Font;hint.fontSize=23;hint.alignment=TextAnchor.UpperRight;hint.color=new Color(.95f,.91f,.75f);hint.raycastTarget=false;
            var outline=go.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(2,-2);
        }
        void OnDestroy()
        {
            Shader.SetGlobalFloat("_SchoolDarkness",0);
            if(!captured)return;
            foreach(var entry in lights)if(entry.light!=null){entry.light.intensity=entry.intensity;entry.light.enabled=entry.enabled;}
            foreach(var entry in surfaces)if(entry.renderer!=null)entry.renderer.sharedMaterials=entry.materials;
            foreach(var m in copies.Values)Destroy(m);
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;RenderSettings.ambientIntensity=ambientIntensity;
            RenderSettings.reflectionIntensity=reflection;RenderSettings.fog=fog;LightmapSettings.lightmaps=lightmaps;
            if(player!=null&&player.ViewCamera!=null){player.ViewCamera.clearFlags=clearFlags;player.ViewCamera.backgroundColor=background;}
            if(hint!=null)Destroy(hint.gameObject);
            if(lockerLamp!=null)Destroy(lockerLamp);
            if(lampMaterial!=null)Destroy(lampMaterial);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShader()=>Shader.SetGlobalFloat("_SchoolDarkness",0);
    }
}
