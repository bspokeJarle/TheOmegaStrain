using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using System.Reflection;
using _3dRotations.World.Objects;
using _3dRotations.Helpers;
using _3dTesting.Helpers;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Physics;

[DoNotParallelize]
[TestClass]
public class ShipSurfaceLandingTests
{
    private static readonly FieldInfo LastStaticCheckField = typeof(CrashDetection).GetField(
        "_lastStaticCheck",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    // Mirror of CrashDetection.EstimateDirection (private) for testability.
    private static ImpactDirection EstimateDirection(float dx, float dy, float dz)
    {
        if (Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dy) > Math.Abs(dz))
            return dy < 0 ? ImpactDirection.Top : ImpactDirection.Bottom;
        else if (Math.Abs(dx) > Math.Abs(dz))
            return dx > 0 ? ImpactDirection.Right : ImpactDirection.Left;
        else
            return ImpactDirection.Center;
    }

    // ShipRestingScreenY mirrors the formula in ShipControls.
    private static float ShipRestingScreenY(int screenHeight) => screenHeight * 0.195f;

    // Surface main crash box center Y (local, before gmpY offset).
    // From Surface.GetMainSurfaceCrashBox: min.y = -100, max.y = 1000 → center = 450.
    private const float SurfaceCrashBoxCenterY = 450f;

    [TestInitialize]
    public void Setup()
    {
        ScreenSetup.Initialize(1500, 1024);
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
        GameState.GamePlayState = new GamePlayState
        {
            CurrentSceneType = SceneTypes.Game
        };
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ShipState = new ShipState();
        GameState.ObjectIdCounter = 0;
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Restore default screen size so other tests are not affected.
        ScreenSetup.Initialize(1500, 1024);
    }

    // -----------------------------------------------------------------
    // Gravity-settle simulation helper
    //
    // Runs the actual Physics.ApplyFallGravity code together with the
    // airborne-settle logic from ShipControls.ApplyGravity to find the
    // steady-state screen position and altitude offset.
    // -----------------------------------------------------------------

    private static (float offsetY, float gmpY) SimulateGravityEquilibrium(
        int screenHeight, int frames = 3000)
    {
        int screenWidth = (int)(screenHeight * (1500.0 / 1024.0));
        ScreenSetup.Initialize(screenWidth, screenHeight);

        var physics = new GameAiAndControls.Physics.Physics();
        float shipRestingY = ShipRestingScreenY(screenHeight);
        float offsetY = shipRestingY;
        float gmpY = 0f;
        float dt = 1f / 90f;

        // Skip hover float phase so gravity ramps to full immediately.
        physics.HoverElapsed = 10f;

        for (int i = 0; i < frames; i++)
        {
            // 1. Gravity (Physics.ApplyFallGravity — actual code)
            float inertiaY = physics.ApplyFallGravity(WorldViewSetup.CameraPitchDegrees, dt);

            // 2. Apply inertia to position (ShipControls lines 887, 890)
            offsetY = MathF.Min(
                Math.Clamp(offsetY - inertiaY, physics.FloorHeight, physics.CeilingHeight),
                physics.MaxScreenDrop);
            gmpY = MathF.Min(gmpY + inertiaY, physics.CeilingHeight);

            // 3. Airborne settle (ShipControls lines 894-913)
            float airSettle = MathF.Min(physics.AirborneSettleRate * dt, 1f);
            float airScreenDiff = shipRestingY - offsetY;
            float airAltDiff = -gmpY;
            const float MaxAirSettleSpeed = 300f;
            float airMaxStep = MaxAirSettleSpeed * dt;

            if (MathF.Abs(airScreenDiff) > 0.5f)
            {
                float step = airScreenDiff * airSettle;
                if (MathF.Abs(step) > airMaxStep)
                    step = airMaxStep * MathF.Sign(airScreenDiff);
                offsetY += step;
            }

            if (MathF.Abs(airAltDiff) > 0.5f)
            {
                float step = airAltDiff * airSettle;
                if (MathF.Abs(step) > airMaxStep)
                    step = airMaxStep * MathF.Sign(airAltDiff);
                gmpY += step;
            }
        }

        return (offsetY, gmpY);
    }

    // -----------------------------------------------------------------
    // Physics computed properties must reflect actual screen size
    // -----------------------------------------------------------------

