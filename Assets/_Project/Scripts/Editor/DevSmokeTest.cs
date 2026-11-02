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

        [MenuItem("Freelancer/Setup/Dev/Refresh Assets", priority = 101)]
        static void RefreshAssets() => AssetDatabase.Refresh();

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
            else if (stage == 2 && EditorApplication.timeSinceStartup - s_StartedAt > 4)
            {
                // Host lands in the apartment; take the "computer" route to the cake gig.
                LogState("in hub");
                SessionManager.Instance.StartGig(GigCatalog.All[0]);
                SessionState.SetInt(Key, 10);
            }
            else if (stage == 10 && EditorApplication.timeSinceStartup - s_StartedAt > 16)
            {
                LogState("after briefing");
                foreach (var p in Object.FindObjectsByType<PlayerKnockdown>())
                    p.KnockDown(p.transform.forward, 8f);
                // Cheat: drop the cake onto the table and see if the gig completes.
                var cake = CakeCondition.Current;
                var zone = DeliveryZone.Current;
                if (cake != null && zone != null)
                {
                    // Also exercise the damage path: 70 damage should knock off two tiers.
                    cake.ApplyDamage(70f);
                    cake.Body.position = zone.transform.position + Vector3.down * 0.42f;
                    cake.Body.rotation = Quaternion.identity;
                    cake.Body.linearVelocity = Vector3.zero;
                    cake.Body.angularVelocity = Vector3.zero;
                    cake.transform.SetPositionAndRotation(cake.Body.position, Quaternion.identity);
                }
                SessionState.SetInt(Key, 3);
            }
            else if (stage == 3 && EditorApplication.timeSinceStartup - s_StartedAt > 17.5)
            {
                LogState("mid knockdown");
                SessionState.SetInt(Key, 4);
            }
            else if (stage == 4 && EditorApplication.timeSinceStartup - s_StartedAt > 22)
            {
                LogState("after delivery");
                ScreenCapture.CaptureScreenshot("Temp/results-capture.png");
                SessionState.SetInt(Key, 6);
            }
            else if (stage == 6 && EditorApplication.timeSinceStartup - s_StartedAt > 23)
            {
                SessionManager.Instance.ReturnToHub();
                SessionState.SetInt(Key, 5);
            }
            else if (stage == 5 && EditorApplication.timeSinceStartup - s_StartedAt > 26)
            {
                LogState("back in hub");
                SessionState.SetInt(Key, 0);
                EditorApplication.isPlaying = false;
            }
        }

        static void LogState(string label)
        {
            var nm = NetworkManager.Singleton;
            var spawned = nm != null && nm.SpawnManager != null
                ? nm.SpawnManager.SpawnedObjectsList.Select(o => o.name.Replace("(Clone)", "")).ToList()
                : new System.Collections.Generic.List<string>();
            string summary = string.Join(", ", spawned.GroupBy(n => n).Select(g => $"{g.Key} x{g.Count()}"));
            var gig = GigDirector.Current;
            var cake = CakeCondition.Current;
            string gigText = gig == null ? "none" :
                $"{gig.Phase} timeLeft={gig.TimeLeft:0.0} stars={gig.Stars} review={gig.ReviewIndex} fail={gig.FailReason}";
            string cakeText = cake == null ? "none" :
                $"{cake.Condition:0}% tiers={cake.TiersLeft} pos={cake.transform.position}";
            string playerText = "";
            foreach (var p in Object.FindObjectsByType<PlayerKnockdown>())
            {
                var rag = p.GetComponent<PlayerRagdoll>();
                playerText += $"down={p.IsDown} ragdoll={p.IsRagdolled} root={p.transform.position} hips={rag.Hips.position} ";
            }
            var crew = CrewState.Current;
            string crewText = crew == null ? "none" :
                $"money={crew.Money} earned={crew.TotalEarned} best={crew.BestStars(GigCatalog.All[0].Id)}" +
                (gig != null ? $" pay={gig.Pay} tip={gig.Tip} newBest={gig.NewBest}" : "");
            Debug.Log($"[Freelancer] SMOKE {label}: scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                      $"crew=[{crewText}] host={nm != null && nm.IsHost} spawned=[{summary}] gig=[{gigText}] cake=[{cakeText}] player=[{playerText}]");
        }

        [MenuItem("Freelancer/Setup/Dev/Reset Crew Save", priority = 110)]
        static void ResetCrewSave()
        {
            if (System.IO.File.Exists(CrewSave.FilePath)) System.IO.File.Delete(CrewSave.FilePath);
            Debug.Log("[Freelancer] Crew save deleted: " + CrewSave.FilePath);
        }
    }
}
