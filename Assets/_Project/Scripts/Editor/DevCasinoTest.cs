using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Plays three rounds of roulette at Lucky Lenny's on a local host: bets on red, 17 and odd,
    /// takes the odd bet back and moves it to even, spins from the wheel, and lets the physics ball
    /// land. Checks every payout and saves screenshots to Temp/casino-*.png (first round), then puts
    /// the real crew save back.
    /// </summary>
    [InitializeOnLoad]
    static class DevCasinoTest
    {
        const string Key = "Freelancer.CasinoTest.Stage";
        const int Rounds = 3;
        static double s_StartedAt, s_SpinAt;
        static int s_MoneyBefore, s_Round;

        static DevCasinoTest() => EditorApplication.update += Tick;

        static string Backup => CrewSave.FilePath + ".casino-test-backup";

        [MenuItem("Freelancer/Setup/Dev/Test Roulette (local host)", priority = 109)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (File.Exists(CrewSave.FilePath)) File.Copy(CrewSave.FilePath, Backup, true);
            // Plenty to gamble with, and no landlord letter in the way.
            var save = CrewSave.Load();
            save.money = 1000;
            save.rentIntroShown = true;
            save.Save();
            s_Round = 0;
            RouletteTable.Trace = false; // true logs the ball's flight every quarter second
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            SessionState.SetInt(Key, 1);
            EditorApplication.isPlaying = true;
        }

        static double T => EditorApplication.timeSinceStartup - s_StartedAt;
        static double SinceSpin => EditorApplication.timeSinceStartup - s_SpinAt;

        static void Next(int stage) => SessionState.SetInt(Key, stage);

        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;
            var table = RouletteTable.Current;
            ulong host = NetworkManager.ServerClientId;

            switch (stage)
            {
                case 1 when SessionManager.Instance != null && Time.realtimeSinceStartup > 1f:
                    SessionManager.Instance.HostLocal();
                    s_StartedAt = EditorApplication.timeSinceStartup;
                    Next(2);
                    break;
                case 2 when T > 5:
                    Place(new Vector3(-21.2f, 0.05f, -23.7f), 180f, 38f); // at the table, looking down at the felt
                    Next(3);
                    break;
                case 3 when T > 6 && table != null:
                    s_MoneyBefore = CrewState.Current.Money;
                    RouletteTable.LocalChip = 50;
                    table.ServerPlaceBet(host, RouletteBetKind.Red, 0, 50);
                    table.ServerPlaceBet(host, RouletteBetKind.Straight, 17, 25);
                    table.ServerPlaceBet(host, RouletteBetKind.Odd, 0, 10);
                    // Changed my mind: odd → even.
                    int refund = table.ServerTakeBack(host, RouletteBetKind.Odd, 0, all: false);
                    table.ServerPlaceBet(host, RouletteBetKind.Even, 0, 10);
                    Debug.Log($"[Freelancer] ROULETTE TEST round {s_Round + 1}: took back ${refund} from ODD, money now {CrewState.Current.Money} (expected {s_MoneyBefore - 85})");
                    Next(4);
                    break;
                case 4 when T > 7:
                    if (s_Round == 0) Shot("betting");
                    Next(5);
                    break;
                case 5 when T > 8:
                    if (s_Round == 0) Place(new Vector3(-22.45f, 0.05f, -24.15f), 180f, 34f); // close up on the wheel
                    Debug.Log($"[Freelancer] ROULETTE TEST spin from the wheel: {table.ServerSpin(host)} by={table.SpunBy}");
                    s_SpinAt = EditorApplication.timeSinceStartup;
                    Next(6);
                    break;
                case 6 when SinceSpin > 1.5:
                    if (s_Round == 0) Shot("spin-fast");
                    Next(7);
                    break;
                case 7 when SinceSpin > 4.4:
                    if (s_Round == 0) Shot("spin-slow");
                    Next(8);
                    break;
                case 8 when table != null && table.Phase == RoulettePhase.Payout:
                    s_SpinAt = EditorApplication.timeSinceStartup;
                    Next(9);
                    break;
                case 9 when SinceSpin > 1.2:
                    if (s_Round == 0) Shot("result");
                    int r = table.Result;
                    int expected = s_MoneyBefore - 85 +
                                   (RouletteRules.IsRed(r) ? 100 : 0) + (r == 17 ? 900 : 0) + (r > 0 && r % 2 == 0 ? 20 : 0);
                    Debug.Log($"[Freelancer] ROULETTE TEST round {s_Round + 1} result={RouletteRules.Describe(r)} before={s_MoneyBefore} " +
                              $"after={CrewState.Current.Money} expected={expected} {(expected == CrewState.Current.Money ? "OK" : "MISMATCH")} " +
                              $"staked={table.LastStaked} returned={table.LastReturned}");
                    Next(10);
                    break;
                case 10 when table != null && table.Phase == RoulettePhase.Betting:
                    s_Round++;
                    if (s_Round < Rounds)
                    {
                        s_StartedAt = EditorApplication.timeSinceStartup - 5.5;
                        Next(3);
                        break;
                    }
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
