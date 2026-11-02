using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Scene marker: on the server, spawns one networked prefab here when the scene starts.
    /// Keeps scenes free of in-scene NetworkObjects, which makes reloading a gig trivial.
    /// </summary>
    public class ServerSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject prefab;

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || prefab == null) return;

            var instance = Instantiate(prefab, transform.position, transform.rotation);
            instance.Spawn(destroyWithScene: true);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one);
        }
    }
}
