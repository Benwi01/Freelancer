namespace Freelancer
{
    /// <summary>Picks a client review: each gig has three reviews per star rating (index 0 = 1 star).</summary>
    public static class GigReviews
    {
        public const int PerRating = 3;

        public static int PickIndex(int stars)
        {
            int bucket = UnityEngine.Mathf.Clamp(stars, 1, 5) - 1;
            return bucket * PerRating + UnityEngine.Random.Range(0, PerRating);
        }

        public static string Get(GigDefinition gig, int index) =>
            gig != null && gig.Reviews != null && index >= 0 && index < gig.Reviews.Length
                ? gig.Reviews[index]
                : "No comment. (The client is still processing.)";
    }
}
