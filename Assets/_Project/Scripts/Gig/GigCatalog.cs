namespace Freelancer
{
    /// <summary>Every gig in the game. Adding a gig = adding an entry here (and building its scene).</summary>
    public static class GigCatalog
    {
        public static readonly GigDefinition[] All =
        {
            new GigDefinition
            {
                Id = "cake-delivery",
                Title = "Wedding Cake Delivery",
                Client = "Brenda W.",
                ClientRating = 2.1f,
                Scene = "CakeDelivery",
                Pay = 120,
                Minutes = 3f,
                Tag = "FRAGILE",
                Description =
                    "My daughter Kayleigh is marrying Todd TODAY and the bakery \"can't do stairs\". " +
                    "The cake is in the van outside. Bring it up to my flat on the top floor (3B) " +
                    "and put it on the table. Three tiers. Heavy. Wobbly. Do NOT drop it.\n\n" +
                    "Tips are based on cake condition. - Brenda",
                Briefing =
                    "\"Three-tier wedding cake. In the van. Up to the top floor. On the table. " +
                    "It is VERY expensive and VERY wobbly. Please do not be the freelancers from last time.\"",
                Tips = "Tip: the cake is heavy. Grab it from both sides.",
                Reviewer = CakeReviews.Reviewer,
                Reviews = CakeReviews.All,
            },
            new GigDefinition
            {
                Id = "reception-bar",
                Title = "Wedding Reception Bar",
                Client = "Brenda W.",
                ClientRating = 2.1f,
                Scene = "ReceptionBar",
                Pay = 150,
                Minutes = 3f,
                Tag = "OPEN BAR",
                Description =
                    "The bartender for Kayleigh & Todd's reception quit. Don't ask. You're behind the bar now. " +
                    "Beer on tap, wine from the box, glasses on the rack. Serve everyone before they storm off.\n\n" +
                    "Spare kegs are in the basement. They are heavy. Tips are based on happy guests. - Brenda",
                Briefing =
                    "\"Guests order at the bar. Fill a glass, put it in front of them. Don't overfill, " +
                    "don't break anything, and if the beer runs out the spare kegs are in the basement. " +
                    "Uncle Gary drinks a lot. Do not let Uncle Gary wait.\"",
                Tips = "E at a tap: pour / stop  ·  RMB throws gently: slide drinks down the bar for bonus tips.",
                Reviewer = BarReviews.Reviewer,
                Reviews = BarReviews.All,
            },
        };

        public static GigDefinition ByScene(string sceneName)
        {
            foreach (var gig in All)
                if (gig.Scene == sceneName) return gig;
            return null;
        }

        public static GigDefinition ById(string id)
        {
            foreach (var gig in All)
                if (gig.Id == id) return gig;
            return null;
        }
    }
}
