using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class LightningControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            SurfaceViewportObject = new _3dObject
            {
                ObjectId = -1,
                ObjectOffsets = new Vector3 { x = 0f, y = 500f, z = 400f }
            }
        };
        GameState.WeatherVisualState = new WeatherVisualState();
        GameState.ObjectIdCounter = 0;
        GameState.DeltaTime = 0f;
    }

    [TestMethod]
    public void CreateLightningEmitter_IsNonCollidingSceneOwnedEmitter()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);

        Assert.AreEqual("LightningEmitter", emitter.ObjectName);
        Assert.IsNull(emitter.Particles);
        Assert.IsFalse(emitter.HasShadow);
        Assert.AreEqual(0, emitter.CrashBoxes.Count);
        Assert.IsInstanceOfType(emitter.Movement, typeof(LightningControls));
    }

    [TestMethod]
    public void MoveObject_DrawsCameraFacingWorldDepthLightningAndRaisesFlash()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var lightningPart = emitter.ObjectParts.Single(p => p.PartName == "LightningBolts");
        var visibleTriangles = lightningPart.Triangles
            .Where(t => t.vert1.z != 0f && t.Color != "000000")
            .ToList();

        Assert.AreEqual(LightningControls.TargetTriangleCount, lightningPart.Triangles.Count);
        Assert.IsTrue(visibleTriangles.Count > 0, "Lightning should draw at least one visible bolt segment on strike.");
        Assert.IsTrue(visibleTriangles.All(t => t.noHidden == true));
        Assert.IsTrue(visibleTriangles.All(t => SameDepth(t)), "Lightning quads should be flat and camera-facing.");
        Assert.IsTrue(visibleTriangles.All(t => t.vert1.z >= LightningControls.MinDepthZ && t.vert1.z <= LightningControls.MaxDepthZ));
        Assert.IsTrue(visibleTriangles.Any(t => t.Color != "ffffff" && t.Color != "000000"));
        Assert.IsTrue(GameState.WeatherVisualState.LightningFlashIntensity > 0f, "Lightning should raise a background flash.");
    }

    [TestMethod]
    public void MoveObject_PlaysAlternatingThunderWhenLightningStrikes()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);
        var controls = (LightningControls)emitter.Movement!;
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        controls.MoveObject(emitter, audio, registry);
        ForceNextLightningStrike(controls);
        controls.MoveObject(emitter, audio, registry);

        CollectionAssert.AreEqual(
            new[] { "rainforest_thunder_1", "rainforest_thunder_2" },
            audio.PlayedSoundIds.Take(2).ToArray());
        Assert.IsTrue(audio.PlayModes.Take(2).All(mode => mode == AudioPlayMode.OneShot));
        Assert.IsTrue(audio.VolumeOverrides.Take(2).All(volume => Math.Abs((volume ?? 0f) - 1.3f) < 0.001f));
        Assert.AreEqual(2, audio.PlayOneShotCount);
    }

    [TestMethod]
    public void MoveObject_KeepsBoltsInWorldSpaceAsShipMoves()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        var lightningPart = emitter.ObjectParts.Single(p => p.PartName == "LightningBolts");
        float initialDepth = lightningPart.Triangles.First(t => t.vert1.z != 0f).vert1.z;

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 50f
        };

        emitter.Movement.MoveObject(emitter, null, null);

        float movedDepth = lightningPart.Triangles.First(t => t.vert1.z != 0f).vert1.z;
        float delta = movedDepth - initialDepth;

        Assert.IsTrue(delta < -45f && delta > -55f, $"Lightning depth should shift against ship/world movement. Delta={delta:0.###}");
    }

    [TestMethod]
    public void MoveObject_NeverKeepsMoreThanThreeBoltsOnScreen()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);
        var controls = (LightningControls)emitter.Movement!;
        int maxActiveBolts = 0;

        for (int i = 0; i < 900; i++)
        {
            controls.MoveObject(emitter, null, null);
            maxActiveBolts = Math.Max(maxActiveBolts, controls.ActiveBoltCount);
            Assert.IsTrue(controls.ActiveBoltCount <= LightningControls.MaxActiveBolts);
        }

        Assert.IsTrue(maxActiveBolts <= LightningControls.MaxActiveBolts);
    }

    [TestMethod]
    public void MoveObject_SyncsEmitterVerticallyWithGroundAltitude()
    {
        var emitter = LightningEmitter.CreateLightningEmitter(null);

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

    private static bool SameDepth(ITriangleMeshWithColor triangle)
    {
        return Math.Abs(triangle.vert1.z - triangle.vert2.z) < 0.001f
            && Math.Abs(triangle.vert1.z - triangle.vert3.z) < 0.001f;
    }

    private static void ForceNextLightningStrike(LightningControls controls)
    {
        var secondsField = typeof(LightningControls).GetField("_secondsUntilNextStrike", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(secondsField);
        secondsField!.SetValue(controls, 0f);

        var boltsField = typeof(LightningControls).GetField("_bolts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(boltsField);
        ((IList)boltsField!.GetValue(controls)!).Clear();
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        private readonly Dictionary<string, SoundDefinition> _sounds = new()
        {
            ["rainforest_thunder_1"] = new SoundDefinition
            {
                Id = "rainforest_thunder_1",
                Usage = "RainforestThunder",
                File = "OmegaStrain_Thunder_Sharp_Direct_1.wav",
                Settings = new SoundSettings { Volume = 1.3f }
            },
            ["rainforest_thunder_2"] = new SoundDefinition
            {
                Id = "rainforest_thunder_2",
                Usage = "RainforestThunder",
                File = "OmegaStrain_Thunder_Sharp_Direct_2.wav",
                Settings = new SoundSettings { Volume = 1.3f }
            }
        };

        public SoundDefinition Get(string id)
        {
            if (TryGet(id, out var definition))
                return definition;

            throw new KeyNotFoundException(id);
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            return _sounds.TryGetValue(id, out definition!);
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public List<string> PlayedSoundIds { get; } = new();
        public List<AudioPlayMode> PlayModes { get; } = new();
        public List<float?> VolumeOverrides { get; } = new();
        public int PlayOneShotCount { get; private set; }
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayedSoundIds.Add(definition.Id);
            PlayModes.Add(mode);
            VolumeOverrides.Add(options?.VolumeOverride);
            return new CapturingAudioInstance(definition.Id, mode == AudioPlayMode.SegmentedLoop);
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null)
        {
            PlayOneShotCount++;
            PlayedSoundIds.Add(definition.Id);
            PlayModes.Add(AudioPlayMode.OneShot);
            VolumeOverrides.Add(options?.VolumeOverride);
        }
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
