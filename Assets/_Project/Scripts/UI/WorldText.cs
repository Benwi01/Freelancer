using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Gives a TextMesh a material that is hidden behind walls. The font texture is generated at
    /// runtime, so it's hooked up here (and again whenever Unity rebuilds the font texture).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public class WorldText : MonoBehaviour
    {
        [SerializeField] Material material; // uses the Freelancer/WorldText shader

        static Material s_Shared;

        void OnEnable()
        {
            var text = GetComponent<TextMesh>();
            if (material == null || text.font == null) return;

            if (s_Shared == null)
            {
                s_Shared = new Material(material) { name = "WorldText (runtime)", hideFlags = HideFlags.DontSave };
                Font.textureRebuilt += OnFontRebuilt;
            }
            s_Shared.mainTexture = text.font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial = s_Shared;
        }

        static void OnFontRebuilt(Font font)
        {
            if (s_Shared != null) s_Shared.mainTexture = font.material.mainTexture;
        }
    }
}
