using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Wpf.Rendering;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System.Windows.Media;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
public class RenderSimpleOptimizationTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3()
        };
        GameState.SettingsState = new GameSettingsState();
    }

    [TestMethod]
    public void SurfaceTriangleLookup_UsesCachedLandBasedTriangle()
    {
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>()
        };
        surface.RotatedSurfaceTriangleByLandId[42] = new TriangleMeshWithColor
        {
            landBasedPosition = 42,
            vert1 = new Vector3 { x = 25f, y = 15f, z = 5f },
            vert2 = new Vector3 { x = 35f, y = 15f, z = 5f },
            vert3 = new Vector3 { x = 25f, y = 25f, z = 5f }
        };

        var obj = CreateRenderableObject();
        obj.ParentSurface = surface;
        obj.SurfaceBasedId = 42;

        bool positioned = ObjectPlacementHelpers.TryGetRenderPosition(obj, 100, 100, out var x, out var y, out var z);

        Assert.IsTrue(positioned);
        Assert.AreEqual(100, x);
        Assert.AreEqual(100, y);
        Assert.AreEqual(0, z);
    }

    [TestMethod]
    public void SurfaceBasedPlacement_UsesBottomFootprintAsObjectAnchor()
    {
        var obj = new OmegaObject3D
        {
            ObjectId = 44,
            ObjectName = "FootprintObject",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    IsVisible = true,
                    PartName = "Main",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 10f, y = -20f, z = 30f },
                            vert2 = new Vector3 { x = 30f, y = -20f, z = 30f },
                            vert3 = new Vector3 { x = 200f, y = 80f, z = 300f }
                        }
                    }
                }
            }
        };

        var anchor = ObjectPlacementHelpers.GetObjectGeometricCenter(obj, snapToBottomY: true);

        Assert.AreEqual(20f, anchor.x, 0.001f);
        Assert.AreEqual(-20f, anchor.y, 0.001f);
        Assert.AreEqual(30f, anchor.z, 0.001f);
    }

    [TestMethod]
    public void SurfaceBasedPlacement_UsesPrebakedSurfaceFootprintPivot()
    {
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>()
        };
        var target = new Vector3 { x = 25f, y = 15f, z = 5f };
        surface.RotatedSurfaceTriangleByLandId[42] = new TriangleMeshWithColor
        {
            landBasedPosition = 42,
            vert1 = target,
            vert2 = new Vector3 { x = 35f, y = 15f, z = 5f },
            vert3 = new Vector3 { x = 25f, y = 25f, z = 5f }
        };

        var obj = new OmegaObject3D
        {
            ObjectId = 45,
            ObjectName = "PrebakedPivotObject",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ParentSurface = surface,
            SurfaceBasedId = 42,
            UseSurfaceFootprintPivot = true,
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    IsVisible = true,
                    PartName = "Main",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = -100f, z = 50f }
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = -1f, y = -1f, z = -1f },
                    new Vector3 { x = 1f, y = 1f, z = 1f }
                }
            }
        };

        bool positioned = ObjectPlacementHelpers.TryGetRenderPosition(obj, 100, 100, out _, out _, out _);

        Assert.IsTrue(positioned);
        var pivotVertex = obj.ObjectParts[0].Triangles[0].vert1;
        Assert.AreEqual(target.x, pivotVertex.x, 0.001f);
        Assert.AreEqual(target.y, pivotVertex.y, 0.001f);
        Assert.AreEqual(target.z, pivotVertex.z, 0.001f);
    }

    [TestMethod]
    public void NormalizeSurfaceFootprintPivot_PinsModelSpaceFootprintAndCrashBoxes()
    {
        var obj = new OmegaObject3D
        {
            ObjectId = 46,
            ObjectName = "RawSurfaceObject",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    IsVisible = true,
                    PartName = "Main",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 10f, y = 4f, z = 10f },
                            vert2 = new Vector3 { x = 30f, y = 6f, z = 10f },
                            vert3 = new Vector3 { x = 200f, y = 80f, z = 110f }
                        }
                    }
                },
                new OmegaObjectPart3D
                {
                    IsVisible = false,
                    PartName = "Shadow",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 10f, y = 4f, z = 0f },
                            vert2 = new Vector3 { x = 30f, y = 6f, z = 0f },
                            vert3 = new Vector3 { x = 20f, y = 5f, z = 20f }
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = 10f, y = 4f, z = 10f },
                    new Vector3 { x = 30f, y = 6f, z = 20f }
                }
            }
        };

        OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(obj);

        Assert.IsTrue(obj.UseSurfaceFootprintPivot);
        Assert.AreEqual(-10f, obj.ObjectParts[0].Triangles[0].vert1.x, 0.001f);
        Assert.AreEqual(-1f, obj.ObjectParts[0].Triangles[0].vert1.y, 0.001f);
        Assert.AreEqual(0f, obj.ObjectParts[0].Triangles[0].vert1.z, 0.001f);
        Assert.AreEqual(10f, obj.ObjectParts[0].Triangles[0].vert2.x, 0.001f);
        Assert.AreEqual(1f, obj.ObjectParts[0].Triangles[0].vert2.y, 0.001f);
        Assert.AreEqual(0f, obj.ObjectParts[0].Triangles[0].vert2.z, 0.001f);

        Assert.AreEqual(-10f, obj.CrashBoxes[0][0].x, 0.001f);
        Assert.AreEqual(-1f, obj.CrashBoxes[0][0].y, 0.001f);
        Assert.AreEqual(0f, obj.CrashBoxes[0][0].z, 0.001f);

        Assert.AreEqual(-10f, obj.ObjectParts[1].Triangles[0].vert1.x, 0.001f);
        Assert.AreEqual(-1f, obj.ObjectParts[1].Triangles[0].vert1.y, 0.001f);
        Assert.AreEqual(0f, obj.ObjectParts[1].Triangles[0].vert1.z, 0.001f,
            "A custom shadow's ground-plane vertices must remain at z = 0.");
        Assert.AreEqual(20f, obj.ObjectParts[1].Triangles[0].vert3.z, 0.001f,
            "Footprint normalization must preserve the shadow silhouette's authored height.");
    }

    [TestMethod]
    public void WinterSurfaceObjects_KeepShadowFootprintOnGroundAfterScalingAndNormalization()
    {
        var surface = new Surface();
        var objects = new[]
        {
            Igloo.CreateSmallIgloo(surface),
            Igloo.CreateLargeIgloo(surface),
            PolarBear.CreatePolarBear(surface),
            Seal.CreateSeal(surface),
            SnowTower.CreateSnowTower(surface)
        };

        foreach (var obj in objects)
        {
            var shadow = obj.ObjectParts.Single(part => part.PartName == "Shadow");
            float minShadowZ = shadow.Triangles
                .SelectMany(triangle => new[] { triangle.vert1.z, triangle.vert2.z, triangle.vert3.z })
                .Min();

            Assert.AreEqual(0f, minShadowZ, 0.001f,
                $"{obj.ObjectName} must keep its shadow footprint on the model-space ground plane.");
        }
    }

    [TestMethod]
    public void ProjectToTriangles_ReusesProvidedResultList()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create();
        var reusable = new List<ProjectedTriangleMesh>
        {
            new() { PartName = "Stale" }
        };

        var result = converter.ProjectToTriangles(
            new List<OmegaObject3D> { CreateRenderableObject() },
            1,
            reusable);

        Assert.AreSame(reusable, result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Main", result[0].PartName);

        var emptyResult = converter.ProjectToTriangles(new List<OmegaObject3D>(), 2, reusable);

        Assert.AreSame(reusable, emptyResult);
        Assert.AreEqual(0, emptyResult.Count);
    }

    [TestMethod]
    public void ProjectToTriangles_ReservesCapacityForVisibleTriangles()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create();
        var reusable = new List<ProjectedTriangleMesh>(capacity: 1);
        var obj = CreateRenderableObject();
        var triangles = obj.ObjectParts[0].Triangles;
        var template = triangles[0];

        for (int i = 1; i < 64; i++)
        {
            triangles.Add(new TriangleMeshWithColor
            {
                Color = template.Color,
                noHidden = true,
                normal1 = new Vector3 { x = 0f, y = 0f, z = 1f },
                vert1 = new Vector3 { x = -10f, y = -10f, z = 0f },
                vert2 = new Vector3 { x = 10f, y = -10f, z = 0f },
                vert3 = new Vector3 { x = 0f, y = 10f, z = 0f }
            });
        }

        var result = converter.ProjectToTriangles(
            new List<OmegaObject3D> { obj },
            1,
            reusable);

        Assert.AreSame(reusable, result);
        Assert.AreEqual(64, result.Count);
        Assert.IsTrue(result.Capacity >= 64,
            "Projection output should reserve enough capacity for visible triangles, not just object count.");
    }

    [TestMethod]
    public void ProjectToTriangles_MarksDynamicEffectsForEffectPipeline()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create();

        var result = converter.ProjectToTriangles(
            new List<OmegaObject3D> { CreateRenderableObject("ExplodingPart") },
            1);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("ExplodingPart", result[0].PartName);
        Assert.IsTrue(result[0].UseEffectRenderingPipeline,
            "Explosions should be explicitly marked for the separate effect rendering pipeline before they reach WorldRenderer.");
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(result[0]));
    }

    [TestMethod]
    public void ProjectToTriangles_UsesInjectedProjectionViewport()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create(new ProjectionViewport(
            screenWidth: 1000,
            screenHeight: 800,
            perspectiveAdjustment: 1500,
            objectZoom: 2));

        var result = converter.ProjectToTriangles(
            new List<OmegaObject3D> { CreateRenderableObject() },
            1);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(480, result[0].X1);
        Assert.AreEqual(380, result[0].Y1);
        Assert.AreEqual(520, result[0].X2);
        Assert.AreEqual(380, result[0].Y2);
        Assert.AreEqual(500, result[0].X3);
        Assert.AreEqual(420, result[0].Y3);
    }

    [TestMethod]
    public void ProjectionMath_ProjectsVertexWithViewportSettings()
    {
        var viewport = new ProjectionViewport(1000, 800, 1500, 2);

        bool projected = ProjectionMath.TryProjectVertex(
            new Vector3 { x = 10f, y = -10f, z = 0f },
            objectScreenX: viewport.ScreenCenterX,
            objectScreenY: viewport.ScreenCenterY,
            objectScreenZ: 0,
            viewport,
            out var screenPoint);

        Assert.IsTrue(projected);
        Assert.AreEqual(520, screenPoint.x);
        Assert.AreEqual(380, screenPoint.y);
    }

    [TestMethod]
    public void ProjectionMath_RejectsVerticesBehindPerspectivePlane()
    {
        var viewport = new ProjectionViewport(1000, 800, 1500, 2);

        bool projected = ProjectionMath.TryProjectVertex(
            new Vector3 { x = 10f, y = 10f, z = 1500f },
            objectScreenX: viewport.ScreenCenterX,
            objectScreenY: viewport.ScreenCenterY,
            objectScreenZ: 0,
            viewport,
            out var screenPoint);

        Assert.IsFalse(projected);
        Assert.IsTrue(double.IsNaN(screenPoint.x));
        Assert.IsTrue(double.IsNaN(screenPoint.y));
    }

    [TestMethod]
    public void ProjectionMath_ClampsDebugTrianglesToViewportMargin()
    {
        var viewport = new ProjectionViewport(1000, 800, 1500, 2);
        var p1 = (-1000d, -1000d);
        var p2 = (500d, 400d);
        var p3 = (2000d, 2000d);

        bool kept = ProjectionMath.TryClampTriangleToViewport(
            ref p1,
            ref p2,
            ref p3,
            viewport,
            screenMarginFactor: 0.05);

        Assert.IsTrue(kept);
        Assert.AreEqual(-50d, p1.Item1);
        Assert.AreEqual(-40d, p1.Item2);
        Assert.AreEqual(1050d, p3.Item1);
        Assert.AreEqual(840d, p3.Item2);
    }

    [TestMethod]
    public void ProjectToTriangles_ClampsCrashBoxDebugTrianglesToScreenMargin()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create();
        var surface = CreateRenderableObject();
        surface.ObjectName = "Surface";
        surface.CrashBoxDebugMode = true;
        surface.CrashBoxes = new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -100000, y = -100000, z = 0 },
                new Vector3 { x = 100000, y = 100000, z = 50 })
        };
        surface.CrashBoxNames = new List<string?> { "TerrainSurface" };

        var result = converter.ProjectToTriangles(new List<OmegaObject3D> { surface }, 1);
        var crashTriangles = result.Where(t => t.PartName == "CrashBox-Surface").ToList();

        Assert.IsTrue(crashTriangles.Count > 0, "Expected debug crashbox triangles to be rendered.");
        foreach (var triangle in crashTriangles)
        {
            AssertDebugCoordinateIsClamped(triangle.X1, triangle.Y1);
            AssertDebugCoordinateIsClamped(triangle.X2, triangle.Y2);
            AssertDebugCoordinateIsClamped(triangle.X3, triangle.Y3);
        }
    }

    [TestMethod]
    public void ProjectToTriangles_RendersSurfaceMainCrashBoxDebug()
    {
        var converter = OmegaPerspectiveProjectorFactory.Create();
        var surface = CreateRenderableObject();
        surface.ObjectName = "Surface";
        surface.CrashBoxDebugMode = true;
        surface.CrashBoxes = new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -50, y = -50, z = -50 },
                new Vector3 { x = 50, y = 50, z = 50 })
        };
        surface.CrashBoxNames = new List<string?> { "MainSurface" };

        var result = converter.ProjectToTriangles(new List<OmegaObject3D> { surface }, 1);

        Assert.IsTrue(result.Any(t => t.PartName == "CrashBox-Surface"),
            "MainSurface should still be visible when surface crashbox debug mode is enabled.");
    }

    [TestMethod]
    public void CullTrianglesOutsideRenderDepth_RemovesOutOfRangeTrianglesBeforeSort()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            new() { CalculatedZ = ScreenSetup.RenderNearZ - 1, PartName = "TooNear" },
            new() { CalculatedZ = 0, PartName = "KeepMiddle" },
            new() { CalculatedZ = ScreenSetup.RenderFarZ + 1, PartName = "TooFar" },
            new() { CalculatedZ = ScreenSetup.RenderNearZ, PartName = "KeepNearBoundary" },
            new() { CalculatedZ = ScreenSetup.RenderFarZ, PartName = "KeepFarBoundary" }
        };

        int kept = WorldRenderer.CullTrianglesOutsideRenderDepth(triangles);

        Assert.AreEqual(3, kept);
        Assert.AreEqual(3, triangles.Count);
        Assert.AreEqual("KeepMiddle", triangles[0].PartName);
        Assert.AreEqual("KeepNearBoundary", triangles[1].PartName);
        Assert.AreEqual("KeepFarBoundary", triangles[2].PartName);
    }

    [TestMethod]
    public void ProcessTrianglesForRender_CreatesPensForVisibleTrianglesToCoverSeams()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            new() { CalculatedZ = 0, TriangleAngle = 0.5f, Color = "ffffff", PartName = "Surface" },
            new() { CalculatedZ = 0, TriangleAngle = 0.5f, Color = "ff0000", PartName = "CrashBox-Test" }
        };
        var colorCache = new Dictionary<(float, string), Color>();
        var brushCache = new Dictionary<Color, SolidColorBrush>();
        var penCache = new Dictionary<Color, Pen>();

        int processed = WorldRenderer.ProcessTrianglesForRender(triangles, colorCache, brushCache, penCache);

        Assert.AreEqual(2, processed);
        Assert.AreEqual(2, penCache.Count);
    }

    [TestMethod]
    public void IsSameBatch_RequiresSameBrushAndPenInstances()
    {
        var brush = new SolidColorBrush(Color.FromRgb(10, 20, 30));
        var sameColorDifferentBrush = new SolidColorBrush(Color.FromRgb(10, 20, 30));
        var pen = new Pen(brush, 1);
        var sameColorDifferentPen = new Pen(brush, 1);

        Assert.IsTrue(WorldRenderer.IsSameBatch(brush, pen, brush, pen));
        Assert.IsFalse(WorldRenderer.IsSameBatch(brush, pen, sameColorDifferentBrush, pen));
        Assert.IsFalse(WorldRenderer.IsSameBatch(brush, pen, brush, sameColorDifferentPen));
    }

    [TestMethod]
    public void ExplodingPart_RendersOutsideBatching()
    {
        Assert.IsTrue(WorldRenderer.ShouldRenderAsSeparateTriangle("ExplodingPart"),
            "Explosion fragments should not be merged into one StreamGeometry batch.");
        Assert.IsFalse(WorldRenderer.ShouldRenderAsSeparateTriangle("Surface"),
            "Normal world geometry should keep the optimized batching path.");
        Assert.IsTrue(WorldRenderer.IsExplodingPartName("ExplodingPart"),
            "Renderer should be able to identify explosion fragments for the dynamic effect path.");
    }

    [TestMethod]
    public void DynamicEffects_RenderOutsideBatching()
    {
        Assert.IsTrue(WorldRenderer.ShouldRenderAsSeparateTriangle("Particle"),
            "Particle bursts should not be merged into one StreamGeometry batch.");
        Assert.IsTrue(WorldRenderer.ShouldRenderAsSeparateTriangle("ParticleShadow"),
            "Particle shadows are dynamic effects and should not share batched geometry.");
        Assert.IsTrue(WorldRenderer.ShouldRenderAsSeparateTriangle("MuzzleFlash"),
            "Muzzle flashes blink on/off and should stay out of stable geometry batches.");
        Assert.IsFalse(WorldRenderer.ShouldRenderAsSeparateTriangle("EarthGlobe"),
            "Stable world geometry should keep batching enabled.");

        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(new ProjectedTriangleMesh
        {
            PartName = "Surface",
            UseEffectRenderingPipeline = true
        }), "The explicit 2D marker must force the effect pipeline even without a special part name.");
    }

    [TestMethod]
    public void GlowCandidates_UseEffectPipelineWhenGlowIsEnabledAndParticlesStayDynamic()
    {
        var lazer = new ProjectedTriangleMesh { PartName = "Lazer_Beam" };
        var powerUp = new ProjectedTriangleMesh { PartName = "TravelSpeedPowerUpBody" };
        var bullet = new ProjectedTriangleMesh { PartName = "BulletBody" };
        var particle = new ProjectedTriangleMesh { PartName = "Particle" };
        var stableWorldPart = new ProjectedTriangleMesh { PartName = "HouseWalls" };

        GameState.SettingsState.GlowEffectsEnabled = false;
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(lazer));
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(powerUp));
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(bullet));
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(particle),
            "Particles already use the dynamic effect pipeline even when glow is disabled.");
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(stableWorldPart));

        GameState.SettingsState.GlowEffectsEnabled = true;
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(lazer));
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(powerUp));
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(bullet));
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(particle));
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(stableWorldPart),
            "Normal world geometry should keep the optimized batching path even on high graphics.");
    }

    [TestMethod]
    public void HighGraphics_RendersShadowsThroughEffectPipeline()
    {
        var shadow = new ProjectedTriangleMesh { PartName = "Shadow" };

        GameState.SettingsState.GraphicsQuality = GraphicsQualityPreset.Balanced;
        GameState.SettingsState.EnhancedShadowsEnabled = true;
        Assert.IsFalse(WorldRenderer.ShouldUseEffectRenderingPipeline(shadow));

        GameState.SettingsState.GraphicsQuality = GraphicsQualityPreset.High;
        GameState.SettingsState.EnhancedShadowsEnabled = true;
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(shadow));
    }

    [TestMethod]
    public void DynamicEffects_KeepEffectPipelineWhenGlowIsDisabled()
    {
        GameState.SettingsState.GlowEffectsEnabled = false;

        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(new ProjectedTriangleMesh
        {
            PartName = "ExplodingPart"
        }), "Explosion fragments already use the dynamic effect path and should not depend on glow settings.");
        Assert.IsTrue(WorldRenderer.ShouldUseEffectRenderingPipeline(new ProjectedTriangleMesh
        {
            PartName = "MuzzleFlash"
        }), "Muzzle flashes are short-lived dynamic effects and should not depend on glow settings.");
    }

    private static void AssertDebugCoordinateIsClamped(int x, int y)
    {
        int minX = (int)Math.Floor(-(ScreenSetup.screenSizeX * 0.05));
        int maxX = (int)Math.Ceiling(ScreenSetup.screenSizeX * 1.05);
        int minY = (int)Math.Floor(-(ScreenSetup.screenSizeY * 0.05));
        int maxY = (int)Math.Ceiling(ScreenSetup.screenSizeY * 1.05);

        Assert.IsTrue(x >= minX && x <= maxX, $"Expected debug X coordinate {x} to be inside the crashbox debug screen margin.");
        Assert.IsTrue(y >= minY && y <= maxY, $"Expected debug Y coordinate {y} to be inside the crashbox debug screen margin.");
    }

    private static OmegaObject3D CreateRenderableObject(string partName = "Main")
    {
        return new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "Renderable",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            CrashBoxes = new List<List<IVector3>>(),
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = partName,
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "ffffff",
                            noHidden = true,
                            normal1 = new Vector3 { x = 0f, y = 0f, z = 1f },
                            vert1 = new Vector3 { x = -10f, y = -10f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = -10f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 10f, z = 0f }
                        }
                    }
                }
            }
        };
    }
}
