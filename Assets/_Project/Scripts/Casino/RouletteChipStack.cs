using UnityEngine;

namespace Freelancer
{
    /// <summary>A stack of chips on the table rail: E picks that chip size for your next bets (just for you).</summary>
    public class RouletteChipStack : MonoBehaviour, IInteractable
    {
        [SerializeField] int amount = 25;

        public int Amount => amount;
        public bool Selected => RouletteTable.LocalChip == amount;

        public string Prompt => Selected ? $"${amount} chips (in hand)" : $"Take ${amount} chips";

        public void Interact(PlayerMotor player) => RouletteTable.LocalChip = amount;
    }
}
