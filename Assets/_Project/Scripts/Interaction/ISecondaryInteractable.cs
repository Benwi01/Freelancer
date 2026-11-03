namespace Freelancer
{
    /// <summary>An interactable with a second action on R (e.g. take your chips back off a roulette spot).</summary>
    public interface ISecondaryInteractable
    {
        /// <summary>Shown after the E prompt, e.g. "Take back $25". Empty = nothing to do right now.</summary>
        string SecondaryPrompt { get; }

        /// <summary>Called on the interacting player's own machine.</summary>
        void SecondaryInteract(PlayerMotor player);
    }
}