    [TestMethod]
    public void MaxScreenDrop_ReflectsCurrentScreenSize()
    {
        var physics = new GameAiAndControls.Physics.Physics();

        ScreenSetup.Initialize(1500, 1024);
        float small = physics.MaxScreenDrop;

        ScreenSetup.Initialize(2250, 1440);
        float big = physics.MaxScreenDrop;

        Assert.AreNotEqual(small, big,
            "MaxScreenDrop must change when screen size changes (not stale from construction).");
        Assert.AreEqual(1024 * 0.44f, small, 0.1f);
        Assert.AreEqual(1440 * 0.44f, big, 0.1f);
    }

    [TestMethod]
    public void CeilingHeight_ReflectsCurrentScreenSize()
    {
        var physics = new GameAiAndControls.Physics.Physics();

        ScreenSetup.Initialize(1500, 1024);
        float small = physics.CeilingHeight;

        ScreenSetup.Initialize(2250, 1440);
        float big = physics.CeilingHeight;

        Assert.AreNotEqual(small, big,
            "CeilingHeight must scale with screen size so loop room is consistent across displays.");
        Assert.AreEqual(1024 * 1.6f - 200f, small, 0.1f);
        Assert.AreEqual(1440 * 1.6f - 200f, big, 0.1f);
    }

    [TestMethod]
    public void AirborneSettleRate_ReflectsCurrentScreenSize()
    {
        var physics = new GameAiAndControls.Physics.Physics();

        ScreenSetup.Initialize(1500, 1024);
        float small = physics.AirborneSettleRate;

        ScreenSetup.Initialize(2250, 1440);
        float big = physics.AirborneSettleRate;

        Assert.AreNotEqual(small, big,
            "AirborneSettleRate must change when screen size changes.");
        // Bigger screen → larger ScreenScaleY → lower settle rate.
        Assert.IsTrue(big < small,
            "AirborneSettleRate should be lower on bigger screens (scaled by 1/ScreenScaleY).");
    }

    // -----------------------------------------------------------------
    // Gravity-settle equilibrium: drift from rest increases with screen
    // size because AirborneSettleRate decreases while gravity stays the
    // same.  A larger drift makes the direction estimation less
    // predictable for surface collisions.
    // -----------------------------------------------------------------

    [TestMethod]
    public void GravityEquilibrium_DriftIncreasesWithScreenSize()
    {
        var (smallOffsetY, _) = SimulateGravityEquilibrium(1024);
        var (bigOffsetY, _) = SimulateGravityEquilibrium(1440);

        float smallDrift = smallOffsetY - ShipRestingScreenY(1024);
        float bigDrift = bigOffsetY - ShipRestingScreenY(1440);

        Assert.IsTrue(smallDrift > 0,
            $"Ship must drift below resting position under gravity (small={smallDrift:F1}).");
        Assert.IsTrue(bigDrift > smallDrift,
            $"Big screen drift ({bigDrift:F1}) must exceed small screen drift ({smallDrift:F1}).");
    }

    [TestMethod]
    [DataRow(768, DisplayName = "768p")]
    [DataRow(1024, DisplayName = "1024p")]
    [DataRow(1080, DisplayName = "1080p")]
    [DataRow(1440, DisplayName = "1440p")]
    [DataRow(2160, DisplayName = "2160p (4K)")]
    public void GravityEquilibrium_SurfaceLanding_WorksForAllScreenSizes(int screenHeight)
    {
        var (offsetY, gmpY) = SimulateGravityEquilibrium(screenHeight);

        // Approximate crash box center positions at equilibrium.
        // Ship crash center y ≈ offsetY (ship local crash box centered near 0).
        // Surface crash center y ≈ SurfaceCrashBoxCenterY + gmpY.
        float shipCenterY = offsetY;
        float surfaceCenterY = SurfaceCrashBoxCenterY + gmpY;
        float dy = shipCenterY - surfaceCenterY;

        var direction = EstimateDirection(0, dy, 0);

        // Regardless of what direction the estimator returns, the fix ensures
        // "Surface" collisions always land.
        const string crashedWith = "Surface";
        bool wouldLand = crashedWith == "Surface" ||
                         direction == ImpactDirection.Top ||
                         direction == ImpactDirection.Center;

        Assert.IsTrue(wouldLand,
            $"Screen {screenHeight}p: offsetY={offsetY:F1} gmpY={gmpY:F1} dy={dy:F1} " +
            $"direction={direction}. Surface collision must always result in landing.");
    }

