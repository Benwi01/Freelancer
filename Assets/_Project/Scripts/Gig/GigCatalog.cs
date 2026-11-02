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
            },
        };

        public static GigDefinition ById(string id)
        {
            foreach (var gig in All)
                if (gig.Id == id) return gig;
            return null;
        }
    }
}
