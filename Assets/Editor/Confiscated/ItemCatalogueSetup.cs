using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Builds Resources/ItemCatalogue: every pickup's picture and its short plain-English line, read by the TAB inventory
    /// tooltips, the codex and the new-item pop-up (Docs/Inventory.md). Pictures come from the existing item art; the
    /// sweets, which had none, are rendered from the office sweet jar. Safe to re-run: it only rewrites its own assets.
    /// </summary>
    public static class ItemCatalogueSetup
    {
        const string CataloguePath="Assets/Resources/ItemCatalogue.asset",SpriteDir="Assets/Art/UI/Items/",SweetsTexture="Assets/Art/Textures/T_Item_Sweets.png";

        // id, name, what it is, how to use it (empty if used with F where it matters), picture (sprite asset or texture).
        static readonly (string id,string name,string blurb,string use,string art)[] Entries=
        {
            (Items.Phone,"Your phone","Mr Reed took it. You're not leaving school without it.","","Assets/Data/Inventory/I_Phone.asset"),
            (Items.YoYo,"Your yo-yo","It glows in the dark. Taken at lunch.","","Assets/Art/Textures/T_Item_YoYo.png"),
            (Items.Handheld,"Your handheld game","Half a battery left and a high score to beat.","","Assets/Art/Textures/T_Item_HandheldGame.png"),
            (Items.Skateboard,"Your skateboard","Confiscated for riding it down the ramp. Worth it.","","Assets/Art/Textures/T_Item_Skateboard.png"),
            (Items.Robot,"Your toy robot","Taken for being \"a distraction\". It was.","","Assets/Art/Textures/T_Item_ToyRobot.png"),
            (Items.Duck,"Wind-up duck","Drop it and it quacks. The caretaker can't help stamping on it.","1 or right click to drop","Assets/Art/Textures/T_Item_WindUpToy.png"),
            (Items.Glue,"PVA glue","Pour it behind you. Whoever steps in it gets stuck for a few seconds.","2 or G to pour","Assets/Art/Textures/Glue/T_Glue_Bottle_v1.png"),
            (Items.Sweets,"Sweets","The chatterbox can't talk with his mouth full.","F on the chatterbox to give him one",SweetsTexture),
            (Items.Football,"Football","Throw it and he'll go where it lands.","1 to hold, click to throw","Assets/Data/Inventory/I_Football.asset"),
            (Items.BoltCutters,"Bolt cutters","From the caretaker's bench. They'll cut a chain.","","Assets/Art/Textures/T_Tool_BoltCutters.png"),
            (Items.StoreKey,"Store key","Opens the store cupboard.","","Assets/Art/Textures/T_Tool_StoreKey.png"),
            (Items.OfficeKey,"Caretaker's office key","Don't let him see you with it.","","Assets/Data/Inventory/I_OfficeKey.asset"),
            (Items.HallPass,"Hall pass","Lets you be out of class. Only while you're on the errand.","","Assets/Inventory/HallPass.asset"),
            (Items.Newsletters,"Newsletters","For the tray outside the caretaker's office.","","Assets/Inventory/Newsletters.asset"),
            (Items.Torch,"Your torch","For when the lights go out. Keep it off near the library shadow.","T to switch on","Assets/Data/Inventory/I_Torch.asset"),
        };

        [MenuItem("Confiscated/Items/Build Item Catalogue")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play first.");
            if(!File.Exists(SweetsTexture))RenderSweets();
            Directory.CreateDirectory(SpriteDir);
            var catalogue=AssetDatabase.LoadAssetAtPath<ItemCatalogue>(CataloguePath);
            if(catalogue==null){catalogue=ScriptableObject.CreateInstance<ItemCatalogue>();AssetDatabase.CreateAsset(catalogue,CataloguePath);}
            catalogue.items=Entries.Select(e=>new ItemCatalogue.Item{id=e.id,name=e.name,blurb=e.blurb,use=e.use,picture=Picture(e.id,e.art)}).ToArray();
            EditorUtility.SetDirty(catalogue);AssetDatabase.SaveAssets();
            var missing=catalogue.items.Where(i=>i.picture==null).Select(i=>i.id).ToArray();
            Debug.Log("[Items] Catalogue built: "+catalogue.items.Length+" items"+(missing.Length>0?", missing pictures: "+string.Join(", ",missing):", all with pictures")+".");
        }

        [MenuItem("Confiscated/Items/Forget Codex (testing)")]
        public static void Forget(){ItemCodex.ForgetAll();PlayerPrefs.Save();Debug.Log("[Items] Codex forgotten.");}

        /// <summary>An item definition's own icon, or a sprite made from a texture, cropped to its artwork.</summary>
        static Sprite Picture(string id,string path)
        {
            if(path.EndsWith(".asset"))return AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path)?.icon;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)return null;
            var rect=new Rect(0,0,texture.width,texture.height);
            // Crop the generator's empty padding: read the PNG itself, so the texture's import settings stay untouched.
            var probe=new Texture2D(2,2);
            if(probe.LoadImage(File.ReadAllBytes(path))&&probe.width==texture.width&&probe.height==texture.height)
            {
                var px=probe.GetPixels32();int minX=probe.width,minY=probe.height,maxX=0,maxY=0;
                for(int y=0;y<probe.height;y++)for(int x=0;x<probe.width;x++)if(px[y*probe.width+x].a>20){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                if(maxX>minX&&maxY>minY)
                {
                    int pad=Mathf.RoundToInt(Mathf.Max(maxX-minX,maxY-minY)*.04f);
                    minX=Mathf.Max(0,minX-pad);minY=Mathf.Max(0,minY-pad);maxX=Mathf.Min(probe.width-1,maxX+pad);maxY=Mathf.Min(probe.height-1,maxY+pad);
                    rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
                }
            }
            Object.DestroyImmediate(probe);
            var fresh=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);fresh.name="S_ItemPicture_"+id;
            string spritePath=SpriteDir+fresh.name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if(saved==null){AssetDatabase.CreateAsset(fresh,spritePath);saved=fresh;}
            else{EditorUtility.CopySerialized(fresh,saved);Object.DestroyImmediate(fresh);EditorUtility.SetDirty(saved);}
            return saved;
        }

        /// <summary>The sweets had no artwork: a transparent-background render of the office sweet jar stands in.</summary>
        [MenuItem("Confiscated/Items/Render Sweets Picture")]
        public static void RenderSweets()
        {
            var jar=Object.FindFirstObjectByType<SweetJar>(FindObjectsInactive.Include);
            if(jar==null){Debug.LogWarning("[Items] No sweet jar in the open scene; sweets picture not made.");return;}
            var renderers=jar.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.name!="Jar glass"&&(jar.emptyVisual==null||!r.transform.IsChildOf(jar.emptyVisual.transform))).ToArray();
            if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            const int Isolated=31;
            var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
            var urp=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;var drawer=urp.gpuResidentDrawerMode;
            var go=new GameObject("Sweets picture camera");var cam=go.AddComponent<Camera>();
            var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
            try
            {
                urp.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
                foreach(var r in renderers)r.gameObject.layer=Isolated;
                cam.enabled=false;cam.cullingMask=1<<Isolated;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(0,0,0,0);
                cam.orthographic=true;cam.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z)*1.2f;
                Vector3 view=Quaternion.Euler(18,-30,0)*Vector3.forward;
                cam.transform.position=bounds.center-view*4;cam.transform.rotation=Quaternion.LookRotation(view);cam.nearClipPlane=.05f;cam.farClipPlane=10;
                var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
                // The first render after switching the GPU Resident Drawer off comes back empty: render twice.
                cam.targetTexture=rt;cam.Render();cam.Render();
                RenderTexture.active=rt;var tex=new Texture2D(512,512,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();RenderTexture.active=null;
                File.WriteAllBytes(SweetsTexture,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];
                urp.gpuResidentDrawerMode=drawer;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(go);
            }
            AssetDatabase.ImportAsset(SweetsTexture);
            var importer=(TextureImporter)AssetImporter.GetAtPath(SweetsTexture);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            Debug.Log("[Items] Sweets picture rendered from the office sweet jar.");
        }
    }
}
