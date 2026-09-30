using UnityEngine;

namespace Confiscated
{
    /// <summary>One-way corridor-door release on the caretaker's workbench.</summary>
    public sealed class OfficeDoorRelease : Interactable
    {
        Renderer button;TextMesh label;Material casing,paint;
        GameObject unpressed,pressed;
        AudioSource click;
        public static void Install()
        {
            if(FindFirstObjectByType<OfficeDoorRelease>()!=null)return;
            var bench=GameObject.Find("Workbench");if(bench==null)return;
            var surface=bench.GetComponentInChildren<Renderer>();if(surface==null)return;
            var root=new GameObject("Office blue door release");root.transform.SetParent(bench.transform,false);
            root.transform.position=new Vector3(surface.bounds.center.x,surface.bounds.max.y+.17f,surface.bounds.center.z-.35f);
            root.transform.rotation=Quaternion.Euler(0,90,0);
            var release=root.AddComponent<OfficeDoorRelease>();release.Build();
        }
        void Build()
        {
            click=SchoolAudio.Create(gameObject);click.clip=Resources.Load<AudioClip>("Audio/Doors/OfficeReleaseClick");
            click.spatialBlend=1;click.rolloffMode=AudioRolloffMode.Linear;click.minDistance=1;click.maxDistance=8;click.dopplerLevel=0;click.volume=.8f;
            var asset=Resources.Load<GameObject>("Art/OfficeDoorRelease3D");
            if(asset!=null)
            {
                var model=Instantiate(asset,transform);model.name="AssetHub door release";
                unpressed=model.transform.Find("Unpressed")?.gameObject;pressed=model.transform.Find("Pressed")?.gameObject;
                if(pressed!=null)pressed.SetActive(false);
                var target=gameObject.AddComponent<BoxCollider>();target.size=new Vector3(.3f,.34f,.2f);
                var anchor=model.transform.Find("Label anchor");
                var writing=new GameObject("Blue doors label");writing.transform.SetParent(anchor!=null?anchor:transform,false);
                if(anchor==null)writing.transform.localPosition=new Vector3(0,.07f,.1f);
                writing.transform.localRotation=Quaternion.Euler(0,180,0);
                label=writing.AddComponent<TextMesh>();label.text="BLUE DOORS\nPRESS TO OPEN";label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.005f;label.color=new Color(.035f,.055f,.09f);
                return;
            }
            Material pencil=null;foreach(var m in Resources.FindObjectsOfTypeAll<Material>())if(m.name=="M_Door_Corridor"){pencil=m;break;}
            casing=pencil!=null?new Material(pencil):new Material(Shader.Find("Universal Render Pipeline/Lit"));casing.SetColor("_BaseColor",new Color(.35f,.43f,.48f));
            paint=new Material(casing);paint.SetColor("_BaseColor",new Color(.7f,.13f,.08f));
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="Door control box";box.transform.SetParent(transform,false);box.transform.localScale=new Vector3(.32f,.27f,.12f);box.GetComponent<Renderer>().sharedMaterial=casing;
            var cap=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cap.name="Press to release";cap.transform.SetParent(transform,false);cap.transform.localPosition=new Vector3(0,-.035f,.075f);cap.transform.localRotation=Quaternion.Euler(90,0,0);cap.transform.localScale=new Vector3(.105f,.018f,.105f);button=cap.GetComponent<Renderer>();button.sharedMaterial=paint;
            var text=new GameObject("Blue doors label");text.transform.SetParent(transform,false);text.transform.localPosition=new Vector3(0,.072f,.063f);text.transform.localRotation=Quaternion.Euler(0,180,0);
            label=text.AddComponent<TextMesh>();label.text="BLUE DOORS\nPRESS TO OPEN";label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.006f;label.color=new Color(.94f,.9f,.75f);
        }
        public override string GetPrompt(PlayerInteractor player)=>SchoolRunController.Instance.CorridorDoorsReleased?"Blue corridor doors released.":"F: open all blue corridor doors";
        public override bool CanInteract(PlayerInteractor player)=>SchoolRunController.Instance!=null&&!SchoolRunController.Instance.CorridorDoorsReleased;
        public override void Interact(PlayerInteractor player)
        {
            if(!CanInteract(player))return;
            SchoolRunController.Instance.ReleaseCorridorDoors();
            if(click!=null&&click.clip!=null)click.Play();
            if(paint!=null)paint.SetColor("_BaseColor",new Color(.18f,.65f,.32f));label.text="BLUE DOORS\nOPEN";
            if(button!=null)button.transform.localPosition-=Vector3.forward*.012f;
            if(unpressed!=null)unpressed.SetActive(false);if(pressed!=null)pressed.SetActive(true);
            HudController.Instance?.SetStatus("Blue corridor doors released. Bolt cutters are in Equipment.",5);
        }
        void OnDestroy(){if(casing!=null)Destroy(casing);if(paint!=null)Destroy(paint);}
    }
}
