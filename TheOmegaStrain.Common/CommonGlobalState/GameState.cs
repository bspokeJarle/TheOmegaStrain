using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TheOmegaStrain.Common.CommonGlobalState
{
    // This class holds global state information for the game, expand as needed
    // No deep copy for this class, it's intended to be a singleton-like static holder of state
    public static class GameState
    {
        public static ShipState ShipState = new ShipState();
        public static SurfaceState SurfaceState = new SurfaceState();
        public static ScreenOverlayState ScreenOverlayState = new ScreenOverlayState();
        public static WorldFadeState WorldFade = new WorldFadeState();
        public static WeatherVisualState WeatherVisualState = new WeatherVisualState();
        public static GamePlayState GamePlayState = new GamePlayState();
        public static GameSettingsState SettingsState = new GameSettingsState();
        public static TutorialRuntimeState TutorialState = new TutorialRuntimeState();
        public static IGameEventBus? EventBus { get; set; }
        public static List<_3dSpecificsImplementations._3dObject> PendingWorldObjects { get; } = new();
        public static long FrameCount { get; set; } = 0;
        public static int ObjectIdCounter { get; set; } = 0;
        public const float GameplayBaselineFps = FrameTimingMath.DefaultGameplayBaselineFps;
        public const float GameplayBaselineDeltaTime = FrameTimingMath.DefaultGameplayBaselineDeltaTime;
        public static float DeltaTime { get; set; } = GameplayBaselineDeltaTime;
        public static float ClampedDeltaTime => FrameTimingMath.ClampDeltaTime(DeltaTime);
        public static float FrameScale90 => FrameTimingMath.GetFrameScale(DeltaTime, GameplayBaselineFps);
        public static float ScaleDampingPer90Frame(float perFrameDamping)
        {
            return FrameTimingMath.ScaleDampingPerFrame(perFrameDamping, DeltaTime, GameplayBaselineFps);
        }
    }
}
