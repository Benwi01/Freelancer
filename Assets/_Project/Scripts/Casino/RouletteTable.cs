using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    public enum RoulettePhase : byte
    {
        Betting,
        Spinning,
        Payout,
    }

    /// <summary>
    /// Lucky Lenny's roulette table. Server-authoritative: bets come out of crew funds, the first
    /// bet starts a short countdown, the server picks the number, and winnings go back into crew
    /// funds. Everyone animates the wheel and ball from the synced spin start time and result.
    /// </summary>
    public class RouletteTable : NetworkBehaviour, IInteractable
    {
        public static RouletteTable Current { get; private set; }

        [Header("Rules")]
        [SerializeField] float bettingSeconds = 12f;
        [SerializeField] float spinSeconds = 6f;
        [SerializeField] float payoutSeconds = 5f;
        [SerializeField] int maxBet = 250;

        [Header("Visuals")]
        [SerializeField] Transform wheel;
        [SerializeField] Transform ball;
        [Tooltip("Ball track radius, and where it settles in a pocket.")]
        [SerializeField] float rimRadius = 0.42f;
        [SerializeField] float pocketRadius = 0.3f;
        [SerializeField] float wheelSpeed = 30f;
        [SerializeField] TMP_Text board;
        [SerializeField] Transform chipRoot;
        [Tooltip("Chip spots for Red, Black, Odd, Even, 1-18, 19-36 (in RouletteBetKind order).")]
        [SerializeField] Transform[] outsideSpots;
        [Tooltip("Chip spot for each number, 0-36.")]
        [SerializeField] Transform[] numberCells;

        readonly NetworkVariable<RoulettePhase> m_Phase = new(RoulettePhase.Betting);
        readonly NetworkVariable<double> m_PhaseEndsAt = new(0);   // 0 = waiting for the first bet
        readonly NetworkVariable<int> m_Result = new(-1);
        readonly NetworkVariable<double> m_SpinStartedAt = new(0);
        readonly NetworkVariable<int> m_LastStaked = new(0);
        readonly NetworkVariable<int> m_LastReturned = new(0);
        NetworkList<RouletteBet> m_Bets;
        NetworkList<int> m_History;

        readonly List<GameObject> m_Chips = new();
        Material m_ChipMat;
        MaterialPropertyBlock m_Block;
        bool m_ChipsDirty = true;
        string m_ShownBoard;

        public RoulettePhase Phase => m_Phase.Value;
        public int Result => m_Result.Value;
        public int MaxBet => maxBet;
        public NetworkList<RouletteBet> Bets => m_Bets;
        public int LastStaked => m_LastStaked.Value;
        public int LastReturned => m_LastReturned.Value;
        /// <summary>Seconds until the current phase ends (betting: -1 while waiting for the first bet).</summary>
        public float SecondsLeft => m_PhaseEndsAt.Value <= 0 ? -1f : Mathf.Max(0f, (float)(m_PhaseEndsAt.Value - Now));

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0;

        public string Prompt => "Play roulette";

        public void Interact(PlayerMotor player)
        {
            if (RouletteUI.Instance != null) RouletteUI.Instance.Open(this);
        }

        void Awake()
        {
            m_Bets = new NetworkList<RouletteBet>();
            m_History = new NetworkList<int>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            m_Bets.OnListChanged += _ => m_ChipsDirty = true;
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        // ---------- Betting ----------

        /// <summary>Any machine: put a chip down for the local player.</summary>
        public void PlaceBet(RouletteBetKind kind, int number, int amount) => PlaceBetRpc(kind, (byte)Mathf.Clamp(number, 0, 36), amount);

        [Rpc(SendTo.Server)]
        void PlaceBetRpc(RouletteBetKind kind, byte number, int amount, RpcParams rpcParams = default) =>
            ServerPlaceBet(rpcParams.Receive.SenderClientId, kind, number, amount);

        /// <summary>Server: take the money and put the chips down. False if not allowed or not affordable.</summary>
        public bool ServerPlaceBet(ulong clientId, RouletteBetKind kind, int number, int amount)
        {
            if (!IsServer || m_Phase.Value != RoulettePhase.Betting) return false;
            if (amount <= 0 || amount > maxBet || number < 0 || number > 36) return false;
            var crew = CrewState.Current;
            if (crew == null || !crew.TrySpend(amount)) return false;

            m_Bets.Add(new RouletteBet { ClientId = clientId, Kind = kind, Number = (byte)number, Amount = amount });
            if (m_PhaseEndsAt.Value <= 0) m_PhaseEndsAt.Value = Now + bettingSeconds;
            return true;
        }

        void Update()
        {
            if (IsServer && IsSpawned) ServerTick();
            if (IsSpawned) Animate();
        }

        void ServerTick()
        {
            if (m_PhaseEndsAt.Value <= 0 || Now < m_PhaseEndsAt.Value) return;
            switch (m_Phase.Value)
            {
                case RoulettePhase.Betting:
                    m_Result.Value = Random.Range(0, 37);
                    m_SpinStartedAt.Value = Now;
                    m_PhaseEndsAt.Value = Now + spinSeconds;
                    m_Phase.Value = RoulettePhase.Spinning;
                    break;

                case RoulettePhase.Spinning:
                    int staked = 0, returned = 0;
                    foreach (var bet in m_Bets)
                    {
                        staked += bet.Amount;
                        returned += RouletteRules.Returned(bet, m_Result.Value);
                    }
                    CrewState.Current?.Receive(returned);
                    m_LastStaked.Value = staked;
                    m_LastReturned.Value = returned;
                    m_History.Insert(0, m_Result.Value);
                    while (m_History.Count > 10) m_History.RemoveAt(m_History.Count - 1);
                    m_PhaseEndsAt.Value = Now + payoutSeconds;
                    m_Phase.Value = RoulettePhase.Payout;
                    break;

                case RoulettePhase.Payout:
                    m_Bets.Clear();
                    m_PhaseEndsAt.Value = 0;
                    m_Phase.Value = RoulettePhase.Betting;
                    break;
            }
        }

        /// <summary>What a player has on the table this round, and what it brought back (after the spin).</summary>
        public void Totals(ulong clientId, out int staked, out int returned)
        {
            staked = returned = 0;
            foreach (var bet in m_Bets)
            {
                if (bet.ClientId != clientId) continue;
                staked += bet.Amount;
                if (m_Phase.Value == RoulettePhase.Payout) returned += RouletteRules.Returned(bet, m_Result.Value);
            }
        }

        // ---------- Everyone: wheel, ball, chips, board ----------

        float WheelAngle(double time) => (float)(time * wheelSpeed % 360.0);

        void Animate()
        {
            double now = Now;
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, WheelAngle(now), 0f);

            if (ball != null && wheel != null)
            {
                int result = m_Result.Value;
                bool show = result >= 0;
                if (ball.gameObject.activeSelf != show) ball.gameObject.SetActive(show);
                if (show)
                {
                    float settled = WheelAngle(now) + RouletteRules.PocketAngle(result);
                    float angle = settled, radius = pocketRadius, hop = 0f;
                    if (m_Phase.Value == RoulettePhase.Spinning)
                    {
                        // Spins the other way round the rim, slows down, then drops into its pocket.
                        float u = Mathf.Clamp01((float)((now - m_SpinStartedAt.Value) / spinSeconds));
                        double end = m_SpinStartedAt.Value + spinSeconds;
                        float final = WheelAngle(end) + RouletteRules.PocketAngle(result);
                        float drop = Mathf.InverseLerp(0.68f, 0.9f, u);
                        angle = Mathf.Lerp(final + 360f * 7f * (1f - u) * (1f - u), settled, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, u)));
                        radius = Mathf.Lerp(rimRadius, pocketRadius, drop);
                        hop = drop > 0f && drop < 1f ? Mathf.Abs(Mathf.Sin(drop * Mathf.PI * 3f)) * 0.03f * (1f - drop) : 0f;
                    }
                    float a = angle * Mathf.Deg2Rad;
                    Vector3 center = wheel.localPosition;
                    float y = Mathf.Lerp(0.035f, 0.02f, Mathf.InverseLerp(rimRadius, pocketRadius, radius));
                    ball.localPosition = center + new Vector3(Mathf.Sin(a) * radius, y + hop, Mathf.Cos(a) * radius);
                }
            }

            if (m_ChipsDirty) RebuildChips();
            UpdateBoard();
        }

        static readonly Color[] k_ChipColors =
        {
            new(0.95f, 0.75f, 0.2f), new(0.25f, 0.55f, 0.95f), new(0.9f, 0.3f, 0.35f), new(0.35f, 0.8f, 0.45f),
        };

        void RebuildChips()
        {
            m_ChipsDirty = false;
            foreach (var c in m_Chips) c.SetActive(false);
            if (chipRoot == null) return;

            var heights = new Dictionary<Transform, int>();
            int used = 0;
            foreach (var bet in m_Bets)
            {
                Transform spot = bet.Kind == RouletteBetKind.Straight
                    ? (numberCells != null && bet.Number < numberCells.Length ? numberCells[bet.Number] : null)
                    : (outsideSpots != null && (int)bet.Kind < outsideSpots.Length ? outsideSpots[(int)bet.Kind] : null);
                if (spot == null) continue;
                heights.TryGetValue(spot, out int h);
                int count = Mathf.Clamp(bet.Amount / 25 + 1, 1, 6);
                for (int i = 0; i < count; i++, h++)
                {
                    var chip = Chip(used++);
                    chip.transform.position = spot.position + spot.up * (0.008f + h * 0.012f) +
                                              new Vector3(((int)(bet.ClientId % 2)) * 0.02f - 0.01f, 0f, ((int)(bet.ClientId / 2 % 2)) * 0.02f - 0.01f);
                    m_Block ??= new MaterialPropertyBlock();
                    m_Block.SetColor("_BaseColor", k_ChipColors[(int)(bet.ClientId % (ulong)k_ChipColors.Length)]);
                    chip.GetComponent<Renderer>().SetPropertyBlock(m_Block);
                    chip.SetActive(true);
                }
                heights[spot] = h;
            }
        }

        GameObject Chip(int index)
        {
            while (m_Chips.Count <= index)
            {
                var chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(chip.GetComponent<Collider>());
                chip.name = "Chip";
                chip.transform.SetParent(chipRoot, false);
                chip.transform.localScale = new Vector3(0.06f, 0.006f, 0.06f);
                if (m_ChipMat == null) m_ChipMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                chip.GetComponent<Renderer>().sharedMaterial = m_ChipMat;
                m_Chips.Add(chip);
            }
            return m_Chips[index];
        }

        void UpdateBoard()
        {
            if (board == null) return;
            var sb = new StringBuilder();
            switch (m_Phase.Value)
            {
                case RoulettePhase.Betting:
                    float left = SecondsLeft;
                    sb.Append(left < 0 ? "<b>PLACE YOUR BETS</b>" : $"<b>PLACE YOUR BETS</b>  <color=#FFD24A>{Mathf.CeilToInt(left)}</color>");
                    break;
                case RoulettePhase.Spinning:
                    sb.Append("<b>NO MORE BETS</b>");
                    break;
                default:
                    int r = m_Result.Value;
                    sb.Append($"<b><color={RouletteRules.Hex(r)}>{RouletteRules.Describe(r)}</color></b>");
                    break;
            }
            sb.Append("\n<size=60%>");
            if (m_History.Count == 0) sb.Append("Lucky Lenny's · bets from crew funds");
            for (int i = 0; i < m_History.Count; i++)
                sb.Append($"<color={RouletteRules.Hex(m_History[i])}>{m_History[i]}</color>  ");
            sb.Append("</size>");
            string text = sb.ToString();
            if (text != m_ShownBoard)
            {
                m_ShownBoard = text;
                board.text = text;
            }
        }
    }
}
