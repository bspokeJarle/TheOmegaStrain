using Domain;
using System;
using System.Collections.Generic;

namespace GameAiAndControls.Audio.Services
{
    public enum ShipAiVoiceCue
    {
        CollisionWarning,
        CleanLoop,
        CollisionLoop,
        LowAltitudeRisk,
        PlanetBonusComplete,
        GameStateSaved,
        TutorialIntro,
        TutorialThrust,
        TutorialWeapons,
        TutorialSeederOneDown,
        TutorialPowerup,
        TutorialDecoySelect,
        TutorialDroneInbound,
        TutorialDroneDestroyed,
        TutorialComplete,
        TutorialSkip,
        TutorialWarningLowAltitude,
        TutorialCheckpoint,
        TutorialLaserHint,
        TutorialDecoyHint
    }

    public enum ShipAiVoicePriority
    {
        Bonus = 0,
        Warning = 1,
        Critical = 2
    }

    public sealed class ShipAiVoiceService
    {
        private sealed class CueConfig
        {
            public CueConfig(
                ShipAiVoicePriority priority,
                double cooldownSeconds,
                double interruptCooldownSeconds,
                float? volumeOverride,
                float? speedOverride,
                double speechSeconds,
                params string[] soundIds)
            {
                Priority = priority;
                CooldownSeconds = cooldownSeconds;
                InterruptCooldownSeconds = interruptCooldownSeconds;
                VolumeOverride = volumeOverride;
                SpeedOverride = speedOverride;
                SpeechSeconds = speechSeconds;
                SoundIds = soundIds;
            }

            public ShipAiVoicePriority Priority { get; }
            public double CooldownSeconds { get; }
            public double InterruptCooldownSeconds { get; }
            public float? VolumeOverride { get; }
            public float? SpeedOverride { get; }
            public double SpeechSeconds { get; }
            public IReadOnlyList<string> SoundIds { get; }
        }

        private const float MusicDuckingFactor = 0.35f;
        private const float TutorialVoicePlaybackSpeed = 1.2f;
        private const double SpeechGapSeconds = 0.35;
        private const double DefaultSpeechSeconds = 3.0;
        private const float VolumeEpsilon = 0.0001f;

        private static readonly IReadOnlyDictionary<ShipAiVoiceCue, CueConfig> CueConfigs =
            new Dictionary<ShipAiVoiceCue, CueConfig>
            {
                [ShipAiVoiceCue.CollisionWarning] = new(
                    ShipAiVoicePriority.Warning,
                    cooldownSeconds: 1.5,
                    interruptCooldownSeconds: 0.25,
                    volumeOverride: 1.0f,
                    speedOverride: null,
                    speechSeconds: 1.25,
                    "ship_collision_warning"),

                [ShipAiVoiceCue.CleanLoop] = new(
                    ShipAiVoicePriority.Bonus,
                    cooldownSeconds: 8.0,
                    interruptCooldownSeconds: 8.0,
                    volumeOverride: 0.75f,
                    speedOverride: null,
                    speechSeconds: 1.6,
                    "ship_ai_clean_loop",
                    "ship_ai_great_flying"),

                [ShipAiVoiceCue.CollisionLoop] = new(
                    ShipAiVoicePriority.Bonus,
                    cooldownSeconds: 8.0,
                    interruptCooldownSeconds: 8.0,
                    volumeOverride: 0.7f,
                    speedOverride: null,
                    speechSeconds: 1.6,
                    "ship_ai_close_one",
                    "ship_ai_maneuver_unstable"),

                [ShipAiVoiceCue.LowAltitudeRisk] = new(
                    ShipAiVoicePriority.Bonus,
                    cooldownSeconds: 8.0,
                    interruptCooldownSeconds: 8.0,
                    volumeOverride: 0.7f,
                    speedOverride: null,
                    speechSeconds: 1.8,
                    "ship_ai_low_altitude_bonus"),

                [ShipAiVoiceCue.PlanetBonusComplete] = new(
                    ShipAiVoicePriority.Bonus,
                    cooldownSeconds: 12.0,
                    interruptCooldownSeconds: 12.0,
                    volumeOverride: 0.7f,
                    speedOverride: null,
                    speechSeconds: 2.0,
                    "ship_ai_planet_bonus_complete"),

                [ShipAiVoiceCue.GameStateSaved] = new(
                    ShipAiVoicePriority.Warning,
                    cooldownSeconds: 0.25,
                    interruptCooldownSeconds: 0.25,
                    volumeOverride: 0.75f,
                    speedOverride: null,
                    speechSeconds: 4.4,
                    "ship_ai_tutorial_checkpoint"),

                [ShipAiVoiceCue.TutorialIntro] = Tutorial("ship_ai_tutorial_intro", 5.8),
                [ShipAiVoiceCue.TutorialThrust] = Tutorial("ship_ai_tutorial_thrust", 9.5),
                [ShipAiVoiceCue.TutorialWeapons] = Tutorial("ship_ai_tutorial_weapons", 14.2),
                [ShipAiVoiceCue.TutorialSeederOneDown] = Tutorial("ship_ai_tutorial_seeder_one_down", 4.8),
                [ShipAiVoiceCue.TutorialPowerup] = Tutorial("ship_ai_tutorial_powerup", 5.0),
                [ShipAiVoiceCue.TutorialDecoySelect] = Tutorial("ship_ai_tutorial_decoy_select", 6.2),
                [ShipAiVoiceCue.TutorialDroneInbound] = Tutorial("ship_ai_tutorial_drone_inbound", 6.0),
                [ShipAiVoiceCue.TutorialDroneDestroyed] = Tutorial("ship_ai_tutorial_drone_destroyed", 5.0),
                [ShipAiVoiceCue.TutorialComplete] = Tutorial("ship_ai_tutorial_complete", 5.8),
                [ShipAiVoiceCue.TutorialSkip] = Tutorial("ship_ai_tutorial_skip", 4.8),
                [ShipAiVoiceCue.TutorialWarningLowAltitude] = Tutorial("ship_ai_tutorial_warning_low_altitude", 4.3),
                [ShipAiVoiceCue.TutorialCheckpoint] = Tutorial("ship_ai_tutorial_checkpoint", 4.4),
                [ShipAiVoiceCue.TutorialLaserHint] = Tutorial("ship_ai_tutorial_laser_hint", 4.5),
                [ShipAiVoiceCue.TutorialDecoyHint] = Tutorial("ship_ai_tutorial_decoy_hint", 6.0)
            };

