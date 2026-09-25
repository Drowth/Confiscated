using UnityEngine;
namespace Confiscated
{
    /// <summary>Moves only the display; the pickup's interaction target stays still.</summary>
    public sealed class CollectibleMotion : MonoBehaviour
    {
        public Rigidbody physicsBody;
        public bool spin=true;
        public float lift=.07f,bob=.04f;
        Vector3 rest;Quaternion rotation;float elapsed;bool ready;
        void Awake(){rest=transform.localPosition;rotation=transform.localRotation;ready=true;}
        void OnEnable(){elapsed=0;}
        void OnDisable(){if(ready){transform.localPosition=rest;transform.localRotation=rotation;}}
        void Update()
        {
            bool moving=physicsBody!=null&&(physicsBody.isKinematic||physicsBody.linearVelocity.sqrMagnitude>.04f||physicsBody.angularVelocity.sqrMagnitude>.25f);
            if(moving){transform.localPosition=rest;transform.localRotation=rotation;elapsed=0;return;}
            elapsed+=Time.deltaTime;
            transform.localPosition=rest+Vector3.up*(lift+bob*Mathf.Sin(elapsed*2.4f));
            transform.localRotation=spin?Quaternion.AngleAxis(elapsed*48,Vector3.up)*rotation:rotation;
        }
        public static void Attach(GameObject visual){if(visual!=null&&visual.GetComponent<CollectibleMotion>()==null)visual.AddComponent<CollectibleMotion>();}
        public static void AttachMeshes(Transform owner,Rigidbody body=null)
        {
            if(owner.Find("Collectible display")!=null)return;
            var meshes=owner.GetComponentsInChildren<MeshRenderer>();
            var pivot=new GameObject("Collectible display").transform;pivot.SetParent(owner,false);
            foreach(var source in meshes)
            {
                var filter=source.GetComponent<MeshFilter>();if(filter==null)continue;
                var copy=new GameObject(source.name+" display",typeof(MeshFilter),typeof(MeshRenderer));copy.transform.SetParent(source.transform,false);
                copy.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=copy.GetComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;
                copy.transform.SetParent(pivot,true);source.enabled=false;
            }
            pivot.gameObject.AddComponent<CollectibleMotion>().physicsBody=body;
        }
    }
}
