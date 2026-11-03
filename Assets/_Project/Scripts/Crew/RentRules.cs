namespace Freelancer
{
    /// <summary>
    /// The apartment's rent: due every few gigs and going up each week. Miss it once and it
    /// carries over with a warning; miss it again and the crew is evicted.
    /// </summary>
    public static class RentRules
    {
        /// <summary>Finished gigs (delivered or not) per rent week.</summary>
        public const int GigsPerWeek = 3;

        const int FirstWeek = 250;
        const int RisePerWeek = 75;
        const int Max = 700;

        /// <summary>This week's rent, before any unpaid rent carried over. Weeks start at 1.</summary>
        public static int RentFor(int week)
        {
            int rent = FirstWeek + RisePerWeek * (week < 1 ? 0 : week - 1);
            return rent > Max ? Max : rent;
        }
    }

    /// <summary>What the landlord's latest letter says.</summary>
    public enum RentNotice : byte
    {
        None,
        Intro,
        Paid,
        Missed,
        Evicted,
    }
}
