using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A tap (or a box of wine with a spout). Let go of a glass near the drip tray and it clicks
    /// upright into place under the spout. Press E to start pouring and E again to stop; pour too
    /// long and it overflows (sticky glass, smaller tip). A beer tap runs off a keg; when that runs dry, drag a spare keg onto the keg spot (a child
    /// of this object) to refill it.
    /// </summary>
    public class DrinkTap : NetworkBehaviour, IInteractable
    {
        public static readonly List<DrinkTap> All = new();

        [SerializeField] Drink drink = Drink.Beer;
        [SerializeField] string drinkName = "beer";
        [SerializeField] Transform spout;
        [SerializeField] GameObject stream;
        [SerializeField] Transform handle;
        [SerializeField] float pourPerSecond = 0.45f;

        [Header("Snapping")]
        [Tooltip("Let go of a glass within this distance of the tray and it snaps onto it.")]
        [SerializeField] float snapRadius = 0.3f;
        [SerializeField] float snapHeight = 0.45f;

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
        readonly NetworkVariable<float> m_GlassFill = new(-1f); // fill of the glass on the tray (-1 = none), for the prompt

        public Drink Drink => drink;
        public bool UsesKeg => usesKeg;
        public float Level => m_Level.Value;
        public bool IsEmpty => usesKeg && m_Level.Value <= 0f;
        public bool IsPouring => m_Pouring.Value;

        /// <summary>Where a glass stands to be filled: on the drip tray, under the spout.</summary>
        Vector3 TrayPoint => new(spout.position.x, transform.position.y + 0.002f, spout.position.z);

        public string Prompt
        {
            get
            {
                if (IsEmpty) return "Keg's empty! Spare kegs are in the basement";
                string glass = m_GlassFill.Value < 0f ? "  (no glass!)" : $"  ({Mathf.RoundToInt(m_GlassFill.Value * 100)}%)";
                return (m_Pouring.Value ? "Stop pouring" : $"Pour {drinkName}") + glass;
            }
        }

        public void Interact(PlayerMotor player) => ToggleRpc();

        [Rpc(SendTo.Server)]
        void ToggleRpc()
        {
            if (GigDirector.Current != null && GigDirector.Current.IsOver) return;
            m_Pouring.Value = !m_Pouring.Value && !IsEmpty;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer) m_Level.Value = usesKeg ? startLevel : 1f;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            bool flowing = m_Pouring.Value;
            if (stream != null && stream.activeSelf != flowing) stream.SetActive(flowing);
            if (handle != null)
                handle.localRotation = Quaternion.Slerp(handle.localRotation,
                    Quaternion.Euler(flowing ? 35f : 0f, 0f, 0f), Time.deltaTime * 12f);
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;
            if (usesKeg) TryRefillFromKeg();

            var glass = DockGlass();
            float shown = glass != null ? Mathf.Round(glass.Fill * 100f) / 100f : -1f;
            if (!Mathf.Approximately(m_GlassFill.Value, shown)) m_GlassFill.Value = shown;

            if (!m_Pouring.Value) return;
            if (IsEmpty || (GigDirector.Current != null && GigDirector.Current.IsOver))
            {
                m_Pouring.Value = false;
                return;
            }

            // Pours whether or not there's a glass under it (no glass = beer on the floor).
            if (usesKeg) m_Level.Value = Mathf.Max(0f, m_Level.Value - kegPerSecond * Time.fixedDeltaTime);
            if (glass != null) glass.AddDrink(drink, pourPerSecond * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Server: the glass standing on the tray, if any. A glass let go of nearby gets snapped
        /// upright onto the tray first.
        /// </summary>
        DrinkGlass DockGlass()
        {
            Vector3 tray = TrayPoint;
            DrinkGlass docked = null, nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var glass in DrinkGlass.All)
            {
                if (glass == null || !glass.IsSpawned || glass.IsHeld) continue;
                Vector3 g = glass.transform.position;
                float flat = new Vector2(g.x - tray.x, g.z - tray.z).magnitude;
                if (g.y < tray.y - 0.1f || g.y > tray.y + snapHeight || flat > snapRadius) continue;
                if (flat < 0.04f && Vector3.Dot(glass.transform.up, Vector3.up) > 0.95f) docked = glass;
                else if (flat < nearestDist && glass.Body.linearVelocity.magnitude < 1.5f) // not drinks being slid past
                {
                    nearest = glass;
                    nearestDist = flat;
                }
            }
            if (docked != null) return docked;
            if (nearest == null) return null;

            // Click it into place: upright on the tray, keeping which way it faces.
            var body = nearest.Body;
            var rotation = Quaternion.Euler(0f, nearest.transform.eulerAngles.y, 0f);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = tray;
            body.rotation = rotation;
            nearest.transform.SetPositionAndRotation(tray, rotation);
            return nearest;
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
