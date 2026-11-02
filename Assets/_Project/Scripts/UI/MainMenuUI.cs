using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>Main menu: Host, Join by code, Quit. Builds its own UI at startup.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        InputField m_CodeField;
        Text m_Status;
        Button m_HostButton;
        Button m_JoinButton;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.StatusChanged += RefreshStatus;
                RefreshStatus();
            }
            else
            {
                m_Status.text = "No SessionManager in scene. Run Freelancer/Setup/Build All.";
            }
        }

        void OnDestroy()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.StatusChanged -= RefreshStatus;
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("MainMenuCanvas", 0);
            canvas.transform.SetParent(transform, false);

            var bg = UiFactory.CreatePanel("Background", canvas.transform, new Color(0.13f, 0.11f, 0.2f));
            UiFactory.Stretch(bg.rectTransform);

            var title = UiFactory.CreateText("Title", canvas.transform, "FREELANCER", 120);
            title.fontStyle = FontStyle.Bold;
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400, 160), new Vector2(0, -120));

            var tagline = UiFactory.CreateText("Tagline", canvas.transform,
                "Gigs done badly, together. (1 star? We'll take it.)", 36);
            tagline.color = new Color(1f, 0.8f, 0.5f);
            UiFactory.Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400, 60), new Vector2(0, -270));

            var column = UiFactory.CreateColumn("Buttons", canvas.transform, 20);
            UiFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(560, 420), new Vector2(0, -60));

            m_HostButton = UiFactory.CreateButton("Host", column, "Host a gig", OnHost);
            UiFactory.SetPreferredHeight(m_HostButton, 80);

            m_CodeField = UiFactory.CreateInputField("JoinCode", column, "Join code");
            m_CodeField.characterLimit = 12;
            m_CodeField.onValidateInput += (_, _, c) => char.ToUpperInvariant(c);
            UiFactory.SetPreferredHeight(m_CodeField, 80);

            m_JoinButton = UiFactory.CreateButton("Join", column, "Join", OnJoin);
            UiFactory.SetPreferredHeight(m_JoinButton, 80);

            var quit = UiFactory.CreateButton("Quit", column, "Quit", OnQuit);
            UiFactory.SetPreferredHeight(quit, 80);

            // Dev row: skip Relay and connect on this machine.
            var dev = UiFactory.CreateRect("DevButtons", canvas.transform);
            UiFactory.Place(dev, new Vector2(0f, 0f), new Vector2(560, 60), new Vector2(30, 30));
            var row = dev.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            UiFactory.CreateButton("LocalHost", dev, "Local host", () => SessionManager.Instance?.HostLocal())
                .GetComponentInChildren<Text>().fontSize = 24;
            UiFactory.CreateButton("LocalJoin", dev, "Local join", () => SessionManager.Instance?.JoinLocal())
                .GetComponentInChildren<Text>().fontSize = 24;

            m_Status = UiFactory.CreateText("Status", canvas.transform, "", 32);
            UiFactory.Place(m_Status.rectTransform, new Vector2(0.5f, 0f), new Vector2(1600, 80), new Vector2(0, 80));
        }

        void RefreshStatus()
        {
            if (m_Status == null) return;
            var sm = SessionManager.Instance;
            m_Status.text = sm.Status;
            m_HostButton.interactable = !sm.IsBusy;
            m_JoinButton.interactable = !sm.IsBusy;
        }

        void OnHost() => _ = SessionManager.Instance?.HostAsync();

        void OnJoin() => _ = SessionManager.Instance?.JoinAsync(m_CodeField.text);

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
