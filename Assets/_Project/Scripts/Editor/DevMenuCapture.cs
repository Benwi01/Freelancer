using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Plays the main menu and saves screenshots to Temp/, then stops. "Capture Main Menu" grabs the
    /// menu; "Capture Hub And Computer" hosts locally, grabs the apartment, opens the GigGrabber app and grabs that.
    /// </summary>
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
            SessionState.SetInt(Key + ".Hub", 0);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Freelancer/Setup/Dev/Capture Hub And Computer", priority = 106)]
        static void RunHub()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            SessionState.SetFloat(Key + ".At", 0f);
            SessionState.SetInt(Key + ".Hub", 1);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        static double Since => EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".At", 0f);

        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;

            if (stage == 1 && Time.realtimeSinceStartup > 0f)
            {
                SessionState.SetFloat(Key + ".At", (float)EditorApplication.timeSinceStartup);
                SessionState.SetInt(Key, SessionState.GetInt(Key + ".Hub", 0) == 1 ? 10 : 2);
            }
            else if (stage == 10 && Since > 1.5 && SessionManager.Instance != null)
            {
                SessionManager.Instance.HostLocal();
                SessionState.SetInt(Key, 11);
            }
            else if (stage == 11 && Since > 7)
            {
                ScreenCapture.CaptureScreenshot("Temp/hub-play.png");
                SessionState.SetInt(Key, 12);
            }
            else if (stage == 12 && Since > 8)
            {
                if (GigComputerUI.Instance != null) GigComputerUI.Instance.Open();
                SessionState.SetInt(Key, 13);
            }
            else if (stage == 13 && Since > 9)
            {
                ScreenCapture.CaptureScreenshot("Temp/computer-capture.png");
                SessionState.SetInt(Key, 14);
            }
            else if (stage == 14 && Since > 10.5)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.isPlaying = false;
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
