using System.Collections.Generic;
using UnityEngine;

namespace Confiscated
{
    /// <summary>The scare mesh attached to the animated roaming figure, veiled in smoke.</summary>
    public sealed class LibraryShadowWorldHead : MonoBehaviour
    {
        GameObject head;Renderer face;ParticleSystem veil;Mesh bodyCopy;MaterialPropertyBlock block;
        public bool Ready=>head!=null;
        public void Build(LibraryShadow owner)
        {
            var prefab=Resources.Load<GameObject>(LibraryShadowScareHead.Resource);
            var body=owner.GetComponentInChildren<SkinnedMeshRenderer>();
            if(prefab==null||body==null)return;
            int bone=System.Array.FindIndex(body.bones,t=>t.name=="Head");if(bone<0)return;
            head=Instantiate(prefab,body.bones[bone]);head.name="Roaming shadow head";
            head.transform.localPosition=new Vector3(0,.045f,-.015f);
            head.transform.localRotation=Quaternion.Euler(0,180,0);head.transform.localScale=Vector3.one*.55f;
            // Remove only the old head's triangles from a private runtime copy of the body.
            var original=body.sharedMesh;
            if(original.isReadable)
            {
                bodyCopy=Instantiate(original);var weights=original.boneWeights;
                bool IsHead(int v){var w=weights[v];return (w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0)>.5f;}
                for(int sub=0;sub<original.subMeshCount;sub++)
                {var src=original.GetTriangles(sub);var keep=new List<int>();for(int i=0;i<src.Length;i+=3)if(!IsHead(src[i])&&!IsHead(src[i+1])&&!IsHead(src[i+2])){keep.Add(src[i]);keep.Add(src[i+1]);keep.Add(src[i+2]);}bodyCopy.SetTriangles(keep,sub);}
                body.sharedMesh=bodyCopy;
            }
            foreach(var old in owner.eyes)if(old!=null)old.gameObject.SetActive(false);
            var eyes=new List<Renderer>();foreach(var r in head.GetComponentsInChildren<Renderer>())
            {if(r.name=="White slit eye"){eyes.Add(r);r.transform.localPosition+=Vector3.forward*.045f;r.transform.localScale=new Vector3(.025f,.075f,.025f);}else if(!(r is ParticleSystemRenderer))face=r;}
            owner.eyes=eyes.ToArray();block=new MaterialPropertyBlock();
            veil=head.GetComponentInChildren<ParticleSystem>();
            if(veil!=null)
            {
                veil.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                veil.transform.localPosition=new Vector3(0,0,-.18f);
                var main=veil.main;main.startSize=new ParticleSystem.MinMaxCurve(.4f,.8f);main.startLifetime=1.25f;main.maxParticles=180;main.startColor=new Color(.045f,.055f,.085f,.85f);
                var shape=veil.shape;shape.radius=.45f;var emission=veil.emission;emission.rateOverTime=90;veil.Play();
            }
        }
        public void SetVisible(float visibility,bool hunting)
        {
            if(head==null)return;
            head.SetActive(visibility>.01f);
            if(face!=null){face.GetPropertyBlock(block);block.SetFloat("_Visibility",visibility);block.SetFloat("_InkBrightness",hunting?.65f:.26f);face.SetPropertyBlock(block);}
        }
        void OnDestroy(){if(bodyCopy!=null)Destroy(bodyCopy);}
    }
}
