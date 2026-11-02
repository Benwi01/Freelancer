using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// The crew's progress, saved as JSON on the host's PC (the host "owns" the crew save):
    /// money, lifetime earnings and the best star rating on each gig.
    /// </summary>
    [Serializable]
    public class CrewSave
    {
        public int money;
        public int totalEarned;
        public int gigsDelivered;
        public List<BestEntry> best = new();

        [Serializable]
        public class BestEntry
        {
            public string gigId;
            public int stars;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "crew-save.json");

        public static CrewSave Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<CrewSave>(File.ReadAllText(FilePath)) ?? new CrewSave();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Freelancer] Couldn't read crew save, starting fresh: {e.Message}");
            }
            return new CrewSave();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Freelancer] Couldn't write crew save: {e.Message}");
            }
        }

        public int BestStars(string gigId)
        {
            foreach (var e in best)
                if (e.gigId == gigId) return e.stars;
            return 0;
        }

        public void SetBestStars(string gigId, int stars)
        {
            foreach (var e in best)
                if (e.gigId == gigId) { e.stars = stars; return; }
            best.Add(new BestEntry { gigId = gigId, stars = stars });
        }
    }
}
