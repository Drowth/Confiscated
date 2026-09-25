using System.Collections.Generic;
using UnityEngine;
namespace Confiscated
{
    /// <summary>
    /// The school shows its other face while the caretaker hunts. Pupils and the dinner lady switch to their grinning artwork for as long
    /// as he is chasing or searching (the tape break-up hides the change), and for good once all five belongings are recovered.
    /// Mr Reed switches once he joins the hunt and stays that way. Only the artwork changes: nobody moves, turns or gains behaviour.
    /// </summary>
    public sealed class HuntFaces : MonoBehaviour
    {
        static readonly string[] CrowdArt={"T_Student_Seated_Views","T_Student_Girl_Seated_Views","T_DinnerLady"};
        const string TeacherArt="T_Mr_Reed";
        static readonly int BaseMap=Shader.PropertyToID("_BaseMap");
        sealed class Face{public Renderer renderer;public Texture normal,hunt;public bool teacher;}
        readonly List<Face> faces=new List<Face>();MaterialPropertyBlock block;HuntVhsEffect tape;
        public bool CrowdTurned {get;private set;}
        public bool TeacherTurned {get;private set;}
        public int CrowdCount {get;private set;}
        public int TeacherCount {get;private set;}
        public bool Showing(Renderer renderer,bool hunt)
        {
            renderer.GetPropertyBlock(block);var shown=block.GetTexture(BaseMap);
            foreach(var face in faces)if(face.renderer==renderer)
            {
                if(face.teacher&&renderer.GetComponentInParent<ReedDirectionalArt>()?.IsRearTexture(shown)==true)
                    return hunt==TeacherTurned;
                if(face.teacher&&renderer.GetComponentInParent<ReedDirectionalArt>()?.IsFrontWalkTexture(shown,hunt)==true)
                    return true;
                return hunt?shown==face.hunt:shown==null||shown==face.normal;
            }
            return false;
        }
        public IEnumerable<Renderer> Renderers(bool teacher){foreach(var face in faces)if(face.teacher==teacher)yield return face.renderer;}
        void Start()
        {
            block=new MaterialPropertyBlock();tape=GetComponent<HuntVhsEffect>();
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var art=renderer.sharedMaterial!=null&&renderer.sharedMaterial.HasProperty(BaseMap)?renderer.sharedMaterial.GetTexture(BaseMap):null;if(art==null)continue;
                bool teacher=art.name==TeacherArt;if(!teacher&&System.Array.IndexOf(CrowdArt,art.name)<0)continue;
                var hunt=Resources.Load<Texture2D>("Art/Hunt/"+art.name+"_Hunt");if(hunt==null)continue;
                faces.Add(new Face{renderer=renderer,normal=art,hunt=hunt,teacher=teacher});
                if(teacher)TeacherCount++;else CrowdCount++;
            }
        }
        void Update()
        {
            var run=SchoolRunController.Instance;var game=GameManager.Instance;if(run==null||game==null)return;
            bool playing=game.IsPlaying;float level=tape!=null?tape.Level:0;
            // Turns as the picture breaks up and turns back while it is still unsettled, so the change is never seen cleanly.
            bool crowd=playing&&(run.RoundStarted&&run.Count>=5||(CrowdTurned?level>.35f:level>.5f));
            bool teacher=playing&&run.RoundStarted&&run.Count>=3&&run.secondStaff!=null&&run.secondStaff.enabled;
            if(crowd!=CrowdTurned){CrowdTurned=crowd;Apply(false,crowd);}
            if(teacher!=TeacherTurned){TeacherTurned=teacher;Apply(true,teacher);}
        }
        void Apply(bool teacher,bool hunt)
        {
            foreach(var face in faces)
            {
                if(face.teacher!=teacher||face.renderer==null)continue;
                face.renderer.GetPropertyBlock(block);block.SetTexture(BaseMap,hunt?face.hunt:face.normal);face.renderer.SetPropertyBlock(block);
            }
        }
    }
}
