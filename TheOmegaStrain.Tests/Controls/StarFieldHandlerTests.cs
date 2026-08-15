using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using TheOmegaStrain.Runtime.Rendering;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class StarFieldHandlerTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 0f }
        };
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void GenerateStarfield_CreatesWorldSpacePoolWithoutOffsetSync()
    {
        var handler = new StarFieldHandler(CreateSurface());

        handler.GenerateStarfield();

        var stars = handler.GetStars();
        Assert.AreEqual(StarFieldHandler.TargetStarCount, handler.PooledStarCount);
        Assert.AreEqual(StarFieldHandler.TargetStarCount, handler.RenderableStarCount);
        Assert.AreEqual(StarFieldHandler.TargetStarCount, stars.Count);

        Assert.IsTrue(stars.All(s => s.ObjectName == "Star"));
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
    public void GenerateStarfield_RecyclesMostStarsAheadOfTravelDirection()
    {
        var handler = new StarFieldHandler(CreateSurface());
        handler.GenerateStarfield();

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 30f };
        handler.GenerateStarfield();

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 500f, z = 5000f };
        handler.GenerateStarfield();
        handler.GenerateStarfield();

        var stars = handler.GetStars();
        int ahead = stars.Count(s => s.WorldPosition.z - GameState.SurfaceState.GlobalMapPosition.z > 700f);

        Assert.IsTrue(
            ahead >= StarFieldHandler.TargetStarCount * 3 / 5,
            $"Recycled stars should be biased ahead of travel. Ahead={ahead}");
    }

    private static Surface CreateSurface()
    {
        return new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>()
        };
    }

    private static int GetFirstStarColorIntensity(List<_3dObject> stars)
    {
        string color = stars[0].ObjectParts[0].Triangles[0].Color ?? "000000";
        color = color.TrimStart('#');

        int red = Convert.ToInt32(color.Substring(0, 2), 16);
        int green = Convert.ToInt32(color.Substring(2, 2), 16);
        int blue = Convert.ToInt32(color.Substring(4, 2), 16);
        return red + green + blue;
    }
}
