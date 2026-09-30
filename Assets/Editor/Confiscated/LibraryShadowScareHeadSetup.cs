using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Only builds the isolated scare-head prefab and its material; never touches the school scene.</summary>
    public static class LibraryShadowScareHeadSetup
    {
        const string Folder="Assets/Art/Models/Library/ScareHead/";
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before importing the head");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"ShadowScare.mat");
            if(material==null){material=new Material(Shader.Find("Confiscated/Shadow Scare Charcoal"));AssetDatabase.CreateAsset(material,Folder+"ShadowScare.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"ShadowJumpscare_Color.png"));
            var root=new GameObject("LibraryShadowScareHead3D");
            try
            {
                var holder=new GameObject("Head mesh").transform;holder.SetParent(root.transform,false);holder.localRotation=Quaternion.Euler(0,90,0);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ShadowJumpscare.fbx"),holder);
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;bounds.Encapsulate(r.bounds);}
                holder.localScale=Vector3.one/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                holder.localPosition=-bounds.center;
                var eyeMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"ShadowEyes.mat");
                if(eyeMaterial==null){eyeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(eyeMaterial,Folder+"ShadowEyes.mat");}
                eyeMaterial.SetColor("_BaseColor",new Color(1,1,.95f,1));EditorUtility.SetDirty(eyeMaterial);
                var filter=model.GetComponentInChildren<MeshFilter>();var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;Physics.SyncTransforms();
                foreach(float x in new[]{-.11f,.11f})
                {
                    RaycastHit hit;var ray=new Ray(new Vector3(x,.14f,2),Vector3.back);
                    if(!collider.Raycast(ray,out hit,4))throw new System.InvalidOperationException("Cannot locate eye surface");
                    var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.name="White slit eye";Object.DestroyImmediate(eye.GetComponent<Collider>());eye.transform.SetParent(root.transform,false);
                    eye.transform.localPosition=hit.point+Vector3.forward*.008f;eye.transform.localScale=new Vector3(.016f,.065f,.018f);eye.GetComponent<Renderer>().sharedMaterial=eyeMaterial;
                }
                Object.DestroyImmediate(collider);
                var puff=new GameObject("Trailing smoke");puff.transform.SetParent(root.transform,false);puff.transform.localPosition=new Vector3(0,.05f,-.12f);
                var smoke=puff.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=smoke.main;main.loop=true;main.prewarm=true;main.duration=1;main.startLifetime=.7f;main.startSpeed=.07f;main.startSize=new ParticleSystem.MinMaxCurve(.12f,.25f);main.maxParticles=90;
                main.startColor=new Color(.12f,.15f,.22f,.4f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
                var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.31f;
                var emission=smoke.emission;emission.rateOverTime=55;
                var fade=smoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.25f),new GradientAlphaKey(0,1)});fade.color=gradient;
                puff.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Library_ShadowSmoke.mat");
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Art/LibraryShadowScareHead3D.prefab");
            }
            finally{Object.DestroyImmediate(root);}
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        }
    }
}
