using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class GroundControlsCraterTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            Global2DMap = new SurfaceData[5, 5],
            AiObjects = new List<OmegaObject3D>()
        };

        for (int z = 0; z < 5; z++)
        {
            for (int x = 0; x < 5; x++)
            {
                GameState.SurfaceState.Global2DMap[z, x] = new SurfaceData
                {
                    mapDepth = 20,
                    isInfected = false
                };
            }
        }
    }

    [TestMethod]
    public void MoveObject_WhenBombCratersSurface_DoesNotCraterWaterOrCoastTiles()
    {
        SetTile(1, 1, 0);  // DeepWater
        SetTile(2, 1, 5);  // Coast
        SetTile(1, 2, 20); // Grassland
        SetTile(2, 2, 40); // Highlands
        SetTile(3, 2, 60); // Mountains

        GameState.SurfaceState.AiObjects.Add(CreateSurfaceBombAtTile(2, 2));

        var ground = new OmegaObject3D
        {
            ObjectId = 100,
            ObjectName = "Surface",
            ImpactStatus = new ImpactStatus(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3()
        };

        new GroundControls().MoveObject(ground, null, null);

        Assert.IsFalse(GameState.SurfaceState.Global2DMap![1, 1].isCratered, "Deep water should keep its normal water rendering.");
        Assert.AreEqual(0, GameState.SurfaceState.Global2DMap[1, 1].mapDepth);
        Assert.IsFalse(GameState.SurfaceState.Global2DMap[1, 2].isCratered, "Coast/water edge should not become black or grey.");
        Assert.AreEqual(5, GameState.SurfaceState.Global2DMap[1, 2].mapDepth);

        AssertDryCratered(1, 2, GamePlayHelpers.TerrainType.Grassland);
        AssertDryCratered(2, 2, GamePlayHelpers.TerrainType.Highlands);
        AssertDryCratered(3, 2, GamePlayHelpers.TerrainType.Mountains);
    }

    [TestMethod]
    public void MoveObject_WhenBombImpactIsInLowerRightOfTile_CratersContainingTileArea()
    {
        for (int z = 0; z < 5; z++)
        {
            for (int x = 0; x < 5; x++)
            {
                SetTile(x, z, 0);
            }
        }

        SetTile(1, 1, 20);

        GameState.SurfaceState.AiObjects.Add(new OmegaObject3D
        {
            ObjectId = 9002,
            ObjectName = "BomberBomb",
            WorldPosition = new Vector3
            {
                x = (2 * SurfaceSetup.tileSize) + (SurfaceSetup.tileSize * 0.75f),
                y = 0f,
                z = (2 * SurfaceSetup.tileSize) + (SurfaceSetup.tileSize * 0.75f)
            },
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = "Surface"
            }
        });

        var ground = new OmegaObject3D
        {
            ObjectId = 100,
            ObjectName = "Surface",
            ImpactStatus = new ImpactStatus(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3()
        };

        new GroundControls().MoveObject(ground, null, null);

        AssertDryCratered(1, 1, GamePlayHelpers.TerrainType.Grassland);
    }

    [TestMethod]
    public void MoveObject_WhenVisibleBombImpacts_CratersTileUnderRenderedBomb()
    {
        ResizeMap(128);
        GameState.SurfaceState.SurfaceViewportObject = new OmegaObject3D
        {
            ObjectId = 8000,
            ObjectOffsets = new Vector3(70f, 500f, 400f)
        };
        var bomb = new OmegaObject3D
        {
            ObjectId = 9003,
            ObjectName = "BomberBomb",
            WorldPosition = new Vector3(
                10.25f * SurfaceSetup.tileSize,
                0f,
                10.25f * SurfaceSetup.tileSize),
            ObjectOffsets = new Vector3(0f, 0f, 600f),
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = "Surface"
            }
        };
        var footprint = SurfacePositionSyncHelpers.GetSurfaceFootprintWorldPosition(bomb);
        int expectedX = MapCoordinateHelpers.WorldXToTileIndex(footprint.x, GameState.SurfaceState.Global2DMap);
        int expectedZ = MapCoordinateHelpers.WorldZToTileIndex(footprint.z, GameState.SurfaceState.Global2DMap);
        int rawX = MapCoordinateHelpers.WorldXToTileIndex(bomb.WorldPosition.x, GameState.SurfaceState.Global2DMap);
        int rawZ = MapCoordinateHelpers.WorldZToTileIndex(bomb.WorldPosition.z, GameState.SurfaceState.Global2DMap);
        GameState.SurfaceState.AiObjects.Add(bomb);

        var ground = new OmegaObject3D
        {
            ObjectId = 100,
            ObjectName = "Surface",
            ImpactStatus = new ImpactStatus(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3()
        };

        new GroundControls().MoveObject(ground, null, null);

        Assert.IsTrue(GameState.SurfaceState.Global2DMap![expectedZ, expectedX].isCratered,
            "The crater must be centred under the bomb's rendered impact position.");
        Assert.IsFalse(GameState.SurfaceState.Global2DMap[rawZ, rawX].isCratered,
            "The bomb's raw viewport-corner origin must not receive the crater.");
    }

    [TestMethod]
    public void MoveObject_WhenBombCratersSurface_UsesLargerBlastRadius()
    {
        // The crater radius scales with the surface tile resolution, so grow the map and
        // centre the bomb so the full blast (and its corners) stay inside the map bounds.
        int radius = SurfaceSetup.ScaleTileCount(2);
        int size = radius * 2 + 3;
        int centre = size / 2;
        ResizeMap(size);

        GameState.SurfaceState.AiObjects.Add(CreateSurfaceBombAtTile(centre, centre));

        var ground = new OmegaObject3D
        {
            ObjectId = 100,
            ObjectName = "Surface",
            ImpactStatus = new ImpactStatus(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3()
        };

        new GroundControls().MoveObject(ground, null, null);

        AssertDryCratered(centre - radius, centre, GamePlayHelpers.TerrainType.Grassland);
        AssertDryCratered(centre + radius, centre, GamePlayHelpers.TerrainType.Grassland);
        AssertDryCratered(centre, centre - radius, GamePlayHelpers.TerrainType.Grassland);
        AssertDryCratered(centre, centre + radius, GamePlayHelpers.TerrainType.Grassland);
        Assert.IsFalse(GameState.SurfaceState.Global2DMap![centre - radius, centre - radius].isCratered,
            "Blast radius should be wider than before without turning into a full square.");
    }

    private static void ResizeMap(int size)
    {
        GameState.SurfaceState.Global2DMap = new SurfaceData[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                GameState.SurfaceState.Global2DMap[z, x] = new SurfaceData
                {
                    mapDepth = 20,
                    isInfected = false
                };
            }
        }
    }

    private static void SetTile(int x, int z, int depth)
    {
        GameState.SurfaceState.Global2DMap![z, x] = new SurfaceData
        {
            mapDepth = depth,
            isInfected = false
        };
    }

    private static OmegaObject3D CreateSurfaceBombAtTile(int x, int z)
    {
        return new OmegaObject3D
        {
            ObjectId = 9001,
            ObjectName = "BomberBomb",
            WorldPosition = new Vector3
            {
                x = x * SurfaceSetup.tileSize,
                y = 0f,
                z = z * SurfaceSetup.tileSize
            },
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = "Surface"
            }
        };
    }

    private static void AssertDryCratered(int x, int z, GamePlayHelpers.TerrainType originalTerrain)
    {
        var tile = GameState.SurfaceState.Global2DMap![z, x];
        Assert.IsTrue(tile.isCratered, $"{originalTerrain} should be allowed to show crater damage.");
        Assert.IsTrue(tile.mapDepth >= Math.Ceiling(MapSetup.maxHeight * 0.15f), "Cratered dry terrain should not be pushed down into water/coast height.");
    }
}
