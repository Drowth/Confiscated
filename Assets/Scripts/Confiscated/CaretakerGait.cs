using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    [DefaultExecutionOrder(-50), DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class CaretakerGait : MonoBehaviour
    {
        public AudioClip leftStep, rightStep;
        public Texture2D walkSheet;
        public Renderer cutout;
        [Min(.2f)] public float metresPerStep=.7f;
        [Range(0,1)] public float volume=.65f;
        public int StepCount { get; private set; }
        public int CurrentFrame { get; private set; } = -1;
        public AudioSource Footsteps { get; private set; }
        NavMeshAgent agent; CutoutMotion motion; MaterialPropertyBlock properties;
        Texture idle; Vector3 previous; float travelled;
        Texture2D rearIdle, rearWalk, rearWalkDown;
        bool previousRear, previousDown;
        Texture expressions; CaretakerAI ai; bool wasChasing; float alertUntil; int previousMood=-2;
        public bool Alert => Time.time<alertUntil;
        // One complete, consistently framed texture per chase phase and foot pose.
        // Even frames raise his left foot; odd frames raise his right foot.
        Texture2D[] proximityArt;
        int previousProximity=-2;
        public int ProximityStage {get;private set;}=-1;
        public static int StageAtDistance(float metres)=>metres<=2.4384f?2:metres<=4.572f?1:metres<=6.096f?0:-1;
        public int Mood => Alert?0:SchoolRunController.Instance!=null&&SchoolRunController.Instance.Count>=4?2:(ai!=null&&ai.Current==CaretakerAI.State.Chase)||(SchoolRunController.Instance!=null&&SchoolRunController.Instance.Count>=2)?1:-1;
        // Measured transparent gaps in the 1024 x 1536 source, not equal thirds:
        // silhouettes occupy rows 10..514, 520..1024 and 1030..1531 (top origin).
        public static Vector4 ExpressionUV(int mood,int frame)
        {
            int top=mood==0?0:mood==1?518:1028;
            int bottom=mood==0?518:mood==1?1028:1536;
            // All six expressions use the same visible coat width as idle. The old atlas
            // left extra side padding, which made him visibly shrink on an alert change.
            return new Vector4(.40f,(bottom-top)/1536f,frame==1?.515f:.09f,(1536-bottom)/1536f);
        }
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();motion=GetComponentInChildren<CutoutMotion>();
            ai=GetComponent<CaretakerAI>();expressions=Resources.Load<Texture2D>("Art/CaretakerExpressions");
            rearIdle=Resources.Load<Texture2D>("Art/CaretakerRearIdle");
            rearWalk=Resources.Load<Texture2D>("Art/CaretakerRearWalk");
            rearWalkDown=Resources.Load<Texture2D>("Art/CaretakerRearWalkDown");
            proximityArt=new[]{
                Resources.Load<Texture2D>("Art/CaretakerPhase2_LeftUp"),
                Resources.Load<Texture2D>("Art/CaretakerPhase2_RightUp"),
                Resources.Load<Texture2D>("Art/CaretakerPhase3_LeftUp"),
                Resources.Load<Texture2D>("Art/CaretakerPhase3_RightUp"),
                Resources.Load<Texture2D>("Art/CaretakerPhase4_LeftUp"),
                Resources.Load<Texture2D>("Art/CaretakerPhase4_RightUp")
            };
            if(cutout!=null)idle=cutout.sharedMaterial.GetTexture("_BaseMap");
            properties=new MaterialPropertyBlock();previous=transform.position;
            var emitter=new GameObject("Caretaker footfalls");emitter.transform.SetParent(transform,false);emitter.transform.localPosition=Vector3.up*.1f;
            Footsteps=emitter.AddComponent<AudioSource>();Footsteps.playOnAwake=false;Footsteps.spatialBlend=1;
            Footsteps.rolloffMode=AudioRolloffMode.Linear;Footsteps.minDistance=2;Footsteps.maxDistance=28;Footsteps.dopplerLevel=0;
            if(motion!=null)motion.externalGait=true;
        }
        void LateUpdate()
        {
            bool chasing=ai!=null&&ai.Current==CaretakerAI.State.Chase;
            var player=SchoolRunController.Instance?.period?.Player;
            ProximityStage=chasing&&player!=null?StageAtDistance(Vector3.Distance(transform.position,player.transform.position)):-1;
            if(chasing&&!wasChasing)alertUntil=Time.time+1;wasChasing=chasing;
            Vector3 delta=transform.position-previous;previous=transform.position;delta.y=0;
            bool moving=agent.enabled&&agent.isOnNavMesh&&!agent.isStopped&&delta.magnitude>.0001f;
            Advance(moving?delta.magnitude:0,Time.deltaTime);
            SetFrame(CurrentFrame);
        }
        // Distance, rather than a timer or target speed, keeps accelerating feet and sound together.
        public void Advance(float distance,float seconds)
        {
            if(seconds<=0)return;
            float speed=distance/seconds;
            if(speed>12||speed<.06f)
            {
                travelled=0;SetFrame(-1);
                if(motion!=null){motion.gaitSpeed=0;motion.gaitPhase=0;}
                return;
            }
            float old=travelled;travelled+=distance;
            int before=Mathf.FloorToInt(old/metresPerStep),after=Mathf.FloorToInt(travelled/metresPerStep);
            if(after>before)
            {
                int foot=after%2;var clip=foot==0?leftStep:rightStep;
                if(clip!=null)Footsteps.PlayOneShot(clip,volume);
                StepCount++;
            }
            SetFrame(after%2);
            if(motion!=null){motion.gaitSpeed=speed;motion.gaitPhase=travelled/metresPerStep*Mathf.PI;}
        }
        void SetFrame(int frame)
        {
            int mood=Mood;
            var viewer=Camera.main;
            // CaretakerAI uses this same forward vector for his sight cone.
            bool rear=!ComicDialogue.IsAddressingPlayer(transform)&&viewer!=null&&Vector3.Dot(transform.forward,viewer.transform.position-transform.position)<0f;
            bool down=frame>=0&&travelled/metresPerStep%1f>.5f;
            if(cutout==null||CurrentFrame==frame&&previousMood==mood&&previousProximity==ProximityStage&&previousRear==rear&&previousDown==down)return;
            previousRear=rear;
            previousDown=down;
            previousProximity=ProximityStage;
            previousMood=mood;
            CurrentFrame=frame;cutout.GetPropertyBlock(properties);
            properties.SetTexture("_BaseMap",frame<0?idle:walkSheet);
            // The two generated steps are paired and alternate feet. Normalize their
            // wider sheet framing without changing the caretaker's world-space height.
            properties.SetFloat("_PoseWidth",frame>=0&&mood<0&&ProximityStage<0?.87f:1f);
            properties.SetFloat("_ChaseTorsoWidth",1);
            properties.SetVector("_BaseMap_ST",frame<0?new Vector4(1,1,0,0):new Vector4(.5f,1,frame==0?.016f:.479f,0));
            if(expressions!=null&&mood>=0)
            {
                properties.SetTexture("_BaseMap",expressions);
                properties.SetVector("_BaseMap_ST",ExpressionUV(mood,frame));
            }
            int chaseFrame=ProximityStage>=0?ProximityStage*2+(frame==1?1:0):-1;
            if(chaseFrame>=0&&proximityArt[chaseFrame]!=null)
            {
                properties.SetTexture("_BaseMap",proximityArt[chaseFrame]);
                properties.SetVector("_BaseMap_ST",new Vector4(1,1,0,0));
                if(ProximityStage==2)properties.SetFloat("_PoseWidth",1.10f);
            }
            if(rear&&(frame<0?rearIdle!=null:rearWalk!=null))
            {
                properties.SetTexture("_BaseMap",frame<0?rearIdle:down&&rearWalkDown!=null?rearWalkDown:rearWalk);
                properties.SetVector("_BaseMap_ST",frame<0?new Vector4(1,1,0,0):new Vector4(.5f,1,frame==0?0:.5f,0));
                properties.SetFloat("_PoseWidth",1f);
            }
            // Eye positions are in the sampled texture's UVs, after the walking/expression atlas crop.
            // All six proximity drawings share the idle head alignment. Other characters have zero radii.
            Vector4 left=new(.482f,.885f,.011f,.009f),right=new(.522f,.885f,.011f,.009f);
            if(chaseFrame<0||proximityArt[chaseFrame]==null)
            {
                if(expressions!=null&&mood>=0)
                {
                    float y=mood==0?.9596f:mood==1?.6257f:.2923f;
                    float x=frame==1?.423f:0;
                    left=new Vector4(.2783f+x,y,.0068f,.0058f);right=new Vector4(.3018f+x,y,.0068f,.0058f);
                }
                else if(frame>=0)
                {
                    left=new Vector4(frame==1?.712f:.266f,.894f,.0075f,.010f);
                    right=new Vector4(frame==1?.735f:.293f,.894f,.0075f,.010f);
                }
            }
            if(rear)left=right=Vector4.zero;
            properties.SetVector("_EyeGlowLeft",left);properties.SetVector("_EyeGlowRight",right);
            cutout.SetPropertyBlock(properties);
        }
        void OnDisable(){if(Footsteps!=null)Footsteps.Stop();SetFrame(-1);if(motion!=null)motion.externalGait=false;}
        void OnDestroy(){if(Footsteps!=null)Destroy(Footsteps.gameObject);}
    }
}
