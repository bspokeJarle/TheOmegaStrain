using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Numerics;

/// <summary>
/// NAudio-based implementation of IAudioInstance.
/// Represents one active playback chain inside the mixer.
/// The instance owns the per-sound volume and pan stages and can optionally
/// refresh its spatial state from provider delegates on each audio update tick.
/// </summary>
internal sealed class NAudioAudioInstance : IAudioInstance
{
    private bool enableLogging = false;
    private readonly NAudioAudioPlayer _owner;
    private readonly ISampleProvider _pipeline;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly SpatialPanSampleProvider _panProvider;
    private readonly SegmentedLoopSampleProvider? _loopProvider;
    private readonly SoundDefinition _definition;
    private readonly bool _use3dSpatialization;
    private readonly AudioRuntimeSettings _runtimeSettings;

    private float _baseVolume;
    private float _currentSpeed;
    private Vector3 _worldPosition;
    private float _currentPan;
    private bool _hasExplicitPan;
    private bool _isPlaying;
    private readonly AudioPlayMode _mode;

    public Guid Id { get; } = Guid.NewGuid();

    public string SoundId => _definition.Id;

    public bool IsPlaying => _isPlaying;
    public bool IsLooping => _loopProvider?.IsLooping ?? false;

    internal ISampleProvider Pipeline => _pipeline;

    internal NAudioAudioInstance(
        NAudioAudioPlayer owner,
        SoundDefinition definition,
        VolumeSampleProvider volumeProvider,
        SpatialPanSampleProvider panProvider,
        ISampleProvider pipeline,
        SegmentedLoopSampleProvider? loopProvider,
        AudioPlayMode mode,
        float initialVolume,
        float initialSpeed,
        AudioPlayOptions? options,
        AudioRuntimeSettings runtimeSettings)
    {
        _owner = owner;
        _definition = definition;
        _volumeProvider = volumeProvider;
        _panProvider = panProvider;
        _pipeline = pipeline;
        _loopProvider = loopProvider;
        _use3dSpatialization = definition.Settings.Is3D;
        _runtimeSettings = runtimeSettings;
        _baseVolume = SanitizeVolume(initialVolume);
        _currentSpeed = initialSpeed;
        _worldPosition = SanitizeWorldPosition(options?.WorldPosition ?? Vector3.Zero);
        _currentPan = SanitizePan(options?.Pan ?? 0f);
        _hasExplicitPan = options?.Pan != null;
        _isPlaying = true;
        _mode = mode;
        ApplySpatialization();
    }

    public void SetVolume(float volume)
    {
        _baseVolume = SanitizeVolume(volume);
        ApplySpatialization();
    }

    public void SetSpeed(float speed)
    {
        // Playback speed is applied when the instance is created.
        _currentSpeed = speed;
    }

    public void SetWorldPosition(Vector3 position)
    {
        _worldPosition = SanitizeWorldPosition(position);
        ApplySpatialization();
    }

    private void ApplySpatialization()
    {
        float pan = _hasExplicitPan
            ? _currentPan
            : _use3dSpatialization
                ? SanitizePan(_worldPosition.X / Math.Max(1f, _runtimeSettings.SpatialPanDistance))
                : 0f;

        _panProvider.Pan = pan;

        if (!_use3dSpatialization)
        {
            _volumeProvider.Volume = _baseVolume;
            return;
        }

        float depth = MathF.Abs(SanitizeFinite(_worldPosition.Z));
        float attenuation = 1f / (1f + (depth / Math.Max(1f, _runtimeSettings.SpatialDepthScale)));
        _volumeProvider.Volume = SanitizeVolume(_baseVolume * attenuation);
    }

    private static Vector3 SanitizeWorldPosition(Vector3 position)
    {
        return new Vector3(
            SanitizeFinite(position.X),
            SanitizeFinite(position.Y),
            SanitizeFinite(position.Z));
    }

    private static float SanitizePan(float pan)
    {
        return Math.Clamp(SanitizeFinite(pan), -1f, 1f);
    }

    private static float SanitizeVolume(float volume)
    {
        return Math.Max(0f, SanitizeFinite(volume));
    }

    private static float SanitizeFinite(float value)
    {
        return float.IsFinite(value) ? value : 0f;
    }

    public void Stop(bool playEndSegment)
    {
        if (!_isPlaying)
            return;

        if (_mode == AudioPlayMode.SegmentedLoop && _loopProvider != null && playEndSegment)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log(@"Audio:Requested stop with EndSegment.");
            _loopProvider.RequestStopWithEndSegment();
        }
        else
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log(@"Audio:IsPlaying set to false.");
            _isPlaying = false;
            _owner.InternalStopInstance(this);
        }
    }

    internal void MarkFinished()
    {
        if (Logger.ShouldLog(enableLogging)) Logger.Log(@"Audio: Marking instance as finished.");
        _isPlaying = false;
    }
}
