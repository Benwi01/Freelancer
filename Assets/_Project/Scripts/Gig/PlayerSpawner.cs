using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Server: spawns a player object for everyone in the session when the gig scene starts,
    /// and for anyone who joins later. Spawn points are this object's children.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject playerPrefab;

        int m_NextPoint;

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            nm.OnConnectionEvent += OnConnectionEvent;
            foreach (ulong id in nm.ConnectedClientsIds)
                SpawnFor(id);
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }

        void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType == ConnectionEvent.ClientConnected && nm.IsServer)
                SpawnFor(data.ClientId);
        }

        void SpawnFor(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                return;

            Transform point = transform.childCount > 0
                ? transform.GetChild(m_NextPoint++ % transform.childCount)
                : transform;
            var player = Instantiate(playerPrefab, point.position, point.rotation);
            player.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            foreach (Transform t in transform)
            {
                Gizmos.DrawWireSphere(t.position + Vector3.up * 0.9f, 0.4f);
                Gizmos.DrawLine(t.position + Vector3.up * 1.6f, t.position + Vector3.up * 1.6f + t.forward * 0.6f);
            }
        }
    }
}
