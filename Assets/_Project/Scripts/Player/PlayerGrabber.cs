using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Freelancer
{
    /// <summary>
    /// Owner input for grabbing: hold LMB on a physics object to grab it at the aimed point,
    /// release to drop, RMB to throw. The server does the actual physics (see GrabbableObject).
    /// </summary>
    public class PlayerGrabber : NetworkBehaviour
    {
        public const float MaxReach = 3.2f;

        [SerializeField] LayerMask grabMask = ~0;

        PlayerMotor m_Motor;
        PlayerKnockdown m_Knockdown;
        GrabbableObject m_Held;      // what we asked to hold (owner-side view)
        GrabbableObject m_Aimed;     // what the crosshair is over
        float m_GrabRequestedAt;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            m_Knockdown = GetComponent<PlayerKnockdown>();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            var mouse = Mouse.current;
            bool allowed = CursorLock.GameplayInputAllowed && !m_Knockdown.IsDown;

            m_Aimed = null;
            Ray ray = new Ray(m_Motor.Camera.transform.position, m_Motor.Camera.transform.forward);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, MaxReach, grabMask, QueryTriggerInteraction.Ignore);
            if (hitSomething) m_Aimed = hit.collider.GetComponentInParent<GrabbableObject>();

            // If the server dropped us (knockdown, too far), forget it locally.
            // (Wait a moment after asking so the server's answer has time to arrive.)
            if (m_Held != null && Time.time - m_GrabRequestedAt > 0.6f && !m_Held.IsHeldBy(OwnerClientId))
                m_Held = null;

            if (mouse == null) return;

            if (allowed && mouse.leftButton.wasPressedThisFrame && m_Aimed != null)
            {
                m_Held = m_Aimed;
                m_GrabRequestedAt = Time.time;
                Vector3 localAnchor = m_Aimed.transform.InverseTransformPoint(hit.point);
                float distance = Mathf.Clamp(hit.distance, 1.0f, MaxReach);
                RequestGrabRpc(m_Aimed.NetworkObject, localAnchor, distance);
            }

            if (m_Held != null && (!mouse.leftButton.isPressed || !allowed))
            {
                RequestReleaseRpc(m_Held.NetworkObject);
                m_Held = null;
            }

            if (m_Held != null && allowed && mouse.rightButton.wasPressedThisFrame)
            {
                RequestThrowRpc(m_Held.NetworkObject);
                m_Held = null;
            }
        }

        [Rpc(SendTo.Server)]
        void RequestGrabRpc(NetworkObjectReference target, Vector3 localAnchor, float distance)
        {
            if (!target.TryGet(out NetworkObject obj) || !obj.TryGetComponent(out GrabbableObject grab)) return;
            if (m_Knockdown.IsDown) return;

            // Don't trust the client blindly: the grab point must be roughly within reach.
            Vector3 worldAnchor = obj.transform.TransformPoint(localAnchor);
            if (Vector3.Distance(m_Motor.EyePosition, worldAnchor) > MaxReach + 1.5f) return;

            grab.AddHolder(OwnerClientId, localAnchor, Mathf.Clamp(distance, 1f, MaxReach));
        }

        [Rpc(SendTo.Server)]
        void RequestReleaseRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out GrabbableObject grab))
                grab.RemoveHolder(OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        void RequestThrowRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out NetworkObject obj) && obj.TryGetComponent(out GrabbableObject grab))
                grab.Throw(OwnerClientId, m_Motor.LookDirection);
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || !CursorLock.GameplayInputAllowed) return;

            // Crosshair: white dot, orange ring when something grabbable is in reach.
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            bool canGrab = m_Aimed != null && m_Held == null;
            float s = canGrab ? 14f : 6f;
            var old = GUI.color;
            GUI.color = m_Held != null ? new Color(0.4f, 1f, 0.4f) : canGrab ? new Color(1f, 0.6f, 0.2f) : Color.white;
            GUI.DrawTexture(new Rect(cx - s / 2, cy - s / 2, s, s), Texture2D.whiteTexture);
            if (canGrab)
                GUI.Label(new Rect(cx + 14, cy - 10, 300, 24), $"[LMB] grab {m_Aimed.DisplayName}");
            else if (m_Held != null)
                GUI.Label(new Rect(cx + 14, cy - 10, 300, 24), "[RMB] throw");
            GUI.color = old;
        }
    }
}