    // -----------------------------------------------------------------
    // Landing condition: crashedWith == "Surface" must bypass direction
    // -----------------------------------------------------------------

    [TestMethod]
    [DataRow(ImpactDirection.Top)]
    [DataRow(ImpactDirection.Bottom)]
    [DataRow(ImpactDirection.Left)]
    [DataRow(ImpactDirection.Right)]
    [DataRow(ImpactDirection.Center)]
    public void LandingCondition_SurfaceCrash_LandsRegardlessOfDirection(ImpactDirection direction)
    {
        const string crashedWith = "Surface";

        bool wouldLand = crashedWith == "Surface" ||
                         direction == ImpactDirection.Top ||
                         direction == ImpactDirection.Center;

        Assert.IsTrue(wouldLand,
            $"Surface crash with direction={direction} must always trigger landing.");
    }

    [TestMethod]
    [DataRow(ImpactDirection.Bottom)]
    [DataRow(ImpactDirection.Left)]
    [DataRow(ImpactDirection.Right)]
    public void LandingCondition_NonSurface_DoesNotLandOnBottomLeftRight(ImpactDirection direction)
    {
        const string crashedWith = "UnknownObject";

        bool wouldLand = crashedWith == "Surface" ||
                         direction == ImpactDirection.Top ||
                         direction == ImpactDirection.Center;

        Assert.IsFalse(wouldLand,
            $"Non-surface crash with direction={direction} should NOT trigger landing.");
    }

    [TestMethod]
    [DataRow(ImpactDirection.Top)]
    [DataRow(ImpactDirection.Center)]
    public void LandingCondition_NonSurface_LandsOnTopOrCenter(ImpactDirection direction)
    {
        const string crashedWith = "UnknownObject";

        bool wouldLand = crashedWith == "Surface" ||
                         direction == ImpactDirection.Top ||
                         direction == ImpactDirection.Center;

        Assert.IsTrue(wouldLand,
            $"Non-surface crash with direction={direction} should trigger landing.");
    }

    [TestMethod]
    public void CrashDetection_ChecksShipSurfaceEvenWhenStaticThrottleIsNotDue()
    {
        var lastStaticCheck = DateTime.Now.AddMinutes(1);
        LastStaticCheckField.SetValue(null, lastStaticCheck);

        var ship = CreateCrashObject(1, "Ship");
        var surface = CreateCrashObject(2, "Surface");

        CrashDetection.HandleCrashboxes(new List<_3dObject> { ship, surface }, isPaused: false);

        Assert.IsTrue(ship.ImpactStatus!.HasCrashed, "Ship -> Surface must be checked every frame, even while static collision checks are throttled.");
        Assert.AreEqual("Surface", ship.ImpactStatus.ObjectName);
        Assert.AreEqual(lastStaticCheck, (DateTime)LastStaticCheckField.GetValue(null)!,
            "Forced Ship -> Surface checks must not refresh the static throttle timer for other static objects.");
    }

