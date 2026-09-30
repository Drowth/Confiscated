using UnityEngine;

namespace Confiscated
{
    /// <summary>Fits the illustrated handset to the existing phone props without changing their interactions.</summary>
    public static class PhoneModelVisuals
    {
        const string ModelPath="Art/SmithOldPhone";
        const string ColorPath="Art/SmithOldPhoneColor";
        const string DisplayName="Illustrated old mobile phone";
        static GameObject model;
        static Material material;

        static GameObject Create(Transform parent,Vector3 position,Quaternion rotation,float length)
        {
            if(model==null)model=Resources.Load<GameObject>(ModelPath);
            if(model==null)return null;
            var display=Object.Instantiate(model,parent,false);display.name=DisplayName;
            display.transform.localPosition=position;display.transform.localRotation=rotation;
            display.transform.localScale=Vector3.one*length;
            if(material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                material=new Material(shader){name="Smith old phone pencil matte"};
                var color=Resources.Load<Texture2D>(ColorPath);
                if(color!=null){material.SetTexture("_BaseMap",color);material.mainTexture=color;}
                material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",0);
            }
            foreach(var renderer in display.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
            return display;
        }

        public static void InstallDesk(GameObject prop)
        {
            if(prop==null||prop.transform.Find(DisplayName)!=null)return;
            var body=prop.transform.Find("Phone case");var screen=prop.transform.Find("Phone screen");
            Vector3 centre=body!=null&&body.GetComponent<Renderer>()!=null?
                prop.transform.InverseTransformPoint(body.GetComponent<Renderer>().bounds.center):Vector3.zero;
            Quaternion turn=body!=null?body.localRotation:Quaternion.identity;
            var display=Create(prop.transform,centre,turn*Quaternion.Euler(-90,0,0),.15f);if(display==null)return;
            // The old flat case is centred on the desktop; the handset is thicker, so lift it to rest on top.
            if(body!=null&&body.GetComponent<Renderer>()!=null)
            {
                float bottom=float.MaxValue;foreach(var r in display.GetComponentsInChildren<Renderer>())bottom=Mathf.Min(bottom,r.bounds.min.y);
                display.transform.position+=Vector3.up*(body.GetComponent<Renderer>().bounds.center.y-bottom+.001f);
            }
            if(body!=null&&body.GetComponent<Renderer>()!=null)body.GetComponent<Renderer>().enabled=false;
            if(screen!=null&&screen.GetComponent<Renderer>()!=null)screen.GetComponent<Renderer>().enabled=false;
        }

        public static void InstallPickup(GameObject visual)
        {
            if(visual==null||visual.transform.Find(DisplayName)!=null)return;
            if(model==null)model=Resources.Load<GameObject>(ModelPath);
            if(model==null)return;
            visual.transform.localScale=Vector3.one;
            var collider=visual.GetComponent<BoxCollider>();
            if(collider!=null){collider.size=new Vector3(.065f,.026f,.15f);collider.center=Vector3.zero;}
            var renderer=visual.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=false;
            Create(visual.transform,Vector3.zero,Quaternion.Euler(-90,0,0),.15f);
        }
    }
}
