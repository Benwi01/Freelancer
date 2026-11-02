namespace Freelancer
{
    /// <summary>The client's snarky reviews, three per star rating (index 0 = 1 star).</summary>
    public static class CakeReviews
    {
        public const string Reviewer = "Brenda W., Mother of the Bride";
        public const int PerRating = 3;

        public static readonly string[] All =
        {
            // 1 star
            "They arrived with a cake. Technically. What they handed me was a crime scene with frosting. " +
            "One star because the app won't let me give zero.",
            "One of them carried it by the topper. Like a handle. The little bride is now in the groom's soup " +
            "and nobody can find the little groom. Fitting, honestly.",
            "I watched from the window as they carried it up the stairs. Then down the stairs. Then up again, " +
            "faster, for reasons. Please never contact me again.",

            // 2 stars
            "The bottom tier made it. The bottom tier was lovely. I will be framing the bottom tier.",
            "The cake arrived 'mostly upright', a phrase I have now heard a grown adult say with pride.",
            "They blamed the stairs. Those stairs have been there since 1974 and have never once dropped a cake.",

            // 3 stars
            "Delivered on time, slightly dented, and one of them was licking their fingers. " +
            "I'm choosing not to ask questions.",
            "It looks like it was carried by people who have heard of cakes but never seen one. " +
            "Three stars. Exactly average, just like the cake now.",
            "Got it to the table. Knocked over two lamps and my husband on the way. " +
            "My husband is fine. The lamps are not.",

            // 4 stars
            "Honestly impressed. They screamed the entire way up the stairs, but the cake looks great. " +
            "Minus one star for the screaming.",
            "Nearly perfect. There's a handprint on the side, but we're calling it a 'rustic artisanal touch'.",
            "Fast, careful-ish, and only one of them fell down the stairs. Would hire again if desperate.",

            // 5 stars
            "Flawless. I did not think these clowns could do it. Was it a different crew? Five stars regardless.",
            "Perfect cake, record time. The bride wept with joy. One of the freelancers also wept, unclear why. Lovely day.",
            "Absolutely perfect delivery. I'm suspicious. Leaving five stars before they break something on the way out.",
        };

        public static int PickIndex(int stars)
        {
            int bucket = UnityEngine.Mathf.Clamp(stars, 1, 5) - 1;
            return bucket * PerRating + UnityEngine.Random.Range(0, PerRating);
        }

        public static string Get(int index) =>
            index >= 0 && index < All.Length ? All[index] : "No comment. (Brenda is still processing.)";
    }
}
