using UnityEngine;

namespace Freelancer
{
    /// <summary>A betting spot on the felt (a number, red, odd...): look at it and press E to put a chip down.</summary>
    public class RouletteSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] RouletteTable table;
        [SerializeField] RouletteBetKind kind;
        [SerializeField] int number;

        public RouletteTable Table => table;
        public RouletteBetKind Kind => kind;
        public int Number => number;

        public string Prompt => table != null ? table.SpotPrompt(kind, number) : "";

        public void Interact(PlayerMotor player) => table?.PlaceBet(kind, number, RouletteTable.LocalChip);
    }
}
