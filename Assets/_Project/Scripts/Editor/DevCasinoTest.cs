using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Plays one round of roulette at Lucky Lenny's: hosts locally, walks the player up to the table,
    /// bets $50 on red and $25 on 17, and screenshots the panel, the spin and the payout to
    /// Temp/casino-*.png. Puts the real crew save back afterwards.
    /// </summary>
    [InitializeOnLoad]
    static class DevCasinoTest
    {
        const string Key = "Freelancer.CasinoTest.Stage";
        static double s_StartedAt;
        static int s_MoneyBefore;

        static DevCasinoTest() => EditorApplication.update += Tick;

        static string Backup => CrewSave.FilePath + ".casino-test-backup";

        [MenuItem("Freelancer/Setup/Dev/Test Roulette (local host)", priority = 109)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (File.Exists(CrewSave.FilePath)) File.Copy(CrewSave.FilePath, Backup, true);
            // Plenty to gamble with, and no landlord letter in the way.
            var save = CrewSave.Load();
            save.money = 500;
            save.rentIntroShown = true;
            save.Save();
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
            var table = RouletteTable.Current;

            switch (stage)
            {
                case 1 when SessionManager.Instance != null && Time.realtimeSinceStartup > 1f:
                    SessionManager.Instance.HostLocal();
                    s_StartedAt = EditorApplication.timeSinceStartup;
                    Next(2);
                    break;
                case 2 when T > 5:
                    Place(new Vector3(-21.5f, 0.05f, -19.2f), 180f, 4f); // outside, looking in through the door
                    Next(3);
                    break;
                case 3 when T > 6:
                    Shot("entrance");
                    Place(new Vector3(-17.5f, 0.05f, -21.2f), 230f, 12f); // the hall
                    Next(4);
                    break;
                case 4 when T > 7:
                    Shot("hall");
                    Place(new Vector3(-21.2f, 0.05f, -23.7f), 180f, 38f); // at the table, looking down at the wheel and felt
                    Next(5);
                    break;
                case 5 when T > 8 && table != null:
                    s_MoneyBefore = CrewState.Current.Money;
                    RouletteUI.Instance.Open(table);
                    table.ServerPlaceBet(NetworkManager.ServerClientId, RouletteBetKind.Red, 0, 50);
                    table.ServerPlaceBet(NetworkManager.ServerClientId, RouletteBetKind.Straight, 17, 25);
                    table.ServerPlaceBet(NetworkManager.ServerClientId, RouletteBetKind.Odd, 0, 10);
                    Next(6);
                    break;
                case 6 when T > 9.5:
                    Shot("betting");
                    Next(7);
                    break;
                case 7 when table != null && table.Phase == RoulettePhase.Spinning && table.SecondsLeft < 3.2f:
                    Shot("spin");
                    Next(8);
                    break;
                case 8 when table != null && table.Phase == RoulettePhase.Payout && table.SecondsLeft < 3.5f:
                    Shot("result");
                    int r = table.Result;
                    int expected = s_MoneyBefore - 85 +
                                   (RouletteRules.IsRed(r) ? 100 : 0) + (r == 17 ? 900 : 0) + (r > 0 && r % 2 == 1 ? 20 : 0);
                    Debug.Log($"[Freelancer] ROULETTE TEST result={RouletteRules.Describe(r)} before={s_MoneyBefore} " +
                              $"after={CrewState.Current.Money} expected={expected} {(expected == CrewState.Current.Money ? "OK" : "MISMATCH")} " +
                              $"staked={table.LastStaked} returned={table.LastReturned}");
                    Next(9);
                    break;
                case 9 when table != null && table.Phase == RoulettePhase.Betting:
                    Debug.Log($"[Freelancer] ROULETTE TEST next round open, bets on table={table.Bets.Count}");
                    RouletteUI.Instance.Close();
                    Next(0);
                    EditorApplication.isPlaying = false;
                    if (File.Exists(Backup))
                    {
                        File.Copy(Backup, CrewSave.FilePath, true);
                        File.Delete(Backup);
                    }
                    Debug.Log("[Freelancer] ROULETTE TEST done, crew save restored.");
                    break;
            }
        }

        /// <summary>Move the host's player and point the camera.</summary>
        static void Place(Vector3 pos, float yaw, float pitch)
        {
            if (!PlayerMotor.ByClientId.TryGetValue(NetworkManager.ServerClientId, out var me)) return;
            me.Teleport(pos);
            me.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            typeof(PlayerMotor).GetField("m_LocalPitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(me, pitch);
        }

        static void Shot(string label)
        {
            var c = CrewState.Current;
            var t = RouletteTable.Current;
            Debug.Log($"[Freelancer] ROULETTE TEST {label}: money={(c != null ? c.Money : -1)} " +
                      (t == null ? "no table" : $"phase={t.Phase} left={t.SecondsLeft:0.0} bets={t.Bets.Count} result={t.Result}"));
            ScreenCapture.CaptureScreenshot($"Temp/casino-{label}.png");
        }
    }
}
