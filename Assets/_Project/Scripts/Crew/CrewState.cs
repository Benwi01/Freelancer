using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// The crew's shared progress for this session: money and best stars per gig.
    /// Spawned once by the host when the session starts and kept across scene loads.
    /// The host loads it from <see cref="CrewSave"/> and writes it back after every gig;
    /// everyone else just reads the synced values.
    /// </summary>
    public class CrewState : NetworkBehaviour
    {
        public static CrewState Current { get; private set; }

        readonly NetworkVariable<int> m_Money = new(0);
        readonly NetworkVariable<int> m_TotalEarned = new(0);
        NetworkList<GigRecord> m_Best;

        CrewSave m_Save;

        public int Money => m_Money.Value;
        public int TotalEarned => m_TotalEarned.Value;

        void Awake() => m_Best = new NetworkList<GigRecord>();

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (!IsServer) return;

            m_Save = CrewSave.Load();
            m_Money.Value = m_Save.money;
            m_TotalEarned.Value = m_Save.totalEarned;
            m_Best.Clear();
            foreach (var e in m_Save.best)
                m_Best.Add(new GigRecord { GigId = e.gigId, Stars = (byte)e.stars });
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
        /// Server: book a finished gig. Pays the crew, updates the best rating and saves.
        /// Returns whether this beat the previous best.
        /// </summary>
        public bool RecordGig(GigDefinition gig, bool delivered, int stars, int pay, int tip)
        {
            if (!IsServer || gig == null) return false;

            m_Money.Value += pay + tip;
            m_TotalEarned.Value += pay + tip;
            m_Save.money = m_Money.Value;
            m_Save.totalEarned = m_TotalEarned.Value;

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
            m_Save.Save();
            return newBest;
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
    }
}
