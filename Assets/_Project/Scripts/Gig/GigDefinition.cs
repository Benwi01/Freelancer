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
        public int Pay;              // dollars (shown now; earned once money exists)
        public float Minutes;        // time limit
        public string Tag;           // short warning label, e.g. "FRAGILE"
        public string Description;
    }
}
