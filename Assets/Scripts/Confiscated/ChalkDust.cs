using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Confiscated
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChalkDust : MaskableGraphic
    {
        struct Dust {public Vector2 p,v;public float life,size,initialLife;}
        readonly List<Dust> particles=new();
        public int Emitted {get;private set;}
        public void Puff(Vector2 at,int count)
        {
            for(int i=0;i<count&&particles.Count<600;i++){float life=Random.Range(.55f,1.25f);particles.Add(new Dust{p=at+Random.insideUnitCircle*23,v=new Vector2(Random.Range(-45,45),Random.Range(-15,45)),life=life,initialLife=life,size=Random.Range(1,3.8f)});Emitted++;}
        }
        public void Burst(Vector2 at,float strength)
        {
            int count=Mathf.RoundToInt(Mathf.Lerp(40,100,strength));
            for(int i=0;i<count&&particles.Count<600;i++)
            {
                float life=Random.Range(.6f,1.7f);Vector2 v=Random.insideUnitCircle*Mathf.Lerp(150,370,strength);v.y+=70;
                particles.Add(new Dust{p=at+Random.insideUnitCircle*20,v=v,life=life,initialLife=life,size=i%6==0?Random.Range(8,15):Random.Range(1.5f,5)});Emitted++;
            }
            SetVerticesDirty();
        }
        public void Clear(){particles.Clear();Emitted=0;SetVerticesDirty();}
        void Update()
        {
            for(int i=particles.Count-1;i>=0;i--){var p=particles[i];p.life-=Time.unscaledDeltaTime;if(p.life<=0){particles.RemoveAt(i);continue;}p.v.y-=90*Time.unscaledDeltaTime;p.p+=p.v*Time.unscaledDeltaTime;particles[i]=p;}
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();foreach(var p in particles){int n=vh.currentVertCount;var c=new Color(.91f,.90f,.77f,Mathf.Clamp01(p.life/Mathf.Max(.01f,p.initialLife))*(p.size>7?.13f:.85f));var r=Vector2.one*p.size;
                vh.AddVert(p.p-r,c,Vector2.zero);vh.AddVert(p.p+new Vector2(-r.x,r.y),c,Vector2.zero);vh.AddVert(p.p+r,c,Vector2.zero);vh.AddVert(p.p+new Vector2(r.x,-r.y),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        }
    }
}
