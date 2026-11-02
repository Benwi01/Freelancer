namespace Freelancer
{
    /// <summary>What a finished gig pays: the listed pay plus a tip that grows with the star rating.</summary>
    public static class GigPayout
    {
        // Tip as a share of the listed pay, by stars (index = stars).
        static readonly float[] k_TipShare = { 0f, 0f, 0.1f, 0.25f, 0.5f, 1f };

        public static (int pay, int tip) Calculate(GigDefinition gig, bool delivered, int stars)
        {
            if (gig == null || !delivered) return (0, 0); // no cake, no cash
            int s = stars < 0 ? 0 : stars > 5 ? 5 : stars;
            int tip = (int)(gig.Pay * k_TipShare[s] + 0.5f);
            return (gig.Pay, tip);
        }
    }
}
