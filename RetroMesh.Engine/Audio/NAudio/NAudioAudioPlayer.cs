using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.Threading;

namespace RetroMesh.Engine;

/// <summary>
/// NAudio-based implementation of IAudioPlayer.
/// Each sound is turned into a small processing chain and then routed into a shared mixer:
/// file reader -> optional segmented loop -> normalization -> volume -> stereo pan -> mixer.
/// </summary>
public sealed class NAudioAudioPlayer : IAudioPlayer, IDisposable
{
    private bool enableLogging = false;
    private readonly IWavePlayer _outputDevice;
    private readonly MixingSampleProvider _mixer;
    private readonly ConcurrentDictionary<Guid, NAudioAudioInstance> _instances =
        new();

    private readonly string _audioBasePath;
    private readonly Random _rng = new();
    private readonly AudioRuntimeSettings _runtimeSettings;

    private NAudioAudioInstance? _musicInstance;
    private float _musicVolume = 0.6f;
    public float MusicVolume => _musicVolume;

    public NAudioAudioPlayer(string audioBasePath)
        : this(audioBasePath, new AudioRuntimeSettings())
    {
    }

    public NAudioAudioPlayer(string audioBasePath, AudioRuntimeSettings? runtimeSettings)
    {
        _audioBasePath = audioBasePath;
        _runtimeSettings = runtimeSettings ?? new AudioRuntimeSettings();

        // The mixer runs in one fixed format so every sound can be combined safely.
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _mixer = new MixingSampleProvider(waveFormat)
        {
            ReadFully = true
        };

        _outputDevice = new WaveOutEvent();
        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    public void Dispose()
    {
        _outputDevice?.Stop();
        _outputDevice?.Dispose();
    }

    public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
    {
        var fullPath = System.IO.Path.Combine(_audioBasePath, definition.File);
        if (!System.IO.File.Exists(fullPath))
            throw new System.IO.FileNotFoundException($"Audio file not found: {fullPath}");

        // AudioFileReader acts as both a decoded stream and an ISampleProvider.
        var fileReader = new AudioFileReader(fullPath);

        // For segmented-loop sounds the loop provider must wrap the file reader
        // directly so that loop-back seeks are not blocked by intermediate
        // resampler / channel-converter buffers.
        SegmentedLoopSampleProvider? loopProvider = null;
        ISampleProvider source = fileReader;

        if (mode == AudioPlayMode.SegmentedLoop)
        {
            loopProvider = new SegmentedLoopSampleProvider(
                fileReader,
                fileReader,
                definition.Segments,
                fileReader.WaveFormat);

            source = loopProvider;
            if (Logger.ShouldLog(enableLogging)) Logger.Log(@"Audio: Playing segmented loop.");
        }
        else if (mode == AudioPlayMode.OneShot)
        {
            // OneShot with segment markers: seek to start, truncate at end.
            if (definition.Segments.Start > 0)
                fileReader.CurrentTime = TimeSpan.FromSeconds(definition.Segments.Start);

            double segDuration = definition.Segments.End - definition.Segments.Start;
            if (segDuration > 0 && definition.Segments.End < fileReader.TotalTime.TotalSeconds)
                source = new TruncatingSampleProvider(source, TimeSpan.FromSeconds(segDuration));
        }

        // Playback speed is stored for future pitch/varispeed support.
        float speed;
        if (options?.SpeedOverride is { } speedOverride)
            speed = speedOverride;
        else
            speed = definition.Speed.GetRandomizedSpeed(_rng);

        if (MathF.Abs(speed - 1f) > 0.001f)
            source = new PlaybackRateSampleProvider(source, speed);

        // Normalize the source into the shared mixer format.
        ISampleProvider sample = NormalizeToMixerFormat(source);

        // Apply the requested base volume before any spatial attenuation.
        float baseVolume = definition.Settings.Volume;
        if (options?.VolumeOverride is { } volOverride)
            baseVolume = volOverride;
        baseVolume = ApplyUserSettingsVolume(definition, baseVolume);

        var volumeProvider = new VolumeSampleProvider(sample)
        {
            Volume = baseVolume
        };

        var panProvider = new SpatialPanSampleProvider(volumeProvider);
        ISampleProvider pipeline = panProvider;

        var instance = new NAudioAudioInstance(
            this,
            definition,
            volumeProvider,
            panProvider,
            pipeline,
            loopProvider,
            mode,
            baseVolume,
            speed,
            options,
            _runtimeSettings);

        _instances[instance.Id] = instance;
        _mixer.AddMixerInput(pipeline);
        if (Logger.ShouldLog(enableLogging)) Logger.Log(@"Audio: Play end of method.");
        return instance;
    }

    public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null)
    {
        _ = Play(definition, AudioPlayMode.OneShot, options);
        // For one-shot, we rely on AudioFileReader naturally reaching EOF.
    }

    public void Stop(IAudioInstance instance, bool playEndSegment)
    {
        if (instance is NAudioAudioInstance naudioInstance)
        {
            naudioInstance.Stop(playEndSegment);
        }
    }

    public void StopAll()
    {
        foreach (var kvp in _instances)
        {
            kvp.Value.Stop(playEndSegment: false);
        }

        _instances.Clear();
    }