        public static ShipAiVoiceService Shared { get; } = new();

        private readonly Func<DateTime> _now;
        private readonly Random _random;
        private DateTime _lastSpokenAt = DateTime.MinValue;
        private ShipAiVoicePriority _lastPriority = ShipAiVoicePriority.Bonus;
        private string? _lastSoundId;
        private DateTime _speechAvailableAt = DateTime.MinValue;
        private DateTime _musicRestoreAt = DateTime.MinValue;
        private bool _musicDucked;
        private float _musicVolumeBeforeDuck = 1f;
        private float _duckedMusicVolume = 1f;
        private IAudioInstance? _activeSpeechInstance;
        private IAudioPlayer? _activeAudioPlayer;
        private ShipAiVoiceCue? _pendingGameplayCue;

        public ShipAiVoiceService(Func<DateTime>? now = null, Random? random = null)
        {
            _now = now ?? (() => DateTime.Now);
            _random = random ?? new Random();
        }

        public bool TrySpeak(
            ShipAiVoiceCue cue,
            IAudioPlayer? audioPlayer,
            ISoundRegistry? soundRegistry,
            AudioPlayOptions? options = null)
        {
            Update(audioPlayer);

            if (audioPlayer == null || soundRegistry == null)
                return false;

            if (!CueConfigs.TryGetValue(cue, out var config))
                return false;

            var now = _now();
            if (!CanSpeak(config, now))
                return false;

            if (!TryPickSound(config.SoundIds, soundRegistry, out var definition))
                return false;

            var playOptions = BuildOptions(config, options);
            if (_activeSpeechInstance?.IsPlaying == true)
                _activeSpeechInstance.Stop(playEndSegment: false);

            _activeSpeechInstance = audioPlayer.Play(definition, AudioPlayMode.OneShot, playOptions);
            _activeAudioPlayer = audioPlayer;

            var speechSeconds = ResolveSpeechSeconds(config, definition, playOptions.SpeedOverride);
            DuckMusic(audioPlayer, now, speechSeconds);

            _lastSpokenAt = now;
            _lastPriority = config.Priority;
            _lastSoundId = definition.Id;
            _speechAvailableAt = now.AddSeconds(speechSeconds + SpeechGapSeconds);
            return true;
        }

        public static bool TryGetEstimatedSpeechSeconds(ShipAiVoiceCue cue, out double speechSeconds)
        {
            speechSeconds = 0.0;
            if (!CueConfigs.TryGetValue(cue, out var config))
                return false;

            double baseSeconds = config.SpeechSeconds > 0.1
                ? config.SpeechSeconds
                : DefaultSpeechSeconds;

            float speed = config.SpeedOverride.GetValueOrDefault(1f);
            speechSeconds = speed > 0.1f
                ? baseSeconds / speed
                : baseSeconds;
            return true;
        }

        public void RequestGameplaySaveConfirmation()
        {
            _pendingGameplayCue = ShipAiVoiceCue.GameStateSaved;
        }

        public bool TrySpeakPendingGameplayCue(
            bool isGameplayScene,
            IAudioPlayer? audioPlayer,
            ISoundRegistry? soundRegistry)
        {
            Update(audioPlayer);

            if (!isGameplayScene)
            {
                _pendingGameplayCue = null;
                return false;
            }

            if (!_pendingGameplayCue.HasValue)
                return false;

            var cue = _pendingGameplayCue.Value;
            if (!TrySpeak(cue, audioPlayer, soundRegistry))
                return false;

            _pendingGameplayCue = null;
            return true;
        }

