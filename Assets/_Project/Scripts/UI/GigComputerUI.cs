using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// The GigGrabber app on the apartment computer: a list of gigs and their details.
    /// Anyone can browse; only the host can accept, which takes the whole crew to the gig.
    /// </summary>
    [DefaultExecutionOrder(-50)] // handle Esc before SessionHud does
    public class GigComputerUI : MonoBehaviour
    {
        public static GigComputerUI Instance { get; private set; }

        /// <summary>True while open, and on the frame Esc closed it (so Esc doesn't also open the pause menu).</summary>
        public static bool UsedEscapeThisFrame =>
            (Instance != null && Instance.IsOpen) || s_EscClosedFrame == Time.frameCount;

        static int s_EscClosedFrame = -1;

        static readonly Color Orange = new(1f, 0.54f, 0.17f);
        static readonly Color Paper = new(0.97f, 0.95f, 0.91f);
        static readonly Color Card = Color.white;
        static readonly Color Ink = new(0.16f, 0.16f, 0.22f);
        static readonly Color Muted = new(0.45f, 0.45f, 0.5f);
        static readonly Color Selected = new(1f, 0.88f, 0.74f);

        GameObject m_Root;
        GigDefinition m_Selected;
        Image[] m_CardImages;
        Text m_Title, m_Client, m_Tag, m_Stats, m_Description, m_Crew, m_HostOnly, m_Wallet, m_BestLabel;
        Image[] m_BestStars;
        Text[] m_CardLabels;
        Image m_TagChip;
        Button m_Accept;

        public bool IsOpen => m_Root != null && m_Root.activeSelf;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) CursorLock.ScreenWantsCursor = false;
        }

        void Start()
        {
            Build();
            m_Root.SetActive(false);
        }

        public void Open()
        {
            if (m_Root == null) return;
            m_Root.SetActive(true);
            CursorLock.ScreenWantsCursor = true;
            CursorLock.Refresh();
            Select(m_Selected ?? GigCatalog.All[0]);
        }

        public void Close()
        {
            if (m_Root == null) return;
            m_Root.SetActive(false);
            CursorLock.ScreenWantsCursor = false;
            CursorLock.Refresh();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                s_EscClosedFrame = Time.frameCount;
                return;
            }
            RefreshCrew();
        }

        // ---------- Layout ----------

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("GigComputerCanvas", 20);
            canvas.transform.SetParent(transform, false);

            var dim = UiFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Stretch(dim.rectTransform);
            m_Root = dim.gameObject;

            var window = UiFactory.CreatePanel("Window", dim.transform, Paper);
            UiFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1240, 760), Vector2.zero);

            // Top bar
            var bar = UiFactory.CreatePanel("TopBar", window.transform, Orange);
            UiFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(1240, 84), Vector2.zero);
            var logo = UiFactory.CreateText("Logo", bar.transform, "GigGrabber", 46, TextAnchor.MiddleLeft);
            logo.fontStyle = FontStyle.Bold;
            UiFactory.Place(logo.rectTransform, new Vector2(0f, 0.5f), new Vector2(420, 70), new Vector2(32, 0));
            m_Wallet = UiFactory.CreateText("Wallet", bar.transform, "", 26, TextAnchor.MiddleLeft);
            m_Wallet.fontStyle = FontStyle.Bold;
            UiFactory.Place(m_Wallet.rectTransform, new Vector2(0f, 0.5f), new Vector2(360, 50), new Vector2(330, 0));
            var tagline = UiFactory.CreateText("Tagline", bar.transform, "Be your own boss.*", 24, TextAnchor.MiddleRight);
            tagline.color = new Color(1f, 1f, 1f, 0.85f);
            UiFactory.Place(tagline.rectTransform, new Vector2(1f, 0.5f), new Vector2(500, 40), new Vector2(-110, 0));
            var close = UiFactory.CreateButton("Close", bar.transform, "X", Close);
            close.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(64, 56), new Vector2(-18, 0));

            // Gig list
            var listHeader = UiFactory.CreateText("ListHeader", window.transform, "AVAILABLE GIGS", 24, TextAnchor.MiddleLeft);
            listHeader.color = Muted;
            listHeader.fontStyle = FontStyle.Bold;
            UiFactory.Place(listHeader.rectTransform, new Vector2(0f, 1f), new Vector2(400, 40), new Vector2(32, -104));

            var list = UiFactory.CreateColumn("List", window.transform, 14);
            list.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            UiFactory.Place(list, new Vector2(0f, 1f), new Vector2(400, 560), new Vector2(32, -150));

            m_CardImages = new Image[GigCatalog.All.Length];
            m_CardLabels = new Text[GigCatalog.All.Length];
            for (int i = 0; i < GigCatalog.All.Length; i++)
            {
                var gig = GigCatalog.All[i];
                var card = UiFactory.CreateButton("Gig_" + gig.Id, list,
                    $"<b>{gig.Title}</b>\n<size=22>${gig.Pay}   ·   {FormatTime(gig.Minutes)}   ·   {gig.Tag}</size>", () => Select(gig));
                StyleCard(card, Card);
                m_CardImages[i] = card.GetComponent<Image>();
                m_CardLabels[i] = card.GetComponentInChildren<Text>();
            }
            var soon = UiFactory.CreateButton("ComingSoon", list,
                "<b>More gigs coming soon</b>\n<size=22>New listings every week*</size>", () => { });
            StyleCard(soon, new Color(0.9f, 0.89f, 0.86f));
            soon.interactable = false;
            soon.GetComponentInChildren<Text>().color = Muted;

            var fine = UiFactory.CreateText("FinePrint", window.transform,
                "*GigGrabber is not your employer. GigGrabber is not responsible for cakes.", 16, TextAnchor.LowerLeft);
            fine.color = Muted;
            UiFactory.Place(fine.rectTransform, new Vector2(0f, 0f), new Vector2(420, 50), new Vector2(32, 22));

            // Details
            var details = UiFactory.CreatePanel("Details", window.transform, Card);
            UiFactory.Place(details.rectTransform, new Vector2(1f, 1f), new Vector2(760, 630), new Vector2(-32, -104));

            m_Title = DetailText("Title", details.transform, 42, new Vector2(700, 60), new Vector2(30, -24));
            m_Title.fontStyle = FontStyle.Bold;
            m_Client = DetailText("Client", details.transform, 24, new Vector2(700, 36), new Vector2(30, -88));
            m_Client.color = Muted;

            m_TagChip = UiFactory.CreatePanel("TagChip", details.transform, new Color(0.85f, 0.25f, 0.25f));
            UiFactory.Place(m_TagChip.rectTransform, new Vector2(0f, 1f), new Vector2(160, 40), new Vector2(30, -136));
            m_Tag = UiFactory.CreateText("Tag", m_TagChip.transform, "", 22);
            m_Tag.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(m_Tag.rectTransform);

            // Best rating: label plus five diamonds (the font has no star glyph).
            m_BestLabel = DetailText("BestLabel", details.transform, 22, new Vector2(330, 40), new Vector2(215, -136));
            m_BestLabel.color = Muted;
            m_BestStars = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var d = UiFactory.CreatePanel("BestStar" + i, details.transform, Muted);
                UiFactory.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(20, 20), new Vector2(345 + i * 32, -146));
                d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                m_BestStars[i] = d;
            }

            m_Stats = DetailText("Stats", details.transform, 28, new Vector2(700, 44), new Vector2(30, -196));
            m_Description = DetailText("Description", details.transform, 24, new Vector2(700, 250), new Vector2(30, -256));
            m_Description.alignment = TextAnchor.UpperLeft;

            m_Crew = UiFactory.CreateText("Crew", details.transform, "", 22, TextAnchor.MiddleLeft);
            m_Crew.color = Muted;
            UiFactory.Place(m_Crew.rectTransform, new Vector2(0f, 0f), new Vector2(360, 40), new Vector2(30, 40));

            m_Accept = UiFactory.CreateButton("Accept", details.transform, "Accept gig", Accept);
            UiFactory.Place((RectTransform)m_Accept.transform, new Vector2(1f, 0f), new Vector2(320, 84), new Vector2(-30, 26));

            m_HostOnly = UiFactory.CreateText("HostOnly", details.transform,
                "Only the host can accept gigs.\nGo nag them.", 24, TextAnchor.MiddleRight);
            m_HostOnly.color = Muted;
            UiFactory.Place(m_HostOnly.rectTransform, new Vector2(1f, 0f), new Vector2(420, 84), new Vector2(-30, 26));
        }

        static Text DetailText(string name, Transform parent, int size, Vector2 boxSize, Vector2 offset)
        {
            var t = UiFactory.CreateText(name, parent, "", size, TextAnchor.MiddleLeft);
            t.color = Ink;
            UiFactory.Place(t.rectTransform, new Vector2(0f, 1f), boxSize, offset);
            return t;
        }

        static void StyleCard(Button card, Color color)
        {
            card.GetComponent<Image>().color = color;
            UiFactory.SetPreferredHeight(card, 104);
            var label = card.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 28;
            label.color = Ink;
            label.rectTransform.offsetMin = new Vector2(22, 0);
            label.rectTransform.offsetMax = new Vector2(-16, 0);
        }

        // ---------- Content ----------

        void Select(GigDefinition gig)
        {
            m_Selected = gig;
            for (int i = 0; i < GigCatalog.All.Length; i++)
                m_CardImages[i].color = GigCatalog.All[i] == gig ? Selected : Card;

            m_Title.text = gig.Title;
            m_Client.text = $"Client: {gig.Client}      Client rating: {gig.ClientRating:0.0} / 5";
            m_Tag.text = gig.Tag;
            m_Stats.text = $"<b>${gig.Pay}</b> pay        <b>{FormatTime(gig.Minutes)}</b> time limit        <b>1-{SessionManager.MaxPlayers}</b> crew";
            m_Description.text = gig.Description;
            RefreshCrew();
            RefreshBest();
        }

        void RefreshBest()
        {
            var crew = CrewState.Current;
            for (int i = 0; i < GigCatalog.All.Length; i++)
            {
                var g = GigCatalog.All[i];
                int b = crew != null ? crew.BestStars(g.Id) : 0;
                string best = b > 0 ? $"Best {b}/5" : "New!";
                m_CardLabels[i].text = $"<b>{g.Title}</b>\n<size=21>${g.Pay}  ·  {FormatTime(g.Minutes)}  ·  {g.Tag}  ·  {best}</size>";
            }
            if (m_Selected == null) return;
            int stars = crew != null ? crew.BestStars(m_Selected.Id) : 0;
            m_BestLabel.text = stars > 0 ? "Your best:" : "Your best: not delivered yet";
            for (int i = 0; i < m_BestStars.Length; i++)
            {
                m_BestStars[i].gameObject.SetActive(stars > 0);
                m_BestStars[i].color = i < stars ? new Color(1f, 0.75f, 0.2f) : new Color(0.85f, 0.84f, 0.8f);
            }
        }

        void RefreshCrew()
        {
            m_Wallet.text = CrewState.Current != null ? $"Crew funds: ${CrewState.Current.Money}" : "";
            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsServer;
            m_Accept.gameObject.SetActive(isHost);
            m_HostOnly.gameObject.SetActive(!isHost);
            int count = PlayerMotor.ByClientId.Count;
            m_Crew.text = $"Crew in the apartment: {count}/{SessionManager.MaxPlayers}";
        }

        void Accept()
        {
            if (m_Selected == null || SessionManager.Instance == null) return;
            Close();
            SessionManager.Instance.StartGig(m_Selected);
        }

        static string FormatTime(float minutes)
        {
            int seconds = Mathf.RoundToInt(minutes * 60f);
            return $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
