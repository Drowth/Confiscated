using UnityEngine;
using UnityEditor;

namespace Confiscated.EditorTools
{
    public static class PaperInventoryArtSetup
    {
        public static string SpritePath(string id)=>"Assets/Art/UI/S_Item_"+id+".asset";

        [MenuItem("Confiscated/Inventory/Apply Paper Item Artwork")]
        public static void Build()
        {
            foreach(string id in new[]{"HallPass","Newsletters"})
            {
                string path="Assets/Art/UI/T_Item_"+id+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
                importer.isReadable=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                // Fit the icon to its artwork instead of the generator's empty canvas padding.
                var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a>20){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                minX=Mathf.Max(0,minX-4);minY=Mathf.Max(0,minY-4);
                maxX=Mathf.Min(texture.width-1,maxX+4);maxY=Mathf.Min(texture.height-1,maxY+4);
                var fresh=Sprite.Create(texture,new Rect(minX,minY,maxX-minX+1,maxY-minY+1),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                fresh.name="S_Item_"+id;
                var saved=AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(id));
                if(saved==null){AssetDatabase.CreateAsset(fresh,SpritePath(id));saved=fresh;}
                else{EditorUtility.CopySerialized(fresh,saved);Object.DestroyImmediate(fresh);EditorUtility.SetDirty(saved);}
                var item=AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>("Assets/Inventory/"+id+".asset");
                item.icon=saved;EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
