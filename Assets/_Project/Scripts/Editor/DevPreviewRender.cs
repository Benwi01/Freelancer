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

                float[] yaws = { 180f, 160f, 200f };
                for (int i = 0; i < yaws.Length; i++)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    p.transform.SetPositionAndRotation(origin + new Vector3((i - 1) * 1.3f, 0, 0), Quaternion.Euler(0, yaws[i], 0));
                    var looks = new[]
                    {
                        new PlayerLook { Color = 0, Hat = 0, Eyes = 0, Mouth = 0, Outfit = 0 },
                        new PlayerLook { Color = 1, Hat = 1, Eyes = 2, Mouth = 1, Outfit = 1 },
                        new PlayerLook { Color = 5, Hat = 2, Eyes = 1, Mouth = 0, Outfit = 1 },
                    };
                    p.GetComponent<PlayerAppearance>().Apply(looks[i]);
                    // Pupils are posed at runtime; give them a derpy pose for the picture.
                    foreach (var t in p.GetComponentsInChildren<Transform>())
                        if (t.name.StartsWith("PupilPivot"))
                            t.localRotation = Quaternion.Euler(Random.Range(-5f, 25f), Random.Range(-25f, 25f), 0);
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

                Shoot(c, "player-preview.png");
                c.fieldOfView = 22f;
                camGo.transform.position = origin + new Vector3(-0.1f, 1.66f, -1.5f);
                camGo.transform.LookAt(origin + new Vector3(0, 1.58f, 0));
                Shoot(c, "player-face.png");
                Debug.Log("[Freelancer] Wrote Temp/player-preview.png");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Shoot(Camera c, string file)
        {
            var rt = new RenderTexture(1200, 800, 24) { antiAliasing = 4 };
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../Temp/" + file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            c.targetTexture = null;
            rt.Release();
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
