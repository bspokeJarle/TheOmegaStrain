using Domain;
using GameAiAndControls.Audio.Services;

namespace _3DSpesificsUnitTests.Audio;

[TestClass]
public class ShipAiVoiceServiceTests
{
    [TestMethod]
    public void TrySpeak_CollisionWarning_PlaysExistingWarningSound()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_collision_warning");

        bool played = service.TrySpeak(ShipAiVoiceCue.CollisionWarning, audio, registry);

        Assert.IsTrue(played);
        Assert.AreEqual(1, audio.PlayedSoundIds.Count);
        Assert.AreEqual("ship_collision_warning", audio.PlayedSoundIds[0]);
    }

    [TestMethod]
    public void TrySpeak_WhenCooldownActive_DoesNotRepeatBonusVoice()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_clean_loop");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));

        now = now.AddSeconds(2);
        Assert.IsFalse(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));
        Assert.AreEqual(1, audio.PlayedSoundIds.Count);
    }

    [TestMethod]
    public void TrySpeak_WarningCanInterruptRecentBonusVoice()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_clean_loop", "ship_collision_warning");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));

        now = now.AddSeconds(2);
        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CollisionWarning, audio, registry));
        Assert.AreEqual("ship_collision_warning", audio.PlayedSoundIds[^1]);
    }

    [TestMethod]
    public void TrySpeak_WhenCueHasNoRegisteredSound_DoesNotPlay()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_collision_warning");

        bool played = service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry);

        Assert.IsFalse(played);
        Assert.AreEqual(0, audio.PlayedSoundIds.Count);
    }

    [TestMethod]
    public void TrySpeak_WhenMultipleLinesAreAvailable_DoesNotRepeatPreviousLine()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_clean_loop", "ship_ai_great_flying");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));
        string firstLine = audio.PlayedSoundIds[0];

        now = now.AddSeconds(9);
        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));

        Assert.AreNotEqual(firstLine, audio.PlayedSoundIds[1]);
    }

    [TestMethod]
    public void TrySpeak_TutorialCue_PlaysTutorialVoiceSound()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro");

        bool played = service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry);

        Assert.IsTrue(played);
        Assert.AreEqual("ship_ai_tutorial_intro", audio.PlayedSoundIds.Single());
    }

    [TestMethod]
    public void TrySpeak_DucksMusicWhileVoiceIsPlayingAndRestoresAfterward()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry));

        Assert.AreEqual(0.0525f, audio.MusicVolume, 0.0001f);

        now = now.AddSeconds(3);
        service.Update(audio);
        Assert.AreEqual(0.0525f, audio.MusicVolume, 0.0001f);

        now = now.AddSeconds(3);
        service.Update(audio);
        Assert.AreEqual(0.15f, audio.MusicVolume, 0.0001f);
    }

    [TestMethod]
    public void TrySpeak_ReappliesMusicDuckingWhenMusicStartsAfterVoice()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer(initialMusicVolume: 0.6f);
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry));
        Assert.AreEqual(0.21f, audio.MusicVolume, 0.0001f);

        audio.PlayMusic(new SoundDefinition { Id = "music_kanpai", Settings = new SoundSettings { Volume = 0.15f } }, 0.15f);
        service.Update(audio);

        Assert.AreEqual(0.0525f, audio.MusicVolume, 0.0001f);

        now = now.AddSeconds(5);
        service.Update(audio);

        Assert.AreEqual(0.15f, audio.MusicVolume, 0.0001f);
    }

    [TestMethod]
    public void TrySpeak_UsesFasterPlaybackSpeedOnlyForTutorialCues()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro", "ship_ai_clean_loop");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry));

        float? speedOverride = audio.SpeedOverrides.Single();
        Assert.IsTrue(speedOverride.HasValue);
        Assert.AreEqual(1.2f, speedOverride.Value, 0.0001f);

        service.StopCurrentSpeech();
        now = now.AddSeconds(9);

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.CleanLoop, audio, registry));
        Assert.IsFalse(audio.SpeedOverrides[^1].HasValue);
    }

    [TestMethod]
    public void TrySpeak_GameStateSaved_UsesCheckpointVoiceAtNormalSpeed()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_checkpoint");

        bool played = service.TrySpeak(ShipAiVoiceCue.GameStateSaved, audio, registry);

        Assert.IsTrue(played);
        Assert.AreEqual("ship_ai_tutorial_checkpoint", audio.PlayedSoundIds.Single());
        Assert.IsFalse(audio.SpeedOverrides.Single().HasValue,
            "Gameplay save confirmation should reuse the training line, but not the tutorial playback speed.");
    }

    [TestMethod]
    public void PendingGameplayCue_PlaysOnlyForGameplayScene()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_checkpoint");

        service.RequestGameplaySaveConfirmation();

        Assert.IsFalse(service.TrySpeakPendingGameplayCue(isGameplayScene: false, audio, registry));
        Assert.AreEqual(0, audio.PlayedSoundIds.Count);

        Assert.IsFalse(service.TrySpeakPendingGameplayCue(isGameplayScene: true, audio, registry),
            "A pending gameplay cue observed outside gameplay should be cleared instead of leaking into the next scene.");
    }

    [TestMethod]
    public void PendingGameplayCue_PlaysSaveConfirmationInGameplay()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_checkpoint");

        service.RequestGameplaySaveConfirmation();

        Assert.IsTrue(service.TrySpeakPendingGameplayCue(isGameplayScene: true, audio, registry));
        Assert.AreEqual("ship_ai_tutorial_checkpoint", audio.PlayedSoundIds.Single());
    }

    [TestMethod]
    public void TrySpeak_DoesNotStartNextTutorialCueWhilePreviousSpeechIsActive()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro", "ship_ai_tutorial_thrust");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry));

        now = now.AddSeconds(2);
        Assert.IsFalse(service.TrySpeak(ShipAiVoiceCue.TutorialThrust, audio, registry));
        Assert.AreEqual(1, audio.PlayedSoundIds.Count);

        now = now.AddSeconds(5);
        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialThrust, audio, registry));
        Assert.AreEqual("ship_ai_tutorial_thrust", audio.PlayedSoundIds[^1]);
    }

    [TestMethod]
    public void StopCurrentSpeech_StopsActiveVoiceAndRestoresMusic()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var service = new ShipAiVoiceService(() => now, new Random(0));
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro", "ship_ai_tutorial_thrust");

        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialIntro, audio, registry));
        var activeVoice = audio.Instances.Single();
        Assert.IsTrue(activeVoice.IsPlaying);
        Assert.AreEqual(0.0525f, audio.MusicVolume, 0.0001f);

        service.StopCurrentSpeech();

        Assert.IsFalse(activeVoice.IsPlaying);
        Assert.AreEqual(0.15f, audio.MusicVolume, 0.0001f);

        now = now.AddSeconds(0.3);
        Assert.IsTrue(service.TrySpeak(ShipAiVoiceCue.TutorialThrust, audio, registry),
            "Skipping a speech line should not block the next tutorial cue for the full original voice duration.");
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        private readonly HashSet<string> _soundIds;

        public FakeSoundRegistry(params string[] soundIds)
        {
            _soundIds = new HashSet<string>(soundIds, StringComparer.OrdinalIgnoreCase);
        }

        public SoundDefinition Get(string id)
        {
            if (!TryGet(id, out var definition))
                throw new KeyNotFoundException(id);

            return definition;
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            if (!_soundIds.Contains(id))
            {
                definition = null!;
                return false;
            }

            definition = new SoundDefinition
            {
                Id = id,
                Usage = id,
                File = $"{id}.wav",
                Settings = new SoundSettings { Volume = 1f }
            };
            return true;
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public CapturingAudioPlayer(float initialMusicVolume = 0.15f)
        {
            MusicVolume = initialMusicVolume;
        }

        public List<string> PlayedSoundIds { get; } = new();
        public List<float?> SpeedOverrides { get; } = new();
        public List<CapturingAudioInstance> Instances { get; } = new();
        public float MusicVolume { get; private set; }
        public List<float> MusicVolumeChanges { get; } = new();

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayedSoundIds.Add(definition.Id);
            SpeedOverrides.Add(options?.SpeedOverride);
            var instance = new CapturingAudioInstance(definition.Id, mode == AudioPlayMode.SegmentedLoop);
            Instances.Add(instance);
            return instance;
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null) =>
            Play(definition, AudioPlayMode.OneShot, options);

        public void Stop(IAudioInstance instance, bool playEndSegment) => instance.Stop(playEndSegment);
        public void StopAll() { }
        public void StopNonMusic() { }
        public void PlayMusic(SoundDefinition definition, float? volumeOverride = null) =>
            MusicVolume = volumeOverride ?? definition.Settings.Volume;
        public void SetMusicVolume(float volume)
        {
            MusicVolume = volume;
            MusicVolumeChanges.Add(volume);
        }
        public void StopMusic() { }
        public void Update(double deltaTimeSeconds) { }
    }

    private sealed class CapturingAudioInstance : IAudioInstance
    {
        public CapturingAudioInstance(string soundId, bool isLooping)
        {
            SoundId = soundId;
            IsLooping = isLooping;
            IsPlaying = true;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string SoundId { get; }
        public bool IsPlaying { get; private set; }
        public bool IsLooping { get; }

        public void SetVolume(float volume) { }
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) { }
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
