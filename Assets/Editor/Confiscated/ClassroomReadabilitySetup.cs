using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Confiscated.EditorTools
{
    public static class ClassroomReadabilitySetup
    {
        public static string DoorCaption(SchoolPlan.Entrance door)
        {
            if(door.name=="Caretaker office")return "CARETAKER'S OFFICE";
            if(door.name=="South yard doors")return "MAIN ENTRANCE";
            if(door.name.Contains("yard"))return "COURTYARD";
            if(door.name.Contains("Dining"))return "DINING HALL";
            if(door.name=="Store cupboard")return "STORE";
            if(door.name=="East room A west")return "DETENTION";
            int a=door.vertical?SchoolPlan.ZoneAt(door.line-.1f,door.centre):SchoolPlan.ZoneAt(door.centre,door.line-.1f);
            int b=door.vertical?SchoolPlan.ZoneAt(door.line+.1f,door.centre):SchoolPlan.ZoneAt(door.centre,door.line+.1f);
            int number=Mathf.Max(a,b) switch {10=>1,11=>2,12=>3,13=>4,9=>5,8=>6,5=>7,4=>8,3=>9,2=>10,7=>11,_=>0};
            return number==1?"CLASSROOM 1\nYEAR 6":"CLASSROOM "+number;
        }

        [MenuItem("Confiscated/Improve Classroom Layout And Readability")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play mode before changing the school.");
            ApplyToScene();DiningHallSetup.Rebake();EditorSceneManager.SaveOpenScenes();
        }

        public static void ApplyToScene()
        {
            foreach(var door in SchoolPlan.Doors)
            {
                var sign=GameObject.Find("School/Details/"+door.name+" sign");
                if(sign==null)continue;
                var text=sign.GetComponentInChildren<TextMesh>();
                if(text==null)continue;
                text.text=DoorCaption(door);text.color=Color.black;
                var plate=sign.transform.Find("Plate");
                if(plate!=null){var s=plate.localScale;s.y=door.name.StartsWith("Year 6")?.38f:.24f;plate.localScale=s;}
                EditorUtility.SetDirty(text);
            }

            var classroom=GameObject.Find("School/Details/Year 6 furnishings")?.transform;
            if(classroom!=null)
            {
                var previous=classroom.Find("Additional pupil desks");
                if(previous!=null)Object.DestroyImmediate(previous.gameObject);
                var desk=classroom.Find("PupilDesk_00");var chair=classroom.Find("PupilChair_00");
                if(desk!=null&&chair!=null)
                {
                    var group=new GameObject("Additional pupil desks").transform;group.SetParent(classroom,false);
                    float[] rows={-27.38f,-24.365f,-21.35f,-18.335f};
                    float[] columns={20f,22f,24.2f,26.6f,29f};
                    var originals=classroom.Cast<Transform>().Where(t=>t.name.StartsWith("PupilDesk_")).ToArray();
                    for(int row=0;row<rows.Length;row++)for(int column=0;column<columns.Length;column++)
                    {
                        var p=new Vector3(rows[row],0,columns[column]);
                        if(originals.Any(t=>Vector3.Distance(t.position,p)<.1f))continue;
                        Clone(desk,"PupilDesk_Extra_"+row+"_"+column,p);
                        Clone(chair,"PupilChair_Extra_"+row+"_"+column,p+Vector3.right*.78f);
                    }
                    void Clone(Transform source,string name,Vector3 position)
                    {
                        var copy=Object.Instantiate(source.gameObject,group);copy.name=name;
                        copy.transform.SetPositionAndRotation(position,source.rotation);
                        copy.transform.localScale=source.localScale;
                    }
                }
            }
            // Printed signs and paper labels use black ink; board writing retains its chalk colour.
            foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                string path=AnimationUtility.CalculateTransformPath(text.transform,null);
                if(path.Contains("/Detention door plaque/")||path.Contains("/Delivery lettering")||
                   path.Contains("/RoundsList/")||path.Contains("/DrawerLabel/")||path.Contains("/PropertyShelfSign/")||
                   path.Contains("/StoresBox")||path.Contains("/LogTitle/")||path.Contains("/YourDesk/")||
                   path.Contains("/ConfiscationPolicy/")||path.Contains("/P_Hall_WetFloorSign/"))
                {text.color=Color.black;EditorUtility.SetDirty(text);}
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}

