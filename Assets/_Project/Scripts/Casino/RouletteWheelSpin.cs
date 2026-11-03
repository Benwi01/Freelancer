using UnityEngine;

namespace Freelancer
{
    /// <summary>The wheel itself: E spins it (once there's at least one bet on the table), R takes all your bets back.</summary>
    public class RouletteWheelSpin : MonoBehaviour, IInteractable, ISecondaryInteractable
    {
        [SerializeField] RouletteTable table;

        public string Prompt => table != null ? table.WheelPrompt : "";

        public void Interact(PlayerMotor player) => table?.RequestSpin();

        public string SecondaryPrompt => table != null ? table.TakeBackAllPrompt : "";

        public void SecondaryInteract(PlayerMotor player) => table?.TakeBack(RouletteBetKind.Red, 0, all: true);
    }
}
