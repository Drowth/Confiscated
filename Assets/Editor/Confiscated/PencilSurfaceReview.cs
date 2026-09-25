using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Confiscated.EditorTools
{
    public static class PencilSurfaceReview
    {
        [MenuItem("Confiscated/Capture Pencil Surface Review")]
        public static void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath,"../../Screenshots/SurfacePass2"));
            Directory.CreateDirectory(dir);
            var cameraObject = new GameObject("PencilSurfaceReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = .035f;
            camera.farClipPlane = 50f;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            try
            {
                camera.fieldOfView = 65f;
                camera.transform.position = new Vector3(0,1.55f,2.2f);
                camera.transform.LookAt(new Vector3(0,1.55f,19f));
                Save(camera,1920,1080,Path.Combine(dir,"01_FullCorridor.png"));
                // Inspection camera only: keep moving actors from obscuring the requested surface close-ups.
                camera.cullingMask &= ~(LayerMask.GetMask("Enemy", "Player"));
                camera.fieldOfView = 55f;
                Close(camera,dir,"02_Window","Window_Escape",0,1.6f);
                Close(camera,dir,"03_OfficeDoor","Door_Office_South",1f,2.45f);
                Close(camera,dir,"04_Noticeboard","Noticeboard_Corridor_West",0,1.35f);
                Close(camera,dir,"05_FireDoors","Door_FireExit_End",1f,2.8f);
                var chair = GameObject.Find("Chair_Corridor").transform;
                var centre = chair.position + Vector3.up*.48f;
                camera.transform.position = centre + new Vector3(-.85f,.4f,-1.1f);
                camera.transform.LookAt(centre);
                Save(camera,1200,1200,Path.Combine(dir,"06_Chair.png"));
                camera.transform.position = centre + chair.forward*1.25f - chair.right*.35f + Vector3.up*.15f;
                camera.transform.LookAt(centre);
                Save(camera,1200,1200,Path.Combine(dir,"07_ChairRear.png"));
                camera.transform.position = chair.position + new Vector3(-.7f,.26f,-.65f);
                camera.transform.LookAt(chair.position+Vector3.up*.3f);
                Save(camera,1200,1200,Path.Combine(dir,"08_ChairLegsAndUnderside.png"));
                Close(camera,dir,"09_WindowOblique","Window_Escape",0,1.1f,.65f);
                Close(camera,dir,"10_OfficeDoorEdges","Door_Office_South",1f,1.65f,.8f);
                Close(camera,dir,"11_FireDoorEdges","Door_FireExit_End",1f,1.8f,.9f);
                Close(camera,dir,"12_NoticeboardEdges","Noticeboard_Corridor_West",0,.95f,.55f);
                File.WriteAllText(Path.Combine(dir,"CaptureMode.txt"),
                    "Captured from the running scene: " + Application.isPlaying + "\nLighting, post-processing, geometry and materials are the scene's actual configuration.\nThe close-up camera excludes Player and Enemy layers so actors cannot obscure surfaces; the corridor camera includes all layers.\n");
                Debug.Log("[Confiscated] Saved corridor, five close-ups and six side inspections to " + dir);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        static void Close(Camera camera, string dir, string file, string name, float height, float distance, float side=0)
        {
            var target = GameObject.Find(name).transform;
            var centre = target.position + Vector3.up*height;
            camera.transform.position = centre - target.forward*distance + target.right*side;
            camera.transform.LookAt(centre);
            Save(camera,1200,1200,Path.Combine(dir,file+".png"));
        }

        static void Save(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            rt.Create();
            var previous = RenderTexture.active;
            var texture = new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);
                texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
