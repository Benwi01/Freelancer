using System.Collections.Generic;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Turns the jointed body into a physics ragdoll and back. Every machine simulates its own copy
    /// (it's cosmetic); remote copies are gently pulled toward the owner's synced position so they
    /// don't drift apart. While active the body is unparented so moving the player root doesn't
    /// teleport the simulated parts.
    /// </summary>
    [DefaultExecutionOrder(60)] // after PlayerBody, so the get-up blend wins
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] Transform bodyRoot;
        [Tooltip("Ragdoll parts, parent first. The first one is the hips.")]
        [SerializeField] Rigidbody[] parts;
        [SerializeField] float getUpBlendTime = 0.4f;

        readonly HashSet<Collider> m_Colliders = new();
        Collider[] m_PartColliders;
        CharacterController m_Controller;
        Vector3 m_HipsRestPos;
        Quaternion m_HipsRestRot;
        bool m_FollowRoot;

        Vector3[] m_FromPos;
        Quaternion[] m_FromRot;
        float m_Blend = 1f;

        public bool IsActive { get; private set; }
        public Transform Hips => parts[0].transform;
        public bool Owns(Collider c) => m_Colliders.Contains(c);

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_PartColliders = new Collider[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                m_PartColliders[i] = parts[i].GetComponent<Collider>();
                m_Colliders.Add(m_PartColliders[i]);
            }
            m_FromPos = new Vector3[parts.Length];
            m_FromRot = new Quaternion[parts.Length];
            m_HipsRestPos = Hips.localPosition;
            m_HipsRestRot = Hips.localRotation;
            SetPhysics(false);
        }

        void OnDestroy()
        {
            // The hips live outside the player while ragdolling; take them with us.
            if (IsActive && parts.Length > 0 && parts[0] != null) Destroy(parts[0].gameObject);
        }

        /// <param name="inheritVelocity">How fast the player was moving.</param>
        /// <param name="impulse">Extra velocity from whatever hit them.</param>
        /// <param name="followRoot">True on remote copies: steer toward the synced position.</param>
        public void Activate(Vector3 inheritVelocity, Vector3 impulse, bool followRoot)
        {
            if (IsActive) return;
            IsActive = true;
            m_FollowRoot = followRoot;
            m_Blend = 1f;

            m_Controller.enabled = false; // the ragdoll parts are the collision now
            Hips.SetParent(null, true);
            SetPhysics(true);

            for (int i = 0; i < parts.Length; i++)
            {
                // Limbs flail a bit more than the core.
                float limb = i < 3 ? 1f : 1.25f;
                parts[i].linearVelocity = inheritVelocity + impulse * limb;
            }
            Vector3 spinAxis = Vector3.Cross(Vector3.up, impulse.sqrMagnitude > 0.01f ? impulse.normalized : transform.forward);
            parts[1].angularVelocity = spinAxis * 5f + Random.insideUnitSphere * 2f;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            for (int i = 0; i < parts.Length; i++)
            {
                m_FromPos[i] = parts[i].transform.position;
                m_FromRot[i] = parts[i].transform.rotation;
            }

            SetPhysics(false);
            Hips.SetParent(bodyRoot, false);
            Hips.localPosition = m_HipsRestPos;
            Hips.localRotation = m_HipsRestRot;
            m_Controller.enabled = true;
            m_Blend = 0f;
        }

        void SetPhysics(bool on)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var rb = parts[i];
                rb.isKinematic = !on;
                rb.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                if (m_PartColliders[i] != null) m_PartColliders[i].enabled = on;
            }
            if (!on) return;

            // Parts don't collide with each other (keeps it stable); joints hold them together.
            for (int a = 0; a < m_PartColliders.Length; a++)
            for (int b = a + 1; b < m_PartColliders.Length; b++)
                if (m_PartColliders[a] != null && m_PartColliders[b] != null)
                    Physics.IgnoreCollision(m_PartColliders[a], m_PartColliders[b]);
        }

        void FixedUpdate()
        {
            if (!IsActive || !m_FollowRoot) return;

            // Remote copy: nudge the hips toward where the owner says the body is.
            var hips = parts[0];
            Vector3 offset = transform.position - hips.position;
            offset.y = 0f;
            if (offset.magnitude < 0.25f) return;
            Vector3 vel = hips.linearVelocity;
            vel.y = 0f;
            hips.AddForce(offset * 30f - vel * 5f, ForceMode.Acceleration);
        }

        void LateUpdate()
        {
            if (m_Blend >= 1f) return;

            // Get up: blend from the ragdoll pose to the animated pose.
            m_Blend = Mathf.Min(1f, m_Blend + Time.deltaTime / getUpBlendTime);
            float t = m_Blend * m_Blend * (3f - 2f * m_Blend);

            var toPos = new Vector3[parts.Length];
            var toRot = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                toPos[i] = parts[i].transform.position;
                toRot[i] = parts[i].transform.rotation;
            }
            for (int i = 0; i < parts.Length; i++)
                parts[i].transform.SetPositionAndRotation(
                    Vector3.Lerp(m_FromPos[i], toPos[i], t),
                    Quaternion.Slerp(m_FromRot[i], toRot[i], t));
        }
    }
}
