using UnityEngine;
using UnityEngine.AI;

namespace Confiscated
{
    /// <summary>Keys are audible at the caretaker's waist only while his navigation agent moves.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class CaretakerWalkAudio : MonoBehaviour
    {
        public AudioClip keysClip;
        [Range(0,1)] public float volume=.4f;
        public float audibleDistance=18f;
        NavMeshAgent agent;
        AudioSource source;
        public AudioSource Source=>source;

        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();
            var emitter=new GameObject("Walking keys sound");
            emitter.transform.SetParent(transform,false);
            emitter.transform.localPosition=new Vector3(-.3f,1.05f,0);
            source=emitter.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=true;source.spatialBlend=1;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1;
            source.maxDistance=audibleDistance;source.dopplerLevel=0;source.volume=0;
            source.clip=keysClip;
        }

        void LateUpdate()
        {
            bool moving=keysClip!=null&&agent.enabled&&agent.isOnNavMesh&&!agent.isStopped
                &&new Vector2(agent.velocity.x,agent.velocity.z).sqrMagnitude>.0064f;
            if(moving&&!source.isPlaying){source.clip=keysClip;source.Play();}
            source.volume=Mathf.MoveTowards(source.volume,moving?volume:0,Time.deltaTime*volume/.12f);
            if(!moving&&source.volume<=0&&source.isPlaying)source.Stop();
        }

        void OnDisable(){if(source!=null){source.Stop();source.volume=0;}}
        void OnDestroy(){if(source!=null)Destroy(source.gameObject);}
    }
}
