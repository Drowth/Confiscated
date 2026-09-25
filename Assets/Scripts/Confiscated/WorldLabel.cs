using UnityEngine;

namespace Confiscated
{
    /// <summary>Depth-tested text for signs, with the live dynamic font atlas kept in sync.</summary>
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public class WorldLabel : MonoBehaviour
    {
        [SerializeField] Shader letteringShader;
        TextMesh lettering;
        Material material;
        void OnEnable()
        {
            lettering = GetComponent<TextMesh>();
            // Keep a serialized reference so player builds retain this shader.
            if (letteringShader == null) letteringShader = Shader.Find("Confiscated/World Lettering");
            material = new Material(letteringShader);
            material.hideFlags = HideFlags.HideAndDontSave;
            GetComponent<Renderer>().sharedMaterial = material;
            Font.textureRebuilt += RefreshAtlas;
            RefreshAtlas(lettering.font);
        }
        void RefreshAtlas(Font font)
        {
            if (material != null && font != null && font == lettering.font)
                material.mainTexture = font.material.mainTexture;
        }
        void OnDisable()
        {
            Font.textureRebuilt -= RefreshAtlas;
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            }
        }
    }
}
