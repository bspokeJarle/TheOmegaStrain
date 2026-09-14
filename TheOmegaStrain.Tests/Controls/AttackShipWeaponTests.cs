using System.Reflection;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Collision;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
[DoNotParallelize]
public partial class AttackShipWeaponTests
{
    private SurfaceState _surface = null!;
    private ShipState _ship = null!;
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void Setup()
    {
        _surface = GameState.SurfaceState;
        _ship = GameState.ShipState;
        GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3(50000, 0, 50000) };
        GameState.ShipState = new ShipState
        {
            ShipObjectOffsets = new Vector3(0, 0, 400),
            ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(0, 400)
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = _surface;
        GameState.ShipState = _ship;
    }

    [TestMethod]
    public void OffscreenOrInactiveAttackShip_CannotFire()
    {
        var ship = CreateAttackShip();
        ship.IsOnScreen = false;
        Fire(ship, Now);
        Assert.AreEqual(0, ship.WeaponSystems!.ActiveWeapons.Count);
        ship.IsOnScreen = true;
        ship.IsActive = false;
        Fire(ship, Now);
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);
    }

    [DataTestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(90)]
    public void RocketMovesAt750UnitsPerSecondUsingDeltaTime(int fps)
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        Assert.AreEqual(750f, active.Velocity);
        var start = active.WeaponObject.ObjectOffsets;
        var previousUpdate = DateTime.UtcNow.AddSeconds(-1d / fps);
        active.LastUpdateUtc = previousUpdate;

        ship.WeaponSystems.MoveWeapon(null, null);

        float elapsed = (float)(active.LastUpdateUtc - previousUpdate).TotalSeconds;
        float expectedDistance = 750f * elapsed;
        Assert.AreEqual(expectedDistance, active.DistanceTraveled, 0.001f);
        Assert.AreEqual(expectedDistance,
            VectorMath.Length(VectorMath.Subtract(active.WeaponObject.ObjectOffsets, start)), 0.001f);
    }

    [DataTestMethod]
    [DataRow(1499f, 1)]
    [DataRow(1500f, 1)]
    [DataRow(1500.01f, 0)]
    public void RocketLaunch_Uses1500UnitDistanceLimit(float distance, int expectedRockets)
    {
        var ship = CreateAttackShip();
        var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(ship);
        ship.WorldPosition = new Vector3(target.x - distance, target.y, target.z);
        Fire(ship, Now);
        Assert.AreEqual(expectedRockets, ship.WeaponSystems!.ActiveWeapons.Count);
        Assert.IsTrue(ship.IsOnScreen, "The controller only reads the render/AI system's flag.");
    }

    [TestMethod]
    public void MissingGuides_DoNotStartCooldown()
    {
        var ship = CreateAttackShip(bindGuides: false);
        Fire(ship, Now);
        Assert.AreEqual(0, ship.WeaponSystems!.ActiveWeapons.Count);
        BindGuides(ship);
        Fire(ship, Now);
        Assert.AreEqual(1, ship.WeaponSystems.ActiveWeapons.Count);
    }

    [TestMethod]
    public void FailedLaunch_DoesNotStartCooldown()
    {
        var ship = CreateAttackShip();
        var templates = new List<I3dObject>();
        ship.WeaponSystems = CreateWeapons(ship, templates);
        Fire(ship, Now);
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);
        templates.Add(Rocket.CreateRocket(null!));
        Fire(ship, Now);
        Assert.AreEqual(1, ship.WeaponSystems.ActiveWeapons.Count);
    }

    [TestMethod]
    public void CooldownAndOneActiveRocket_ArePerAttackShip()
    {
        var first = CreateAttackShip();
        var second = CreateAttackShip();
        Fire(first, Now);
        Assert.AreEqual(1, first.WeaponSystems!.ActiveWeapons.Count);
        first.WeaponSystems.ActiveWeapons.Clear();
        Fire(first, Now); // Observe removal: cooldown starts here, not at the original launch.
        Fire(first, Now.AddSeconds(9.999));
        Assert.AreEqual(0, first.WeaponSystems.ActiveWeapons.Count);
        Fire(first, Now.AddSeconds(10));
        Assert.AreEqual(1, first.WeaponSystems.ActiveWeapons.Count);
        Fire(first, Now.AddSeconds(20));
        Assert.AreEqual(1, first.WeaponSystems.ActiveWeapons.Count, "An active rocket still blocks after cooldown.");
        Fire(second, Now.AddSeconds(20));
        Assert.AreEqual(1, second.WeaponSystems!.ActiveWeapons.Count);
        Assert.AreNotSame(first.WeaponSystems.ActiveWeapons, second.WeaponSystems.ActiveWeapons);
    }

    [DataTestMethod]
    [DataRow(63f, 90f)]
    [DataRow(70f, 180f)]
    public void LaunchUsesCurrentNoseGuideAndRetargetsShip(float pitch, float heading)
    {
        var ship = CreateAttackShip();
        ship.Rotation = new Vector3(pitch, 0, heading);
        var rendered = OmegaObjectHelpers.DeepCopySingleObject(ship);
        new ObjectFrameTransformer().RotateObjectGeometry((OmegaObject3D)rendered);
        var start = rendered.ObjectParts.Single(p => p.PartName == "WeaponStartGuide").Triangles[0].vert1;
        var muzzle = VectorMath.Add(start, ship.ObjectOffsets);
        var map = GameState.SurfaceState.GlobalMapPosition;
        var renderOrigin = new Vector3(ship.WorldPosition.x - map.x + muzzle.x,
            ship.WorldPosition.y - map.y + muzzle.y, map.z - ship.WorldPosition.z + muzzle.z);
        var expectedDirection = VectorMath.Normalize(VectorMath.Subtract(GameState.ShipState.ShipObjectOffsets, renderOrigin));

        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        Assert.AreEqual("EnemyRocket", active.WeaponObject.ObjectName);
        Assert.AreEqual(WeaponType.Rocket, active.WeaponType);
        AssertVector(VectorMath.Add(start, ship.ObjectOffsets), active.WeaponObject.ObjectOffsets);
        AssertVector(expectedDirection, active.Trajectory);
        AssertVector(ship.WorldPosition, active.WeaponObject.WorldPosition);
        var rotate = new OmegaMeshRotation();
        var noseDirection = rotate.RotatePoint(active.WeaponObject.Rotation.z, new Vector3(1, 0, 0), 'Z');
        noseDirection = rotate.RotatePoint(active.WeaponObject.Rotation.y, noseDirection, 'Y');
        noseDirection = rotate.RotatePoint(active.WeaponObject.Rotation.x, noseDirection, 'X');
        AssertVector(expectedDirection, noseDirection);

        ship.WorldPosition.x += 100;
        ship.Rotation.z += 90;
        GameState.ShipState.ShipWorldPosition.x += 500;
        GameState.ShipState.ShipObjectOffsets.x += 500;
        var retargetedDirection = VectorMath.Normalize(VectorMath.Subtract(GameState.ShipState.ShipObjectOffsets, renderOrigin));
        active.LastUpdateUtc = DateTime.UtcNow.AddSeconds(-0.1);
        ship.WeaponSystems.MoveWeapon(null, null);
        AssertVector(retargetedDirection, active.Trajectory);
        Assert.AreNotEqual(ship.WorldPosition.x, active.WeaponObject.WorldPosition.x);
    }

    [DataTestMethod]
    [DataRow(99f, true)]
    [DataRow(100f, true)]
    [DataRow(101f, false)]
    public void GuidanceLocksAtOneHundredUnitsAndNeverReacquires(float distance, bool locks)
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        active.Velocity = 0f; // Isolate the decision exactly at the distance boundary.
        active.WeaponObject.WorldPosition = new Vector3(50000, 0, 50000);
        active.WeaponObject.ObjectOffsets = new Vector3(distance, 0, 400);
        var initialDirection = active.Trajectory;
        AdvanceRocket(ship, active);
        Assert.AreEqual(locks, active.RocketGuidanceLocked);
        if (locks) AssertVector(initialDirection, active.Trajectory);
        else AssertVector(new Vector3(-1, 0, 0), active.Trajectory);

        var finalApproachDirection = active.Trajectory;
        GameState.ShipState.ShipObjectOffsets = new Vector3(0, -500, 700);
        AdvanceRocket(ship, active);
        Assert.AreEqual(locks, active.RocketGuidanceLocked);
        if (locks) AssertVector(finalApproachDirection, active.Trajectory);
        else Assert.IsTrue(active.Trajectory.y < 0 && active.Trajectory.z > 0);
    }

    [DataTestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(90)]
    public void GuidanceLocksOnTheFrameThatEntersTheFinalApproach(int fps)
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        active.WeaponObject.WorldPosition = new Vector3(50000, 0, 50000);
        active.WeaponObject.ObjectOffsets = new Vector3(105, 0, 400);
        AdvanceRocket(ship, active, 1d / fps);
        Assert.IsTrue(active.RocketGuidanceLocked);
        AssertVector(new Vector3(-1, 0, 0), active.Trajectory);

        GameState.ShipState.ShipObjectOffsets = new Vector3(0, -500, 700);
        AdvanceRocket(ship, active, 1d / fps);
        AssertVector(new Vector3(-1, 0, 0), active.Trajectory);
    }

    [TestMethod]
    public void LaunchWithinFinalApproachIsLockedEvenThroughOwnerFrameCopies()
    {
        var ship = CreateAttackShip();
        ship.Rotation = new Vector3();
        var muzzle = VectorMath.Add(ship.ObjectOffsets,
            ship.ObjectParts.Single(part => part.PartName == "WeaponStartGuide").Triangles[0].vert1);
        ship.WorldPosition = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(muzzle);
        ship.WorldPosition.x -= 90;
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        Assert.IsTrue(active.RocketGuidanceLocked);
        var direction = active.Trajectory;

        var frameCopy = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(ship);
        GameState.ShipState.ShipObjectOffsets.y -= 500;
        AdvanceRocket(frameCopy, active);
        Assert.AreSame(ship.WeaponSystems, frameCopy.WeaponSystems);
        Assert.IsTrue(active.RocketGuidanceLocked);
        AssertVector(direction, active.Trajectory);
    }

    [TestMethod]
    public void RocketLongFrameCannotSkipTheLockingZone()
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        active.WeaponObject.WorldPosition = new Vector3(50000, 0, 50000);
        active.WeaponObject.ObjectOffsets = new Vector3(150, 0, 400);
        AdvanceRocket(ship, active, 2d);
        Assert.AreEqual(75f, active.DistanceTraveled, 0.001f);
        Assert.IsTrue(active.RocketGuidanceLocked);
    }

    [TestMethod]
    public void GuidanceStateIsPerRocketAndDoesNotAffectPlayerRockets()
    {
        var first = CreateAttackShip();
        var second = CreateAttackShip();
        Fire(first, Now);
        Fire(second, Now);
        var locked = (ActiveWeapon)first.WeaponSystems!.ActiveWeapons.Single();
        var unlocked = (ActiveWeapon)second.WeaponSystems!.ActiveWeapons.Single();
        locked.RocketGuidanceLocked = true;
        GameState.ShipState.ShipObjectOffsets.y -= 400;
        AdvanceRocket(second, unlocked);
        Assert.IsFalse(unlocked.RocketGuidanceLocked);
        Assert.IsTrue(unlocked.Trajectory.y < 0);

        second.WeaponSystems.ActiveWeapons.Clear();
        ((Weapons)second.WeaponSystems).FireAsEnemyWeapon = false;
        second.WeaponSystems.FireWeapon(new Vector3(288, 0, 0), new Vector3(48, 0, 0),
            second.WorldPosition, WeaponType.Rocket, second, 0);
        var playerRocket = (ActiveWeapon)second.WeaponSystems.ActiveWeapons.Single();
        var direction = playerRocket.Trajectory;
        AdvanceRocket(second, playerRocket);
        AssertVector(direction, playerRocket.Trajectory);
        Assert.IsFalse(playerRocket.RocketGuidanceLocked);
    }

    private static void AdvanceRocket(OmegaObject3D owner, ActiveWeapon active, double deltaSeconds = 1d / 90d)
    {
        active.LastUpdateUtc = DateTime.UtcNow.AddSeconds(-deltaSeconds);
        owner.WeaponSystems!.MoveWeapon(null, null);
    }

    [DataTestMethod]
    [DataRow("collision")]
    public void RocketUsesExistingActiveWeaponCleanup(string reason)
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        switch (reason)
        {
            case "collision": active.WeaponObject.ImpactStatus!.HasCrashed = true; break;
        }
        ship.WeaponSystems.MoveWeapon(null, null);
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);

        Fire(ship, Now); // Observe cleanup; the configured delay starts now.
        Fire(ship, Now.AddSeconds(9));
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count, "Cleanup does not bypass cooldown.");
        ship.IsOnScreen = false;
        Fire(ship, Now.AddSeconds(10));
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);
        ship.IsOnScreen = true;
        Fire(ship, Now.AddSeconds(10));
        Assert.AreEqual(1, ship.WeaponSystems.ActiveWeapons.Count, "Cleanup must allow the next on-screen launch.");
        Assert.AreNotEqual(active.WeaponObject.ObjectId, ship.WeaponSystems.ActiveWeapons.Single().WeaponObject.ObjectId);
    }

    [DataTestMethod]
    [DataRow(63f, -650f, -500f, 0f, 90, false)]
    [DataRow(70f, 650f, -500f, 90f, 60, false)]
    [DataRow(63f, 250f, 1200f, 180f, 90, false)]
    [DataRow(70f, -250f, 1200f, 270f, 30, false)]
    [DataRow(63f, -650f, -500f, 0f, 90, true)]
    [DataRow(70f, -250f, 1200f, 270f, 30, true)]
    public void FiredRocket_HitsShipsRealCollisionBoxes(float pitch, float dx, float dz, float yaw, int fps, bool moveShip)
    {
        var player = Ship.CreateShip(null!);
        player.ObjectName = "Ship";
        player.WorldPosition = new Vector3();
        player.ObjectOffsets = new Vector3(0f, 189.5f, 400f);
        player.Rotation = new Vector3(pitch, 0f, yaw);
        player.ImpactStatus = new ImpactStatus { ObjectHealth = 100 };
        player.IsOnScreen = true;
        GameState.ShipState.ShipObjectOffsets = player.ObjectOffsets;
        GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(189.5f, 400f);
        GameState.ShipState.ShipCrashCenterWorldPosition = SurfacePositionSyncHelpers.GetObjectCrashCenterWorldPosition(player);
        var playerFrame = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(player);
        new ObjectFrameTransformer().RotateObjectGeometry(playerFrame);
        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(playerFrame, 750, 512, out _, out _, out _));

        var attacker = CreateAttackShip();
        attacker.Rotation = new Vector3(pitch, 0f, 90f);
        // Distances are relative to Ship, not the map origin, to stay inside launch range.
        var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(attacker);
        attacker.WorldPosition = new Vector3(target.x + dx, target.y, target.z + dz);
        Fire(attacker, Now);
        Assert.AreEqual(1, attacker.WeaponSystems!.ActiveWeapons.Count, "Collision scenario must launch within range.");
        var active = (ActiveWeapon)attacker.WeaponSystems!.ActiveWeapons.Single();
        var rocket = (OmegaObject3D)active.WeaponObject;
        rocket.IsOnScreen = true;
        if (moveShip)
        {
            // Ship leaves the original firing line on all three axes after launch.
            player.ObjectOffsets = new Vector3(400, 9.5f, 550);
            GameState.ShipState.ShipObjectOffsets = player.ObjectOffsets;
            GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(9.5f, 550);
            GameState.ShipState.ShipCrashCenterWorldPosition = SurfacePositionSyncHelpers.GetObjectCrashCenterWorldPosition(player);
            playerFrame.ObjectOffsets = player.ObjectOffsets;
            Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(playerFrame, 750, 512, out _, out _, out _));
        }
        float closest = float.MaxValue;
        for (int i = 0; i < fps * 5 && attacker.WeaponSystems.ActiveWeapons.Count > 0; i++)
        {
            active.LastUpdateUtc = DateTime.UtcNow.AddSeconds(-1d / fps);
            attacker.WeaponSystems.MoveWeapon(null, null);
            Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(rocket, 750, 512, out _, out _, out _));
            closest = MathF.Min(closest, (float)GeometryMath.GetDistance(
                GeometryMath.GetCenterOfBox(rocket.GetAllCrashPointsWorld()),
                GeometryMath.GetCenterOfBox(playerFrame.GetAllCrashPointsWorld())));
            CrashDetection.HandleCrashboxes(new() { rocket, playerFrame }, isPaused: false);
            if (rocket.ImpactStatus!.HasCrashed) break;
        }
        Assert.IsTrue(player.ImpactStatus.HasCrashed,
            $"Rocket missed: closest centres={closest:F1}, travel={active.DistanceTraveled:F1}, localZ={rocket.ObjectOffsets.z:F1}, active={attacker.WeaponSystems.ActiveWeapons.Count}.");
        Assert.AreEqual("EnemyRocket", player.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void FrameCopies_ShareWeaponSystemAndControllerWithoutDuplicatingLaunch()
    {
        var original = CreateAttackShip(bindGuides: false);
        var first = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(original);
        first.Movement!.MoveObject(first, null, null);
        Assert.AreEqual(0, original.WeaponSystems!.ActiveWeapons.Count);

        new ObjectFrameTransformer().RotateObjectGeometry(first);
        BindGuides(first);
        var second = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(original);
        typeof(AttackShipControls).GetField("_lastMovementTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(second.Movement, DateTime.Now.AddSeconds(-0.02));
        second.Movement!.MoveObject(second, null, null);
        Assert.AreSame(original.Movement, second.Movement);
        Assert.AreSame(original.WeaponSystems, second.WeaponSystems);
        Assert.AreEqual(1, original.WeaponSystems.ActiveWeapons.Count);
        Fire(second, DateTime.UtcNow);
        Assert.AreEqual(1, original.WeaponSystems.ActiveWeapons.Count);
    }

    [TestMethod]
    public void MoveObject_CleansUpPreviousRocketAndWaitsBeforeLaunchingNext()
    {
        var ship = CreateAttackShip();
        Fire(ship, DateTime.UtcNow.AddSeconds(-11));
        var previous = ship.WeaponSystems!.ActiveWeapons.Single();
        previous.WeaponObject.ImpactStatus!.HasCrashed = true;

        ship.Movement!.MoveObject(ship, null, null);

        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);
        Fire(ship, DateTime.UtcNow.AddSeconds(11));
        Assert.AreEqual(1, ship.WeaponSystems.ActiveWeapons.Count);
        Assert.AreNotEqual(previous.WeaponObject.ObjectId, ship.WeaponSystems.ActiveWeapons.Single().WeaponObject.ObjectId);
    }

    [TestMethod]
    public void WeaponSystem_AlsoRejectsOffscreenEnemyLaunch()
    {
        var ship = CreateAttackShip();
        ship.IsOnScreen = false;
        ship.WeaponSystems!.FireWeapon(new Vector3(288, 0, 0), new Vector3(48, 0, 0),
            ship.WorldPosition, WeaponType.Rocket, ship, 0);
        Assert.AreEqual(0, ship.WeaponSystems.ActiveWeapons.Count);
    }

    [TestMethod]
    public void LaunchDoesNotMutateTemplateGeometryOrImpactState()
    {
        var ship = CreateAttackShip();
        var template = Rocket.CreateRocket(null!);
        template.ImpactStatus = new ImpactStatus { ObjectHealth = 75 };
        var originalVertex = template.ObjectParts[0].Triangles[0].vert1;
        var expectedVertex = new Vector3(originalVertex.x, originalVertex.y, originalVertex.z);
        var originalCorner = template.CrashBoxes[0][0];
        var expectedCorner = new Vector3(originalCorner.x, originalCorner.y, originalCorner.z);
        ship.WeaponSystems = CreateWeapons(ship, new List<I3dObject> { template });
        ship.Rotation = new Vector3(63, 15, 110);
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems.ActiveWeapons.Single();
        AssertVector(expectedVertex, template.ObjectParts[0].Triangles[0].vert1);
        AssertVector(expectedCorner, template.CrashBoxes[0][0]);
        Assert.AreNotSame(template.ImpactStatus, active.WeaponObject.ImpactStatus);
        Assert.AreNotEqual(template.ObjectId, active.WeaponObject.ObjectId);
        Assert.AreEqual(75, template.ImpactStatus.ObjectHealth);
    }

    [TestMethod]
    public void RetargetingRotatesGeometryAndCrashBoxesWithoutMutatingTemplateOrRuntimeState()
    {
        var ship = CreateAttackShip();
        var template = Rocket.CreateRocket(null!);
        var templateVertices = template.ObjectParts.SelectMany(part => part.Triangles)
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
            .Select(v => (v.x, v.y, v.z)).ToArray();
        var templateCorners = template.CrashBoxes.SelectMany(box => box).Select(v => (v.x, v.y, v.z)).ToArray();
        ship.WeaponSystems = CreateWeapons(ship, new List<I3dObject> { template });
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems.ActiveWeapons.Single();
        var rocket = active.WeaponObject;
        var impact = rocket.ImpactStatus;
        active.Velocity = 0;
        for (int i = 0; i < 12; i++)
        {
            GameState.ShipState.ShipObjectOffsets = new Vector3(300 + 30 * i, -200 - 20 * i, 700 - 50 * i);
            AdvanceRocket(ship, active);
            Assert.IsFalse(active.RocketGuidanceLocked);
            Assert.AreSame(rocket, active.WeaponObject);
            Assert.AreSame(impact, rocket.ImpactStatus);
            var expected = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(template);
            expected.Rotation = rocket.Rotation;
            new ObjectFrameTransformer().RotateObjectGeometry(expected);
            for (int part = 0; part < expected.ObjectParts.Count; part++)
            {
                for (int triangle = 0; triangle < expected.ObjectParts[part].Triangles.Count; triangle++)
                {
                    var expectedMesh = expected.ObjectParts[part].Triangles[triangle];
                    var actualMesh = rocket.ObjectParts[part].Triangles[triangle];
                    AssertVector(expectedMesh.vert1, actualMesh.vert1);
                    AssertVector(expectedMesh.vert2, actualMesh.vert2);
                    AssertVector(expectedMesh.vert3, actualMesh.vert3);
                }
            }
            for (int corner = 0; corner < expected.CrashBoxes[0].Count; corner++)
                AssertVector(expected.CrashBoxes[0][corner], rocket.CrashBoxes[0][corner]);
            var nose = rocket.ObjectParts.Single(part => part.PartName == "RocketNose").Triangles[0].vert1;
            AssertVector(active.Trajectory, VectorMath.Normalize(nose));
        }
        CollectionAssert.AreEqual(templateVertices, template.ObjectParts.SelectMany(part => part.Triangles)
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
            .Select(v => (v.x, v.y, v.z)).ToArray());
        CollectionAssert.AreEqual(templateCorners, template.CrashBoxes.SelectMany(box => box).Select(v => (v.x, v.y, v.z)).ToArray());
    }

    [TestMethod]
    public void MoveObject_AdvancesExistingRocketWhileOwnerIsExploding()
    {
        var ship = CreateAttackShip();
        Fire(ship, Now);
        var active = (ActiveWeapon)ship.WeaponSystems!.ActiveWeapons.Single();
        active.LastUpdateUtc = DateTime.UtcNow.AddSeconds(-0.1);
        ship.ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "Ship", ObjectHealth = 155 };
        ship.Movement!.MoveObject(ship, null, null);
        Assert.IsTrue(active.DistanceTraveled > 0);
        Assert.AreEqual(1, ship.WeaponSystems.ActiveWeapons.Count);
    }

    private static OmegaObject3D CreateAttackShip(bool bindGuides = true)
    {
        var ship = AttackShip.CreateAttackShip(null!);
        ship.WorldPosition = new Vector3(49500, 0, 50000);
        ship.ObjectOffsets = new Vector3(0, 0, 100);
        ship.IsOnScreen = true;
        ship.IsActive = true;
        ship.Movement = new AttackShipControls();
        ship.WeaponSystems = CreateWeapons(ship, new List<I3dObject> { Rocket.CreateRocket(null!) });
        if (bindGuides) BindGuides(ship);
        return ship;
    }

    private static Weapons CreateWeapons(OmegaObject3D ship, List<I3dObject> templates)
        => new(templates, ship.Movement!, ship) { FireAsEnemyWeapon = true, ShowAimAssist = false };

    private static void BindGuides(OmegaObject3D ship)
    {
        ship.Movement!.SetWeaponGuideCoordinates(
            ship.ObjectParts.Single(p => p.PartName == "WeaponStartGuide").Triangles[0], null!);
        ship.Movement.SetWeaponGuideCoordinates(null!,
            ship.ObjectParts.Single(p => p.PartName == "WeaponDirectionGuide").Triangles[0]);
    }

    private static void Fire(OmegaObject3D ship, DateTime now)
        => typeof(AttackShipControls).GetMethod("UpdateFire", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ship.Movement, new object[] { ship, now });

    private static void AssertVector(IVector3 expected, IVector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 0.001f);
        Assert.AreEqual(expected.y, actual.y, 0.001f);
        Assert.AreEqual(expected.z, actual.z, 0.001f);
    }
}
