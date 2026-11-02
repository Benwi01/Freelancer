using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Renders the player prefab from a few angles to Temp/player-preview.png (for quick visual checks).</summary>
    static class DevPreviewRender
    {
        [MenuItem("Freelancer/Setup/Dev/Render Player Preview", priority = 102)]
        static void Render()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.PlayerPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var origin = new Vector3(0, 1000f, 0); // far from whatever else is open
            try
            {
                var lightGo = new GameObject("Light", typeof(Light));
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                SceneManager_Move(lightGo, scene);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.GetComponent<Renderer>().sharedMaterial = SetupUtil.Mat("PreviewFloor", new Color(0.75f, 0.78f, 0.8f));
                SceneManager_Move(floor, scene);
                floor.transform.position = origin;

                float[] yaws = { 180f, 135f, 90f };
                for (int i = 0; i < yaws.Length; i++)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    p.transform.SetPositionAndRotation(origin + new Vector3((i - 1) * 1.3f, 0, 0), Quaternion.Euler(0, yaws[i], 0));
                    var tint = new SerializedObject(p.GetComponent<PlayerMotor>()).FindProperty("tintRenderers");
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", new[] { new Color(0.95f, 0.35f, 0.3f), new Color(0.3f, 0.6f, 0.95f), new Color(0.4f, 0.85f, 0.4f) }[i]);
                    for (int t = 0; t < tint.arraySize; t++)
                        (tint.GetArrayElementAtIndex(t).objectReferenceValue as Renderer)?.SetPropertyBlock(block);
                    var cam = p.GetComponentInChildren<Camera>(true);
                    if (cam != null) cam.gameObject.SetActive(false);
                }

                var camGo = new GameObject("PreviewCam", typeof(Camera));
                SceneManager_Move(camGo, scene);
                var c = camGo.GetComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
                c.fieldOfView = 35f;
                camGo.transform.position = origin + new Vector3(0, 1.3f, -5.8f);
                camGo.transform.LookAt(origin + new Vector3(0, 0.95f, 0));
                c.farClipPlane = 30f;

                var rt = new RenderTexture(1200, 800, 24) { antiAliasing = 4 };
                c.targetTexture = rt;
                c.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Application.dataPath, "../Temp/player-preview.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                c.targetTexture = null;
                rt.Release();
                Debug.Log("[Freelancer] Wrote Temp/player-preview.png");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
