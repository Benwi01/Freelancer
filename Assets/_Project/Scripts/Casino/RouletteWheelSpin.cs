using UnityEngine;

namespace Freelancer
{
    /// <summary>The wheel itself: E spins it (once there's at least one bet on the table).</summary>
    public class RouletteWheelSpin : MonoBehaviour, IInteractable
    {
        [SerializeField] RouletteTable table;

        public string Prompt => table != null ? table.WheelPrompt : "";

        public void Interact(PlayerMotor player) => table?.RequestSpin();
    }
}
