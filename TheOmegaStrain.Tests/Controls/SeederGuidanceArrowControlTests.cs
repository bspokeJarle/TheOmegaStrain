using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Threading;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class SeederGuidanceArrowControlTests
{
    [TestInitialize]
    public void Setup()
    {
        ScreenSetup.Initialize(1500, 1024);
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ShipState = new ShipState();
        GameState.ShipState.ShipWorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
    }

    [TestMethod]
    public void MoveObject_PrioritizesSeeder_WhenSeederAndBomberExist()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // Seeder is farther away in XZ than bomber, but must still be prioritized.
        GameState.SurfaceState.AiObjects.Add(CreateAi("Seeder", 1000f, 0f, 0f, isActive: true));
        GameState.SurfaceState.AiObjects.Add(CreateAi("ZeppelinBomber", 0f, 0f, 100f, isActive: true));

        control.MoveObject(arrow, null, null);
        Thread.Sleep(20);
        control.MoveObject(arrow, null, null);

        // Bomber heading would keep arrow at ~90, while seeder heading drives it down toward 0.
        Assert.IsTrue(arrow.Rotation!.z < 85f,
            "Arrow should prioritize seeder while any seeder is alive.");
    }

    [TestMethod]
    public void MoveObject_FallsBackToBomber_WhenNoSeederOrMothershipOrActiveDrone()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // Bomber straight ahead in +Z from origin; heading should be +90.
        GameState.SurfaceState.AiObjects.Add(CreateAi("ZeppelinBomber", 0f, 0f, 1000f, isActive: true));
        // Inactive drone should not be considered.
        GameState.SurfaceState.AiObjects.Add(CreateAi("KamikazeDrone", 1000f, 0f, 0f, isActive: false));

        control.MoveObject(arrow, null, null);

        Assert.AreEqual(90f, arrow.Rotation!.z, 0.1f,
            "Arrow should point to bomber when it is the remaining objective enemy.");
    }

    [TestMethod]
    public void MoveObject_UsesRenderAlignedTargetX_WhenSeederIsStraightAhead()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        GameState.ShipState.ShipWorldPosition = new Vector3 { x = 1000f, y = 0f, z = 1000f };
        GameState.SurfaceState.AiObjects.Add(CreateAi("Seeder", 250f, 0f, 2000f, isActive: true));

        control.MoveObject(arrow, null, null);
        Thread.Sleep(40);
        control.MoveObject(arrow, null, null);

        Assert.AreEqual(90f, arrow.Rotation!.z, 0.1f,
            "A seeder rendered straight ahead should not inherit a false leftward heading from raw WorldPosition.x.");
    }

    [TestMethod]
    public void MoveObject_AnchorsArrowBelowGameOverlay_OnLowHeightScreen()
    {
        ScreenSetup.Initialize(1920, 1080);
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();
        arrow.ObjectOffsets!.y = -200f;

        control.MoveObject(arrow, null, null);

        float screenY = ScreenSetup.screenSizeY / 2f + arrow.ObjectOffsets.y;
        Assert.IsTrue(screenY >= GameOverlaySetup.GuidanceArrowMinimumScreenY,
            "Guidance arrow should stay below the top HUD overlay on lower-height screens.");
    }

    [TestMethod]
    public void MoveObject_KeepsPreferredArrowY_WhenAlreadyBelowGameOverlay()
    {
        ScreenSetup.Initialize(2256, 1504);
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();
        arrow.ObjectOffsets!.y = -200f;

        control.MoveObject(arrow, null, null);

        Assert.AreEqual(-200f, arrow.ObjectOffsets.y, 0.1f,
            "Guidance arrow should keep the scene's preferred placement when it is already below the HUD.");
    }

    [TestMethod]
    public void MoveObject_WhenSeederIsBeyondFourScreens_SnapsTargetToFourScreensInSameDirection()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // Ship is at world origin; seeder WorldPosition.x = 8250 means the
        // guidance target lands at x = 8250 + screenSizeX/2 = 9000 (= 6 screens
        // away on a 1500-wide screen), which exceeds the 4-screen pacing cap.
        var seeder = CreateAi("Seeder", 8250f, 0f, 0f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);

        control.MoveObject(arrow, null, null);

        // After snap the guidance target should sit at exactly 4 screens (= 6000)
        // along the same +X direction, so WorldPosition.x = 6000 - 750 = 5250.
        Assert.AreEqual(5250f, seeder.WorldPosition!.x, 0.1f,
            "Seeder beyond four screens should be snapped to four screens along the ship->seeder direction.");
        Assert.AreEqual(0f, seeder.WorldPosition.z, 0.1f,
            "Snap must preserve the original direction; z should not drift when seeder is on the +X axis.");
    }

    [TestMethod]
    public void MoveObject_WhenSeederIsWithinFourScreens_DoesNotMoveSeeder()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // Seeder at WorldPosition.x = 2000 gives a guidance target at 2750
        // (~1.83 screens), well inside the 4-screen cap. WorldPosition must
        // remain untouched.
        var seeder = CreateAi("Seeder", 2000f, 0f, 0f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);

        control.MoveObject(arrow, null, null);

        Assert.AreEqual(2000f, seeder.WorldPosition!.x, 0.1f,
            "Seeder within four screens should not be snapped.");
        Assert.AreEqual(0f, seeder.WorldPosition.z, 0.1f,
            "Seeder within four screens should not be snapped on z either.");
    }

    [TestMethod]
    public void MoveObject_WhenTargetFirstSeenNearbyThenDriftsTooFar_SnapsThatTarget()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        var seeder = CreateAi("Seeder", 2000f, 0f, 0f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);

        control.MoveObject(arrow, null, null);
        Assert.AreEqual(2000f, seeder.WorldPosition!.x, 0.1f,
            "A nearby target should not be moved just because the arrow pointed at it.");

        seeder.WorldPosition.x = 8250f;
        control.MoveObject(arrow, null, null);

        Assert.AreEqual(5250f, seeder.WorldPosition.x, 0.1f,
            "A target first seen nearby must still snap later if the arrow points at it beyond four screens.");
    }

    [TestMethod]
    public void MoveObject_SnapsEachTargetOnlyOnce()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // First lock-on: seeder is six screens away and gets snapped to four.
        var seeder = CreateAi("Seeder", 8250f, 0f, 0f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);
        control.MoveObject(arrow, null, null);
        Assert.AreEqual(5250f, seeder.WorldPosition!.x, 0.1f,
            "First lock-on should snap the seeder to four screens.");

        // Simulate something external moving the seeder back out beyond four
        // screens (e.g., AI movement). The arrow should no longer snap it
        // because the snap is a one-shot per ObjectId.
        seeder.WorldPosition.x = 8250f;
        control.MoveObject(arrow, null, null);

        Assert.AreEqual(8250f, seeder.WorldPosition.x, 0.1f,
            "Once snapped, the same target must not be snapped again even if it drifts beyond four screens.");
    }

    [TestMethod]
    public void MoveObject_WhenMultipleSeedersExist_SnapsOnlyTheTargetedSeeder()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        var targetedSeeder = CreateAi("Seeder", 8250f, 0f, 0f, isActive: true);
        var fartherSeeder = CreateAi("Seeder", 12000f, 0f, 0f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(targetedSeeder);
        GameState.SurfaceState.AiObjects.Add(fartherSeeder);

        control.MoveObject(arrow, null, null);

        Assert.AreEqual(5250f, targetedSeeder.WorldPosition!.x, 0.1f,
            "The seeder selected by the guidance arrow should be snapped to four screens.");
        Assert.AreEqual(12000f, fartherSeeder.WorldPosition!.x, 0.1f,
            "A non-targeted seeder must not be snapped just because it is also far away.");
    }

    [TestMethod]
    public void MoveObject_SnapsAlongDiagonalDirection_WhenSeederIsBeyondFourScreensDiagonally()
    {
        var control = new SeederGuidanceArrowControl();
        var arrow = CreateArrow();

        // Place a seeder along a +X/+Z diagonal so guidance target = (5300+750, 0, 5300) = (6050, 0, 5300).
        // Distance from origin = sqrt(6050^2 + 5300^2) = ~8042 (~5.36 screens), beyond the four-screen cap.
        var seeder = CreateAi("Seeder", 5300f, 0f, 5300f, isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);

        control.MoveObject(arrow, null, null);

        // The guidance position should now sit at exactly 6000 (= 4 screens) from origin along the same direction.
        float guidanceX = seeder.WorldPosition!.x + ScreenSetup.screenSizeX / 2f;
        float guidanceZ = seeder.WorldPosition.z;
        float distance = MathF.Sqrt(guidanceX * guidanceX + guidanceZ * guidanceZ);
        Assert.AreEqual(6000f, distance, 1.0f,
            "After snap the guidance target should sit at exactly four screens from the ship.");

        // Direction must be preserved: original guidance was (6050, 5300) with ratio x/z = 6050/5300 ~= 1.1415.
        // Snapped guidance must keep that same ratio.
        Assert.AreEqual(6050f / 5300f, guidanceX / guidanceZ, 0.001f,
            "Snap must preserve the original ship->seeder direction.");
    }

    private static _3dObject CreateArrow()
    {
        return new _3dObject
        {
            ObjectId = 1001,
            ObjectName = "SeederGuidanceArrow",
            Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0f, z = 90f },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            ImpactStatus = new ImpactStatus()
        };
    }

    private static _3dObject CreateAi(string name, float x, float y, float z, bool isActive)
    {
        return new _3dObject
        {
            ObjectId = ++GameState.ObjectIdCounter,
            ObjectName = name,
            IsActive = isActive,
            WorldPosition = new Vector3 { x = x, y = y, z = z },
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { HasExploded = false }
        };
    }
}
