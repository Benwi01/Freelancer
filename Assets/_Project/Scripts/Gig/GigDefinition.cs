namespace Freelancer
{
    /// <summary>One job on the GigGrabber app: what it is, who it's for, what it pays and which scene it plays in.</summary>
    public sealed class GigDefinition
    {
        public string Id;
        public string Title;
        public string Client;
        public float ClientRating;   // out of 5, shown in the app
        public string Scene;         // scene name in Build Settings
        public int Pay;              // dollars
        public float Minutes;        // time limit
        public string Tag;           // short warning label, e.g. "FRAGILE"
        public string Description;   // the listing text in the app

        // In-gig text
        public string Briefing;      // the client's message on the briefing card
        public string Tips;          // extra hint under the controls
        public string Reviewer;      // name under the review
        public string[] Reviews;     // 3 per star rating, 1 star first (see GigReviews)
    }
}
