using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Shared building blocks for level builders: visual boxes and meshes, solid props, windows,
    /// doors, lamps, signs and network spawners. Use with <c>using static</c>.
    /// </summary>
    static class SceneKit
    {
        public static Material Frame => SetupUtil.Mat("WindowFrame", Palette.WindowFrame);
        public static Material GlassPane => SetupUtil.Mat("WindowGlass", Palette.WindowGlass, 0.35f);
        public static Material Stone => SetupUtil.Mat("Stone", Palette.Stone);
        public static Material Metal => SetupUtil.Mat("Metal", Palette.Metal, 0.4f);
        public static Material WarmLight => SetupUtil.Emissive("WarmLight", Palette.WarmLight);
        public static Material Trim => SetupUtil.Mat("Trim", Palette.Trim);

        /// <summary>Framed window with a cross mullion and a sill, sticking out of a wall along local -z.</summary>
        public static void Window(Transform parent, Vector3 position, float yaw, float w, float h)
        {
            var p = SetupUtil.Empty("Window", parent, position).transform;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Frame", p, new Vector3(0, 0, -0.03f), new Vector3(w + 0.16f, h + 0.16f, 0.06f), Frame);
            Box("Glass", p, new Vector3(0, 0, -0.075f), new Vector3(w, h, 0.02f), GlassPane);
            Box("MullionV", p, new Vector3(0, 0, -0.095f), new Vector3(0.05f, h, 0.02f), Frame);
            Box("MullionH", p, new Vector3(0, 0.08f, -0.095f), new Vector3(w, 0.05f, 0.02f), Frame);
            Box("Sill", p, new Vector3(0, -h / 2f - 0.1f, -0.08f), new Vector3(w + 0.3f, 0.07f, 0.16f), Stone);
        }
        /// <summary>
        /// A closed front door on a wall: frame, panel, handle, peephole, number plate and doormat.
        /// <paramref name="floorPoint"/> is where the door meets the floor on the wall face;
        /// yaw turns local +z (out of the wall) toward the corridor.
        /// </summary>
        public static void Door(Transform parent, Vector3 floorPoint, float yaw, string number, int colour)
        {
            var p = SetupUtil.Empty("Door_" + number, parent, floorPoint).transform;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Frame", p, new Vector3(0f, 1.1f, 0.03f), new Vector3(1.16f, 2.22f, 0.06f), Stone);
            Box("Panel", p, new Vector3(0f, 1.03f, 0.06f), new Vector3(0.96f, 2.04f, 0.04f),
                SetupUtil.Mat("Door" + colour, Palette.Doors[colour]));
            Box("PanelInsetTop", p, new Vector3(0f, 1.5f, 0.083f), new Vector3(0.66f, 0.7f, 0.01f),
                SetupUtil.Mat("Door" + colour, Palette.Doors[colour]));
            Box("PanelInsetLow", p, new Vector3(0f, 0.55f, 0.083f), new Vector3(0.66f, 0.6f, 0.01f),
                SetupUtil.Mat("Door" + colour, Palette.Doors[colour]));
            var brass = SetupUtil.Mat("Brass", Palette.Brass, 0.6f);
            Vis(LowPolyMeshes.Sphere, "Handle", p, new Vector3(0.36f, 1f, 0.11f), Vector3.one * 0.07f, brass);
            Vis(LowPolyMeshes.Sphere, "Peephole", p, new Vector3(0f, 1.55f, 0.085f), Vector3.one * 0.035f, brass);
            Box("Doormat", p, new Vector3(0f, 0.007f, 0.45f), new Vector3(0.9f, 0.012f, 0.55f),
                SetupUtil.Mat("Doormat", Palette.Doormat));

            // Number plate on the door, readable from the corridor.
            Vector3 platePos = p.TransformPoint(new Vector3(0f, 1.95f, 0.12f));
            Label(number, parent, platePos, p.rotation * Quaternion.Euler(0f, 180f, 0f), 0.08f, Palette.Trim, Palette.Brass);
        }
        /// <summary>A warm point light (no shadows, cheap).</summary>
        public static void Lamp(Transform parent, Vector3 position, float range, float intensity)
        {
            var go = SetupUtil.Empty("Light", parent, position);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Palette.WarmLight;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
        /// <summary>Visual-only box (no collider).</summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, Quaternion? rot = null)
        {
            var go = SetupUtil.Visual(PrimitiveType.Cube, name, parent, localPos, size, mat);
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.isStatic = true;
            return go;
        }
        /// <summary>Visual-only low-poly mesh.</summary>
        public static GameObject Vis(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
            Quaternion? rot = null)
        {
            var go = LowPolyMeshes.Visual(mesh, name, parent, localPos, scale, mat, rot);
            go.isStatic = true;
            return go;
        }
        /// <summary>A primitive you can bump into (keeps its collider).</summary>
        public static GameObject Solid(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }
        public static void Spawner(Transform parent, string prefabPath, Vector3 position, float yaw, string name = null)
        {
            var go = new GameObject(name ?? "Spawn_" + System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var spawner = go.AddComponent<ServerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        /// <summary>
        /// Crisp world-space text (TextMesh Pro). Readable when looking along the rotation's +z;
        /// with a plate colour it sits on a sign board sized to fit the text.
        /// </summary>
        public static GameObject Label(string text, Transform parent, Vector3 position, Quaternion rotation, float letterHeight,
            Color color, Color? plate = null)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = letterHeight * 13f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);

            if (plate.HasValue)
            {
                tmp.ForceMeshUpdate();
                Bounds b = tmp.textBounds;
                float pad = letterHeight * 0.55f;
                var board = SetupUtil.Visual(PrimitiveType.Cube, "Plate", go.transform,
                    new Vector3(b.center.x, b.center.y, 0.015f),
                    new Vector3(b.size.x + pad * 2f, b.size.y + pad * 2f, 0.02f),
                    SetupUtil.Mat("Sign_" + ColorUtility.ToHtmlStringRGB(plate.Value), plate.Value));
                board.isStatic = true;
            }
            return go;
        }
    }
}
