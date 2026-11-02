using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>Main menu: logo, Host, Join by code, Quit, and the character customizer. Builds its own UI at startup.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Shown in the customizer preview.")]
        [SerializeField] GameObject playerPrefab;

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

        static readonly Color k_BackTop = new(0.19f, 0.13f, 0.29f);
        static readonly Color k_BackBottom = new(0.07f, 0.07f, 0.13f);
        static readonly Color k_Accent = new(1f, 0.55f, 0.17f);
        public static readonly Color CardColor = new(0.15f, 0.12f, 0.23f);

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("MainMenuCanvas", 0);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // Background: dusk gradient with a warm glow in the corner.
            var bg = UiFactory.CreateGradient("Background", root, k_BackBottom, k_BackTop);
            UiFactory.Stretch(bg.rectTransform);
            var glow = UiFactory.CreateGlow("Glow", root, new Color(k_Accent.r, k_Accent.g, k_Accent.b, 0.22f));
            UiFactory.Place(glow.rectTransform, new Vector2(0f, 0f), new Vector2(1500, 1500), new Vector2(-650, -700));
            var glow2 = UiFactory.CreateGlow("Glow2", root, new Color(0.55f, 0.35f, 1f, 0.16f));
            UiFactory.Place(glow2.rectTransform, new Vector2(1f, 1f), new Vector2(1300, 1300), new Vector2(450, 450));

            // Logo, top left: a little orange bar, then the name.
            const float left = 120f;
            var bar = UiFactory.CreatePanel("LogoBar", root, k_Accent);
            UiFactory.Round(bar, 4f);
            UiFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(90, 12), new Vector2(left + 6, -150));
            var title = UiFactory.CreateText("Title", root, "FREELANCER", 128, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1000, 160), new Vector2(left - 4, -170));

            // Main actions, left column.
            var column = UiFactory.CreateColumn("Buttons", root, 16);
            UiFactory.Place(column, new Vector2(0f, 0.5f), new Vector2(560, 300), new Vector2(left, -10));

            m_HostButton = UiFactory.CreateButton("Host", column, "HOST A GIG", OnHost);
            m_HostButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            UiFactory.SetPreferredHeight(m_HostButton, 88);

            var joinRow = UiFactory.CreateRect("JoinRow", column);
            UiFactory.SetPreferredHeight(joinRow, 76);
            var rowLayout = joinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            m_CodeField = UiFactory.CreateInputField("JoinCode", joinRow, "Join code");
            m_CodeField.characterLimit = 12;
            m_CodeField.onValidateInput += (_, _, c) => char.ToUpperInvariant(c);
            m_CodeField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
            Flex(m_CodeField, 0, 1);
            m_JoinButton = UiFactory.Ghost(UiFactory.CreateButton("Join", joinRow, "JOIN", OnJoin));
            m_JoinButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            Flex(m_JoinButton, 150, 0);

            var quit = UiFactory.Ghost(UiFactory.CreateButton("Quit", column, "Quit", OnQuit));
            UiFactory.SetPreferredHeight(quit, 64);
            quit.GetComponentInChildren<Text>().fontSize = 30;

            m_Status = UiFactory.CreateText("Status", root, "", 28, TextAnchor.UpperLeft);
            m_Status.color = new Color(1f, 1f, 1f, 0.75f);
            UiFactory.Place(m_Status.rectTransform, new Vector2(0f, 0.5f), new Vector2(560, 120), new Vector2(left, -185));

            // Dev shortcuts (no Relay, same machine): small and out of the way.
            var dev = UiFactory.CreateRect("DevButtons", root);
            UiFactory.Place(dev, new Vector2(0f, 0f), new Vector2(330, 44), new Vector2(left, 40));
            var row = dev.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            foreach (var b in new[]
                     {
                         UiFactory.CreateButton("LocalHost", dev, "Local host", () => SessionManager.Instance?.HostLocal()),
                         UiFactory.CreateButton("LocalJoin", dev, "Local join", () => SessionManager.Instance?.JoinLocal()),
                     })
            {
                UiFactory.Ghost(b);
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 20;
                label.color = new Color(1f, 1f, 1f, 0.6f);
            }

            var version = UiFactory.CreateText("Version", root, "pre-alpha", 20, TextAnchor.LowerRight);
            version.color = new Color(1f, 1f, 1f, 0.35f);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(300, 30), new Vector2(-40, 30));

            gameObject.AddComponent<CustomizePanel>().Build(root, playerPrefab, new Vector2(1f, 0.5f), new Vector2(-120, -20));
        }

        static void Flex(Component c, float width, float flexible)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
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
