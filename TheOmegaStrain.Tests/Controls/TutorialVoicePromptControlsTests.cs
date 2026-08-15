using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Audio.Services;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class TutorialVoicePromptControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ShipState = new ShipState();
        GameState.TutorialState = new TutorialRuntimeState();
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 1000f, y = 0f, z = 1000f };
        GameState.ShipState.ShipWorldPosition = new Vector3 { x = 1000f, y = 0f, z = 1000f };
    }

    [TestMethod]
    public void MoveObject_PlaysTutorialCuesFromObjectiveProgress()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var voiceService = new ShipAiVoiceService(() => now, new Random(0));
        var control = new TutorialVoicePromptControls(() => now, voiceService);
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry(
            "ship_ai_tutorial_intro",
            "ship_ai_tutorial_thrust",
            "ship_ai_tutorial_checkpoint",
            "ship_ai_tutorial_weapons",
            "ship_ai_tutorial_seeder_one_down",
            "ship_ai_tutorial_powerup",
            "ship_ai_tutorial_decoy_select",
            "ship_ai_tutorial_drone_inbound",
            "ship_ai_tutorial_decoy_hint",
            "ship_ai_tutorial_drone_destroyed",
            "ship_ai_tutorial_complete");
        var marker = CreateMarker();
        var seeder = CreateSeeder(new Vector3 { x = 5000f, y = 0f, z = 1000f });
        GameState.SurfaceState.AiObjects.Add(seeder);
        GameState.ScreenOverlayState.ShowOverlay = true;

        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_intro");
        Assert.IsTrue(GameState.TutorialState.InstructionOverlayPauseActive);
        DismissTutorialOverlay();

        now = now.AddSeconds(8.7);
        control.MoveObject(marker, audio, registry);
        Assert.AreEqual(1, audio.PlayedSoundIds.Count);

        now = now.AddSeconds(0.2);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_thrust");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "LEFT and RIGHT");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "UP and DOWN");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "guidance arrow");
        DismissTutorialOverlay();

        GameState.ShipState.ShipWorldPosition = new Vector3 { x = 1150f, y = 0f, z = 1000f };
        now = now.AddSeconds(10.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_checkpoint");

        seeder.WorldPosition = new Vector3 { x = 2700f, y = 0f, z = 1000f };
        now = now.AddSeconds(5.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_weapons");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "front");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "attack angle");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "debris");
        DismissTutorialOverlay();

        seeder.ImpactStatus!.ObjectHealth = 0;
        now = now.AddSeconds(15.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_seeder_one_down");

        var powerUp = CreatePowerUp(new Vector3 { x = 1400f, y = 0f, z = 1000f });
        GameState.SurfaceState.AiObjects.Add(powerUp);
        now = now.AddSeconds(5.5);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_powerup");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "Fly through it");
        DismissTutorialOverlay();

        now = now.AddSeconds(8.5);
        control.MoveObject(marker, audio, registry);
        Assert.AreEqual("ship_ai_tutorial_powerup", audio.PlayedSoundIds[^1]);

        GameState.GamePlayState.PowerUpsCollected = 1;
        now = now.AddSeconds(0.1);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_decoy_select");
        Assert.IsTrue(GameState.TutorialState.DecoySelectCueSpoken);
        DismissTutorialOverlay();

        var drone = CreateDrone(new Vector3 { x = 1600f, y = 0f, z = 1000f });
        GameState.SurfaceState.AiObjects.Add(drone);
        now = now.AddSeconds(7.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_drone_inbound");
        AssertNoTutorialOverlay();

        now = now.AddSeconds(7.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_decoy_hint");
        AssertNoTutorialOverlay();

        drone.ImpactStatus!.ObjectHealth = 0;
        now = now.AddSeconds(7.0);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_drone_destroyed");

        now = now.AddSeconds(8.5);
        control.MoveObject(marker, audio, registry);
        AssertPlayed(audio, "ship_ai_tutorial_complete");
        Assert.IsTrue(GameState.TutorialState.CompleteCueSpoken);
    }

    [TestMethod]
    public void MoveObject_TutorialOverlayAutoCloseWaitsAtLeastDoubleSpeechTime()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0);
        var voiceService = new ShipAiVoiceService(() => now, new Random(0));
        var control = new TutorialVoicePromptControls(() => now, voiceService);
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry("ship_ai_tutorial_intro");
        var marker = CreateMarker();

        Assert.IsTrue(ShipAiVoiceService.TryGetEstimatedSpeechSeconds(ShipAiVoiceCue.TutorialIntro, out double speechSeconds));

        control.MoveObject(marker, audio, registry);

        double autoCloseSeconds = (GameState.TutorialState.InstructionOverlayAutoCloseAt - now).TotalSeconds;
        Assert.IsTrue(autoCloseSeconds >= speechSeconds * 2.0);
    }

    private static void DismissTutorialOverlay()
    {
        GameState.TutorialState.ClearInstructionOverlay();
        GameState.ScreenOverlayState.HardHide();
        GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
    }

    private static void AssertNoTutorialOverlay()
    {
        Assert.IsFalse(GameState.TutorialState.InstructionOverlayPauseActive);
        Assert.IsFalse(GameState.ScreenOverlayState.ShowOverlay);
    }

    private static void AssertPlayed(CapturingAudioPlayer audio, string soundId) =>
        Assert.AreEqual(soundId, audio.PlayedSoundIds[^1]);

    private static _3dObject CreateMarker() =>
        new()
        {
            ObjectId = 1,
            ObjectName = "TutorialVoicePrompt",
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus()
        };

    private static _3dObject CreateSeeder(Vector3 worldPosition) =>
        new()
        {
            ObjectId = 2,
            ObjectName = "Seeder",
            WorldPosition = worldPosition,
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = 100 },
            IsActive = true
        };

    private static _3dObject CreateDrone(Vector3 worldPosition) =>
        new()
        {
            ObjectId = 3,
            ObjectName = "KamikazeDrone",
            WorldPosition = worldPosition,
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = 55 },
            IsActive = true
        };

    private static _3dObject CreatePowerUp(Vector3 worldPosition) =>
        new()
        {
            ObjectId = 4,
            ObjectName = "PowerUp",
            WorldPosition = worldPosition,
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = 100 },
            IsActive = true,
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "PowerUpBody",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>()
                }
            }
        };

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
                File = $"{id}.mp3",
                Settings = new SoundSettings { Volume = 1f, Is3D = false }
            };
            return true;
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public List<string> PlayedSoundIds { get; } = new();
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayedSoundIds.Add(definition.Id);
            return new CapturingAudioInstance(definition.Id, mode == AudioPlayMode.SegmentedLoop);
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null) =>
            Play(definition, AudioPlayMode.OneShot, options);

        public void Stop(IAudioInstance instance, bool playEndSegment) => instance.Stop(playEndSegment);
        public void StopAll() { }
        public void StopNonMusic() { }
        public void PlayMusic(SoundDefinition definition, float? volumeOverride = null) { }
        public void SetMusicVolume(float volume) => MusicVolume = volume;
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
