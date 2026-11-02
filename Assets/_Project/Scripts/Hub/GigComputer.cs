using UnityEngine;

namespace Freelancer
{
    /// <summary>The apartment computer: press E to open the GigGrabber app.</summary>
    public class GigComputer : MonoBehaviour, IInteractable
    {
        public string Prompt => "Use computer (GigGrabber)";

        public void Interact(PlayerMotor player)
        {
            if (GigComputerUI.Instance != null) GigComputerUI.Instance.Open();
        }
    }
}
