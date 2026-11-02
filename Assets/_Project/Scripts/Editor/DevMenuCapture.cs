using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Plays the main menu, saves a screenshot to Temp/menu-capture.png after a moment, then stops.</summary>
    [InitializeOnLoad]
    static class DevMenuCapture
    {
        const string Key = "Freelancer.MenuCapture.Stage";

        static DevMenuCapture() => EditorApplication.update += Tick;

        [MenuItem("Freelancer/Setup/Dev/Capture Main Menu", priority = 103)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            SessionState.SetFloat(Key + ".At", 0f);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;

            if (stage == 1 && Time.realtimeSinceStartup > 0f)
            {
                SessionState.SetFloat(Key + ".At", (float)EditorApplication.timeSinceStartup);
                SessionState.SetInt(Key, 2);
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".At", 0f) > 3)
            {
                ScreenCapture.CaptureScreenshot("Temp/menu-capture.png");
                SessionState.SetInt(Key, 3);
            }
            else if (stage == 3 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".At", 0f) > 4.5)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
