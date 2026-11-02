using System.IO;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Shared helpers for the setup menu items: folders, materials, primitives.</summary>
    static class SetupUtil
    {
        public const string Root = "Assets/_Project";
        public const string ScenesDir = Root + "/Scenes";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string MaterialsDir = Root + "/Art/Materials";
        public const string MenuScenePath = ScenesDir + "/MainMenu.unity";
        public const string GigScenePath = ScenesDir + "/CakeDelivery.unity";
        public const string HubScenePath = ScenesDir + "/Hub.unity";

        public static void EnsureFolders()
        {
            foreach (var dir in new[] { ScenesDir, PrefabsDir, MaterialsDir, Root + "/Scripts" })
                EnsureFolder(dir);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Get or create a flat-colour URP Lit material asset.</summary>
        public static Material Mat(string name, Color color, float smoothness = 0.1f)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A glowing material (lamps, headlights). Bright enough to catch a little bloom.</summary>
        public static Material Emissive(string name, Color color, float intensity = 2.2f)
        {
            var mat = Mat(name, color, 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A see-through material (glasses, windows you can see drinks through). Alpha comes from the colour.</summary>
        public static Material Transparent(string name, Color color, float smoothness = 0.85f)
        {
            var mat = Mat(name, color, smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A static greybox block (box collider, no rigidbody).</summary>
        public static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            float yRotation = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        /// <summary>A primitive used purely as a visual (collider removed).</summary>
        public static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }
    }
}
