using UnityEngine;
namespace Confiscated
{
    public sealed class SchoolNoiseMarks : MonoBehaviour
    {
        GameObject template;float lastAt=-10;
        void Start(){foreach(var bell in FindObjectsByType<SchoolBell>(FindObjectsSortMode.None))if(bell.ringingMarks!=null){template=bell.ringingMarks;break;}}
        void OnEnable(){NoiseEvents.OnNoise+=Show;}
        void OnDisable(){NoiseEvents.OnNoise-=Show;}
        void Show(Vector3 position,float radius,string cause)
        {
            if(template==null||Time.time-lastAt<.25f)return;
            lastAt=Time.time;var marks=Instantiate(template,position+Vector3.up*.4f,Quaternion.identity);marks.name="Audible pencil marks";marks.SetActive(true);marks.AddComponent<NoiseMarkLife>();
        }
    }
    public sealed class NoiseMarkLife : MonoBehaviour
    {
        float age;
        void Update(){age+=Time.deltaTime;if(Camera.main!=null)transform.rotation=Quaternion.LookRotation(transform.position-Camera.main.transform.position);transform.localScale=Vector3.one*(1+age*.8f);if(age>.8f)Destroy(gameObject);}
    }
}
