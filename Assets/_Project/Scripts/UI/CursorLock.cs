using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// One place that decides whether the mouse is captured for looking around.
    /// Free whenever a menu or results screen is up.
    /// </summary>
    public static class CursorLock
    {
        /// <summary>Set by screens like the results panel that need the mouse.</summary>
        public static bool ScreenWantsCursor;

        public static bool GameplayInputAllowed => !SessionHud.MenuOpen && !ScreenWantsCursor;

        public static void Refresh()
        {
            bool free = !GameplayInputAllowed;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
}
