using System;

namespace Domain;

public interface IAudioVolumeProfile
{
    float ApplyMusicVolume(float baseVolume);
    float ApplyEffectsVolume(float baseVolume);
    float ApplyVoiceVolume(float baseVolume);
    bool IsVoiceSound(string? soundId, string? usage);
}

public sealed class AudioRuntimeSettings
{
    public const int DefaultMusicFadeOutDurationMs = 180;
    public const int DefaultMusicFadeOutSteps = 6;
    public const float DefaultSpatialPanDistance = 500f;
    public const float DefaultSpatialDepthScale = 1200f;

    public int MusicFadeOutDurationMs { get; set; } = DefaultMusicFadeOutDurationMs;
    public int MusicFadeOutSteps { get; set; } = DefaultMusicFadeOutSteps;
    public float SpatialPanDistance { get; set; } = DefaultSpatialPanDistance;
    public float SpatialDepthScale { get; set; } = DefaultSpatialDepthScale;
    public Func<IAudioVolumeProfile?> VolumeProfileProvider { get; set; } = static () => null;

    public IAudioVolumeProfile? GetVolumeProfile() => VolumeProfileProvider();
}
