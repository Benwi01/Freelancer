using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Freelancer
{
    /// <summary>
    /// Creates and joins Multiplayer Services sessions over Relay.
    /// The Sessions SDK starts Netcode (host or client) for us once a session is created or joined.
    /// Lives on the NetworkRoot object next to the NetworkManager and survives scene loads.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public const int MaxPlayers = 4;

        public static SessionManager Instance { get; private set; }

        [Tooltip("The crew's apartment: where the host goes once the session is up, and where gigs return to.")]
        [SerializeField] string hubSceneName = "Hub";
        [SerializeField] string menuSceneName = "MainMenu";
        [Tooltip("Spawned by the host when a session starts; holds the crew's money and best ratings.")]
        [SerializeField] NetworkObject crewStatePrefab;

        public ISession ActiveSession { get; private set; }
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "";

        public event Action StatusChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // Covers local (no Relay) games too: if the connection drops, go back to the menu.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped += OnClientStopped;
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            }
        }

        void OnServerStarted()
        {
            if (crewStatePrefab == null) return;
            // Lives for the whole session, across every scene load.
            var crew = Instantiate(crewStatePrefab);
            crew.Spawn(destroyWithScene: false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            }
        }

        void OnClientStopped(bool wasHost)
        {
            if (!m_Leaving && SceneManager.GetActiveScene().name != menuSceneName)
                _ = LeaveAsync();
        }

        bool m_Leaving;

        void SetStatus(string text)
        {
            Status = text;
            StatusChanged?.Invoke();
        }

        async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                options.SetProfile(GetProfileName());
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Each Multiplayer Play Mode player runs from its own project folder, so hashing the
        /// data path gives every virtual player its own anonymous account. Builds get a random
        /// profile so two copies on one machine can still join each other.
        /// </summary>
        static string GetProfileName()
        {
#if UNITY_EDITOR
            return "ed" + ((uint)Application.dataPath.GetHashCode()).ToString("x8");
#else
            return "pl" + UnityEngine.Random.Range(0, int.MaxValue).ToString("x8");
#endif
        }

        public async Task HostAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                SetStatus("Signing in...");
                await EnsureSignedInAsync();

                SetStatus("Creating session...");
                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                ActiveSession.RemovedFromSession += OnRemovedFromSession;

                SetStatus($"Session up. Code: {ActiveSession.Code}");
                NetworkManager.Singleton.SceneManager.LoadScene(hubSceneName, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus(DescribeError(e));
                ActiveSession = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task JoinAsync(string code)
        {
            if (IsBusy) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetStatus("Type a join code first.");
                return;
            }

            IsBusy = true;
            try
            {
                SetStatus("Signing in...");
                await EnsureSignedInAsync();

                SetStatus($"Joining {code}...");
                ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                ActiveSession.RemovedFromSession += OnRemovedFromSession;
                SetStatus("Joined. Waiting for host...");
                // The host's NetworkSceneManager moves us into the gig scene automatically.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus(DescribeError(e));
                ActiveSession = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Host without Relay on 127.0.0.1 (no Unity Cloud needed). Handy for quick tests with
        /// Multiplayer Play Mode on one machine.
        /// </summary>
        public void HostLocal()
        {
            if (IsBusy || NetworkRootGuard.IsOnline) return;
            SetLocalAddress();
            if (NetworkManager.Singleton.StartHost())
            {
                SetStatus("Local host on 127.0.0.1:" + LocalPort);
                NetworkManager.Singleton.SceneManager.LoadScene(hubSceneName, LoadSceneMode.Single);
            }
            else SetStatus("Could not start local host (port in use?).");
        }

        public void JoinLocal()
        {
            if (IsBusy || NetworkRootGuard.IsOnline) return;
            SetLocalAddress();
            SetStatus(NetworkManager.Singleton.StartClient() ? "Connecting to local host..." : "Could not start client.");
        }

        const ushort LocalPort = 7777;

        public bool InHub => SceneManager.GetActiveScene().name == hubSceneName;

        /// <summary>Host only: take the whole crew to a gig.</summary>
        public void StartGig(GigDefinition gig)
        {
            var nm = NetworkManager.Singleton;
            if (gig == null || nm == null || !nm.IsServer) return;
            nm.SceneManager.LoadScene(gig.Scene, LoadSceneMode.Single);
        }

        /// <summary>Host only: everyone back to the apartment.</summary>
        public void ReturnToHub()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            nm.SceneManager.LoadScene(hubSceneName, LoadSceneMode.Single);
        }

        static void SetLocalAddress()
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData("127.0.0.1", LocalPort);
        }

        /// <summary>Leave the session, stop Netcode and go back to the main menu.</summary>
        public async Task LeaveAsync()
        {
            if (m_Leaving) return;
            m_Leaving = true;
            try { await LeaveInternalAsync(); }
            finally { m_Leaving = false; }
        }

        async Task LeaveInternalAsync()
        {
            var session = ActiveSession;
            ActiveSession = null;
            if (session != null)
            {
                session.RemovedFromSession -= OnRemovedFromSession;
                try { await session.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"Leaving session failed: {e.Message}"); }
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SetStatus("");
            if (SceneManager.GetActiveScene().name != menuSceneName)
                SceneManager.LoadScene(menuSceneName);
        }

        void OnRemovedFromSession()
        {
            // Host left or we were kicked: bail out to the menu.
            _ = LeaveAsync();
        }

        static string DescribeError(Exception e)
        {
            string msg = e.Message;
            if (msg.Contains("project", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("link", StringComparison.OrdinalIgnoreCase))
                return "Not linked to a Unity Cloud project (Project Settings > Services).";
            return "Failed: " + msg;
        }
    }
}
