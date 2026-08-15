using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Wpf.Rendering;
using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.World.Objects.EarthObject;
using TheOmegaStrain.Game.Scenes.Outro;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.IO;
using System.Windows.Media;
using NumericsVector3 = System.Numerics.Vector3;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class OutroSceneTests
{
    private const int ExpectedOutroStarCount = 250;
    private const float ExpectedStarFieldRadius = 560f;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WorldFade = new WorldFadeState();
        GameState.ObjectIdCounter = 0;
        GameState.DeltaTime = 0f;
    }

    private static GameWorld CreateWorldAtOutro()
    {
        GameState.GamePlayState.SceneIndex = 9;
        var handler = new SceneHandler();
        var world = new GameWorld();
        world.SceneHandler = handler;
        world.WorldInhabitants.Clear();
        handler.SetupActiveScene(world);
        return world;
    }

    [TestMethod]
    public void OutroScene_IsActiveAtIndexNine()
    {
        GameState.GamePlayState.SceneIndex = 9;
        var handler = new SceneHandler();
        var scene = handler.GetActiveScene();
        Assert.AreEqual(SceneTypes.Outro, scene.SceneType, "Scene at index 9 should be Outro.");
    }

    [TestMethod]
    public void OutroScene_HasEarthObject()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "Earth");
        Assert.IsNotNull(earth, "Outro should contain an Earth object.");
    }

    [TestMethod]
    public void OutroScene_EarthIsActive()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        Assert.IsTrue(earth.IsActive, "Earth should be active in the Outro.");
    }

    [TestMethod]
    public void OutroScene_EarthHasMovementController()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        Assert.IsNotNull(earth.Movement, "Earth should have a movement controller assigned.");
    }

    [TestMethod]
    public void OutroScene_EarthHasCrashBoxes()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        Assert.IsTrue(earth.CrashBoxes?.Count > 0, "Earth should have crash boxes.");
    }

    [TestMethod]
    public void OutroScene_EarthGlobePart_IsVisible()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var globe = earth.ObjectParts.FirstOrDefault(p => p.PartName == "EarthGlobe");

        Assert.IsNotNull(globe, "Earth should contain an EarthGlobe part.");
        Assert.IsTrue(globe.IsVisible, "EarthGlobe must be visible or the renderer skips all Earth triangles.");
    }

    [TestMethod]
    public void OutroScene_EarthGlobeTriangleCount_UsesCompleteGlbModel()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var globe = earth.ObjectParts.FirstOrDefault(p => p.PartName == "EarthGlobe");

        Assert.IsNotNull(globe, "Earth should contain the imported GLB model as EarthGlobe.");
        Assert.AreEqual(EarthModelData.TriangleCount, globe.Triangles.Count,
            "Earth should use the complete baked low_poly_earth.glb triangle set.");
        Assert.AreEqual(1280, EarthModelData.TriangleCount, "The imported source model should keep all GLB triangles.");
        Assert.AreEqual(642, EarthModelData.SourceVertexCount, "The imported source model should keep the GLB vertex count metadata.");
    }

    [TestMethod]
    public void OutroScene_EarthGlobeTriangles_UseBackfaceCulling()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var globe = earth.ObjectParts.FirstOrDefault(p => p.PartName == "EarthGlobe");

        Assert.IsNotNull(globe, "Earth should contain an EarthGlobe part.");
        Assert.IsTrue(globe.Triangles.All(t => t.noHidden != true),
            "Outro Earth must use backface culling; rendering every hidden sphere face is too expensive.");
    }

    [TestMethod]
    public void OutroScene_EarthGlobePalette_UsesDarkOceanGreenTerrainAndGrayMountains()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var colors = earth.ObjectParts
            .Where(p => p.PartName == "EarthGlobe")
            .SelectMany(p => p.Triangles)
            .Select(t => ParseColor(t.Color))
            .ToList();

        var blueColors = colors.Where(IsBlueDominant).Distinct().ToList();
        var greenTerrainColors = colors.Where(IsLandColor).Distinct().ToList();
        var mountainColors = colors.Where(IsMountainGray).Distinct().OrderBy(c => c.R).ToList();

        Assert.AreEqual(1, blueColors.Count, "Only the ocean should use blue in the Earth model.");
        Assert.IsTrue(IsDarkOceanColor(blueColors[0]), "The single ocean model color should be dark blue.");
        Assert.IsTrue(greenTerrainColors.Count >= 2,
            $"Mid terrain should include green height variation. Green color count was {greenTerrainColors.Count}.");
        Assert.IsTrue(mountainColors.Count >= 2,
            $"High terrain should include multiple gray levels. Gray color count was {mountainColors.Count}.");
        Assert.IsTrue(mountainColors.Last().R > mountainColors.First().R,
            "Higher mountain levels should become lighter gray.");
        Assert.IsTrue(colors.Count(IsLandColor) > EarthModelData.TriangleCount * 0.15f,
            "Earth should contain broad readable land areas, not just scattered green specks.");
    }

    [TestMethod]
    public void OutroScene_StylizedEarthColor_UsesHeightBandsForTerrain()
    {
        var centralEurope = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(52f, 15f)));
        var scandinavia = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(67f, 21f)));
        var northAtlantic = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(40f, -35f)));
        var midTerrain = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(0f, -120f, 186f)));
        var lowMountain = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(0f, -120f, 188.5f)));
        var highMountain = ParseColor(EarthObject.GetStylizedEarthColor(CreateSpherePoint(0f, -120f, 190.2f)));

        Assert.IsTrue(centralEurope.G > centralEurope.R + 25 && centralEurope.G > centralEurope.B + 10,
            "Central Europe should read as land.");
        Assert.IsTrue(scandinavia.G > scandinavia.R + 20 && scandinavia.G > scandinavia.B,
            "Scandinavia/Nordic region should read as land.");
        Assert.IsTrue(IsDarkOceanColor(northAtlantic),
            "Open Atlantic should remain the fixed dark-blue ocean color.");
        Assert.IsTrue(IsLandColor(midTerrain),
            "Middle height terrain should be green, even outside the broad land masks.");
        Assert.IsTrue(IsMountainGray(lowMountain),
            "High terrain should become gray instead of blue.");
        Assert.IsTrue(IsMountainGray(highMountain) && highMountain.R > lowMountain.R,
            "The highest terrain should be lighter gray than lower mountains.");
    }

    [TestMethod]
    public void OutroScene_EarthContainsGlobeAndStarParts()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var globeParts = earth.ObjectParts.Where(p => p.PartName == "EarthGlobe").ToList();
        var starParts = earth.ObjectParts.Where(IsOutroStarPart).ToList();
        var unexpectedParts = earth.ObjectParts
            .Where(p => p.PartName != "EarthGlobe"
                     && !IsOutroStarPart(p)
                     && !IsMiniaturePart(p))
            .ToList();

        var miniatureParts = earth.ObjectParts.Where(IsMiniaturePart).ToList();

        Assert.AreEqual(1, globeParts.Count, "Earth should contain exactly one imported GLB globe part.");
        Assert.AreEqual(ExpectedOutroStarCount, starParts.Count, "Outro Earth should contain the requested star count.");
        Assert.IsTrue(miniatureParts.Count > 0, "Earth should contain surface miniatures (trees, houses, igloos).");
        Assert.AreEqual(0, unexpectedParts.Count,
            $"Earth contains unexpected parts. Found: {string.Join(", ", unexpectedParts.Select(p => p.PartName))}");
    }

    [TestMethod]
    public void OutroScene_EarthStarField_HasRequestedRadius()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var starCenters = earth.ObjectParts.Where(IsOutroStarPart).Select(GetPartCenter).ToList();

        Assert.AreEqual(ExpectedOutroStarCount, starCenters.Count);

        float averageRadius = starCenters.Average(v => MathF.Sqrt((v.x * v.x) + (v.y * v.y) + (v.z * v.z)));
        Assert.IsTrue(averageRadius >= ExpectedStarFieldRadius - 3f && averageRadius <= ExpectedStarFieldRadius + 3f,
            $"Star field should be roughly doubled to radius {ExpectedStarFieldRadius}. Average radius was {averageRadius:0.0}.");
    }

    [TestMethod]
    public void OutroScene_EarthStars_HaveVariedLocalRotations()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");

        var distinctAngles = earth.ObjectParts
            .Where(IsOutroStarPart)
            .Select(GetFirstTriangleBaseAngleBucket)
            .Distinct()
            .Count();

        Assert.IsTrue(distinctAngles > 50,
            $"Stars should have varied local rotations. Distinct angle buckets: {distinctAngles}.");
    }

    [TestMethod]
    public void OutroScene_EarthGlobeVisibleTriangles_AreCulledToRoughlyFrontHalf()
    {
        var world = CreateWorldAtOutro();
        var earth = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        ApplyLiveMeshRotation(earth);

        int triangleCount = earth.ObjectParts.Sum(p => p.Triangles.Count);
        var converter = OmegaPerspectiveProjectorFactory.Create();
        var projected = converter.ProjectToTriangles(new List<_3dObject> { earth }, currentFrame: 1);

        // Stars carry noHidden=true and always project; globe and miniatures use backface culling.
        int starTriangleCount = earth.ObjectParts.Where(IsOutroStarPart).Sum(p => p.Triangles.Count);
        int cullableCount = triangleCount - starTriangleCount;

        Assert.IsTrue(projected.Count > starTriangleCount + cullableCount * 0.25f,
            $"Earth should still have enough visible front-side triangles. Projected {projected.Count} of {triangleCount}.");
        Assert.IsTrue(projected.Count < starTriangleCount + cullableCount * 0.75f,
            $"Earth should cull hidden back-side triangles. Projected {projected.Count} of {triangleCount}.");
    }

    [TestMethod]
    public void OutroScene_EarthOffsets_AreOnScreen()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var offsets = earth.ObjectOffsets;

        Assert.IsNotNull(offsets, "Earth should have ObjectOffsets.");
        // Z offset should be positive (zoomed in, same layer as ship)
        Assert.IsTrue(offsets.z > 0, $"Earth Z offset should be positive (zoom), got {offsets.z}.");
        // X offset 0 = horizontally centered
        Assert.AreEqual(0f, offsets.x, $"Earth should be horizontally centered (x=0), got {offsets.x}.");
        Assert.AreEqual(0f, offsets.y, $"Earth should be vertically centered (y=0), got {offsets.y}.");
    }

    [TestMethod]
    public void OutroScene_EarthWorldPosition_IsAlwaysVisibleOrigin()
    {
        var world = CreateWorldAtOutro();
        var earth = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Earth");

        Assert.IsNotNull(earth.WorldPosition, "Earth must have a WorldPosition so the render loop can test visibility.");
        Assert.AreEqual(0f, earth.WorldPosition.x);
        Assert.AreEqual(0f, earth.WorldPosition.y);
        Assert.AreEqual(0f, earth.WorldPosition.z);
        Assert.IsTrue(earth.CheckInhabitantVisibility(), "Earth should render like Intro objects without needing a Surface.");
    }

    [TestMethod]
    public void OutroScene_HasShipWithOutroControls()
    {
        var world = CreateWorldAtOutro();
        var ship = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "Ship");

        Assert.IsNotNull(ship, "Outro should contain a Ship object.");
        Assert.IsInstanceOfType(ship.Movement, typeof(OutroShipControls),
            "Outro ship should use the cinematic controller, not player input controls.");
        Assert.IsInstanceOfType(ship.Particles, typeof(ParticlesAI),
            "Outro ship should keep the normal particle system for engine flames.");
        Assert.IsFalse(ship.HasShadow, "Space outro ship should not cast a terrain shadow.");
        Assert.IsTrue(ship.ObjectParts.Any(p => p.PartName == "JetMotor"), "Ship should keep the lower engine emitter.");
        Assert.IsTrue(ship.ObjectParts.Any(p => p.PartName == "RearEngine"), "Ship should keep the rear engine emitter.");
    }

    [TestMethod]
    public void OutroScene_HasTwoVisibleMovingAsteroidsWithTrailParticles()
    {
        var world = CreateWorldAtOutro();
        var asteroids = world.WorldInhabitants
            .Where(o => o.ObjectName == "Asteroid")
            .ToList();

        Assert.AreEqual(2, asteroids.Count, "Space outro should keep exactly two cinematic asteroids.");
        Assert.IsTrue(asteroids.All(o => o.Movement is AsteroidControls),
            "Outro asteroids should use the cinematic asteroid controller.");
        Assert.IsTrue(asteroids.All(o => o.Particles is ParticlesAI),
            "Outro asteroids should emit visible trail particles.");
        Assert.IsTrue(asteroids.All(o => o.ObjectOffsets!.z < 420f),
            "Outro asteroids should be close enough to read clearly over the Earth scene.");

        asteroids[0].Movement!.MoveObject(asteroids[0], null, null);
        asteroids[1].Movement!.MoveObject(asteroids[1], null, null);
        var initial1 = CopyVector(asteroids[0].ObjectOffsets!);
        var initial2 = CopyVector(asteroids[1].ObjectOffsets!);

        for (int frame = 0; frame < 5; frame++)
        {
            asteroids[0].Movement!.MoveObject(asteroids[0], null, null);
            asteroids[1].Movement!.MoveObject(asteroids[1], null, null);
        }

        Assert.IsTrue(asteroids[0].ObjectOffsets!.x > initial1.x && asteroids[0].ObjectOffsets.y > initial1.y,
            "First outro asteroid should cross down and right.");
        Assert.IsTrue(asteroids[1].ObjectOffsets!.x < initial2.x && Math.Abs(asteroids[1].ObjectOffsets.y - initial2.y) > 0.1f,
            "Second outro asteroid should cross left on a different diagonal.");
        Assert.IsTrue(asteroids[0].Particles!.Particles.Count > 0,
            "First outro asteroid should leave a particle trail.");
        Assert.IsTrue(asteroids[1].Particles!.Particles.Count > 0,
            "Second outro asteroid should leave a particle trail.");
    }

    [TestMethod]
    public void OutroShipControls_FirstMove_StartsShipFromRightSide()
    {
        var world = CreateWorldAtOutro();
        var ship = world.WorldInhabitants.First(o => o.ObjectName == "Ship");

        ship.Movement!.MoveObject(ship, null, null);

        Assert.IsTrue(ship.ObjectOffsets!.x > ScreenSetup.screenSizeX * 0.35f,
            $"Outro ship should enter from the right side. X offset was {ship.ObjectOffsets.x:0.0}.");
        Assert.IsTrue(ship.ObjectOffsets.x < ScreenSetup.screenSizeX * 0.5f,
            $"Outro ship should start like the Intro object: right side but already renderable. X offset was {ship.ObjectOffsets.x:0.0}.");
        Assert.IsTrue(ship.ObjectOffsets.z < 520f,
            $"Outro ship should start in front of Earth (depth < Earth's 520). Z offset was {ship.ObjectOffsets.z:0.0}.");
        Assert.AreEqual(WorldViewSetup.CameraPitchDegrees, ship.Rotation!.x, "Outro ship should keep the same camera tilt as the rest of the scene.");
    }

    [TestMethod]
    public void OutroShipControls_FirstMove_ProjectsVisibleShipTriangles()
    {
        var world = CreateWorldAtOutro();
        var ship = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Ship");

        ship.Movement!.MoveObject(ship, null, null);
        ApplyLiveMeshRotation(ship);

        var converter = OmegaPerspectiveProjectorFactory.Create();
        var projected = converter.ProjectToTriangles(new List<_3dObject> { ship }, currentFrame: 1);

        Assert.IsTrue(projected.Count > 0, "Outro ship should be visible on the first rendered movement frame.");
        Assert.IsTrue(projected.Any(t => t.PartName == "UpperPart" || t.PartName == "LowerPart" || t.PartName == "RearPart"),
            "Projected triangles should include the ship hull, not only helper effects.");
    }

    [TestMethod]
    public void OutroShipControls_ApproachPhase_FliesFromRightToCenterBeforeTurningAndDiving()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        float startX = ship.ObjectOffsets!.x;
        float startY = ship.ObjectOffsets.y;
        float startZ = ship.ObjectOffsets.z;

        now = now.AddSeconds(2.5);
        controls.MoveObject(ship, null, null);

        Assert.IsTrue(ship.ObjectOffsets!.x > 0f && ship.ObjectOffsets.x < startX,
            $"Approach phase should move laterally from the right toward center. Start x={startX:0.0}, approach x={ship.ObjectOffsets.x:0.0}.");
        Assert.IsTrue(ship.ObjectOffsets.y > startY && ship.ObjectOffsets.y < 0f,
            $"Approach phase should move vertically toward center without overshooting. Start y={startY:0.0}, approach y={ship.ObjectOffsets.y:0.0}.");
        Assert.AreEqual(startZ, ship.ObjectOffsets.z, 1f,
            "Approach phase should not dive inward; depth must stay stable until the ship reaches center.");

        now = now.AddSeconds(0.7);
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0f, ship.ObjectOffsets!.x, 5f,
            "Ship should reach screen center before turning toward Earth.");
        Assert.AreEqual(0f, ship.ObjectOffsets.y, 5f,
            "Ship should reach Earth center vertically before turning toward Earth.");
        Assert.AreEqual(startZ, ship.ObjectOffsets.z, 1f,
            "Ship should not zoom inward immediately after reaching center; it should turn toward Earth first.");
    }

    [TestMethod]
    public void OutroShipControls_FliesTowardEarthByIncreasingDepth()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        float startZ = ship.ObjectOffsets!.z;

        now = now.AddSeconds(5.0);
        controls.MoveObject(ship, null, null);

        Assert.IsTrue(ship.ObjectOffsets!.z > startZ + 700f,
            $"Ship should move toward Earth by increasing z offset. Start z={startZ:0.0}, final z={ship.ObjectOffsets.z:0.0}.");
        Assert.AreEqual(1000f, ship.ObjectOffsets.z, 1f,
            $"Ship should finish at the requested perspective depth. Final z={ship.ObjectOffsets.z:0.0}.");
        Assert.IsTrue(ship.ZSortBias > 0f,
            "Ship should stay rendered in front of Earth via sort bias, not by limiting the perspective depth.");
        Assert.AreEqual(0f, ship.ObjectOffsets.x, 3f, "Ship should fly into the center of Earth on screen.");
        Assert.AreEqual(0f, ship.ObjectOffsets.y, 3f, "Ship should fly into the center of Earth on screen.");
    }

    [TestMethod]
    public void OutroShipControls_TargetDepthShrinksProjectedShip()
    {
        var startBounds = ProjectShipHullBoundsAfter(seconds: 0.01);
        var targetBounds = ProjectShipHullBoundsAfter(seconds: 5.0);

        Assert.IsTrue(targetBounds.Width < startBounds.Width * 0.50f,
            $"Ship should visibly shrink as z offset increases. Start width={startBounds.Width}px, target width={targetBounds.Width}px.");
        Assert.IsTrue(targetBounds.Height < startBounds.Height * 0.50f,
            $"Ship should visibly shrink as z offset increases. Start height={startBounds.Height}px, target height={targetBounds.Height}px.");
    }

    [TestMethod]
    public void OutroShipControls_ApproachPhase_DoesNotShrinkShip()
    {
        var startBounds = GetShipHullLocalBoundsAfter(seconds: 0.01);
        var approachBounds = GetShipHullLocalBoundsAfter(seconds: 2.5);

        Assert.AreEqual(startBounds.Width, approachBounds.Width, 0.001f,
            $"Ship model should not be scaled while it is only flying in from the right. Start width={startBounds.Width}, approach width={approachBounds.Width}.");
        Assert.AreEqual(startBounds.Height, approachBounds.Height, 0.001f,
            $"Ship model should not be scaled before the inward dive. Start height={startBounds.Height}, approach height={approachBounds.Height}.");
    }

    [TestMethod]
    public void OutroShipControls_TurnPhase_DoesNotZoomBeforeFacingEarth()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        float startZ = ship.ObjectOffsets!.z;
        var startBounds = GetShipHullLocalBounds(ship);

        now = now.AddSeconds(3.4);
        controls.MoveObject(ship, null, null);
        var turnBounds = GetShipHullLocalBounds(ship);

        Assert.IsTrue(ship.Rotation!.z < -5f,
            $"Ship should still be turning toward Earth at this point. Z rotation was {ship.Rotation.z:0.0}.");
        Assert.AreEqual(startZ, ship.ObjectOffsets!.z, 1f,
            "Ship should not change depth until it has finished turning toward Earth.");
        Assert.AreEqual(startBounds.Width, turnBounds.Width, 0.001f,
            "Ship should not scale down while it is still turning toward Earth.");
        Assert.AreEqual(startBounds.Height, turnBounds.Height, 0.001f,
            "Ship should not scale down while it is still turning toward Earth.");

        now = now.AddSeconds(0.4);
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0f, ship.Rotation!.z, 1f,
            "Ship should finish the turn before the zoom/dive phase is visible.");
        Assert.AreEqual(startZ, ship.ObjectOffsets!.z, 1f,
            "Ship should still hold depth at the end of the turn phase.");

        now = now.AddSeconds(0.2);
        controls.MoveObject(ship, null, null);

        Assert.IsTrue(ship.ObjectOffsets!.z > startZ,
            "Ship should begin moving inward only after the turn toward Earth is complete.");
    }

    [TestMethod]
    public void OutroShipControls_DivePhase_UsesSmallArcIntoEarth()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        float startZ = ship.ObjectOffsets!.z;

        now = now.AddSeconds(4.4);
        controls.MoveObject(ship, null, null);

        Assert.IsTrue(ship.ObjectOffsets!.z > startZ,
            "Ship should be in the inward dive when the arc is visible.");
        Assert.IsTrue(Math.Abs(ship.ObjectOffsets.x) > 20f,
            $"Mid-dive path should arc sideways instead of staying mechanically centered. X={ship.ObjectOffsets.x:0.0}.");
        Assert.IsTrue(Math.Abs(ship.ObjectOffsets.y) > 5f,
            $"Mid-dive path should arc vertically a little instead of staying mechanically centered. Y={ship.ObjectOffsets.y:0.0}.");

        now = now.AddSeconds(0.7);
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0f, ship.ObjectOffsets!.x, 3f,
            "Ship should still finish the dive at Earth center after the arc.");
        Assert.AreEqual(0f, ship.ObjectOffsets.y, 3f,
            "Ship should still finish the dive at Earth center after the arc.");
    }

    [TestMethod]
    public void OutroShipControls_DivePhase_RotatesWithArcAndReturnsToEarthHeading()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);

        now = now.AddSeconds(4.1);
        controls.MoveObject(ship, null, null);
        float earlyDiveZ = ship.Rotation!.z;
        float earlyDiveY = ship.Rotation.y;

        Assert.IsTrue(earlyDiveZ < -3f,
            $"Ship should yaw slightly with the first half of the arc. Z rotation was {earlyDiveZ:0.0}.");
        Assert.IsTrue(earlyDiveY < -1f,
            $"Ship should bank slightly with the first half of the arc. Y rotation was {earlyDiveY:0.0}.");

        now = now.AddSeconds(0.65);
        controls.MoveObject(ship, null, null);

        Assert.IsTrue(ship.Rotation!.z > 3f,
            $"Ship should yaw back the other way as the arc returns to center. Z rotation was {ship.Rotation.z:0.0}.");
        Assert.IsTrue(ship.Rotation.y > 1f,
            $"Ship should bank back the other way as the arc returns to center. Y rotation was {ship.Rotation.y:0.0}.");

        now = now.AddSeconds(0.35);
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(0f, ship.Rotation!.z, 0.001f,
            "Ship should finish the arc on the established Earth heading.");
        Assert.AreEqual(0f, ship.Rotation.y, 0.001f,
            "Ship should finish the arc without residual bank.");
    }

    [TestMethod]
    public void OutroShipControls_TurnsTowardEarthDuringDiveWithoutPitchingWrongAxis()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        float startXRotation = ship.Rotation!.x;
        float startYRotation = ship.Rotation.y;
        float startZRotation = ship.Rotation.z;

        now = now.AddSeconds(2.9);
        controls.MoveObject(ship, null, null);
        float approachXRotation = ship.Rotation!.x;
        float approachZRotation = ship.Rotation.z;

        now = now.AddSeconds(2.1);
        controls.MoveObject(ship, null, null);

        Assert.AreEqual(WorldViewSetup.CameraPitchDegrees, startXRotation, 0.001f,
            "Ship should enter with the normal scene pitch.");
        Assert.IsTrue(startZRotation < -80f && startZRotation > -120f,
            $"Ship should point across the screen from the right side toward Earth center. Start Z rotation was {startZRotation:0.0}.");
        Assert.AreEqual(WorldViewSetup.CameraPitchDegrees, approachXRotation, 0.001f,
            "Ship should keep the normal scene pitch while approaching Earth center.");
        Assert.AreEqual(startZRotation, approachZRotation, 0.001f,
            "Ship should not yaw away from its approach heading while flying from the right.");
        Assert.AreEqual(startXRotation, ship.Rotation!.x, 0.001f,
            "The inward dive should not use the X/camera-tilt axis.");
        Assert.IsTrue(ship.Rotation.z > startZRotation,
            $"The inward dive should turn the ship's screen-plane heading toward Earth. Start Z={startZRotation:0.0}, final Z={ship.Rotation.z:0.0}.");
        Assert.AreEqual(0f, ship.Rotation.z, 0.001f,
            "The inward dive should finish nose-first on the Earth heading, not tail-first.");
        Assert.IsTrue(ship.Rotation.y > startYRotation,
            $"Ship may relax its bank during the dive, but should not spin. Start Y={startYRotation:0.0}, final Y={ship.Rotation.y:0.0}.");
    }

    [TestMethod]
    public void OutroDirector_RequestsFullFadeWhenShipReachesEarth()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        var director = new OutroDirector();
        var world = new TestWorld
        {
            EventBus = new GameEventBus(),
            WorldInhabitants = new List<I3dObject> { ship }
        };
        director.Initialize(world.EventBus!, world);

        controls.MoveObject(ship, null, null);
        director.Update();
        Assert.AreEqual(WorldFadePhase.Idle, GameState.WorldFade.Phase);

        now = now.AddSeconds(5.0);
        controls.MoveObject(ship, null, null);
        director.Update();

        Assert.AreEqual(WorldFadePhase.FadeOutRequested, GameState.WorldFade.Phase,
            "Outro director should use the shared full-screen fade when the space approach reaches Earth.");
        Assert.AreEqual("OutroShipReachedEarth", GameState.WorldFade.Reason);
    }

    [TestMethod]
    public void OutroDirector_WhenSpaceFadeIsBlack_BuildsGroundRevealAndRequestsFadeIn()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        var director = new OutroDirector();
        var world = new TestWorld
        {
            EventBus = new GameEventBus(),
            WorldInhabitants = new List<I3dObject> { ship }
        };
        director.Initialize(world.EventBus!, world);

        controls.MoveObject(ship, null, null);
        now = now.AddSeconds(5.0);
        controls.MoveObject(ship, null, null);
        director.Update();
        GameState.WorldFade.MarkFadeOutComplete();

        director.Update();

        Assert.AreEqual(OutroPhase.GroundReveal, director.Phase,
            "Black screen after the space approach should build the ground landing scene and start the reveal.");
        Assert.AreEqual(WorldFadePhase.FadeInRequested, GameState.WorldFade.Phase,
            "Ground reveal should fade back in after the landing scene is built while the screen is black.");
        Assert.AreEqual("OutroGroundReveal", GameState.WorldFade.Reason);
        Assert.IsNotNull(GameState.SurfaceState.SurfaceViewportObject,
            "Ground reveal should install a surface viewport object.");
        Assert.IsTrue(world.WorldInhabitants.Any(o => o.ObjectName == "Surface"),
            "Ground reveal should replace the space approach with the landing surface.");
        Assert.IsFalse(world.WorldInhabitants.Any(o => o.ObjectName == "Earth" || o.ObjectName == "Asteroid" || o.ObjectName == "Ship"),
            "Ground reveal should clear the space approach objects before fading back in.");
    }

    [TestMethod]
    public void OutroDirector_GroundReveal_AnimatesSurfaceToNormalOffset()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        var director = new OutroDirector();
        var world = new TestWorld
        {
            EventBus = new GameEventBus(),
            WorldInhabitants = new List<I3dObject> { ship }
        };
        director.Initialize(world.EventBus!, world);

        controls.MoveObject(ship, null, null);
        now = now.AddSeconds(5.0);
        controls.MoveObject(ship, null, null);
        director.Update();
        GameState.WorldFade.MarkFadeOutComplete();
        director.Update();

        var surface = GameState.SurfaceState.SurfaceViewportObject!;
        var initialOffset = OutroLandingSceneBuilder.CreateInitialSurfaceOffset();
        var finalOffset = OutroLandingSceneBuilder.CreateFinalSurfaceOffset();
        var revealStartOffset = (Vector3)surface.ObjectOffsets!;
        Assert.IsTrue(revealStartOffset.y <= initialOffset.y && revealStartOffset.y > finalOffset.y,
            "Surface should start the reveal below its normal gameplay position.");

        GameState.DeltaTime = 0.1f;
        for (int i = 0; i < 24; i++)
        {
            director.Update();
        }

        Assert.AreEqual(OutroPhase.GroundShipLanding, director.Phase,
            "After the ground reveal, the director should spawn the landing ship and start its descent.");
        var landingShip = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "Ship");
        Assert.IsNotNull(landingShip, "Ground landing should add the ship back above the platform.");
        Assert.IsInstanceOfType(landingShip.Movement, typeof(OutroLandingShipControls));
        Assert.AreEqual(OutroLandingSceneBuilder.CreateInitialLandingShipOffset().y, landingShip.ObjectOffsets!.y, 0.001f,
            "Landing ship should start high in the sky before lowering onto the platform.");
        var endOffset = (Vector3)surface.ObjectOffsets!;
        Assert.AreEqual(finalOffset.x, endOffset.x, 0.001f);
        Assert.AreEqual(finalOffset.y, endOffset.y, 0.001f);
        Assert.AreEqual(finalOffset.z, endOffset.z, 0.001f);
    }

    [TestMethod]
    public void OutroShipControls_Phase1_ShipSortsAboveAllEarthTriangles()
    {
        // Reproduce exactly what the renderer does:
        //   1. Project Earth + ship to 2D together (same list).
        //   2. Sort ascending by CalculatedZ (lower = drawn first = behind).
        //   3. Every ship hull triangle must have a higher CalculatedZ than
        //      every Earth globe triangle so that the ship is always painted last (on top).
        var world = CreateWorldAtOutro();
        var earth = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        var ship  = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Ship");

        // Run one movement tick � this sets ZSortBias and positions ship in Phase 1
        ship.Movement!.MoveObject(ship, null, null);

        ApplyLiveMeshRotation(earth);
        ApplyLiveMeshRotation(ship);

        var converter = OmegaPerspectiveProjectorFactory.Create();
        // Project both objects together, exactly as the renderer does
        var allTriangles = converter.ProjectToTriangles(new List<_3dObject> { earth, ship }, currentFrame: 1);

        // Renderer sort: ascending CalculatedZ ? highest = drawn last = on top
        allTriangles.Sort((a, b) => a.CalculatedZ.CompareTo(b.CalculatedZ));

        var earthGlobeTriangles = allTriangles.Where(t => t.PartName == "EarthGlobe").ToList();
        var shipHullTriangles   = allTriangles.Where(t => t.PartName == "UpperPart" || t.PartName == "LowerPart" || t.PartName == "RearPart").ToList();

        Assert.IsTrue(earthGlobeTriangles.Count > 0, "Earth globe must project triangles.");
        Assert.IsTrue(shipHullTriangles.Count > 0,   "Ship hull must project triangles.");

        float maxEarthZ = earthGlobeTriangles.Max(t => t.CalculatedZ);
        float minShipZ  = shipHullTriangles.Min(t => t.CalculatedZ);
        float maxShipZ  = shipHullTriangles.Max(t => t.CalculatedZ);

        Assert.IsTrue(minShipZ > maxEarthZ,
            $"ALL ship hull triangles must sort above ALL Earth globe triangles so the ship renders on top. " +
            $"Ship CalculatedZ range: {minShipZ:0.0}�{maxShipZ:0.0}, Earth globe max: {maxEarthZ:0.0}.");
    }

    [TestMethod]
    public void OutroShipControls_ReleasesParticlesFromBothEngines()
    {
        var world = CreateWorldAtOutro();
        var ship = world.WorldInhabitants.First(o => o.ObjectName == "Ship");
        var controls = (OutroShipControls)ship.Movement!;

        controls.SetParticleGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "JetMotor").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "JetMotorDirectionGuide").Triangles.First());
        controls.SetRearEngineGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "RearEngine").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "RearEngineDirectionGuide").Triangles.First());

        controls.ReleaseParticles(ship);

        var particlePositions = ship.Particles!.Particles
            .Select(p => (Vector3)p.Position)
            .ToList();

        Assert.IsTrue(particlePositions.Count >= 40,
            $"Both engines should emit in the same frame. Particle count was {particlePositions.Count}.");
        Assert.IsTrue(particlePositions.Any(p => p.y < 30f),
            "Lower jet motor should contribute particles near the belly engine.");
        Assert.IsTrue(particlePositions.Any(p => p.y > 45f),
            "Rear engine should contribute particles from the tail engine.");
    }

    [TestMethod]
    public void OutroShipControls_EmitsEngineParticlesEveryFifthFrame()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);

        controls.SetParticleGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "JetMotor").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "JetMotorDirectionGuide").Triangles.First());
        controls.SetRearEngineGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "RearEngine").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "RearEngineDirectionGuide").Triangles.First());

        controls.ReleaseParticles(ship);
        int firstCount = ship.Particles!.Particles.Count;

        for (int i = 0; i < 4; i++)
            controls.ReleaseParticles(ship);
        int skippedFrameCount = ship.Particles.Particles.Count;

        controls.ReleaseParticles(ship);
        int fifthFrameCount = ship.Particles.Particles.Count;

        Assert.IsTrue(firstCount > 0, "First engine emission should create particles.");
        Assert.AreEqual(firstCount, skippedFrameCount,
            "Engine particles should not emit again during the four skipped frames.");
        Assert.IsTrue(fifthFrameCount > skippedFrameCount,
            "Engine particles should emit again on the fifth frame.");
    }

    [TestMethod]
    public void OutroShipControls_PlaysRocketLoop()
    {
        var world = CreateWorldAtOutro();
        var ship = world.WorldInhabitants.First(o => o.ObjectName == "Ship");
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        ship.Movement!.MoveObject(ship, audio, registry);

        Assert.AreEqual("rocket_main", audio.LastSoundId, "Outro ship should start the normal rocket sound.");
        Assert.AreEqual(AudioPlayMode.SegmentedLoop, audio.LastMode, "Rocket sound should run as a loop during the fly-in.");
    }

    [TestMethod]
    public void OutroScene_EarthProjectsVisibleTriangles()
    {
        var world = CreateWorldAtOutro();
        var earth = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        ApplyLiveMeshRotation(earth);
        int triangleCount = earth.ObjectParts.Sum(p => p.Triangles.Count);
        var converter = OmegaPerspectiveProjectorFactory.Create();

        var projected = converter.ProjectToTriangles(new List<_3dObject> { earth }, currentFrame: 1);
        var bounds = GetProjectedBounds(projected);
        var brightestShade = projected.Max(CalculateRenderShadeFactor);

        Assert.IsTrue(projected.Count > 0, "Earth should project at least one visible triangle on the Outro screen.");
        Assert.IsTrue(projected.Count < triangleCount,
            $"Earth should cull hidden faces before render. Projected {projected.Count} of {triangleCount} triangles.");
        Assert.IsTrue(bounds.Width >= 260,
            $"Earth should be large enough to see. Projected width was {bounds.Width}px.");
        Assert.IsTrue(bounds.Height >= 180,
            $"Earth should be large enough to see. Projected height was {bounds.Height}px.");
        Assert.IsTrue(brightestShade >= 0.15f,
            $"Earth should have lit triangles against the black background. Brightest shade was {brightestShade:0.00}.");
    }

    [TestMethod]
    public void OutroScene_EarthSurvivesLiveRotationAndRenderFiltering()
    {
        var world = CreateWorldAtOutro();
        var earth = (_3dObject)world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        ApplyLiveMeshRotation(earth);

        var converter = OmegaPerspectiveProjectorFactory.Create();
        var projected = converter.ProjectToTriangles(new List<_3dObject> { earth }, currentFrame: 1);
        int renderable = WorldRenderer.ProcessTrianglesForRender(
            projected,
            new Dictionary<(float, string), Color>(),
            new Dictionary<Color, SolidColorBrush>(),
            new Dictionary<Color, Pen>());

        Assert.IsTrue(projected.Count > 0, "Earth should still project triangles after live-loop mesh rotation.");
        Assert.IsTrue(renderable > 0, "Earth should survive renderer depth/color filtering after projection.");
    }

    [TestMethod]
    public void OutroScene_EarthRotation_HasCorrectXAxis()
    {
        var world = CreateWorldAtOutro();
        var earth = world.WorldInhabitants.First(o => o.ObjectName == "Earth");
        Assert.AreEqual(WorldViewSetup.WorldPitchDegrees, earth.Rotation.x, "Earth X rotation should match the shared world pitch.");
    }

    [TestMethod]
    public void OutroScene_HasSceneMusic()
    {
        GameState.GamePlayState.SceneIndex = 9;
        var handler = new SceneHandler();
        var scene = handler.GetActiveScene();
        Assert.IsFalse(string.IsNullOrEmpty(scene.SceneMusic), "Outro scene should have a SceneMusic id.");
        Assert.AreEqual("music_outro", scene.SceneMusic, "Outro should use music_outro.");
    }

    [TestMethod]
    public void OutroScene_HasOutroDirector()
    {
        GameState.GamePlayState.SceneIndex = 9;
        var handler = new SceneHandler();
        Assert.IsInstanceOfType(handler.GetActiveScene().Director, typeof(OutroDirector),
            "Outro should use a director so the cinematic phases can coordinate fade, ground reveal, landing, and pilot reveal.");
    }

    [TestMethod]
    public void OutroScene_OverlayType_IsOutro()
    {
        var world = CreateWorldAtOutro();
        Assert.AreEqual(ScreenOverlayType.Outro, GameState.ScreenOverlayState.Type,
            "Outro should set overlay type to Outro.");
    }

    [TestMethod]
    public void OutroScene_OverlayIsHidden()
    {
        var world = CreateWorldAtOutro();
        Assert.IsFalse(GameState.ScreenOverlayState.ShowOverlay,
            "Outro overlay should be hidden on setup.");
    }

    [TestMethod]
    public void OutroScene_OnlyContainsEarth()
    {
        var world = CreateWorldAtOutro();
        var unexpected = world.WorldInhabitants
            .Where(o => o.ObjectName != "Earth" && o.ObjectName != "Asteroid" && o.ObjectName != "Ship")
            .ToList();
        Assert.AreEqual(0, unexpected.Count,
            $"Outro should only contain Earth, Ship, and Asteroid objects. Found: {string.Join(", ", unexpected.Select(o => o.ObjectName))}");
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_Build_ClearsSpaceObjectsAndCreatesLandingSurface()
    {
        var world = new TestWorld();
        world.WorldInhabitants.Add(new _3dObject { ObjectId = 1, ObjectName = "Earth" });
        world.WorldInhabitants.Add(new _3dObject { ObjectId = 2, ObjectName = "Asteroid" });
        world.WorldInhabitants.Add(new _3dObject { ObjectId = 3, ObjectName = "Ship" });

        var builder = new OutroLandingSceneBuilder();
        builder.Build(world);

        Assert.IsFalse(world.WorldInhabitants.Any(o => o.ObjectName == "Earth" || o.ObjectName == "Asteroid" || o.ObjectName == "Ship"),
            "Landing scene build should replace the space approach objects.");

        var surface = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "Surface");
        Assert.IsNotNull(surface, "Landing scene should create a surface viewport object.");
        Assert.AreSame(surface, GameState.SurfaceState.SurfaceViewportObject,
            "Surface state should point to the landing scene viewport object.");
        Assert.IsInstanceOfType(surface.Movement, typeof(GroundControls));
        Assert.IsInstanceOfType(surface.ParentSurface, typeof(Surface));
        Assert.IsFalse(surface.CrashBoxesFollowRotation,
            "Surface crash boxes should follow the normal scene setup pattern.");

        var startOffset = (Vector3)surface.ObjectOffsets!;
        var finalOffset = OutroLandingSceneBuilder.CreateFinalSurfaceOffset();
        Assert.AreEqual(OutroLandingSceneBuilder.CreateInitialSurfaceOffset().y, startOffset.y);
        Assert.IsTrue(startOffset.y > finalOffset.y + (400 * ScreenSetup.ScreenScaleY),
            "Landing surface should start below the normal surface position so the director can reveal it upward.");
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_Build_CreatesThreeScreenMapWithCenteredPlatform()
    {
        var world = new TestWorld();

        new OutroLandingSceneBuilder().Build(world);

        var map = GameState.SurfaceState.Global2DMap;
        Assert.IsNotNull(map, "Landing scene should install a surface map.");
        Assert.AreEqual(SurfaceSetup.viewPortSize * OutroLandingSceneBuilder.ScreenSpan, map.GetLength(0));
        Assert.AreEqual(SurfaceSetup.viewPortSize * OutroLandingSceneBuilder.ScreenSpan, map.GetLength(1));
        Assert.AreEqual(OutroLandingSceneBuilder.OutroMapMaxHeight, MapSetup.maxHeight);
        Assert.IsTrue(map.Cast<SurfaceData>().All(tile =>
            tile.mapDepth >= MapSetup.maxHeight * 0.15f &&
            tile.mapDepth < MapSetup.maxHeight * 0.40f),
            "Outro landing scene should use the normal green grassland height band, not winter/highland colors.");

        int centerZ = map.GetLength(0) / 2;
        int centerX = map.GetLength(1) / 2;
        int topLeftZ = centerZ - (OutroLandingSceneBuilder.LandingPlatformSizeTiles / 2);
        int topLeftX = centerX - (OutroLandingSceneBuilder.LandingPlatformSizeTiles / 2);

        for (int z = topLeftZ; z < topLeftZ + OutroLandingSceneBuilder.LandingPlatformSizeTiles; z++)
        {
            for (int x = topLeftX; x < topLeftX + OutroLandingSceneBuilder.LandingPlatformSizeTiles; x++)
            {
                Assert.AreEqual(OutroLandingSceneBuilder.LandingPlatformDepth, map[z, x].mapDepth,
                    "Landing platform should be a flat plateau in the surface data.");
            }
        }

        var crashBox = map[topLeftZ, topLeftX].crashBox;
        Assert.IsTrue(crashBox.HasValue, "Landing platform should expose a surface crash box.");
        Assert.AreEqual(OutroLandingSceneBuilder.LandingPlatformSizeTiles, crashBox.Value.width);
        Assert.AreEqual(OutroLandingSceneBuilder.LandingPlatformSizeTiles, crashBox.Value.height);
        Assert.AreEqual(OutroLandingSceneBuilder.LandingPlatformDepth + 20, crashBox.Value.boxDepth);
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_Build_AddsLandBasedPropsAroundPlatform()
    {
        var world = new TestWorld();

        new OutroLandingSceneBuilder().Build(world);

        var trees = world.WorldInhabitants.Where(o => o.ObjectName == "Tree").ToList();
        var houses = world.WorldInhabitants.Where(o => o.ObjectName == "House").ToList();
        var props = trees.Concat(houses).ToList();

        Assert.AreEqual(25, trees.Count, "Landing scene should add a dense tree line around the platform.");
        Assert.AreEqual(4, houses.Count, "Landing scene should add a few nearby props using the normal house object.");
        Assert.IsTrue(trees.All(t => t.Movement is TreeControls));
        Assert.IsTrue(houses.All(h => h.Movement is HouseControls));
        Assert.IsTrue(props.All(p => p.ParentSurface == GameState.SurfaceState.SurfaceViewportObject!.ParentSurface));

        var map = GameState.SurfaceState.Global2DMap!;
        var visibleSurfaceIds = GameState.SurfaceState.SurfaceViewportObject!.ParentSurface!.LandBasedIds;
        foreach (var prop in props)
        {
            Assert.IsTrue(prop.SurfaceBasedId > 0, $"{prop.ObjectName} should be bound to a surface tile.");
            Assert.IsTrue(FindTileByMapId(map, prop.SurfaceBasedId!.Value).hasLandbasedObject,
                $"{prop.ObjectName} tile should be marked as occupied in the surface map.");
            Assert.IsTrue(visibleSurfaceIds.Contains(prop.SurfaceBasedId),
                $"{prop.ObjectName} should be placed inside the initial visible landing viewport.");
        }
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_Build_AddsVisiblePlatformAndGroundStars()
    {
        var world = new TestWorld();

        new OutroLandingSceneBuilder().Build(world);

        var platform = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "OutroLandingPlatform");
        Assert.IsNotNull(platform, "Landing scene should add a visible platform object on top of the surface plateau.");
        Assert.AreSame(GameState.SurfaceState.SurfaceViewportObject!.ParentSurface, platform.ParentSurface);
        Assert.IsNull(platform.SurfaceBasedId,
            "Platform should be reveal-anchored, not recentered onto a surface triangle where it can sink into terrain.");
        var padPart = platform.ObjectParts.FirstOrDefault(p => p.PartName == "LandingPlatformPad");
        var markingPart = platform.ObjectParts.FirstOrDefault(p => p.PartName == "LandingPlatformMarkings");
        Assert.IsNotNull(padPart);
        Assert.IsNotNull(markingPart);
        Assert.IsTrue(platform.ZSortBias >= 30f,
            "Platform should sort clearly above the surface so it does not look pushed into the ground.");
        Assert.IsTrue(platform.CrashBoxesFollowRotation,
            "Platform crashbox should rotate with the platform mesh so landing particles can bounce from the visible pad.");
        Assert.AreEqual(1, platform.CrashBoxes.Count,
            "Landing platform should expose one pad crashbox for landing exhaust particles.");
        Assert.AreEqual("LandingPad", platform.CrashBoxNames!.Single());
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalSurfaceOffset().y,
            OutroLandingSceneBuilder.CreateFinalPlatformOffset().y,
            "Platform should stay tied to the surface offset instead of floating as a separate screen layer.");

        var platformZValues = platform.ObjectParts
            .SelectMany(p => p.Triangles)
            .SelectMany(t => new[] { t.vert1.z, t.vert2.z, t.vert3.z })
            .ToList();
        Assert.IsTrue(platformZValues.Max() >= OutroLandingSceneBuilder.LandingPlatformDepth + 90f,
            "Platform top should sit clearly above the surface plateau, not just a few units over it.");
        Assert.IsTrue(platformZValues.Max() - platformZValues.Min() >= 90f,
            "Platform should be a raised slab so the visible top sits above the surface.");

        var padTopZ = padPart.Triangles
            .SelectMany(t => new[] { t.vert1.z, t.vert2.z, t.vert3.z })
            .Max();
        var markingVertices = markingPart.Triangles
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
            .ToList();
        Assert.AreEqual(4, markingPart.Triangles.Count,
            "Landing platform should use a clean X landing mark, not several loose stripe blocks.");
        Assert.IsTrue(markingVertices.All(v => v.z >= padTopZ + 40f),
            "Landing mark should be lifted clearly above the platform top so the whole symbol is visible.");
        Assert.IsTrue(markingVertices.All(v => Math.Abs(v.x) < 330f && Math.Abs(v.y) < 210f),
            "Landing mark should fit fully within the platform top.");

        var stars = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "OutroGroundStars");
        Assert.IsNotNull(stars, "Landing scene should add a star field above the ground reveal.");
        Assert.IsNull(stars.SurfaceBasedId, "Ground reveal stars should stay screen/world anchored, not tied to one tile.");
        Assert.AreEqual(OutroGroundStars.StarCount * 4, stars.ObjectParts.Sum(p => p.Triangles.Count),
            "Ground reveal stars should render as small diamond quads.");
        Assert.AreEqual(OutroLandingSceneBuilder.CreateRevealInitialOffset(OutroLandingSceneBuilder.CreateFinalGroundStarsOffset()).y,
            stars.ObjectOffsets!.y);
        Assert.IsInstanceOfType(stars.Movement, typeof(OutroGroundStarsControls));
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_Build_AddsLargeSegmentedWelcomeBanner()
    {
        var world = new TestWorld();

        new OutroLandingSceneBuilder().Build(world);

        var banner = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "OutroLandingBanner");
        Assert.IsNotNull(banner, "Landing scene should add the welcome banner.");
        Assert.AreEqual(OutroLandingBanner.Line1, "Welcome back");
        Assert.AreEqual(OutroLandingBanner.Line2, "Glad you did not die!");
        Assert.AreSame(GameState.SurfaceState.SurfaceViewportObject!.ParentSurface, banner.ParentSurface);
        Assert.IsNull(banner.SurfaceBasedId,
            "Banner should be reveal-anchored with the platform instead of being recentered into the surface.");
        Assert.AreEqual(world.WorldInhabitants.First(o => o.ObjectName == "OutroLandingPlatform").SurfaceBasedId, banner.SurfaceBasedId,
            "Banner and platform should use the same anchoring model so they stay attached during reveal.");
        Assert.AreEqual(
            OutroLandingSceneBuilder.CreateFinalPlatformOffset().y - (OutroLandingSceneBuilder.BannerOffsetAbovePlatform * ScreenSetup.ScreenScaleY),
            OutroLandingSceneBuilder.CreateFinalBannerOffset().y,
            0.001f,
            "Banner final offset should keep the welcome banner raised above the rear of the platform.");
        Assert.IsTrue(OutroLandingSceneBuilder.CreateFinalBannerOffset().y < OutroLandingSceneBuilder.CreateFinalPlatformOffset().y,
            "Banner should stand on the back side of the platform in screen space.");

        var bannerYValues = banner.ObjectParts
            .SelectMany(p => p.Triangles)
            .SelectMany(t => new[] { t.vert1.y, t.vert2.y, t.vert3.y })
            .ToList();
        Assert.IsTrue(bannerYValues.Min() < -150f && bannerYValues.Max() <= 1f,
            "Banner geometry should rise upward from its platform foot instead of extending down into the platform.");

        var segmentParts = banner.ObjectParts
            .Where(p => p.PartName != null && p.PartName.StartsWith("BannerSegment_", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(OutroLandingBanner.SegmentCount, segmentParts.Count,
            "Banner cloth should be split into eight connected segments for later wind animation.");
        Assert.IsTrue(segmentParts.All(p => p.Triangles.Count >= 2),
            "Each banner segment should include its cloth panel.");
        Assert.IsTrue(segmentParts.Sum(p => p.Triangles.Count) > OutroLandingBanner.SegmentCount * 2,
            "Text decal triangles should be assigned to the segmented banner cloth.");
        Assert.IsNotNull(banner.ObjectParts.FirstOrDefault(p => p.PartName == "BannerPoleLeft"));
        Assert.IsNotNull(banner.ObjectParts.FirstOrDefault(p => p.PartName == "BannerPoleRight"));
        Assert.IsInstanceOfType(banner.Movement, typeof(OutroLandingBannerControls));
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_AddLandingShip_StartsShipAbovePlatform()
    {
        var world = new TestWorld();
        var builder = new OutroLandingSceneBuilder();
        builder.Build(world);

        builder.AddLandingShip(world);

        var ship = world.WorldInhabitants.Single(o => o.ObjectName == "Ship");
        Assert.IsInstanceOfType(ship.Movement, typeof(OutroLandingShipControls));
        Assert.IsInstanceOfType(ship.Particles, typeof(ParticlesAI));
        Assert.IsNull(ship.SurfaceBasedId, "Landing ship should be screen/reveal anchored during the descent.");
        Assert.AreEqual(0, ship.CrashBoxes.Count,
            "Landing ship should not crash with the platform; only its exhaust particles should use the platform crashbox.");

        var initial = OutroLandingSceneBuilder.CreateInitialLandingShipOffset();
        var final = OutroLandingSceneBuilder.CreateFinalLandingShipOffset();
        Assert.AreEqual(initial.y, ship.ObjectOffsets!.y, 0.001f);
        Assert.AreEqual(
            final.y - (OutroLandingSceneBuilder.LandingShipStartHeightAbovePlatform * ScreenSetup.ScreenScaleY),
            initial.y,
            0.001f,
            "Landing ship should start well above the platform before descending.");
        Assert.IsTrue(initial.y < final.y,
            "Landing ship should start above the platform and lower downward on screen.");
        Assert.IsTrue(final.y < OutroLandingSceneBuilder.CreateFinalPlatformOffset().y - (150 * ScreenSetup.ScreenScaleY),
            "Landing ship should finish a bit higher above the platform than the first landing draft.");
        Assert.IsTrue(final.z < initial.z,
            "Landing ship should come closer to the camera during the last landing beat.");
        Assert.AreEqual(OutroLandingShipControls.CreateInitialLandingRotation().z, ship.Rotation!.z, 0.001f,
            "Landing ship should start before the 180-degree turn.");
        Assert.AreEqual(OutroLandingShipControls.LandingZSortBias, ship.ZSortBias);
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_AddAstronaut_SpawnsSeparateObjectInsideOpenedShip()
    {
        var world = new TestWorld();
        var builder = new OutroLandingSceneBuilder();
        builder.Build(world);
        builder.AddLandingShip(world);

        builder.AddAstronaut(world);
        builder.AddAstronaut(world);

        var astronaut = world.WorldInhabitants.Single(o => o.ObjectName == OutroAstronaut.ObjectName);
        Assert.AreNotEqual(world.WorldInhabitants.Single(o => o.ObjectName == "Ship").ObjectId, astronaut.ObjectId,
            "Astronaut should be its own outro object, not part of the ship mesh.");
        Assert.IsInstanceOfType(astronaut.Movement, typeof(OutroAstronautControls));
        Assert.IsNull(astronaut.SurfaceBasedId,
            "Astronaut should be screen anchored with the landed ship, not bound to a surface tile.");
        Assert.AreEqual(0, astronaut.CrashBoxes.Count,
            "The tiny outro astronaut should not participate in collision.");
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalAstronautOffset().y, astronaut.ObjectOffsets!.y, 0.001f);
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalAstronautOffset().z, astronaut.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(OutroLandingShipControls.CreateFinalLandingRotation().x, astronaut.Rotation!.x, 0.001f);
        Assert.AreEqual(OutroLandingShipControls.CreateFinalLandingRotation().z, astronaut.Rotation.z, 0.001f);
        Assert.IsTrue(astronaut.ZSortBias > OutroLandingShipControls.LandingZSortBias,
            "Astronaut should sort above the opened ship so the pilot is visible inside the cockpit.");
        Assert.IsTrue(astronaut.ObjectParts.SelectMany(p => p.Triangles).All(t => t.noHidden == true),
            "The small astronaut should render both sides because it is seen through the opened hatch.");
        Assert.IsTrue(astronaut.ObjectParts.First(p => p.PartName == "AstronautHelmet").Triangles.Any(t => t.Color == OutroAstronaut.VisorColor),
            "Astronaut visor should use the darker helmet window color so the face opening reads clearly.");
    }

    [TestMethod]
    public void OutroLandingSceneBuilder_AddFireworks_SpawnsScreenAnchoredFireworks()
    {
        var world = new TestWorld();
        var builder = new OutroLandingSceneBuilder();
        builder.Build(world);

        builder.AddFireworks(world);
        builder.AddFireworks(world);

        var fireworks = world.WorldInhabitants.Single(o => o.ObjectName == OutroFireworks.ObjectName);
        Assert.IsInstanceOfType(fireworks.Movement, typeof(OutroFireworksControls));
        Assert.IsNull(fireworks.SurfaceBasedId,
            "Outro fireworks should be screen anchored in the reveal sky, not attached to a terrain tile.");
        Assert.AreEqual(0, fireworks.CrashBoxes.Count,
            "Firework particles are visual celebration particles and should not participate in collision.");
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalFireworksOffset().y, fireworks.ObjectOffsets!.y, 0.001f);
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalFireworksOffset().z, fireworks.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(OutroFireworks.ParticlePartName, fireworks.ObjectParts.Single().PartName);
    }

    [TestMethod]
    public void OutroAstronautControls_RevealScalesPilotUpOverFirstSecond()
    {
        var astronaut = OutroAstronaut.CreateAstronaut(null);
        var initialBounds = GetObjectLocalBounds(astronaut);

        GameState.DeltaTime = 0.1f;
        for (int i = 0; i < 12; i++)
            astronaut.Movement!.MoveObject(astronaut, null, null);

        var revealedBounds = GetObjectLocalBounds(astronaut);
        Assert.IsTrue(revealedBounds.Width > initialBounds.Width * 1.8f,
            "Astronaut should size up during its first second instead of popping into full size.");
        Assert.IsTrue(revealedBounds.DepthY > initialBounds.DepthY * 8f,
            "Astronaut reveal should grow from a compressed Y scale so it does not flicker into existence.");
    }

    [TestMethod]
    public void OutroAstronautControls_WavesArmWithoutMovingBody()
    {
        var astronaut = OutroAstronaut.CreateAstronaut(null);
        var wavingArm = astronaut.ObjectParts.First(p => p.PartName == OutroAstronaut.WavingArmPartName);
        var body = astronaut.ObjectParts.First(p => p.PartName == "AstronautBody");

        GameState.DeltaTime = 0.1f;
        for (int i = 0; i < 12; i++)
            astronaut.Movement!.MoveObject(astronaut, null, null);

        var beforeArm = (Vector3)wavingArm.Triangles[0].vert1;
        var beforeBody = (Vector3)body.Triangles[0].vert1;

        astronaut.Movement!.MoveObject(astronaut, null, null);
        astronaut.Movement.MoveObject(astronaut, null, null);

        var afterArm = (Vector3)wavingArm.Triangles[0].vert1;
        var afterBody = (Vector3)body.Triangles[0].vert1;
        Assert.IsTrue(Math.Abs(afterArm.x - beforeArm.x) > 0.1f || Math.Abs(afterArm.z - beforeArm.z) > 0.1f,
            "Astronaut waving arm should animate after the pilot appears.");
        Assert.AreEqual(beforeBody.x, afterBody.x, 0.001f,
            "Astronaut body should stay anchored while the arm waves.");
        Assert.AreEqual(beforeBody.z, afterBody.z, 0.001f,
            "Astronaut body should stay anchored while the arm waves.");
    }

    [TestMethod]
    public void OutroDirector_WhenLandingHatchStartsOpening_SpawnsAstronautBeforeFullyOpen()
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        var director = new OutroDirector();
        var world = new TestWorld
        {
            EventBus = new GameEventBus(),
            WorldInhabitants = new List<I3dObject> { ship }
        };
        director.Initialize(world.EventBus!, world);

        controls.MoveObject(ship, null, null);
        now = now.AddSeconds(5.0);
        controls.MoveObject(ship, null, null);
        director.Update();
        GameState.WorldFade.MarkFadeOutComplete();
        director.Update();

        GameState.DeltaTime = 0.1f;
        for (int i = 0; i < 24; i++)
            director.Update();

        var landingShip = world.WorldInhabitants.First(o => o.ObjectName == "Ship");
        var landingControls = (OutroLandingShipControls)landingShip.Movement!;
        for (int i = 0; i < 35; i++)
        {
            landingControls.MoveObject(landingShip, null, null);
            director.Update();
        }

        Assert.IsTrue(landingControls.IsAstronautRevealReady);
        Assert.IsFalse(landingControls.IsHatchOpen,
            "Astronaut should appear while the hatch is opening, not wait until it is fully open.");
        Assert.AreEqual(OutroPhase.GroundPilotReveal, director.Phase,
            "Director should advance to the pilot reveal phase once the hatch has opened enough to reveal the astronaut.");
        var astronaut = world.WorldInhabitants.Single(o => o.ObjectName == OutroAstronaut.ObjectName);
        Assert.AreEqual(OutroLandingSceneBuilder.CreateFinalAstronautOffset().y, astronaut.ObjectOffsets!.y, 0.001f,
            "Astronaut should be placed in the landed ship after hatch open.");
        Assert.IsNotNull(world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == OutroFireworks.ObjectName),
            "Fireworks should start in the sky as soon as the astronaut becomes visible.");
    }

    [TestMethod]
    public void OutroDirector_FinalOverlayPage_UsesSharedHighscoreFormatter()
    {
        string originalLocalFolder = PersistenceSetup.LocalFolder;
        int originalMaxEntries = PersistenceSetup.MaxHighscoreEntries;
        string? originalSupabaseUrl = PersistenceSetup.SupabaseUrl;
        string? originalSupabaseAnonKey = PersistenceSetup.SupabaseAnonKey;
        string originalSupabaseTableName = PersistenceSetup.SupabaseTableName;
        string testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainOutroHighscoreTests", Guid.NewGuid().ToString("N"));

        try
        {
            PersistenceSetup.LocalFolder = testLocalFolder;
            PersistenceSetup.MaxHighscoreEntries = 100;
            PersistenceSetup.SupabaseUrl = null;
            PersistenceSetup.SupabaseAnonKey = null;
            PersistenceSetup.SupabaseTableName = "highscores";
            HighscoreService.ResetRemoteFetchStateForTests();
            PersistenceSetup.Initialize();
            HighscoreService.SaveLocalHighscores(new HighscoreList
            {
                Entries = new List<HighscoreEntry>
                {
                    new()
                    {
                        PlayerName = "Jarle",
                        Score = 123456,
                        WaveReached = 9,
                        TotalKills = 42,
                        DateUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc).ToString("o")
                    }
                }
            });

            var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
            var controls = new OutroShipControls(() => now);
            var ship = Ship.CreateShip(null, controls);
            ship.ObjectName = "Ship";
            ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

            var director = new OutroDirector();
            var world = new TestWorld
            {
                EventBus = new GameEventBus(),
                WorldInhabitants = new List<I3dObject> { ship }
            };
            director.Initialize(world.EventBus!, world);

            controls.MoveObject(ship, null, null);
            now = now.AddSeconds(5.0);
            controls.MoveObject(ship, null, null);
            director.Update();
            GameState.WorldFade.MarkFadeOutComplete();
            director.Update();

            GameState.DeltaTime = 0.1f;
            for (int i = 0; i < 24; i++)
                director.Update();

            var landingShip = world.WorldInhabitants.First(o => o.ObjectName == "Ship");
            var landingControls = (OutroLandingShipControls)landingShip.Movement!;
            for (int i = 0; i < 35; i++)
            {
                landingControls.MoveObject(landingShip, null, null);
                director.Update();
            }

            for (int i = 0; i < 105; i++)
                director.Update();

            var overlay = GameState.ScreenOverlayState;
            Assert.IsTrue(overlay.ShowOverlay, "Outro should show the final congratulations overlay after the astronaut reveal.");
            Assert.AreEqual(3, overlay.Pages.Count, "Outro overlay should end with a leaderboard/highscore page.");
            Assert.AreEqual("LEADERBOARD", overlay.Pages[2][1]);
            Assert.AreEqual(HighscoreOverlayFormatter.BuildBody(), overlay.Pages[2][2],
                "Outro leaderboard page should reuse the same highscore body formatter as the existing highscore page.");
            Assert.IsTrue(overlay.Pages[2][2].Contains(PlayerNameFormatter.Normalize("Jarle")),
                "Final outro overlay should include the saved highscore list.");
            Assert.IsTrue(overlay.Pages[2][3].Contains("PAGE 3 / 3"));
        }
        finally
        {
            PersistenceSetup.LocalFolder = originalLocalFolder;
            PersistenceSetup.MaxHighscoreEntries = originalMaxEntries;
            PersistenceSetup.SupabaseUrl = originalSupabaseUrl;
            PersistenceSetup.SupabaseAnonKey = originalSupabaseAnonKey;
            PersistenceSetup.SupabaseTableName = originalSupabaseTableName;
            HighscoreService.ResetRemoteFetchStateForTests();

            try
            {
                if (Directory.Exists(testLocalFolder))
                    Directory.Delete(testLocalFolder, recursive: true);
            }
            catch
            {
            }
        }
    }

    [TestMethod]
    public void OutroLandingShipControls_LowersShipAndEmitsOnlyLowerEngineParticles()
    {
        var initial = OutroLandingSceneBuilder.CreateInitialLandingShipOffset();
        var final = OutroLandingSceneBuilder.CreateFinalLandingShipOffset();
        var controls = new OutroLandingShipControls(initial, final);
        var ship = Ship.CreateShip(null, controls);

        controls.SetParticleGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "JetMotor").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "JetMotorDirectionGuide").Triangles.First());
        controls.SetRearEngineGuideCoordinates(
            ship.ObjectParts.First(p => p.PartName == "RearEngine").Triangles.First(),
            ship.ObjectParts.First(p => p.PartName == "RearEngineDirectionGuide").Triangles.First());
        var startBounds = GetShipHullLocalBounds(ship);

        controls.ReleaseParticles(ship);

        var particlePositions = ship.Particles!.Particles
            .Select(p => (Vector3)p.Position)
            .ToList();

        Assert.IsTrue(particlePositions.Count > 0,
            "Landing ship should continuously feed the lower engine particle stream.");
        Assert.IsTrue(particlePositions.All(p => p.y < 35f),
            "Landing particles should come from the lower JetMotor, not the rear engine.");

        GameState.DeltaTime = 0.1f;
        controls.MoveObject(ship, null, null);
        float afterFirstMoveY = ship.ObjectOffsets!.y;
        float afterFirstMoveZRotation = ship.Rotation!.z;
        for (int i = 0; i < 40; i++)
            controls.MoveObject(ship, null, null);

        Assert.IsTrue(afterFirstMoveY > initial.y,
            "Landing ship should move downward from the sky.");
        Assert.IsTrue(afterFirstMoveZRotation > OutroLandingShipControls.CreateInitialLandingRotation().z,
            "Landing ship should begin turning while it lowers from the sky.");
        Assert.AreEqual(final.y, ship.ObjectOffsets!.y, 0.001f,
            "Landing ship should finish on the platform landing offset.");
        Assert.AreEqual(OutroLandingShipControls.CreateFinalLandingRotation().z, ship.Rotation!.z, 0.001f,
            "Landing ship should finish its 180-degree turn facing the camera.");
        Assert.AreEqual(OutroLandingShipControls.CreateFinalLandingRotation().x, ship.Rotation.x, 0.001f,
            "Landing ship should pitch its front up when it reaches the bottom.");
        var finalBounds = GetShipHullLocalBounds(ship);
        Assert.IsTrue(finalBounds.Width > startBounds.Width * 1.15f,
            "Landing ship should size up during the final landing beat.");
        Assert.IsTrue(controls.IsLanded);
    }

    [TestMethod]
    public void OutroLandingShipControls_AfterLanding_FlipsUpperPartOpen()
    {
        var initial = OutroLandingSceneBuilder.CreateInitialLandingShipOffset();
        var final = OutroLandingSceneBuilder.CreateFinalLandingShipOffset();
        var controls = new OutroLandingShipControls(initial, final);
        var ship = Ship.CreateShip(null, controls);

        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "UpperPart").Triangles.All(t => t.noHidden != true),
            "Regular ship hatch geometry should keep normal culling until the outro hatch actually opens.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "TopCannon").Triangles.All(t => t.noHidden != true),
            "Regular ship cannon geometry should keep normal culling until the outro hatch actually opens.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "LowerPart").Triangles.All(t => t.noHidden != true),
            "Regular ship lower hull should keep normal culling until the outro hatch actually opens.");

        GameState.DeltaTime = 0.1f;
        for (int i = 0; i < 31; i++)
            controls.MoveObject(ship, null, null);

        Assert.IsFalse(controls.IsLanded);
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "UpperPart").Triangles.All(t => t.noHidden != true),
            "Upper hatch should keep normal culling before landing.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "LowerPart").Triangles.All(t => t.noHidden != true),
            "Lower hull should keep normal culling before landing.");

        for (int i = 0; i < 2; i++)
            controls.MoveObject(ship, null, null);

        Assert.IsTrue(controls.IsLanded);
        Assert.IsFalse(controls.IsHatchOpen,
            "Upper hatch should begin opening after touchdown, not before the ship lands.");
        float landedUpperMaxZ = GetPartMaxZ(ship, "UpperPart");
        float landedCannonMaxZ = GetPartMaxZ(ship, "TopCannon");
        float landedLowerMaxZ = GetPartMaxZ(ship, "LowerPart");

        for (int i = 0; i < 10; i++)
            controls.MoveObject(ship, null, null);

        Assert.IsTrue(controls.IsHatchOpen);
        Assert.IsTrue(GetPartMaxZ(ship, "UpperPart") > landedUpperMaxZ + 50f,
            "After landing, the ship upper part should split from the hull and flip visibly upward.");
        Assert.IsTrue(GetPartMaxZ(ship, "TopCannon") > landedCannonMaxZ + 40f,
            "The top-mounted cannon should move with the opened upper hatch.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "UpperPart").Triangles.All(t => t.noHidden == true),
            "The opened upper hatch should render both sides so it does not disappear when flipped up.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "TopCannon").Triangles.All(t => t.noHidden == true),
            "Hatch-mounted detail should keep rendering with the opened hatch.");
        Assert.IsTrue(ship.ObjectParts.First(p => p.PartName == "LowerPart").Triangles.All(t => t.noHidden == true),
            "The exposed lower hull should render both sides while the hatch is open.");
        Assert.AreEqual(landedLowerMaxZ, GetPartMaxZ(ship, "LowerPart"), 0.001f,
            "Opening the upper hatch should not keep bending or moving the lower hull.");
        Assert.AreEqual(final.y, ship.ObjectOffsets!.y, 0.001f,
            "Opening the hatch should not move the landed ship away from its final offset.");
    }

    [TestMethod]
    public void OutroLandingBannerControls_WaveKeepsSegmentEdgesConnected()
    {
        var surface = new Surface();
        var banner = OutroLandingBanner.CreateBanner(surface);
        var segmentParts = banner.ObjectParts
            .Where(p => p.PartName != null && p.PartName.StartsWith("BannerSegment_", StringComparison.Ordinal))
            .OrderBy(p => p.PartName)
            .ToList();

        var before = (Vector3)segmentParts[3].Triangles[0].vert2;
        GameState.DeltaTime = 0.1f;
        banner.Movement!.MoveObject(banner, null, null);
        var after = (Vector3)segmentParts[3].Triangles[0].vert2;

        Assert.AreNotEqual(before.y, after.y,
            "Banner wave should move segment vertices vertically.");
        Assert.AreNotEqual(before.z, after.z,
            "Banner wave should move segment vertices in depth for a cloth-like ripple.");
        var animatedZValues = segmentParts
            .SelectMany(p => p.Triangles)
            .SelectMany(t => new[] { t.vert1.z, t.vert2.z, t.vert3.z })
            .ToList();
        Assert.IsTrue(animatedZValues.Max() - animatedZValues.Min() > 10f,
            "Banner wave should keep its subtler stable depth movement without the artifact-prone stronger z wave.");

        for (int i = 0; i < segmentParts.Count - 1; i++)
        {
            var currentTopRight = (Vector3)segmentParts[i].Triangles[0].vert2;
            var currentBottomRight = (Vector3)segmentParts[i].Triangles[0].vert3;
            var nextTopLeft = (Vector3)segmentParts[i + 1].Triangles[0].vert1;
            var nextBottomLeft = (Vector3)segmentParts[i + 1].Triangles[1].vert3;

            Assert.AreEqual(currentTopRight.x, nextTopLeft.x, 0.001f);
            Assert.AreEqual(currentTopRight.y, nextTopLeft.y, 0.001f,
                "Adjacent banner segments should share top-edge wave displacement.");
            Assert.AreEqual(currentTopRight.z, nextTopLeft.z, 0.001f,
                "Adjacent banner segments should share top-edge depth displacement.");

            Assert.AreEqual(currentBottomRight.x, nextBottomLeft.x, 0.001f);
            Assert.AreEqual(currentBottomRight.y, nextBottomLeft.y, 0.001f,
                "Adjacent banner segments should share bottom-edge wave displacement.");
            Assert.AreEqual(currentBottomRight.z, nextBottomLeft.z, 0.001f,
                "Adjacent banner segments should share bottom-edge depth displacement.");
        }
    }

    [TestMethod]
    public void OutroGroundStarsControls_PulsesStarsOverTime()
    {
        var stars = OutroGroundStars.CreateStarField();
        var part = stars.ObjectParts.Single();
        var before = (Vector3)part.Triangles[0].vert1;
        string beforeColor = part.Triangles[0].Color!;

        GameState.DeltaTime = 0.1f;
        stars.Movement!.MoveObject(stars, null, null);
        var afterFirstMove = (Vector3)part.Triangles[0].vert1;
        string afterFirstColor = part.Triangles[0].Color!;

        stars.Movement.MoveObject(stars, null, null);
        var afterSecondMove = (Vector3)part.Triangles[0].vert1;
        string afterSecondColor = part.Triangles[0].Color!;

        Assert.IsTrue(before.x != afterFirstMove.x || before.y != afterFirstMove.y,
            "Star pulse should move the star vertices slightly.");
        Assert.IsTrue(afterFirstMove.x != afterSecondMove.x || afterFirstMove.y != afterSecondMove.y,
            "Star pulse should keep changing over time.");
        Assert.AreNotEqual(beforeColor, afterFirstColor,
            "Star pulse should modulate brightness.");
        Assert.AreNotEqual(afterFirstColor, afterSecondColor,
            "Star brightness should continue pulsing over time.");
    }

    [TestMethod]
    public void OutroFireworksControls_LaunchesExplodesWithBrightColorsAndSound()
    {
        var fireworks = OutroFireworks.CreateFireworks();
        var part = fireworks.ObjectParts.Single();
        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();

        GameState.DeltaTime = 0.1f;
        fireworks.Movement!.MoveObject(fireworks, audio, registry);
        Assert.IsTrue(part.Triangles.Count > 0,
            "Fireworks should immediately launch visible particles when the pilot reveal starts.");

        for (int i = 0; i < 28; i++)
            fireworks.Movement.MoveObject(fireworks, audio, registry);

        var colors = part.Triangles.Select(t => t.Color).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var vertices = part.Triangles
            .SelectMany(t => new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            .ToList();

        Assert.IsTrue(OutroFireworksControls.FireworkSoundIds.Contains(audio.LastSoundId),
            "Firework explosion should trigger the configured firework sound.");
        Assert.AreEqual(AudioPlayMode.OneShot, audio.LastMode);
        Assert.IsTrue(audio.PlayedSoundIds.Count > 0,
            "Firework explosions should play audio on the explosion frame.");
        Assert.IsTrue(colors.Count >= 6,
            "Firework explosions should use a bright multi-color palette.");
        Assert.IsTrue(vertices.Max(v => v.x) - vertices.Min(v => v.x) > 180f,
            "Explosion particles should spread out in the sky after launch.");
        Assert.IsTrue(part.Triangles.All(t => t.noHidden == true),
            "Flat firework particles should render from both sides.");
    }

    private static void ApplyLiveMeshRotation(_3dObject obj)
    {
        var rotate = new OmegaMeshRotation();
        var rotation = (Vector3)obj.Rotation;

        foreach (var part in obj.ObjectParts)
        {
            var rotated = rotate.RotateMesh(part.Triangles, rotation.z, 'Z');
            rotated = rotate.RotateMesh(rotated, rotation.y, 'Y');
            rotated = rotate.RotateMesh(rotated, rotation.x, 'X');
            part.Triangles = rotated;
        }
    }

    private static (int Width, int Height) GetProjectedBounds(List<ProjectedTriangleMesh> triangles)
    {
        Assert.IsTrue(triangles.Count > 0, "Cannot measure bounds without projected triangles.");

        int minX = triangles.Min(t => Math.Min(t.X1, Math.Min(t.X2, t.X3)));
        int maxX = triangles.Max(t => Math.Max(t.X1, Math.Max(t.X2, t.X3)));
        int minY = triangles.Min(t => Math.Min(t.Y1, Math.Min(t.Y2, t.Y3)));
        int maxY = triangles.Max(t => Math.Max(t.Y1, Math.Max(t.Y2, t.Y3)));

        return (maxX - minX, maxY - minY);
    }

    private static (int Width, int Height) ProjectShipHullBoundsAfter(double seconds)
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        now = now.AddSeconds(seconds);
        controls.MoveObject(ship, null, null);
        ApplyLiveMeshRotation(ship);

        var converter = OmegaPerspectiveProjectorFactory.Create();
        var projected = converter.ProjectToTriangles(new List<_3dObject> { ship }, currentFrame: 1)
            .Where(t => t.PartName == "UpperPart" || t.PartName == "LowerPart" || t.PartName == "RearPart" || t.PartName == "Winglets")
            .ToList();

        return GetProjectedBounds(projected);
    }

    private static (float Width, float Height) GetShipHullLocalBoundsAfter(double seconds)
    {
        var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var controls = new OutroShipControls(() => now);
        var ship = Ship.CreateShip(null, controls);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };

        controls.MoveObject(ship, null, null);
        now = now.AddSeconds(seconds);
        controls.MoveObject(ship, null, null);

        return GetShipHullLocalBounds(ship);
    }

    private static (float Width, float Height) GetShipHullLocalBounds(I3dObject ship)
    {
        var vertices = ship.ObjectParts
            .Where(part => part.PartName == "UpperPart" || part.PartName == "LowerPart" || part.PartName == "RearPart" || part.PartName == "Winglets")
            .SelectMany(part => part.Triangles)
            .SelectMany(t => new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            .ToList();

        Assert.IsTrue(vertices.Count > 0, "Cannot measure ship hull bounds without vertices.");
        return (
            vertices.Max(v => v.x) - vertices.Min(v => v.x),
            vertices.Max(v => v.y) - vertices.Min(v => v.y));
    }

    private static Vector3 CopyVector(IVector3 vector)
    {
        return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
    }

    private static (float Width, float DepthY, float HeightZ) GetObjectLocalBounds(I3dObject obj)
    {
        var vertices = obj.ObjectParts
            .SelectMany(part => part.Triangles)
            .SelectMany(t => new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            .ToList();

        Assert.IsTrue(vertices.Count > 0, "Cannot measure object bounds without vertices.");
        return (
            vertices.Max(v => v.x) - vertices.Min(v => v.x),
            vertices.Max(v => v.y) - vertices.Min(v => v.y),
            vertices.Max(v => v.z) - vertices.Min(v => v.z));
    }

    private static float GetPartMaxZ(I3dObject obj, string partName)
    {
        var part = obj.ObjectParts.First(p => p.PartName == partName);
        return part.Triangles
            .SelectMany(t => new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            .Max(v => v.z);
    }

    private static float CalculateRenderShadeFactor(ProjectedTriangleMesh triangle)
    {
        float depthFactor01 = WorldRenderer.GetDepthFactor01(triangle.CalculatedZ);
        float angleFactor01 = Math.Clamp((triangle.TriangleAngle + 1f) * 0.5f, 0f, 1f);

        return Math.Clamp(depthFactor01 * angleFactor01, 0f, 1f);
    }

    private static Color ParseColor(string? value)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(value), "Triangle color should be set.");
        return (Color)ColorConverter.ConvertFromString(value)!;
    }

    private static bool IsDarkOceanColor(Color color)
    {
        return color.B > color.G + 35 && color.B > color.R + 45 && color.R < 20 && color.G < 50;
    }

    private static bool IsBlueDominant(Color color)
    {
        return color.B > color.G && color.B > color.R;
    }

    private static bool IsLandColor(Color color)
    {
        return color.G > color.R + 25 && color.G > color.B + 10;
    }

    private static bool IsMountainGray(Color color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max - min <= 3 && color.R >= 100;
    }

    private static Vector3 CreateSpherePoint(float latitude, float longitude, float radius = 180f)
    {
        float lat = latitude * MathF.PI / 180f;
        float lon = longitude * MathF.PI / 180f;
        float cosLat = MathF.Cos(lat);

        return new Vector3
        {
            x = radius * cosLat * MathF.Cos(lon),
            y = radius * MathF.Sin(lat),
            z = radius * cosLat * MathF.Sin(lon)
        };
    }

    private static bool IsOutroStarPart(I3dObjectPart part)
    {
        return part.PartName != null && part.PartName.StartsWith("Star_", StringComparison.Ordinal);
    }

    private static bool IsMiniaturePart(I3dObjectPart part)
    {
        return part.PartName != null
            && (part.PartName.StartsWith("MiniTree_", StringComparison.Ordinal)
             || part.PartName.StartsWith("MiniHouse_", StringComparison.Ordinal)
             || part.PartName.StartsWith("MiniIgloo_", StringComparison.Ordinal));
    }

    private static Vector3 GetPartCenter(I3dObjectPart part)
    {
        var vertices = part.Triangles
            .SelectMany(t => new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            .ToList();

        return new Vector3
        {
            x = vertices.Average(v => v.x),
            y = vertices.Average(v => v.y),
            z = vertices.Average(v => v.z)
        };
    }

    private static int GetFirstTriangleBaseAngleBucket(I3dObjectPart part)
    {
        var tri = part.Triangles.First();
        var v1 = (Vector3)tri.vert1;
        var v2 = (Vector3)tri.vert2;
        float angle = MathF.Atan2(v2.y - v1.y, v2.x - v1.x) * 180f / MathF.PI;
        if (angle < 0f) angle += 360f;
        return (int)MathF.Round(angle);
    }

    private static SurfaceData FindTileByMapId(SurfaceData[,] map, int mapId)
    {
        for (int z = 0; z < map.GetLength(0); z++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                if (map[z, x].mapId == mapId)
                    return map[z, x];
            }
        }

        Assert.Fail($"Could not find surface tile with map id {mapId}.");
        return default;
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        private readonly SoundDefinition _rocket = new()
        {
            Id = "rocket_main",
            Settings = new SoundSettings { Volume = 1f },
            Speed = new SoundSpeed { Base = 1f, Min = 0.8f, Max = 1.4f }
        };

        public SoundDefinition Get(string id)
        {
            if (id == _rocket.Id)
                return _rocket;

            if (IsFireworkSound(id))
                return CreateFireworkSound(id);

            throw new InvalidOperationException($"Unexpected sound id {id}.");
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            if (id == _rocket.Id)
            {
                definition = _rocket;
                return true;
            }

            if (IsFireworkSound(id))
            {
                definition = CreateFireworkSound(id);
                return true;
            }

            definition = new SoundDefinition();
            return false;
        }

        private static bool IsFireworkSound(string id)
        {
            return id == OutroFireworksControls.FireworkSoundId
                || OutroFireworksControls.FireworkSoundIds.Contains(id);
        }

        private static SoundDefinition CreateFireworkSound(string id)
        {
            int index = Array.IndexOf(OutroFireworksControls.FireworkSoundIds, id);
            double start = index switch
            {
                0 => 2.62,
                1 => 4.40,
                2 => 13.46,
                3 => 14.54,
                _ => 0.0
            };

            return new SoundDefinition
            {
                Id = id,
                Settings = new SoundSettings { Volume = 0.8f },
                Segments = new SoundSegments
                {
                    Start = start,
                    LoopStart = start,
                    LoopEnd = start + 0.6,
                    End = start + 0.6
                },
                Speed = new SoundSpeed { Base = 1f, Min = 0.9f, Max = 1.12f }
            };
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public string? LastSoundId { get; private set; }
        public AudioPlayMode? LastMode { get; private set; }
        public List<string> PlayedSoundIds { get; } = new();
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            LastSoundId = definition.Id;
            LastMode = mode;
            PlayedSoundIds.Add(definition.Id);
            return new CapturingAudioInstance(definition.Id, mode == AudioPlayMode.SegmentedLoop);
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
        public void SetWorldPosition(NumericsVector3 position) { }
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
