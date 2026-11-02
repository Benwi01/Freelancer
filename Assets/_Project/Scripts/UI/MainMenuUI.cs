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

        // The game's own palette: sunny sky, warm cream, navy trim, GigGrabber orange.
        public static readonly Color SkyTop = Hex("3A8BF2");
        public static readonly Color SkyHorizon = Hex("FDEDD8");
        public static readonly Color Navy = Hex("2E3A5F");
        public static readonly Color Cream = Hex("FBF7F0");
        public static readonly Color Stone = Hex("EDE6DA");
        public static readonly Color Orange = Hex("FF8A2B");
        public static readonly Color CardColor = Cream;

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("MainMenuCanvas", 0);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // Background: the same sky as the levels, a warm sun glow and a few flat clouds.
            var bg = UiFactory.CreateGradient("Background", root, SkyHorizon, SkyTop);
            UiFactory.Stretch(bg.rectTransform);
            var sun = UiFactory.CreateGlow("Sun", root, new Color(1f, 0.92f, 0.75f, 0.75f));
            UiFactory.Place(sun.rectTransform, new Vector2(1f, 1f), new Vector2(1100, 1100), new Vector2(380, 420));
            Cloud(root, new Vector2(0.68f, 0.93f), 1f);
            Cloud(root, new Vector2(0.22f, 0.2f), 0.85f);
            Cloud(root, new Vector2(0.5f, 0.06f), 0.6f);
            Cloud(root, new Vector2(0.9f, 0.12f), 0.9f);

            // Logo, top left: a little orange bar, then the name with a navy drop shadow.
            const float left = 120f;
            var bar = UiFactory.CreatePanel("LogoBar", root, Orange);
            UiFactory.Round(bar, 4f);
            UiFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(90, 12), new Vector2(left + 6, -150));
            var title = UiFactory.CreateText("Title", root, "FREELANCER", 128, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Navy.r, Navy.g, Navy.b, 0.55f);
            shadow.effectDistance = new Vector2(6, -6);
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
            m_CodeField.GetComponent<Image>().color = Cream;
            m_CodeField.textComponent.color = Navy;
            Flex(m_CodeField, 0, 1);
            m_JoinButton = UiFactory.Solid(UiFactory.CreateButton("Join", joinRow, "JOIN", OnJoin), Navy, Color.white);
            m_JoinButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            Flex(m_JoinButton, 150, 0);

            var quit = UiFactory.Solid(UiFactory.CreateButton("Quit", column, "Quit", OnQuit), new Color(1f, 1f, 1f, 0.75f), Navy);
            UiFactory.SetPreferredHeight(quit, 64);
            quit.GetComponentInChildren<Text>().fontSize = 30;

            m_Status = UiFactory.CreateText("Status", root, "", 28, TextAnchor.UpperLeft);
            m_Status.color = Navy;
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
                UiFactory.Solid(b, new Color(1f, 1f, 1f, 0.45f), new Color(Navy.r, Navy.g, Navy.b, 0.75f));
                b.GetComponentInChildren<Text>().fontSize = 20;
            }

            var version = UiFactory.CreateText("Version", root, "pre-alpha", 20, TextAnchor.LowerRight);
            version.color = new Color(Navy.r, Navy.g, Navy.b, 0.5f);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(300, 30), new Vector2(-40, 30));

            gameObject.AddComponent<CustomizePanel>().Build(root, playerPrefab, new Vector2(1f, 0.5f), new Vector2(-120, -20));
        }

        /// <summary>A flat cartoon cloud: a few overlapping rounded white blobs.</summary>
        static void Cloud(Transform parent, Vector2 anchor, float scale)
        {
            var cloud = UiFactory.CreateRect("Cloud", parent);
            UiFactory.Place(cloud, anchor, new Vector2(360, 120) * scale, Vector2.zero);
            var white = new Color(0.96f, 0.98f, 1f, 1f);
            foreach (var (pos, size) in new[]
                     {
                         (new Vector2(0f, -30f), new Vector2(360f, 70f)),
                         (new Vector2(-60f, 5f), new Vector2(150f, 120f)),
                         (new Vector2(50f, 15f), new Vector2(170f, 150f)),
                     })
            {
                var blob = UiFactory.CreatePanel("Blob", cloud, white);
                UiFactory.Round(blob, 60f * scale);
                blob.raycastTarget = false;
                UiFactory.Place(blob.rectTransform, new Vector2(0.5f, 0.5f), size * scale, pos * scale);
            }
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
