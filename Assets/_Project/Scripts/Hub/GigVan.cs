using UnityEngine;

namespace Freelancer
{
    /// <summary>The crew's van at the curb: press E for the GigGrabber app on the dashboard tablet.</summary>
    public class GigVan : MonoBehaviour, IInteractable
    {
        public string Prompt => "Check gigs (van tablet)";

        public void Interact(PlayerMotor player)
        {
            if (GigComputerUI.Instance != null) GigComputerUI.Instance.Open();
        }
    }
}
