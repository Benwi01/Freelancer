using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Freelancer
{
    /// <summary>
    /// First-person movement: walk, sprint, jump, mouse look. Owner-authoritative; position and
    /// yaw are replicated by an owner-mode NetworkTransform, pitch by a NetworkVariable so the
    /// server knows where each player is looking (used for grab hand points).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        public const float EyeHeight = 1.6f;

        /// <summary>All spawned players by owner client id (on every machine).</summary>
        public static readonly Dictionary<ulong, PlayerMotor> ByClientId = new();

        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float groundAccel = 40f;
        [SerializeField] float airAccel = 8f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.1f;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Camera playerCamera;

        [Header("Visuals")]
        [SerializeField] Renderer[] tintRenderers;

        readonly NetworkVariable<float> m_Pitch = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        static readonly Color[] k_PlayerColors =
        {
            new(0.95f, 0.35f, 0.3f), new(0.3f, 0.6f, 0.95f), new(0.4f, 0.85f, 0.4f), new(0.95f, 0.8f, 0.25f),
        };

        CharacterController m_Controller;
        PlayerKnockdown m_Knockdown;
        Vector3 m_Velocity;
        float m_LocalPitch;
        Vector3 m_SpawnPosition;
        Camera m_SceneCamera;
        Renderer[] m_BodyRenderers;
        bool m_OwnBodyVisible = true;

        /// <summary>Look pitch in degrees (positive = down). Valid on every machine.</summary>
        public float Pitch => IsOwner ? m_LocalPitch : m_Pitch.Value;

        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;

        /// <summary>Where this player is looking. Valid on every machine.</summary>
        public Vector3 LookDirection
        {
            get
            {
                float pitch = IsOwner ? m_LocalPitch : m_Pitch.Value;
                return Quaternion.Euler(pitch, transform.eulerAngles.y, 0f) * Vector3.forward;
            }
        }

        public Camera Camera => playerCamera;
        public Color PlayerColor => k_PlayerColors[OwnerClientId % (ulong)k_PlayerColors.Length];

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Knockdown = GetComponent<PlayerKnockdown>();
            // Cached now: the ragdoll later moves the body out of this hierarchy for a while.
            m_BodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            ByClientId[OwnerClientId] = this;
            ApplyTint();

            bool local = IsOwner;
            playerCamera.enabled = local;
            if (playerCamera.TryGetComponent(out AudioListener listener)) listener.enabled = local;
            // The CharacterController stays enabled on every copy: it is also the collider that
            // props and other players bump into. Only the owner calls Move() on it.

            if (local)
            {
                m_SpawnPosition = transform.position;
                // You don't see your own body (except when ragdolled), but it still casts a shadow.
                SetOwnBodyVisible(false);
                m_SceneCamera = Camera.main != playerCamera ? Camera.main : null;
                if (m_SceneCamera != null) m_SceneCamera.gameObject.SetActive(false);
                CursorLock.Refresh();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ByClientId.TryGetValue(OwnerClientId, out var p) && p == this)
                ByClientId.Remove(OwnerClientId);
            if (IsOwner && m_SceneCamera != null) m_SceneCamera.gameObject.SetActive(true);
        }

        void ApplyTint()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", PlayerColor);
            foreach (var r in tintRenderers)
                if (r != null) r.SetPropertyBlock(block);
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner) OwnerUpdate();
        }

        void OwnerUpdate()
        {
            bool canControl = CursorLock.GameplayInputAllowed && (m_Knockdown == null || !m_Knockdown.IsDown);
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // Re-capture the mouse after clicking into the game window.
            if (CursorLock.GameplayInputAllowed && Cursor.lockState != CursorLockMode.Locked &&
                mouse != null && mouse.leftButton.wasPressedThisFrame)
                CursorLock.Refresh();

            // Look (allowed while knocked down so you can enjoy the view).
            if (CursorLock.GameplayInputAllowed && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, delta.x, 0f);
                m_LocalPitch = Mathf.Clamp(m_LocalPitch - delta.y, -85f, 85f);
            }
            cameraPivot.localRotation = Quaternion.Euler(m_LocalPitch, 0, 0);
            if (Mathf.Abs(m_Pitch.Value - m_LocalPitch) > 0.5f) m_Pitch.Value = m_LocalPitch;

            // While ragdolled, PlayerKnockdown moves us along with the body.
            if (m_Knockdown != null && m_Knockdown.IsRagdolled)
            {
                m_Velocity = Vector3.zero;
                return;
            }

            // Move
            Vector2 input = Vector2.zero;
            bool sprint = false, jump = false;
            if (canControl && kb != null)
            {
                if (kb.wKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed) input.x -= 1;
                sprint = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            bool grounded = m_Controller.isGrounded;
            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;
            Vector3 horizontal = new Vector3(m_Velocity.x, 0, m_Velocity.z);
            float accel = grounded ? groundAccel : airAccel;

            if (m_Knockdown != null && m_Knockdown.IsDown)
                horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, 6f * Time.deltaTime);
            else
                horizontal = Vector3.MoveTowards(horizontal, wish, accel * Time.deltaTime);

            if (grounded && m_Velocity.y < 0) m_Velocity.y = -2f;
            if (grounded && jump) m_Velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            m_Velocity.y += gravity * Time.deltaTime;
            m_Velocity = new Vector3(horizontal.x, m_Velocity.y, horizontal.z);

            m_Controller.Move(m_Velocity * Time.deltaTime);

            if (transform.position.y < -20f) Teleport(m_SpawnPosition);
        }

        /// <summary>Owner only: show or hide your own body on your screen (it always casts shadows).</summary>
        public void SetOwnBodyVisible(bool visible)
        {
            if (visible == m_OwnBodyVisible) return;
            m_OwnBodyVisible = visible;
            var mode = visible ? UnityEngine.Rendering.ShadowCastingMode.On
                               : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in m_BodyRenderers)
                if (r != null) r.shadowCastingMode = mode;
        }

        /// <summary>Owner only: back to where you spawned.</summary>
        public void Respawn() => Teleport(m_SpawnPosition);

        /// <summary>Owner only: shove the player.</summary>
        public void AddImpulse(Vector3 velocity)
        {
            m_Velocity += velocity;
        }

        /// <summary>Owner only: move without collision.</summary>
        public void Teleport(Vector3 position)
        {
            m_Controller.enabled = false;
            transform.position = position;
            m_Velocity = Vector3.zero;
            m_Controller.enabled = true;
        }
    }
}
