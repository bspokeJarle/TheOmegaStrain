using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Runtime.Rendering;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class StarFieldHandlerTests
{
    private int originalScreenWidth;
    private int originalScreenHeight;

    [TestInitialize]
    public void Setup()
    {
        originalScreenWidth = ScreenSetup.screenSizeX;
        originalScreenHeight = ScreenSetup.screenSizeY;
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 0f }
        };
        GameState.ObjectIdCounter = 0;
    }

    [TestCleanup]
    public void Cleanup()
    {
        ScreenSetup.Initialize(originalScreenWidth, originalScreenHeight);
    }

    [TestMethod]
    public void GenerateStarfield_CreatesWorldSpacePoolWithoutOffsetSync()
    {
        var handler = new StarFieldHandler(CreateSurface());

        handler.GenerateStarfield();

        var stars = handler.GetStars();
        Assert.AreEqual(StarFieldHandler.TargetStarCount, handler.PooledStarCount);
        Assert.IsTrue(handler.RenderableStarCount > 0);
        Assert.IsTrue(handler.RenderableStarCount < handler.PooledStarCount,
            "Off-screen reserve stars should stay pooled without entering the render list.");
        Assert.AreEqual(handler.RenderableStarCount, stars.Count);

        Assert.IsTrue(stars.All(s => s.ObjectName == "Star"));
        Assert.IsTrue(stars.All(OmegaPerspectiveProjectorFactory.IntersectsViewport),
            "A star should begin rendering only when its projected geometry intersects the viewport.");
        Assert.IsTrue(stars.All(s => s.Movement == null), "Stars should not run the old per-object sync movement.");
        Assert.IsTrue(stars.All(s => s.CrashBoxes.Count == 0));
        Assert.IsTrue(stars.All(s => s.ObjectOffsets.x == 0f && s.ObjectOffsets.y == 0f && s.ObjectOffsets.z == 0f));
        Assert.IsTrue(stars.Any(s => s.WorldPosition.x != 0f || s.WorldPosition.z != 0f));
    }

    [TestMethod]
    public void GenerateStarfield_FadesStarsIn()
    {
        var handler = new StarFieldHandler(CreateSurface());

        handler.GenerateStarfield();
        int firstFrameIntensity = GetFirstStarColorIntensity(handler.GetStars());

        for (int i = 0; i < 30; i++)
            handler.GenerateStarfield();

        int settledIntensity = GetFirstStarColorIntensity(handler.GetStars());

        Assert.IsTrue(settledIntensity > firstFrameIntensity, "Stars should brighten after they appear instead of popping to full brightness.");
    }

    [TestMethod]
    public void GenerateStarfield_KeepsStarRotationZeroSoGeometryDoesNotAccumulate()
    {
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        var first = handler.GetStars()[0];
        var startVert = first.ObjectParts[0].Triangles[0].vert1;
        float startX = startVert.x;
        float startY = startVert.y;

        for (int frame = 0; frame < 30; frame++)
            handler.GenerateStarfield();

        // Stars are appended after the per-frame deep copy, so a non-zero Rotation would be
        // re-applied to these exact triangles every frame and the angle would accumulate.
        foreach (var star in handler.GetStars())
        {
            Assert.AreEqual(0f, star.Rotation.x);
            Assert.AreEqual(0f, star.Rotation.y);
            Assert.AreEqual(0f, star.Rotation.z);
        }

        Assert.AreEqual(startX, first.ObjectParts[0].Triangles[0].vert1.x, 0.0001f,
            "Star geometry must not drift between frames.");
        Assert.AreEqual(startY, first.ObjectParts[0].Triangles[0].vert1.y, 0.0001f,
            "Star geometry must not drift between frames.");
    }

    [TestMethod]
    public void GenerateStarfield_ClampsApparentSizeWithoutShrinkingOverTime()
    {
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        var first = handler.GetStars()[0];
        float afterFirstFrame = MaxVertexRadius(first);

        // The clamp must be recomputed from an immutable base mesh. If it were applied to the
        // live triangles the factor would compound and stars would collapse to nothing, since
        // stars bypass the per-frame deep copy.
        for (int frame = 0; frame < 60; frame++)
            handler.GenerateStarfield();

        Assert.AreEqual(afterFirstFrame, MaxVertexRadius(first), 0.0001f,
            "Star size must not compound across frames.");
        Assert.IsTrue(afterFirstFrame > 0f, "Star must not collapse to a point.");
    }

    [DataTestMethod]
    [DataRow(1280, 720)]
    [DataRow(1500, 1024)]
    [DataRow(2560, 1440)]
    public void GenerateStarfield_UsesProjectedViewportAtDifferentResolutions(int width, int height)
    {
        ScreenSetup.Initialize(width, height);
        GameState.SurfaceState.GlobalMapPosition.y = 500f * ScreenSetup.ScreenScaleY;
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        var stars = handler.GetStars();
        Assert.IsNotNull(stars);
        Assert.IsTrue(stars.Count > 0);
        Assert.IsTrue(stars.All(OmegaPerspectiveProjectorFactory.IntersectsViewport),
            $"Star visibility should follow the {width}x{height} viewport rather than fixed world bounds.");
    }

    private static float MaxVertexRadius(OmegaObject3D star)
    {
        float max = 0f;
        foreach (var tri in star.ObjectParts[0].Triangles)
        {
            foreach (var v in new[] { tri.vert1, tri.vert2, tri.vert3 })
                max = MathF.Max(max, MathF.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z));
        }

        return max;
    }

    [TestMethod]
    public void GenerateStarfield_FadesOutBelowAltitudeWithoutClearingPool()
    {
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        for (int i = 0; i < 30; i++)
            handler.GenerateStarfield();

        Assert.AreEqual(StarFieldHandler.TargetStarCount, handler.PooledStarCount);
        Assert.AreEqual(0, handler.RenderableStarCount);
        Assert.IsFalse(handler.HasStars());

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 0f };
        handler.GenerateStarfield();

        Assert.AreEqual(StarFieldHandler.TargetStarCount, handler.PooledStarCount);
        Assert.IsTrue(handler.RenderableStarCount > 0, "The retained star pool should fade back in when altitude allows stars again.");
    }

    [TestMethod]
    public void GenerateStarfield_RecyclesExitedStarsAfterLargeMovement()
    {
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 30f };
        handler.GenerateStarfield();

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 5000f };
        for (int frame = 0; frame < 30; frame++)
            handler.GenerateStarfield();

        var stars = handler.GetStars();
        Assert.IsNotNull(stars);
        Assert.IsTrue(stars.Count > 0);
        Assert.IsTrue(stars.All(OmegaPerspectiveProjectorFactory.IntersectsViewport),
            "Stars that have fully exited should be recycled instead of lingering beyond the viewport.");
    }

    private static Surface CreateSurface()
    {
        return new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColorAndTexture>()
        };
    }

    private static int GetFirstStarColorIntensity(List<OmegaObject3D> stars)
    {
        string color = stars[0].ObjectParts[0].Triangles[0].Color ?? "000000";
        color = color.TrimStart('#');

        int red = Convert.ToInt32(color.Substring(0, 2), 16);
        int green = Convert.ToInt32(color.Substring(2, 2), 16);
        int blue = Convert.ToInt32(color.Substring(4, 2), 16);
        return red + green + blue;
    }
}
