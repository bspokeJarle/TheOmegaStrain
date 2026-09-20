using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Collision;

namespace TheOmegaStrain.Tests.Physics;

[TestClass]
[DoNotParallelize]
public class FlyingEnemyCollisionTests
{
    private SurfaceState _originalSurface = null!;
    private ShipState _originalShip = null!;
    private GameSettingsState _originalSettings = null!;
    private WeatherVisualState _originalWeather = null!;

    [TestInitialize]
    public void Setup()
    {
        _originalSurface = GameState.SurfaceState;
        _originalShip = GameState.ShipState;
        _originalSettings = GameState.SettingsState;
        _originalWeather = GameState.WeatherVisualState;
        GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3(50000f, 0f, 50000f) };
        GameState.ShipState = new ShipState();
        GameState.SettingsState = new GameSettingsState();
        GameState.WeatherVisualState = new WeatherVisualState();
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = _originalSurface;
        GameState.ShipState = _originalShip;
        GameState.SettingsState = _originalSettings;
        GameState.WeatherVisualState = _originalWeather;
    }

    [DataTestMethod]
    [DataRow(0, -38f, 48f, -15f, 15f, -7f, 12.5f)]
    [DataRow(1, -54.5f, 8f, 10f, 36f, -6.5f, 15f)]
    [DataRow(2, -54.5f, 8f, -36f, -10f, -6.5f, 15f)]
    public void AttackShip_ScalesPaddedBoxesAndTheirCentresWithTheHull(
        int index, float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        var attack = AttackShip.CreateAttackShip(null!);
        Assert.AreEqual(3, attack.CrashBoxes.Count);
        var actual = AabbBounds.FromPoints(attack.CrashBoxes[index]);
        const float scale = 1.5f;
        Assert.AreEqual(new AabbBounds((minX - 12f) * scale, (maxX + 12f) * scale,
            (minY - 12f) * scale, (maxY + 12f) * scale, (minZ - 12f) * scale, (maxZ + 12f) * scale), actual);
        var centre = CollisionBoxMath.GetCenter(actual);
        Assert.AreEqual((minX + maxX) / 2f * scale, centre.x, 0.001f);
        Assert.AreEqual((minY + maxY) / 2f * scale, centre.y, 0.001f);
        Assert.AreEqual((minZ + maxZ) / 2f * scale, centre.z, 0.001f);
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void AttackShip_CloseWingContactNowCollidesAndStartsExplosionAcrossFrameCopies(float pitch)
    {
        var attack = CreateAttackShip(pitch);
        var oldAttack = CreateAttackShip(pitch);
        oldAttack.CrashBoxes = PreviousAttackBoxes();
        foreach (var corner in oldAttack.CrashBoxes.SelectMany(box => box))
        {
            corner.x *= 1.5f;
            corner.y *= 1.5f;
            corner.z *= 1.5f;
        }
        var attackFrame = PrepareFrame(attack);
        var oldFrame = PrepareFrame(oldAttack);
        var shipFrame = PrepareFrame(CreateShip(pitch));

        var oldWing = AabbBounds.FromPoints(oldFrame.CrashBoxes[2]);
        var oldWingCentre = CollisionBoxMath.GetCenter(oldWing);
        var shipBody = AabbBounds.FromPoints(shipFrame.CrashBoxes[0]);
        var shipCentre = CollisionBoxMath.GetCenter(shipBody);
        // A shallow wing contact previously fell short of the shared X overlap margin.
        shipFrame.ObjectOffsets = new Vector3(
            oldWing.MaxX - 14f - shipBody.MinX,
            oldWingCentre.y - shipCentre.y - 200f,
            oldWingCentre.z - shipCentre.z + 400f);

        CrashDetection.HandleCrashboxes(new() { oldFrame, shipFrame }, isPaused: false);
        Assert.IsFalse(oldAttack.ImpactStatus!.HasCrashed, "This contact must reproduce the old miss.");

        CrashDetection.HandleCrashboxes(new() { attackFrame, shipFrame }, isPaused: false);
        Assert.IsTrue(attack.ImpactStatus!.HasCrashed, "The frame copy must share the actual collision with the original.");
        Assert.AreEqual("Ship", attack.ImpactStatus.ObjectName);

        var nextFrame = CopyFrame(attack);
        nextFrame.Movement!.MoveObject(nextFrame, null, null);
        Assert.AreEqual(0, attack.ImpactStatus.ObjectHealth);
        Assert.AreEqual(0, nextFrame.CrashBoxes.Count);
        AssertExplosionParts(nextFrame);
        Assert.IsTrue(attack.ObjectParts.All(part => part.PartName != "ExplodingPart"), "Do not mutate the template geometry.");

        var followingFrame = CopyFrame(attack);
        followingFrame.Movement!.MoveObject(followingFrame, null, null);
        Assert.AreEqual(0, followingFrame.CrashBoxes.Count);
        AssertExplosionParts(followingFrame);
    }

    [DataTestMethod]
    [DataRow(63f, 0f, 189.5f, 400f, 100f, 90, false)]
    [DataRow(70f, 0f, 189.5f, 400f, 100f, 90, false)]
    [DataRow(63f, 100f, -80f, 600f, 100f, 60, false)]
    [DataRow(70f, -100f, 80f, 600f, 100f, 30, false)]
    [DataRow(63f, -200f, 230f, 100f, 650f, 144, false)]
    [DataRow(70f, 150f, -40f, 200f, 550f, 90, false)]
    [DataRow(63f, 0f, 189.5f, 400f, 100f, 90, true)]
    [DataRow(70f, 0f, 189.5f, 400f, 100f, 60, true)]
    public void AttackShip_HoldsVisibleFiringDistanceWithoutRamming(
        float pitch, float mapY, float shipY, float shipZ, float attackZ, int fps, bool startTooClose)
    {
        var originalPitch = WorldViewSetup.CameraPitchDegrees;
        try
        {
            WorldViewSetup.ConfigurePitch(pitch);
            GameState.SurfaceState.GlobalMapPosition.y = mapY;
            var ship = CreateShip(pitch);
            ship.ObjectOffsets = new Vector3(0f, shipY, shipZ);
            GameState.ShipState.ShipObjectOffsets = ship.ObjectOffsets;
            GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(shipY, shipZ);
            GameState.ShipState.ShipCrashCenterWorldPosition = SurfacePositionSyncHelpers.GetObjectCrashCenterWorldPosition(ship);
            var shipFrame = PrepareFrame(ship);
            var attack = CreateAttackShip(pitch);
            attack.WorldPosition = new Vector3(49200f, 0f, 49500f);
            attack.ObjectOffsets = new Vector3(0f, 150f, attackZ);
            if (startTooClose)
            {
                attack.WorldPosition = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(attack);
                attack.WorldPosition.x -= 300f;
            }
            GameState.SurfaceState.AiObjects.Add(attack);
            var controls = (AttackShipControls)attack.Movement!;
            var lastUpdate = typeof(AttackShipControls).GetField("_lastMovementTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            OmegaObject3D frame = null!;

            for (int i = 0; i < fps * 8 && !attack.ImpactStatus!.HasCrashed; i++)
            {
                frame = CopyFrame(attack);
                frame.IsOnScreen = true;
                // Advance the controller without waiting for wall-clock gameplay time.
                lastUpdate.SetValue(controls, DateTime.Now.AddSeconds(-1d / fps));
                controls.MoveObject(frame, null, null);
                new ObjectFrameTransformer().RotateObjectGeometry(frame);
                Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(frame, 800, 450, out _, out _, out _));
                CrashDetection.HandleCrashboxes(new() { frame, shipFrame }, isPaused: false);
            }

            var actualCentre = GeometryMath.GetCenterOfBox(frame.GetAllCrashPointsWorld());
            var shipCentre = GeometryMath.GetCenterOfBox(shipFrame.GetAllCrashPointsWorld());
            Assert.IsFalse(attack.ImpactStatus!.HasCrashed, "A ranged attacker must stop before ramming Ship.");
            var horizontalDistance = VectorMath.Length(new Vector3(
                actualCentre.x - shipCentre.x, 0f, actualCentre.z - shipCentre.z));
            Assert.AreEqual(EnemySetup.AttackShipFiringDistance * ScreenSetup.ScreenScaleX,
                horizontalDistance, 15f, "Measure the rendered collision centres, not raw world coordinates.");
            Assert.AreEqual(shipCentre.y, actualCentre.y, 15f, "Hold Ship's height within the firing ring.");
            Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(frame,
                ScreenSetup.screenSizeX / 2, ScreenSetup.screenSizeY / 2, out var x, out var y, out var z));
            Assert.IsTrue(ProjectionMath.TryProjectVertex(GeometryMath.GetCenterOfBox(frame.CrashBoxes.SelectMany(box => box).ToList()),
                x, y, z, ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom, out var screen));
            Assert.IsTrue(screen.x > 0 && screen.x < ScreenSetup.screenSizeX &&
                          screen.y > 0 && screen.y < ScreenSetup.screenSizeY,
                $"The firing position must remain visible: ({screen.x:F1}, {screen.y:F1}).");
        }
        finally
        {
            WorldViewSetup.ConfigurePitch(originalPitch);
        }
    }

    [DataTestMethod]
    [DataRow("AttackShip", 63f)]
    [DataRow("SpaceSwan", 63f)]
    [DataRow("AttackShip", 70f)]
    [DataRow("SpaceSwan", 70f)]
    public void RamTarget_AlignsAttackShipAndSwanUsingTheSameCollisionPipeline(string name, float pitch)
    {
        var ship = CreateShip(pitch);
        ship.ObjectOffsets = new Vector3(25f, -80f, 550f);
        GameState.ShipState.ShipObjectOffsets = ship.ObjectOffsets;
        GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(-80f, 550f);
        GameState.ShipState.ShipCrashCenterWorldPosition = SurfacePositionSyncHelpers.GetObjectCrashCenterWorldPosition(ship);
        var enemy = name == "AttackShip" ? CreateAttackShip(pitch) : SpaceSwan.CreateSpaceSwan(null!);
        enemy.Rotation = new Vector3(pitch, 15f, 220f);
        enemy.ObjectOffsets = new Vector3(40f, 120f, 170f);
        enemy.ImpactStatus = new ImpactStatus { ObjectHealth = 155 };
        enemy.WorldPosition = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(enemy);
        var expectedPosition = enemy.WorldPosition;
        var enemyFrame = PrepareFrame(enemy);
        var shipFrame = PrepareFrame(ship);
        CrashDetection.HandleCrashboxes(new() { enemyFrame, shipFrame }, isPaused: false);
        Assert.IsTrue(enemy.ImpactStatus.HasCrashed);
        Assert.AreEqual("Ship", enemy.ImpactStatus.ObjectName);

        var marker = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(enemy)!;
        Assert.AreEqual(GameState.SurfaceState.GlobalMapPosition.x + MapSetup.viewPortCenterOffsetX, marker.x, 0.01f);
        Assert.AreEqual(GameState.SurfaceState.GlobalMapPosition.z + MapSetup.viewPortCenterOffsetX, marker.z, 0.01f);
        Assert.AreSame(expectedPosition, enemy.WorldPosition, "Rendering must not mutate the original's transform.");
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void AttackShip_SeparatedShipStillMisses(float pitch)
    {
        var attack = PrepareFrame(CreateAttackShip(pitch));
        var ship = PrepareFrame(CreateShip(pitch));
        ship.ObjectOffsets = new Vector3(400f, -200f, 400f);
        CrashDetection.HandleCrashboxes(new() { attack, ship }, isPaused: false);
        Assert.IsFalse(attack.ImpactStatus!.HasCrashed);
        Assert.IsFalse(ship.ImpactStatus!.HasCrashed);
    }

    [TestMethod]
    public void InvisibleGeometry_DoesNotLeaveCollisionOnlyExplosionBehind()
    {
        var attack = PrepareFrame(CreateAttackShip(70f));
        var ship = PrepareFrame(CreateShip(70f));
        foreach (var part in attack.ObjectParts)
            part.IsVisible = false;

        CrashDetection.HandleCrashboxes(new() { attack, ship }, isPaused: false);

        Assert.IsFalse(attack.ImpactStatus!.HasCrashed);
        Assert.IsFalse(ship.ImpactStatus!.HasCrashed);
    }

    [DataTestMethod]
    [DataRow(63f, 175f, true)]
    [DataRow(70f, 175f, true)]
    [DataRow(63f, 185f, false)]
    [DataRow(70f, 185f, false)]
    public void Drone_OverlappingBoxesUseTheCloseContactLimit(float pitch, float distance, bool expectedHit)
    {
        var drone = KamikazeDrone.CreateKamikazeDrone(null!);
        drone.WorldPosition = new Vector3(50000f, 0f, 50000f);
        drone.ObjectOffsets = new Vector3(0f, -200f, 400f);
        drone.Rotation = new Vector3(pitch, 0f, 90f);
        drone.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth };
        // Isolate one real engine/body box pair so another pair cannot mask the distance gate.
        drone.CrashBoxes = new() { drone.CrashBoxes[2] };
        var ship = CreateShip(pitch);
        ship.CrashBoxes = new() { ship.CrashBoxes[0] };
        var droneFrame = PrepareFrame(drone);
        var shipFrame = PrepareFrame(ship);
        var droneCentre = CollisionBoxMath.GetCenter(AabbBounds.FromPoints(droneFrame.CrashBoxes[0]));
        var shipCentre = CollisionBoxMath.GetCenter(AabbBounds.FromPoints(shipFrame.CrashBoxes[0]));
        shipFrame.ObjectOffsets = new Vector3(
            droneCentre.x - shipCentre.x + distance,
            droneCentre.y - shipCentre.y - 200f,
            droneCentre.z - shipCentre.z + 400f);

        Assert.IsTrue(OmegaObject3DHelpers.CheckCollisionBoxVsBox(
            droneFrame.GetAllCrashPointsWorld(), shipFrame.GetAllCrashPointsWorld()), "The boxes must overlap before testing the extra gate.");
        CrashDetection.HandleCrashboxes(new() { droneFrame, shipFrame }, isPaused: false);
        Assert.AreEqual(expectedHit, drone.ImpactStatus.HasCrashed);
        Assert.AreEqual(expectedHit, ship.ImpactStatus!.HasCrashed);
    }

    private static OmegaObject3D CreateAttackShip(float pitch)
    {
        var attack = AttackShip.CreateAttackShip(null!);
        attack.WorldPosition = new Vector3(50000f, 0f, 50000f);
        attack.ObjectOffsets = new Vector3(0f, -200f, 400f);
        attack.Rotation = new Vector3(pitch, 0f, 90f);
        attack.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.AttackShipHealth };
        attack.Movement = new AttackShipControls();
        return attack;
    }

    private static OmegaObject3D CreateShip(float pitch)
    {
        var ship = Ship.CreateShip(null!);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.Rotation = new Vector3(pitch, 0f, 0f);
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = 100 };
        return ship;
    }

    private static OmegaObject3D CopyFrame(OmegaObject3D original) =>
        OmegaObjectHelpers.DeepCopy3dObjects(new() { original })[0];

    private static OmegaObject3D PrepareFrame(OmegaObject3D original)
    {
        var frame = CopyFrame(original);
        new ObjectFrameTransformer().RotateObjectGeometry(frame);
        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(frame, 800, 450, out _, out _, out _));
        return frame;
    }

    private static void AssertExplosionParts(I3dObject obj)
    {
        Assert.IsTrue(obj.ObjectParts.Any(part => part.PartName == "ExplodingPart"));
        Assert.IsTrue(obj.ObjectParts.All(part =>
            part.PartName is "ExplodingPart" or "Shadow" or "ExplosionShadowReference"));
    }

    private static List<List<IVector3>> PreviousAttackBoxes() => new()
    {
        OmegaObject3DHelpers.GenerateCrashBoxCorners(new(-38f, -15f, -7f), new(48f, 15f, 12.5f)),
        OmegaObject3DHelpers.GenerateCrashBoxCorners(new(-54.5f, 10f, -6.5f), new(8f, 36f, 15f)),
        OmegaObject3DHelpers.GenerateCrashBoxCorners(new(-54.5f, -36f, -6.5f), new(8f, -10f, 15f))
    };
}
