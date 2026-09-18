using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Runtime.Rendering;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Game.Helpers;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
[DoNotParallelize]
public class ObjectShadowManagerTests
{
    private SurfaceState previousSurfaceState = null!;
    private int previousObjectIdCounter;
    private float previousPitch;
    private int previousScreenWidth;
    private int previousScreenHeight;
    private float previousShadowInwardOffset;
    private float previousShadowSideOffset;
    private float previousMotherShipShadowSizeMultiplier;
    private float previousZeppelinShadowSizeMultiplier;
    private float previousDefaultFlyingShadowSizeMultiplier;

    [TestInitialize]
    public void Setup()
    {
        previousSurfaceState = GameState.SurfaceState;
        previousObjectIdCounter = GameState.ObjectIdCounter;
        previousPitch = WorldViewSetup.SurfacePitchDegrees;
        previousScreenWidth = ScreenSetup.screenSizeX;
        previousScreenHeight = ScreenSetup.screenSizeY;
        previousShadowInwardOffset = ObjectShadowManager.TerrainShadowInwardOffset;
        previousShadowSideOffset = ObjectShadowManager.TerrainShadowSideOffset;
        previousMotherShipShadowSizeMultiplier = ObjectShadowManager.MotherShipShadowSizeMultiplier;
        previousZeppelinShadowSizeMultiplier = ObjectShadowManager.ZeppelinShadowSizeMultiplier;
        previousDefaultFlyingShadowSizeMultiplier = ObjectShadowManager.DefaultFlyingShadowSizeMultiplier;
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3(),
            SurfaceViewportObject = new OmegaObject3D
            {
                ObjectId = 1,
                ObjectName = "Surface",
                ObjectOffsets = new Vector3 { x = 0f, y = 500f, z = 0f },
                WorldPosition = new Vector3(),
                Rotation = new Vector3()
            }
        };
        GameState.ObjectIdCounter = 10;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = previousSurfaceState;
        GameState.ObjectIdCounter = previousObjectIdCounter;
        WorldViewSetup.ConfigurePitch(previousPitch);
        ScreenSetup.Initialize(previousScreenWidth, previousScreenHeight);
        ObjectShadowManager.TerrainShadowInwardOffset = previousShadowInwardOffset;
        ObjectShadowManager.TerrainShadowSideOffset = previousShadowSideOffset;
        ObjectShadowManager.MotherShipShadowSizeMultiplier = previousMotherShipShadowSizeMultiplier;
        ObjectShadowManager.ZeppelinShadowSizeMultiplier = previousZeppelinShadowSizeMultiplier;
        ObjectShadowManager.DefaultFlyingShadowSizeMultiplier = previousDefaultFlyingShadowSizeMultiplier;
    }

    [TestMethod]
    public void FreeFlyingShadow_InterpolatesGroundYInsideSurfaceTriangle()
    {
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        float oldStaticOffsetY = ObjectShadowManager.StaticOffsetY;
        try
        {
            ObjectShadowManager.StaticOffsetY = -40f;

            var surface = new Surface
            {
                RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
                {
                    new TriangleMeshWithColor
                    {
                        vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 100f, y = 100f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 0f, z = 100f }
                    }
                }
            };

            GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 25f;
            var flyingObject = CreateFreeFlyingShadowCaster(surface, x: 25f * ScreenSetup.defaultObjectZoom, y: 400f, z: 0f);
            var shadows = new List<OmegaObject3D>();

            new ObjectShadowManager().HandleObjectShadow(flyingObject, shadows);

            Assert.AreEqual(1, shadows.Count);
            var shadowVertex = shadows[0].ObjectParts[0].Triangles[0].vert1;

            Assert.AreEqual(
                25f - ObjectShadowManager.TerrainShadowSurfaceLift,
                shadowVertex.y,
                0.001f,
                "Free-flying shadows should use barycentric surface Y under the object, not the nearest tile center.");
        }
        finally
        {
            ObjectShadowManager.StaticOffsetY = oldStaticOffsetY;
        }
    }

    [TestMethod]
    public void FreeFlyingShadow_IsSkippedWhenObjectIsOutsideSurfaceBounds()
    {
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                    vert2 = new Vector3 { x = 100f, y = 100f, z = 0f },
                    vert3 = new Vector3 { x = 0f, y = 0f, z = 100f }
                }
            }
        };

        // Mirrors a mother ship during descent: spawned far behind the tile grid
        // (DescentSpawnOffsetZ = -1500), so no ground exists under it.
        var flyingObject = CreateFreeFlyingShadowCaster(surface, x: 25f, y: 400f, z: 1500f);
        var shadows = new List<OmegaObject3D>();

        new ObjectShadowManager().HandleObjectShadow(flyingObject, shadows);

        Assert.AreEqual(
            0,
            shadows.Count,
            "Objects outside the tile grid must not snap their shadow to the nearest edge tile.");
    }

    [TestMethod]
    public void FreeFlyingShadow_StaysOnTerrainDespiteVerticalShadowOffset()
    {
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        float oldStaticOffsetY = ObjectShadowManager.StaticOffsetY;
        try
        {
            ObjectShadowManager.StaticOffsetY = 0f;

            // Horizon row (smallest Y) of this grid is y = 0.
            var surface = new Surface
            {
                RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
                {
                    new TriangleMeshWithColor
                    {
                        vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 100f, y = 100f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 0f, z = 100f }
                    }
                }
            };

            GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 25f;
            var flyingObject = CreateFreeFlyingShadowCaster(surface, x: 25f * ScreenSetup.defaultObjectZoom, y: 400f, z: 0f);

            // A large negative offset would drive the anchor far above the
            // horizon, making the shadow float in the sky behind the terrain.
            flyingObject.ShadowOffset = new Vector3 { x = 0f, y = -900f, z = 0f };

            var shadows = new List<OmegaObject3D>();

            new ObjectShadowManager().HandleObjectShadow(flyingObject, shadows);

            Assert.AreEqual(1, shadows.Count);
            var shadowVertex = shadows[0].ObjectParts[0].Triangles[0].vert1;

            Assert.AreEqual(
                25f - ObjectShadowManager.TerrainShadowSurfaceLift,
                shadowVertex.y,
                0.001f,
                "Per-vertex terrain sampling must override an offset that would leave the shadow floating.");
        }
        finally
        {
            ObjectShadowManager.StaticOffsetY = oldStaticOffsetY;
        }
    }

    [DataTestMethod]
    [DataRow(63f, 0f)]
    [DataRow(63f, 90f)]
    [DataRow(70f, 0f)]
    [DataRow(70f, 90f)]
    public void ShipAndSeeder_UseComparableFootprintsAtEqualGroundClearance(float pitch, float heading)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 400f;
        float factor = ScreenSetup.perspectiveAdjustment / (ScreenSetup.perspectiveAdjustment + 400f) * ScreenSetup.defaultObjectZoom;
        foreach (float clearance in new[] { 0f, 100f, 300f, 1000f })
        {
            foreach (bool isShip in new[] { true, false })
            {
                var caster = isShip ? Ship.CreateShip(surface) : Seeder.CreateSeeder(surface);
                caster.ObjectName = isShip ? "Ship" : "Seeder";
                caster.WorldPosition = new Vector3();
                caster.ObjectOffsets = new Vector3(0f, 500f - clearance * factor, 400f);
                caster.Rotation = new Vector3(pitch, 0f, heading);
                new ObjectFrameTransformer().RotateObjectGeometry(caster);
                // Match collision-centre clearance, not differently authored origins.
                var centre = ObjectCollisionGeometry.GetLocalCrashCenter(caster);
                caster.ObjectOffsets = new Vector3(-centre.x, 500f - clearance * factor - centre.y, 400f - centre.z);
                var shadows = new List<OmegaObject3D>();
                new ObjectShadowManager().HandleObjectShadow(caster, shadows);
                Assert.AreEqual(1, shadows.Count);
                float inwardZ = -ObjectShadowManager.TerrainShadowInwardOffset;
                var anchor = new Vector3(ObjectShadowManager.TerrainShadowSideOffset,
                    inwardZ / MathF.Tan(pitch * MathF.PI / 180f) - ObjectShadowManager.TerrainShadowSurfaceLift, inwardZ);
                float radius = shadows[0].ObjectParts[0].Triangles
                    .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
                    .Max(v => MathF.Sqrt(GeometryMath.GetDistanceSquared(v, anchor)));
                float expectedRadius = 40f * MathF.Max(0.2f, 1f - clearance * 0.0002f) * 1.15f;
                Assert.AreEqual(expectedRadius, radius, 0.002f,
                    $"{caster.ObjectName}: equal clearance must produce the same reference radius, independent of authored mesh size.");
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(FlyingShadowSyncCases), DynamicDataSourceType.Method)]
    public void FlyingCasters_CoincidentCollisionCentresHaveCoincidentShadowAnchors(
        float pitch, float heading, int width, int height, string name)
    {
        ScreenSetup.Initialize(width, height);
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        var surfaceOffsets = new Vector3(70f, 500f, 400f);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets = surfaceOffsets;
        var ship = Ship.CreateShip(surface);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3(); // Ship is screen-fixed, the other caster is world-positioned.
        ship.Rotation = new Vector3(pitch, 0f, heading);
        var caster = CreateShadowCaster(name, surface);
        caster.ObjectName = name;
        caster.ObjectOffsets = new Vector3(35f, -200f, 600f);
        caster.Rotation = new Vector3(pitch, 0f, heading + 45f);
        var transformer = new ObjectFrameTransformer();
        transformer.RotateObjectGeometry(ship);
        transformer.RotateObjectGeometry(caster);
        var shipLocalCentre = ObjectCollisionGeometry.GetLocalCrashCenter(ship);
        var casterLocalCentre = ObjectCollisionGeometry.GetLocalCrashCenter(caster);
        var shipGeometry = SnapshotVertices(ship);
        var casterGeometry = SnapshotVertices(caster);
        var shipBoxes = SnapshotCrashBoxes(ship);
        var casterBoxes = SnapshotCrashBoxes(caster);

        foreach (var camera in new[] { new Vector3(95100f, 0f, 95200f), new Vector3(95800f, 40f, 94900f) })
        foreach (float depth in new[] { name == "Ship" || name == "Seeder" ? -1100f : -400f, 400f, 900f })
        {
            GameState.SurfaceState.GlobalMapPosition = camera;
            ship.ObjectOffsets = new Vector3(0f, 150f, depth);
            // Place the two real, differently shaped/rotated collision centres
            // together. Raw WorldPosition equality is NOT sufficient in Omega.
            var shipCentre = VectorMath.Add(ship.ObjectOffsets, shipLocalCentre);
            caster.WorldPosition = new Vector3(
                camera.x + shipCentre.x - caster.ObjectOffsets.x - casterLocalCentre.x,
                camera.y + shipCentre.y - caster.ObjectOffsets.y - casterLocalCentre.y,
                camera.z + caster.ObjectOffsets.z + casterLocalCentre.z - shipCentre.z);
            var casterWorldBefore = (caster.WorldPosition.x, caster.WorldPosition.y, caster.WorldPosition.z);
            var casterOffsetsBefore = (caster.ObjectOffsets.x, caster.ObjectOffsets.y, caster.ObjectOffsets.z);
            Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(caster, 0, 0, out _, out _, out _));
            var actualCasterCentre = VectorMath.Add(CrashBoxTransform.GetEffectiveCrashOffset(caster,
                (x, y, z) => new Vector3(x, y, z)), casterLocalCentre);
            AssertPointEqual(shipCentre, actualCasterCentre);

            var shipShadows = new List<OmegaObject3D>();
            var casterShadows = new List<OmegaObject3D>();
            var manager = new ObjectShadowManager();
            manager.HandleObjectShadow(ship, shipShadows);
            manager.HandleObjectShadow(caster, casterShadows);
            Assert.AreEqual(1, shipShadows.Count);
            Assert.AreEqual(1, casterShadows.Count);
            var shipAnchor = ShadowBoundsCentre(shipShadows[0]);
            var casterAnchor = ShadowBoundsCentre(casterShadows[0]);
            AssertPointEqual(shipAnchor, casterAnchor);

            // Independent flat-plane expectation, including the renderer's near
            // depth cap. This also rejects two equally wrong, camera-fixed anchors.
            float perspective = ScreenSetup.perspectiveAdjustment;
            float cappedDepth = MathF.Max(shipCentre.z, perspective / 2.5f - perspective);
            float expectedX = (shipCentre.x - surfaceOffsets.x) * (perspective + cappedDepth)
                / (perspective * ScreenSetup.defaultObjectZoom) + ObjectShadowManager.TerrainShadowSideOffset;
            float expectedZ = surfaceOffsets.z - cappedDepth - ObjectShadowManager.TerrainShadowInwardOffset;
            float expectedY = expectedZ / MathF.Tan(pitch * MathF.PI / 180f) - ObjectShadowManager.TerrainShadowSurfaceLift;
            AssertPointEqual(new Vector3(expectedX, expectedY, expectedZ), shipAnchor);

            Assert.IsTrue(ProjectionMath.TryProjectVertex(shipAnchor, surfaceOffsets.x, surfaceOffsets.y, surfaceOffsets.z,
                perspective, ScreenSetup.defaultObjectZoom, out var shipScreen));
            Assert.IsTrue(ProjectionMath.TryProjectVertex(casterAnchor, surfaceOffsets.x, surfaceOffsets.y, surfaceOffsets.z,
                perspective, ScreenSetup.defaultObjectZoom, out var casterScreen));
            Assert.AreEqual(shipScreen.x, casterScreen.x, 0.03);
            Assert.AreEqual(shipScreen.y, casterScreen.y, 0.03);
            Assert.AreEqual(casterWorldBefore, (caster.WorldPosition.x, caster.WorldPosition.y, caster.WorldPosition.z));
            Assert.AreEqual(casterOffsetsBefore, (caster.ObjectOffsets.x, caster.ObjectOffsets.y, caster.ObjectOffsets.z));
        }
        CollectionAssert.AreEqual(shipGeometry, SnapshotVertices(ship));
        CollectionAssert.AreEqual(casterGeometry, SnapshotVertices(caster));
        CollectionAssert.AreEqual(shipBoxes, SnapshotCrashBoxes(ship));
        CollectionAssert.AreEqual(casterBoxes, SnapshotCrashBoxes(caster));
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void SeederShadow_FollowsStationaryCasterWhenCameraMovesAndReturns(float pitch)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        var seeder = Seeder.CreateSeeder(surface);
        seeder.ObjectName = "Seeder";
        seeder.WorldPosition = new Vector3(95100f, 0f, 95200f);
        seeder.ObjectOffsets = new Vector3(0f, 100f, 400f);
        seeder.Rotation = new Vector3(pitch, 0f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(seeder);
        var anchors = new List<IVector3>();
        foreach (float travel in new[] { 0f, 150f, 0f })
        {
            GameState.SurfaceState.GlobalMapPosition = new Vector3(95100f + travel, 0f, 95200f + travel);
            var shadows = new List<OmegaObject3D>();
            new ObjectShadowManager().HandleObjectShadow(seeder, shadows);
            Assert.AreEqual(1, shadows.Count);
            anchors.Add(ShadowBoundsCentre(shadows[0]));
        }
        Assert.IsTrue(anchors[1].x < anchors[0].x);
        Assert.AreEqual(anchors[0].z - 150f, anchors[1].z, 0.02f);
        AssertPointEqual(anchors[0], anchors[2]);
        Assert.AreEqual(95100f, seeder.WorldPosition.x);
        Assert.AreEqual(95200f, seeder.WorldPosition.z);
    }

    [DataTestMethod]
    [DataRow(63f, 1500, 1024, "KamikazeDrone")]
    [DataRow(70f, 1500, 1024, "KamikazeDrone")]
    [DataRow(63f, 2560, 1440, "KamikazeDrone")]
    [DataRow(70f, 2560, 1440, "KamikazeDrone")]
    [DataRow(63f, 1500, 1024, "Seeder")]
    [DataRow(70f, 1500, 1024, "Seeder")]
    [DataRow(63f, 2560, 1440, "Seeder")]
    [DataRow(70f, 2560, 1440, "Seeder")]
    public void FreeFlyingShadow_WithoutInwardOffset_ProjectsWithCasterPositionAndDepthWhileCameraMoves(float pitch, int width, int height, string name)
    {
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        ScreenSetup.Initialize(width, height);
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets = new Vector3(70f, 100f, 400f);
        var caster = CreateFreeFlyingShadowCaster(surface, 90f, -250f, 450f);
        caster.ObjectName = name;
        caster.WorldPosition = new Vector3(1000f, 0f, 1000f);
        // A real triangle with its first vertex at the object's origin. This lets
        // us compare the rendered anchor, not a perspective-distorted centroid.
        var footprint = caster.ObjectParts[0].Triangles[0];
        footprint.vert2 = new Vector3(10f, 0f, 0f);
        footprint.vert3 = new Vector3(0f, 10f, 0f);
        caster.Rotation = new Vector3(pitch, 0f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        caster.ObjectParts.Add(new OmegaObjectPart3D
        {
            PartName = "CasterBody", IsVisible = true,
            Triangles = new List<ITriangleMeshWithColorAndTexture> { footprint }
        });

        var projector = OmegaPerspectiveProjectorFactory.Create();
        double? previousShadowY = null;
        double? previousShadowRhw = null;
        foreach (var camera in new[] { (X: -220f, Z: -400f), (X: 0f, Z: 0f), (X: 220f, Z: 400f) })
        {
            GameState.SurfaceState.GlobalMapPosition = new Vector3(1000f + camera.X, 0f, 1000f + camera.Z);
            var shadows = new List<OmegaObject3D>();
            new ObjectShadowManager().HandleObjectShadow(caster, shadows);
            Assert.AreEqual(1, shadows.Count);
            var triangles = projector.ProjectToTriangles(new List<OmegaObject3D> { caster, shadows[0] }, null);
            var body = triangles.Single(t => t.PartName == "CasterBody");
            // Airborne casters centre the silhouette on the reference point rather
            // than retaining this fixture's first vertex as the anchor.
            var anchor = ShadowBoundsCentre(shadows[0]);
            var viewport = new ProjectionViewport(width, height, ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom);
            Assert.IsTrue(ProjectionMath.TryProjectVertexWithReciprocalW(anchor,
                width / 2 + 70, height / 2 + 100, 400, viewport, out var shadowScreen, out var shadowRhw));
            Assert.AreEqual(body.X1, shadowScreen.x, 1.0, "Shadow must follow the caster AFTER perspective projection.");
            Assert.AreEqual(body.Rhw1, shadowRhw, 0.00001f,
                "Shadow and caster must recede together, not move in opposite depth directions.");
            if (previousShadowY.HasValue)
            {
                Assert.IsTrue(shadowScreen.y < previousShadowY.Value, "As the object recedes, its shadow must move back toward the horizon.");
                Assert.IsTrue(shadowRhw < previousShadowRhw!.Value, "The shadow must shrink in perspective as the caster moves away.");
            }
            previousShadowY = shadowScreen.y;
            previousShadowRhw = shadowRhw;
        }
    }

    [DataTestMethod]
    [DataRow(63f, -120f, -150f)]
    [DataRow(63f, 0f, 0f)]
    [DataRow(63f, 120f, 150f)]
    [DataRow(70f, -120f, -150f)]
    [DataRow(70f, 0f, 0f)]
    [DataRow(70f, 120f, 150f)]
    public void FreeFlyingShadow_SamplesGroundAtPerspectiveCorrectAnchor(float pitch, float cameraX, float cameraZ)
    {
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets = new Vector3(70f, 500f, 400f);
        GameState.SurfaceState.GlobalMapPosition = new Vector3(1000f + cameraX, 0f, 1000f + cameraZ);
        var caster = CreateFreeFlyingShadowCaster(surface, 90f, 400f, 450f);
        caster.WorldPosition = new Vector3(1000f, 0f, 1000f);
        SetRotatedFootprint(caster, pitch);

        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);

        Assert.AreEqual(1, shadows.Count);
        var center = ShadowBoundsCentre(shadows[0]);
        float objectX = 90f - cameraX;
        float objectZ = 450f + cameraZ;
        float factor = ScreenSetup.perspectiveAdjustment / (ScreenSetup.perspectiveAdjustment + objectZ) * ScreenSetup.defaultObjectZoom;
        float groundZ = 400f - objectZ;
        float groundY = groundZ / MathF.Tan(pitch * MathF.PI / 180f);
        Assert.AreEqual((objectX - 70f) / factor, center.x, 0.001f, "Vertex positions are scaled, whereas object translations are not.");
        Assert.AreEqual(groundZ, center.z, 0.001f, "Surface vertex Z has the opposite sign to object depth.");
        Assert.AreEqual(groundY - ObjectShadowManager.TerrainShadowSurfaceLift, center.y, 0.001f, "Sample ground at the corrected X/Z.");
    }

    [DataTestMethod]
    [DataRow(-1800d)]
    [DataRow(-900d)]
    [DataRow(0d)]
    [DataRow(900d)]
    public void SurfaceLocalShadowAnchor_MatchesProjectionIncludingNearDepthCap(double casterDepth)
    {
        ScreenSetup.Initialize(1500, 1024);
        var casterPosition = new RenderPosition(300, 100, casterDepth);
        var surfacePosition = new RenderPosition(70, 500, 400);
        Assert.IsTrue(OmegaPerspectiveProjectorFactory.TryGetSurfaceLocalShadowAnchor(
            casterPosition, surfacePosition, out float x, out float y, out float z));
        // At the 2.5x cap, the actual renderer uses -900 rather than a lower depth.
        double expectedDepth = Math.Max(-900d, casterDepth);
        var viewport = new ProjectionViewport(1500, 1024, 1500, 2);
        Assert.IsTrue(ProjectionMath.TryProjectVertexWithReciprocalW(new Vector3(),
            casterPosition.X, casterPosition.Y, expectedDepth, viewport, out var casterPoint, out var casterRhw));
        Assert.IsTrue(ProjectionMath.TryProjectVertexWithReciprocalW(new Vector3(x, y, z),
            surfacePosition.X, surfacePosition.Y, surfacePosition.Z, viewport, out var shadowPoint, out var shadowRhw));
        Assert.AreEqual(casterPoint.x, shadowPoint.x, 0.001d);
        Assert.AreEqual(casterPoint.y, shadowPoint.y, 0.001d);
        Assert.AreEqual(casterRhw, shadowRhw, 0.00001f);
    }

    [DataTestMethod]
    [DataRow(63f, 1500, 1024)]
    [DataRow(70f, 1500, 1024)]
    [DataRow(63f, 2560, 1440)]
    [DataRow(70f, 2560, 1440)]
    public void FreeFlyingShadow_SizeUsesLocalGroundClearanceNotCameraDepth(float pitch, int width, int height)
    {
        ScreenSetup.Initialize(width, height);
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        var surfaceOffsets = GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets;
        surfaceOffsets.y = 100f;
        surfaceOffsets.z = 400f;
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 0f, 450f);
        caster.WorldPosition = new Vector3(1000f, 0f, 1000f);
        SetRotatedFootprint(caster, pitch);
        float originalWidth = caster.ObjectParts[0].Triangles[0].vert2.x - caster.ObjectParts[0].Triangles[0].vert1.x;

        foreach (float cameraZ in new[] { -400f, 0f, 400f })
        {
            GameState.SurfaceState.GlobalMapPosition = new Vector3(1000f, 0f, 1000f + cameraZ);
            float objectDepth = cameraZ + caster.ObjectOffsets.z;
            float factor = ScreenSetup.perspectiveAdjustment / (ScreenSetup.perspectiveAdjustment + objectDepth) * ScreenSetup.defaultObjectZoom;
            float groundY = (surfaceOffsets.z - objectDepth) / MathF.Tan(pitch * MathF.PI / 180f);
            foreach (float clearance in new[] { 0f, 100f, 300f, 1000f })
            {
                // Position the caster at a known height in Surface vertex units,
                // then project it to the screen-offset convention used by objects.
                caster.ObjectOffsets.y = surfaceOffsets.y + (groundY - clearance) * factor;
                var shadows = new List<OmegaObject3D>();
                new ObjectShadowManager().HandleObjectShadow(caster, shadows);
                Assert.AreEqual(1, shadows.Count);
                var triangle = shadows[0].ObjectParts[0].Triangles[0];
                float expectedScale = MathF.Max(0.2f, 1.8f - clearance * 0.0002f) * 0.65f;
                Assert.AreEqual(originalWidth * expectedScale, triangle.vert2.x - triangle.vert1.x, 0.001f,
                    $"Same ground clearance must give the same silhouette scale: cameraZ={cameraZ}, clearance={clearance}.");
            }
        }
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void FreeFlyingShadow_RotatesFootprintOnlyOnceAndDoesNotMutateCaster(float pitch)
    {
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        WorldViewSetup.ConfigurePitch(pitch);
        var caster = CreateFreeFlyingShadowCaster(CreateTiltedFlatSurface(pitch), 0f, 400f, 0f);
        SetRotatedFootprint(caster, pitch);
        var original = caster.ObjectParts[0].Triangles[0];
        var originalVertices = new[] { original.vert1, original.vert2, original.vert3 };
        var before = originalVertices.Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
        var footprintCentre = GeometryMath.GetCenterOfBox(originalVertices.ToList());
        var originalOffsets = new Vector3(caster.ObjectOffsets.x, caster.ObjectOffsets.y, caster.ObjectOffsets.z);
        var shadows = new List<OmegaObject3D>();

        var manager = new ObjectShadowManager();
        manager.HandleObjectShadow(caster, shadows);
        manager.HandleObjectShadow(caster, shadows);

        Assert.AreEqual(2, shadows.Count);
        float scale = (ObjectShadowManager.BaseScale * ObjectShadowManager.FreeFlyingShadowScale
            - (100f / ScreenSetup.defaultObjectZoom) * ObjectShadowManager.AltitudeShrinkFactor)
            * ObjectShadowManager.DefaultFlyingShadowSizeMultiplier;
        foreach (var shadow in shadows)
        {
            var projected = shadow.ObjectParts[0].Triangles[0];
            var vertices = new[] { projected.vert1, projected.vert2, projected.vert3 };
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.AreEqual((before[i].x - footprintCentre.x) * scale, vertices[i].x, 0.001f);
                Assert.AreEqual((before[i].y - footprintCentre.y) * scale - ObjectShadowManager.TerrainShadowSurfaceLift, vertices[i].y, 0.001f);
                Assert.AreEqual((before[i].z - footprintCentre.z) * scale, vertices[i].z, 0.001f);
                Assert.AreEqual(before[i].x, originalVertices[i].x);
                Assert.AreEqual(before[i].y, originalVertices[i].y);
                Assert.AreEqual(before[i].z, originalVertices[i].z);
            }
        }
        Assert.AreEqual(originalOffsets.x, caster.ObjectOffsets.x);
        Assert.AreEqual(originalOffsets.y, caster.ObjectOffsets.y);
        Assert.AreEqual(originalOffsets.z, caster.ObjectOffsets.z);
    }

    [DataTestMethod]
    [DataRow("PolarBear")]
    public void SurfaceBoundShadows_KeepAnchorAndSilhouetteProportions(string name)
    {
        var surface = CreateTiltedFlatSurface(WorldViewSetup.SurfacePitchDegrees);
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 400f, 0f);
        caster.ObjectName = name;
        SetRotatedFootprint(caster, WorldViewSetup.SurfacePitchDegrees);
        caster.SurfaceBasedId = 1;
        surface.RotatedSurfaceTriangles[0].landBasedPosition = 1;
        var center = TriangleCenter(surface.RotatedSurfaceTriangles[0]);
        float baseX = center.x + ObjectShadowManager.TowerShadowNudgeX;
        float baseY = center.y - ObjectShadowManager.TowerShadowSurfaceLift + ObjectShadowManager.TowerShadowNudgeY;
        float baseZ = center.z + ObjectShadowManager.TowerShadowNudgeZ;
        float horizonY = surface.RotatedSurfaceTriangles.Min(t => MathF.Min(t.vert1.y, MathF.Min(t.vert2.y, t.vert3.y)));
        baseY = MathF.Max(baseY, horizonY + ObjectShadowManager.ShadowHorizonMargin);
        var expected = ObjectShadowProjectionMath.ProjectModelTriangleShadow(caster.ObjectParts[0].Triangles[0],
            new ObjectShadowProjectionOptions
            {
                ShadowBaseX = baseX, ShadowBaseY = baseY, ShadowBaseZ = baseZ,
                ShadowOffsetX = 0f,
                ShadowOffsetY = 0f,
                ShadowOffsetZ = ObjectShadowManager.StaticOffsetZ,
                Scale = MathF.Max(ObjectShadowManager.MinScale, ObjectShadowManager.BaseScale - 100f * ObjectShadowManager.AltitudeShrinkFactor),
                ShadowSlopeX = ObjectShadowManager.ShadowSlopeX,
                ShadowSlopeY = ObjectShadowManager.ShadowSlopeY,
                VertexStretchBoost = ObjectShadowManager.VertexStretchBoost,
                SurfaceTiltDegrees = WorldViewSetup.SurfacePitchDegrees
            });

        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);

        Assert.AreEqual(1, shadows.Count);
        var result = shadows[0].ObjectParts[0].Triangles[0];
        var expectedVertices = new[] { expected.Vertex1, expected.Vertex2, expected.Vertex3 };
        var actualVertices = new[] { result.vert1, result.vert2, result.vert3 };
        for (int i = 0; i < actualVertices.Length; i++)
        {
            Assert.AreEqual(expectedVertices[i].x, actualVertices[i].x, 0.001f);
            Assert.AreEqual(expectedVertices[i].y, actualVertices[i].y, 0.001f);
            Assert.AreEqual(expectedVertices[i].z, actualVertices[i].z, 0.001f);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(FlyingShadowTerrainCases), DynamicDataSourceType.Method)]
    public void FlyingShadow_FollowsRidgeAtEveryVertexWithoutChangingCaster(float pitch, string name)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        var caster = CreateShadowCaster(name, surface);
        caster.ObjectName = name;
        caster.WorldPosition = new Vector3();
        caster.ObjectOffsets = new Vector3(0f, -300f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        var before = SnapshotVertices(caster);
        var manager = new ObjectShadowManager();
        float sine = MathF.Sin(pitch * MathF.PI / 180f);
        float tangent = MathF.Tan(pitch * MathF.PI / 180f);

        var beforeBoxes = SnapshotCrashBoxes(caster);
        // Include enough terrain for the large mothership footprint as well.
        const float ridgeWidth = 2000f;
        // Replace the terrain between frames, as the real scrolling Surface does.
        foreach (float ridgeHeight in new[] { 50f, 100f, 50f })
        {
            var rotation = new OmegaMeshRotation();
            IVector3 Point(float x, float y) => rotation.RotatePoint(pitch,
                new Vector3(x, y, ridgeHeight * (1f - MathF.Abs(x) / ridgeWidth)), 'X');
            var tiles = new List<ITriangleMeshWithColorAndTexture>();
            foreach (float x in new[] { -ridgeWidth, 0f })
            {
                tiles.Add(new TriangleMeshWithColor
                {
                    vert1 = Point(x, -2000f), vert2 = Point(x + ridgeWidth, -2000f), vert3 = Point(x + ridgeWidth, 2000f)
                });
                tiles.Add(new TriangleMeshWithColor
                {
                    vert1 = Point(x, -2000f), vert2 = Point(x + ridgeWidth, 2000f), vert3 = Point(x, 2000f)
                });
            }
            surface.RotatedSurfaceTriangles = tiles;
            var shadows = new List<OmegaObject3D>();
            manager.HandleObjectShadow(caster, shadows);
            Assert.AreEqual(1, shadows.Count);
            var vertices = shadows[0].ObjectParts[0].Triangles
                .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }).ToArray();
            Assert.IsTrue(vertices.Max(v => v.x) > vertices.Min(v => v.x));
            foreach (var vertex in vertices)
            {
                // Independent analytic height of this piecewise-linear ridge
                // AFTER X rotation: y = z*cot(pitch) - elevation/sin(pitch).
                float elevation = ridgeHeight * (1f - MathF.Abs(vertex.x) / ridgeWidth);
                float groundY = vertex.z / tangent - elevation / sine;
                Assert.AreEqual(groundY - 10f, vertex.y, 0.002f,
                    "Each corner must follow its own terrain height, not a plane at the centre.");
            }
            CollectionAssert.AreEqual(before, SnapshotVertices(caster), "Shared body, guide and shadow geometry must remain untouched.");
            CollectionAssert.AreEqual(beforeBoxes, SnapshotCrashBoxes(caster));
            Assert.AreEqual(-300f, caster.ObjectOffsets.y);
            Assert.AreEqual(0f, caster.WorldPosition.y);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SeederShadow_OmitsFacesOutsideActualTerrainEvenInsideBoundingBox(bool reverseWinding)
    {
        WorldViewSetup.ConfigurePitch(63f);
        var tile = new TriangleMeshWithColor
        {
            vert1 = new Vector3(-200f, 0f, -200f),
            vert2 = new Vector3(200f, 0f, -200f),
            vert3 = new Vector3(-200f, 0f, 200f)
        };
        if (reverseWinding)
            (tile.vert2, tile.vert3) = (tile.vert3, tile.vert2);
        var surface = new Surface { RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture> { tile } };
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 120f;
        var caster = CreateFreeFlyingShadowCaster(surface, 120f * ScreenSetup.defaultObjectZoom, 400f, 0f);
        caster.ObjectName = "Seeder";
        SetRotatedFootprint(caster, 63f);
        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);
        Assert.AreEqual(0, shadows.Count, "The bounding box contains this shadow, but the triangle does not. No nearest-centre fallback.");

        // Also verify that valid ground works with either winding.
        // Keep the final anchor at -80 after applying the inward offset, so
        // the complete silhouette is inside this deliberately small test tile.
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = -80f + ObjectShadowManager.TerrainShadowInwardOffset;
        caster.ObjectOffsets.x = -80f * ScreenSetup.defaultObjectZoom;
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);
        Assert.AreEqual(1, shadows.Count);
        foreach (var vertex in shadows[0].ObjectParts[0].Triangles.SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }))
            Assert.AreEqual(-10f, vertex.y, 0.001f);
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void SeederShadow_KeepsValidFacesWhenOtherFacesCrossSurfaceEdge(float pitch)
    {
        // Isolate edge handling from the configurable placement nudge.
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    vert1 = new Vector3(-200f, 0f, -200f),
                    vert2 = new Vector3(200f, 0f, -200f),
                    vert3 = new Vector3(-200f, 0f, 200f)
                }
            }
        };
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 400f, 0f);
        caster.ObjectName = "Seeder";
        caster.ObjectParts[0].Triangles = new List<ITriangleMeshWithColorAndTexture>
        {
            new TriangleMeshWithColor
            {
                vert1 = new Vector3(-20f, -20f, 0f), vert2 = new Vector3(20f, 20f, 0f), vert3 = new Vector3(-20f, -10f, 0f)
            },
            new TriangleMeshWithColor
            {
                vert1 = new Vector3(-20f, -20f, 0f), vert2 = new Vector3(-10f, -20f, 0f), vert3 = new Vector3(-20f, -10f, 0f)
            }
        };
        caster.Rotation = new Vector3(pitch, 0f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);
        Assert.AreEqual(1, shadows.Count);
        Assert.AreEqual(1, shadows[0].ObjectParts[0].Triangles.Count, "Retain the valid face after omitting the incomplete first face.");
        Assert.AreEqual(2, caster.ObjectParts[0].Triangles.Count, "Never remove faces from the shared silhouette.");
    }

    [DataTestMethod]
    [DataRow(63f, "Seeder")]
    [DataRow(70f, "Seeder")]
    [DataRow(63f, "Ship")]
    [DataRow(70f, "Ship")]
    [DataRow(63f, "AttackShip")]
    [DataRow(70f, "AttackShip")]
    [DataRow(63f, "KamikazeDrone")]
    [DataRow(70f, "KamikazeDrone")]
    [DataRow(63f, "MotherShipSmall")]
    [DataRow(70f, "MotherShipSmall")]
    [DataRow(63f, "MotherShipMedium")]
    [DataRow(70f, "MotherShipMedium")]
    [DataRow(63f, "MotherShipLarge")]
    [DataRow(70f, "MotherShipLarge")]
    [DataRow(63f, "ZeppelinBomber")]
    [DataRow(70f, "ZeppelinBomber")]
    [DataRow(63f, "BomberBomb")]
    [DataRow(70f, "BomberBomb")]
    [DataRow(63f, "SpaceSwan")]
    [DataRow(70f, "SpaceSwan")]
    [DataRow(63f, "DroneDecoy")]
    [DataRow(70f, "DroneDecoy")]
    [DataRow(63f, "PowerUp")]
    [DataRow(70f, "PowerUp")]
    [DataRow(63f, "JumpingFish")]
    [DataRow(70f, "JumpingFish")]
    public void FlyingShadow_InwardOffsetPrecedesProjectionAndDoesNotChangeHeightScaling(float pitch, string name)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 400f;
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 100f, 0f);
        caster.ObjectName = name;
        SetRotatedFootprint(caster, pitch);
        var original = SnapshotVertices(caster);
        var manager = new ObjectShadowManager();

        foreach (float height in new[] { 0f, 300f, 1000f })
        {
            caster.ObjectOffsets.y = 100f - height;
            foreach (float depth in new[] { -200f, 0f, 200f })
            {
                caster.ObjectOffsets.z = depth;
                ObjectShadowManager.TerrainShadowInwardOffset = 0f;
                ObjectShadowManager.TerrainShadowSideOffset = 0f;
                var baseline = new List<OmegaObject3D>();
                manager.HandleObjectShadow(caster, baseline);
                ObjectShadowManager.TerrainShadowInwardOffset = 200f;
                ObjectShadowManager.TerrainShadowSideOffset = -12f;
                var shifted = new List<OmegaObject3D>();
                manager.HandleObjectShadow(caster, shifted);
                Assert.AreEqual(1, baseline.Count);
                Assert.AreEqual(1, shifted.Count);
                var before = SnapshotVertices(baseline[0]);
                var after = SnapshotVertices(shifted[0]);
                Assert.AreEqual(before.Length, after.Length);
                for (int i = 0; i < before.Length; i++)
                {
                    Assert.AreEqual(before[i].X - 12f, after[i].X, 0.002f);
                    Assert.AreEqual(before[i].Z - 200f, after[i].Z, 0.002f,
                        "The anchor offset must not be multiplied by the height-dependent footprint scale.");
                    float lift = ObjectShadowManager.TerrainShadowSurfaceLift;
                    Assert.AreEqual(after[i].Z / MathF.Tan(pitch * MathF.PI / 180f) - lift, after[i].Y, 0.002f,
                        "Sample terrain at the shifted point before applying the lift.");
                }
                var viewport = new ProjectionViewport(1500, 1024, ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom);
                Assert.IsTrue(ProjectionMath.TryProjectVertexWithReciprocalW(new Vector3(before[0].X, before[0].Y, before[0].Z),
                    0, 500, 400, viewport, out var oldScreen, out var oldDepthScale));
                Assert.IsTrue(ProjectionMath.TryProjectVertexWithReciprocalW(new Vector3(after[0].X, after[0].Y, after[0].Z),
                    0, 500, 400, viewport, out var newScreen, out var newDepthScale));
                Assert.IsTrue(newScreen.y < oldScreen.y, "The inward offset must move the foreground shadow UP on screen in both angles.");
                Assert.IsTrue(newDepthScale < oldDepthScale,
                    "Subtracting Surface-vertex Z must move the shadow farther from the camera, not closer.");
                Assert.AreEqual(100f - height, caster.ObjectOffsets.y);
                Assert.AreEqual(depth, caster.ObjectOffsets.z);
            }
        }
        CollectionAssert.AreEqual(original, SnapshotVertices(caster));
    }

    [DataTestMethod]
    [DataRow(63f, 0f)]
    [DataRow(63f, 45f)]
    [DataRow(63f, 90f)]
    [DataRow(70f, 0f)]
    [DataRow(70f, 45f)]
    [DataRow(70f, 90f)]
    public void ShipAndSeeder_ShadowsAreSmallerThanRenderedObjects(float pitch, float heading)
    {
        ScreenSetup.Initialize(1500, 1024);
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 400f;
        var projector = OmegaPerspectiveProjectorFactory.Create();
        foreach (bool isShip in new[] { true, false })
        {
            var caster = isShip ? Ship.CreateShip(surface) : Seeder.CreateSeeder(surface);
            caster.ObjectName = isShip ? "Ship" : "Seeder";
            caster.WorldPosition = new Vector3();
            caster.Rotation = new Vector3(pitch, 0f, heading);
            new ObjectFrameTransformer().RotateObjectGeometry(caster);
            foreach (float clearance in new[] { 0f, 300f })
            {
                caster.ObjectOffsets = new Vector3(0f, 500f - clearance, 400f);
                var shadows = new List<OmegaObject3D>();
                new ObjectShadowManager().HandleObjectShadow(caster, shadows);
                Assert.AreEqual(1, shadows.Count);
                var bodySize = ScreenSize(projector.ProjectToTriangles(new List<OmegaObject3D> { caster }, null));
                var shadowSize = ScreenSize(projector.ProjectToTriangles(shadows, null));
                Assert.IsTrue(shadowSize.Width < bodySize.Width,
                    $"{caster.ObjectName}: shadow width {shadowSize.Width} must stay below body width {bodySize.Width}.");
                Assert.IsTrue(shadowSize.Height < bodySize.Height,
                    $"{caster.ObjectName}: shadow height {shadowSize.Height} must stay below body height {bodySize.Height}.");
            }
        }

        static (int Width, int Height) ScreenSize(List<ProjectedTriangleMesh> triangles)
        {
            Assert.IsTrue(triangles.Count > 0);
            var x = triangles.SelectMany(t => new[] { t.X1, t.X2, t.X3 }).ToArray();
            var y = triangles.SelectMany(t => new[] { t.Y1, t.Y2, t.Y3 }).ToArray();
            return (x.Max() - x.Min(), y.Max() - y.Min());
        }
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void SpaceSwan_ShadowUsesModelScaleInsteadOfFlyingEnemySizeBoost(float pitch)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var caster = SpaceSwan.CreateSpaceSwan(CreateTiltedFlatSurface(pitch));
        caster.WorldPosition = new Vector3();
        caster.Rotation = new Vector3(pitch, 0f, 0f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        var centre = ObjectCollisionGeometry.GetLocalCrashCenter(caster);
        caster.ObjectOffsets = new Vector3(-centre.x, 500f - centre.y, -centre.z);
        var source = caster.ObjectParts.Single(p => p.PartName == "Shadow").Triangles
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }).ToArray();
        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);
        var result = shadows.Single().ObjectParts.Single().Triangles
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }).ToArray();
        float sourceWidth = source.Max(v => v.x) - source.Min(v => v.x);
        float shadowWidth = result.Max(v => v.x) - result.Min(v => v.x);
        Assert.AreEqual(sourceWidth * 0.9f, shadowWidth, 0.002f);
    }

    [DataTestMethod]
    [DataRow("PolarBear")]
    public void StaticShadow_IgnoresFlyingPlacementOffset(string name)
    {
        var surface = CreateTiltedFlatSurface(WorldViewSetup.SurfacePitchDegrees);
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 400f, 0f);
        caster.ObjectName = name;
        if (name == "PolarBear")
        {
            caster.SurfaceBasedId = 1;
            surface.RotatedSurfaceTriangles[0].landBasedPosition = 1;
        }
        SetRotatedFootprint(caster, WorldViewSetup.SurfacePitchDegrees);
        var baseline = new List<OmegaObject3D>();
        var shifted = new List<OmegaObject3D>();
        var manager = new ObjectShadowManager();
        ObjectShadowManager.TerrainShadowInwardOffset = 0f;
        ObjectShadowManager.TerrainShadowSideOffset = 0f;
        manager.HandleObjectShadow(caster, baseline);
        ObjectShadowManager.TerrainShadowInwardOffset = 200f;
        ObjectShadowManager.TerrainShadowSideOffset = -12f;
        manager.HandleObjectShadow(caster, shifted);
        Assert.AreEqual(1, baseline.Count);
        Assert.AreEqual(1, shifted.Count);
        CollectionAssert.AreEqual(SnapshotVertices(baseline[0]), SnapshotVertices(shifted[0]));
    }

    [DataTestMethod]
    [DataRow("MotherShipSmall", 63f)]
    [DataRow("MotherShipSmall", 70f)]
    [DataRow("MotherShipMedium", 63f)]
    [DataRow("MotherShipMedium", 70f)]
    [DataRow("MotherShipLarge", 63f)]
    [DataRow("MotherShipLarge", 70f)]
    [DataRow("ZeppelinBomber", 63f)]
    [DataRow("ZeppelinBomber", 70f)]
    [DataRow("KamikazeDrone", 63f)]
    [DataRow("KamikazeDrone", 70f)]
    [DataRow("AttackShip", 63f)]
    [DataRow("AttackShip", 70f)]
    [DataRow("BomberBomb", 63f)]
    [DataRow("BomberBomb", 70f)]
    [DataRow("DroneDecoy", 63f)]
    [DataRow("DroneDecoy", 70f)]
    [DataRow("PowerUp", 63f)]
    [DataRow("PowerUp", 70f)]
    [DataRow("JumpingFish", 63f)]
    [DataRow("JumpingFish", 70f)]
    public void FlyingShadow_Is35PercentSmallerAroundUnchangedAnchor(string name, float pitch)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        var surface = CreateTiltedFlatSurface(pitch);
        GameState.SurfaceState.SurfaceViewportObject.ObjectOffsets.z = 400f;
        var caster = CreateShadowCaster(name, surface);
        caster.WorldPosition = new Vector3();
        caster.Rotation = new Vector3(pitch, 0f, 30f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        var original = SnapshotVertices(caster);
        var centre = ObjectCollisionGeometry.GetLocalCrashCenter(caster);
        float anchorZ = -ObjectShadowManager.TerrainShadowInwardOffset;
        var anchor = new Vector3(ObjectShadowManager.TerrainShadowSideOffset,
            anchorZ / MathF.Tan(pitch * MathF.PI / 180f) - ObjectShadowManager.TerrainShadowSurfaceLift,
            anchorZ);
        var manager = new ObjectShadowManager();
        foreach (float height in new[] { 0f, 300f, 10000f })
        {
            caster.ObjectOffsets = new Vector3(-centre.x, 100f - height - centre.y, 400f - centre.z);
            var baseline = new List<OmegaObject3D>();
            ObjectShadowManager.MotherShipShadowSizeMultiplier = 1f;
            ObjectShadowManager.ZeppelinShadowSizeMultiplier = 1f;
            ObjectShadowManager.DefaultFlyingShadowSizeMultiplier = 1f;
            manager.HandleObjectShadow(caster, baseline);
            var reduced = new List<OmegaObject3D>();
            ObjectShadowManager.MotherShipShadowSizeMultiplier = 0.65f;
            ObjectShadowManager.ZeppelinShadowSizeMultiplier = 0.65f;
            ObjectShadowManager.DefaultFlyingShadowSizeMultiplier = 0.65f;
            manager.HandleObjectShadow(caster, reduced);
            Assert.AreEqual(1, baseline.Count);
            Assert.AreEqual(1, reduced.Count);
            var before = SnapshotVertices(baseline[0]);
            var after = SnapshotVertices(reduced[0]);
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(anchor.x + (before[i].X - anchor.x) * 0.65f, after[i].X, 0.002f);
                Assert.AreEqual(anchor.y + (before[i].Y - anchor.y) * 0.65f, after[i].Y, 0.002f);
                Assert.AreEqual(anchor.z + (before[i].Z - anchor.z) * 0.65f, after[i].Z, 0.002f);
            }
            CollectionAssert.AreEqual(original, SnapshotVertices(caster));
            Assert.AreEqual(100f - height - centre.y, caster.ObjectOffsets.y);
            Assert.AreEqual(400f - centre.z, caster.ObjectOffsets.z);
        }
    }

    [DataTestMethod]
    [DataRow("Ship")]
    [DataRow("Seeder")]
    [DataRow("SpaceSwan")]
    [DataRow("MotherShipSmall")]
    [DataRow("MotherShipMedium")]
    [DataRow("MotherShipLarge")]
    [DataRow("ZeppelinBomber")]
    [DataRow("PolarBear")]
    public void DefaultFlyingSizeAdjustment_DoesNotReduceAlreadyTunedOrSurfaceBoundShadows(string name)
    {
        var surface = CreateTiltedFlatSurface(WorldViewSetup.SurfacePitchDegrees);
        var caster = CreateFreeFlyingShadowCaster(surface, 0f, 400f, 0f);
        caster.ObjectName = name;
        if (name == "PolarBear")
        {
            caster.SurfaceBasedId = 1;
            surface.RotatedSurfaceTriangles[0].landBasedPosition = 1;
        }
        SetRotatedFootprint(caster, WorldViewSetup.SurfacePitchDegrees);
        var baseline = new List<OmegaObject3D>();
        var reduced = new List<OmegaObject3D>();
        var manager = new ObjectShadowManager();
        ObjectShadowManager.DefaultFlyingShadowSizeMultiplier = 1f;
        manager.HandleObjectShadow(caster, baseline);
        ObjectShadowManager.DefaultFlyingShadowSizeMultiplier = 0.65f;
        manager.HandleObjectShadow(caster, reduced);
        Assert.AreEqual(1, baseline.Count);
        Assert.AreEqual(1, reduced.Count);
        CollectionAssert.AreEqual(SnapshotVertices(baseline[0]), SnapshotVertices(reduced[0]));
    }

    private static readonly string[] FlyingShadowNames =
    {
        "Ship", "Seeder", "AttackShip", "KamikazeDrone", "MotherShipSmall", "MotherShipMedium",
        "MotherShipLarge", "ZeppelinBomber", "BomberBomb", "SpaceSwan", "DroneDecoy", "PowerUp", "JumpingFish"
    };

    public static IEnumerable<object[]> FlyingShadowTerrainCases()
    {
        foreach (float pitch in new[] { 63f, 70f })
        foreach (string name in FlyingShadowNames)
            yield return new object[] { pitch, name };
    }

    public static IEnumerable<object[]> FlyingShadowSyncCases()
    {
        foreach (float pitch in new[] { 63f, 70f })
        foreach (string name in FlyingShadowNames)
        {
            yield return new object[] { pitch, 0f, 1500, 1024, name };
            yield return new object[] { pitch, 90f, 2560, 1440, name };
        }
    }

    private static OmegaObject3D CreateShadowCaster(string name, Surface surface) => name switch
    {
        "Ship" => Ship.CreateShip(surface),
        "Seeder" => Seeder.CreateSeeder(surface),
        "AttackShip" => AttackShip.CreateAttackShip(surface),
        "KamikazeDrone" => KamikazeDrone.CreateKamikazeDrone(surface),
        "MotherShipSmall" => MotherShipSmall.CreateMotherShipSmall(surface),
        "MotherShipMedium" => MotherShipMedium.CreateMotherShipMedium(surface),
        "MotherShipLarge" => MotherShipLarge.CreateMotherShipLarge(surface),
        "ZeppelinBomber" => ZeppelinBomber.CreateZeppelinBomber(surface),
        "BomberBomb" => BomberBomb.CreateBomberBomb(surface),
        "SpaceSwan" => SpaceSwan.CreateSpaceSwan(surface),
        "DroneDecoy" => DecoyBeacon.CreateDecoyBeacon(surface),
        "PowerUp" => PowerUp.CreatePowerup(surface),
        "JumpingFish" => JumpingFish.CreateJumpingFish(surface),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static IVector3 ShadowBoundsCentre(OmegaObject3D shadow) => GeometryMath.GetCenterOfBox(
        shadow.ObjectParts.SelectMany(p => p.Triangles).SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }).ToList());

    private static void AssertPointEqual(IVector3 expected, IVector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 0.02f, "X");
        Assert.AreEqual(expected.y, actual.y, 0.02f, "Y");
        Assert.AreEqual(expected.z, actual.z, 0.02f, "Z");
    }

    private static (float X, float Y, float Z)[] SnapshotCrashBoxes(OmegaObject3D obj) =>
        obj.CrashBoxes.SelectMany(b => b).Select(v => (v.x, v.y, v.z)).ToArray();

    private static (float X, float Y, float Z)[] SnapshotVertices(OmegaObject3D obj) =>
        obj.ObjectParts.SelectMany(p => p.Triangles)
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
            .Select(v => (v.x, v.y, v.z)).ToArray();

    private static Surface CreateTiltedFlatSurface(float pitch)
    {
        var rotation = new OmegaMeshRotation();
        IVector3 Point(float x, float y) => rotation.RotatePoint(pitch, new Vector3(x, y, 0f), 'X');
        return new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor { vert1 = Point(-2000f, -2000f), vert2 = Point(2000f, -2000f), vert3 = Point(2000f, 2000f) },
                new TriangleMeshWithColor { vert1 = Point(-2000f, -2000f), vert2 = Point(2000f, 2000f), vert3 = Point(-2000f, 2000f) }
            }
        };
    }

    private static void SetRotatedFootprint(OmegaObject3D caster, float pitch)
    {
        caster.ObjectParts[0].Triangles[0] = new TriangleMeshWithColor
        {
            vert1 = new Vector3(-20f, -10f, 0f),
            vert2 = new Vector3(20f, -10f, 0f),
            vert3 = new Vector3(0f, 20f, 0f)
        };
        caster.Rotation = new Vector3(pitch, 0f, 35f);
        // Same ordering as LiveGameLoop: shadow parts have already been rotated.
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
    }

    private static Vector3 TriangleCenter(ITriangleMeshWithColorAndTexture triangle) => new(
        (triangle.vert1.x + triangle.vert2.x + triangle.vert3.x) / 3f,
        (triangle.vert1.y + triangle.vert2.y + triangle.vert3.y) / 3f,
        (triangle.vert1.z + triangle.vert2.z + triangle.vert3.z) / 3f);

    private static OmegaObject3D CreateFreeFlyingShadowCaster(Surface surface, float x, float y, float z)
    {
        return new OmegaObject3D
        {
            ObjectId = 2,
            ObjectName = "KamikazeDrone",
            HasShadow = true,
            ParentSurface = surface,
            ObjectOffsets = new Vector3 { x = x, y = y, z = z },
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = "Shadow",
                    IsVisible = false,
                    Triangles = new List<ITriangleMeshWithColorAndTexture>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "000000",
                            noHidden = true,
                            vert1 = new Vector3(),
                            vert2 = new Vector3(),
                            vert3 = new Vector3()
                        }
                    }
                }
            }
        };
    }
}
