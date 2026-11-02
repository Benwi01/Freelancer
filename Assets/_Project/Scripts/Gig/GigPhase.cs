namespace Freelancer
{
    public enum GigPhase : byte
    {
        Briefing,
        Running,
        Delivered,
        Failed,
    }

    public enum GigFailReason : byte
    {
        None,
        TimeUp,
        CakeRuined,
        NoDrinksServed,
        Fired,
    }
}
