using _3dRotations.World.Objects;
using _3dRotations.Helpers;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class SurfaceViewportPoolingTests
{
    private int _originalMaxHeight;

    [TestInitialize]
    public void Setup()
    {
        _originalMaxHeight = MapSetup.maxHeight;
        MapSetup.maxHeight = 100;

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            Global2DMap = CreateMap(30),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        MapSetup.maxHeight = _originalMaxHeight;
    }

    [TestMethod]
    public void GetSurfaceViewPort_ReusesSurfaceTrianglesWithoutSharingReturnedPartState()
    {
        var surface = new Surface();
        int expectedTriangleCount = GetExpectedTriangleCount(surface.ViewPortSize());

        var firstViewport = surface.GetSurfaceViewPort();
        var firstPart = GetSurfacePart(firstViewport);
        var firstTriangle = firstPart.Triangles[0];

        firstPart.Triangles = new List<ITriangleMeshWithColor>();

        var secondViewport = surface.GetSurfaceViewPort();
        var secondPart = GetSurfacePart(secondViewport);

        Assert.AreEqual(expectedTriangleCount, secondPart.Triangles.Count, "Surface viewport should rebuild the full triangle list after a returned part is mutated by rendering.");
        Assert.AreSame(firstTriangle, secondPart.Triangles[0], "Surface viewport should reuse triangle instances to avoid per-frame mesh allocation.");
    }

    [TestMethod]
    public void GetSurfaceViewPort_AlignsGeneratedCrashBoxWithSourceTile()
    {
        var surface = new Surface
        {
            GlobalMapRotation = new Vector3()
        };
        var map = GameState.SurfaceState.Global2DMap!;
        var sourceTile = map[1, 0];
        sourceTile.mapDepth = 60;
        sourceTile.crashBox = new SurfaceData.CrashBoxData
        {
            width = 2,
            height = 3,
            boxDepth = 80
        };
        map[1, 0] = sourceTile;

        var viewport = surface.GetSurfaceViewPort();
        var surfacePart = GetSurfacePart(viewport);
        var sourceTriangle = surfacePart.Triangles.First(t => t.landBasedPosition == sourceTile.mapId);
        var terrainCrashBox = viewport.CrashBoxes.First();
        int tileSize = surface.TileSize();

        Assert.AreEqual(sourceTriangle.vert1.x, terrainCrashBox.Min(point => point.x), 0.001f, "Crashbox should start at the same X as its source terrain tile.");
        Assert.AreEqual(sourceTriangle.vert1.x + (2 * tileSize), terrainCrashBox.Max(point => point.x), 0.001f, "Crashbox width should match the source tile span.");
        Assert.AreEqual(sourceTriangle.vert1.y, terrainCrashBox.Min(point => point.y), 0.001f, "Crashbox should start at the same Y as its source terrain tile.");
        Assert.AreEqual(sourceTriangle.vert1.y + (3 * tileSize), terrainCrashBox.Max(point => point.y), 0.001f, "Crashbox height should match the source tile span.");
        Assert.AreEqual(80, terrainCrashBox.Max(point => point.z), 0.001f, "Crashbox depth should use the generated box depth, not only the anchor tile depth.");
        Assert.AreEqual("TerrainSurface", viewport.CrashBoxNames![0], "Generated terrain crashboxes should be named for debugging and collision logs.");
        Assert.AreEqual("MainSurface", viewport.CrashBoxNames.Last(), "The broad landing/collision surface should be identifiable separately from terrain crashboxes.");
    }

    [TestMethod]
    public void GetSurfaceViewPort_FallsBackToAnchorDepthWhenCrashBoxDepthIsMissing()
    {
        var surface = new Surface
        {
            GlobalMapRotation = new Vector3()
        };
        var map = GameState.SurfaceState.Global2DMap!;
        var sourceTile = map[1, 0];
        sourceTile.mapDepth = 60;
        sourceTile.crashBox = new SurfaceData.CrashBoxData
        {
            width = 2,
            height = 2,
            boxDepth = 0
        };
        map[1, 0] = sourceTile;

        var viewport = surface.GetSurfaceViewPort();
        var terrainCrashBox = viewport.CrashBoxes.First();

        Assert.AreEqual(100, terrainCrashBox.Max(point => point.z), 0.001f, "Old or incomplete surface files should not create zero-height terrain crashboxes.");
    }

    [TestMethod]
    public void GetSurfaceViewPort_RotatesTerrainCrashBoxesWithSurfacePitch()
    {
        var surface = new Surface
        {
            GlobalMapRotation = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 }
        };
        var map = GameState.SurfaceState.Global2DMap!;
        var sourceTile = map[1, 0];
        sourceTile.mapDepth = 60;
        sourceTile.crashBox = new SurfaceData.CrashBoxData
        {
            width = 2,
            height = 6,
            boxDepth = 80
        };
        map[1, 0] = sourceTile;

        var viewport = surface.GetSurfaceViewPort();
        var terrainCrashBox = viewport.CrashBoxes.First();
        int tileSize = surface.TileSize();
        float ySpan = terrainCrashBox.Max(point => point.y) - terrainCrashBox.Min(point => point.y);
        float zSpan = terrainCrashBox.Max(point => point.z) - terrainCrashBox.Min(point => point.z);

        Assert.AreEqual(sourceTile.mapDepth, map[1, 0].mapDepth, "Viewport generation should not mutate map depth while rotating terrain crashboxes.");
        Assert.IsTrue(ySpan < 6 * tileSize,
            "Rotated terrain crashboxes should follow the pitched surface instead of becoming tall screen-space walls.");
        Assert.IsTrue(zSpan > 80,
            "The pitched surface depth should move some of the map-Y span into Z, matching the rendered terrain transform.");
    }

    [TestMethod]
    public void GetSurfaceViewPort_SyncsTerrainCrashBoxDepthWithSurfaceAltitude()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 35f, z = 0f };
        var surface = new Surface
        {
            GlobalMapRotation = new Vector3()
        };
        var map = GameState.SurfaceState.Global2DMap!;
        var sourceTile = map[1, 0];
        sourceTile.mapDepth = 60;
        sourceTile.crashBox = new SurfaceData.CrashBoxData
        {
            width = 2,
            height = 2,
            boxDepth = 80
        };
        map[1, 0] = sourceTile;

        var viewport = surface.GetSurfaceViewPort();
        var terrainCrashBox = viewport.CrashBoxes.First();

        Assert.AreEqual(-35f, terrainCrashBox.Min(point => point.z), 0.001f,
            "Terrain crashbox sealevel should move with the surface altitude.");
        Assert.AreEqual(45f, terrainCrashBox.Max(point => point.z), 0.001f,
            "Terrain crashbox depth should use boxDepth after applying surface altitude sync.");
    }

    [TestMethod]
    public void GetSurfaceViewPort_SyncsMainSurfaceCrashBoxWithSurfaceAltitude()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 75f, z = 0f };
        var surface = new Surface();

        var viewport = surface.GetSurfaceViewPort();
        var mainSurfaceCrashBox = viewport.CrashBoxes.Last();

        Assert.AreEqual(-33f, mainSurfaceCrashBox.Min(point => point.y), 0.001f,
            "MainSurface crashbox top should sit slightly above the visible surface while following surface altitude.");
        Assert.AreEqual(1075f, mainSurfaceCrashBox.Max(point => point.y), 0.001f,
            "MainSurface crashbox bottom should keep following the surface altitude.");
    }

    [TestMethod]
    public void GetEffectiveCrashOffset_DoesNotApplySurfaceAltitudeTwice()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 120f, z = 0f };
        var surfaceObject = new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Surface",
            ObjectOffsets = new Vector3 { x = 105f, y = 500f, z = 400f },
            CrashBoxes = new List<List<IVector3>>()
        };

        var offset = surfaceObject.GetEffectiveCrashOffset();

        Assert.AreEqual(500f, offset.y, 0.001f,
            "Surface altitude sync belongs in the generated surface crashbox data, not as an extra collision offset.");
    }

    private static int GetExpectedTriangleCount(int viewPortSize)
    {
        int rowLimit = (int)(viewPortSize / 1.5) + 2;
        int tileCount = (rowLimit - 1) * (viewPortSize - 1);
        return tileCount * 2;
    }

    private static I3dObjectPart GetSurfacePart(I3dObject viewport)
    {
        var part = viewport.ObjectParts.SingleOrDefault(part => part.PartName == "Surface");
        Assert.IsNotNull(part, "Expected a Surface object part.");
        return part;
    }

    private static SurfaceData[,] CreateMap(int size)
    {
        var map = new SurfaceData[size, size];
        int mapId = 0;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                map[z, x] = new SurfaceData
                {
                    mapDepth = 20,
                    mapId = ++mapId,
                    isInfected = false
                };
            }
        }

        return map;
    }
}
