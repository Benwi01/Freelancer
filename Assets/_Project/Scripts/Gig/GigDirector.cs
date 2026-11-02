using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Freelancer
{
    /// <summary>
    /// Server-authoritative gig flow shared by every gig: briefing, countdown, then results with
    /// stars, a review and the pay. What counts as winning lives in the <see cref="GigObjective"/>
    /// on the same object. Clients only read the replicated state.
    /// </summary>
    [RequireComponent(typeof(GigObjective))]
    public class GigDirector : NetworkBehaviour
    {
        public static GigDirector Current { get; private set; }

        [SerializeField] float briefingSeconds = 10f;
        [SerializeField] float gigSeconds = 180f;

        readonly NetworkVariable<GigPhase> m_Phase = new(GigPhase.Briefing);
        readonly NetworkVariable<float> m_TimeLeft = new(0f);
        readonly NetworkVariable<float> m_BriefingLeft = new(0f);
        readonly NetworkVariable<GigFailReason> m_FailReason = new(GigFailReason.None);
        readonly NetworkVariable<int> m_Stars = new(0);
        readonly NetworkVariable<int> m_ReviewIndex = new(-1);
        readonly NetworkVariable<int> m_Pay = new(0);
        readonly NetworkVariable<int> m_Tip = new(0);
        readonly NetworkVariable<bool> m_NewBest = new(false);

        GigObjective m_Objective;

        public GigPhase Phase => m_Phase.Value;
        public float TimeLeft => m_TimeLeft.Value;
        public float GigSeconds => gigSeconds;
        public float Elapsed => gigSeconds - m_TimeLeft.Value;
        public float BriefingLeft => m_BriefingLeft.Value;
        public GigFailReason FailReason => m_FailReason.Value;
        public int Stars => m_Stars.Value;
        public int ReviewIndex => m_ReviewIndex.Value;
        public int Pay => m_Pay.Value;
        public int Tip => m_Tip.Value;
        public bool NewBest => m_NewBest.Value;
        public GigObjective Objective => m_Objective;
        /// <summary>Which gig this scene is (from the catalogue).</summary>
        public GigDefinition Gig => GigCatalog.ByScene(gameObject.scene.name);
        public bool IsRunning => m_Phase.Value == GigPhase.Running;
        public bool IsOver => m_Phase.Value is GigPhase.Delivered or GigPhase.Failed;
        public bool CakeCanTakeDamage => IsRunning;

        void Awake() => m_Objective = GetComponent<GigObjective>();

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer)
            {
                var gig = Gig;
                if (gig != null && gig.Minutes > 0f) gigSeconds = gig.Minutes * 60f;
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
                    m_TimeLeft.Value = Mathf.Max(0f, m_TimeLeft.Value - Time.deltaTime);
                    m_Objective.ServerTick(this);
                    break;
            }
        }

        /// <summary>Server: end the gig. Pay is worked out here from the stars.</summary>
        public void Finish(bool delivered, GigFailReason reason, int stars)
        {
            if (!IsServer || IsOver) return;
            stars = Mathf.Clamp(stars, 1, 5);

            // Get paid (or not) and remember the best rating. Set before the phase changes so
            // the results screen has the numbers the moment it appears.
            var (pay, tip) = GigPayout.Calculate(Gig, delivered, stars);
            m_Pay.Value = pay;
            m_Tip.Value = tip;
            m_NewBest.Value = CrewState.Current != null && CrewState.Current.RecordGig(Gig, delivered, stars, pay, tip);

            m_Stars.Value = stars;
            m_ReviewIndex.Value = GigReviews.PickIndex(stars);
            m_FailReason.Value = reason;
            m_Phase.Value = delivered ? GigPhase.Delivered : GigPhase.Failed;
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
