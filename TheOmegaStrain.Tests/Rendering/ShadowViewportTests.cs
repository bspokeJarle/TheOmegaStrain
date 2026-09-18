using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Runtime.Rendering;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
[DoNotParallelize]
public class ShadowViewportTests
{
    private SurfaceState previousSurface = null!;
    private float previousPitch;
    private int previousWidth, previousHeight, previousObjectId;

    [TestInitialize]
    public void Setup()
    {
        previousSurface = GameState.SurfaceState;
        previousPitch = WorldViewSetup.SurfacePitchDegrees;
        previousWidth = ScreenSetup.screenSizeX;
        previousHeight = ScreenSetup.screenSizeY;
        previousObjectId = GameState.ObjectIdCounter;
        GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3() };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.SurfaceState = previousSurface;
        WorldViewSetup.ConfigurePitch(previousPitch);
        ScreenSetup.Initialize(previousWidth, previousHeight);
        GameState.ObjectIdCounter = previousObjectId;
    }

    public static IEnumerable<object[]> ViewportCases()
    {
        foreach (float pitch in new[] { 63f, 70f })
        foreach (var size in new[] { (1280, 800), (1500, 1024), (1920, 1080), (2560, 1440), (3440, 1440) })
            yield return new object[] { pitch, size.Item1, size.Item2 };
    }

    [DataTestMethod]
    [DynamicData(nameof(ViewportCases), DynamicDataSourceType.Method)]
    public void ForegroundShadow_RemainsPartlyVisibleOnTerrainWithoutChangingCaster(float pitch, int width, int height)
    {
        var surface = ConfigureSurface(pitch, width, height);
        foreach (string name in new[] { "Seeder", "KamikazeDrone", "MotherShipLarge" })
        {
            var caster = CreateCaster(name, surface, 650f * ScreenSetup.ScreenScaleX);
            var geometryBefore = Vertices(caster).Select(v => (v.x, v.y, v.z)).ToArray();
            var boxesBefore = caster.CrashBoxes.SelectMany(b => b).Select(v => (v.x, v.y, v.z)).ToArray();
            var offsetsBefore = (caster.ObjectOffsets.x, caster.ObjectOffsets.y, caster.ObjectOffsets.z);
            var rotationBefore = (caster.Rotation.x, caster.Rotation.y, caster.Rotation.z);
            caster.IsOnScreen = false;
            Assert.AreEqual(0, Shadows(caster).Count, "The original anchor is beyond the foreground Surface edge.");

            caster.IsOnScreen = true;
            var shadow = Shadows(caster).Single();
            var screen = ProjectShadow(shadow);
            double minY = screen.Min(p => p.y), maxY = screen.Max(p => p.y);
            Assert.IsTrue(minY < height - 1, $"{name}: some shadow must stay inside the bottom edge.");
            Assert.IsTrue(maxY > 0);
            Assert.IsTrue(screen.Max(p => p.x) > 0 && screen.Min(p => p.x) < width);
            Assert.IsTrue((height - minY) / (maxY - minY) >= 0.35,
                $"{name}: retain a useful part of the shadow, not a single-pixel sliver.");
            if (name == "Seeder")
                Assert.IsTrue(maxY > height, "A partial shadow is allowed; do not pull the whole footprint onto the screen.");
            AssertLiesOnTerrain(shadow, pitch, 0f);

            CollectionAssert.AreEqual(geometryBefore, Vertices(caster).Select(v => (v.x, v.y, v.z)).ToArray());
            CollectionAssert.AreEqual(boxesBefore, caster.CrashBoxes.SelectMany(b => b).Select(v => (v.x, v.y, v.z)).ToArray());
            Assert.AreEqual(offsetsBefore, (caster.ObjectOffsets.x, caster.ObjectOffsets.y, caster.ObjectOffsets.z));
            Assert.AreEqual(rotationBefore, (caster.Rotation.x, caster.Rotation.y, caster.Rotation.z));
            Assert.AreEqual(0f, caster.WorldPosition.x);
            Assert.AreEqual(0f, caster.WorldPosition.y);
            Assert.AreEqual(0f, caster.WorldPosition.z);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(ViewportCases), DynamicDataSourceType.Method)]
    public void EdgeAssistance_AlsoWorksWhileCasterIsPartlyBelowViewport(float pitch, int width, int height)
    {
        var surface = ConfigureSurface(pitch, width, height);
        var caster = CreateCaster("Seeder", surface, 650f * ScreenSetup.ScreenScaleX);
        caster.IsOnScreen = true;
        // Move the visible body's projected centre onto the screen edge so only
        // part of the object remains visible. No centre-only visibility shortcut.
        var viewport = new ProjectionViewport(width, height, ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom);
        var projectedY = new List<double>();
        foreach (var vertex in caster.ObjectParts.Where(p => p.IsVisible).SelectMany(p => p.Triangles)
                     .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }))
        {
            Assert.IsTrue(ProjectionMath.TryProjectVertex(vertex,
                width / 2 + caster.ObjectOffsets.x, height / 2 + caster.ObjectOffsets.y,
                OmegaPerspectiveProjectorFactory.ClampRenderDepth(caster.ObjectOffsets.z, viewport.PerspectiveAdjustment),
                viewport, out var point));
            projectedY.Add(point.y);
        }
        caster.ObjectOffsets.y += (float)(height - (projectedY.Min() + projectedY.Max()) / 2);
        var shadow = Shadows(caster).Single();
        Assert.IsTrue(ProjectShadow(shadow).Min(p => p.y) < height - 1);
        AssertLiesOnTerrain(shadow, pitch, 0f);
    }

    [DataTestMethod]
    [DynamicData(nameof(ViewportCases), DynamicDataSourceType.Method)]
    public void EdgeAssistance_LeavesOrdinaryAndHorizonShadowsAlone(float pitch, int width, int height)
    {
        var surface = ConfigureSurface(pitch, width, height);
        var ordinary = CreateCaster("Seeder", surface, -250f * ScreenSetup.ScreenScaleX);
        ordinary.IsOnScreen = false;
        var original = Vertices(Shadows(ordinary).Single()).Select(v => (v.x, v.y, v.z)).ToArray();
        ordinary.IsOnScreen = true;
        CollectionAssert.AreEqual(original, Vertices(Shadows(ordinary).Single()).Select(v => (v.x, v.y, v.z)).ToArray());

        var horizon = CreateCaster("Seeder", surface, -1200f * ScreenSetup.ScreenScaleX);
        horizon.IsOnScreen = true;
        Assert.AreEqual(0, Shadows(horizon).Count, "Do not bring off-surface horizon shadows back into view.");
    }

    [DataTestMethod]
    [DynamicData(nameof(ViewportCases), DynamicDataSourceType.Method)]
    public void EdgeAssistance_RequiresVisibleBodyAndResamplesRaisedTerrain(float pitch, int width, int height)
    {
        const float ridgeHeight = 60f;
        var surface = ConfigureSurface(pitch, width, height, ridgeHeight);
        var caster = CreateCaster("Seeder", surface, 650f * ScreenSetup.ScreenScaleX);
        caster.IsOnScreen = true; // The render-list flag alone also includes nearby off-screen objects.
        float visibleY = caster.ObjectOffsets.y;
        caster.ObjectOffsets.y = height * 2f;
        Assert.AreEqual(0, Shadows(caster).Count, "An off-screen body must not leave a pinned shadow.");

        caster.ObjectOffsets.y = visibleY;
        var shadow = Shadows(caster).Single();
        Assert.IsTrue(ProjectShadow(shadow).Min(p => p.y) < height);
        AssertLiesOnTerrain(shadow, pitch, ridgeHeight);
    }

    private static Surface ConfigureSurface(float pitch, int width, int height, float ridgeHeight = 0f)
    {
        WorldViewSetup.ConfigurePitch(pitch);
        ScreenSetup.Initialize(width, height);
        GameState.SurfaceState.SurfaceViewportObject = new OmegaObject3D
        {
            ObjectId = GameState.ObjectIdCounter++,
            ObjectName = "Surface", WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(70f * ScreenSetup.ScreenScaleX, 500f * ScreenSetup.ScreenScaleY, 400f)
        };
        // Same longitudinal extent as the 36-tile viewport's visible rows.
        float halfWidth = SurfaceSetup.tileSize * 18f;
        var rotation = new OmegaMeshRotation();
        IVector3 Point(float x, float y) => rotation.RotatePoint(pitch,
            new Vector3(x, y, ridgeHeight * (1f - MathF.Abs(x) / halfWidth)), 'X');
        float back = -17f * SurfaceSetup.tileSize, front = 8f * SurfaceSetup.tileSize;
        var triangles = new List<ITriangleMeshWithColorAndTexture>();
        foreach (float x in new[] { -halfWidth, 0f })
        {
            triangles.Add(new TriangleMeshWithColor
            {
                vert1 = Point(x, back), vert2 = Point(x + halfWidth, back), vert3 = Point(x + halfWidth, front)
            });
            triangles.Add(new TriangleMeshWithColor
            {
                vert1 = Point(x, back), vert2 = Point(x + halfWidth, front), vert3 = Point(x, front)
            });
        }
        return new Surface { RotatedSurfaceTriangles = triangles };
    }

    private static OmegaObject3D CreateCaster(string name, Surface surface, float shadowZ)
    {
        var caster = name switch
        {
            "MotherShipLarge" => MotherShipLarge.CreateMotherShipLarge(surface),
            "KamikazeDrone" => KamikazeDrone.CreateKamikazeDrone(surface),
            _ => Seeder.CreateSeeder(surface)
        };
        caster.ObjectName = name;
        caster.WorldPosition = new Vector3();
        caster.Rotation = new Vector3(WorldViewSetup.SurfacePitchDegrees, 0f, 30f);
        new ObjectFrameTransformer().RotateObjectGeometry(caster);
        var centre = ObjectCollisionGeometry.GetLocalCrashCenter(caster);
        caster.ObjectOffsets = new Vector3(-centre.x, ScreenSetup.screenSizeY * 0.2f - centre.y,
            400f - ObjectShadowManager.TerrainShadowInwardOffset - shadowZ - centre.z);
        return caster;
    }

    private static List<OmegaObject3D> Shadows(OmegaObject3D caster)
    {
        var shadows = new List<OmegaObject3D>();
        new ObjectShadowManager().HandleObjectShadow(caster, shadows);
        return shadows;
    }

    private static IEnumerable<IVector3> Vertices(OmegaObject3D obj) =>
        obj.ObjectParts.SelectMany(p => p.Triangles).SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 });

    private static List<(double x, double y)> ProjectShadow(OmegaObject3D shadow)
    {
        var screen = new List<(double x, double y)>();
        foreach (var vertex in Vertices(shadow))
        {
            Assert.IsTrue(ProjectionMath.TryProjectVertex(vertex,
                ScreenSetup.screenSizeX / 2 + shadow.ObjectOffsets.x, ScreenSetup.screenSizeY / 2 + shadow.ObjectOffsets.y,
                OmegaPerspectiveProjectorFactory.ClampRenderDepth(shadow.ObjectOffsets.z, ScreenSetup.perspectiveAdjustment),
                ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom, out var point));
            screen.Add(point);
        }
        return screen;
    }

    private static void AssertLiesOnTerrain(OmegaObject3D shadow, float pitch, float ridgeHeight)
    {
        float radians = pitch * MathF.PI / 180f;
        foreach (var vertex in Vertices(shadow))
        {
            float elevation = ridgeHeight * (1f - MathF.Abs(vertex.x) / (SurfaceSetup.tileSize * 18f));
            float groundY = vertex.z / MathF.Tan(radians) - elevation / MathF.Sin(radians);
            Assert.AreEqual(groundY - ObjectShadowManager.TerrainShadowSurfaceLift, vertex.y, 0.003f);
        }
    }
}
