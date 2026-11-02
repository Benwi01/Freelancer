using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>Apartment HUD: a hint at the bottom telling the crew what to do.</summary>
    public class HubHud : MonoBehaviour
    {
        Text m_Hint;

        void Start()
        {
            var canvas = UiFactory.CreateCanvas("HubHudCanvas", 5);
            canvas.transform.SetParent(transform, false);
            m_Hint = UiFactory.CreateText("Hint", canvas.transform, "", 30, TextAnchor.LowerCenter);
            UiFactory.Place(m_Hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400, 60), new Vector2(0, 40));
            CursorLock.ScreenWantsCursor = false;
            CursorLock.Refresh();
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsServer;
            bool computerOpen = GigComputerUI.Instance != null && GigComputerUI.Instance.IsOpen;
            m_Hint.gameObject.SetActive(!computerOpen);
            m_Hint.text = isHost
                ? "Rent's due. Find a gig on the computer and press <b>E</b>."
                : "Wait for the host to pick a gig (or browse the computer with <b>E</b>).";
        }
    }
}
