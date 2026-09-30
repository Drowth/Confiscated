using UnityEngine;
using UnityEngine.Rendering;

namespace Confiscated
{
    /// <summary>Polishes only the hand-built Reflection Room doorway when the level loads.</summary>
    public static class ReflectionDoorVisuals
    {
        const string Art="Art/Isolation/";
        static readonly float[] Heights={.30f,.72f,1.17f,1.62f,2.04f};
        static readonly float[] Centres={5.27f,5.34f,5.25f,5.32f,5.28f};
        static readonly float[] Lengths={1.81f,1.96f,1.79f,1.97f,1.86f};
        static readonly float[] Widths={.20f,.15f,.22f,.17f,.18f};
        static readonly float[] Angles={-6f,7f,-4f,5f,-3f};

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallOnLoad()
        {
            var room=Object.FindFirstObjectByType<IsolationRoom>();
            if(room!=null)Install(room);
        }

        public static void Install(IsolationRoom room)
        {
            if(room==null||room.boards==null||room.lightUnderDoor==null)return;
            var root=room.boards.transform;
            for(int i=0;i<5;i++)
            {
                var plank=root.Find("Plank "+(i+1));
                if(plank==null)continue;
                plank.position=new Vector3(-14.79f+(i%2)*.007f,Heights[i],Centres[i]);
                plank.rotation=Quaternion.Euler(Angles[i],0,0);
                plank.localScale=new Vector3(.036f,Widths[i],Lengths[i]);
                SetBoard(plank,i+1,i%3);
            }
            var brace=root.Find("Diagonal brace");
            if(brace!=null)
            {
                brace.position=new Vector3(-14.82f,1.18f,5.29f);
                brace.rotation=Quaternion.Euler(37,0,0);
                brace.localScale=new Vector3(.034f,.11f,2.10f);
                SetBoard(brace,6,1);
            }
            var leak=room.lightUnderDoor.transform;
            foreach(Transform child in leak)
                if(child.name.StartsWith("Glow "))child.gameObject.SetActive(false);
            var spill=leak.Find("Soft amber floor spill");
            if(spill==null)
            {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name="Soft amber floor spill";quad.transform.SetParent(leak,false);
                Object.Destroy(quad.GetComponent<Collider>());spill=quad.transform;
            }
            spill.position=new Vector3(-14.48f,.012f,5.3f);
            spill.rotation=Quaternion.Euler(-90,0,0);
            spill.localScale=new Vector3(.72f,1.53f,1);
            var renderer=spill.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=Resources.Load<Material>(Art+"DoorSpill");
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var gap=leak.Find("Lit gap");
            if(gap!=null)
            {
                gap.position=new Vector3(-14.865f,.027f,5.3f);
                gap.localScale=new Vector3(1.47f,.022f,1);
            }
            var bounce=leak.Find("Threshold bounce light");
            if(bounce==null){bounce=new GameObject("Threshold bounce light").transform;bounce.SetParent(leak,false);}
            bounce.position=new Vector3(-14.72f,.10f,5.3f);
            var light=bounce.GetComponent<Light>();if(light==null)light=bounce.gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.color=new Color(1f,.73f,.43f);
            light.intensity=.08f;light.range=.9f;light.shadows=LightShadows.None;
        }

        static void SetBoard(Transform plank,int number,int shade)
        {
            var mesh=Resources.Load<Mesh>(Art+"Board"+number);
            if(mesh!=null)plank.GetComponent<MeshFilter>().sharedMesh=mesh;
            string material=shade==0?"TimberHoney":shade==1?"TimberDust":"TimberRusset";
            var mat=Resources.Load<Material>(Art+material);
            if(mat!=null)plank.GetComponent<MeshRenderer>().sharedMaterial=mat;
            var iron=Resources.Load<Material>(Art+"NailIron");int n=0;
            foreach(Transform child in plank)
            {
                if(child.name!="Nail")continue;
                child.localPosition=new Vector3(.63f,0,n==0?-.43f:n==1?.43f:0);
                child.localScale=new Vector3(.23f,.012f/plank.localScale.y,.012f/plank.localScale.z);
                if(iron!=null)child.GetComponent<MeshRenderer>().sharedMaterial=iron;
                n++;
            }
        }
    }
}
