using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>Scene marker: on the server, keeps a few clean glasses on the rack.</summary>
    public class GlassRack : MonoBehaviour
    {
        [SerializeField] NetworkObject glassPrefab;
        [SerializeField] int keepOnRack = 4;
        [SerializeField] int maxGlassesInScene = 16;
        [SerializeField] float interval = 2f;
        [SerializeField] Vector2 rackSize = new(0.6f, 0.3f);

        float m_Next;

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || glassPrefab == null || Time.time < m_Next) return;
            m_Next = Time.time + interval;

            int onRack = 0;
            foreach (var g in DrinkGlass.All)
            {
                Vector3 local = transform.InverseTransformPoint(g.transform.position);
                if (Mathf.Abs(local.x) < rackSize.x && Mathf.Abs(local.z) < rackSize.y && Mathf.Abs(local.y) < 0.4f) onRack++;
            }
            if (onRack >= keepOnRack || DrinkGlass.All.Count >= maxGlassesInScene) return;

            Vector3 pos = transform.TransformPoint(new Vector3(
                Random.Range(-rackSize.x, rackSize.x) * 0.8f, 0.05f, Random.Range(-rackSize.y, rackSize.y) * 0.6f));
            var glass = Instantiate(glassPrefab, pos, transform.rotation);
            glass.Spawn(destroyWithScene: true);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(rackSize.x * 2f, 0.2f, rackSize.y * 2f));
        }
    }
}
