using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>Renders a deterministic navigation map from the same SchoolPlan data that builds the level.</summary>
    public static class SchoolMapCapture
    {
        const string Output = "D:/Confiscated/output/maps/confiscated-school-map.png";
        const int MapLayer = 30;
        static readonly Color Ink = new(.055f,.075f,.10f);
        static readonly List<Material> transientMaterials = new();

        [MenuItem("Confiscated/Art/Capture Accurate School Map")]
        public static void Capture()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before rendering the map.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var root=new GameObject("Temporary accurate school map").transform;
            try
            {
                var ink=MaterialFrom("Map ink","M_Chapter_Ink",Ink);
                var paper=MaterialFrom("Map paper","M_Chapter_Paper",new Color(.92f,.83f,.59f));
                var hall=MaterialFrom("Map corridors","M_Chapter_Paper",new Color(.78f,.65f,.34f));
                var classroom=MaterialFrom("Map classrooms","M_Chapter_Green",new Color(.32f,.62f,.46f));
                var special=MaterialFrom("Map special rooms","M_Chapter_Brass",new Color(.90f,.57f,.16f));
                var danger=MaterialFrom("Map office and detention","M_Chapter_ToyRed",new Color(.72f,.24f,.20f));
                var closed=MaterialFrom("Map closed rooms","M_Chapter_Grey",new Color(.46f,.52f,.52f));
                var outdoor=MaterialFrom("Map outdoors","M_Pencil_Grass",new Color(.48f,.66f,.34f));
                var blue=MaterialFrom("Map landmarks","M_Trolley_SketchedTeal",new Color(.24f,.53f,.64f));

                // Oversized paper sheet behind the plan.
                Box(root,"Map paper",new Vector3(0,-.08f,57.5f),new Vector3(126,.10f,157),paper);
                Box(root,"Drawn border",new Vector3(0,-.04f,57.5f),new Vector3(123,.04f,153),ink);
                Box(root,"Map field",new Vector3(0,-.01f,57.5f),new Vector3(121.8f,.04f,151.8f),paper);

                foreach(var area in SchoolPlan.Areas)
                {
                    var center=SchoolPlan.Point(area.rect.center.x,area.rect.center.y);
                    var size=new Vector3(area.rect.width/SchoolPlan.PixelsPerMetre,.04f,area.rect.height/SchoolPlan.PixelsPerMetre);
                    Material fill=area.zone<0?outdoor:area.zone==0?hall:RoomMaterial(area.zone,classroom,special,danger,closed);
                    Box(root,area.name+" outline",center+Vector3.up*.03f,size+new Vector3(.20f,.02f,.20f),ink);
                    Box(root,area.name,center+Vector3.up*.06f,new Vector3(Mathf.Max(.03f,size.x-.16f),.04f,Mathf.Max(.03f,size.z-.16f)),fill);
                    string label=AreaLabel(area);
                    if(!string.IsNullOrEmpty(label))
                    {
                        float maxWidth=Mathf.Max(.7f,size.x-.35f),maxHeight=Mathf.Max(.45f,size.z-.30f);
                        float initial=area.zone==0?.14f:area.zone<0?.16f:.20f;
                        MapText(root,label,center+Vector3.up*.12f,initial,maxWidth,maxHeight,Ink);
                    }
                }

                // Door gaps are bright mustard strokes drawn directly over the room outlines.
                foreach(var door in SchoolPlan.Doors)
                {
                    var p=door.Position+Vector3.up*.13f;
                    var size=door.vertical?new Vector3(.22f,.04f,door.Gap):new Vector3(door.Gap,.04f,.22f);
                    Box(root,"Door - "+door.name,p,size,special);
                }

                Landmark(root,"FOUNTAIN",SchoolPlan.Point(904,560),blue,new Vector3(-3.7f,0,0));
                Landmark(root,"TROPHIES",SchoolPlan.Point(540,257),special,new Vector3(0,0,-2.2f));
                Landmark(root,"BOOK CART",SchoolPlan.Point(268,760),blue,new Vector3(3.4f,0,0));
                Landmark(root,"SKELETON",SchoolPlan.Point(690,480),danger,new Vector3(0,0,2.0f));

                Objective(root,"1",new Vector3(-44.58f,0,78.82f),danger,"PHONE");
                Objective(root,"2",new Vector3(-10.0f,0,44.3f),danger,"YO-YO");
                Objective(root,"3",new Vector3(10.61f,0,88.06f),danger,"GAME");
                Objective(root,"4",new Vector3(26.06f,0,54.72f),danger,"BOARD");
                Objective(root,"5",new Vector3(26.56f,0,10.22f),danger,"ROBOT");

                MapText(root,"CONFISCATED!  SCHOOL MAP",new Vector3(0,.14f,132.7f),.32f,70,3.2f,Ink);
                MapText(root,"N",new Vector3(53,.14f,130.4f),.28f,3,2.5f,Ink);
                Arrow(root,new Vector3(53,.12f,126.9f),special);
                MapText(root,"MAIN ENTRANCE / EXIT",SchoolPlan.Point(585,1265,.14f),.16f,18,2.2f,Ink);
                MapText(root,"START",new Vector3(-30.67f,.14f,25.89f),.16f,7,2.0f,Ink);
                MapText(root,"Numbers mark the five confiscated belongings   |   Yellow strokes are doors",new Vector3(0,.14f,-17.3f),.11f,92,2.2f,Ink);

                var lightObject=new GameObject("Map light");lightObject.transform.SetParent(root,false);lightObject.layer=MapLayer;
                lightObject.transform.rotation=Quaternion.Euler(90,0,0);
                var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.cullingMask=1<<MapLayer;light.shadows=LightShadows.None;

                var cameraObject=new GameObject("Map camera");cameraObject.transform.SetParent(root,false);cameraObject.layer=MapLayer;
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=78;
                camera.transform.position=new Vector3(0,100,57.5f);camera.transform.rotation=Quaternion.Euler(90,0,0);
                camera.cullingMask=1<<MapLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.07f,.065f);
                HallwayPropLibrary.Capture(camera,1300,1600,Output);
                Debug.Log("[SchoolMap] Rendered accurate plan to "+Output);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                foreach(var material in transientMaterials)Object.DestroyImmediate(material);
                transientMaterials.Clear();
            }
        }

        static Material RoomMaterial(int zone,Material classroom,Material special,Material danger,Material closed)
        {
            if(zone==1||zone==7)return danger;
            if(zone==4||zone==6||zone==8||zone==14)return special;
            if(zone==2||zone==3||zone==11||zone==13)return closed;
            return classroom;
        }

        static string AreaLabel(SchoolPlan.Area area)
        {
            if(area.zone<0)
            {
                if(area.name.EndsWith("yard",StringComparison.Ordinal))return area.name.ToUpperInvariant().Replace(" YARD","\nYARD");
                return "";
            }
            if(area.zone==0)
            {
                return area.name switch
                {
                    "West perimeter"=>"WEST\nCORRIDOR","East perimeter"=>"EAST\nCORRIDOR","North hall"=>"NORTH HALL",
                    "North cross hall"=>"NORTH CROSS HALL","South cross hall"=>"SOUTH CROSS HALL",
                    "South perimeter"=>"SOUTH HALL","Central spine"=>"CENTRAL","East inner spine"=>"EAST LINK",
                    _=>""
                };
            }
            return area.zone switch
            {
                1=>"CARETAKER\nOFFICE",2=>"CLOSED\nROOM",3=>"CLOSED\nCLASSROOM",4=>"RESOURCES",
                5=>"CLASSROOM 4",6=>"DINING\nHALL",7=>"DETENTION",8=>"EQUIPMENT",
                9=>"CLASSROOM 3",10=>"YEAR 6\nCLASSROOM",11=>"CLOSED\nROOM",12=>"ART\nROOM",
                13=>"CLOSED\nCLASSROOM",14=>"STORE",_=>area.name.ToUpperInvariant()
            };
        }

        static void Landmark(Transform root,string label,Vector3 position,Material material,Vector3 labelOffset)
        {
            Disc(root,label+" marker",position+Vector3.up*.16f,.72f,material);
            MapText(root,label,position+labelOffset+Vector3.up*.17f,.13f,8.5f,1.8f,Ink);
        }

        static void Objective(Transform root,string number,Vector3 position,Material material,string label)
        {
            Disc(root,label+" objective",position+Vector3.up*.20f,.75f,material);
            MapText(root,number,position+Vector3.up*.25f,.18f,1.0f,1.0f,Color.white);
            MapText(root,label,position+new Vector3(0,.21f,-1.2f),.11f,5.2f,1.3f,Ink);
        }

        static void Arrow(Transform root,Vector3 position,Material material)
        {
            Box(root,"North arrow stem",position,new Vector3(.22f,.05f,4.2f),material);
            var head=GameObject.CreatePrimitive(PrimitiveType.Cylinder);head.name="North arrow head";head.layer=MapLayer;
            head.transform.SetParent(root,false);head.transform.position=position+new Vector3(0,.03f,2.5f);
            head.transform.localScale=new Vector3(1.0f,.04f,1.0f);head.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void Disc(Transform root,string name,Vector3 position,float diameter,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.layer=MapLayer;go.transform.SetParent(root,false);
            go.transform.position=position;go.transform.localScale=new Vector3(diameter,.035f,diameter);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=MapLayer;go.transform.SetParent(root,false);
            go.transform.position=position;go.transform.localScale=size;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }

        static void MapText(Transform root,string value,Vector3 position,float size,float maxWidth,float maxHeight,Color color)
        {
            var go=new GameObject(value.Replace('\n',' '));go.layer=MapLayer;go.transform.SetParent(root,false);go.transform.position=position;
            go.transform.rotation=Quaternion.Euler(90,0,0);
            var text=go.AddComponent<TextMesh>();text.font=SchoolTypography.Font;text.fontSize=96;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=value;text.color=color;
            text.font.RequestCharactersInTexture(value,96);
            go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            var lines=value.Split('\n');int longest=1;
            foreach(var line in lines)longest=Mathf.Max(longest,line.Length);
            text.characterSize=Mathf.Min(size,maxWidth/(longest*3.6f),maxHeight/(lines.Length*5f));
        }

        static Material MaterialFrom(string name,string sourceName,Color fallback)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.name=name;
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+sourceName+".mat");
            var texture=source!=null&&source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null;
            if(texture!=null)material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",fallback);
            transientMaterials.Add(material);return material;
        }
    }
}