    public void StopNonMusic()
    {
        foreach (var kvp in _instances)
        {
            var instance = kvp.Value;
            if (_musicInstance != null && ReferenceEquals(instance, _musicInstance))
                continue;

            instance.Stop(playEndSegment: false);
        }
    }

    /// <summary>
    /// Plays background music using segmented loop.
    /// The SoundDefinition's segments (start, loopStart, loopEnd, end)
    /// define which part of the file is used.
    /// Any previously playing music instance is stopped first.
    /// </summary>
    public void PlayMusic(SoundDefinition definition, float? volumeOverride = null)
    {
        // Stop the previous music instance before starting a new one.
        if (_musicInstance != null)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log("Audio: Stopping previous music instance before starting new.");
            FadeOutAndStopMusicInstance(_musicInstance);
        }

        // Clamp the requested music volume to a safe range.
        float vol = volumeOverride ?? definition.Settings.Volume;
        if (vol < 0f) vol = 0f;
        if (vol > 1f) vol = 1f;
        _musicVolume = vol;

        if (Logger.ShouldLog(enableLogging)) Logger.Log(
            $"Audio: PlayMusic id={definition.Id}, " +
            $"segments: start={definition.Segments.Start:F2}, " +
            $"loopStart={definition.Segments.LoopStart:F2}, " +
            $"loopEnd={definition.Segments.LoopEnd:F2}, end={definition.Segments.End:F2}");

        // Music uses segmented looping so intro / loop / outro boundaries can be respected.
        var instance = Play(
            definition,
            AudioPlayMode.SegmentedLoop,
            new AudioPlayOptions
            {
                VolumeOverride = vol
                // SpeedOverride is optional, so the sound definition controls playback speed.
            });

        // The current implementation always returns NAudioAudioInstance.
        _musicInstance = instance as NAudioAudioInstance;
    }

    private float ApplyUserSettingsVolume(SoundDefinition definition, float baseVolume)
    {
        var volumeProfile = _runtimeSettings.GetVolumeProfile();
        if (volumeProfile == null)
            return baseVolume;

        if (IsMusicSound(definition))
            return baseVolume;

        return volumeProfile.IsVoiceSound(definition.Id, definition.Usage)
            ? volumeProfile.ApplyVoiceVolume(baseVolume)
            : volumeProfile.ApplyEffectsVolume(baseVolume);
    }

    private static bool IsMusicSound(SoundDefinition definition)
    {
        return StartsWithIgnoreCase(definition.Id, "music_") ||
               StartsWithIgnoreCase(definition.Usage, "Music");
    }

    private static bool StartsWithIgnoreCase(string? value, string prefix) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stops the currently playing music, if any.
    /// </summary>
    public void StopMusic()
    {
        if (_musicInstance != null)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log("Audio: Stopping music instance.");
            FadeOutAndStopMusicInstance(_musicInstance);
        }
    }

    private void FadeOutAndStopMusicInstance(NAudioAudioInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        float startVolume = _musicVolume;

        if (_runtimeSettings.MusicFadeOutDurationMs > 0 && _runtimeSettings.MusicFadeOutSteps > 0 && startVolume > 0f)
        {
            int sleepPerStepMs = Math.Max(1, _runtimeSettings.MusicFadeOutDurationMs / _runtimeSettings.MusicFadeOutSteps);

            for (int step = _runtimeSettings.MusicFadeOutSteps - 1; step >= 0; step--)
            {
                instance.SetVolume(startVolume * step / _runtimeSettings.MusicFadeOutSteps);
                Thread.Sleep(sleepPerStepMs);
            }
        }

        instance.Stop(playEndSegment: false);

        if (_musicInstance == instance)
        {
            _musicInstance = null;
        }
    }

    /// <summary>
    /// Adjusts music volume at runtime.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Math.Clamp(volume, 0f, 1f);

        if (_musicInstance != null)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log($"Audio: Setting music volume to {_musicVolume:F2}.");
            _musicInstance.SetVolume(_musicVolume);
        }
    }

    private ISampleProvider NormalizeToMixerFormat(ISampleProvider source)
    {
        ISampleProvider sample = source;

        // 1) Convert channel count as needed so every input matches the stereo mixer.
        if (sample.WaveFormat.Channels != _mixer.WaveFormat.Channels)
        {
            if (sample.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
            {
                // Mono -> stereo
                sample = new MonoToStereoSampleProvider(sample);
            }
            else if (sample.WaveFormat.Channels == 2 && _mixer.WaveFormat.Channels == 1)
            {
                // Stereo -> mono
                sample = new StereoToMonoSampleProvider(sample);
            }
            else
            {
                throw new NotSupportedException(
                    $"Unsupported channel config: source={sample.WaveFormat.Channels}, mixer={_mixer.WaveFormat.Channels}");
            }
        }

        // 2) Resample when the source sample rate does not match the mixer sample rate.
        if (sample.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
        {
            sample = new WdlResamplingSampleProvider(sample, _mixer.WaveFormat.SampleRate);
        }

        return sample;
    }

    public void Update(double deltaTimeSeconds)
    {
        // No per-frame audio updates are currently required.
    }

    internal void InternalStopInstance(NAudioAudioInstance instance)
    {
        _instances.TryRemove(instance.Id, out _);

        if (_musicInstance == instance)
        {
            _musicInstance = null;
        }

        _mixer.RemoveMixerInput(instance.Pipeline);
    }
}
