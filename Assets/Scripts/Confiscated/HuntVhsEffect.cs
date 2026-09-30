using UnityEngine;
using UnityEngine.AI;
namespace Confiscated
{
    /// <summary>
    /// Pursuit distortion, only while the caretaker or Mr Reed is actively chasing. The last walk to the exit washes the tape red.
    /// </summary>
    public sealed class HuntVhsEffect : MonoBehaviour
    {
        [Range(0,1)] public float chase=.28f;
        public float Level {get;private set;}
        /// <summary>A one-off slam of the picture (the library shadow's jumpscare): takes hold at once, then fades on its own.</summary>
        public static float Burst;
        public float ExitRed {get;private set;}
        public float ExitDistance {get;private set;}=float.PositiveInfinity;
        static readonly int Intensity=Shader.PropertyToID("_VhsIntensity"),Clock=Shader.PropertyToID("_VhsTime"),Red=Shader.PropertyToID("_VhsExitRed");
        ChaseCamera feel;
        Transform exit;
        NavMeshPath exitPath;
        float nextPath;
        void Awake(){exitPath=new NavMeshPath();}
        public static float RedForDistance(float distance)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(65,3,distance));
        static bool Chasing(CaretakerAI staff)=>staff!=null&&staff.enabled&&staff.Current==CaretakerAI.State.Chase;
        public static bool ActivelyChased(SchoolRunController run)=>run!=null&&(Chasing(run.caretaker)||Chasing(run.secondStaff));
        void Update()
        {
            var run=SchoolRunController.Instance;var game=GameManager.Instance;
            bool live=run!=null&&run.caretaker!=null&&run.caretaker.enabled&&game!=null&&game.IsPlaying&&!ComicDialogue.IsActive;
            float target=live&&ActivelyChased(run)?chase:0;
            if(live&&run.ReadyToEscape&&Time.unscaledTime>=nextPath)
            {
                nextPath=Time.unscaledTime+.25f;
                if(exit==null)foreach(var door in FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None))if(door.mainExit){exit=door.transform;break;}
                ExitDistance=float.PositiveInfinity;
                if(exit!=null)
                {
                    // Route distance avoids turning the screen red through a nearby wall.
                    Vector3 approach=exit.position-exit.forward*1.3f;
                    if(NavMesh.SamplePosition(approach,out var end,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(run.period.Player.transform.position,end.position,NavMesh.AllAreas,exitPath)&&exitPath.status==NavMeshPathStatus.PathComplete)
                    {
                        ExitDistance=0;var corners=exitPath.corners;for(int i=1;i<corners.Length;i++)ExitDistance+=Vector3.Distance(corners[i-1],corners[i]);
                    }
                }
            }
            float redTarget=live&&run.ReadyToEscape?RedForDistance(ExitDistance):0;
            ExitRed=Mathf.MoveTowards(ExitRed,redTarget,Time.unscaledDeltaTime*.7f);
            // Snaps in when he turns on you, drains away as he gives up.
            Level=Mathf.MoveTowards(Level,target,Time.unscaledDeltaTime*(target>Level?4f:1.2f));
            if(Burst>0){Level=Mathf.Max(Level,Burst);Burst=Mathf.MoveTowards(Burst,0,Time.unscaledDeltaTime*2.5f);}
            if(feel==null&&run!=null&&run.period!=null&&run.period.Player!=null)feel=run.period.Player.GetComponent<ChaseCamera>();
            // Honours the player's existing camera-motion setting (F8): reduced halves it, off removes it.
            Shader.SetGlobalFloat(Intensity,live?Level*(feel!=null?feel.intensity:1):0);
            Shader.SetGlobalFloat(Red,live?ExitRed*(feel!=null?feel.intensity:1):0);
            Shader.SetGlobalFloat(Clock,Time.unscaledTime);
        }
        void OnDisable(){Shader.SetGlobalFloat(Intensity,0);Shader.SetGlobalFloat(Red,0);}
    }
}
