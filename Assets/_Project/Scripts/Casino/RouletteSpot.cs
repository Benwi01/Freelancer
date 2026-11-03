using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A betting spot on the felt (a number, red, odd...): look at it and press E to put a chip down,
    /// R to take your chips on it back (before the spin).
    /// </summary>
    public class RouletteSpot : MonoBehaviour, IInteractable, ISecondaryInteractable
    {
        [SerializeField] RouletteTable table;
        [SerializeField] RouletteBetKind kind;
        [SerializeField] int number;

        public RouletteTable Table => table;
        public RouletteBetKind Kind => kind;
        public int Number => number;

        public string Prompt => table != null ? table.SpotPrompt(kind, number) : "";

        public void Interact(PlayerMotor player) => table?.PlaceBet(kind, number, RouletteTable.LocalChip);

        public string SecondaryPrompt => table != null ? table.TakeBackPrompt(kind, number) : "";

        public void SecondaryInteract(PlayerMotor player) => table?.TakeBack(kind, number, all: false);
    }
}
