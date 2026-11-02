using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A tap (or a box of wine with a spout). Press E to start pouring, E again to stop: whatever
    /// glass is under the spout fills up. Keep pouring and it overflows. A beer tap runs off a keg;
    /// when that runs dry, drag a spare keg onto the keg spot (a child of this object) to refill it.
    /// </summary>
    public class DrinkTap : NetworkBehaviour, IInteractable
    {
        public static readonly List<DrinkTap> All = new();

        [SerializeField] Drink drink = Drink.Beer;
        [SerializeField] string drinkName = "beer";
        [SerializeField] Transform spout;
        [SerializeField] GameObject stream;
        [SerializeField] Transform handle;
        [SerializeField] float pourPerSecond = 0.42f;

        [Header("Keg")]
        [SerializeField] bool usesKeg;
        [Tooltip("How full the keg is at the start (so it runs out mid-shift).")]
        [SerializeField] float startLevel = 0.5f;
        [Tooltip("Keg used per second of pouring. 0.04 = about ten pints per keg.")]
        [SerializeField] float kegPerSecond = 0.04f;
        [SerializeField] Transform kegSpot;
        [SerializeField] float kegSpotRadius = 0.9f;

        readonly NetworkVariable<bool> m_Pouring = new(false);
        readonly NetworkVariable<float> m_Level = new(1f);

        public Drink Drink => drink;
        public bool UsesKeg => usesKeg;
        public float Level => m_Level.Value;
        public bool IsEmpty => usesKeg && m_Level.Value <= 0f;
        public bool IsPouring => m_Pouring.Value;

        public string Prompt => IsEmpty ? "Keg's empty! Spare kegs are in the basement"
            : m_Pouring.Value ? "Stop pouring" : $"Pour {drinkName}";

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer) m_Level.Value = usesKeg ? startLevel : 1f;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        public void Interact(PlayerMotor player) => ToggleRpc();

        [Rpc(SendTo.Server)]
        void ToggleRpc()
        {
            if (GigDirector.Current != null && GigDirector.Current.IsOver) return;
            m_Pouring.Value = !m_Pouring.Value && !IsEmpty;
        }

        void Update()
        {
            bool flowing = m_Pouring.Value && !IsEmpty;
            if (stream != null && stream.activeSelf != flowing) stream.SetActive(flowing);
            if (handle != null)
                handle.localRotation = Quaternion.Slerp(handle.localRotation,
                    Quaternion.Euler(m_Pouring.Value ? 35f : 0f, 0f, 0f), Time.deltaTime * 12f);

            if (!IsServer || !IsSpawned) return;
            if (usesKeg) TryRefillFromKeg();
            if (!m_Pouring.Value) return;

            if (IsEmpty || (GigDirector.Current != null && GigDirector.Current.IsOver))
            {
                m_Pouring.Value = false;
                return;
            }

            if (usesKeg) m_Level.Value = Mathf.Max(0f, m_Level.Value - kegPerSecond * Time.deltaTime);
            var glass = GlassUnderSpout();
            if (glass != null) glass.AddDrink(drink, pourPerSecond * Time.deltaTime);
        }

        DrinkGlass GlassUnderSpout()
        {
            Vector3 s = spout.position;
            foreach (var glass in DrinkGlass.All)
            {
                if (glass == null || !glass.IsSpawned) continue;
                Vector3 g = glass.transform.position;
                Vector2 flat = new Vector2(g.x - s.x, g.z - s.z);
                if (flat.magnitude > 0.11f) continue;
                if (g.y > s.y || g.y < s.y - 0.6f) continue;
                if (Vector3.Dot(glass.transform.up, Vector3.up) < 0.7f) continue;
                return glass;
            }
            return null;
        }

        void TryRefillFromKeg()
        {
            if (kegSpot == null || m_Level.Value > 0.25f) return;
            foreach (var keg in BeerKeg.All)
            {
                if (keg == null || !keg.IsSpawned || keg.IsHeld) continue;
                Vector3 d = keg.transform.position - kegSpot.position;
                d.y = 0f;
                if (d.magnitude > kegSpotRadius) continue;
                m_Level.Value = 1f;
                keg.NetworkObject.Despawn();
                return;
            }
        }
    }
}
