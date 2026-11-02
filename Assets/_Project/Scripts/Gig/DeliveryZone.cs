using UnityEngine;

namespace Freelancer
{
    /// <summary>Scene marker for "place cake here": a box volume sitting on top of the table.</summary>
    public class DeliveryZone : MonoBehaviour
    {
        public static DeliveryZone Current { get; private set; }

        [SerializeField] Vector3 size = new(1.6f, 1.4f, 1.1f);

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
