using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// Gig HUD: timer, cake condition bar, briefing card, delivery progress and the results screen
    /// (stars + Brenda's review). Reads the server-replicated GigDirector / CakeCondition state.
    /// </summary>
    public class GigHud : MonoBehaviour
    {
        static readonly Color Gold = new(1f, 0.8f, 0.2f);
        static readonly Color Dim = new(1f, 1f, 1f, 0.15f);

        Text m_Timer;
        Image m_ConditionFill;
        Text m_ConditionLabel;
        Text m_Objective;
        Image m_SettleFill;
        GameObject m_SettleBar;
        GameObject m_Briefing;
        Text m_BriefingCountdown;
        GameObject m_Results;
        Text m_ResultTitle;
        Text m_ResultDetail;
        Image[] m_Stars;
        Text m_Review;
        Button m_Retry;
        Image m_DamageFlash;
        float m_Flash;
        CakeCondition m_SubscribedCake;

        void Start() => Build();

        void OnDestroy()
        {
            CursorLock.ScreenWantsCursor = false;
            if (m_SubscribedCake != null) m_SubscribedCake.Damaged -= OnCakeDamaged;
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("GigHudCanvas", 5);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            m_DamageFlash = UiFactory.CreatePanel("DamageFlash", root, new Color(1f, 0.2f, 0.2f, 0f));
            UiFactory.Stretch(m_DamageFlash.rectTransform);
            m_DamageFlash.raycastTarget = false;

            // Timer, top centre
            m_Timer = UiFactory.CreateText("Timer", root, "", 72);
            m_Timer.fontStyle = FontStyle.Bold;
            UiFactory.Place(m_Timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(600, 90), new Vector2(0, -20));

            // Cake condition, top left
            var label = UiFactory.CreateText("CakeLabel", root, "CAKE", 26, TextAnchor.UpperLeft);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(420, 34), new Vector2(30, -24));
            var barBg = UiFactory.CreatePanel("CakeBar", root, new Color(0, 0, 0, 0.6f));
            UiFactory.Place(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(420, 30), new Vector2(30, -60));
            m_ConditionFill = UiFactory.CreatePanel("Fill", barBg.transform, new Color(0.4f, 0.9f, 0.4f));
            SetupFill(m_ConditionFill);
            m_ConditionLabel = UiFactory.CreateText("Value", barBg.transform, "", 22);
            UiFactory.Stretch(m_ConditionLabel.rectTransform);

            // Objective, bottom centre
            m_Objective = UiFactory.CreateText("Objective", root, "", 30);
            UiFactory.Place(m_Objective.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400, 50), new Vector2(0, 40));

            // Settle progress (cake sitting on the table)
            var settleBg = UiFactory.CreatePanel("SettleBar", root, new Color(0, 0, 0, 0.6f));
            UiFactory.Place(settleBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 22), new Vector2(0, -90));
            m_SettleFill = UiFactory.CreatePanel("Fill", settleBg.transform, new Color(0.4f, 1f, 0.5f));
            SetupFill(m_SettleFill);
            m_SettleBar = settleBg.gameObject;

            BuildBriefing(root);
            BuildResults(root);
        }

        static void SetupFill(Image fill)
        {
            UiFactory.Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(3, 3);
            fill.rectTransform.offsetMax = new Vector2(-3, -3);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        static void SetFill(Image fill, float amount)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        }

        void BuildBriefing(Transform root)
        {
            var card = UiFactory.CreatePanel("Briefing", root, UiFactory.PanelColor);
            UiFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 560), new Vector2(0, 40));
            m_Briefing = card.gameObject;

            var header = UiFactory.CreatePanel("AppHeader", card.transform, new Color(0.98f, 0.55f, 0.2f));
            UiFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 70), Vector2.zero);
            var app = UiFactory.CreateText("App", header.transform, "GigGrabber  |  NEW GIG ACCEPTED", 30);
            app.color = Color.black;
            app.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(app.rectTransform);

            var body = UiFactory.CreateText("Body", card.transform,
                "<b><size=44>Cake Delivery</size></b>\n" +
                "Client: <b>" + CakeReviews.Reviewer + "</b>  (rated 4.9)\n" +
                "Pay: $40 + \"exposure\"\n\n" +
                "\"Three-tier wedding cake. In the van. Up to the top floor. On the table. " +
                "It is VERY expensive and VERY wobbly. Please do not be the freelancers from last time.\"\n\n" +
                "<color=#ffcc66>WASD move · Shift sprint · Space jump · Hold LMB grab · RMB throw · Esc menu</color>\n" +
                "Tip: the cake is heavy. Grab it from both sides.",
                28, TextAnchor.UpperLeft);
            UiFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(820, 400), new Vector2(0, -95));

            m_BriefingCountdown = UiFactory.CreateText("Countdown", card.transform, "", 34);
            m_BriefingCountdown.fontStyle = FontStyle.Bold;
            UiFactory.Place(m_BriefingCountdown.rectTransform, new Vector2(0.5f, 0f), new Vector2(820, 60), new Vector2(0, 20));
        }

        void BuildResults(Transform root)
        {
            var dim = UiFactory.CreatePanel("Results", root, new Color(0, 0, 0, 0.65f));
            UiFactory.Stretch(dim.rectTransform);
            m_Results = dim.gameObject;

            var card = UiFactory.CreatePanel("Card", dim.transform, UiFactory.PanelColor);
            UiFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000, 680), Vector2.zero);

            m_ResultTitle = UiFactory.CreateText("Title", card.transform, "", 64);
            m_ResultTitle.fontStyle = FontStyle.Bold;
            UiFactory.Place(m_ResultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(940, 90), new Vector2(0, -25));

            m_ResultDetail = UiFactory.CreateText("Detail", card.transform, "", 28);
            m_ResultDetail.color = new Color(1, 1, 1, 0.75f);
            UiFactory.Place(m_ResultDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(940, 40), new Vector2(0, -115));

            // Five "stars" (rotated squares, since the built-in font has no star glyph).
            m_Stars = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var star = UiFactory.CreatePanel("Star" + i, card.transform, Dim);
                UiFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(54, 54),
                    new Vector2((i - 2) * 90f, -200));
                star.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                star.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                m_Stars[i] = star;
            }

            m_Review = UiFactory.CreateText("Review", card.transform, "", 32);
            m_Review.fontStyle = FontStyle.Italic;
            UiFactory.Place(m_Review.rectTransform, new Vector2(0.5f, 1f), new Vector2(880, 220), new Vector2(0, -270));

            var row = UiFactory.CreateRect("Buttons", card.transform);
            UiFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(880, 80), new Vector2(0, 30));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            m_Retry = UiFactory.CreateButton("Retry", row, "Retry gig", OnRetry);
            UiFactory.CreateButton("Menu", row, "Back to menu", OnMenu);
        }

        void Update()
        {
            var gig = GigDirector.Current;
            var cake = CakeCondition.Current;
            SubscribeCake(cake);

            bool hasGig = gig != null && gig.IsSpawned;
            m_Timer.gameObject.SetActive(hasGig);
            m_Briefing.SetActive(hasGig && gig.Phase == GigPhase.Briefing);
            bool over = hasGig && gig.IsOver;
            if (m_Results.activeSelf != over)
            {
                m_Results.SetActive(over);
                CursorLock.ScreenWantsCursor = over;
                CursorLock.Refresh();
            }

            if (!hasGig)
            {
                m_Objective.text = "Waiting for the gig to start...";
                m_SettleBar.SetActive(false);
                return;
            }

            // Timer
            float t = gig.Phase == GigPhase.Briefing ? gig.GigSeconds : gig.TimeLeft;
            int secs = Mathf.CeilToInt(t);
            m_Timer.text = $"{secs / 60}:{secs % 60:00}";
            m_Timer.color = gig.Phase == GigPhase.Running && t < 30f
                ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), Mathf.PingPong(Time.time * 2f, 1f))
                : Color.white;
            m_BriefingCountdown.text = $"Clock starts in {Mathf.CeilToInt(gig.BriefingLeft)}...";

            // Cake condition
            float c = cake != null ? cake.Condition01 : (over ? gig.FinalCondition : 0f);
            SetFill(m_ConditionFill, c);
            m_ConditionFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.4f), c);
            m_ConditionLabel.text = cake != null
                ? $"{Mathf.CeilToInt(c * 100)}%   ({cake.TiersLeft}/{cake.TierCount} tiers)"
                : "no cake?!";

            // Objective & delivery progress
            m_SettleBar.SetActive(gig.Phase == GigPhase.Running && gig.SettleProgress > 0f);
            SetFill(m_SettleFill, gig.SettleProgress);
            m_Objective.text = gig.Phase switch
            {
                GigPhase.Briefing => "",
                GigPhase.Running when gig.SettleProgress > 0f => "Let go... don't touch it... Brenda is watching...",
                GigPhase.Running => "Get the cake from the van to the table on the top floor. Set it down upright and LET GO.",
                _ => "",
            };

            if (over) UpdateResults(gig);

            // Red flash when the cake gets hurt
            m_Flash = Mathf.MoveTowards(m_Flash, 0f, Time.deltaTime * 1.5f);
            m_DamageFlash.color = new Color(1f, 0.2f, 0.2f, m_Flash * 0.35f);
        }

        void UpdateResults(GigDirector gig)
        {
            bool delivered = gig.Phase == GigPhase.Delivered;
            m_ResultTitle.text = delivered ? "GIG COMPLETE" : "GIG FAILED";
            m_ResultTitle.color = delivered ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
            m_ResultDetail.text = gig.FailReason switch
            {
                GigFailReason.TimeUp => "Out of time. The wedding started without the cake.",
                GigFailReason.CakeRuined => "The cake is no longer a cake.",
                _ => $"Delivered with {Mathf.CeilToInt(gig.TimeLeft)}s left, cake at {Mathf.CeilToInt(gig.FinalCondition * 100)}%.",
            };
            for (int i = 0; i < m_Stars.Length; i++)
                m_Stars[i].color = i < gig.Stars ? Gold : Dim;
            m_Review.text = $"\"{CakeReviews.Get(gig.ReviewIndex)}\"\n<size=24>— {CakeReviews.Reviewer}</size>";

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            m_Retry.gameObject.SetActive(isHost);
        }

        void SubscribeCake(CakeCondition cake)
        {
            if (cake == m_SubscribedCake) return;
            if (m_SubscribedCake != null) m_SubscribedCake.Damaged -= OnCakeDamaged;
            m_SubscribedCake = cake;
            if (cake != null) cake.Damaged += OnCakeDamaged;
        }

        void OnCakeDamaged(float amount) => m_Flash = Mathf.Clamp01(m_Flash + amount / 25f);

        void OnRetry() => GigDirector.Current?.RequestRestartRpc();

        void OnMenu() => _ = SessionManager.Instance?.LeaveAsync();
    }
}
