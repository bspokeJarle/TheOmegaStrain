using System.IO;
using CommonUtilities.CommonGlobalState;
using Domain;

namespace CommonUtilities.CommonSetup
{
    /// <summary>
    /// Centralized audio configuration shared by playback, game loop wiring,
    /// and spatial emitter behavior.
    /// </summary>
    public static class AudioSetup
    {
        public const string AudioBasePath = "Soundeffects";
        public const string SoundRegistryFileName = "sounds.json";

        public const int MusicFadeOutDurationMs = 180;
        public const int MusicFadeOutSteps = 6;

        public const float SpatialPanDistance = 500f;
        public const float SpatialDepthScale = 1200f;

        public const float OffscreenAiAudioMaxDistance = 5000f;
        public const float OffscreenAiAudioCurveExponent = 2.2f;

        public static string SoundRegistryPath => Path.Combine(AudioBasePath, SoundRegistryFileName);

        public static AudioRuntimeSettings CreateRuntimeSettings() => new()
        {
            MusicFadeOutDurationMs = MusicFadeOutDurationMs,
            MusicFadeOutSteps = MusicFadeOutSteps,
            SpatialPanDistance = SpatialPanDistance,
            SpatialDepthScale = SpatialDepthScale,
            VolumeProfileProvider = static () => GameState.SettingsState
        };
    }
}
