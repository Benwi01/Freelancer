using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A glass that can hold one drink. Fills under a tap, spills when tipped, overflows if you
    /// keep pouring (sticky!), and smashes when it hits something hard. Server-authoritative.
    /// </summary>
    [RequireComponent(typeof(GrabbableObject))]
    public class DrinkGlass : NetworkBehaviour
    {
        public static readonly List<DrinkGlass> All = new();

        [SerializeField] Transform liquid;           // child scaled on y, pivot at the bottom
        [SerializeField] Renderer liquidRenderer;
        [SerializeField] Renderer glassRenderer;
        [SerializeField] float maxLiquidHeight = 0.13f;
        [Tooltip("Sudden change in speed (m/s) that shatters the glass.")]
        [SerializeField] float breakSpeed = 3.8f;
        [SerializeField] float spillPerSecond = 0.9f;

        readonly NetworkVariable<Drink> m_Drink = new(Drink.None);
        readonly NetworkVariable<float> m_Fill = new(0f);
        readonly NetworkVariable<bool> m_Sticky = new(false);

        GrabbableObject m_Grab;
        MaterialPropertyBlock m_Block;
        float m_SpawnedAt;
        bool m_Smashed;

        public Drink Contents => m_Drink.Value;
        public float Fill => m_Fill.Value;
        /// <summary>Overfilled at some point: the guest gets a sticky glass.</summary>
        public bool Sticky => m_Sticky.Value;
        public bool IsHeld => m_Grab.IsHeld;
        public Rigidbody Body => m_Grab.Body;
        /// <summary>Server: where it was last let go (for "nice slide!").</summary>
        public Vector3 ReleasedAt { get; private set; }

        void Awake() => m_Grab = GetComponent<GrabbableObject>();

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            m_SpawnedAt = Time.time;
            ReleasedAt = transform.position;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        /// <summary>Server: pour some drink in. Returns false if it overflowed.</summary>
        public bool AddDrink(Drink drink, float amount)
        {
            if (!IsServer || m_Smashed) return false;
            if (m_Fill.Value < 0.05f) m_Drink.Value = drink;
            else if (m_Drink.Value != drink) m_Drink.Value = Drink.Mystery;

            float fill = m_Fill.Value + amount;
            if (fill > 1f)
            {
                m_Sticky.Value = true;
                m_Fill.Value = 1f;
                return false;
            }
            m_Fill.Value = fill;
            return true;
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            if (m_Grab.IsHeld) ReleasedAt = transform.position;

            // Tipped over: it pours out.
            if (m_Fill.Value > 0f && Vector3.Dot(transform.up, Vector3.up) < 0.45f)
            {
                m_Fill.Value = Mathf.Max(0f, m_Fill.Value - spillPerSecond * Time.fixedDeltaTime);
                if (m_Fill.Value <= 0.02f)
                {
                    m_Fill.Value = 0f;
                    m_Drink.Value = Drink.None;
                }
            }

            if (transform.position.y < -10f) NetworkObject.Despawn();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !IsSpawned || m_Smashed) return;
            if (Time.time - m_SpawnedAt < 1f) return; // settling on the rack
            if (collision.collider.GetComponentInParent<DrinkGlass>() != null &&
                collision.relativeVelocity.magnitude < breakSpeed * 1.5f) return; // clinking glasses is fine

            float hit = collision.impulse.magnitude / Mathf.Max(0.01f, Body.mass);
            if (hit >= breakSpeed) Smash();
        }

        /// <summary>Server.</summary>
        public void Smash()
        {
            if (!IsServer || m_Smashed) return;
            m_Smashed = true;
            BarObjective.Current?.OnGlassSmashed();
            SmashRpc(transform.position, m_Drink.Value, m_Fill.Value);
            NetworkObject.Despawn();
        }

        [Rpc(SendTo.Everyone)]
        void SmashRpc(Vector3 position, Drink drink, float fill)
        {
            var mat = glassRenderer != null ? glassRenderer.sharedMaterial : null;
            for (int i = 0; i < 7; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.transform.position = position + Random.insideUnitSphere * 0.06f;
                shard.transform.rotation = Random.rotation;
                shard.transform.localScale = new Vector3(0.05f, 0.012f, 0.04f) * Random.Range(0.6f, 1.4f);
                if (mat != null) shard.GetComponent<Renderer>().sharedMaterial = mat;
                var rb = shard.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.insideUnitSphere * 2f + Vector3.up * 2f;
                rb.angularVelocity = Random.insideUnitSphere * 15f;
                Destroy(shard, 1.6f);
            }
            if (fill > 0.05f && drink != Drink.None)
            {
                // A splat of whatever was in it.
                var splat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(splat.GetComponent<Collider>());
                if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                    position = hit.point;
                splat.transform.position = position + Vector3.up * 0.004f;
                splat.transform.localScale = new Vector3(0.5f * fill + 0.2f, 0.002f, 0.45f * fill + 0.2f);
                var r = splat.GetComponent<Renderer>();
                if (liquidRenderer != null) r.sharedMaterial = liquidRenderer.sharedMaterial;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Drinks.Liquid(drink));
                r.SetPropertyBlock(block);
                Destroy(splat, 25f);
            }
        }

        void Update()
        {
            if (liquid == null) return;
            float fill = m_Fill.Value;
            bool show = fill > 0.01f && m_Drink.Value != Drink.None;
            if (liquid.gameObject.activeSelf != show) liquid.gameObject.SetActive(show);
            if (!show) return;
            var s = liquid.localScale;
            s.y = Mathf.Max(0.001f, fill * maxLiquidHeight);
            liquid.localScale = s;
            if (liquidRenderer != null)
            {
                m_Block ??= new MaterialPropertyBlock();
                m_Block.SetColor("_BaseColor", Drinks.Liquid(m_Drink.Value));
                liquidRenderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
