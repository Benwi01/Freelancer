using System.Collections.Generic;
using UnityEngine;

namespace Freelancer
{
    /// <summary>Scene marker: a spot at the bar where a guest can stand and order. Faces the bar.</summary>
    public class GuestSlot : MonoBehaviour
    {
        public static readonly List<GuestSlot> All = new();

        /// <summary>Server: who is standing here.</summary>
        public BarGuest Occupant { get; set; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.2f, transform.position + Vector3.up * 1.2f + transform.forward);
        }
    }
}
