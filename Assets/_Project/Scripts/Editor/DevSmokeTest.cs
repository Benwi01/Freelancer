using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Quick automated check: open MainMenu, enter Play, host locally (no Relay), wait a few
    /// seconds and log what got spawned. Then leave Play mode.
    /// </summary>
    [InitializeOnLoad]
    static class DevSmokeTest
    {
        const string Key = "Freelancer.SmokeTest.Stage";
        static double s_StartedAt;

        static DevSmokeTest()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem("Freelancer/Setup/Dev/Smoke Test (local host)", priority = 100)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;

            if (stage == 1 && SessionManager.Instance != null && Time.realtimeSinceStartup > 1f)
            {
                SessionManager.Instance.HostLocal();
                s_StartedAt = EditorApplication.timeSinceStartup;
                SessionState.SetInt(Key, 2);
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - s_StartedAt > 8)
            {
                var nm = NetworkManager.Singleton;
                var spawned = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.SpawnedObjectsList.Select(o => o.name.Replace("(Clone)", "")).ToList()
                    : new System.Collections.Generic.List<string>();
                string summary = string.Join(", ", spawned.GroupBy(n => n).Select(g => $"{g.Key} x{g.Count()}"));
                Debug.Log($"[Freelancer] SMOKE scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                          $"host={nm != null && nm.IsHost} spawned=[{summary}] ");
                SessionState.SetInt(Key, 0);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
