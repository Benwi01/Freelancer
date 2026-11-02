using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Server-authoritative cake health. Hard impacts cost condition; below thresholds the top
    /// tiers break off and become separate (grabbable, sad) debris. At 0 the cake is ruined.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CakeCondition : NetworkBehaviour
    {
        public const float MaxCondition = 100f;

        public static CakeCondition Current { get; private set; }

        [Header("Damage")]
        [Tooltip("Impacts slower than this (m/s) don't hurt.")]
        [SerializeField] float damageThreshold = 3f;
        [SerializeField] float damagePerSpeed = 7f;
        [SerializeField] float maxDamagePerHit = 40f;
        [SerializeField] float hitCooldown = 0.15f;

        [Header("Tiers (top first)")]
        [Tooltip("Tier objects that can break off, top tier first.")]
        [SerializeField] GameObject[] detachableTiers;
        [Tooltip("Debris prefab spawned for each detachable tier, same order.")]
        [SerializeField] NetworkObject[] tierDebrisPrefabs;
        [Tooltip("Mass of each detachable tier, same order.")]
        [SerializeField] float[] tierMasses = { 4f, 8f };
        [Tooltip("Condition at or below which each tier breaks off, same order.")]
        [SerializeField] float[] detachAt = { 66f, 33f };

        readonly NetworkVariable<float> m_Condition = new(MaxCondition);
        readonly NetworkVariable<int> m_TiersLost = new(0);

        Rigidbody m_Body;
        float m_LastHitTime;

        public float Condition => m_Condition.Value;
        public float Condition01 => m_Condition.Value / MaxCondition;
        public int TiersLeft => 1 + (detachableTiers?.Length ?? 0) - m_TiersLost.Value;
        public int TierCount => 1 + (detachableTiers?.Length ?? 0);
        public bool IsRuined => m_Condition.Value <= 0f || transform.position.y < -10f;
        public Rigidbody Body => m_Body;

        /// <summary>Raised on every machine when the cake takes a hit (amount).</summary>
        public event System.Action<float> Damaged;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            m_TiersLost.OnValueChanged += OnTiersLostChanged;
            m_Condition.OnValueChanged += OnConditionChanged;
            ApplyTierVisibility(m_TiersLost.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_TiersLost.OnValueChanged -= OnTiersLostChanged;
            m_Condition.OnValueChanged -= OnConditionChanged;
            if (Current == this) Current = null;
        }

        void OnConditionChanged(float previous, float current)
        {
            if (current < previous) Damaged?.Invoke(previous - current);
        }

        void OnTiersLostChanged(int previous, int current) => ApplyTierVisibility(current);

        void ApplyTierVisibility(int lost)
        {
            for (int i = 0; i < detachableTiers.Length; i++)
                if (detachableTiers[i] != null) detachableTiers[i].SetActive(i >= lost);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !IsSpawned) return;
            if (GigDirector.Current == null || !GigDirector.Current.CakeCanTakeDamage) return;
            if (Time.time - m_LastHitTime < hitCooldown) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < damageThreshold) return;

            m_LastHitTime = Time.time;
            float damage = Mathf.Min((speed - damageThreshold) * damagePerSpeed, maxDamagePerHit);
            ApplyDamage(damage);
        }

        /// <summary>Server only.</summary>
        public void ApplyDamage(float amount)
        {
            if (!IsServer || amount <= 0f || m_Condition.Value <= 0f) return;
            m_Condition.Value = Mathf.Max(0f, m_Condition.Value - amount);

            while (m_TiersLost.Value < detachableTiers.Length && m_Condition.Value <= detachAt[m_TiersLost.Value])
                DetachNextTier();
        }

        void DetachNextTier()
        {
            int index = m_TiersLost.Value;
            var tier = detachableTiers[index];

            if (index < tierDebrisPrefabs.Length && tierDebrisPrefabs[index] != null)
            {
                var debris = Instantiate(tierDebrisPrefabs[index], tier.transform.position, tier.transform.rotation);
                debris.Spawn(destroyWithScene: true);
                if (debris.TryGetComponent(out Rigidbody rb))
                {
                    // Pop it off with a bit of drama.
                    rb.linearVelocity = m_Body.GetPointVelocity(tier.transform.position) +
                                        Vector3.up * 2.5f + Random.insideUnitSphere * 1.5f;
                    rb.angularVelocity = Random.insideUnitSphere * 4f;
                }
            }

            if (index < tierMasses.Length)
                m_Body.mass = Mathf.Max(5f, m_Body.mass - tierMasses[index]);

            m_TiersLost.Value = index + 1; // hides the tier on every machine (and the server's collider)
        }
    }
}
