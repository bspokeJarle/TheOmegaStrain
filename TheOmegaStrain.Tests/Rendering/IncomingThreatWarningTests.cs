using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Audio.Services;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Rendering;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
[DoNotParallelize]
public class IncomingThreatWarningTests
{
    private SurfaceState _previousSurface = null!;
    private int _width, _height;
    private DateTime _now;
    private GamePlayState _gameplay = null!;
    private ShipAiVoiceService _voice = null!;
    private IncomingThreatWarningManager _manager = null!;
    private CapturingAudio _audio = null!;
    private readonly WarningSounds _sounds = new();
    private OmegaObject3D _ship = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousSurface = GameState.SurfaceState;
        _width = ScreenSetup.screenSizeX;
        _height = ScreenSetup.screenSizeY;
        ScreenSetup.Initialize(1500, 1024);
        GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3() };
        _gameplay = new GamePlayState { Phase = GamePhase.Playing, SceneIndex = 1 };
        _now = new DateTime(2026, 9, 15, 12, 0, 0);
        _voice = new ShipAiVoiceService(() => _now, new Random(1));
        _manager = new IncomingThreatWarningManager(() => _now);
        _audio = new CapturingAudio();
        _ship = CreateObject("Ship", 1, 0f, 0f);
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = _previousSurface;
        ScreenSetup.Initialize(_width, _height);
    }

    [DataTestMethod]
    [DataRow("AttackShip", true)]
    [DataRow("KamikazeDrone", true)]
    [DataRow("EnemyRocket", true)]
    [DataRow("Rocket", false)]
    [DataRow("DroneDecoy", false)]
    [DataRow("Seeder", false)]
    [DataRow("MotherShipSmall", false)]
    [DataRow("EnemyLazer", false)]
    [DataRow("Particle", false)]
    public void Detection_OnlyIncludesRequestedHostileTypes(string name, bool expected)
    {
        Update(CreateObject(name, 2, 300f, 0f));
        Assert.AreEqual(expected, _gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(expected ? 1 : 0, _audio.SoundIds.Count);
        if (expected) Assert.AreEqual(name == "KamikazeDrone" ? "ship_drone_incoming_warning" : "ship_enemy_incoming_warning", _audio.SoundIds.Single());
    }

    [TestMethod]
    public void Diagnostics_ReportsGameplayGateAndThrottlesFrameLogging()
    {
        var messages = new List<string>();
        var manager = new IncomingThreatWarningManager(() => _now, messages.Add);
        manager.UpdateFromWorld(_ship, Array.Empty<OmegaObject3D>(), _gameplay, false, _audio, _sounds);
        Assert.IsTrue(messages.Any(m => m.StartsWith("SOURCES ai=0")));
        Assert.IsTrue(messages.Any(m => m.StartsWith("BLOCK gameplay gate")));
        int count = messages.Count;
        for (int i = 0; i < 90; i++)
            manager.UpdateFromWorld(_ship, Array.Empty<OmegaObject3D>(), _gameplay, false, _audio, _sounds);
        Assert.AreEqual(count, messages.Count, "Do not write diagnostics every frame.");
        _now = _now.AddSeconds(1);
        manager.UpdateFromWorld(_ship, Array.Empty<OmegaObject3D>(), _gameplay, false, _audio, _sounds);
        Assert.IsTrue(messages.Count > count);
    }

    [TestMethod]
    public void Detection_KeepsPublishedMarkerActiveWhileScanningNextFrame()
    {
        bool observeScan = false;
        bool sawShip = false;
        var manager = new IncomingThreatWarningManager(() => _now, message =>
        {
            if (observeScan && message.StartsWith("SHIP "))
            {
                sawShip = true;
                Assert.IsTrue(_gameplay.IncomingThreatWarningActive,
                    "The UI must keep the last completed warning while detection is still running.");
            }
        });
        var drone = CreateObject("KamikazeDrone", 2, 300f, 0f);
        manager.UpdateFromWorld(_ship, new[] { drone }, _gameplay, true, _audio, _sounds);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        _now = _now.AddSeconds(1);
        observeScan = true;
        manager.UpdateFromWorld(_ship, new[] { drone }, _gameplay, true, _audio, _sounds);
        Assert.IsTrue(sawShip);
        manager.UpdateFromWorld(_ship, Array.Empty<OmegaObject3D>(), _gameplay, true, _audio, _sounds);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive, "A completed scan without threats must clear the marker.");
    }

    [TestMethod]
    public void Diagnostics_ReportsMissingShipAndRejectedDistance()
    {
        var messages = new List<string>();
        var manager = new IncomingThreatWarningManager(() => _now, messages.Add);
        manager.UpdateFromWorld(null, Array.Empty<OmegaObject3D>(), _gameplay, true, _audio, _sounds);
        Assert.IsTrue(messages.Any(m => m == "BLOCK ship-not-live null"));
        _now = _now.AddSeconds(1);
        var drone = CreateObject("KamikazeDrone", 2, 2500f, 0f);
        manager.UpdateFromWorld(_ship, new[] { drone }, _gameplay, true, _audio, _sounds);
        Assert.IsTrue(messages.Any(m => m.Contains("distance=2500") && m.Contains("accepted=False")));
        Assert.IsTrue(messages.Any(m => m.Contains("outsideRange=1")));
    }

    [TestMethod]
    public void Diagnostics_ReportsSoundRegistrationAttemptAndPlaybackReturn()
    {
        var messages = new List<string>();
        var manager = new IncomingThreatWarningManager(() => _now, messages.Add);
        manager.UpdateFromWorld(_ship, new[] { CreateObject("KamikazeDrone", 2, 300f, 0f) },
            _gameplay, true, _audio, _sounds);
        Assert.IsTrue(messages.Any(m => m.StartsWith("AUDIO ") && m.Contains("registered=True")));
        Assert.IsTrue(messages.Any(m => m.StartsWith("PLAY id=ship_drone_incoming_warning")));
        Assert.IsTrue(messages.Any(m => m.StartsWith("PLAY-RETURN ") && m.Contains("playing=True")));
    }

    [DataTestMethod]
    [DataRow("AttackShip", "ship_enemy_incoming_warning")]
    [DataRow("KamikazeDrone", "ship_drone_incoming_warning")]
    public void WorldDetection_FindsOffscreenAiWithoutAnyEnemyRenderObjects(string name, string soundId)
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3(95000f, 0f, 96000f);
        var enemy = CreateObject(name, 2, 0f, 0f);
        enemy.WorldPosition = new Vector3(93700f, 0f, 96000f);
        enemy.IsOnScreen = false;
        var before = enemy.WorldPosition.x;
        _manager.UpdateFromWorld(_ship, new[] { enemy }, _gameplay, true, _audio, _sounds);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(180f, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Assert.AreEqual(soundId, _audio.SoundIds.Single());
        Assert.IsFalse(enemy.IsOnScreen);
        Assert.AreEqual(before, enemy.WorldPosition.x);
    }

    [TestMethod]
    public void WorldDetection_UsesActiveRocketOffsetsEvenWhenOwnerIsFarAwayAndStopsAfterCleanup()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3(95000f, 0f, 96000f);
        var owner = CreateObject("AttackShip", 2, 0f, 0f);
        owner.WorldPosition = new Vector3(45000f, 0f, 46000f);
        owner.IsOnScreen = false;
        var rocket = CreateObject("EnemyRocket", 3, 50300f, 0f, -50000f);
        rocket.WorldPosition = new Vector3(45000f, 0f, 46000f);
        owner.WeaponSystems = new Weapons(new List<I3dObject>(), new PowerUpControls(), owner);
        owner.WeaponSystems.ActiveWeapons.Add(new ActiveWeapon { WeaponObject = rocket });
        _manager.UpdateFromWorld(_ship, new[] { owner }, _gameplay, true, _audio, _sounds);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0f, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Assert.AreEqual("ship_enemy_incoming_warning", _audio.SoundIds.Single());
        owner.WeaponSystems.ActiveWeapons.Clear();
        _now = _now.AddSeconds(4);
        _manager.UpdateFromWorld(_ship, new[] { owner }, _gameplay, true, _audio, _sounds);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(1, _audio.SoundIds.Count);
    }

    [DataTestMethod]
    [DataRow(0f, 90f)]
    [DataRow(63f, 45f)]
    [DataRow(70f, -45f)]
    public void WorldDetection_ActiveRocketUsesAlreadyRotatedGeometry(float pitch, float heading)
    {
        var owner = CreateObject("AttackShip", 2, 3000f, 0f);
        var weapons = new Weapons(new List<I3dObject>(), new PowerUpControls(), owner);
        owner.WeaponSystems = weapons;
        var rocket = Rocket.CreateRocket(null!);
        rocket.ObjectName = "EnemyRocket";
        rocket.ObjectId = 3;
        rocket.IsActive = true;
        rocket.WorldPosition = new Vector3();
        rocket.ObjectOffsets = new Vector3(40f, 0f, 0f);
        rocket.Rotation = new Vector3(pitch, 0f, heading);
        rocket.ImpactStatus = new ImpactStatus { ObjectHealth = 1 };
        // Use the launch path's transform, not a synthetic centred crash box.
        var initialize = typeof(Weapons).GetMethod("InitializeWeaponGeometry",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        initialize.Invoke(weapons, new object[] { rocket, rocket.Rotation, 0 });
        weapons.ActiveWeapons.Add(new ActiveWeapon { WeaponObject = rocket });
        var originalCorners = rocket.CrashBoxes.SelectMany(box => box).Select(p => (p.x, p.y, p.z)).ToArray();

        Update(rocket); // Render-list objects have already rotated geometry.
        float expectedAngle = _gameplay.IncomingThreatWarningAngle;
        float expectedX = _gameplay.IncomingThreatWarningScreenX;
        float expectedY = _gameplay.IncomingThreatWarningScreenY;
        _manager.Reset(_gameplay);
        _manager.UpdateFromWorld(_ship, new[] { owner }, _gameplay, true, _audio, _sounds);

        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(expectedAngle, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Assert.AreEqual(expectedX, _gameplay.IncomingThreatWarningScreenX, 0.001f);
        Assert.AreEqual(expectedY, _gameplay.IncomingThreatWarningScreenY, 0.001f);
        CollectionAssert.AreEqual(originalCorners,
            rocket.CrashBoxes.SelectMany(box => box).Select(p => (p.x, p.y, p.z)).ToArray(),
            "Warning detection must never mutate the live weapon geometry.");
    }

    [TestMethod]
    public void WorldDetection_RotatesAiCrashCentreOnlyForReading()
    {
        var enemy = CreateObject("KamikazeDrone", 2, 0f, 0f, centerZ: 100f);
        enemy.Rotation.x = 63f;
        float originalY = enemy.CrashBoxes[0][0].y;
        _manager.UpdateFromWorld(_ship, new[] { enemy }, _gameplay, true, _audio, _sounds);
        float angle = _gameplay.IncomingThreatWarningAngle;
        Assert.AreEqual(originalY, enemy.CrashBoxes[0][0].y, "Detection must not rotate shared geometry.");
        new ObjectFrameTransformer().RotateObjectGeometry(enemy);
        Update(enemy);
        Assert.AreEqual(angle, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Assert.AreNotEqual(originalY, enemy.CrashBoxes[0][0].y);
    }

    [DataTestMethod]
    [DataRow(300f, 0f, 0f)]
    [DataRow(-300f, 0f, 180f)]
    [DataRow(0f, -300f, -90f)]
    [DataRow(0f, 300f, 90f)]
    public void Arrow_PointsFromShipTowardThreat(float x, float y, float angle)
    {
        Update(CreateObject("KamikazeDrone", 2, x, y));
        Assert.AreEqual(angle, _gameplay.IncomingThreatWarningAngle, 0.001f);
        float dx = _gameplay.IncomingThreatWarningScreenX - 750f;
        float dy = _gameplay.IncomingThreatWarningScreenY - 512f;
        Assert.AreEqual(120f, VectorMath.Length(new Vector3(dx, dy, 0f)), 0.001f);
        Assert.IsTrue(dx * x + dy * y > 0f);
    }

    [DataTestMethod]
    [DataRow(1501f, true)]
    [DataRow(2000f, true)]
    [DataRow(2001f, false)]
    public void Detection_UsesConfiguredEntryRange(float distance, bool expected)
    {
        Update(CreateObject("AttackShip", 2, distance, 0f));
        Assert.AreEqual(expected, _gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(expected ? 1 : 0, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void Detection_IncludesDepthInsteadOfOnlyScreenOverlap()
    {
        Update(CreateObject("EnemyRocket", 2, 0f, 0f, 2100f));
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void Rocket_UsesCurrentOffsetsNotItsDistantLaunchAnchor()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3(50000f, 0f, 50000f);
        var rocket = CreateObject("EnemyRocket", 2, 9700f, 0f);
        rocket.WorldPosition = new Vector3(40000f, 0f, 50000f);
        Assert.IsFalse(rocket.CheckInhabitantVisibility());
        Update(rocket);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(180f, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Assert.AreEqual(1, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void Marker_StaysOnScreenForAnOffscreenThreat()
    {
        Update(CreateObject("KamikazeDrone", 2, -1400f, 0f));
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.IsTrue(_gameplay.IncomingThreatWarningScreenX > 0f);
        Assert.IsTrue(_gameplay.IncomingThreatWarningScreenX < ScreenSetup.screenSizeX);
        Assert.AreEqual(180f, _gameplay.IncomingThreatWarningAngle, 0.001f);
    }

    [TestMethod]
    public void Selection_PrioritizesRocketThenNearestHostile()
    {
        var attackShip = CreateObject("AttackShip", 2, 200f, 0f);
        var drone = CreateObject("KamikazeDrone", 3, 0f, -100f);
        var rocket = CreateObject("EnemyRocket", 4, -600f, 0f);
        Update(attackShip, drone);
        Assert.AreEqual(-90f, _gameplay.IncomingThreatWarningAngle, 0.001f);
        Update(attackShip, drone, rocket);
        Assert.AreEqual(180f, _gameplay.IncomingThreatWarningAngle, 0.001f);
        rocket.ImpactStatus!.HasCrashed = true;
        Update(attackShip, drone, rocket);
        Assert.AreEqual(-90f, _gameplay.IncomingThreatWarningAngle, 0.001f);
    }

    [TestMethod]
    public void Audio_AnnouncesOnceAcrossFramesAndObjectCopies()
    {
        for (int i = 0; i < 10; i++)
        {
            _now = _now.AddSeconds(4);
            Update(CreateObject("AttackShip", 2, 300f, 0f));
        }
        Assert.AreEqual(1, _audio.SoundIds.Count);
        _now = _now.AddSeconds(4);
        Update(CreateObject("AttackShip", 2, 300f, 0f), CreateObject("EnemyRocket", 3, 400f, 0f));
        Assert.AreEqual(2, _audio.SoundIds.Count, "A newly launched rocket is a new threat.");
    }

    [TestMethod]
    public void Audio_AnnouncesBothCategoriesWithoutRepeatingThem()
    {
        var threats = new[] { CreateObject("AttackShip", 2, 300f, 0f), CreateObject("KamikazeDrone", 3, -300f, 0f) };
        Update(threats);
        _now = _now.AddSeconds(4);
        Update(threats);
        CollectionAssert.AreEqual(new[] { "ship_enemy_incoming_warning", "ship_drone_incoming_warning" }, _audio.SoundIds);
        _now = _now.AddSeconds(4);
        Update(threats);
        Assert.AreEqual(2, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void Audio_IsIndependentOfHalEAndDropsVanishedPendingThreats()
    {
        Assert.IsTrue(_voice.TrySpeak(ShipAiVoiceCue.CollisionWarning, _audio, _sounds));
        var rocket = CreateObject("EnemyRocket", 2, 300f, 0f);
        Update(rocket);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive, "Visual warning must not wait for audio.");
        Assert.AreEqual(2, _audio.SoundIds.Count);
        _now = _now.AddSeconds(4);
        Update(rocket);
        Assert.AreEqual(2, _audio.SoundIds.Count);
        Update(rocket, CreateObject("KamikazeDrone", 4, 0f, 300f));
        Update(CreateObject("EnemyRocket", 3, -300f, 0f));
        _now = _now.AddSeconds(4);
        Update();
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(3, _audio.SoundIds.Count, "Removed threats must not leave queued speech.");
    }

    [TestMethod]
    public void MissingAudio_DoesNotHideOrConsumeWarning()
    {
        var rocket = CreateObject("EnemyRocket", 2, 300f, 0f);
        _manager.Update(new[] { _ship, rocket }, _gameplay, true, null, null);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Update(rocket);
        Assert.AreEqual(1, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void ReleaseMargin_AvoidsChatterAndAllowsARealReentry()
    {
        var drone = CreateObject("KamikazeDrone", 2, 1900f, 0f);
        Update(drone);
        drone.ObjectOffsets!.x = 2200f;
        _now = _now.AddSeconds(4);
        Update(drone);
        Assert.IsTrue(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(1, _audio.SoundIds.Count);
        drone.ObjectOffsets.x = 2201f;
        Update(drone);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        drone.ObjectOffsets.x = 1900f;
        Update(drone);
        Assert.AreEqual(2, _audio.SoundIds.Count);
    }

    [DataTestMethod]
    [DataRow("inactive")]
    [DataRow("crashed")]
    [DataRow("exploded")]
    [DataRow("dead")]
    [DataRow("no-boxes")]
    public void Detection_IgnoresNonThreateningObjectStates(string state)
    {
        var enemy = CreateObject("KamikazeDrone", 2, 200f, 0f);
        switch (state)
        {
            case "inactive": enemy.IsActive = false; break;
            case "crashed": enemy.ImpactStatus!.HasCrashed = true; break;
            case "exploded": enemy.ImpactStatus!.HasExploded = true; break;
            case "dead": enemy.ImpactStatus!.ObjectHealth = 0; break;
            case "no-boxes": enemy.CrashBoxes.Clear(); break;
        }
        Update(enemy);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0, _audio.SoundIds.Count);
    }

    [DataTestMethod]
    [DataRow(GamePhase.Intro)]
    [DataRow(GamePhase.Paused)]
    [DataRow(GamePhase.GameOver)]
    [DataRow(GamePhase.Outro)]
    public void NonGameplayPhases_HaveNoWarning(GamePhase phase)
    {
        _gameplay.Phase = phase;
        Update(CreateObject("AttackShip", 2, 300f, 0f));
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void PausesOverlaysAndFades_CanSuppressAndClearTheWarning()
    {
        var enemy = CreateObject("AttackShip", 2, 300f, 0f);
        Update(enemy);
        _manager.Update(new[] { _ship, enemy }, _gameplay, false, _audio, _sounds);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0f, _gameplay.IncomingThreatWarningScreenX);
        _gameplay.IsVictoryRewardPauseActive = true;
        _now = _now.AddSeconds(4);
        Update(enemy);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(1, _audio.SoundIds.Count);
    }

    [TestMethod]
    public void SceneReset_DropsOldThreatIdentityAndHudState()
    {
        var enemy = CreateObject("AttackShip", 2, 300f, 0f);
        Update(enemy);
        _manager.Reset(_gameplay);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        _now = _now.AddSeconds(4);
        Update(enemy);
        Assert.AreEqual(2, _audio.SoundIds.Count);
        _gameplay.ResetForNewGame();
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0f, _gameplay.IncomingThreatWarningAngle);
    }

    [TestMethod]
    public void DeadOrMissingShip_HidesWarning()
    {
        var enemy = CreateObject("EnemyRocket", 2, 200f, 0f);
        _ship.ImpactStatus!.ObjectHealth = 0;
        Update(enemy);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        _manager.Update(new[] { enemy }, _gameplay, true, _audio, _sounds);
        Assert.IsFalse(_gameplay.IncomingThreatWarningActive);
        Assert.AreEqual(0, _audio.SoundIds.Count);
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void Projection_UsesFrameRotatedCentreExactlyOnce(float pitch)
    {
        var enemy = CreateObject("KamikazeDrone", 2, 300f, 100f, 200f, centerZ: 80f);
        enemy.Rotation = new Vector3(pitch, 0f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(enemy);
        Assert.IsTrue(OmegaPerspectiveProjectorFactory.TryProjectCrashCenter(enemy, out var actual, out var screen));
        float radians = pitch * MathF.PI / 180f;
        var expectedLocal = new Vector3(0f, -80f * MathF.Sin(radians), 80f * MathF.Cos(radians));
        Assert.AreEqual(300f, actual.x, 0.001f);
        Assert.AreEqual(100f + expectedLocal.y, actual.y, 0.001f);
        Assert.AreEqual(200f + expectedLocal.z, actual.z, 0.001f);
        Assert.IsTrue(ProjectionMath.TryProjectVertex(expectedLocal, 1050f, 612f, 200f,
            ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom, out var expectedScreen));
        Assert.AreEqual((float)expectedScreen.x, screen.x, 0.001f);
        Assert.AreEqual((float)expectedScreen.y, screen.y, 0.001f);
    }

    [TestMethod]
    public void Projection_FollowsTheRendererNearDepthCapWithoutChangingTheObject()
    {
        var enemy = CreateObject("EnemyRocket", 2, 100f, 100f, -1400f, centerZ: 10f);
        Assert.IsTrue(OmegaPerspectiveProjectorFactory.TryProjectCrashCenter(enemy, out var actual, out var screen));
        var rendered = OmegaPerspectiveProjectorFactory.Create().ProjectToTriangles(new() { enemy }, 0);
        Assert.AreEqual(1, rendered.Count);
        var triangle = rendered.Single();
        Assert.AreEqual((triangle.X1 + triangle.X2 + triangle.X3) / 3f, screen.x, 1f);
        Assert.AreEqual((triangle.Y1 + triangle.Y2 + triangle.Y3) / 3f, screen.y, 1f);
        Assert.AreEqual(-1390f, actual.z, 0.001f, "Detection distance must not use the cosmetic depth cap.");
        Assert.AreEqual(-1400f, enemy.ObjectOffsets!.z);
    }

    private void Update(params OmegaObject3D[] threats) =>
        _manager.Update(new[] { _ship }.Concat(threats).ToArray(), _gameplay, true, _audio, _sounds);

    private static OmegaObject3D CreateObject(string name, int id, float x, float y, float z = 0f, float centerZ = 0f)
    {
        return new OmegaObject3D
        {
            ObjectId = id, ObjectName = name, IsActive = true,
            WorldPosition = new Vector3(), ObjectOffsets = new Vector3(x, y, z), Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = 100 },
            CrashBoxes = new() { OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3(-5f, -5f, centerZ - 5f), new Vector3(5f, 5f, centerZ + 5f)) },
            ObjectParts = new() { new OmegaObjectPart3D { PartName = "Body", IsVisible = true,
                Triangles = new() { new TriangleMeshWithColor { Color = "ffffff", noHidden = true,
                    vert1 = new Vector3(-6f, -3f, centerZ), vert2 = new Vector3(6f, -3f, centerZ),
                    vert3 = new Vector3(0f, 6f, centerZ) } } } }
        };
    }

    private sealed class WarningSounds : ISoundRegistry
    {
        public SoundDefinition Get(string id) => new() { Id = id, Usage = id, File = id + ".wav", Settings = new SoundSettings { Volume = 1f } };
        public bool TryGet(string id, out SoundDefinition definition)
        {
            definition = Get(id);
            return id is "ship_collision_warning" or "ship_enemy_incoming_warning" or "ship_drone_incoming_warning";
        }
    }

    private sealed class CapturingAudio : IAudioPlayer
    {
        public List<string> SoundIds { get; } = new();
        public float MusicVolume { get; private set; } = 0.15f;
        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            SoundIds.Add(definition.Id);
            return new SilentInstance();
        }
        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null) => Play(definition, AudioPlayMode.OneShot, options);
        public void Stop(IAudioInstance instance, bool playEndSegment) => instance.Stop(playEndSegment);
        public void StopAll() { }
        public void StopNonMusic() { }
        public void PlayMusic(SoundDefinition definition, float? volumeOverride = null) { }
        public void SetMusicVolume(float volume) => MusicVolume = volume;
        public void StopMusic() { }
        public void Update(double deltaTimeSeconds) { }
    }

    private sealed class SilentInstance : IAudioInstance
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string SoundId => "ship_collision_warning";
        public bool IsPlaying { get; private set; } = true;
        public bool IsLooping => false;
        public void SetVolume(float volume) { }
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) { }
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
