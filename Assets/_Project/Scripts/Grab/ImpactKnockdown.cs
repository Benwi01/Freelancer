using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Server-side: when this physics object hits a player fast enough, knock them over.
    /// Collisions only happen on the server (clients see these bodies as kinematic).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ImpactKnockdown : MonoBehaviour
    {
        [SerializeField] float minSpeed = 5.5f;

        void OnCollisionEnter(Collision collision)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            var player = collision.collider.GetComponentInParent<PlayerKnockdown>();
            if (player == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;

            Vector3 dir = player.transform.position - transform.position;
            player.KnockDown(dir, speed * 0.6f);
        }
    }
}
