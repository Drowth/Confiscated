using UnityEngine;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>Owns the HUD widgets built by the scene builder. All calls are null-safe on missing widgets.</summary>
    public class HudController : MonoBehaviour
    {
        public enum PhoneState { Hidden, Idle, AboutToRing, Ringing }

        public static HudController Instance { get; private set; }

        public Text objectiveText;
        public Text promptText;
        public Text statusText;
        public Text phoneText;
        public Image phoneImage;
        public Image holdBar;
        public GameObject overlay;
        public Text overlayTitle;
        public Text overlayBody;

        RectTransform phoneRect;
        Vector2 phoneRestPos;
        Image caretakerSeenImage;
        RectTransform caretakerSeenRect;
        Vector2 caretakerSeenRestPos;
        CaretakerAI caretaker;
        float statusUntil;
        PlayerInventory inventory;

        void Awake()
        {
            Instance = this;
            inventory=Object.FindFirstObjectByType<PlayerInventory>();
            if (phoneImage != null)
            {
                phoneRect = phoneImage.rectTransform;
                phoneRestPos = phoneRect.anchoredPosition;
                phoneImage.gameObject.SetActive(false);
            }
            if (phoneText != null) phoneText.text = "";
            if (overlay != null) overlay.SetActive(false);
            ConfigureHoldBar();
            BuildCaretakerSeenIndicator();
            SetPrompt(null);
            SetHoldProgress(-1f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (statusText != null && statusText.text.Length > 0 && Time.time > statusUntil) statusText.text = "";
            UpdateCaretakerSeenIndicator();
            UpdateTrespassWarning();
        }

        // Built on first use so it survives HUD rebuilds. Shown only where being seen is an offence (SchoolRunController.Trespassing).
        Text trespassText;
        public bool TrespassWarningVisible=>trespassText!=null&&trespassText.gameObject.activeSelf;
        void UpdateTrespassWarning()
        {
            var run=SchoolRunController.Instance;var game=GameManager.Instance;
            bool show=run!=null&&game!=null&&game.IsPlaying&&!ComicDialogue.IsActive&&run.Trespassing;
            if(trespassText==null)
            {
                if(!show||objectiveText==null)return;
                var canvas=GetComponentInParent<Canvas>();if(canvas==null)return;
                var go=new GameObject("Trespassing warning",typeof(RectTransform));go.transform.SetParent(canvas.transform,false);
                var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-28);rect.sizeDelta=new Vector2(900,90);
                trespassText=go.AddComponent<Text>();trespassText.font=objectiveText.font;trespassText.fontSize=34;trespassText.alignment=TextAnchor.UpperCenter;
                trespassText.raycastTarget=false;trespassText.horizontalOverflow=HorizontalWrapMode.Overflow;trespassText.verticalOverflow=VerticalWrapMode.Overflow;
                trespassText.text="TRESPASSING\n<size=22>Out of bounds. Don't let the caretaker see you here.</size>";
                var outline=go.AddComponent<Outline>();outline.effectColor=new Color(.05f,.05f,.08f,.9f);outline.effectDistance=new Vector2(2,-2);
            }
            if(trespassText.gameObject.activeSelf!=show)trespassText.gameObject.SetActive(show);
            if(!show)return;
            // A slow pulse in the HUD's low-stamina terracotta: noticeable without flashing.
            float pulse=.78f+.22f*Mathf.Sin(Time.unscaledTime*3.2f);
            trespassText.color=new Color(.86f,.36f,.22f,pulse);
        }

        void BuildCaretakerSeenIndicator()
        {
            var sprite=Resources.Load<Sprite>("Art/CaretakerSeenIndicator");
            var canvas=GetComponentInParent<Canvas>();
            if(sprite==null||canvas==null)return;
            var go=new GameObject("Caretaker can see you",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(canvas.transform,false);
            caretakerSeenImage=go.GetComponent<Image>();
            caretakerSeenImage.sprite=sprite;
            caretakerSeenImage.preserveAspect=true;
            caretakerSeenImage.raycastTarget=false;
            caretakerSeenImage.color=new Color(1f,1f,1f,.97f);
            caretakerSeenRect=caretakerSeenImage.rectTransform;
            caretakerSeenRect.anchorMin=caretakerSeenRect.anchorMax=caretakerSeenRect.pivot=Vector2.one;
            caretakerSeenRect.sizeDelta=new Vector2(132f,132f);
            caretakerSeenRestPos=new Vector2(-34f,-30f);
            caretakerSeenRect.anchoredPosition=caretakerSeenRestPos;
            go.SetActive(false);
        }

        void UpdateCaretakerSeenIndicator()
        {
            if(caretakerSeenImage==null)return;
            if(caretaker==null)caretaker=Object.FindFirstObjectByType<CaretakerAI>();
            var game=GameManager.Instance;
            bool seen=caretaker!=null&&caretaker.CanCurrentlySeePlayer&&game!=null&&game.IsPlaying&&!ComicDialogue.IsActive;
            if(caretakerSeenImage.gameObject.activeSelf!=seen)caretakerSeenImage.gameObject.SetActive(seen);
            if(!seen)return;

            // A distant sighting trembles; the movement becomes faster and much wider near catching range.
            float closeness=caretaker.VisibilityCloseness;
            float urgency=closeness*closeness;
            float amplitude=Mathf.Lerp(1.25f,13f,urgency);
            float frequency=Mathf.Lerp(15f,42f,closeness);
            float phase=Time.unscaledTime*frequency;
            // The belongings checklist (EscapeRunFeedback: top right, 90 tall, drawn above this) appears once recovery begins; sit clear below it, shake included.
            var run=SchoolRunController.Instance;
            caretakerSeenRestPos=new Vector2(-34f,run!=null&&run.RecoveryBegun?-134f:-30f);
            caretakerSeenRect.anchoredPosition=caretakerSeenRestPos+new Vector2(
                Mathf.Sin(phase*1.73f),Mathf.Cos(phase*2.17f))*amplitude;
            caretakerSeenRect.localRotation=Quaternion.Euler(0,0,Mathf.Sin(phase*1.31f)*Mathf.Lerp(.5f,4.5f,urgency));
            float pulse=1f+Mathf.Sin(phase*.78f)*Mathf.Lerp(.008f,.055f,urgency);
            caretakerSeenRect.localScale=Vector3.one*pulse;
        }

        public void SetObjective(string text) { if (objectiveText != null) objectiveText.text = text ?? ""; }

        public void SetPrompt(string text) { if (promptText != null) promptText.text = text ?? ""; }

        public void SetStatus(string text, float seconds = 2.5f)
        {
            text=DarkModeDialogue.Resolve(text);
            var run = SchoolRunController.Instance;
            bool chaseBark = run != null && (run.RoundStarted || (run.caretaker.GetComponent<CaretakerPassCheck>()?.WitnessedOffence ?? false));
            if(!chaseBark && ComicDialogue.TrySpeak(text)){if(statusText!=null)statusText.text="";return;}
            if(DarkModeDialogue.Active&&DarkModeDialogue.TryCaption(text,out var darkLine))seconds=Mathf.Max(seconds,DarkModeDialogue.PlayWorldVoice(darkLine));
            if (statusText == null) return;
            statusText.text = text ?? "";
            statusUntil = Time.time + seconds;
        }
        // In-world remarks keep the camera and movement with the player, including Reed calling from the classroom.
        public void SetBark(string text,float seconds)
        {
            if(statusText==null)return;statusText.text=text??"";statusUntil=Time.time+seconds;
        }

        public void SetHoldProgress(float p)
        {
            if (holdBar == null) return;
            bool show = p >= 0f;
            holdBar.transform.parent.gameObject.SetActive(show);
            if (!show) return;
            float amount = Mathf.Clamp01(p);
            holdBar.fillAmount = amount;
            // The bar deliberately uses Unity's sprite-less white graphic. Filled Images do not
            // generate dependable fill geometry without a source sprite, so reveal it by scale.
            var scale = holdBar.rectTransform.localScale;
            holdBar.rectTransform.localScale = new Vector3(amount, scale.y, scale.z);
        }

        public void ConfigureHoldBar()
        {
            if (holdBar == null) return;
            holdBar.type = Image.Type.Simple;
            holdBar.rectTransform.pivot = new Vector2(0f, .5f);
            holdBar.rectTransform.localScale = new Vector3(0f, 1f, 1f);
        }

        public void SetPhoneState(PhoneState state, float secondsToRing)
        {
            if (phoneImage == null) return;
            bool activePhone=state!=PhoneState.Hidden;
            bool visible = activePhone&&(inventory==null||inventory.IsEquipped(InventoryItemKind.Phone));
            if (phoneImage.gameObject.activeSelf != visible) phoneImage.gameObject.SetActive(visible);
            if (!activePhone) { if (phoneText != null) phoneText.text = ""; return; }

            switch (state)
            {
                case PhoneState.Idle:
                    phoneRect.anchoredPosition = phoneRestPos;
                    if (phoneText != null) phoneText.text = "";
                    break;
                case PhoneState.AboutToRing:
                    phoneRect.anchoredPosition = phoneRestPos + new Vector2(Mathf.Sin(Time.time * 60f) * 4f, 0f);
                    if (phoneText != null) phoneText.text = "Your phone is about to ring! (" + Mathf.CeilToInt(secondsToRing) + ")";
                    break;
                case PhoneState.Ringing:
                    phoneRect.anchoredPosition = phoneRestPos + new Vector2(Mathf.Sin(Time.time * 80f) * 10f, Mathf.Cos(Time.time * 70f) * 6f);
                    if (phoneText != null) phoneText.text = "RINGING!";
                    break;
            }
        }

        public void ShowOverlay(string title, string body)
        {
            if (overlay == null) return;
            overlay.SetActive(true);
            if (overlayTitle != null) overlayTitle.text = title;
            if (overlayBody != null) overlayBody.text = body;
        }
    }
}
