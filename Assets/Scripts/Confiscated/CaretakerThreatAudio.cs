using UnityEngine;
namespace Confiscated
{
    [DisallowMultipleComponent]
    public sealed class CaretakerThreatAudio : MonoBehaviour
    {
        public AudioClip whistleOne,whistleTwo,heartbeat;
        public float whistleRange=45f;
        public AudioSource Whistle {get;private set;}
        CaretakerAI ai;float nextWhistle;bool wasPatrolling,whistling;int nextClip;
        void Awake()
        {
            ai=GetComponent<CaretakerAI>();
            var mouth=new GameObject("Patrol whistle");mouth.transform.SetParent(transform,false);mouth.transform.localPosition=Vector3.up*1.75f;
            Whistle=SchoolAudio.Create(mouth,SchoolAudio.Channel.Effects,true);Whistle.playOnAwake=false;Whistle.spatialBlend=1;Whistle.dopplerLevel=0;
            Whistle.rolloffMode=AudioRolloffMode.Linear;Whistle.minDistance=3;Whistle.maxDistance=whistleRange;Whistle.volume=.55f;
            // The heartbeat is the player's, driven by whichever threat is nearest (not just him, not just mid-chase).
            ThreatHeartbeat.Ensure(heartbeat);
        }
        void Update()
        {
            var gm=GameManager.Instance;
            bool active=gm!=null&&gm.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0&&SchoolRunController.Instance!=null;
            Tick(ai.Current,active,Time.time);
        }
        public void Tick(CaretakerAI.State state,bool active,float now)
        {
            bool patrol=active&&state==CaretakerAI.State.Patrol;
            if(!patrol){Whistle.Stop();whistling=false;wasPatrolling=false;}
            else
            {
                if(!wasPatrolling){nextWhistle=now+Random.Range(12f,20f);wasPatrolling=true;}
                if(whistling&&!Whistle.isPlaying){whistling=false;nextWhistle=now+Random.Range(12f,20f);}
                if(!whistling&&now>=nextWhistle)
                {
                    Whistle.clip=nextClip==0?whistleOne:whistleTwo;nextClip=1-nextClip;
                    if(Whistle.clip!=null){Whistle.Play();whistling=true;}
                    else nextWhistle=now+20;
                }
            }
        }
        void OnDisable(){if(Whistle!=null)Whistle.Stop();wasPatrolling=whistling=false;}
        void OnDestroy(){if(Whistle!=null)Destroy(Whistle.gameObject);}
    }
}
