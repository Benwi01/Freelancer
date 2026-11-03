using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// The landlord's letter, waiting in the apartment after rent day (and once at the very start).
    /// Each machine shows each letter once; reading it is local, so nobody waits on anyone.
    /// </summary>
    [DefaultExecutionOrder(-50)] // handle Esc before SessionHud does
    public class LandlordLetterUI : MonoBehaviour
    {
        public static LandlordLetterUI Instance { get; private set; }

        /// <summary>True while open, and on the frame Esc closed it (so Esc doesn't also open the pause menu).</summary>
        public static bool UsedEscapeThisFrame =>
            (Instance != null && Instance.IsOpen) || s_EscClosedFrame == Time.frameCount;

        static int s_EscClosedFrame = -1;
        /// <summary>Last letter read on this machine (survives scene loads).</summary>
        static int s_ReadNoticeId;

        static readonly Color Paper = new(0.98f, 0.96f, 0.9f);
        static readonly Color Ink = new(0.18f, 0.18f, 0.24f);
        static readonly Color Red = new(0.82f, 0.18f, 0.16f);
        static readonly Color Green = new(0.2f, 0.6f, 0.3f);

        GameObject m_Root;
        Text m_Heading, m_Body, m_Stamp;
        int m_ShowingId;

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

        void Update()
        {
            var crew = CrewState.Current;
            if (!IsOpen)
            {
                if (crew != null && crew.Notice != RentNotice.None && crew.NoticeId != s_ReadNoticeId &&
                    (GigComputerUI.Instance == null || !GigComputerUI.Instance.IsOpen) && !SessionHud.MenuOpen)
                    Open(crew);
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                if (kb.escapeKey.wasPressedThisFrame) s_EscClosedFrame = Time.frameCount;
                Close();
            }
        }

        void Open(CrewState crew)
        {
            m_ShowingId = crew.NoticeId;
            Fill(crew);
            m_Root.SetActive(true);
            CursorLock.ScreenWantsCursor = true;
            CursorLock.Refresh();
        }

        void Close()
        {
            s_ReadNoticeId = m_ShowingId;
            m_Root.SetActive(false);
            CursorLock.ScreenWantsCursor = false;
            CursorLock.Refresh();
        }

        void Fill(CrewState crew)
        {
            int amount = crew.NoticeAmount;
            int next = crew.RentDue;
            string gigs = Gigs(RentRules.GigsPerWeek);
            m_Stamp.gameObject.SetActive(true);
            switch (crew.Notice)
            {
                case RentNotice.Intro:
                    m_Heading.text = "RE: THE RENT";
                    m_Body.text =
                        "Dear tenants,\n\n" +
                        $"Welcome to the flat. Rent is <b>${next}</b>, due every {gigs}. I'll take it out of your " +
                        "crew funds myself, so don't go spending it.\n\n" +
                        "It goes up a bit every week. That's just how buildings work.\n\n" +
                        "Miss it once and I'll be cross. Miss it twice and you're out.\n\n" +
                        "<i>— Mr. Pemberton, your landlord</i>";
                    StampAs("RENT: EVERY " + RentRules.GigsPerWeek + " GIGS", Ink);
                    break;
                case RentNotice.Paid:
                    m_Heading.text = "RECEIPT";
                    m_Body.text =
                        "Dear tenants,\n\n" +
                        $"Received: <b>${amount}</b> in rent. Thank you, and well done for not being late.\n\n" +
                        $"Next week's rent is <b>${next}</b>, due in {Gigs(crew.GigsUntilRent)}. " +
                        "Prices have gone up. Not my fault.\n\n" +
                        "<i>— Mr. Pemberton</i>";
                    StampAs("PAID", Green);
                    break;
                case RentNotice.Missed:
                    m_Heading.text = "FINAL WARNING";
                    m_Body.text =
                        "Dear tenants,\n\n" +
                        $"Rent was <b>${amount}</b>. You did not have <b>${amount}</b>. I checked.\n\n" +
                        $"It's going on next week's bill. You now owe <b>${next}</b> in {Gigs(crew.GigsUntilRent)}.\n\n" +
                        "If it's short again, I'm changing the locks.\n\n" +
                        "<i>— Mr. Pemberton (cross)</i>";
                    StampAs("OVERDUE", Red);
                    break;
                case RentNotice.Evicted:
                    m_Heading.text = "NOTICE OF EVICTION";
                    m_Body.text =
                        "Dear ex-tenants,\n\n" +
                        $"You owed <b>${amount}</b>. You did not have it. Again.\n\n" +
                        "I've kept your crew funds for the trouble and your things are in the skip. " +
                        "The new tenants move in... well, it's you. I've no one else. Rent is " +
                        $"<b>${next}</b>, starting from scratch.\n\n" +
                        "<i>— Mr. Pemberton (very cross)</i>";
                    StampAs("EVICTED", Red);
                    break;
                default:
                    m_Stamp.gameObject.SetActive(false);
                    break;
            }
        }

        void StampAs(string text, Color color)
        {
            m_Stamp.text = text;
            m_Stamp.color = color;
            m_Stamp.fontSize = text.Length > 10 ? 36 : 72;
        }

        static string Gigs(int n) => n == 1 ? "1 gig" : $"{n} gigs";

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("LandlordLetterCanvas", 25);
            canvas.transform.SetParent(transform, false);

            var dim = UiFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Stretch(dim.rectTransform);
            m_Root = dim.gameObject;

            var envelope = UiFactory.CreatePanel("Envelope", dim.transform, new Color(0.85f, 0.78f, 0.62f));
            UiFactory.Round(envelope, 10f);
            UiFactory.Place(envelope.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(820, 760), new Vector2(14, -18));
            envelope.rectTransform.localRotation = Quaternion.Euler(0, 0, -2.5f);

            var paper = UiFactory.CreatePanel("Letter", dim.transform, Paper);
            UiFactory.Round(paper, 6f);
            UiFactory.Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(780, 740), Vector2.zero);
            paper.rectTransform.localRotation = Quaternion.Euler(0, 0, 1f);

            var from = UiFactory.CreateText("From", paper.transform, "PEMBERTON PROPERTIES LTD.  ·  Flat 3B", 20, TextAnchor.MiddleLeft);
            from.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
            UiFactory.Place(from.rectTransform, new Vector2(0f, 1f), new Vector2(700, 40), new Vector2(48, -30));
            var rule = UiFactory.CreatePanel("Rule", paper.transform, new Color(Ink.r, Ink.g, Ink.b, 0.25f));
            UiFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(684, 2), new Vector2(0, -72));

            m_Heading = UiFactory.CreateText("Heading", paper.transform, "", 44, TextAnchor.MiddleLeft);
            m_Heading.fontStyle = FontStyle.Bold;
            m_Heading.color = Ink;
            UiFactory.Place(m_Heading.rectTransform, new Vector2(0f, 1f), new Vector2(700, 64), new Vector2(48, -88));

            m_Body = UiFactory.CreateText("Body", paper.transform, "", 25, TextAnchor.UpperLeft);
            m_Body.color = Ink;
            m_Body.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_Body.lineSpacing = 1.1f;
            UiFactory.Place(m_Body.rectTransform, new Vector2(0f, 1f), new Vector2(684, 450), new Vector2(48, -168));

            m_Stamp = UiFactory.CreateText("Stamp", paper.transform, "", 64, TextAnchor.MiddleCenter);
            m_Stamp.fontStyle = FontStyle.Bold;
            m_Stamp.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Place(m_Stamp.rectTransform, new Vector2(0f, 0f), new Vector2(420, 110), new Vector2(50, 50));
            m_Stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 9f);
            var outline = m_Stamp.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.6f);
            outline.effectDistance = new Vector2(2, -2);

            var ok = UiFactory.Solid(UiFactory.CreateButton("Ok", paper.transform, "Fine.", Close), Ink, Color.white);
            ok.GetComponentInChildren<Text>().fontSize = 28;
            UiFactory.Place((RectTransform)ok.transform, new Vector2(1f, 0f), new Vector2(220, 70), new Vector2(-40, 36));
        }
    }
}
