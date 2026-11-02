using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Renders a few viewpoints of the Cake Delivery scene (with some posed characters) to
    /// Temp/gig-view-N.png, for checking the look without playing.
    /// </summary>
    static class DevGigViews
    {
        static readonly (Vector3 pos, Vector3 lookAt)[] k_Views =
        {
            (new Vector3(-1.5f, 1.65f, -4.8f), new Vector3(-2f, 2.6f, 4f)),     // street, looking at the building
            (new Vector3(3.5f, 2.2f, -1.5f), new Vector3(-0.5f, 0.9f, -8f)),    // sidewalk, looking at the van
            (new Vector3(-5f, 1.7f, 4.6f), new Vector3(-5f, 2.6f, 11f)),        // stairwell, bottom of flight 1
            (new Vector3(-0.8f, 7.65f, 8f), new Vector3(3.5f, 6.8f, 12f)),      // apartment
            (new Vector3(16f, 14f, -18f), new Vector3(0f, 3f, 4f)),             // overview
        };

        [MenuItem("Freelancer/Setup/Dev/Capture Gig Views", priority = 104)]
        static void Capture()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SetupUtil.GigScenePath)
                scene = EditorSceneManager.OpenScene(SetupUtil.GigScenePath);

            var temp = new List<GameObject>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // otherwise not-yet-compiled shaders render invisible
            try
            {
                // A few freelancers standing around, plus the cake in the van.
                var player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.PlayerPath);
                var looks = new[]
                {
                    (new Vector3(-1.2f, 0.1f, -2.8f), 200f, new PlayerLook { Color = 0, Hat = 1, Eyes = 0, Mouth = 0, Outfit = 1 }),
                    (new Vector3(0.6f, 0.1f, -3.4f), 160f, new PlayerLook { Color = 1, Hat = 0, Eyes = 1, Mouth = 1, Outfit = 0 }),
                    (new Vector3(-4.2f, 2.1f, 13.5f), 200f, new PlayerLook { Color = 3, Hat = 2, Eyes = 2, Mouth = 0, Outfit = 1 }),
                    (new Vector3(1.6f, 6.1f, 11f), 240f, new PlayerLook { Color = 5, Hat = 3, Eyes = 3, Mouth = 2, Outfit = 0 }),
                };
                foreach (var (pos, yaw, look) in looks)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(player, scene);
                    p.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
                    p.GetComponent<PlayerAppearance>().Apply(look);
                    foreach (var c in p.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
                    temp.Add(p);
                }
                var cake = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.CakePath), scene);
                cake.transform.position = new Vector3(0, 0.62f, -8.4f);
                temp.Add(cake);

                var camGo = new GameObject("DevViewCamera", typeof(Camera));
                temp.Add(camGo);
                var cam = camGo.GetComponent<Camera>();
                cam.fieldOfView = 70f;
                cam.nearClipPlane = 0.05f;
                LookSetup.ConfigureCamera(cam);

                // Throwaway frame: the first render after a rebuild can come out incomplete.
                cam.transform.position = k_Views[0].pos;
                cam.transform.LookAt(k_Views[0].lookAt);
                Shoot(cam, "gig-view-warmup.png");

                for (int i = 0; i < k_Views.Length; i++)
                {
                    cam.transform.position = k_Views[i].pos;
                    cam.transform.LookAt(k_Views[i].lookAt);
                    cam.fieldOfView = i == k_Views.Length - 1 ? 45f : 70f;
                    Shoot(cam, $"gig-view-{i + 1}.png");
                }
                Debug.Log("[Freelancer] Wrote Temp/gig-view-*.png");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                foreach (var go in temp)
                    if (go != null) Object.DestroyImmediate(go);
                // Everything temporary is gone again; save so the scene isn't left marked as modified.
                EditorSceneManager.SaveScene(scene);
            }
        }

        static void Shoot(Camera c, string file)
        {
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 1 };
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
    }
}
