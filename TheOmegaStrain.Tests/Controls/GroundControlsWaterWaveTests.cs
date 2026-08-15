using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class GroundControlsWaterWaveTests
{
    private float _originalAmplitude;
    private int _originalMinimumVisibleTiles;
    private float _originalSpeed;
    private float _originalLength;
    private int _originalMaxHeight;

    [TestInitialize]
    public void Setup()
    {
        _originalAmplitude = SurfaceAnimationSetup.WaterWaveAmplitude;
        _originalMinimumVisibleTiles = SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles;
        _originalSpeed = SurfaceAnimationSetup.WaterWaveSpeedRadiansPerSecond;
        _originalLength = SurfaceAnimationSetup.WaterWaveLengthInTiles;
        _originalMaxHeight = MapSetup.maxHeight;

        SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles = 10;
        SurfaceAnimationSetup.WaterWaveSpeedRadiansPerSecond = 1.65f;
        SurfaceAnimationSetup.WaterWaveLengthInTiles = 3.25f;
        MapSetup.maxHeight = 100;

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<_3dObject>(),
            DirtyTiles = new List<IVector3>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        SurfaceAnimationSetup.WaterWaveAmplitude = _originalAmplitude;
        SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles = _originalMinimumVisibleTiles;
        SurfaceAnimationSetup.WaterWaveSpeedRadiansPerSecond = _originalSpeed;
        SurfaceAnimationSetup.WaterWaveLengthInTiles = _originalLength;
        MapSetup.maxHeight = _originalMaxHeight;
    }

    [TestMethod]
    public void MoveObject_WaterWave_UsesTransientDepthForViewportAndRestoresGlobalMap()
    {
        SurfaceAnimationSetup.WaterWaveAmplitude = 30f;
        GameState.SurfaceState.Global2DMap = CreateMap(size: 30, waterDepth: 0);

        var ground = CreateGround();

        new GroundControls().MoveObject(ground, null, null);

        Assert.IsTrue(GetMaxSurfaceZ(ground) > 0.1f, "Visible water should use temporary mapDepth while the viewport is generated.");
        AssertMapDepthsAre(0);
    }

    [TestMethod]
    public void MoveObject_WaterWave_DoesNotAnimateGroupsBelowMinimumVisibleTileCount()
    {
        SurfaceAnimationSetup.WaterWaveAmplitude = 30f;
        SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles = 9999;
        GameState.SurfaceState.Global2DMap = CreateMap(size: 30, waterDepth: 0);

        var ground = CreateGround();

        new GroundControls().MoveObject(ground, null, null);

        Assert.AreEqual(0f, GetMaxSurfaceZ(ground), 0.001f, "Water groups smaller than the configured threshold should stay still.");
        AssertMapDepthsAre(0);
    }

    [TestMethod]
    public void MoveObject_WaterWave_UsesAdjustableAmplitude()
    {
        float lowAmplitudeZ = AnimateWaterAndGetMaxZ(amplitude: 4f);
        float highAmplitudeZ = AnimateWaterAndGetMaxZ(amplitude: 30f);

        Assert.IsTrue(lowAmplitudeZ > 0.1f, "Visible water should animate when amplitude is above zero.");
        Assert.IsTrue(highAmplitudeZ > lowAmplitudeZ * 2f, "Increasing WaterWaveAmplitude should make the generated viewport wave stronger.");
    }

    [TestMethod]
    public void MoveObject_WaterWave_PreservesWaterEdges()
    {
        SurfaceAnimationSetup.WaterWaveAmplitude = 30f;
        var map = CreateMapWithWaterRectangle(size: 30, grassDepth: 40, waterDepth: 0, minX: 5, maxX: 20, minZ: 4, maxZ: 20);
        GameState.SurfaceState.Global2DMap = map;

        var baselineGround = CreateGround();
        SurfaceAnimationSetup.WaterWaveAmplitude = 0f;
        new GroundControls().MoveObject(baselineGround, null, null);
        string edgeColorWithoutWave = GetFirstSurfaceColorForTile(baselineGround, map[7, 5].mapId);

        var wavedGround = CreateGround();
        SurfaceAnimationSetup.WaterWaveAmplitude = 30f;
        new GroundControls().MoveObject(wavedGround, null, null);

        Assert.IsTrue(IsBlueDominant(edgeColorWithoutWave), "The shoreline test tile should start as water.");
        Assert.IsTrue(IsBlueDominant(GetFirstSurfaceColorForTile(wavedGround, map[7, 5].mapId)), "Water edge color should not switch surface type while inner water waves.");
        Assert.IsTrue(GetMaxSurfaceZForTile(wavedGround, map[8, 8].mapId) > 0.1f, "Inner water with water on every side should still animate.");
        AssertMapDepthsAreRestored(map, grassDepth: 40, waterDepth: 0, minX: 5, maxX: 20, minZ: 4, maxZ: 20);
    }

    [TestMethod]
    public void SurfaceViewPort_WhenWaterTileIsMarkedInfected_DoesNotRenderInfectionRed()
    {
        var map = CreateMap(size: 30, waterDepth: 0);
        map[4, 0].isInfected = true;
        GameState.SurfaceState.Global2DMap = map;

        var viewport = new Surface().GetSurfaceViewPort();
        int infectedWaterId = map[4, 0].mapId;
        var colors = GetSurfacePart(viewport).Triangles
            .Where(triangle => triangle.landBasedPosition == infectedWaterId)
            .Select(triangle => triangle.Color)
            .ToList();

        Assert.IsTrue(colors.Count > 0, "Expected the infected water tile to be visible in the generated viewport.");
        Assert.IsTrue(colors.All(IsBlueDominant), "Water tiles should keep water coloring even if a stale infection flag exists.");
    }

    [TestMethod]
    public void SurfaceViewPort_WinterShoreline_UsesPaletteDepthWithoutSnowWhiteJump()
    {
        GameState.SurfaceState.SceneBiome = SceneBiomeTypes.Winter;
        var map = CreateMapWithWaterRectangle(
            size: 30,
            grassDepth: 40,
            waterDepth: 0,
            minX: 5,
            maxX: 5,
            minZ: 4,
            maxZ: 4);
        GameState.SurfaceState.Global2DMap = map;

        Assert.AreEqual(0, map[4, 5].mapDepth);
        Assert.AreEqual(40, map[4, 6].mapDepth);

        var viewport = new Surface().GetSurfaceViewPort();
        string shorelineColor = GetFirstSurfaceColorForTile(viewport, map[4, 5].mapId);
        int renderDepth = (map[4, 5].mapDepth + map[4, 6].mapDepth) / 2;
        TerrainPaletteHelpers.GetTerrainColorRgb(
            renderDepth,
            MapSetup.maxHeight,
            GameState.SurfaceState.SceneBiome,
            out int expectedRed,
            out int expectedGreen,
            out int expectedBlue);

        Assert.AreEqual(ToHex(expectedRed, expectedGreen, expectedBlue), shorelineColor,
            "Winter shoreline rendering should still use the normal depth-based palette calculation.");
        Assert.IsTrue(IsColdShorelineNotSnowWhite(shorelineColor),
            $"Winter low-depth shoreline should not jump to the snow-white land color. Color was {shorelineColor}.");
    }

    private static float AnimateWaterAndGetMaxZ(float amplitude)
    {
        SurfaceAnimationSetup.WaterWaveAmplitude = amplitude;
        GameState.SurfaceState.Global2DMap = CreateMap(size: 30, waterDepth: 0);

        var ground = CreateGround();
        new GroundControls().MoveObject(ground, null, null);

        AssertMapDepthsAre(0);
        return GetMaxSurfaceZ(ground);
    }

    private static SurfaceData[,] CreateMap(int size, int waterDepth)
    {
        var map = new SurfaceData[size, size];
        int mapId = 0;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                map[z, x] = new SurfaceData
                {
                    mapDepth = waterDepth,
                    mapId = ++mapId,
                    isInfected = false
                };
            }
        }

        return map;
    }

    private static SurfaceData[,] CreateMapWithWaterRectangle(
        int size,
        int grassDepth,
        int waterDepth,
        int minX,
        int maxX,
        int minZ,
        int maxZ)
    {
        var map = new SurfaceData[size, size];
        int mapId = 0;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isWater = x >= minX && x <= maxX && z >= minZ && z <= maxZ;
                map[z, x] = new SurfaceData
                {
                    mapDepth = isWater ? waterDepth : grassDepth,
                    mapId = ++mapId,
                    isInfected = false
                };
            }
        }

        return map;
    }

    private static _3dObject CreateGround()
    {
        return new _3dObject
        {
            ObjectId = 100,
            ObjectName = "Surface",
            ImpactStatus = new ImpactStatus(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            ParentSurface = new Surface()
        };
    }

    private static float GetMaxSurfaceZ(I3dObject obj)
    {
        var triangles = GetSurfacePart(obj).Triangles;
        float maxZ = float.MinValue;

        foreach (var triangle in triangles)
        {
            maxZ = MathF.Max(maxZ, triangle.vert1.z);
            maxZ = MathF.Max(maxZ, triangle.vert2.z);
            maxZ = MathF.Max(maxZ, triangle.vert3.z);
        }

        return maxZ;
    }

    private static I3dObjectPart GetSurfacePart(I3dObject obj)
    {
        var part = obj.ObjectParts.SingleOrDefault(part => part.PartName == "Surface");
        Assert.IsNotNull(part, "Expected a Surface object part.");
        return part;
    }

    private static float GetMaxSurfaceZForTile(I3dObject obj, int mapId)
    {
        var tileTriangles = GetSurfacePart(obj).Triangles
            .Where(triangle => triangle.landBasedPosition == mapId)
            .ToList();

        Assert.IsTrue(tileTriangles.Count > 0, $"Expected visible triangles for tile {mapId}.");

        float maxZ = float.MinValue;
        foreach (var triangle in tileTriangles)
        {
            maxZ = MathF.Max(maxZ, triangle.vert1.z);
            maxZ = MathF.Max(maxZ, triangle.vert2.z);
            maxZ = MathF.Max(maxZ, triangle.vert3.z);
        }

        return maxZ;
    }

    private static string GetFirstSurfaceColorForTile(I3dObject obj, int mapId)
    {
        var color = GetSurfacePart(obj).Triangles
            .Where(triangle => triangle.landBasedPosition == mapId)
            .Select(triangle => triangle.Color)
            .FirstOrDefault();

        Assert.IsNotNull(color, $"Expected visible triangles for tile {mapId}.");
        return color!;
    }

    private static bool IsBlueDominant(string? color)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(color), "Expected a generated surface color.");

        int red = Convert.ToInt32(color![..2], 16);
        int blue = Convert.ToInt32(color[4..6], 16);
        return blue > red;
    }

    private static bool IsColdShorelineNotSnowWhite(string? color)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(color), "Expected a generated surface color.");

        int red = Convert.ToInt32(color![..2], 16);
        int green = Convert.ToInt32(color[2..4], 16);
        int blue = Convert.ToInt32(color[4..6], 16);
        return red < 180 && green < 210 && blue > red;
    }

    private static string ToHex(int red, int green, int blue)
    {
        return $"{red:X2}{green:X2}{blue:X2}";
    }

    private static void AssertMapDepthsAre(int expectedDepth)
    {
        foreach (var tile in GameState.SurfaceState.Global2DMap!)
        {
            Assert.AreEqual(expectedDepth, tile.mapDepth, "Water animation should restore the real mapDepth after the viewport is generated.");
        }
    }

    private static void AssertMapDepthsAreRestored(SurfaceData[,] map, int grassDepth, int waterDepth, int minX, int maxX, int minZ, int maxZ)
    {
        for (int z = 0; z < map.GetLength(0); z++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                bool isWater = x >= minX && x <= maxX && z >= minZ && z <= maxZ;
                Assert.AreEqual(isWater ? waterDepth : grassDepth, map[z, x].mapDepth, "Water animation should restore every tile depth after the viewport is generated.");
            }
        }
    }
}
