using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// A small strip at the bottom of the screen while you stand at the roulette table: your chip,
    /// crew funds, your bets and how the round went, plus a big result splash. No menu: all the
    /// betting happens on the table itself. Also tells the table which spot you're aiming at, so
    /// it can glow.
    /// </summary>
    public class RouletteHud : MonoBehaviour
    {
        public static RouletteHud Instance { get; private set; }

        const float ShowWithin = 4.2f;

        GameObject m_Strip;
        Text m_Top, m_Bottom;
        Text m_Splash, m_SplashSub;
        RouletteSpot m_LastHover;

        /// <summary>Showing the strip (standing at the table).</summary>
        public bool Visible => m_Strip != null && m_Strip.activeSelf;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => Build();

        void Update()
        {
            var table = RouletteTable.Current;
            var me = LocalPlayer();
            bool near = table != null && table.IsSpawned && me != null &&
                        Vector3.Distance(me.transform.position, table.transform.position) < ShowWithin;
            bool show = near && !SessionHud.MenuOpen;
            if (m_Strip.activeSelf != show) m_Strip.SetActive(show);

            UpdateHover(table, me, near);
            if (!near)
            {
                m_Splash.gameObject.SetActive(false);
                return;
            }

            var crew = CrewState.Current;
            int funds = crew != null ? crew.Money : 0;
            ulong id = NetworkManager.Singleton.LocalClientId;
            string chipHex = ColorUtility.ToHtmlStringRGB(RouletteTable.PlayerChipColor(id));
            m_Top.text = $"<color=#FFD24A><b>ROULETTE</b></color>   Chip in hand: <b>${RouletteTable.LocalChip}</b>" +
                         $"   <color=#{chipHex}>(your chips)</color>   Crew funds: <b>${funds}</b>";

            table.Totals(id, out int staked, out int returned);
            var mine = new StringBuilder();
            foreach (var bet in table.Bets)
            {
                if (bet.ClientId != id) continue;
                if (mine.Length > 0) mine.Append(", ");
                mine.Append($"${bet.Amount} {RouletteRules.Name(bet.Kind, bet.Number)}");
            }

            switch (table.Phase)
            {
                case RoulettePhase.Betting:
                    float left = table.SecondsLeft;
                    m_Bottom.text = mine.Length == 0
                        ? "Look at the felt and press <b>E</b> to bet.  <b>E</b> on a chip stack to change chip."
                        : $"Your bets: {mine}   ·   <b>E</b> on the wheel to spin" + (left >= 0 ? $" (spins itself in {Mathf.CeilToInt(left)})" : "");
                    break;
                case RoulettePhase.Spinning:
                    m_Bottom.text = mine.Length == 0 ? "No more bets. Round and round it goes..." : $"No more bets.   Your bets: {mine}";
                    break;
                default:
                    int r = table.Result;
                    string res = $"<color={RouletteRules.Hex(r)}><b>{RouletteRules.Describe(r)}</b></color>";
                    m_Bottom.text = staked == 0 ? $"{res}   (you sat this one out)"
                        : returned > 0 ? $"{res}   <color=#7CFF7C>You won ${returned}!</color>"
                        : $"{res}   <color=#FF6A5C>Lenny thanks you for the ${staked}.</color>";
                    break;
            }

            // Big splash in the middle when the ball lands.
            bool payout = table.Phase == RoulettePhase.Payout;
            m_Splash.gameObject.SetActive(payout);
            if (payout)
            {
                int r = table.Result;
                m_Splash.text = $"<color={RouletteRules.Hex(r)}>{RouletteRules.Describe(r)}</color>";
                m_SplashSub.text = staked == 0 ? "" : returned > 0 ? $"<color=#7CFF7C>+${returned}</color>" : $"<color=#FF6A5C>-${staked}</color>";
                float pop = 1f + Mathf.Max(0f, 0.25f - (table.SecondsLeft < 0 ? 0f : 5f - table.SecondsLeft) * 0.5f);
                m_Splash.rectTransform.localScale = Vector3.one * pop;
            }
        }

        /// <summary>Same ray as the E prompt: which betting spot is under the crosshair.</summary>
        void UpdateHover(RouletteTable table, PlayerMotor me, bool near)
        {
            RouletteSpot spot = null;
            if (near && CursorLock.GameplayInputAllowed && me.Camera != null)
            {
                var cam = me.Camera.transform;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, PlayerInteractor.Reach, ~0, QueryTriggerInteraction.Collide))
                    spot = hit.collider.GetComponent<RouletteSpot>();
            }
            if (spot == m_LastHover) return;
            m_LastHover = spot;
            if (table != null) table.SetHover(spot);
        }

        static PlayerMotor LocalPlayer()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && PlayerMotor.ByClientId.TryGetValue(nm.LocalClientId, out var me) ? me : null;
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("RouletteHudCanvas", 6);
            canvas.transform.SetParent(transform, false);

            var strip = UiFactory.CreatePanel("Strip", canvas.transform, new Color(0.06f, 0.2f, 0.12f, 0.82f));
            UiFactory.Round(strip, 14f);
            UiFactory.Place(strip.rectTransform, new Vector2(0.5f, 0f), new Vector2(1240, 104), new Vector2(0, 26));
            strip.raycastTarget = false;
            m_Strip = strip.gameObject;
            m_Top = UiFactory.CreateText("Top", strip.transform, "", 26, TextAnchor.MiddleCenter);
            UiFactory.Place(m_Top.rectTransform, new Vector2(0.5f, 1f), new Vector2(1200, 44), new Vector2(0, -8));
            m_Bottom = UiFactory.CreateText("Bottom", strip.transform, "", 24, TextAnchor.MiddleCenter);
            m_Bottom.color = new Color(1f, 1f, 1f, 0.9f);
            UiFactory.Place(m_Bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(1200, 44), new Vector2(0, 10));
            m_Strip.SetActive(false);

            m_Splash = UiFactory.CreateText("Splash", canvas.transform, "", 88, TextAnchor.MiddleCenter);
            m_Splash.fontStyle = FontStyle.Bold;
            var shadow = m_Splash.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(4, -4);
            UiFactory.Place(m_Splash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000, 120), new Vector2(0, 230));
            m_SplashSub = UiFactory.CreateText("SplashSub", m_Splash.transform, "", 56, TextAnchor.MiddleCenter);
            m_SplashSub.fontStyle = FontStyle.Bold;
            m_SplashSub.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            UiFactory.Place(m_SplashSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(600, 80), new Vector2(0, -80));
            m_Splash.gameObject.SetActive(false);
        }
    }
}
