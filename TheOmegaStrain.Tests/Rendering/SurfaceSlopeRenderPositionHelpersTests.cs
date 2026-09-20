using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
[DoNotParallelize]
public class SurfaceSlopeRenderPositionHelpersTests
{
    private SurfaceState _previousSurface = null!;
    private float _previousPitch;

    [TestInitialize]
    public void Setup()
    {
        _previousSurface = GameState.SurfaceState;
        _previousPitch = WorldViewSetup.SurfacePitchDegrees;
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { z = 1000f }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = _previousSurface;
        WorldViewSetup.ConfigurePitch(_previousPitch);
    }

    [TestMethod]
    public void CalculateCorrectionY_IsSymmetricAroundViewportCentre()
    {
        const float distance = 400f;

        double behind = SurfaceSlopeRenderPositionHelpers.CalculateCorrectionY(
            -distance, 63f, OmegaWorldViewSetup.OriginalWorldPitchDegrees);
        double ahead = SurfaceSlopeRenderPositionHelpers.CalculateCorrectionY(
            distance, 63f, OmegaWorldViewSetup.OriginalWorldPitchDegrees);

        Assert.AreEqual(-ahead, behind, 0.0001d);
        Assert.AreNotEqual(0d, behind, 0.0001d);
    }

    [TestMethod]
    public void CalculateCorrectionY_AtOriginalPitch_PreservesShippedPlacement()
    {
        double correction = SurfaceSlopeRenderPositionHelpers.CalculateCorrectionY(
            -1500f,
            OmegaWorldViewSetup.OriginalWorldPitchDegrees,
            OmegaWorldViewSetup.OriginalWorldPitchDegrees);

        Assert.AreEqual(0d, correction, 0.0001d);
    }

    [TestMethod]
    public void Apply_ChangesOnlyRenderY_AndDoesNotMutateGameplayState()
    {
        WorldViewSetup.ConfigurePitch(63f);
        var obj = CreateFlyingObject("Seeder", worldZ: 600f);
        var before = new RenderPosition(250d, 300d, 40d);

        var after = SurfaceSlopeRenderPositionHelpers.Apply(obj, before);

        double expectedY = before.Y + SurfaceSlopeRenderPositionHelpers.CalculateCorrectionY(
            -400f, 63f, OmegaWorldViewSetup.OriginalWorldPitchDegrees);
        Assert.AreEqual(before.X, after.X, 0.0001d);
        Assert.AreEqual(expectedY, after.Y, 0.0001d);
        Assert.AreEqual(before.Z, after.Z, 0.0001d);
        Assert.AreEqual(600f, obj.WorldPosition.z, 0.0001f);
        Assert.AreEqual(125f, obj.ObjectOffsets.y, 0.0001f);
    }

    [TestMethod]
    public void GetCorrectionY_UsesParticleEmissionAnchorWithoutMutatingSource()
    {
        WorldViewSetup.ConfigurePitch(63f);
        var source = CreateFlyingObject("KamikazeDrone", worldZ: 600f);
        var particleAnchor = new Vector3 { x = 100f, y = 50f, z = 800f };

        double correction = SurfaceSlopeRenderPositionHelpers.GetCorrectionY(source, particleAnchor);

        double expected = SurfaceSlopeRenderPositionHelpers.CalculateCorrectionY(
            -200f, 63f, OmegaWorldViewSetup.OriginalWorldPitchDegrees);
        Assert.AreEqual(expected, correction, 0.0001d);
        Assert.AreEqual(600f, source.WorldPosition.z, 0.0001f);
        Assert.AreEqual(800f, particleAnchor.z, 0.0001f);
    }

    [TestMethod]
    public void Apply_ExcludesShipSurfaceBoundObjectsAndWeapons()
    {
        WorldViewSetup.ConfigurePitch(63f);
        var before = new RenderPosition(250d, 300d, 40d);
        var ship = CreateFlyingObject("Ship", worldZ: 600f);
        var surfaceBound = CreateFlyingObject("Tower", worldZ: 600f);
        surfaceBound.SurfaceBasedId = 42;
        var rocket = CreateFlyingObject("EnemyRocket", worldZ: 600f);

        Assert.AreEqual(before, SurfaceSlopeRenderPositionHelpers.Apply(ship, before));
        Assert.AreEqual(before, SurfaceSlopeRenderPositionHelpers.Apply(surfaceBound, before));
        Assert.AreEqual(before, SurfaceSlopeRenderPositionHelpers.Apply(rocket, before));
    }

    private static OmegaObject3D CreateFlyingObject(string objectName, float worldZ)
    {
        return new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = objectName,
            ParentSurface = new Surface(),
            WorldPosition = new Vector3 { x = 100f, y = 50f, z = worldZ },
            ObjectOffsets = new Vector3 { x = 25f, y = 125f, z = 15f },
            Rotation = new Vector3()
        };
    }
}
