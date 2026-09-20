using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.OmegaEngineAdapters;

[TestClass]
public class FlyingObjectSurfaceClearanceHelpersTests
{
    [TestMethod]
    public void ClearanceState_HoldsLiftThenReleasesGraduallyAndRemainsIndependent()
    {
        var state = new FlyingObjectSurfaceClearanceState();
        var other = new FlyingObjectSurfaceClearanceState();
        Assert.AreEqual(100f, state.Update(100f, 0.01f));
        for (int i = 0; i < 19; i++)
            Assert.AreEqual(100f, state.Update(0f, 0.1f));
        Assert.AreEqual(0f, other.Update(0f, 0.1f));
        for (int i = 0; i < 4; i++) state.Update(0f, 0.1f);
        Assert.IsTrue(state.RetainedLift > 0f && state.RetainedLift < 100f);
        Assert.AreEqual(150f, state.Update(150f, 0.01f), "Higher terrain must lift immediately.");
    }

    [TestInitialize]
    public void InitializeForTest() => Setup();

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void VisibleArrivalBeyondSurface_UsesNearestTileAndRetainsLiftWhenTerrainIsMissing(float pitch)
    {
        var drone = new OmegaObject3D
        {
            ObjectId = 999, ObjectName = "KamikazeDrone", IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1f },
            ObjectOffsets = new Vector3 { z = 900f, y = 300f },
            ParentSurface = CreateSurfacePlane(pitch)
        };
        var state = new FlyingObjectSurfaceClearanceState();
        Assert.IsTrue(FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone, state, 0.01f));
        float lift = state.RetainedLift;
        Assert.IsTrue(lift > 0f);
        drone.ParentSurface = null;
        for (int i = 0; i < 40; i++)
        {
            drone.ObjectOffsets.y = 300f;
            FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone, state, 0.1f);
        }
        Assert.AreEqual(lift, state.RetainedLift);
        Assert.AreEqual(300f - lift, drone.ObjectOffsets.y, 0.001f);
    }

    public void Setup()
    {
        OmegaWorldViewSetup.ConfigurePitch(63f);
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3(),
            SurfaceViewportObject = new OmegaObject3D
            {
                ObjectId = 1,
                ObjectOffsets = new Vector3()
            }
        };
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void ApplyMinimumClearance_UsesLocalRotatedTileAtBothCameraAngles(float pitchDegrees)
    {
        OmegaWorldViewSetup.ConfigurePitch(pitchDegrees);
        const float targetZ = 100f;
        var surface = CreateSurfacePlane(pitchDegrees);
        var drone = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1f },
            // Object depth runs opposite Surface-local Z.
            ObjectOffsets = new Vector3 { z = -targetZ, y = 100f },
            ParentSurface = surface
        };

        bool applied = FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone);

        Assert.IsTrue(applied);
        AssertMinimumClearance(drone, surface, TerrainAvoidanceSetup.KamikazeDroneMinimumSurfaceClearance);
    }

    [TestMethod]
    public void ApplyMinimumClearance_DoesNotRestrictFreeFlightAboveFloor()
    {
        var surface = CreateSurfacePlane(70f);
        var drone = new OmegaObject3D
        {
            ObjectId = 3,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1f },
            ObjectOffsets = new Vector3 { z = 100f, y = -500f },
            ParentSurface = surface
        };
        float originalY = drone.ObjectOffsets.y;

        bool applied = FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone);

        Assert.IsFalse(applied);
        Assert.AreEqual(originalY, drone.ObjectOffsets.y);
    }

    [TestMethod]
    public void ApplyMinimumClearance_UsesSurfaceSlopeAdjustedRenderHeight()
    {
        OmegaWorldViewSetup.ConfigurePitch(63f);
        const float groundY = 200f;
        const float worldZ = 1800f;
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    vert1 = new Vector3(-500f, groundY, 0f),
                    vert2 = new Vector3(500f, groundY, 0f),
                    vert3 = new Vector3(500f, groundY, 500f)
                },
                new TriangleMeshWithColor
                {
                    vert1 = new Vector3(-500f, groundY, 0f),
                    vert2 = new Vector3(500f, groundY, 500f),
                    vert3 = new Vector3(-500f, groundY, 500f)
                }
            }
        };
        var drone = new OmegaObject3D
        {
            ObjectId = 31,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            WorldPosition = new Vector3(1f, 0f, worldZ),
            // Cancel world Z in render space so the sample stays on this tile.
            ObjectOffsets = new Vector3(0f, 0f, worldZ),
            ParentSurface = surface
        };

        bool applied = FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone);

        Assert.IsTrue(applied);
        AssertMinimumClearance(drone, surface, TerrainAvoidanceSetup.KamikazeDroneMinimumSurfaceClearance);
    }

    [TestMethod]
    public void ApplyMinimumClearance_SamplesRisingTerrainAcrossObjectFootprint()
    {
        var surface = CreateSurfacePlane(63f);
        var drone = new OmegaObject3D
        {
            ObjectId = 4,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1f },
            ObjectOffsets = new Vector3 { z = 220f, y = -20f },
            ParentSurface = surface,
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColorAndTexture>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { z = -60f },
                            vert2 = new Vector3 { x = 60f },
                            vert3 = new Vector3 { z = 60f }
                        }
                    }
                }
            }
        };

        bool applied = FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone);

        Assert.IsTrue(applied, "The highest ground under the body footprint must enforce the floor.");
    }

    [TestMethod]
    public void ApplyMinimumClearance_SamplesSurfaceTileInFrontAndBehind()
    {
        OmegaWorldViewSetup.ConfigurePitch(63f);
        int tileDepth = (int)MathF.Round(
            SurfaceSetup.tileSize * MathF.Sin(OmegaWorldViewSetup.SurfacePitchDegrees * MathF.PI / 180f));
        var surface = CreateThreeTileSurface(tileDepth);
        var drone = new OmegaObject3D
        {
            ObjectId = 5,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1f },
            ObjectOffsets = new Vector3 { z = -tileDepth * 1.5f, y = 200f },
            ParentSurface = surface
        };

        bool applied = FlyingObjectSurfaceClearanceHelpers.ApplyMinimumClearance(drone);

        Assert.IsTrue(applied, "The raised adjacent tile must be detected before the drone centre reaches it.");
        AssertMinimumClearance(drone, surface, TerrainAvoidanceSetup.KamikazeDroneMinimumSurfaceClearance);
    }

    [TestMethod]
    public void ConfiguredFlyingEnemiesHaveIndependentClearances()
    {
        Assert.AreEqual(200f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("Seeder"));
        Assert.AreEqual(120f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("KamikazeDrone"));
        Assert.AreEqual(130f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("ZeppelinBomber"));
        Assert.AreEqual(120f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("SpaceSwan"));
        Assert.AreEqual(25f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("MotherShipSmall"));
        Assert.AreEqual(105f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("MotherShipMedium"));
        Assert.AreEqual(75f, TerrainAvoidanceSetup.GetMinimumSurfaceClearance("MotherShipLarge"));
    }

    private static Surface CreateSurfacePlane(float pitchDegrees)
    {
        float radians = pitchDegrees * MathF.PI / 180f;
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);

        Vector3 Rotate(float x, float z) => new(x, z * cos, z * sin);

        return new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    vert1 = Rotate(-500f, 0f),
                    vert2 = Rotate(500f, 0f),
                    vert3 = Rotate(500f, 500f)
                },
                new TriangleMeshWithColor
                {
                    vert1 = Rotate(-500f, 0f),
                    vert2 = Rotate(500f, 500f),
                    vert3 = Rotate(-500f, 500f)
                }
            }
        };
    }

    private static void AssertMinimumClearance(OmegaObject3D drone, Surface surface, float expectedClearance)
    {
        var localWorld = drone.GetLocalWorldPosition()!;
        var surfaceOffsets = GameState.SurfaceState.SurfaceViewportObject!.ObjectOffsets;
        float objectScreenY = -localWorld.y + drone.ObjectOffsets.y
            + (float)SurfaceSlopeRenderPositionHelpers.GetCorrectionY(drone, drone.WorldPosition);
        Assert.IsTrue(SurfaceRenderAnchorHelpers.TryGetSurfaceLocalAnchor(
            new RenderPosition(
                -localWorld.x + drone.ObjectOffsets.x,
                objectScreenY,
                localWorld.z + drone.ObjectOffsets.z),
            new RenderPosition(surfaceOffsets.x, surfaceOffsets.y, surfaceOffsets.z),
            out float localX, out float localY, out float localZ));
        Assert.IsTrue(SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
            surface.RotatedSurfaceTriangles, localX, localZ,
            out _, out float groundY, out _));
        Assert.IsTrue(groundY - localY >= expectedClearance - 0.01f,
            $"Expected at least {expectedClearance:F2} units above Surface, actual {groundY - localY:F2}.");
    }

    private static Surface CreateThreeTileSurface(float tileDepth)
    {
        var triangles = new List<ITriangleMeshWithColorAndTexture>();
        AddTile(0f, tileDepth, 200f);
        AddTile(tileDepth, tileDepth * 2f, 200f);
        AddTile(tileDepth * 2f, tileDepth * 3f, 50f);
        return new Surface { RotatedSurfaceTriangles = triangles };

        void AddTile(float nearZ, float farZ, float groundY)
        {
            triangles.Add(new TriangleMeshWithColor
            {
                vert1 = new Vector3(-500f, groundY, nearZ),
                vert2 = new Vector3(500f, groundY, nearZ),
                vert3 = new Vector3(500f, groundY, farZ)
            });
            triangles.Add(new TriangleMeshWithColor
            {
                vert1 = new Vector3(-500f, groundY, nearZ),
                vert2 = new Vector3(500f, groundY, farZ),
                vert3 = new Vector3(-500f, groundY, farZ)
            });
        }
    }
}
