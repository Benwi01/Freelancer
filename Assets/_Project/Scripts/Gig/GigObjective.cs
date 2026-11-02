using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// The gig-specific half of a <see cref="GigDirector"/> (same object): decides when the gig is
    /// won or lost and what the HUD says. The director owns the clock, pay and results.
    /// </summary>
    public abstract class GigObjective : NetworkBehaviour
    {
        /// <summary>Server, every frame while the clock runs. Call director.Finish to end the gig.</summary>
        public abstract void ServerTick(GigDirector director);

        /// <summary>Bottom-of-screen instruction while running.</summary>
        public abstract string ObjectiveText(GigDirector director);

        /// <summary>One line under the results title.</summary>
        public abstract string ResultDetail(GigDirector director);

        /// <summary>Money line when the gig failed.</summary>
        public virtual string NoPayLine => "No job, no cash.";

        /// <summary>Top-left status meter: title, fill (0..1), text on the bar, bar colour.</summary>
        public abstract void Status(out string title, out float fill, out string text, out Color color);

        /// <summary>0..1 "almost done" bar in the middle of the screen (0 hides it).</summary>
        public virtual float Progress => 0f;

        /// <summary>Big temporary shout-out in the middle of the screen (empty = none).</summary>
        public virtual string Callout => "";
    }
}
