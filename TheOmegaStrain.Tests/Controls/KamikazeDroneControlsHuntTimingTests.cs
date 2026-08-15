using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Controls.KamikazeDroneControls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class KamikazeDroneControlsHuntTimingTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState
        {
            PowerUpsCollected = 1
        };
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0, y = 0, z = 0 }
        };
        GameState.ShipState = new ShipState
        {
            ShipCrashCenterWorldPosition = new Vector3 { x = 1000, y = 0, z = 1000 }
        };
    }

    [TestMethod]
    public void MoveObject_BeforeStartHuntDate_WithOnlyDrones_StartsHuntImmediately()
    {
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(5)
        };

        var drone = CreateDrone(100, 0, 0);
        var otherDrone = CreateDrone(200, 2000, 2000);

        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.SurfaceState.AiObjects.Add(otherDrone);

        control.MoveObject(drone, null, null);
        var before = (Vector3)drone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(drone, null, null);
        var after = (Vector3)drone.WorldPosition;

        Assert.IsTrue(after.x != before.x || after.y != before.y || after.z != before.z,
            "Drone should start hunting immediately when no non-drone enemies are alive.");
    }

    [TestMethod]
    public void MoveObject_BeforeStartHuntDate_WithLiveSeeder_StillWaitsForDelay()
    {
        GameState.ShipState!.ShipCrashCenterWorldPosition = new Vector3 { x = 50000, y = 0, z = 50000 };

        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(5)
        };

        var drone = CreateDrone(300, 0, 0);
        var liveSeeder = new _3dObject
        {
            ObjectId = 301,
            ObjectName = "Seeder",
            IsActive = true,
            WorldPosition = new Vector3 { x = 5000, y = 0, z = 5000 },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } }
        };

        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.SurfaceState.AiObjects.Add(liveSeeder);

        control.MoveObject(drone, null, null);
        var before = (Vector3)drone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(drone, null, null);
        var after = (Vector3)drone.WorldPosition;

        Assert.AreEqual(before.x, after.x, 0.0001, "Drone should still wait for hunt delay while live non-drone enemies remain.");
        Assert.AreEqual(before.y, after.y, 0.0001, "Drone should still wait for hunt delay while live non-drone enemies remain.");
        Assert.AreEqual(before.z, after.z, 0.0001, "Drone should still wait for hunt delay while live non-drone enemies remain.");
    }

    [TestMethod]
    public void MoveObject_BeforeStartHuntDate_WithLiveSpaceSwan_StartsHuntImmediately()
    {
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(5)
        };

        var drone = CreateDrone(400, 0, 0);
        var swan = new _3dObject
        {
            ObjectId = 401,
            ObjectName = "SpaceSwan",
            IsActive = true,
            WorldPosition = new Vector3 { x = 5000, y = 0, z = 5000 },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } }
        };

        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.SurfaceState.AiObjects.Add(swan);

        control.MoveObject(drone, null, null);
        var before = (Vector3)drone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(drone, null, null);
        var after = (Vector3)drone.WorldPosition;

        Assert.IsTrue(after.x != before.x || after.y != before.y || after.z != before.z,
            "Drone should start hunting immediately even if non-essential enemies like swans exist.");
    }

    [TestMethod]
    public void MoveObject_BeforeStartHuntDate_WithLiveZeppelinBomber_StartsHuntImmediately()
    {
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(5)
        };

        var drone = CreateDrone(500, 0, 0);
        var zeppelin = new _3dObject
        {
            ObjectId = 501,
            ObjectName = "ZeppelinBomber",
            IsActive = true,
            WorldPosition = new Vector3 { x = 5000, y = 0, z = 5000 },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } }
        };

        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.SurfaceState.AiObjects.Add(zeppelin);

        control.MoveObject(drone, null, null);
        var before = (Vector3)drone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(drone, null, null);
        var after = (Vector3)drone.WorldPosition;

        Assert.IsTrue(after.x != before.x || after.y != before.y || after.z != before.z,
            "Drone should start hunting immediately even if non-essential enemies like zeppelins exist.");
    }

    [TestMethod]
    public void MoveObject_OnScreenDrone_BoostsFlyingLoopVolume()
    {
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(-1)
        };
        var drone = CreateDrone(600, 1000, 1000);
        drone.IsOnScreen = true;
        drone.ObjectOffsets = new Vector3 { x = 0, y = 150, z = 400 };
        GameState.SurfaceState.AiObjects.Add(drone);

        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        control.MoveObject(drone, audio, registry);

        Assert.IsNotNull(audio.LastInstance);
        Assert.AreEqual(1.15f, audio.LastInstance!.LastVolume, 0.001f,
            "Drone flying loop should be a little louder than the base sound definition.");
    }

    [TestMethod]
    public void MoveObject_GameplaySurpriseDrone_WaitsAfterDecoyUnlock()
    {
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Game;
        GameState.ShipState!.ShipCrashCenterWorldPosition = new Vector3 { x = 50000, y = 0, z = 50000 };

        var drones = AddDroneGroup(700, 10);
        var surpriseDrone = drones[^1];
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(-1)
        };

        control.MoveObject(surpriseDrone, null, null);
        var before = (Vector3)surpriseDrone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(surpriseDrone, null, null);
        var after = (Vector3)surpriseDrone.WorldPosition;

        Assert.AreEqual(before.x, after.x, 0.0001, "Surprise drone should wait a little longer before hunting.");
        Assert.AreEqual(before.y, after.y, 0.0001, "Surprise drone should wait a little longer before hunting.");
        Assert.AreEqual(before.z, after.z, 0.0001, "Surprise drone should wait a little longer before hunting.");
    }

    [TestMethod]
    public void MoveObject_GameplayRegularDrone_DoesNotWaitForSurpriseDelay()
    {
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Game;

        var drones = AddDroneGroup(800, 10);
        var regularDrone = drones[0];
        var control = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddMinutes(-1)
        };

        control.MoveObject(regularDrone, null, null);
        var before = (Vector3)regularDrone.WorldPosition;

        Thread.Sleep(20);

        control.MoveObject(regularDrone, null, null);
        var after = (Vector3)regularDrone.WorldPosition;

        Assert.IsTrue(after.x != before.x || after.y != before.y || after.z != before.z,
            "Regular drones should keep hunting normally while only the selected surprise group waits.");
    }

    private static _3dObject CreateDrone(int id, float x, float z)
    {
        return new _3dObject
        {
            ObjectId = id,
            ObjectName = "KamikazeDrone",
            IsActive = true,
            WorldPosition = new Vector3 { x = x, y = 0, z = z },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } }
        };
    }

    private static List<_3dObject> AddDroneGroup(int firstId, int count)
    {
        var drones = new List<_3dObject>(count);
        for (int i = 0; i < count; i++)
        {
            var drone = CreateDrone(firstId + i, i * 1000f, i * 1000f);
            drones.Add(drone);
            GameState.SurfaceState.AiObjects.Add(drone);
        }

        return drones;
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        private readonly Dictionary<string, SoundDefinition> _sounds = new()
        {
            ["explosion_main"] = new SoundDefinition
            {
                Id = "explosion_main",
                Usage = "Explosion",
                File = "OmegaStrain_Explosion_Main.wav",
                Settings = new SoundSettings { Volume = 1f, Is3D = true }
            },
            ["drone_flying"] = new SoundDefinition
            {
                Id = "drone_flying",
                Usage = "DroneComing",
                File = "OmegaStrain_Drone_Coming.wav",
                Settings = new SoundSettings { Volume = 1f, Is3D = true }
            }
        };

        public SoundDefinition Get(string id) => _sounds[id];

        public bool TryGet(string id, out SoundDefinition definition) => _sounds.TryGetValue(id, out definition!);
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public CapturingAudioInstance? LastInstance { get; private set; }
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            LastInstance = new CapturingAudioInstance(definition.Id, mode == AudioPlayMode.SegmentedLoop);
            return LastInstance;
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null)
        {
            LastInstance = new CapturingAudioInstance(definition.Id, isLooping: false);
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
        public float LastVolume { get; private set; }
        public System.Numerics.Vector3 LastWorldPosition { get; private set; }

        public void SetVolume(float volume) => LastVolume = volume;
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) => LastWorldPosition = position;
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
