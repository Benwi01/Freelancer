using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Walks through every rent letter on a fresh save: intro, missed, evicted, paid. Saves a
    /// screenshot of each to Temp/rent-*.png, then puts the real crew save back.
    /// </summary>
    [InitializeOnLoad]
    static class DevRentTest
    {
        const string Key = "Freelancer.RentTest.Stage";
        static double s_StartedAt;

        static DevRentTest() => EditorApplication.update += Tick;

        static string Backup => CrewSave.FilePath + ".rent-test-backup";

        [MenuItem("Freelancer/Setup/Dev/Test Rent Letters (local host)", priority = 108)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (File.Exists(CrewSave.FilePath)) File.Copy(CrewSave.FilePath, Backup, true);
            if (File.Exists(CrewSave.FilePath)) File.Delete(CrewSave.FilePath);
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        static double T => EditorApplication.timeSinceStartup - s_StartedAt;

        static void Next(int stage) => SessionState.SetInt(Key, stage);

        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;
            var crew = CrewState.Current;

            switch (stage)
            {
                case 1 when SessionManager.Instance != null && Time.realtimeSinceStartup > 1f:
                    SessionManager.Instance.HostLocal();
                    s_StartedAt = EditorApplication.timeSinceStartup;
                    Next(2);
                    break;
                case 2 when T > 6:
                    Shot("intro");
                    Next(3);
                    break;
                case 3 when T > 7:
                    CloseLetter();
                    crew.DevSetMoney(100);
                    crew.DevRentDay(); // can't pay → warning
                    Next(4);
                    break;
                case 4 when T > 8.5:
                    Shot("missed");
                    Next(5);
                    break;
                case 5 when T > 9.5:
                    CloseLetter();
                    Shot("hud-warning");
                    Next(6);
                    break;
                case 6 when T > 10.5:
                    crew.DevRentDay(); // still can't → evicted
                    Next(7);
                    break;
                case 7 when T > 12:
                    Shot("evicted");
                    Next(8);
                    break;
                case 8 when T > 13:
                    CloseLetter();
                    crew.DevSetMoney(1000);
                    crew.DevRentDay(); // pays
                    Next(9);
                    break;
                case 9 when T > 14.5:
                    Shot("paid");
                    Next(10);
                    break;
                case 10 when T > 15.5:
                    CloseLetter();
                    GigComputerUI.Instance?.Open();
                    Next(11);
                    break;
                case 11 when T > 16.5:
                    Shot("computer");
                    Next(12);
                    break;
                case 12 when T > 17.5:
                    Next(0);
                    EditorApplication.isPlaying = false;
                    if (File.Exists(Backup))
                    {
                        File.Copy(Backup, CrewSave.FilePath, true);
                        File.Delete(Backup);
                    }
                    Debug.Log("[Freelancer] RENT TEST done, crew save restored.");
                    break;
            }
        }

        static void Shot(string label)
        {
            var c = CrewState.Current;
            var letter = LandlordLetterUI.Instance;
            Debug.Log($"[Freelancer] RENT TEST {label}: letterOpen={(letter != null && letter.IsOpen)} " +
                      (c == null ? "no crew" :
                          $"money={c.Money} week={c.Week} due={c.RentDue} owed={c.RentOwed} gigsLeft={c.GigsUntilRent} " +
                          $"warning={c.OnFinalWarning} notice={c.Notice} amount={c.NoticeAmount} id={c.NoticeId}"));
            ScreenCapture.CaptureScreenshot($"Temp/rent-{label}.png");
        }

        static void CloseLetter()
        {
            var letter = LandlordLetterUI.Instance;
            if (letter != null && letter.IsOpen)
                typeof(LandlordLetterUI).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(letter, null);
        }
    }
}
