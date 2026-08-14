using Gma.System.MouseKeyHook;
using System.Diagnostics;

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
                    Log("InputManager: Initializing global hook.");
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
                Log("InputManager: Disposing global hook.");
                _sharedHook.Dispose();
                _sharedHook = null;
            }
        }

        private static void Log(string message)
        {
            if (enableLogging)
                Debug.WriteLine($"[Input] {message}");
        }
    }
}
