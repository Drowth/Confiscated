using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Print-oriented editorial rendering of the exact SchoolPlan geometry.</summary>
    public static class EditorialSchoolMapCapture
    {
        const string Output="D:/Confiscated/output/maps/confiscated-school-map-editorial.png";
        const int Layer=30;
        static readonly Color Navy=Hex("#17324D"), Cream=Hex("#F5F1E8"), Corridor=Hex("#E8E1D3"),
            Classroom=Hex("#9EC5B4"), ObjectiveRoom=Hex("#E7B45A"), AlertRoom=Hex("#D77868"),
            ClosedRoom=Hex("#C5CDD1"), Ground=Hex("#D5E2B5"), Door=Hex("#F3C44E"),
            Marker=Hex("#D94E4E"), LandmarkBlue=Hex("#367A9B"), Shadow=Hex("#CCD1D2");
        static readonly List<Material> materials=new();
        static Font regular,bold;

        [MenuItem("Confiscated/Art/Capture Magazine School Map")]
        public static void Capture()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before rendering the map.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            regular=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},96);
            bold=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI Semibold","Arial"},96);
            var root=new GameObject("Temporary magazine school map").transform;
            try
            {
                var navy=Solid("Navy",Navy);var cream=Solid("Cream",Cream);var corridor=Solid("Corridor",Corridor);
                var classroom=Solid("Classroom",Classroom);var objective=Solid("Objective room",ObjectiveRoom);
                var alert=Solid("Alert room",AlertRoom);var closed=Solid("Closed room",ClosedRoom);
                var ground=Solid("Ground",Ground);var door=Solid("Door",Door);var red=Solid("Objective marker",Marker);
                var blue=Solid("Landmark marker",LandmarkBlue);var shadow=Solid("Map shadow",Shadow);var white=Solid("White",Color.white);

                // Quiet editorial page with a subtle offset block behind the plan.
                Box(root,"Page",new Vector3(0,-.10f,55.5f),new Vector3(133,.08f,164),cream);
                Box(root,"Map shadow",new Vector3(.65f,-.04f,56.1f),new Vector3(73.2f,.03f,104.8f),shadow);

                foreach(var area in SchoolPlan.Areas)
                {
                    var center=SchoolPlan.Point(area.rect.center.x,area.rect.center.y);
                    var size=new Vector3(area.rect.width/SchoolPlan.PixelsPerMetre,.04f,area.rect.height/SchoolPlan.PixelsPerMetre);
                    Material fill=area.zone<0?ground:area.zone==0?corridor:RoomMaterial(area.zone,classroom,objective,alert,closed);
                    Box(root,area.name+" edge",center+Vector3.up*.02f,size+new Vector3(.18f,.02f,.18f),navy);
                    Box(root,area.name,center+Vector3.up*.05f,new Vector3(Mathf.Max(.03f,size.x-.14f),.04f,Mathf.Max(.03f,size.z-.14f)),fill);
                    string label=AreaLabel(area);
                    if(!string.IsNullOrEmpty(label))
                    {
                        float width=Mathf.Max(.7f,size.x-.40f),height=Mathf.Max(.45f,size.z-.35f);
                        float sizeLimit=area.zone==0?.12f:area.zone<0?.14f:.19f;
                        Text(root,label,center+Vector3.up*.11f,sizeLimit,width,height,Navy,true);
                    }
                }

                foreach(var entrance in SchoolPlan.Doors)
                {
                    var p=entrance.Position+Vector3.up*.13f;
                    var size=entrance.vertical?new Vector3(.24f,.04f,entrance.Gap):new Vector3(entrance.Gap,.04f,.24f);
                    Box(root,"Door - "+entrance.name,p,size,door);
                }

                Objective(root,"1","PHONE",new Vector3(-44.58f,0,78.82f),red,white);
                Objective(root,"2","YO-YO",new Vector3(-10f,0,44.3f),red,white);
                Objective(root,"3","HANDHELD",new Vector3(10.61f,0,88.06f),red,white);
                Objective(root,"4","SKATEBOARD",new Vector3(26.06f,0,54.72f),red,white);
                Objective(root,"5","ROBOT",new Vector3(26.56f,0,10.22f),red,white,false);

                Landmark(root,"A","Trophy cabinet",SchoolPlan.Point(540,257),blue);
                Landmark(root,"B","Library cart",SchoolPlan.Point(268,760),blue);
                Landmark(root,"C","Skeleton case",SchoolPlan.Point(690,480),blue);
                Landmark(root,"D","Water fountain",SchoolPlan.Point(904,560),blue);

                Text(root,"CONFISCATED!",new Vector3(-51,.15f,135.3f),.40f,34,3.6f,Navy,true,TextAnchor.MiddleLeft);
                Text(root,"SCHOOL NAVIGATION MAP",new Vector3(-51,.15f,131.9f),.19f,38,2.2f,Marker,true,TextAnchor.MiddleLeft);
                Text(root,"FIVE BELONGINGS. ONE WAY OUT.",new Vector3(-51,.15f,129.4f),.11f,38,1.7f,Navy,false,TextAnchor.MiddleLeft);
                Box(root,"Header rule",new Vector3(0,.10f,127.5f),new Vector3(105,.035f,.18f),navy);

                Text(root,"N",new Vector3(53,.15f,134.4f),.25f,3,2.2f,Navy,true);
                NorthArrow(root,new Vector3(53,.12f,130.5f),navy);
                Text(root,"START",new Vector3(-30.67f,.19f,25.89f),.13f,6,1.4f,Marker,true);
                Text(root,"MAIN EXIT",SchoolPlan.Point(585,1195,.18f),.12f,11,1.4f,Navy,true);

                Box(root,"Footer rule",new Vector3(0,.10f,-16.1f),new Vector3(105,.035f,.16f),navy);
                Text(root,"OBJECTIVES",new Vector3(-50,.15f,-18.7f),.14f,15,1.5f,Marker,true,TextAnchor.MiddleLeft);
                Text(root,"1 PHONE    2 YO-YO    3 HANDHELD    4 SKATEBOARD    5 ROBOT",new Vector3(-35,.15f,-18.7f),.10f,74,1.5f,Navy,true,TextAnchor.MiddleLeft);
                Text(root,"LANDMARKS",new Vector3(-50,.15f,-21.4f),.14f,15,1.5f,LandmarkBlue,true,TextAnchor.MiddleLeft);
                Text(root,"A TROPHY CABINET    B LIBRARY CART    C SKELETON CASE    D WATER FOUNTAIN",new Vector3(-35,.15f,-21.4f),.10f,78,1.5f,Navy,false,TextAnchor.MiddleLeft);
                Text(root,"Doors are shown in yellow. Closed rooms are grey.",new Vector3(-50,.15f,-24.0f),.095f,98,1.4f,Navy,false,TextAnchor.MiddleLeft);

                var cameraObject=new GameObject("Editorial map camera");cameraObject.layer=Layer;cameraObject.transform.SetParent(root,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=82;
                camera.transform.position=new Vector3(0,100,55.5f);camera.transform.rotation=Quaternion.Euler(90,0,0);
                camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Cream;
                HallwayPropLibrary.Capture(camera,1800,2200,Output);
                Debug.Log("[EditorialSchoolMap] Rendered magazine map to "+Output);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                foreach(var material in materials)Object.DestroyImmediate(material);materials.Clear();
                if(regular!=null)Object.DestroyImmediate(regular);if(bold!=null&&bold!=regular)Object.DestroyImmediate(bold);
                regular=bold=null;
            }
        }

        static Material RoomMaterial(int zone,Material classroom,Material objective,Material alert,Material closed)
        {
            if(zone==1||zone==7)return alert;
            if(zone==4||zone==6||zone==8||zone==14)return objective;
            if(zone==2||zone==3||zone==11||zone==13)return closed;
            return classroom;
        }

        static string AreaLabel(SchoolPlan.Area area)
        {
            if(area.zone<0)return area.name.EndsWith("yard",StringComparison.Ordinal)?area.name.ToUpperInvariant():"";
            if(area.zone==0)return area.name switch
            {
                "West perimeter"=>"WEST\nCORRIDOR","East perimeter"=>"EAST\nCORRIDOR","North hall"=>"NORTH HALL",
                "North cross hall"=>"NORTH CROSS HALL","South cross hall"=>"SOUTH CROSS HALL",
                "South perimeter"=>"SOUTH HALL",_=>""
            };
            return area.zone switch
            {
                1=>"CARETAKER\nOFFICE",2=>"CLOSED",3=>"CLOSED",4=>"RESOURCES",5=>"CLASSROOM 4",
                6=>"DINING\nHALL",7=>"DETENTION",8=>"EQUIPMENT",9=>"CLASSROOM 3",
                10=>"YEAR 6\nCLASSROOM",11=>"CLOSED",12=>"ART\nROOM",13=>"CLOSED",14=>"",_=>""
            };
        }

        static void Objective(Transform root,string number,string label,Vector3 position,Material red,Material white,bool showLabel=true)
        {
            Disc(root,label+" objective",position+Vector3.up*.17f,.92f,red);
            Text(root,number,position+Vector3.up*.22f,.15f,1.0f,1.0f,Color.white,true);
            if(showLabel)Text(root,label,position+new Vector3(0,.18f,-1.25f),.095f,6.8f,1.2f,Navy,true);
        }

        static void Landmark(Transform root,string code,string label,Vector3 position,Material material)
        {
            Disc(root,label+" landmark",position+Vector3.up*.16f,.62f,material);
            Text(root,code,position+Vector3.up*.21f,.11f,.7f,.7f,Color.white,true);
        }

        static void NorthArrow(Transform root,Vector3 position,Material material)
        {
            Box(root,"North line",position,new Vector3(.16f,.04f,4.0f),material);
            var head=GameObject.CreatePrimitive(PrimitiveType.Cylinder);head.layer=Layer;head.name="North point";head.transform.SetParent(root,false);
            head.transform.position=position+new Vector3(0,.03f,2.25f);head.transform.localScale=new Vector3(.65f,.035f,.65f);
            head.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void Disc(Transform root,string name,Vector3 position,float diameter,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.layer=Layer;go.name=name;go.transform.SetParent(root,false);
            go.transform.position=position;go.transform.localScale=new Vector3(diameter,.035f,diameter);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.layer=Layer;go.name=name;go.transform.SetParent(root,false);
            go.transform.position=position;go.transform.localScale=size;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }

        static void Text(Transform root,string value,Vector3 position,float maxSize,float maxWidth,float maxHeight,Color color,bool strong,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var go=new GameObject(value.Replace('\n',' '));go.layer=Layer;go.transform.SetParent(root,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,0,0);
            var text=go.AddComponent<TextMesh>();text.font=strong?bold:regular;text.fontSize=96;text.anchor=anchor;
            text.alignment=anchor==TextAnchor.MiddleLeft?TextAlignment.Left:TextAlignment.Center;text.text=value;text.color=color;
            text.fontStyle=strong?FontStyle.Bold:FontStyle.Normal;text.font.RequestCharactersInTexture(value,96,text.fontStyle);
            go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            var lines=value.Split('\n');int longest=1;foreach(var line in lines)longest=Mathf.Max(longest,line.Length);
            text.characterSize=Mathf.Min(maxSize,maxWidth/(longest*3.4f),maxHeight/(lines.Length*4.8f));
        }

        static Material Solid(string name,Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.name=name;material.SetColor("_BaseColor",color);
            materials.Add(material);return material;
        }

        static Color Hex(string value){ColorUtility.TryParseHtmlString(value,out var color);return color;}
    }
}