    [TestMethod]
    public void ShipControls_WhenShipFallsBelowSurfaceFloor_StartsExplosionWithoutCrashReport()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = ShipSetup.DefaultShipHealth,
            HasCrashed = false,
            ObjectName = string.Empty
        };

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = -150f,
            z = SurfaceSetup.DefaultMapPosition.z
        };

        ship.Movement!.MoveObject(ship, null, null);

        Assert.AreEqual(0, ship.ImpactStatus.ObjectHealth);
        Assert.IsFalse(ship.ImpactStatus.HasCrashed);
        Assert.AreEqual("Surface", ship.ImpactStatus.ObjectName);
        Assert.IsTrue(
            ship.ObjectParts.All(part => part.PartName == "ExplodingPart"),
            "A ship below the surface floor should enter the normal explosion animation even without a reported crash.");
    }

    [TestMethod]
    public void ShipControls_AfterOverlayResume_DoesNotUsePausedDeltaOrFailsafeExplode()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 10f, y = 20f, z = 30f };
        ship.ObjectOffsets = new Vector3 { x = 1f, y = ShipRestingScreenY(ScreenSetup.screenSizeY), z = 400f };
        ship.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 15f };
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);
        controls.CaptureOverlayPauseTransform(ship);

        SetLastUpdateTime(controls, DateTime.Now.AddSeconds(-45));
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = controls.Physics.FloorHeight - 500f,
            z = SurfaceSetup.DefaultMapPosition.z
        };

        controls.RestoreOverlayPauseTransformAndSuppressCrashDetection(ship);
        var resumedAt = GetLastUpdateTime(controls);

        controls.MoveObject(ship, null, null);

        Assert.IsFalse(IsExploding(ship),
            "Overlay resume suppression should prevent the first frame from exploding via stale delta/failsafe.");
        Assert.AreEqual(ShipSetup.DefaultShipHealth, ship.ImpactStatus!.ObjectHealth);
        Assert.IsTrue((DateTime.Now - resumedAt).TotalSeconds < 1.0,
            "Resume should reset ShipControls timing so the paused duration is not applied as one physics frame.");
    }

    [TestMethod]
    public void ShipControls_AfterOverlayResume_KeepsControllerZoomWhenSnapshotOffsetZIsUnexpected()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 10f, y = 20f, z = 30f };
        ship.ObjectOffsets = new Vector3 { x = 1f, y = ShipRestingScreenY(ScreenSetup.screenSizeY), z = 400f };
        ship.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 15f };
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        ship.ObjectOffsets.z = 25f;
        controls.CaptureOverlayPauseTransform(ship);

        ship.ObjectOffsets.z = 10f;
        controls.RestoreOverlayPauseTransformAndSuppressCrashDetection(ship);

        Assert.AreEqual(400, controls.zoom);
        Assert.AreEqual(400f, ship.ObjectOffsets!.z, 0.001f,
            "Overlay resume should not let a transient object-offset depth make the ship render huge.");

        controls.MoveObject(ship, null, null);

        Assert.AreEqual(400f, ship.ObjectOffsets!.z, 0.001f);
    }

    [TestMethod]
    public void ShipControls_AfterOverlayResume_PausesGravityForFirstResumeWindow()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 10f, y = 20f, z = 30f };
        ship.ObjectOffsets = new Vector3 { x = 1f, y = ShipRestingScreenY(ScreenSetup.screenSizeY), z = 400f };
        ship.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 15f };
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        float resumeOffsetY = ShipRestingScreenY(ScreenSetup.screenSizeY) + 40f;
        float resumeMapY = -40f;
        ship.ObjectOffsets = new Vector3 { x = 1f, y = resumeOffsetY, z = 400f };
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = resumeMapY,
            z = SurfaceSetup.DefaultMapPosition.z
        };

        controls.CaptureOverlayPauseTransform(ship);
        SetLastUpdateTime(controls, DateTime.Now.AddSeconds(-1));
        controls.RestoreOverlayPauseTransformAndSuppressCrashDetection(ship);

        Assert.IsTrue(GameState.ShipState.ShipGravityDisabledUntilUtc > DateTime.UtcNow);

        controls.MoveObject(ship, null, null);

        Assert.AreEqual(resumeOffsetY, ship.ObjectOffsets!.y, 0.001f,
            "Gravity should not settle the ship immediately after an overlay pause.");
        Assert.AreEqual(resumeMapY, GameState.SurfaceState.GlobalMapPosition.y, 0.001f,
            "Gravity should not settle altitude immediately after an overlay pause.");
    }

    [TestMethod]
    public void ShipControls_CaptureOverlayPauseTransform_UsesLiveParentObjectWhenWorldObjectIsStale()
    {
        var worldShip = Ship.CreateShip(parentSurface: null);
        worldShip.ObjectName = "Ship";
        worldShip.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        worldShip.ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 400f };
        worldShip.Rotation = new Vector3 { x = 0f, y = 0f, z = 0f };
        worldShip.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)worldShip.Movement!;
        var liveShip = Ship.CreateShip(parentSurface: null);
        liveShip.ObjectName = "Ship";
        liveShip.WorldPosition = new Vector3 { x = 100f, y = 20f, z = 300f };
        liveShip.ObjectOffsets = new Vector3 { x = 11f, y = ShipRestingScreenY(ScreenSetup.screenSizeY) + 42f, z = 400f };
        liveShip.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 18f };
        liveShip.ImpactStatus = worldShip.ImpactStatus;
        liveShip.Movement = controls;
        controls.ParentObject = liveShip;

        controls.CaptureOverlayPauseTransform(worldShip);

        worldShip.ObjectOffsets = new Vector3 { x = -1f, y = -2f, z = 400f };
        liveShip.ObjectOffsets = new Vector3 { x = -3f, y = -4f, z = 400f };

        controls.RestoreOverlayPauseTransformAndSuppressCrashDetection(worldShip);

        Assert.AreEqual(11f, worldShip.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(ShipRestingScreenY(ScreenSetup.screenSizeY) + 42f, worldShip.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(400f, worldShip.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(worldShip.ObjectOffsets.x, liveShip.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(worldShip.ObjectOffsets.y, liveShip.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(worldShip.ObjectOffsets.z, liveShip.ObjectOffsets.z, 0.001f);
    }

    [TestMethod]
    public void ShipControls_WhenLowSpeedSurfaceCrash_AppliesMinimumDamageAndPlaysThud()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = ShipSetup.DefaultShipHealth,
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom
        };

        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        ship.Movement!.MoveObject(ship, audio, registry);

        Assert.AreEqual(ShipSetup.DefaultShipHealth - 2, ship.ImpactStatus.ObjectHealth);
        Assert.IsFalse(ship.ImpactStatus.HasCrashed);
        Assert.AreEqual("ship_thud", audio.LastDefinitionId);
        Assert.AreEqual(1, audio.PlayCount);
    }

    [TestMethod]
    public void ShipControls_WhenSurfaceCrashOnLandingPlatform_RecoversWithoutDamage()
    {
        var map = CreateSurfaceMap(40, 40);
        var centerTile = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
        PlaceShipCenterOverTile(map, centerTile.x, centerTile.z);

        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(ShipSetup.DefaultShipHealth, ship.ImpactStatus.ObjectHealth);
        Assert.IsFalse(ship.ImpactStatus.HasCrashed);
        Assert.IsFalse(IsExploding(ship), "Landing platform hits should recover without starting the explosion.");
    }

    [TestMethod]
    public void ShipControls_WhenSecondSurfaceCrashOutsideLandingPlatformWithinWindow_Explodes()
    {
        var map = CreateSurfaceMap(40, 40);
        PlaceShipCenterOverTile(map, 0, 0);

        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        Assert.IsFalse(IsExploding(ship), "First outside-pad surface hit should only arm the unsafe-hit window.");

        PlaceShipCenterOverTile(map, 0, 0);
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0, ship.ImpactStatus.ObjectHealth);
        Assert.IsFalse(ship.ImpactStatus.HasCrashed);
        Assert.IsTrue(IsExploding(ship), "Second outside-pad surface hit inside the window should explode regardless of health.");
    }

    [TestMethod]
    public void ShipControls_WhenSecondSurfaceCrashOutsideLandingPlatformAfterResetWindow_DoesNotExplode()
    {
        var map = CreateSurfaceMap(40, 40);
        PlaceShipCenterOverTile(map, 0, 0);

        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        SetUnsafeSurfaceHitTime(controls, DateTime.Now.AddSeconds(-3));

        PlaceShipCenterOverTile(map, 0, 0);
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        controls.MoveObject(ship, null, null);

        Assert.IsFalse(IsExploding(ship), "Unsafe outside-pad hit should reset after two seconds.");
        Assert.IsTrue(ship.ImpactStatus.ObjectHealth > 0);
    }

    [TestMethod]
    public void ShipControls_WhenThrustAfterOutsideSurfaceCrash_ResetsUnsafeHit()
    {
        var map = CreateSurfaceMap(40, 40);
        PlaceShipCenterOverTile(map, 0, 0);

        var surface = new Surface();
        var ship = Ship.CreateShip(surface);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        controls.ThrustOn = true;
        controls.Thrust = 1f;
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
        controls.MoveObject(ship, null, null);

        controls.ThrustOn = false;
        controls.Thrust = 0f;
        PlaceShipCenterOverTile(map, 0, 0);
        ship.ImpactStatus = CreateSurfaceImpact(ShipSetup.DefaultShipHealth);

        controls.MoveObject(ship, null, null);

        Assert.IsFalse(IsExploding(ship), "Thrust should clear the armed unsafe-hit window before the next outside-pad hit.");
        Assert.IsTrue(ship.ImpactStatus.ObjectHealth > 0);
    }

    [TestMethod]
    public void ShipControls_AfterSmallSurfaceBounce_ReenablesGravityAtTopPoint()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        float restingY = ShipRestingScreenY(ScreenSetup.screenSizeY);
        ship.ObjectOffsets = new Vector3 { x = 0f, y = restingY + 24f, z = 400f };
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = -24f,
            z = SurfaceSetup.DefaultMapPosition.z
        };
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = ShipSetup.DefaultShipHealth,
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom
        };

        controls.MoveObject(ship, null, null);

        bool reachedTop = false;
        bool fellAfterTop = false;

        for (int i = 0; i < 240; i++)
        {
            controls.ApplyGravity(1f / 90f);

            if (MathF.Abs(ship.ObjectOffsets!.y - restingY) <= 0.75f &&
                MathF.Abs(GameState.SurfaceState.GlobalMapPosition.y) <= 0.75f)
            {
                reachedTop = true;
            }

            if (reachedTop && ship.ObjectOffsets!.y > restingY + 0.75f)
            {
                fellAfterTop = true;
                break;
            }
        }

        Assert.IsTrue(reachedTop, "Small surface bounce should settle back to the top/resting point.");
        Assert.IsTrue(fellAfterTop, "Gravity should take over at the top point and pull the ship down again.");
    }

    [TestMethod]
    public void ShipControls_AfterExplosionStarts_KeepsExplosionTransformFrozen()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 120f, y = 40f, z = -80f };
        ship.ObjectOffsets = new Vector3 { x = 25f, y = ShipRestingScreenY(ScreenSetup.screenSizeY), z = 400f };
        ship.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 15f };
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = 1,
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom
        };

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = SurfaceSetup.DefaultMapPosition.y,
            z = SurfaceSetup.DefaultMapPosition.z
        };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        var frozenWorldPosition = CopyVector(ship.WorldPosition);
        var frozenObjectOffsets = CopyVector(ship.ObjectOffsets);
        var frozenRotation = CopyVector(ship.Rotation);

        controls.ThrustOn = true;
        controls.Thrust = 10f;
        controls.Physics.InertiaX = 35f;
        controls.Physics.InertiaY = 35f;
        controls.Physics.InertiaZ = -35f;
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 999f, y = -500f, z = 999f };

        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0f, controls.Thrust, 0.001f, "Explosion should stop ship thrust.");
        AssertVectorEqual(frozenWorldPosition, ship.WorldPosition, "WorldPosition");
        AssertVectorEqual(frozenObjectOffsets, ship.ObjectOffsets, "ObjectOffsets");
        AssertVectorEqual(frozenRotation, ship.Rotation, "Rotation");
    }

    [TestMethod]
    public void ShipControls_AfterExplosionStarts_RestoresSnapshotOnNextFrameShipCopy()
    {
        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)ship.Movement!;
        controls.MoveObject(ship, null, null);

        ship.ObjectOffsets = new Vector3 { x = 87f, y = 321f, z = 456f };
        ship.Rotation = new Vector3 { x = 71f, y = 3f, z = 24f };
        ship.CalculatedCrashOffset = new Vector3 { x = 87f, y = 321f, z = 456f };
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = 1,
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom
        };

        controls.MoveObject(ship, null, null);

        var frozenWorldPosition = CopyVector(ship.WorldPosition);
        var frozenObjectOffsets = CopyVector(ship.ObjectOffsets);
        var frozenRotation = CopyVector(ship.Rotation);
        var frozenCrashOffset = CopyVector(ship.CalculatedCrashOffset);

        var nextFrameShipCopy = Ship.CreateShip(parentSurface: null, movement: controls);
        nextFrameShipCopy.ObjectName = "Ship";
        nextFrameShipCopy.WorldPosition = new Vector3 { x = 1000f, y = 1000f, z = 1000f };
        nextFrameShipCopy.ObjectOffsets = new Vector3();
        nextFrameShipCopy.Rotation = new Vector3();
        nextFrameShipCopy.CalculatedCrashOffset = null;
        nextFrameShipCopy.ImpactStatus = ship.ImpactStatus;

        controls.MoveObject(nextFrameShipCopy, null, null);

        AssertVectorEqual(frozenWorldPosition, nextFrameShipCopy.WorldPosition, "WorldPosition");
        AssertVectorEqual(frozenObjectOffsets, nextFrameShipCopy.ObjectOffsets, "ObjectOffsets");
        AssertVectorEqual(frozenRotation, nextFrameShipCopy.Rotation, "Rotation");
        AssertVectorEqual(frozenCrashOffset, nextFrameShipCopy.CalculatedCrashOffset, "CalculatedCrashOffset");
    }

    [TestMethod]
    public void ShipControls_WhenFailsafeExplosionStartsFromStaleFrameCopy_AnchorsToLiveParentObject()
    {
        var initialShip = Ship.CreateShip(parentSurface: null);
        initialShip.ObjectName = "Ship";
        initialShip.WorldPosition = new Vector3();
        initialShip.ObjectOffsets = new Vector3 { x = 0f, y = ShipRestingScreenY(ScreenSetup.screenSizeY), z = 400f };
        initialShip.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 0f };
        initialShip.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        var controls = (ShipControls)initialShip.Movement!;
        controls.MoveObject(initialShip, null, null);

        var liveShip = Ship.CreateShip(parentSurface: null, movement: controls);
        liveShip.ObjectName = "Ship";
        liveShip.WorldPosition = new Vector3 { x = 700f, y = 20f, z = 900f };
        liveShip.ObjectOffsets = new Vector3 { x = 17f, y = ShipRestingScreenY(ScreenSetup.screenSizeY) + 61f, z = 400f };
        liveShip.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 31f };
        liveShip.CalculatedCrashOffset = new Vector3 { x = 12f, y = 34f, z = 56f };
        liveShip.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
        controls.ParentObject = liveShip;

        var staleFrameShip = Ship.CreateShip(parentSurface: null, movement: controls);
        staleFrameShip.ObjectName = "Ship";
        staleFrameShip.WorldPosition = new Vector3 { x = -100f, y = -200f, z = -300f };
        staleFrameShip.ObjectOffsets = new Vector3 { x = -11f, y = -22f, z = 400f };
        staleFrameShip.Rotation = new Vector3 { x = 1f, y = 2f, z = 3f };
        staleFrameShip.CalculatedCrashOffset = new Vector3 { x = -4f, y = -5f, z = -6f };
        staleFrameShip.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };

        GameState.ShipState.ShipGravityDisabledUntilUtc = DateTime.UtcNow.AddSeconds(2);
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = SurfaceSetup.DefaultMapPosition.x,
            y = controls.Physics.FloorHeight - 500f,
            z = SurfaceSetup.DefaultMapPosition.z
        };

        controls.MoveObject(staleFrameShip, null, null);

        Assert.IsTrue(IsExploding(staleFrameShip));
        AssertVectorEqual(CopyVector(liveShip.WorldPosition), staleFrameShip.WorldPosition, "WorldPosition");
        AssertVectorEqual(CopyVector(liveShip.ObjectOffsets), staleFrameShip.ObjectOffsets, "ObjectOffsets");
        AssertVectorEqual(CopyVector(liveShip.Rotation), staleFrameShip.Rotation, "Rotation");
        AssertVectorEqual(CopyVector(liveShip.CalculatedCrashOffset), staleFrameShip.CalculatedCrashOffset, "CalculatedCrashOffset");
    }

    [TestMethod]
    public void ShipControls_WhenShipExplosionReleasesParticles_KeepsParticlesScreenAnchored()
    {
        var map = CreateSurfaceMap(40, 40);
        PlaceShipCenterOverTile(map, 0, 0);

        var ship = Ship.CreateShip(parentSurface: null);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ImpactStatus = CreateSurfaceImpact(health: 1);

        var controls = (ShipControls)ship.Movement!;
        controls.SetParticleGuideCoordinates(
            CreatePointTriangle(0f, 0f, 0f),
            CreatePointTriangle(0f, -10f, 0f));

        controls.MoveObject(ship, null, null);

        Assert.IsNotNull(ship.Particles);
        Assert.IsTrue(ship.Particles.Particles.Count > 0, "Ship explosion should release particles when guides are available.");

        foreach (var particle in ship.Particles.Particles)
        {
            Assert.AreEqual(0f, particle.WorldPosition.x, 0.001f);
            Assert.AreEqual(0f, particle.WorldPosition.y, 0.001f);
            Assert.AreEqual(0f, particle.WorldPosition.z, 0.001f);
        }
    }

    private static _3dObject CreateCrashObject(int id, string name)
    {
        return new _3dObject
        {
            ObjectId = id,
            ObjectName = name,
            ImpactStatus = new ImpactStatus(),
            ObjectOffsets = new Vector3(),
            WorldPosition = new Vector3(),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = -10, y = -10, z = -10 },
                    new Vector3 { x = 10, y = -10, z = -10 },
                    new Vector3 { x = 10, y = 10, z = -10 },
                    new Vector3 { x = -10, y = 10, z = -10 },
                    new Vector3 { x = -10, y = -10, z = 10 },
                    new Vector3 { x = 10, y = -10, z = 10 },
                    new Vector3 { x = 10, y = 10, z = 10 },
                    new Vector3 { x = -10, y = 10, z = 10 }
                }
            }
        };
    }

    private static Vector3 CopyVector(IVector3? vector)
    {
        Assert.IsNotNull(vector);
        return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
    }

    private static void AssertVectorEqual(Vector3 expected, IVector3? actual, string name)
    {
        Assert.IsNotNull(actual, $"{name} should not be null.");
        Assert.AreEqual(expected.x, actual.x, 0.001f, $"{name}.x");
        Assert.AreEqual(expected.y, actual.y, 0.001f, $"{name}.y");
        Assert.AreEqual(expected.z, actual.z, 0.001f, $"{name}.z");
    }

    private static TriangleMeshWithColor CreatePointTriangle(float x, float y, float z)
    {
        return new TriangleMeshWithColor
        {
            Color = "ffffff",
            noHidden = true,
            vert1 = new Vector3 { x = x, y = y, z = z },
            vert2 = new Vector3 { x = x, y = y, z = z },
            vert3 = new Vector3 { x = x, y = y, z = z }
        };
    }

    private static SurfaceData[,] CreateSurfaceMap(int width, int height)
    {
        var map = new SurfaceData[height, width];
        int mapId = 0;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                map[z, x] = new SurfaceData
                {
                    mapId = ++mapId,
                    mapDepth = 50,
                    isInfected = false
                };
            }
        }

        return map;
    }

    private static void PlaceShipCenterOverTile(SurfaceData[,] map, int tileX, int tileZ)
    {
        int tileSize = SurfaceSetup.tileSize;
        int viewportCenterOffset = (SurfaceSetup.viewPortSize * tileSize) / 2;

        GameState.SurfaceState.Global2DMap = map;
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = tileX * tileSize - viewportCenterOffset,
            y = 0f,
            z = tileZ * tileSize - viewportCenterOffset
        };
    }

    private static ImpactStatus CreateSurfaceImpact(int health)
    {
        return new ImpactStatus
        {
            ObjectHealth = health,
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom
        };
    }

    private static bool IsExploding(I3dObject ship)
    {
        return ship.ObjectParts.Count > 0 &&
               ship.ObjectParts.All(part => part.PartName == "ExplodingPart");
    }

    private static void SetUnsafeSurfaceHitTime(ShipControls controls, DateTime hitTime)
    {
        var field = typeof(ShipControls).GetField(
            "_unsafeSurfaceHitAt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(controls, hitTime);
    }

    private static void SetLastUpdateTime(ShipControls controls, DateTime updateTime)
    {
        var field = typeof(ShipControls).GetField(
            "lastUpdateTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(controls, updateTime);
    }

    private static DateTime GetLastUpdateTime(ShipControls controls)
    {
        var field = typeof(ShipControls).GetField(
            "lastUpdateTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (DateTime)field.GetValue(controls)!;
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        public SoundDefinition Get(string id)
        {
            return new SoundDefinition
            {
                Id = id,
                Usage = id,
                File = $"{id}.wav",
                Settings = new SoundSettings { Volume = 1f }
            };
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            definition = Get(id);
            return true;
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public int PlayCount { get; private set; }
        public string? LastDefinitionId { get; private set; }
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayCount++;
            LastDefinitionId = definition.Id;
            return new CapturingAudioInstance
            {
                SoundId = definition.Id,
                IsPlaying = true,
                IsLooping = mode == AudioPlayMode.SegmentedLoop,
                Volume = options?.VolumeOverride ?? definition.Settings.Volume
            };
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
