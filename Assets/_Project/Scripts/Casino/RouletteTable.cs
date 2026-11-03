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
    /// Lucky Lenny's roulette table, played entirely on the table: E on a chip stack picks your
    /// chip, E on a spot of the felt bets it (R takes it back), E on the wheel spins (or it spins by
    /// itself when the countdown that the first bet starts runs out).
    ///
    /// The ball is real physics, simulated on the server: launched round the rim against the
    /// spinning wheel, it slows, rolls down the track, rattles off deflectors and frets, and the
    /// pocket it comes to rest in is the result. Everyone computes the wheel's angle from the same
    /// synced clock; the ball's flight is synced, and once it lands every machine sits it in that
    /// pocket, so all screens agree.
    /// Bets come out of crew funds and winnings go back into them.
    /// </summary>
    public class RouletteTable : NetworkBehaviour
    {
        public static RouletteTable Current { get; private set; }

        /// <summary>The chip size this machine's player bets with (picked from the stacks on the rail).</summary>
        public static int LocalChip = 25;

        [Header("Rules")]
        [Tooltip("Spins by itself this long after the first bet, if nobody spins it.")]
        [SerializeField] float bettingSeconds = 25f;
        [SerializeField] float payoutSeconds = 5f;
        [SerializeField] int maxBet = 250;

        [Header("Wheel")]
        [SerializeField] Transform wheel;
        [SerializeField] Rigidbody wheelBody;
        [Tooltip("Idle turning speed, degrees per second.")]
        [SerializeField] float wheelSpeed = 30f;
        [Tooltip("Extra speed a spin gives the wheel, fading out over spinBoostTime.")]
        [SerializeField] float spinBoost = 200f;
        [SerializeField] float spinBoostTime = 3f;

        [Header("Ball (server)")]
        [SerializeField] Transform ball;
        [SerializeField] Rigidbody ballBody;
        [Tooltip("The ball circles the rim here (radius, and height above the wheel centre) until it's too slow to stay up.")]
        [SerializeField] float launchRadius = 0.49f;
        [SerializeField] float launchHeight = 0.146f;
        [SerializeField] Vector2 launchSpeed = new(2.7f, 3.4f);
        [Tooltip("Slowing on the rim: drag (per second, times speed) plus a constant rolling loss (m/s²).")]
        [SerializeField] float rimDrag = 0.12f;
        [SerializeField] float rimFriction = 0.22f;
        [Tooltip("Below this speed the rim can't hold the ball and physics takes over.")]
        [SerializeField] float fallSpeed = 1.25f;
        [Tooltip("Resting spot in a pocket (wheel space).")]
        [SerializeField] float pocketRadius = 0.3f;
        [SerializeField] float restHeight = 0.0175f;
        [Tooltip("Ball counts as in a pocket inside this radius, moving with the wheel slower than settleSpeed for settleTime.")]
        [SerializeField] float settleRadius = 0.36f;
        [SerializeField] float settleSpeed = 0.07f;
        [SerializeField] float settleTime = 0.5f;
        [Tooltip("Give up waiting and read the nearest pocket after this long.")]
        [SerializeField] float maxSpinSeconds = 16f;

        [Header("Table")]
        [SerializeField] TMP_Text board;
        [SerializeField] Transform chipRoot;
        [Tooltip("Chip spots for Red, Black, Odd, Even, 1-18, 19-36 (in RouletteBetKind order).")]
        [SerializeField] Transform[] outsideSpots;
        [Tooltip("Chip spot for each number, 0-36.")]
        [SerializeField] Transform[] numberCells;
        [Tooltip("Glow over the spot you're aiming at (local only).")]
        [SerializeField] Transform hoverMarker;
        [Tooltip("Glow over the winning number during the payout.")]
        [SerializeField] Transform winMarker;
        [SerializeField] RouletteChipStack[] chipStacks;

        readonly NetworkVariable<RoulettePhase> m_Phase = new(RoulettePhase.Betting);
        readonly NetworkVariable<double> m_PhaseEndsAt = new(0);   // 0 = waiting for the first bet
        readonly NetworkVariable<int> m_Result = new(-1);
        readonly NetworkVariable<double> m_SpinStartedAt = new(0);
        readonly NetworkVariable<float> m_WheelOffset = new(0f);   // angle carried over from earlier spins
        readonly NetworkVariable<int> m_SpunBy = new(-1);          // client id + 1 who spun it (0 = it spun itself)
        readonly NetworkVariable<int> m_LastStaked = new(0);
        readonly NetworkVariable<int> m_LastReturned = new(0);
        NetworkList<RouletteBet> m_Bets;
        NetworkList<int> m_History;

        readonly List<GameObject> m_Chips = new();
        Material m_ChipMat;
        MaterialPropertyBlock m_Block;
        bool m_ChipsDirty = true;
        string m_ShownBoard;
        RouletteSpot m_Hover;
        readonly Dictionary<RouletteChipStack, Vector3> m_StackHome = new();
        Renderer m_BallRenderer;
        float m_SettledFor;
        bool m_OnRim;
        float m_RimAngle, m_RimSpeed;
        float m_NextTrace;

        /// <summary>Dev: log the ball's flight while it spins.</summary>
        public static bool Trace;

        public RoulettePhase Phase => m_Phase.Value;
        public int Result => m_Result.Value;
        public int MaxBet => maxBet;
        public NetworkList<RouletteBet> Bets => m_Bets;
        public int LastStaked => m_LastStaked.Value;
        public int LastReturned => m_LastReturned.Value;
        /// <summary>Who spun it: a player label like "P2", or "" if it spun by itself.</summary>
        public string SpunBy => m_SpunBy.Value > 0 ? "P" + m_SpunBy.Value : "";
        /// <summary>Seconds until the current phase ends (betting: -1 while waiting for the first bet).</summary>
        public float SecondsLeft => m_PhaseEndsAt.Value <= 0 ? -1f : Mathf.Max(0f, (float)(m_PhaseEndsAt.Value - Now));

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0;

        void Awake()
        {
            m_Bets = new NetworkList<RouletteBet>();
            m_History = new NetworkList<int>();
            if (ball != null) m_BallRenderer = ball.GetComponentInChildren<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            m_Bets.OnListChanged += _ => m_ChipsDirty = true;
            if (chipStacks != null)
                foreach (var s in chipStacks)
                    if (s != null) m_StackHome[s] = s.transform.localPosition;
            if (IsServer) RestBall(0);
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        // ---------- What the prompts say ----------

        public string SpotPrompt(RouletteBetKind kind, int number)
        {
            string name = kind == RouletteBetKind.Straight ? number.ToString() : RouletteRules.Name(kind, number);
            string pays = kind == RouletteBetKind.Straight ? "pays 35:1" : "pays 1:1";
            if (m_Phase.Value != RoulettePhase.Betting) return $"{name}  ·  no more bets";
            var crew = CrewState.Current;
            if (crew != null && crew.Money < LocalChip) return $"{name}  ·  crew can't cover a ${LocalChip} chip";
            return $"Bet ${LocalChip} on {name}  ({pays})";
        }

        public string WheelPrompt => m_Phase.Value switch
        {
            RoulettePhase.Betting when m_Bets.Count > 0 => "SPIN THE WHEEL!",
            RoulettePhase.Betting => "Spin the wheel (bet first: look at the felt)",
            RoulettePhase.Spinning => "Round and round it goes...",
            _ => RouletteRules.Describe(m_Result.Value),
        };

        /// <summary>"Take back $25" if this machine's player has chips on that spot (before the spin).</summary>
        public string TakeBackPrompt(RouletteBetKind kind, int number)
        {
            int mine = MineOn(kind, number, all: false);
            return mine > 0 ? $"Take back ${mine}" : "";
        }

        public string TakeBackAllPrompt
        {
            get
            {
                int mine = MineOn(RouletteBetKind.Red, 0, all: true);
                return mine > 0 ? $"Take back all your bets (${mine})" : "";
            }
        }

        int MineOn(RouletteBetKind kind, int number, bool all)
        {
            if (m_Phase.Value != RoulettePhase.Betting || NetworkManager == null) return 0;
            ulong me = NetworkManager.LocalClientId;
            int sum = 0;
            foreach (var bet in m_Bets)
                if (bet.ClientId == me && (all || Same(bet, kind, number))) sum += bet.Amount;
            return sum;
        }

        static bool Same(RouletteBet bet, RouletteBetKind kind, int number) =>
            bet.Kind == kind && (kind != RouletteBetKind.Straight || bet.Number == number);

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

        /// <summary>Any machine: pick your chips back up off a spot (or everything, with <paramref name="all"/>).</summary>
        public void TakeBack(RouletteBetKind kind, int number, bool all) => TakeBackRpc(kind, (byte)Mathf.Clamp(number, 0, 36), all);

        [Rpc(SendTo.Server)]
        void TakeBackRpc(RouletteBetKind kind, byte number, bool all, RpcParams rpcParams = default) =>
            ServerTakeBack(rpcParams.Receive.SenderClientId, kind, number, all);

        /// <summary>Server: give a player's chips back (only before the spin). Returns the refund.</summary>
        public int ServerTakeBack(ulong clientId, RouletteBetKind kind, int number, bool all)
        {
            if (!IsServer || m_Phase.Value != RoulettePhase.Betting) return 0;
            int refund = 0;
            for (int i = m_Bets.Count - 1; i >= 0; i--)
            {
                var bet = m_Bets[i];
                if (bet.ClientId != clientId || !(all || Same(bet, kind, number))) continue;
                refund += bet.Amount;
                m_Bets.RemoveAt(i);
            }
            if (refund > 0) CrewState.Current?.Receive(refund);
            if (m_Bets.Count == 0) m_PhaseEndsAt.Value = 0; // table's empty again: no countdown
            return refund;
        }

        // ---------- Spinning ----------

        /// <summary>Any machine: give the wheel a spin.</summary>
        public void RequestSpin() => SpinRpc();

        [Rpc(SendTo.Server)]
        void SpinRpc(RpcParams rpcParams = default) => ServerSpin(rpcParams.Receive.SenderClientId);

        /// <summary>Server: spin now, if there's anything on the table.</summary>
        public bool ServerSpin(ulong clientId)
        {
            if (!IsServer || m_Phase.Value != RoulettePhase.Betting || m_Bets.Count == 0) return false;
            StartSpin((int)clientId + 1);
            return true;
        }

        void StartSpin(int spunBy)
        {
            double now = Now;
            // Keep the wheel's angle continuous: fold the last spin's extra turn into the offset.
            if (m_SpinStartedAt.Value > 0) m_WheelOffset.Value = (float)((m_WheelOffset.Value + Boost(now - m_SpinStartedAt.Value)) % 360.0);
            m_SpinStartedAt.Value = now;
            m_Result.Value = -1;
            m_SpunBy.Value = spunBy;
            m_PhaseEndsAt.Value = now + maxSpinSeconds;
            m_Phase.Value = RoulettePhase.Spinning;
            LaunchBall();
        }

        /// <summary>Extra wheel angle (degrees) a spin has added after <paramref name="dt"/> seconds.</summary>
        double Boost(double dt) => dt <= 0 ? 0 : spinBoost * spinBoostTime * (1.0 - System.Math.Exp(-dt / spinBoostTime));

        float WheelAngle(double t)
        {
            double a = t * wheelSpeed + m_WheelOffset.Value;
            if (m_SpinStartedAt.Value > 0) a += Boost(t - m_SpinStartedAt.Value);
            return (float)(a % 360.0);
        }

        /// <summary>Wheel speed right now, degrees per second.</summary>
        float WheelSpeedNow(double t)
        {
            double dt = t - m_SpinStartedAt.Value;
            return wheelSpeed + (m_SpinStartedAt.Value > 0 && dt > 0 ? (float)(spinBoost * System.Math.Exp(-dt / spinBoostTime)) : 0f);
        }

        /// <summary>
        /// Server: send the ball round the rim, against the wheel. On the rim it's moved exactly
        /// (a fixed-step physics ball against a curved wall loses speed every step, far faster than a
        /// real one), slowing with drag; once it's too slow to stay up, physics takes over for the
        /// drop down the track, the deflectors, the frets and the pocket.
        /// </summary>
        void LaunchBall()
        {
            if (ballBody == null || wheel == null) return;
            ballBody.isKinematic = true;
            m_OnRim = true;
            m_RimAngle = Random.Range(0f, Mathf.PI * 2f);
            m_RimSpeed = Random.Range(launchSpeed.x, launchSpeed.y);
            m_SettledFor = 0f;
            ballBody.position = RimPoint(m_RimAngle);
            ball.position = ballBody.position;
        }

        Vector3 RimPoint(float angle) =>
            transform.TransformPoint(wheel.localPosition + new Vector3(Mathf.Sin(angle) * launchRadius, launchHeight, Mathf.Cos(angle) * launchRadius));

        /// <summary>Server, each physics step while the ball circles the rim.</summary>
        void RideRim()
        {
            float dt = Time.fixedDeltaTime;
            m_RimSpeed = Mathf.Max(0f, m_RimSpeed - (rimDrag * m_RimSpeed + rimFriction) * dt);
            m_RimAngle -= m_RimSpeed / launchRadius * dt; // the opposite way to the wheel
            if (m_RimSpeed > fallSpeed)
            {
                ballBody.MovePosition(RimPoint(m_RimAngle));
                return;
            }

            // Too slow to hold the rim: let go, moving the way it was, with a nudge down the slope.
            var tangent = -new Vector3(Mathf.Cos(m_RimAngle), 0f, -Mathf.Sin(m_RimAngle));
            var inward = -new Vector3(Mathf.Sin(m_RimAngle), 0f, Mathf.Cos(m_RimAngle));
            m_OnRim = false;
            ballBody.isKinematic = false;
            ballBody.linearVelocity = transform.TransformDirection(tangent * m_RimSpeed + inward * 0.15f);
            ballBody.angularVelocity = Vector3.zero;
        }

        /// <summary>Server: stop the ball's physics; from now on it's placed in its pocket every frame.</summary>
        void RestBall(int pocketIndex)
        {
            if (ballBody == null) return;
            if (!ballBody.isKinematic)
            {
                ballBody.linearVelocity = Vector3.zero;
                ballBody.angularVelocity = Vector3.zero;
                ballBody.isKinematic = true;
            }
            PlaceInPocket(pocketIndex);
        }

        /// <summary>
        /// Put the ball in a pocket at the wheel's current angle. The ball is a child of the table, not
        /// the wheel (a moving parent would drag a physics ball around), so once it has landed every
        /// machine places it here each frame from the shared wheel clock.
        /// </summary>
        void PlaceInPocket(int pocketIndex)
        {
            if (ball == null || wheel == null) return;
            float a = (WheelAngle(Now) + pocketIndex * 360f / RouletteRules.WheelOrder.Length) * Mathf.Deg2Rad;
            ball.localPosition = wheel.localPosition + new Vector3(Mathf.Sin(a) * pocketRadius, restHeight, Mathf.Cos(a) * pocketRadius);
        }

        void LateUpdate()
        {
            // After the network transform has had its say: outside a spin the ball sits in its pocket.
            if (!IsSpawned || m_Phase.Value == RoulettePhase.Spinning || m_Result.Value < 0) return;
            PlaceInPocket(System.Array.IndexOf(RouletteRules.WheelOrder, m_Result.Value));
        }

        /// <summary>Server: the ball has stopped (or we've waited long enough). Read its pocket and pay out.</summary>
        void Settle(bool forced = false)
        {
            int n = RouletteRules.WheelOrder.Length;
            Vector3 local = Quaternion.Inverse(wheelBody != null ? wheelBody.rotation : wheel.rotation) *
                            (ballBody.position - (wheelBody != null ? wheelBody.position : wheel.position));
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            int index = ((Mathf.RoundToInt(angle / (360f / n)) % n) + n) % n;
            int result = RouletteRules.WheelOrder[index];
            m_OnRim = false;
            Debug.Log($"[Freelancer] Roulette ball settled in {RouletteRules.Describe(result)} after {Now - m_SpinStartedAt.Value:0.0}s" +
                      (forced ? " (forced: didn't come to rest)" : ""));
            RestBall(index);

            int staked = 0, returned = 0;
            foreach (var bet in m_Bets)
            {
                staked += bet.Amount;
                returned += RouletteRules.Returned(bet, result);
            }
            CrewState.Current?.Receive(returned);
            m_Result.Value = result;
            m_LastStaked.Value = staked;
            m_LastReturned.Value = returned;
            m_History.Insert(0, result);
            while (m_History.Count > 10) m_History.RemoveAt(m_History.Count - 1);
            m_PhaseEndsAt.Value = Now + payoutSeconds;
            m_Phase.Value = RoulettePhase.Payout;
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;
            if (wheelBody != null) wheelBody.MoveRotation(transform.rotation * Quaternion.Euler(0f, WheelAngle(Now), 0f));
            if (m_Phase.Value != RoulettePhase.Spinning || ballBody == null) return;
            if (m_OnRim) RideRim();
            else if (!ballBody.isKinematic) WatchBall();
        }

        /// <summary>Server: is the ball sitting in a pocket, riding along with the wheel?</summary>
        void WatchBall()
        {
            Vector3 center = transform.TransformPoint(wheel.localPosition);
            Vector3 offset = transform.InverseTransformDirection(ballBody.position - center);
            float r = new Vector2(offset.x, offset.z).magnitude;

            // Flew off somewhere it shouldn't: read the nearest pocket rather than wait.
            if (r > 0.8f || offset.y < -0.3f || offset.y > 0.6f)
            {
                Debug.LogWarning($"[Freelancer] Roulette ball left the wheel (r={r:0.00}, y={offset.y:0.00}); reading the nearest pocket.");
                Settle(forced: true);
                return;
            }

            if (Trace && Time.time >= m_NextTrace)
            {
                double since = Now - m_SpinStartedAt.Value;
                m_NextTrace = Time.time + (since < 0.4 ? 0f : 0.25f);
                Vector3 v = transform.InverseTransformDirection(ballBody.linearVelocity);
                float radial = Vector3.Dot(new Vector3(v.x, 0f, v.z), new Vector3(offset.x, 0f, offset.z).normalized);
                Debug.Log($"[Freelancer] Roulette trace t={since:0.000} r={r:0.000} y={offset.y:0.000} speed={v.magnitude:0.00} radial={radial:0.00} vy={v.y:0.00}");
            }

            float omega = WheelSpeedNow(Now) * Mathf.Deg2Rad;
            Vector3 surface = transform.TransformDirection(Vector3.Cross(Vector3.up * omega, new Vector3(offset.x, 0f, offset.z)));
            float relative = (ballBody.linearVelocity - surface).magnitude;
            bool inPocket = r < settleRadius && offset.y < restHeight + 0.03f;
            m_SettledFor = inPocket && relative < settleSpeed ? m_SettledFor + Time.fixedDeltaTime : 0f;
            if (m_SettledFor >= settleTime) Settle();
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
                    StartSpin(0);
                    break;
                case RoulettePhase.Spinning:
                    Settle(forced: true); // took too long: whichever pocket it's nearest
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

        /// <summary>Local: the spot this machine's player is aiming at (null for none).</summary>
        public void SetHover(RouletteSpot spot) => m_Hover = spot;

        // ---------- Everyone: wheel, ball, chips, glows, board ----------

        void Animate()
        {
            // The server's wheel is turned by physics (FixedUpdate); everyone else sets it from the clock.
            if (wheel != null && !IsServer) wheel.localRotation = Quaternion.Euler(0f, WheelAngle(Now), 0f);
            if (m_BallRenderer != null)
            {
                bool show = m_Phase.Value == RoulettePhase.Spinning || m_Result.Value >= 0;
                if (m_BallRenderer.enabled != show) m_BallRenderer.enabled = show;
            }

            // Hover glow on the spot you're aiming at, only while bets are open.
            if (hoverMarker != null)
            {
                bool show = m_Hover != null && m_Phase.Value == RoulettePhase.Betting;
                if (hoverMarker.gameObject.activeSelf != show) hoverMarker.gameObject.SetActive(show);
                if (show) Cover(hoverMarker, m_Hover.transform, 0.006f);
            }

            // The winning number glows (pulsing) through the payout.
            if (winMarker != null)
            {
                int r = m_Result.Value;
                bool show = m_Phase.Value == RoulettePhase.Payout && r >= 0 && numberCells != null && r < numberCells.Length;
                if (winMarker.gameObject.activeSelf != show) winMarker.gameObject.SetActive(show);
                if (show)
                {
                    Cover(winMarker, numberCells[r], 0.01f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.006f);
                    winMarker.localScale = Vector3.Scale(winMarker.localScale, new Vector3(1.15f, 1f, 1.15f));
                }
            }

            // The chip size you're holding sits up out of its stack.
            if (chipStacks != null)
                foreach (var s in chipStacks)
                {
                    if (s == null || !m_StackHome.TryGetValue(s, out var home)) continue;
                    var target = home + (s.Selected ? new Vector3(0f, 0.03f + Mathf.Sin(Time.time * 3f) * 0.005f, -0.03f) : Vector3.zero);
                    s.transform.localPosition = Vector3.Lerp(s.transform.localPosition, target, Time.deltaTime * 12f);
                }

            if (m_ChipsDirty) RebuildChips();
            UpdateBoard();
        }

        /// <summary>Lay a flat glow over a spot, sized to its trigger.</summary>
        static void Cover(Transform marker, Transform spot, float lift)
        {
            var box = spot.GetComponent<BoxCollider>();
            Vector3 size = box != null ? box.size : new Vector3(0.1f, 0.01f, 0.1f);
            marker.position = spot.position + spot.up * lift;
            marker.rotation = spot.rotation;
            marker.localScale = new Vector3(size.x, 0.003f, size.z);
        }

        static readonly Color[] k_ChipColors =
        {
            new(0.95f, 0.75f, 0.2f), new(0.25f, 0.55f, 0.95f), new(0.9f, 0.3f, 0.35f), new(0.35f, 0.8f, 0.45f),
        };

        /// <summary>Chip colour for a player (P1 gold, P2 blue, P3 red, P4 green).</summary>
        public static Color PlayerChipColor(ulong clientId) => k_ChipColors[(int)(clientId % (ulong)k_ChipColors.Length)];

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
                    m_Block.SetColor("_BaseColor", PlayerChipColor(bet.ClientId));
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
                    sb.Append(SpunBy.Length > 0 ? $"<b>NO MORE BETS</b>  <size=60%>spun by {SpunBy}</size>" : "<b>NO MORE BETS</b>");
                    break;
                default:
                    int r = m_Result.Value;
                    sb.Append($"<b><color={RouletteRules.Hex(r)}>{RouletteRules.Describe(r)}</color></b>");
                    break;
            }
            sb.Append("\n<size=60%>");
            if (m_History.Count == 0) sb.Append("E on the felt to bet · R takes it back · E on the wheel to spin");
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
