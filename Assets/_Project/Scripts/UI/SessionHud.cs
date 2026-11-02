using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// In-game overlay that always shows the join code, plus an Esc menu with "Leave".
    /// While the Esc menu is open the cursor is free and player input is paused.
    /// </summary>
    public class SessionHud : MonoBehaviour
    {
        public static bool MenuOpen { get; private set; }

        Text m_Code;
        Text m_Players;
        GameObject m_Menu;

        void Start()
        {
            var canvas = UiFactory.CreateCanvas("SessionHudCanvas", 10);
            canvas.transform.SetParent(transform, false);

            m_Code = UiFactory.CreateText("JoinCode", canvas.transform, "", 34, TextAnchor.UpperRight);
            UiFactory.Place(m_Code.rectTransform, new Vector2(1f, 1f), new Vector2(700, 50), new Vector2(-30, -20));

            m_Players = UiFactory.CreateText("Players", canvas.transform, "", 26, TextAnchor.UpperRight);
            m_Players.color = new Color(1, 1, 1, 0.7f);
            UiFactory.Place(m_Players.rectTransform, new Vector2(1f, 1f), new Vector2(700, 40), new Vector2(-30, -70));

            var hint = UiFactory.CreateText("Hint", canvas.transform, "Esc: menu", 22, TextAnchor.LowerRight);
            hint.color = new Color(1, 1, 1, 0.5f);
            UiFactory.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(400, 40), new Vector2(-30, 20));

            // Esc menu
            var menuBg = UiFactory.CreatePanel("EscMenu", canvas.transform, new Color(0, 0, 0, 0.6f));
            UiFactory.Stretch(menuBg.rectTransform);
            m_Menu = menuBg.gameObject;

            var column = UiFactory.CreateColumn("Buttons", menuBg.transform, 20);
            UiFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(500, 200), Vector2.zero);
            UiFactory.SetPreferredHeight(UiFactory.CreateButton("Resume", column, "Resume", () => SetMenu(false)), 80);
            UiFactory.SetPreferredHeight(UiFactory.CreateButton("Leave", column, "Leave gig", OnLeave), 80);

            SetMenu(false);
        }

        void OnDestroy()
        {
            MenuOpen = false;
        }

        void Update()
        {
            var session = SessionManager.Instance != null ? SessionManager.Instance.ActiveSession : null;
            m_Code.text = session != null ? $"Join code: <b>{session.Code}</b>" : "Offline";

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                int count = nm.IsServer ? nm.ConnectedClientsIds.Count : (session?.PlayerCount ?? 0);
                m_Players.text = $"{count}/{SessionManager.MaxPlayers} freelancers";
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetMenu(!MenuOpen);
        }

        void SetMenu(bool open)
        {
            MenuOpen = open;
            m_Menu.SetActive(open);
            CursorLock.Refresh();
        }

        void OnLeave()
        {
            SetMenu(false);
            _ = SessionManager.Instance?.LeaveAsync();
        }
    }
}
