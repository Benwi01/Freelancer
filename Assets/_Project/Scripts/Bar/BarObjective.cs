using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Reception bar shift: guests turn up at the bar faster and faster; serve as many as you can
    /// before the clock runs out. Stars come from drinks served (bonus for slides and perfect pours)
    /// minus guests who stormed off and glasses smashed. Too many walk-outs and Brenda fires you.
    /// </summary>
    public class BarObjective : GigObjective
    {
        public static BarObjective Current { get; private set; }

        [SerializeField] NetworkObject guestPrefab;

        [Header("Guests")]
        [SerializeField] float firstGuestAfter = 1f;
        [SerializeField] float startInterval = 9f;
        [SerializeField] float endInterval = 4.5f;
        [SerializeField] float startPatience = 45f;
        [SerializeField] float endPatience = 30f;
        [SerializeField, Range(0f, 1f)] float beerShare = 0.6f;
        [SerializeField] float lastCallSeconds = 30f;

        [Header("Rating")]
        [Tooltip("Score needed for 2, 3, 4 and 5 stars.")]
        [SerializeField] float[] starScores = { 3f, 6f, 10f, 14f };
        [SerializeField] int walkOutsToGetFired = 8;

        readonly NetworkVariable<int> m_Served = new(0);
        readonly NetworkVariable<int> m_Slides = new(0);
        readonly NetworkVariable<int> m_Perfect = new(0);
        readonly NetworkVariable<int> m_WalkedOff = new(0);
        readonly NetworkVariable<int> m_Smashed = new(0);

        float m_NextGuestIn = -1f;

        public int Served => m_Served.Value;
        public int Slides => m_Slides.Value;
        public int Perfect => m_Perfect.Value;
        public int WalkedOff => m_WalkedOff.Value;
        public int Smashed => m_Smashed.Value;
        public float Score => m_Served.Value + 0.5f * m_Slides.Value + 0.25f * m_Perfect.Value
                              - 0.5f * m_WalkedOff.Value - 0.25f * m_Smashed.Value;

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        // ---------- Server ----------

        public override void ServerTick(GigDirector director)
        {
            float progress = Mathf.Clamp01(director.Elapsed / director.GigSeconds);
            bool lastCall = director.TimeLeft <= lastCallSeconds;

            if (m_NextGuestIn < 0f) m_NextGuestIn = firstGuestAfter;
            m_NextGuestIn -= Time.deltaTime;
            if (m_NextGuestIn <= 0f && TrySpawnGuest(Mathf.Lerp(startPatience, endPatience, progress)))
            {
                float interval = Mathf.Lerp(startInterval, endInterval, progress);
                if (lastCall) interval *= 0.6f;
                m_NextGuestIn = interval * Random.Range(0.8f, 1.2f);
            }

            if (m_WalkedOff.Value >= walkOutsToGetFired)
            {
                director.Finish(false, GigFailReason.Fired, 1);
                return;
            }

            if (director.TimeLeft <= 0f)
            {
                if (m_Served.Value == 0) director.Finish(false, GigFailReason.NoDrinksServed, 1);
                else director.Finish(true, GigFailReason.None, StarsFor(Score));
            }
        }

        int StarsFor(float score)
        {
            int stars = 1;
            foreach (float needed in starScores)
                if (score >= needed) stars++;
            return stars;
        }

        bool TrySpawnGuest(float patience)
        {
            if (guestPrefab == null) return false;

            // Don't flood a small crew: a couple more guests than players at once.
            int players = NetworkManager.ConnectedClientsIds.Count;
            int waiting = 0;
            foreach (var g in BarGuest.All)
                if (g.Mood == GuestMood.Waiting) waiting++;
            if (waiting >= Mathf.Max(3, players + 2)) return false;

            GuestSlot free = null;
            int freeCount = 0;
            foreach (var slot in GuestSlot.All)
            {
                if (slot.Occupant != null) continue;
                freeCount++;
                if (Random.Range(0, freeCount) == 0) free = slot; // pick a random free slot
            }
            if (free == null) return false;

            var instance = Instantiate(guestPrefab, free.transform.position, free.transform.rotation);
            instance.Spawn(destroyWithScene: true);
            var guest = instance.GetComponent<BarGuest>();
            guest.Init(free, Random.value < beerShare ? Drink.Beer : Drink.Wine, patience);
            return true;
        }

        public void OnGuestServed(bool slid, bool perfect)
        {
            if (!IsServer) return;
            m_Served.Value++;
            if (slid) m_Slides.Value++;
            if (perfect) m_Perfect.Value++;
        }

        public void OnGuestWalkedOff()
        {
            if (IsServer) m_WalkedOff.Value++;
        }

        public void OnGlassSmashed()
        {
            if (IsServer && GigDirector.Current != null && GigDirector.Current.IsRunning) m_Smashed.Value++;
        }

        // ---------- HUD ----------

        static DrinkTap BeerTap()
        {
            foreach (var tap in DrinkTap.All)
                if (tap != null && tap.UsesKeg) return tap;
            return null;
        }

        public override string ObjectiveText(GigDirector director)
        {
            var tap = BeerTap();
            if (tap != null && tap.IsEmpty)
                return "The beer's run out! Drag a spare keg up from the basement to the KEG spot under the tap.";
            return "Fill a glass at a tap (E), then put it in front of the guest who ordered it.";
        }

        public override string Callout
        {
            get
            {
                var conga = CongaLine.Current;
                if (conga != null && conga.IsDancing && conga.LapTime < 3f) return "CONGA LINE!";
                var director = GigDirector.Current;
                if (director != null && director.TimeLeft <= lastCallSeconds && director.TimeLeft > lastCallSeconds - 3f)
                    return "LAST CALL!";
                var tap = BeerTap();
                if (tap != null && tap.IsEmpty && Mathf.Repeat(Time.time, 6f) < 2f) return "BEER'S OUT!";
                return "";
            }
        }

        public override void Status(out string title, out float fill, out string text, out Color color)
        {
            title = "DRINKS SERVED";
            float best = starScores[starScores.Length - 1];
            fill = Mathf.Clamp01(Score / best);
            color = Color.Lerp(new Color(0.95f, 0.6f, 0.2f), new Color(0.4f, 0.9f, 0.4f), fill);
            var tap = BeerTap();
            string keg = tap == null ? "" : tap.IsEmpty ? "   ·   keg EMPTY" : $"   ·   keg {Mathf.CeilToInt(tap.Level * 100)}%";
            text = $"{m_Served.Value} served   ·   {m_WalkedOff.Value} left angry{keg}";
        }

        public override string ResultDetail(GigDirector director) => director.FailReason switch
        {
            GigFailReason.Fired => $"{walkOutsToGetFired} guests stormed off. Brenda fired you mid-shift.",
            GigFailReason.NoDrinksServed => "Not one single drink served. Impressive, in a way.",
            _ => $"{m_Served.Value} served ({m_Slides.Value} slid, {m_Perfect.Value} perfect)  ·  " +
                 $"{m_WalkedOff.Value} stormed off  ·  {m_Smashed.Value} glasses smashed",
        };

        public override string NoPayLine => "Brenda is not paying for that.";
    }
}
