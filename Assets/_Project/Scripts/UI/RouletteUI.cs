using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// The roulette betting panel: pick a chip, bet on a colour, odd/even, a half or a number.
    /// Sits on the left so the wheel stays in view. Bets come out of crew funds.
    /// </summary>
    [DefaultExecutionOrder(-50)] // handle Esc before SessionHud does
    public class RouletteUI : MonoBehaviour
    {
        public static RouletteUI Instance { get; private set; }

        /// <summary>True while open, and on the frame Esc closed it (so Esc doesn't also open the pause menu).</summary>
        public static bool UsedEscapeThisFrame =>
            (Instance != null && Instance.IsOpen) || s_EscClosedFrame == Time.frameCount;

        static int s_EscClosedFrame = -1;
        static readonly int[] k_Chips = { 10, 25, 50, 100 };

        static readonly Color Felt = new(0.1f, 0.36f, 0.22f, 0.94f);
        static readonly Color Gold = new(1f, 0.82f, 0.35f);
        static readonly Color Red = new(0.78f, 0.16f, 0.18f);
        static readonly Color Black = new(0.12f, 0.12f, 0.14f);
        static readonly Color Cream = new(0.98f, 0.95f, 0.88f);

        GameObject m_Root;
        RouletteTable m_Table;
        Text m_Funds, m_Status, m_Number, m_NumberBet, m_MyBets;
        Button[] m_ChipButtons;
        Button[] m_BetButtons;
        int m_Chip = 1;
        int m_PickedNumber = 17;

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

        public void Open(RouletteTable table)
        {
            m_Table = table;
            m_Root.SetActive(true);
            CursorLock.ScreenWantsCursor = true;
            CursorLock.Refresh();
            Refresh();
        }

        public void Close()
        {
            m_Root.SetActive(false);
            CursorLock.ScreenWantsCursor = false;
            CursorLock.Refresh();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (m_Table == null || !m_Table.IsSpawned) { Close(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                s_EscClosedFrame = Time.frameCount;
                return;
            }
            Refresh();
        }

        // ---------- Content ----------

        void Refresh()
        {
            var crew = CrewState.Current;
            int funds = crew != null ? crew.Money : 0;
            m_Funds.text = crew != null
                ? $"Crew funds: <b>${funds}</b>   <size=20><color=#FFFFFFAA>(rent ${crew.RentDue} in {HubHud.Gigs(crew.GigsUntilRent)})</color></size>"
                : "";

            var t = m_Table;
            ulong me = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            t.Totals(me, out int staked, out int returned);
            bool betting = t.Phase == RoulettePhase.Betting;

            switch (t.Phase)
            {
                case RoulettePhase.Betting:
                    float left = t.SecondsLeft;
                    m_Status.text = left < 0 ? "Place your bets. The wheel spins after the first one goes down."
                        : $"Place your bets!  Spinning in <b>{Mathf.CeilToInt(left)}</b>...";
                    break;
                case RoulettePhase.Spinning:
                    m_Status.text = "No more bets. Round and round it goes...";
                    break;
                default:
                    int r = t.Result;
                    string line = $"<b><color={RouletteRules.Hex(r)}>{RouletteRules.Describe(r)}</color></b>";
                    if (staked == 0) line += "   (you sat this one out)";
                    else if (returned > 0) line += $"   <color=#7CFF7C>You won ${returned}!</color>";
                    else line += $"   <color=#FF6A5C>Lenny thanks you for the ${staked}.</color>";
                    m_Status.text = line;
                    break;
            }

            int chip = k_Chips[m_Chip];
            for (int i = 0; i < m_ChipButtons.Length; i++)
                m_ChipButtons[i].GetComponent<Image>().color = i == m_Chip ? Gold : new Color(1f, 1f, 1f, 0.85f);
            bool canBet = betting && funds >= chip;
            foreach (var b in m_BetButtons) b.interactable = canBet;

            m_Number.text = m_PickedNumber.ToString();
            m_Number.color = m_PickedNumber == 0 ? new Color(0.4f, 0.85f, 0.5f) : RouletteRules.IsRed(m_PickedNumber) ? new Color(1f, 0.45f, 0.45f) : Color.white;
            m_NumberBet.text = $"${chip} on {m_PickedNumber}  (pays 35:1)";

            var sb = new StringBuilder();
            foreach (var bet in t.Bets)
            {
                if (bet.ClientId != me) continue;
                if (sb.Length > 0) sb.Append(",  ");
                sb.Append($"${bet.Amount} {RouletteRules.Name(bet.Kind, bet.Number)}");
            }
            int tableTotal = 0;
            foreach (var bet in t.Bets) tableTotal += bet.Amount;
            m_MyBets.text = (sb.Length > 0 ? "Your bets: " + sb : "No bets from you yet.") +
                            (tableTotal > staked ? $"\n<size=20>Whole crew on the table: ${tableTotal}</size>" : "");
            if (betting && funds < chip) m_MyBets.text += "\n<color=#FF6A5C>Not enough crew funds for that chip.</color>";
        }

        void Bet(RouletteBetKind kind) => m_Table?.PlaceBet(kind, m_PickedNumber, k_Chips[m_Chip]);

        void StepNumber(int d) => m_PickedNumber = (m_PickedNumber + d + 37) % 37;

        // ---------- Layout ----------

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("RouletteCanvas", 20);
            canvas.transform.SetParent(transform, false);
            var panel = UiFactory.CreatePanel("Panel", canvas.transform, Felt);
            UiFactory.Round(panel, 18f);
            UiFactory.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(560, 860), new Vector2(30, -60)); // left, so the wheel stays in view
            m_Root = panel.gameObject;
            var p = panel.transform;

            var title = UiFactory.CreateText("Title", p, "ROULETTE", 44, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.color = Gold;
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(400, 56), new Vector2(30, -22));
            var sub = UiFactory.CreateText("Sub", p, "Lucky Lenny's  ·  the house always wins (mostly)", 18, TextAnchor.MiddleLeft);
            sub.color = new Color(1f, 1f, 1f, 0.6f);
            UiFactory.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(500, 26), new Vector2(32, -76));
            var close = UiFactory.Solid(UiFactory.CreateButton("Close", p, "X", Close), new Color(1f, 1f, 1f, 0.15f), Color.white);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(56, 50), new Vector2(-20, -22));

            m_Funds = Line("Funds", p, 26, -112, 44);
            m_Status = Line("Status", p, 24, -160, 70);
            m_Status.color = Cream;

            // Chip size.
            var chipLabel = Line("ChipLabel", p, 20, -238, 28);
            chipLabel.text = "CHIP";
            chipLabel.color = new Color(1f, 1f, 1f, 0.6f);
            var chips = Row("Chips", p, -268, 60);
            m_ChipButtons = new Button[k_Chips.Length];
            for (int i = 0; i < k_Chips.Length; i++)
            {
                int index = i;
                var b = UiFactory.CreateButton("Chip" + k_Chips[i], chips, "$" + k_Chips[i], () => m_Chip = index);
                UiFactory.Solid(b, Color.white, Black);
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                m_ChipButtons[i] = b;
            }

            // Outside bets: two to a row.
            var betLabel = Line("BetLabel", p, 20, -346, 28);
            betLabel.text = "EVEN MONEY  (pays 1:1)";
            betLabel.color = new Color(1f, 1f, 1f, 0.6f);
            var bets = new System.Collections.Generic.List<Button>();
            (string, RouletteBetKind, Color, Color)[][] rows =
            {
                new[] { ("RED", RouletteBetKind.Red, Red, Color.white), ("BLACK", RouletteBetKind.Black, Black, Color.white) },
                new[] { ("ODD", RouletteBetKind.Odd, Cream, Black), ("EVEN", RouletteBetKind.Even, Cream, Black) },
                new[] { ("1 - 18", RouletteBetKind.Low, Cream, Black), ("19 - 36", RouletteBetKind.High, Cream, Black) },
            };
            for (int r = 0; r < rows.Length; r++)
            {
                var row = Row("BetRow" + r, p, -376 - r * 72, 62);
                foreach (var (label, kind, bg, fg) in rows[r])
                {
                    var k = kind;
                    var b = UiFactory.Solid(UiFactory.CreateButton(label, row, label, () => Bet(k)), bg, fg);
                    b.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                    bets.Add(b);
                }
            }

            // A single number.
            var numLabel = Line("NumLabel", p, 20, -600, 28);
            numLabel.text = "ONE NUMBER  (pays 35:1)";
            numLabel.color = new Color(1f, 1f, 1f, 0.6f);
            var numRow = UiFactory.CreateRect("NumberRow", p);
            UiFactory.Place(numRow, new Vector2(0f, 1f), new Vector2(500, 64), new Vector2(30, -630));
            var layout = numRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            var down = UiFactory.Solid(UiFactory.CreateButton("Down", numRow, "<", () => StepNumber(-1)), new Color(1f, 1f, 1f, 0.15f), Color.white);
            Width(down, 64);
            m_Number = UiFactory.CreateText("Number", numRow, "17", 40, TextAnchor.MiddleCenter);
            m_Number.fontStyle = FontStyle.Bold;
            Width(m_Number, 80);
            var up = UiFactory.Solid(UiFactory.CreateButton("Up", numRow, ">", () => StepNumber(1)), new Color(1f, 1f, 1f, 0.15f), Color.white);
            Width(up, 64);
            var numBet = UiFactory.Solid(UiFactory.CreateButton("BetNumber", numRow, "", () => Bet(RouletteBetKind.Straight)), Gold, Black);
            m_NumberBet = numBet.GetComponentInChildren<Text>();
            m_NumberBet.fontSize = 22;
            m_NumberBet.fontStyle = FontStyle.Bold;
            var le = numBet.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            bets.Add(numBet);
            m_BetButtons = bets.ToArray();

            m_MyBets = Line("MyBets", p, 22, -716, 110);
            m_MyBets.alignment = TextAnchor.UpperLeft;
            m_MyBets.color = Cream;

            var hint = UiFactory.CreateText("Hint", p, "Esc to step away from the table", 18, TextAnchor.LowerCenter);
            hint.color = new Color(1f, 1f, 1f, 0.45f);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(500, 30), new Vector2(0, 16));
        }

        static Text Line(string name, Transform p, int size, float y, float h)
        {
            var t = UiFactory.CreateText(name, p, "", size, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(500, h), new Vector2(30, y));
            return t;
        }

        static RectTransform Row(string name, Transform p, float y, float h)
        {
            var row = UiFactory.CreateRect(name, p);
            UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(500, h), new Vector2(30, y));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return row;
        }

        static void Width(Component c, float w)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.flexibleWidth = 0;
        }
    }
}
