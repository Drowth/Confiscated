using UnityEngine;
namespace Confiscated
{
    public sealed class ProgressPropFeedback : MonoBehaviour
    {
        public OfficeDoor door;public RunGate gate;public Transform padlock,boxLid;public Renderer exitPlate;public TextMesh exitText;public GameObject crossedOut;
        bool dropped,opened;float fall;Vector3 start;Quaternion rotation;
        void Start(){if(padlock!=null){start=padlock.localPosition;rotation=padlock.localRotation;}}
        void Update()
        {
            var run=SchoolRunController.Instance;if(run==null)return;
            if(boxLid!=null){opened|=run.Has(0);boxLid.localRotation=Quaternion.RotateTowards(boxLid.localRotation,Quaternion.Euler(opened?-110:-45,0,0),Time.deltaTime*160);}
            if(padlock!=null)
            {
                // The main entrance relocks if detention takes a belonging back, so its padlock is not latched.
                if(door!=null&&door.mainExit){bool ready=run.ReadyToEscape||door.Escaped;if(!ready&&dropped){fall=0;padlock.localPosition=start;padlock.localRotation=rotation;}dropped=ready;}
                else dropped|=door!=null&&door.IsUnlocked||gate!=null&&gate.Cleared;
                if(dropped){fall+=Time.deltaTime;padlock.localPosition=start+Vector3.down*Mathf.Min(Mathf.Max(0,start.y-.08f),fall*fall*3);padlock.localRotation=rotation*Quaternion.Euler(0,0,Mathf.Min(85,fall*150));}
            }
            if(exitPlate!=null)
            {
                bool ready=run.ReadyToEscape;var block=new MaterialPropertyBlock();exitPlate.GetPropertyBlock(block);block.SetColor("_BaseColor",ready?new Color(.25f,.72f,.28f):new Color(.35f,.35f,.32f));exitPlate.SetPropertyBlock(block);
                if(exitText!=null){exitText.text=ready?"MAIN EXIT\nGO!":"MAIN EXIT\n5 ITEMS";exitText.color=ready?new Color(.75f,1,.7f):new Color(.7f,.7f,.64f);}
                if(crossedOut!=null)crossedOut.SetActive(!ready);
            }
        }
    }
}
