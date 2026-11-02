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
