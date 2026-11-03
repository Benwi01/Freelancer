using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Freelancer
{
    /// <summary>
    /// Owner-side "look at it and press E": finds an <see cref="IInteractable"/> under the crosshair
    /// within reach and shows its prompt. R does its <see cref="ISecondaryInteractable"/> action, if it has one.
    /// </summary>
    public class PlayerInteractor : NetworkBehaviour
    {
        public const float Reach = 2.6f;

        PlayerMotor m_Motor;
        PlayerKnockdown m_Knockdown;
        IInteractable m_Target;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            m_Knockdown = GetComponent<PlayerKnockdown>();
        }

        void Update()
        {
            m_Target = null;
            if (!IsSpawned || !IsOwner || !CursorLock.GameplayInputAllowed) return;
            if (m_Knockdown != null && m_Knockdown.IsDown) return;

            var cam = m_Motor.Camera.transform;
            // Interactables use trigger colliders, so include triggers here.
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, Reach, ~0, QueryTriggerInteraction.Collide))
                m_Target = hit.collider.GetComponentInParent<IInteractable>();

            if (m_Target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                m_Target.Interact(m_Motor);
            else if (m_Target is ISecondaryInteractable second && Keyboard.current != null &&
                     Keyboard.current.rKey.wasPressedThisFrame && !string.IsNullOrEmpty(second.SecondaryPrompt))
                second.SecondaryInteract(m_Motor);
        }

        void OnGUI()
        {
            if (m_Target == null || !CursorLock.GameplayInputAllowed) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperCenter };
            string text = $"[E] {m_Target.Prompt}";
            if (m_Target is ISecondaryInteractable second && !string.IsNullOrEmpty(second.SecondaryPrompt))
                text += $"      [R] {second.SecondaryPrompt}";
            GUI.Label(new Rect(cx - 400, cy + 18, 800, 30), text, style);
        }
    }
}
