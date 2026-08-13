using Gma.System.MouseKeyHook;

namespace CommonUtilities.Input
{
    public static class InputManager
    {
        private const bool enableLogging = false;
        private static IKeyboardMouseEvents _sharedHook;

        /// <summary>
        /// Gets the shared global keyboard/mouse hook.
        /// Initializes it if not already set.
        /// </summary>
        public static IKeyboardMouseEvents SharedHook
        {
            get
            {
                if (_sharedHook == null)
                {
                    if (Logger.ShouldLog(enableLogging)) Logger.Log("InputManager: Initializing global hook.", "Input");
                    _sharedHook = Hook.GlobalEvents();
                }
                return _sharedHook;
            }
        }

        /// <summary>
        /// Optional: call this on application shutdown if you want to clean up the global hook.
        /// Do not call this mid-game unless you are restarting the entire input system.
        /// </summary>
        public static void Shutdown()
        {
            if (_sharedHook != null)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log("InputManager: Disposing global hook.", "Input");
                _sharedHook.Dispose();
                _sharedHook = null;
            }
        }
    }
}
