using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Slapstick knockdown: the server decides a player got hit, the owner falls over for a moment
    /// (body tips over, camera drops to the floor) and gets back up. Everyone sees the tip-over.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerMotor, so the camera roll isn't overwritten
    public class PlayerKnockdown : NetworkBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform cameraPivot;
        [SerializeField] float downTime = 2.2f;
        [SerializeField] float immunityAfter = 1f;

        readonly NetworkVariable<bool> m_Down = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> m_FallDir = new(Vector3.forward,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerMotor m_Motor;
        float m_UpAt;
        float m_ImmuneUntil;
        float m_Tilt; // 0 = standing, 1 = flat
        Vector3 m_CameraPivotRest;
        float m_ServerImmuneUntil;

        public bool IsDown => m_Down.Value;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            if (cameraPivot != null) m_CameraPivotRest = cameraPivot.localPosition;
        }

        /// <summary>Server: knock this player over, pushed along <paramref name="direction"/>.</summary>
        public void KnockDown(Vector3 direction, float strength)
        {
            if (!IsServer || Time.time < m_ServerImmuneUntil) return;
            m_ServerImmuneUntil = Time.time + downTime + immunityAfter;
            GrabbableObject.ReleaseAllFor(OwnerClientId);
            KnockDownRpc(direction, strength);
        }

        [Rpc(SendTo.Owner)]
        void KnockDownRpc(Vector3 direction, float strength)
        {
            if (m_Down.Value || Time.time < m_ImmuneUntil) return;

            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
            direction.Normalize();

            m_FallDir.Value = direction;
            m_Down.Value = true;
            m_UpAt = Time.time + downTime;
            m_Motor.AddImpulse(direction * Mathf.Clamp(strength, 2f, 8f) + Vector3.up * 2.5f);
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner && m_Down.Value && Time.time >= m_UpAt)
            {
                m_Down.Value = false;
                m_ImmuneUntil = Time.time + immunityAfter;
            }

            // Visual tip-over, same on every machine.
            float target = m_Down.Value ? 1f : 0f;
            m_Tilt = Mathf.MoveTowards(m_Tilt, target, Time.deltaTime * (m_Down.Value ? 5f : 2.5f));
            float eased = m_Tilt * m_Tilt * (3f - 2f * m_Tilt);

            Vector3 localFall = transform.InverseTransformDirection(m_FallDir.Value);
            Vector3 axis = Vector3.Cross(Vector3.up, localFall);
            if (axis.sqrMagnitude < 0.001f) axis = Vector3.right;
            Quaternion tip = Quaternion.AngleAxis(85f * eased, axis.normalized);

            if (body != null) body.localRotation = tip;
            if (IsOwner && cameraPivot != null)
            {
                // Drop the camera to the floor and roll it a bit.
                Vector3 downPos = localFall * 1.2f + Vector3.up * 0.35f;
                cameraPivot.localPosition = Vector3.Lerp(m_CameraPivotRest, downPos, eased);
                cameraPivot.localRotation *= Quaternion.Euler(0, 0, 40f * eased);
            }
        }
    }
}
