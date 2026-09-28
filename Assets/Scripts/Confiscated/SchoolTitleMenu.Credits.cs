using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Title screen credits: the camera sits at the back of Year 6. The testers are the pupils at the four desks, seen from
    /// behind, each with a folded name card; the board says who made it. Everything is borrowed and put back on close.
    /// </summary>
    public sealed partial class SchoolTitleMenu
    {
        public bool CreditsOpen=>creditsOpen;
        bool creditsOpen;
        GameObject creditsUi,creditsProps;Button creditsBack;
        TextMesh board;string boardWas;
        readonly List<GameObject> hiddenClassmates=new();
        // Just behind a 2x2 block of desks in the middle of the room, looking over the testers' shoulders at the board.
        public static readonly Vector3 CreditsCamera=new(-17.3f,1.72f,25.4f),CreditsLookAt=new(-31f,1.05f,25.4f);
        static readonly string[] Desks={"Additional pupil desks/PupilDesk_Extra_2_2","Additional pupil desks/PupilDesk_Extra_2_3","Additional pupil desks/PupilDesk_Extra_1_2","Additional pupil desks/PupilDesk_Extra_1_3"};

        public void ShowCredits(bool show)
        {
            if(show==creditsOpen||starting)return;
            creditsOpen=show;
            menuGroup.alpha=show?0:1;menuGroup.interactable=menuGroup.blocksRaycasts=!show;
            if(show){BuildCreditsProps();BuildCreditsUi();creditsUi.SetActive(true);EventSystem.current?.SetSelectedGameObject(creditsBack.gameObject);}
            else{RestoreCreditsProps();if(creditsUi!=null)creditsUi.SetActive(false);EventSystem.current?.SetSelectedGameObject(startButton.gameObject);}
        }

        void PoseCredits()
        {
            Pose(CreditsCamera,CreditsLookAt,58);
            fade.color=Color.clear;caption.text="";
        }

        void BuildCreditsProps()
        {
            var year6=GameObject.Find("Year 6 furnishings");if(year6==null)return;
            creditsProps=new GameObject("Title credits (temporary)");
            // The two regular classmates step out of shot; the testers take the four desks.
            var classmates=year6.GetComponentsInChildren<SeatedStudent>().Where(s=>s.GetComponent<ChatterboxStudent>()==null).ToList();
            var template=classmates.FirstOrDefault(s=>s.name=="Seated classmate")??classmates.FirstOrDefault();
            foreach(var c in classmates){hiddenClassmates.Add(c.gameObject);c.gameObject.SetActive(false);}
            var paper=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.97f,.94f,.84f)};
            creditsProps.AddComponent<MaterialOwner>().material=paper;
            for(int i=0;i<Desks.Length&&i<SchoolCredits.Testers.Length;i++)
            {
                var desk=year6.transform.Find(Desks[i]);var chair=year6.transform.Find(Desks[i].Replace("Desk","Chair"));
                if(desk==null||chair==null||template==null)continue;
                var kid=Instantiate(template.gameObject,creditsProps.transform);kid.name=SchoolCredits.Testers[i]+" (tester)";kid.SetActive(true);
                kid.transform.SetPositionAndRotation(new Vector3(chair.position.x,template.transform.position.y,chair.position.z),template.transform.rotation);
                kid.transform.localScale=template.transform.lossyScale*(1+(i%2==0?.03f:-.03f));
                NameCard(desk,SchoolCredits.Testers[i],paper);
                ChairLabel(chair,SchoolCredits.Testers[i],paper);
            }
            // The board says who made it (the lesson text comes back on close).
            board=year6.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t=>t.name=="BoardWriting");
            if(board!=null){boardWas=board.text;board.text="CONFISCATED!\n\nCreated and developed by\n"+SchoolCredits.Creator;}
        }

        void NameCard(Transform desk,string name,Material paper)
        {
            var top=desk.Find("Top")?.GetComponent<Renderer>();if(top==null)return;
            var b=top.bounds;
            Vector3 along=b.size.x>=b.size.z?Vector3.right:Vector3.forward,across=Vector3.Cross(Vector3.up,along);
            // Beside the pupil (on the outer side of the block) rather than behind them, so their back doesn't hide it.
            float outer=Mathf.Sign(Vector3.Dot(b.center-CreditsCamera,along));if(outer==0)outer=1;
            var card=new GameObject("Name card - "+name).transform;card.SetParent(creditsProps.transform,false);
            card.SetPositionAndRotation(new Vector3(b.center.x,b.max.y+.07f,b.center.z)+along*.33f*outer,Quaternion.LookRotation(across));
            var p=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(card,false);p.transform.localScale=new Vector3(.44f,.13f,.004f);p.GetComponent<Renderer>().sharedMaterial=paper;
            foreach(var side in new[]{-1f,1f})
            {
                var g=new GameObject(name);g.transform.SetParent(card,false);
                g.transform.localPosition=new Vector3(0,0,side*.0035f);g.transform.localRotation=side<0?Quaternion.identity:Quaternion.Euler(0,180,0);
                var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=64;t.characterSize=.0072f;t.fontStyle=FontStyle.Bold;
                t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;t.text=name;
                g.AddComponent<WorldLabel>();
            }
        }

        // The camera looks at the pupils' backs, so each chair back carries its sitter's name too, facing the camera.
        void ChairLabel(Transform chair,string name,Material paper)
        {
            var parts=chair.GetComponentsInChildren<Renderer>();if(parts.Length==0)return;
            var b=parts[0].bounds;foreach(var r in parts)b.Encapsulate(r.bounds);
            Vector3 toCamera=CreditsCamera-b.center;toCamera.y=0;toCamera=Mathf.Abs(toCamera.x)>Mathf.Abs(toCamera.z)?new Vector3(Mathf.Sign(toCamera.x),0,0):new Vector3(0,0,Mathf.Sign(toCamera.z));
            float reach=Vector3.Scale(b.extents,new Vector3(Mathf.Abs(toCamera.x),0,Mathf.Abs(toCamera.z))).magnitude;
            var label=new GameObject("Chair label - "+name).transform;label.SetParent(creditsProps.transform,false);
            label.SetPositionAndRotation(new Vector3(b.center.x,b.max.y-.13f,b.center.z)+toCamera*(reach+.012f),Quaternion.LookRotation(-toCamera));
            var p=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(label,false);p.transform.localScale=new Vector3(.48f,.13f,.004f);p.GetComponent<Renderer>().sharedMaterial=paper;
            var g=new GameObject(name);g.transform.SetParent(label,false);g.transform.localPosition=new Vector3(0,0,-.004f);
            var t=g.AddComponent<TextMesh>();t.font=SchoolTypography.Font;t.fontSize=64;t.characterSize=.0085f;t.fontStyle=FontStyle.Bold;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.black;t.text=name;
            g.AddComponent<WorldLabel>();
        }

        void RestoreCreditsProps()
        {
            foreach(var c in hiddenClassmates)if(c!=null)c.SetActive(true);hiddenClassmates.Clear();
            if(board!=null){board.text=boardWas;board=null;}
            if(creditsProps!=null){Destroy(creditsProps);creditsProps=null;}
        }

        void BuildCreditsUi()
        {
            if(creditsUi!=null)return;
            var root=Rect("Credits",canvas.transform,Vector2.zero,Vector2.one);creditsUi=root.gameObject;
            Label("CREDITS",root,new Vector2(.07f,.80f),new Vector2(.5f,.87f),34,Cream);
            var card=ImageRect("Credits card",root,new Vector2(.07f,.60f),new Vector2(.50f,.78f),new Color(Cream.r,Cream.g,Cream.b,.94f));card.raycastTarget=false;
            var edge=Rect("Pencil edge",card.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<SketchBorder>();edge.color=Ink;edge.raycastTarget=false;
            var words=Label("Created and developed by  "+SchoolCredits.Creator+"\nTested by  "+SchoolCredits.TesterList+"\n<size=18>Thanks for playing CONFISCATED!</size>",card.transform,new Vector2(.04f,.08f),new Vector2(.96f,.92f),24,Color.black);
            words.supportRichText=true;words.alignment=TextAnchor.MiddleLeft;
            creditsBack=MakeButton("Back",root,new Vector2(.63f,.12f),new Vector2(.76f,.19f),()=>ShowCredits(false));
            creditsUi.SetActive(false);
        }

        sealed class MaterialOwner:MonoBehaviour{public Material material;void OnDestroy(){if(material!=null)Destroy(material);}}
    }
}
