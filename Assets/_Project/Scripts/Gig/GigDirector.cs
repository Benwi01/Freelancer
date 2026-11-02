using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Freelancer
{
    /// <summary>
    /// Server-authoritative gig flow: briefing, then a countdown; success when the cake rests
    /// upright on the table with nobody holding it, failure on timeout or a ruined cake.
    /// Clients only read the replicated state.
    /// </summary>
    public class GigDirector : NetworkBehaviour
    {
        public static GigDirector Current { get; private set; }

        [SerializeField] float briefingSeconds = 10f;
        [SerializeField] float gigSeconds = 180f;
        [Tooltip("How long the cake must sit still on the table, untouched, to count as delivered.")]
        [SerializeField] float settleSeconds = 1.5f;
        [SerializeField] float settleMaxSpeed = 0.35f;
        [SerializeField] float uprightDot = 0.8f;

        readonly NetworkVariable<GigPhase> m_Phase = new(GigPhase.Briefing);
        readonly NetworkVariable<float> m_TimeLeft = new(0f);
        readonly NetworkVariable<float> m_BriefingLeft = new(0f);
        readonly NetworkVariable<float> m_SettleProgress = new(0f);
        readonly NetworkVariable<GigFailReason> m_FailReason = new(GigFailReason.None);
        readonly NetworkVariable<int> m_Stars = new(0);
        readonly NetworkVariable<int> m_ReviewIndex = new(-1);
        readonly NetworkVariable<float> m_FinalCondition = new(0f);
        readonly NetworkVariable<int> m_Pay = new(0);
        readonly NetworkVariable<int> m_Tip = new(0);
        readonly NetworkVariable<bool> m_NewBest = new(false);

        float m_SettledFor;

        public GigPhase Phase => m_Phase.Value;
        public float TimeLeft => m_TimeLeft.Value;
        public float GigSeconds => gigSeconds;
        public float BriefingLeft => m_BriefingLeft.Value;
        /// <summary>0..1 while the cake is sitting on the table waiting to count.</summary>
        public float SettleProgress => m_SettleProgress.Value;
        public GigFailReason FailReason => m_FailReason.Value;
        public int Stars => m_Stars.Value;
        public int ReviewIndex => m_ReviewIndex.Value;
        public float FinalCondition => m_FinalCondition.Value;
        public int Pay => m_Pay.Value;
        public int Tip => m_Tip.Value;
        public bool NewBest => m_NewBest.Value;
        /// <summary>Which gig this scene is (from the catalogue).</summary>
        public GigDefinition Gig => GigCatalog.ByScene(gameObject.scene.name);
        public bool IsOver => m_Phase.Value is GigPhase.Delivered or GigPhase.Failed;
        public bool CakeCanTakeDamage => m_Phase.Value == GigPhase.Running;

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer)
            {
                m_Phase.Value = GigPhase.Briefing;
                m_BriefingLeft.Value = briefingSeconds;
                m_TimeLeft.Value = gigSeconds;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;

            switch (m_Phase.Value)
            {
                case GigPhase.Briefing:
                    m_BriefingLeft.Value = Mathf.Max(0f, m_BriefingLeft.Value - Time.deltaTime);
                    if (m_BriefingLeft.Value <= 0f) m_Phase.Value = GigPhase.Running;
                    break;

                case GigPhase.Running:
                    TickRunning();
                    break;
            }
        }

        void TickRunning()
        {
            m_TimeLeft.Value = Mathf.Max(0f, m_TimeLeft.Value - Time.deltaTime);

            var cake = CakeCondition.Current;
            if (cake == null || cake.IsRuined)
            {
                Fail(GigFailReason.CakeRuined);
                return;
            }

            if (IsCakeSettledOnTable(cake))
            {
                m_SettledFor += Time.deltaTime;
                m_SettleProgress.Value = Mathf.Clamp01(m_SettledFor / settleSeconds);
                if (m_SettledFor >= settleSeconds)
                {
                    Succeed(cake);
                    return;
                }
            }
            else if (m_SettledFor > 0f)
            {
                m_SettledFor = 0f;
                m_SettleProgress.Value = 0f;
            }

            if (m_TimeLeft.Value <= 0f) Fail(GigFailReason.TimeUp);
        }

        bool IsCakeSettledOnTable(CakeCondition cake)
        {
            var zone = DeliveryZone.Current;
            if (zone == null || !zone.Contains(cake.transform.position)) return false;
            if (Vector3.Dot(cake.transform.up, Vector3.up) < uprightDot) return false;
            if (cake.Body.linearVelocity.magnitude > settleMaxSpeed) return false;
            return !cake.TryGetComponent(out GrabbableObject grab) || !grab.IsHeld;
        }

        void Succeed(CakeCondition cake)
        {
            float condition = cake.Condition01;
            float timeFraction = m_TimeLeft.Value / gigSeconds;
            // Mostly about the cake; speed is a bonus.
            float score = 0.65f * condition + 0.35f * timeFraction;
            int stars = Mathf.Clamp(1 + Mathf.RoundToInt(score * 4f), 1, 5);
            if (condition < 0.34f) stars = Mathf.Min(stars, 2); // lost two tiers: Brenda is not amused
            Finish(GigPhase.Delivered, GigFailReason.None, stars, condition);
        }

        void Fail(GigFailReason reason)
        {
            var cake = CakeCondition.Current;
            Finish(GigPhase.Failed, reason, 1, cake != null ? cake.Condition01 : 0f);
        }

        void Finish(GigPhase phase, GigFailReason reason, int stars, float condition)
        {
            // Get paid (or not) and remember the best rating. Set before the phase changes so
            // the results screen has the numbers the moment it appears.
            bool delivered = phase == GigPhase.Delivered;
            var (pay, tip) = GigPayout.Calculate(Gig, delivered, stars);
            m_Pay.Value = pay;
            m_Tip.Value = tip;
            m_NewBest.Value = CrewState.Current != null && CrewState.Current.RecordGig(Gig, delivered, stars, pay, tip);

            m_Stars.Value = stars;
            m_ReviewIndex.Value = CakeReviews.PickIndex(stars);
            m_FinalCondition.Value = condition;
            m_FailReason.Value = reason;
            m_SettleProgress.Value = 0f;
            m_Phase.Value = phase;
        }

        /// <summary>Host only: everyone back to the apartment.</summary>
        [Rpc(SendTo.Server)]
        public void RequestReturnToHubRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            SessionManager.Instance?.ReturnToHub();
        }

        /// <summary>Host only: reload the gig scene for everyone.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRestartRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            NetworkManager.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }
}
