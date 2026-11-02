using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Slapstick knockdown: the server decides a player got hit, the owner goes ragdoll for a moment
    /// and gets back up. Every machine runs the ragdoll locally; the owner's player root follows
    /// their ragdoll's hips (and is synced), and the owner gets a third-person view of the flop.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerMotor, so the camera override wins
    public class PlayerKnockdown : NetworkBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float downTime = 2.5f;
        [SerializeField] float immunityAfter = 1f;
        [SerializeField] float cameraDistance = 3.2f;

        // Push is declared first so it arrives before (or with) the down flag.
        readonly NetworkVariable<Vector3> m_Push = new(Vector3.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> m_Down = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerMotor m_Motor;
        PlayerBody m_Body;
        PlayerRagdoll m_Ragdoll;
        float m_UpAt;
        float m_ImmuneUntil;
        float m_ServerImmuneUntil;
        bool m_Applied;

        Vector3 m_CameraPivotRest;
        float m_CamBlend;
        Vector3 m_Focus;
        readonly RaycastHit[] m_Hits = new RaycastHit[16];

        public bool IsDown => m_Down.Value;
        public bool IsRagdolled => m_Ragdoll != null && m_Ragdoll.IsActive;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            m_Body = GetComponent<PlayerBody>();
            m_Ragdoll = GetComponent<PlayerRagdoll>();
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

            m_Push.Value = direction * Mathf.Clamp(strength, 3f, 7f);
            m_Down.Value = true;
            m_UpAt = Time.time + downTime;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner && m_Down.Value)
            {
                bool fellOut = m_Ragdoll.IsActive && m_Ragdoll.Hips.position.y < -20f;
                if (Time.time >= m_UpAt || fellOut)
                {
                    m_Down.Value = false;
                    m_ImmuneUntil = Time.time + immunityAfter;
                    ApplyState();
                    if (fellOut) m_Motor.Respawn();
                }
            }
            ApplyState();
        }

        /// <summary>Start or stop the local ragdoll to match the synced flag (every machine).</summary>
        void ApplyState()
        {
            if (m_Applied == m_Down.Value) return;
            m_Applied = m_Down.Value;

            if (m_Applied)
            {
                Vector3 inherit = m_Body != null ? m_Body.Velocity : Vector3.zero;
                m_Ragdoll.Activate(inherit, m_Push.Value + Vector3.up * 2.5f, followRoot: !IsOwner);
                m_Focus = m_Ragdoll.Hips.position;
            }
            else
            {
                if (IsOwner) FollowHips();
                m_Ragdoll.Deactivate();
            }
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || cameraPivot == null) return;

            if (m_Ragdoll.IsActive) FollowHips();

            m_CamBlend = Mathf.MoveTowards(m_CamBlend, m_Ragdoll.IsActive ? 1f : 0f, Time.deltaTime / 0.35f);
            m_Motor.SetOwnBodyVisible(m_CamBlend > 0.4f);

            if (m_CamBlend <= 0f)
            {
                cameraPivot.localPosition = m_CameraPivotRest;
                return;
            }

            // Third-person orbit around your own ragdoll; the mouse still turns the view.
            Vector3 restPos = transform.TransformPoint(m_CameraPivotRest);
            Quaternion restRot = cameraPivot.rotation; // PlayerMotor set the pitch this frame

            Vector3 focusTarget = m_Ragdoll.IsActive
                ? m_Ragdoll.Hips.position + Vector3.up * 0.3f
                : transform.position + Vector3.up * 1.2f;
            m_Focus = Vector3.Lerp(m_Focus, focusTarget, 1f - Mathf.Exp(-15f * Time.deltaTime));

            Quaternion orbit = Quaternion.Euler(Mathf.Clamp(m_Motor.Pitch + 20f, -10f, 75f), transform.eulerAngles.y, 0f);
            Vector3 desired = ClipCamera(m_Focus, m_Focus + orbit * Vector3.back * cameraDistance);

            float e = m_CamBlend * m_CamBlend * (3f - 2f * m_CamBlend);
            cameraPivot.position = Vector3.Lerp(restPos, desired, e);
            cameraPivot.rotation = Quaternion.Slerp(restRot, Quaternion.LookRotation(m_Focus - desired), e);
        }

        /// <summary>Owner: keep the (invisible) player root standing on the ground under the ragdoll.</summary>
        void FollowHips()
        {
            Vector3 hips = m_Ragdoll.Hips.position;
            float groundY = hips.y - 0.3f;
            float best = float.MaxValue;
            int n = Physics.RaycastNonAlloc(hips + Vector3.up * 0.2f, Vector3.down, m_Hits, 3f, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (m_Ragdoll.Owns(m_Hits[i].collider) || m_Hits[i].distance >= best) continue;
                best = m_Hits[i].distance;
                groundY = m_Hits[i].point.y;
            }
            transform.position = new Vector3(hips.x, groundY, hips.z);
        }

        Vector3 ClipCamera(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return to;
            dir /= dist;
            float best = dist;
            int n = Physics.SphereCastNonAlloc(from, 0.2f, dir, m_Hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = m_Hits[i];
                if (h.distance <= 0f || m_Ragdoll.Owns(h.collider)) continue;
                if (h.collider.GetComponentInParent<PlayerMotor>() != null) continue;
                best = Mathf.Min(best, h.distance);
            }
            return from + dir * best;
        }
    }
}
