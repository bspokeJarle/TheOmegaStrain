using _3dRotations.World.Objects;
using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class RainfallControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.SettingsState = new GameSettingsState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
        GameState.ObjectIdCounter = 0;
        RainfallControls.GlobalRainOpacity = 1f;
    }

    [TestMethod]
    public void MoveObject_HighParticleDensityCreatesMoreRainDrops()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 130,
            EnhancedWeatherEnabled = true
        };

        var emitter = RainEmitter.CreateRainEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var rainPart = emitter.ObjectParts.Single(p => p.PartName == "Raindrops");
        Assert.AreEqual(RainfallControls.CurrentTargetDropCount, rainPart.Triangles.Count);
        Assert.IsTrue(rainPart.Triangles.Count > RainfallControls.TargetDropCount);
    }

    [TestMethod]
    public void CreateRainEmitter_IsNonCollidingSceneOwnedEmitter()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);

        Assert.AreEqual("RainEmitter", emitter.ObjectName);
        Assert.IsNull(emitter.Particles);
        Assert.IsFalse(emitter.HasShadow);
        Assert.AreEqual(0, emitter.CrashBoxes.Count);
        Assert.IsInstanceOfType(emitter.Movement, typeof(RainfallControls));
    }

    [TestMethod]
    public void MoveObject_DrawsFastThinBlueRainStreaks()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var rainPart = emitter.ObjectParts.Single(p => p.PartName == "Raindrops");
        var visibleDrops = rainPart.Triangles.Where(t => t.vert1.z != 0f).ToList();

        Assert.AreEqual(RainfallControls.TargetDropCount, rainPart.Triangles.Count);
        Assert.IsTrue(rainPart.Triangles.All(t => t.noHidden == true));
        Assert.IsTrue(visibleDrops.Count > RainfallControls.VisibleDropTarget / 2, "Rain should keep a dense visible sheet of streaks.");
        Assert.IsTrue(visibleDrops.Any(t => Math.Abs(t.vert2.y - t.vert1.y) > 12f), "Rain should render as elongated streaks, not snow-like specks.");
        Assert.IsTrue(visibleDrops.Any(t => t.Color != "ffffff" && t.Color != "000000"), "Rain should use a blue-gray rain color.");
    }

    [TestMethod]
    public void MoveObject_HonorsGlobalRainOpacityForStarFade()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);

        RainfallControls.GlobalRainOpacity = 0f;
        emitter.Movement!.MoveObject(emitter, null, null);

        var rainPart = emitter.ObjectParts.Single(p => p.PartName == "Raindrops");

        Assert.IsTrue(
            rainPart.Triangles.All(t => t.Color == "000000" && t.vert1.z == 0f),
            "Rain should fade out completely when the starfield opacity is fully faded in.");
    }

    [TestMethod]
    public void MoveObject_StartsContinuousRainLoopAndTracksRainOpacity()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        emitter.Movement!.MoveObject(emitter, audio, registry);

        Assert.AreEqual(1, audio.PlayCount);
        Assert.AreEqual("rain_loop", audio.LastDefinitionId);
        Assert.AreEqual(AudioPlayMode.SegmentedLoop, audio.LastMode);
        Assert.AreEqual(0.55f, audio.LastInstance.Volume, 0.001f);

        RainfallControls.GlobalRainOpacity = 0.25f;
        emitter.Movement.MoveObject(emitter, audio, registry);

        Assert.AreEqual(1, audio.PlayCount, "The rain bed should keep looping instead of restarting every frame.");
        Assert.AreEqual(0.55f * 0.25f, audio.LastInstance.Volume, 0.001f);
    }

    [TestMethod]
    public void MoveObject_KeepsDropsInWorldSpaceAsShipMoves()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        var rainPart = emitter.ObjectParts.Single(p => p.PartName == "Raindrops");
        var initialDepths = rainPart.Triangles
            .Take(RainfallControls.VisibleDropTarget)
            .Select(t => t.vert1.z)
            .ToList();

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 50f
        };

        emitter.Movement.MoveObject(emitter, null, null);

        var movedDepths = rainPart.Triangles
            .Take(RainfallControls.VisibleDropTarget)
            .Select(t => t.vert1.z)
            .ToList();

        int comparableCount = 0;
        int worldLockedDrops = 0;
        int sampleCount = Math.Min(initialDepths.Count, movedDepths.Count);
        for (int i = 0; i < sampleCount; i++)
        {
            if (initialDepths[i] == 0f || movedDepths[i] == 0f)
                continue;

            comparableCount++;
            float delta = movedDepths[i] - initialDepths[i];
            if (delta < -35f && delta > -65f)
                worldLockedDrops++;
        }

        Assert.IsTrue(
            comparableCount > 0 && worldLockedDrops >= comparableCount * 3 / 4,
            $"Most rain drops should shift against ship/world movement instead of staying screen-locked. WorldLocked={worldLockedDrops}/{comparableCount}");
    }

    [TestMethod]
    public void MoveObject_RecyclesMostRainAheadOfTravelDirection()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 20f
        };
        emitter.Movement.MoveObject(emitter, null, null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 6200f
        };
        emitter.Movement.MoveObject(emitter, null, null);
        emitter.Movement.MoveObject(emitter, null, null);

        var rainPart = emitter.ObjectParts.Single(p => p.PartName == "Raindrops");
        int dropsAhead = rainPart.Triangles.Count(t => t.vert1.z > 500f);

        Assert.IsTrue(dropsAhead >= RainfallControls.TargetDropCount * 3 / 5, "Recycled rain should be biased toward the current travel direction.");
    }

    [TestMethod]
    public void MoveObject_SyncsEmitterVerticallyWithGroundAltitude()
    {
        var emitter = RainEmitter.CreateRainEmitter(null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = 40f,
            z = GameState.SurfaceState.GlobalMapPosition.z
        };

        emitter.Movement!.MoveObject(emitter, null, null);

        Assert.AreEqual(
            40f * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY,
            emitter.ObjectOffsets.y,
            0.001f);
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        private readonly SoundDefinition _rainLoop = new()
        {
            Id = "rain_loop",
            Usage = "RainLoop",
            File = "OmegaStrain_Rain_Loop.wav",
            Settings = new SoundSettings { Volume = 0.55f }
        };

        public SoundDefinition Get(string id)
        {
            if (TryGet(id, out var definition))
                return definition;

            throw new KeyNotFoundException(id);
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            if (id == _rainLoop.Id)
            {
                definition = _rainLoop;
                return true;
            }

            definition = null!;
            return false;
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public int PlayCount { get; private set; }
        public string? LastDefinitionId { get; private set; }
        public AudioPlayMode? LastMode { get; private set; }
        public CapturingAudioInstance LastInstance { get; } = new();
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayCount++;
            LastDefinitionId = definition.Id;
            LastMode = mode;
            LastInstance.SoundId = definition.Id;
            LastInstance.Volume = options?.VolumeOverride ?? definition.Settings.Volume;
            LastInstance.IsPlaying = true;
            LastInstance.IsLooping = mode == AudioPlayMode.SegmentedLoop;
            return LastInstance;
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null) => Play(definition, AudioPlayMode.OneShot, options);
        public void Stop(IAudioInstance instance, bool playEndSegment) => instance.Stop(playEndSegment);
        public void StopAll() => LastInstance.Stop(false);
        public void StopNonMusic() => LastInstance.Stop(false);
        public void PlayMusic(SoundDefinition definition, float? volumeOverride = null) { }
        public void SetMusicVolume(float volume) => MusicVolume = volume;
        public void StopMusic() { }
        public void Update(double deltaTimeSeconds) { }
    }

    private sealed class CapturingAudioInstance : IAudioInstance
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string SoundId { get; set; } = string.Empty;
        public bool IsPlaying { get; set; }
        public bool IsLooping { get; set; }
        public float Volume { get; set; }

        public void SetVolume(float volume) => Volume = volume;
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) { }
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
