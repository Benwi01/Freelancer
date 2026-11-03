using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// The crew's shared progress for this session: money, rent and best stars per gig.
    /// Spawned once by the host when the session starts and kept across scene loads.
    /// The host loads it from <see cref="CrewSave"/> and writes it back after every gig;
    /// everyone else just reads the synced values.
    /// </summary>
    public class CrewState : NetworkBehaviour
    {
        public static CrewState Current { get; private set; }

        readonly NetworkVariable<int> m_Money = new(0);
        readonly NetworkVariable<int> m_TotalEarned = new(0);
        readonly NetworkVariable<int> m_Week = new(1);
        readonly NetworkVariable<int> m_GigsThisWeek = new(0);
        readonly NetworkVariable<int> m_Owed = new(0);
        readonly NetworkVariable<int> m_Strikes = new(0);
        readonly NetworkVariable<RentNotice> m_Notice = new(RentNotice.None);
        readonly NetworkVariable<int> m_NoticeAmount = new(0);
        readonly NetworkVariable<int> m_NoticeId = new(0);
        readonly NetworkVariable<bool> m_LastGigWasRentDay = new(false);
        NetworkList<GigRecord> m_Best;

        CrewSave m_Save;

        public int Money => m_Money.Value;
        public int TotalEarned => m_TotalEarned.Value;

        /// <summary>Rent week, starting at 1.</summary>
        public int Week => m_Week.Value;
        /// <summary>Finished gigs left before rent day (1 = the next gig ends on rent day).</summary>
        public int GigsUntilRent => RentRules.GigsPerWeek - m_GigsThisWeek.Value;
        /// <summary>What rent day will ask for: this week's rent plus anything still owed.</summary>
        public int RentDue => RentRules.RentFor(m_Week.Value) + m_Owed.Value;
        public int RentOwed => m_Owed.Value;
        /// <summary>Already missed rent once: missing it again means eviction.</summary>
        public bool OnFinalWarning => m_Strikes.Value > 0;
        public bool CanAffordRent => m_Money.Value >= RentDue;

        /// <summary>The landlord's latest letter. A new letter gets a new id.</summary>
        public RentNotice Notice => m_Notice.Value;
        public int NoticeAmount => m_NoticeAmount.Value;
        public int NoticeId => m_NoticeId.Value;
        /// <summary>The gig that just finished ended on rent day (for the results screen).</summary>
        public bool LastGigWasRentDay => m_LastGigWasRentDay.Value;

        void Awake() => m_Best = new NetworkList<GigRecord>();

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (!IsServer) return;

            m_Save = CrewSave.Load();
            if (m_Save.rentWeek < 1) m_Save.rentWeek = 1;
            m_Money.Value = m_Save.money;
            m_TotalEarned.Value = m_Save.totalEarned;
            m_Best.Clear();
            foreach (var e in m_Save.best)
                m_Best.Add(new GigRecord { GigId = e.gigId, Stars = (byte)e.stars });
            SyncRent();

            // First time (or an old save): the landlord says hello.
            if (!m_Save.rentIntroShown)
            {
                m_Save.rentIntroShown = true;
                SendNotice(RentNotice.Intro, RentDue);
                m_Save.Save();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        /// <summary>Best stars the crew has got on a gig (0 = never delivered). Any machine.</summary>
        public int BestStars(string gigId)
        {
            foreach (var r in m_Best)
                if (r.GigId.ToString() == gigId) return r.Stars;
            return 0;
        }

        /// <summary>
        /// Server: book a finished gig. Pays the crew, updates the best rating, counts down to
        /// rent day (and settles rent if it's here) and saves. Returns whether this beat the previous best.
        /// </summary>
        public bool RecordGig(GigDefinition gig, bool delivered, int stars, int pay, int tip)
        {
            if (!IsServer || gig == null) return false;

            m_Save.money += pay + tip;
            m_Save.totalEarned += pay + tip;

            bool newBest = false;
            if (delivered)
            {
                m_Save.gigsDelivered++;
                if (stars > m_Save.BestStars(gig.Id))
                {
                    newBest = true;
                    m_Save.SetBestStars(gig.Id, stars);
                    SetBestSynced(gig.Id, stars);
                }
            }

            m_Save.gigsThisWeek++;
            bool rentDay = m_Save.gigsThisWeek >= RentRules.GigsPerWeek;
            if (rentDay) SettleRent();
            m_LastGigWasRentDay.Value = rentDay;

            m_Money.Value = m_Save.money;
            m_TotalEarned.Value = m_Save.totalEarned;
            SyncRent();
            m_Save.Save();
            return newBest;
        }

        /// <summary>Rent day: pay up, get a warning, or get thrown out.</summary>
        void SettleRent()
        {
            int due = RentRules.RentFor(m_Save.rentWeek) + m_Save.rentOwed;
            m_Save.gigsThisWeek = 0;

            if (m_Save.money >= due)
            {
                m_Save.money -= due;
                m_Save.rentOwed = 0;
                m_Save.rentStrikes = 0;
                m_Save.rentWeek++;
                SendNotice(RentNotice.Paid, due);
            }
            else if (m_Save.rentStrikes == 0)
            {
                // Nothing taken: it all carries over to next rent day, on top of next week's rent.
                m_Save.rentOwed = due;
                m_Save.rentStrikes = 1;
                m_Save.rentWeek++;
                SendNotice(RentNotice.Missed, due);
            }
            else
            {
                // Evicted: the landlord keeps what's left, and the crew starts over.
                m_Save.money = 0;
                m_Save.rentOwed = 0;
                m_Save.rentStrikes = 0;
                m_Save.rentWeek = 1;
                m_Save.evictions++;
                SendNotice(RentNotice.Evicted, due);
            }
        }

        void SendNotice(RentNotice notice, int amount)
        {
            m_NoticeAmount.Value = amount;
            m_Notice.Value = notice;
            m_NoticeId.Value++;
        }

        void SyncRent()
        {
            m_Week.Value = m_Save.rentWeek;
            m_GigsThisWeek.Value = m_Save.gigsThisWeek;
            m_Owed.Value = m_Save.rentOwed;
            m_Strikes.Value = m_Save.rentStrikes;
        }

        void SetBestSynced(string gigId, int stars)
        {
            for (int i = 0; i < m_Best.Count; i++)
            {
                if (m_Best[i].GigId.ToString() != gigId) continue;
                m_Best[i] = new GigRecord { GigId = gigId, Stars = (byte)stars };
                return;
            }
            m_Best.Add(new GigRecord { GigId = gigId, Stars = (byte)stars });
        }

        // ---------- Dev helpers (editor test scripts) ----------

        /// <summary>Server, dev only: set the crew's money.</summary>
        public void DevSetMoney(int money)
        {
            if (!IsServer) return;
            m_Save.money = money;
            m_Money.Value = money;
        }

        /// <summary>Server, dev only: jump straight to rent day.</summary>
        public void DevRentDay()
        {
            if (!IsServer) return;
            SettleRent();
            m_Money.Value = m_Save.money;
            SyncRent();
        }
    }
}
