using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// The NetworkRoot (NetworkManager + SessionManager) lives in the MainMenu scene and is kept
    /// across scene loads. When we return to the menu a second copy loads; this destroys it
    /// before its NetworkManager registers itself.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class NetworkRootGuard : MonoBehaviour
    {
        static NetworkRootGuard s_Kept;

        void Awake()
        {
            if (s_Kept != null && s_Kept != this)
            {
                // Deactivate first so the duplicate NetworkManager never wakes up.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            s_Kept = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (s_Kept == this) s_Kept = null;
        }

        /// <summary>True if a NetworkManager is running a session right now.</summary>
        public static bool IsOnline =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
