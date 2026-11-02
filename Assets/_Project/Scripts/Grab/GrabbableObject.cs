using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A server-owned physics object any number of players can hold at once.
    /// Each holder pulls their grab point toward their "hand" (a point in front of their eyes)
    /// with a capped spring force, so heavy things need teamwork and tug-of-war just happens.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableObject : NetworkBehaviour
    {
        static readonly List<GrabbableObject> s_All = new();

        [SerializeField] string displayName = "thing";

        [Header("Spring (per holder)")]
        [Tooltip("Pull strength, as acceleration per metre of stretch.")]
        [SerializeField] float stiffness = 140f;
        [SerializeField] float damping = 16f;
        [Tooltip("Hard cap on one player's pull in newtons. Heavy objects need more than one player.")]
        [SerializeField] float maxForcePerHolder = 380f;
        [SerializeField] float breakDistance = 2.4f;

        [Header("Throw")]
        [SerializeField] float throwSpeed = 11f;
        [SerializeField] float maxThrowImpulse = 70f;

        [Header("While held")]
        [SerializeField] float heldAngularDamping = 3f;

        readonly NetworkList<GrabHolder> m_Holders = new();
        readonly Dictionary<ulong, float> m_StretchedSince = new();

        Rigidbody m_Body;
        float m_RestAngularDamping;

        public string DisplayName => displayName;
        public NetworkList<GrabHolder> Holders => m_Holders;
        public Rigidbody Body => m_Body;
        public bool IsHeld => m_Holders.Count > 0;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_RestAngularDamping = m_Body.angularDamping;
        }

        public override void OnNetworkSpawn() => s_All.Add(this);

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
            m_StretchedSince.Clear();
        }

        public bool IsHeldBy(ulong clientId)
        {
            foreach (var h in m_Holders)
                if (h.ClientId == clientId) return true;
            return false;
        }

        /// <summary>Server: drop whatever this client is holding (e.g. when they get knocked over).</summary>
        public static void ReleaseAllFor(ulong clientId)
        {
            foreach (var g in s_All)
                if (g.IsServer) g.RemoveHolder(clientId);
        }

        public void AddHolder(ulong clientId, Vector3 localAnchor, float distance)
        {
            if (!IsServer) return;
            // A player can only hold one thing at a time.
            ReleaseAllFor(clientId);
            m_Holders.Add(new GrabHolder { ClientId = clientId, LocalAnchor = localAnchor, Distance = distance });
            m_Body.WakeUp();
        }

        public void RemoveHolder(ulong clientId)
        {
            if (!IsServer) return;
            for (int i = m_Holders.Count - 1; i >= 0; i--)
                if (m_Holders[i].ClientId == clientId) m_Holders.RemoveAt(i);
            m_StretchedSince.Remove(clientId);
        }

        public void Throw(ulong clientId, Vector3 direction)
        {
            if (!IsServer || !IsHeldBy(clientId)) return;
            RemoveHolder(clientId);
            float impulse = Mathf.Min(throwSpeed * m_Body.mass, maxThrowImpulse);
            m_Body.AddForce(direction.normalized * impulse, ForceMode.Impulse);
            m_Body.AddTorque(Random.insideUnitSphere * impulse * 0.05f, ForceMode.Impulse);
        }

        /// <summary>World-space point this holder's hand is reaching for.</summary>
        /// <summary>World point a player is holding something at (any machine). Used to aim their arms.</summary>
        public static bool TryFindGrip(ulong clientId, out Vector3 point)
        {
            foreach (var g in s_All)
            {
                if (g == null || !g.IsSpawned) continue;
                foreach (var h in g.m_Holders)
                {
                    if (h.ClientId != clientId) continue;
                    point = g.transform.TransformPoint(h.LocalAnchor);
                    return true;
                }
            }
            point = default;
            return false;
        }

        public static bool TryGetHandPoint(GrabHolder holder, out Vector3 hand)
        {
            if (PlayerMotor.ByClientId.TryGetValue(holder.ClientId, out var player) && player != null)
            {
                hand = player.EyePosition + player.LookDirection * holder.Distance;
                return true;
            }
            hand = default;
            return false;
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            m_Body.angularDamping = IsHeld ? heldAngularDamping : m_RestAngularDamping;

            for (int i = m_Holders.Count - 1; i >= 0; i--)
            {
                var holder = m_Holders[i];
                if (!TryGetHandPoint(holder, out Vector3 hand))
                {
                    m_Holders.RemoveAt(i); // player left
                    continue;
                }

                Vector3 anchor = transform.TransformPoint(holder.LocalAnchor);
                Vector3 stretch = hand - anchor;

                // Let go if it's been yanked out of your hands for a moment.
                if (stretch.magnitude > breakDistance)
                {
                    if (!m_StretchedSince.TryGetValue(holder.ClientId, out float since))
                        m_StretchedSince[holder.ClientId] = Time.time;
                    else if (Time.time - since > 0.4f)
                    {
                        RemoveHolder(holder.ClientId);
                        continue;
                    }
                }
                else
                {
                    m_StretchedSince.Remove(holder.ClientId);
                }

                Vector3 pointVelocity = m_Body.GetPointVelocity(anchor);
                // Scale by mass (but only up to a point) so light things feel snappy and heavy
                // things hit the force cap and sag.
                float massScale = Mathf.Min(m_Body.mass, 25f);
                Vector3 force = (stretch * stiffness - pointVelocity * damping) * massScale;
                force = Vector3.ClampMagnitude(force, maxForcePerHolder);
                m_Body.AddForceAtPosition(force, anchor, ForceMode.Force);
            }
        }
    }
}