        public void Update(IAudioPlayer? audioPlayer)
        {
            if (audioPlayer == null || !_musicDucked)
                return;

            if (_activeSpeechInstance?.IsPlaying == false)
                _activeSpeechInstance = null;

            if (_now() < _musicRestoreAt)
            {
                if (MathF.Abs(audioPlayer.MusicVolume - _duckedMusicVolume) > VolumeEpsilon)
                {
                    _musicVolumeBeforeDuck = audioPlayer.MusicVolume;
                    _duckedMusicVolume = CalculateDuckedVolume(_musicVolumeBeforeDuck);
                    audioPlayer.SetMusicVolume(_duckedMusicVolume);
                }

                return;
            }

            audioPlayer.SetMusicVolume(_musicVolumeBeforeDuck);
            _musicDucked = false;
        }

        public void StopCurrentSpeech()
        {
            var now = _now();

            if (_activeSpeechInstance?.IsPlaying == true)
                _activeSpeechInstance.Stop(playEndSegment: false);

            if (_musicDucked && _activeAudioPlayer != null)
                _activeAudioPlayer.SetMusicVolume(_musicVolumeBeforeDuck);

            _activeSpeechInstance = null;
            _activeAudioPlayer = null;
            _pendingGameplayCue = null;
            _musicDucked = false;
            _musicRestoreAt = DateTime.MinValue;
            _speechAvailableAt = now;
        }

        private bool CanSpeak(CueConfig config, DateTime now)
        {
            if (now < _speechAvailableAt)
                return false;

            if (_lastSpokenAt == DateTime.MinValue)
                return true;

            double elapsed = (now - _lastSpokenAt).TotalSeconds;
            if (elapsed >= config.CooldownSeconds)
                return true;

            return config.Priority > _lastPriority &&
                   elapsed >= config.InterruptCooldownSeconds;
        }

        private void DuckMusic(IAudioPlayer audioPlayer, DateTime now, double speechSeconds)
        {
            if (!_musicDucked)
            {
                _musicVolumeBeforeDuck = audioPlayer.MusicVolume;
                _duckedMusicVolume = CalculateDuckedVolume(_musicVolumeBeforeDuck);
                audioPlayer.SetMusicVolume(_duckedMusicVolume);
                _musicDucked = true;
            }

            _musicRestoreAt = now.AddSeconds(speechSeconds);
        }

        private static float CalculateDuckedVolume(float baselineVolume) =>
            Math.Clamp(baselineVolume * MusicDuckingFactor, 0f, baselineVolume);

        private static double ResolveSpeechSeconds(CueConfig config, SoundDefinition definition, float? speedOverride)
        {
            double segmentSeconds = definition.Segments.End - definition.Segments.Start;
            double baseSeconds = segmentSeconds > 0.1
                ? segmentSeconds
                : config.SpeechSeconds > 0.1
                    ? config.SpeechSeconds
                    : DefaultSpeechSeconds;

            float speed = speedOverride.GetValueOrDefault(1f);
            return speed > 0.1f ? baseSeconds / speed : baseSeconds;
        }

        private bool TryPickSound(
            IReadOnlyList<string> soundIds,
            ISoundRegistry soundRegistry,
            out SoundDefinition definition)
        {
            var available = new List<SoundDefinition>();
            foreach (string soundId in soundIds)
            {
                if (soundRegistry.TryGet(soundId, out var soundDefinition))
                    available.Add(soundDefinition);
            }

            if (available.Count == 0)
            {
                definition = null!;
                return false;
            }

            if (available.Count > 1 && _lastSoundId != null)
                available.RemoveAll(sound => string.Equals(sound.Id, _lastSoundId, StringComparison.OrdinalIgnoreCase));

            definition = available[_random.Next(available.Count)];
            return true;
        }

        private static AudioPlayOptions BuildOptions(CueConfig config, AudioPlayOptions? options)
        {
            var result = new AudioPlayOptions
            {
                VolumeOverride = options?.VolumeOverride ?? config.VolumeOverride,
                SpeedOverride = options?.SpeedOverride ?? config.SpeedOverride,
                Pan = options?.Pan,
                WorldPosition = options?.WorldPosition,
                Tag = options?.Tag
            };

            return result;
        }

        private static CueConfig Tutorial(string soundId, double speechSeconds) =>
            new(
                ShipAiVoicePriority.Critical,
                cooldownSeconds: 0.25,
                interruptCooldownSeconds: 0.0,
                volumeOverride: 0.75f,
                speedOverride: TutorialVoicePlaybackSpeed,
                speechSeconds: speechSeconds,
                soundId);
    }
}
