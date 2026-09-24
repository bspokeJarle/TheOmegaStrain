using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class HatchDroppedDroneControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState { AiObjects = new List<OmegaObject3D>() };
        GameState.GamePlayState = new GamePlayState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
    }

    [TestMethod]
    public void MothershipShield_FirstThreeSeconds_AppliesTwentyPercentWeaponDamage()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(now: () => now);
        var drone = CreateDrone();
        int fullDamage = WeaponSetup.GetWeaponDamage("Lazer");

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(2.5);
        MarkLazerHit(drone);

        controls.MoveObject(drone, null, null);

        int expectedDamage = Math.Max(1, (int)MathF.Round(fullDamage * 0.2f));
        Assert.AreEqual(EnemySetup.KamikazeDroneHealth - expectedDamage, drone.ImpactStatus!.ObjectHealth);
        Assert.IsFalse(drone.ImpactStatus.HasCrashed);
        Assert.IsTrue(drone.CrashBoxes!.Count > 0, "Shielded drones must remain hittable while leaving the hatch.");
    }

    [TestMethod]
    public void MothershipShield_ShipCollisionExplodesDroneAndRemovesCrashBoxes()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(now: () => now);
        var drone = CreateDrone();

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(1);
        drone.ImpactStatus!.HasCrashed = true;
        drone.ImpactStatus.ObjectName = "Ship";
        controls.MoveObject(drone, null, null);

        Assert.AreEqual(0, drone.ImpactStatus.ObjectHealth);
        Assert.AreEqual(0, drone.CrashBoxes.Count,
            "The hatch shield must not leave a collidable drone touching Ship for multiple frames.");
    }

    [TestMethod]
    public void MothershipShield_AfterThreeSeconds_AppliesNormalWeaponDamage()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(now: () => now);
        var drone = CreateDrone();
        int fullDamage = WeaponSetup.GetWeaponDamage("Lazer");

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(3.1);
        MarkLazerHit(drone);

        controls.MoveObject(drone, null, null);

        Assert.AreEqual(EnemySetup.KamikazeDroneHealth - fullDamage, drone.ImpactStatus!.ObjectHealth);
    }

    [TestMethod]
    public void MothershipShield_OverlayDoesNotConsumeShieldDuration()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(now: () => now);
        var drone = CreateDrone();
        int fullDamage = WeaponSetup.GetWeaponDamage("Lazer");

        GameState.ScreenOverlayState.ShowOverlay = true;
        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(5);
        controls.MoveObject(drone, null, null);

        GameState.ScreenOverlayState.ShowOverlay = false;
        MarkLazerHit(drone);
        controls.MoveObject(drone, null, null);

        int shieldedDamage = Math.Max(1, (int)MathF.Round(fullDamage * 0.2f));
        Assert.AreEqual(EnemySetup.KamikazeDroneHealth - shieldedDamage, drone.ImpactStatus!.ObjectHealth);
    }

    [TestMethod]
    public void HatchDrop_MovesDiagonallyDownBeforeHoming()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(
            now: () => now,
            dropHorizontalUnitsPerSecond: -120f);
        var drone = CreateDrone();

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(0.5);
        controls.MoveObject(drone, null, null);

        Assert.AreEqual(-60f, drone.ObjectOffsets!.x, 0.01f);
        Assert.AreEqual(90f, drone.ObjectOffsets.y, 0.01f);
    }

    [TestMethod]
    public void HatchDrop_StaggeredDroneWaitsBeforeDiagonalEscape()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(
            initialDelaySeconds: 0.3f,
            now: () => now,
            dropHorizontalUnitsPerSecond: 120f);
        var drone = CreateDrone();

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(0.2);
        controls.MoveObject(drone, null, null);

        Assert.AreEqual(0f, drone.ObjectOffsets!.x, 0.01f);
        Assert.AreEqual(0f, drone.ObjectOffsets.y, 0.01f);
    }

    [TestMethod]
    public void HatchDrop_DeepCopyMovement_SynchronizesDiagonalEscapeToAuthoritativeDrone()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(
            now: () => now,
            dropHorizontalUnitsPerSecond: 120f);
        var authoritativeDrone = CreateDrone();
        var renderedCopy = CreateDrone();
        renderedCopy.ObjectId = authoritativeDrone.ObjectId;
        GameState.SurfaceState.AiObjects.Add(authoritativeDrone);

        controls.MoveObject(renderedCopy, null, null);
        now = now.AddSeconds(0.5);
        controls.MoveObject(renderedCopy, null, null);

        Assert.AreEqual(60f, authoritativeDrone.ObjectOffsets!.x, 0.01f);
        Assert.AreEqual(90f, authoritativeDrone.ObjectOffsets.y, 0.01f);
    }

    [TestMethod]
    public void HatchDrop_HomingHandoff_EasesOutEscapeVelocityInsteadOfStoppingAbruptly()
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controls = new HatchDroppedDroneControls(
            now: () => now,
            dropHorizontalUnitsPerSecond: 120f);
        var drone = CreateDrone();

        controls.MoveObject(drone, null, null);
        now = now.AddSeconds(2.0);
        controls.MoveObject(drone, null, null);
        float xAtHandoff = drone.ObjectOffsets!.x;

        now = now.AddSeconds(0.25);
        controls.MoveObject(drone, null, null);
        float firstBlendedStep = drone.ObjectOffsets.x - xAtHandoff;

        Assert.IsTrue(firstBlendedStep > 0f, "Escape momentum should continue into the homing transition.");
        Assert.IsTrue(firstBlendedStep < 30f, "Escape velocity should already be easing down during the transition.");
    }

    private static OmegaObject3D CreateDrone()
    {
        var drone = KamikazeDrone.CreateKamikazeDrone(null!);
        drone.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth };
        drone.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        drone.ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 400f };
        return drone;
    }

    private static void MarkLazerHit(OmegaObject3D drone)
    {
        drone.ImpactStatus!.HasCrashed = true;
        drone.ImpactStatus.ObjectName = "Lazer";
    }
}
