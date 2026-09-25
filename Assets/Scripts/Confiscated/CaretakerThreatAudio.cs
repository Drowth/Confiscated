using UnityEngine;
namespace Confiscated
{
    [DisallowMultipleComponent]
    public sealed class CaretakerThreatAudio : MonoBehaviour
    {
        public AudioClip whistleOne,whistleTwo,heartbeat;
        public float heartbeatRange=22f,nearestDistance=2f,whistleRange=45f;
        public AudioSource Whistle {get;private set;}
        public AudioSource Heartbeat {get;private set;}
        CaretakerAI ai;float nextWhistle;bool wasPatrolling,whistling;int nextClip;
        void Awake()
        {
            ai=GetComponent<CaretakerAI>();
            var mouth=new GameObject("Patrol whistle");mouth.transform.SetParent(transform,false);mouth.transform.localPosition=Vector3.up*1.75f;
            Whistle=mouth.AddComponent<AudioSource>();Whistle.playOnAwake=false;Whistle.spatialBlend=1;Whistle.dopplerLevel=0;
            Whistle.rolloffMode=AudioRolloffMode.Linear;Whistle.minDistance=3;Whistle.maxDistance=whistleRange;Whistle.volume=.55f;
            var pulse=new GameObject("Player chase heartbeat");pulse.transform.SetParent(transform,false);
            Heartbeat=pulse.AddComponent<AudioSource>();Heartbeat.playOnAwake=false;Heartbeat.spatialBlend=0;Heartbeat.loop=true;Heartbeat.clip=heartbeat;Heartbeat.volume=0;
        }
        void Update()
        {
            var gm=GameManager.Instance;var run=SchoolRunController.Instance;
            bool active=gm!=null&&gm.IsPlaying&&!ComicDialogue.IsActive&&Time.timeScale>0&&run!=null;
            float distance=run!=null?Vector3.Distance(transform.position,run.period.Player.transform.position):float.MaxValue;
            Tick(ai.Current,distance,active,Time.time,Time.deltaTime);
        }
        public void Tick(CaretakerAI.State state,float distance,bool active,float now,float dt)
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
            bool chase=active&&state==CaretakerAI.State.Chase&&distance<heartbeatRange;
            if(!chase){Heartbeat.Stop();Heartbeat.volume=0;Heartbeat.pitch=1;return;}
            float closeness=Mathf.InverseLerp(heartbeatRange,nearestDistance,distance);
            float pitch=Mathf.Lerp(1f,1.9f,closeness),volume=Mathf.Lerp(.08f,.65f,closeness);
            Heartbeat.pitch=Mathf.MoveTowards(Heartbeat.pitch,pitch,dt*2);
            Heartbeat.volume=Mathf.MoveTowards(Heartbeat.volume,volume,dt*1.5f);
            if(heartbeat!=null&&!Heartbeat.isPlaying)Heartbeat.Play();
        }
        void OnDisable(){if(Whistle!=null)Whistle.Stop();if(Heartbeat!=null)Heartbeat.Stop();wasPatrolling=whistling=false;}
        void OnDestroy(){if(Whistle!=null)Destroy(Whistle.gameObject);if(Heartbeat!=null)Destroy(Heartbeat.gameObject);}
    }
}
