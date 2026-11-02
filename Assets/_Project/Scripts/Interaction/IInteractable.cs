namespace Freelancer
{
    /// <summary>Something a player can look at and press E on (computers, later doors, shops...).</summary>
    public interface IInteractable
    {
        /// <summary>Shown next to the crosshair, e.g. "Use computer".</summary>
        string Prompt { get; }

        /// <summary>Called on the interacting player's own machine.</summary>
        void Interact(PlayerMotor player);
    }
}
