using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>Apartment HUD: a hint at the bottom telling the crew what to do.</summary>
    public class HubHud : MonoBehaviour
    {
        Text m_Hint;
        Text m_Funds;

        void Start()
        {
            var canvas = UiFactory.CreateCanvas("HubHudCanvas", 5);
            canvas.transform.SetParent(transform, false);
            m_Hint = UiFactory.CreateText("Hint", canvas.transform, "", 30, TextAnchor.LowerCenter);
            UiFactory.Place(m_Hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400, 60), new Vector2(0, 40));
            m_Funds = UiFactory.CreateText("Funds", canvas.transform, "", 34, TextAnchor.UpperLeft);
            UiFactory.Place(m_Funds.rectTransform, new Vector2(0f, 1f), new Vector2(700, 170), new Vector2(30, -20));

            CursorLock.ScreenWantsCursor = false;
            CursorLock.Refresh();
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsServer;
            bool computerOpen = GigComputerUI.Instance != null && GigComputerUI.Instance.IsOpen;
            m_Hint.gameObject.SetActive(!computerOpen);
            var crew = CrewState.Current;
            m_Funds.text = crew != null ? FundsText(crew) : "";
            string rent = crew != null ? $"Rent (${crew.RentDue}) is due in {Gigs(crew.GigsUntilRent)}. " : "";
            m_Hint.text = isHost
                ? rent + "Find a gig on the computer or the van outside (<b>E</b>)."
                : rent + "Wait for the host to pick a gig (or browse GigGrabber with <b>E</b>).";
        }

        /// <summary>Funds, then rent: red when the crew can't cover it yet.</summary>
        public static string FundsText(CrewState crew)
        {
            string color = crew.CanAffordRent ? "#FFFFFF" : "#FF6A5C";
            string warning = crew.OnFinalWarning ? "  <color=#FF6A5C><b>FINAL WARNING</b></color>" : "";
            return $"<size=22>CREW FUNDS</size>\n<b>${crew.Money}</b>\n" +
                   $"<size=24><color={color}>Rent <b>${crew.RentDue}</b> due in {Gigs(crew.GigsUntilRent)}</color>{warning}</size>";
        }

        public static string Gigs(int n) => n == 1 ? "1 gig" : $"{n} gigs";
    }
}
