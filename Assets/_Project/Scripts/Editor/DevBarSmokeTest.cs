using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Automated run of the Reception Bar gig: host locally, start the gig, pour at the tap,
    /// serve a guest, start the conga line, end the shift early, then go back to the hub.
    /// Logs state and saves screenshots to Temp/bar-*.png.
    /// </summary>
    [InitializeOnLoad]
    static class DevBarSmokeTest
    {
        const string Key = "Freelancer.BarSmokeTest.Stage";
        static double s_StartedAt;

        static DevBarSmokeTest() => EditorApplication.update += Tick;

        [MenuItem("Freelancer/Setup/Dev/Smoke Test Bar (local host)", priority = 102)]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
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

            switch (stage)
            {
                case 1 when SessionManager.Instance != null && Time.realtimeSinceStartup > 1f:
                    SessionManager.Instance.HostLocal();
                    s_StartedAt = EditorApplication.timeSinceStartup;
                    Next(2);
                    break;
                case 2 when T > 4:
                    SessionManager.Instance.StartGig(GigCatalog.ById("reception-bar"));
                    Next(3);
                    break;
                case 3 when T > 17:
                    Log("running");
                    PourAndServe();
                    Next(4);
                    break;
                case 4 when T > 19:
                    Log("after pour");
                    ScreenCapture.CaptureScreenshot("Temp/bar-play.png");
                    Next(5);
                    break;
                case 5 when T > 20:
                    StartConga();
                    Next(6);
                    break;
                case 6 when T > 25:
                    Log("conga");
                    ScreenCapture.CaptureScreenshot("Temp/bar-conga.png");
                    Next(7);
                    break;
                case 7 when T > 26:
                    SetNetVar(GigDirector.Current, "m_TimeLeft", 0.05f); // close the bar early
                    Next(8);
                    break;
                case 8 when T > 28:
                    Log("results");
                    ScreenCapture.CaptureScreenshot("Temp/bar-results.png");
                    Next(9);
                    break;
                case 9 when T > 29:
                    SessionManager.Instance.ReturnToHub();
                    Next(10);
                    break;
                case 10 when T > 32:
                    Log("back in hub");
                    Next(0);
                    EditorApplication.isPlaying = false;
                    break;
            }
        }

        static void PourAndServe()
        {
            var glassPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.GlassPath);

            // A glass under the beer tap, and turn the tap on.
            foreach (var tap in DrinkTap.All)
            {
                if (!tap.UsesKeg) continue;
                var spout = tap.transform.TransformPoint(new Vector3(0, 0.03f, 0.15f));
                var g = Object.Instantiate(glassPrefab, spout, Quaternion.identity);
                g.Spawn(destroyWithScene: true);
                typeof(DrinkTap).GetMethod("ToggleRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(tap, new object[] { });
            }

            // A full glass of the right drink, set down in front of the first waiting guest.
            foreach (var guest in BarGuest.All)
            {
                if (guest.Mood != GuestMood.Waiting) continue;
                var pos = guest.transform.TransformPoint(new Vector3(0, 1.06f, 0.95f));
                var g = Object.Instantiate(glassPrefab, pos, guest.transform.rotation);
                g.Spawn(destroyWithScene: true);
                g.GetComponent<DrinkGlass>().AddDrink(guest.Order, 0.9f);
                break;
            }
        }

        static void StartConga()
        {
            var conga = CongaLine.Current;
            if (conga == null) return;
            SetNetVar(conga, "m_StartedAt", NetworkManager.Singleton.ServerTime.Time);
            SetNetVar(conga, "m_Dancing", true);
        }

        static void SetNetVar<T>(object owner, string field, T value)
        {
            if (owner == null) return;
            var f = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f?.GetValue(owner) is NetworkVariable<T> v) v.Value = value;
        }

        static void Log(string label)
        {
            var gig = GigDirector.Current;
            var bar = BarObjective.Current;
            string taps = "";
            foreach (var tap in DrinkTap.All)
                taps += $"{tap.Drink}(pouring={tap.IsPouring} level={tap.Level:0.00}) ";
            string glasses = "";
            foreach (var g in DrinkGlass.All)
                glasses += $"{g.Contents}:{g.Fill:0.00}{(g.Sticky ? "*" : "")} ";
            string guests = "";
            foreach (var g in BarGuest.All)
                guests += $"{g.Order}/{g.Mood}/{g.Patience01:0.00} ";
            string players = "";
            foreach (var p in Object.FindObjectsByType<PlayerKnockdown>())
                players += $"down={p.IsDown} pos={p.transform.position} ";
            var crew = CrewState.Current;
            Debug.Log($"[Freelancer] BAR SMOKE {label}: scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                      $"gig=[{(gig == null ? "none" : $"{gig.Phase} left={gig.TimeLeft:0} stars={gig.Stars} pay={gig.Pay} tip={gig.Tip} fail={gig.FailReason}")}] " +
                      $"bar=[{(bar == null ? "none" : $"served={bar.Served} slid={bar.Slides} perfect={bar.Perfect} angry={bar.WalkedOff} smashed={bar.Smashed}")}] " +
                      $"conga=[{(CongaLine.Current == null ? "none" : CongaLine.Current.IsDancing.ToString())}] " +
                      $"taps=[{taps}] glasses=[{glasses}] guests=[{guests}] players=[{players}] money={(crew != null ? crew.Money : -1)}");
        }
    }
}
