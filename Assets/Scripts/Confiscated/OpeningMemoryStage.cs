using System;
using UnityEngine;

namespace Confiscated
{
    /// <summary>A temporary, isolated 3D set for the moving shots in the diary opening.</summary>
    internal sealed class OpeningMemoryStage
    {
        const int Layer = 31;
        static readonly Vector2[] PanelOffsets =
        {
            new Vector2(0,.5f), new Vector2(.5f,.5f),
            new Vector2(0,0), new Vector2(.5f,0)
        };

        GameObject root;
        Camera camera;
        Material paper,backing;
        Transform panel;

        public static OpeningMemoryStage Create(Texture2D art)
        {
            var stage=new OpeningMemoryStage();
            try { stage.Build(art);return stage; }
            catch(Exception error)
            {
                Debug.LogWarning("Opening memory stage unavailable; using the diary page. " + error.Message);
                stage.Dispose();return null;
            }
        }

        void Build(Texture2D art)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)throw new InvalidOperationException("URP Unlit shader missing.");
            root=new GameObject("Opening memory set");root.transform.position=new Vector3(0,-100,0);

            var cameraObject=new GameObject("Memory camera",typeof(Camera));
            cameraObject.transform.SetParent(root.transform,false);
            camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.02f,.033f,.043f);
            camera.cullingMask=1<<Layer;camera.depth=100;
            camera.nearClipPlane=.03f;camera.farClipPlane=30f;
            camera.fieldOfView=48f;camera.allowHDR=false;

            var page=GameObject.CreatePrimitive(PrimitiveType.Quad);
            page.name="Moving illustrated memory";page.transform.SetParent(root.transform,false);
            page.transform.localPosition=new Vector3(0,0,1.5f);
            page.transform.localScale=new Vector3(8.8f,4.95f,1);
            panel=page.transform;page.layer=Layer;
            UnityEngine.Object.Destroy(page.GetComponent<Collider>());
            paper=new Material(shader){name="Opening memory paper"};
            paper.SetTexture("_BaseMap",art);
            paper.SetColor("_BaseColor",Color.white);
            paper.SetTextureScale("_BaseMap",new Vector2(.5f,.5f));
            page.GetComponent<Renderer>().sharedMaterial=paper;

            var edge=GameObject.CreatePrimitive(PrimitiveType.Quad);
            edge.name="Paper edge";edge.transform.SetParent(root.transform,false);
            edge.transform.localPosition=new Vector3(0,0,2f);
            edge.transform.localScale=new Vector3(9.05f,5.2f,1);
            edge.layer=Layer;UnityEngine.Object.Destroy(edge.GetComponent<Collider>());
            backing=new Material(shader){name="Memory ink edge"};
            backing.SetColor("_BaseColor",new Color(.035f,.052f,.07f));
            edge.GetComponent<Renderer>().sharedMaterial=backing;
            ShowBeat(0);Animate(0,0);
        }

        public void ShowBeat(int beat)
        {
            if(camera==null)return;
            camera.enabled=beat<4;
            if(beat<4)paper.SetTextureOffset("_BaseMap",PanelOffsets[beat]);
        }

        public void Animate(int beat,float progress)
        {
            if(camera==null||beat>=4)return;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress));
            float direction=beat%2==0?1f:-1f;
            camera.transform.localPosition=new Vector3(Mathf.Lerp(-.55f,.55f,t)*direction,
                Mathf.Lerp(beat==2 ? .2f : -.1f,beat==2 ? -.12f : .1f,t),Mathf.Lerp(-6.2f,-4.8f,t));
            camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0,0,.15f)));
            camera.fieldOfView=Mathf.Lerp(50f,42f,t);
            panel.localPosition=new Vector3(Mathf.Lerp(.16f,-.16f,t)*direction,0,1.5f);
            panel.localRotation=Quaternion.Euler(Mathf.Lerp(-1.6f,1.6f,t),
                Mathf.Lerp(-3.2f,3.2f,t)*direction,Mathf.Sin(t*Mathf.PI)*.5f);
        }

        public void Dispose()
        {
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(paper!=null)UnityEngine.Object.Destroy(paper);
            if(backing!=null)UnityEngine.Object.Destroy(backing);
            root=null;camera=null;paper=null;backing=null;
        }
    }
}
