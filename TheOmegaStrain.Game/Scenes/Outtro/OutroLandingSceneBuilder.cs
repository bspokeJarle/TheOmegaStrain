using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Outro
{
    public class OutroLandingSceneBuilder
    {
        public const int ScreenSpan = 3;
        public const int LandingPlatformSizeTiles = 8;
        public const int OutroMapMaxHeight = 75;
        public const int LandingPlatformDepth = 28;
        public const int BannerOffsetAbovePlatform = 250;
        public const int LandingShipStartHeightAbovePlatform = 760;
        public const int LandingShipFinalHeightAbovePlatform = 190;
        public const int LandingShipStartDepthAbovePlatform = 92;
        public const int LandingShipFinalDepthAbovePlatform = 68;

        public void Build(I3dWorld world)
        {
            ArgumentNullException.ThrowIfNull(world);

            var surface = new Surface();
            var map = CreateLandingMap(surface);

            ResetSurfaceState(map);
            world.WorldInhabitants.Clear();

            var surfaceObject = CreateSurfaceObject(surface);
            SeedVisibleLandBasedIds(surface, surfaceObject);
            world.WorldInhabitants.Add(surfaceObject);
            GameState.SurfaceState.SurfaceViewportObject = surfaceObject;

            AddBackgroundStars(world);
            AddPlatform(world, surface);
            AddTrees(world, surface, map);
            AddHouses(world, surface, map);
            AddBanner(world, surface, map);
        }

        public static Vector3 CreateInitialSurfaceOffset()
        {
            return new Vector3
            {
                x = 70 * ScreenSetup.ScreenScaleX,
                y = 1120 * ScreenSetup.ScreenScaleY,
                z = 400
            };
        }

        public static Vector3 CreateFinalSurfaceOffset()
        {
            return new Vector3
            {
                x = 70 * ScreenSetup.ScreenScaleX,
                y = 500 * ScreenSetup.ScreenScaleY,
                z = 400
            };
        }

        public static Vector3 CreateRevealInitialOffset(Vector3 finalOffset)
        {
            var surfaceInitial = CreateInitialSurfaceOffset();
            var surfaceFinal = CreateFinalSurfaceOffset();
            return new Vector3
            {
                x = finalOffset.x,
                y = finalOffset.y + (surfaceInitial.y - surfaceFinal.y),
                z = finalOffset.z
            };
        }

        public static Vector3 CreateFinalPlatformOffset()
        {
            return new Vector3 { x = 70 * ScreenSetup.ScreenScaleX, y = 500 * ScreenSetup.ScreenScaleY, z = 402 };
        }

        public static Vector3 CreateFinalTreeOffset()
        {
            return new Vector3 { x = 40 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
        }

        public static Vector3 CreateFinalHouseOffset()
        {
            return new Vector3 { x = 40 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
        }

        public static Vector3 CreateFinalBannerOffset()
        {
            var platformOffset = CreateFinalPlatformOffset();
            return new Vector3 { x = platformOffset.x, y = platformOffset.y - (BannerOffsetAbovePlatform * ScreenSetup.ScreenScaleY), z = 430 };
        }

        public static Vector3 CreateInitialLandingShipOffset()
        {
            var finalOffset = CreateFinalLandingShipOffset();
            var platformOffset = CreateFinalPlatformOffset();
            return new Vector3
            {
                x = finalOffset.x,
                y = finalOffset.y - (LandingShipStartHeightAbovePlatform * ScreenSetup.ScreenScaleY),
                z = platformOffset.z + LandingShipStartDepthAbovePlatform
            };
        }

        public static Vector3 CreateFinalLandingShipOffset()
        {
            var platformOffset = CreateFinalPlatformOffset();
            return new Vector3
            {
                x = platformOffset.x,
                y = platformOffset.y - (LandingShipFinalHeightAbovePlatform * ScreenSetup.ScreenScaleY),
                z = platformOffset.z + LandingShipFinalDepthAbovePlatform
            };
        }

        public static Vector3 CreateFinalAstronautOffset()
        {
            return CreateFinalLandingShipOffset();
        }

        public static Vector3 CreateFinalGroundStarsOffset()
        {
            return new Vector3 { x = 0, y = 160 * ScreenSetup.ScreenScaleY, z = 360 };
        }

        public static Vector3 CreateFinalFireworksOffset()
        {
            return new Vector3 { x = 0, y = 160 * ScreenSetup.ScreenScaleY, z = 365 };
        }

        private static SurfaceData[,] CreateLandingMap(Surface surface)
        {
            int viewPortSize = surface.ViewPortSize();
            int mapSize = viewPortSize * ScreenSpan;
            var map = new SurfaceData[mapSize, mapSize];
            int center = mapSize / 2;
            int mapId = 0;

            for (int z = 0; z < mapSize; z++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    mapId++;
                    int dx = x - center;
                    int dz = z - center;
                    int ripple = ((x * 17) + (z * 23) + (dx * dz)) % 11;
                    int distance = Math.Abs(dx) + Math.Abs(dz);
                    int depth = 17 + (ripple / 2) + Math.Max(0, 5 - (distance / 5));

                    map[z, x] = new SurfaceData
                    {
                        mapDepth = Math.Clamp(depth, 16, 27),
                        mapId = mapId,
                        isInfected = false
                    };
                }
            }

            AddLandingPlatform(map);
            return map;
        }

        private static void AddLandingPlatform(SurfaceData[,] map)
        {
            int centerZ = map.GetLength(0) / 2;
            int centerX = map.GetLength(1) / 2;
            int topLeftZ = centerZ - (LandingPlatformSizeTiles / 2);
            int topLeftX = centerX - (LandingPlatformSizeTiles / 2);

            for (int z = topLeftZ; z < topLeftZ + LandingPlatformSizeTiles; z++)
            {
                for (int x = topLeftX; x < topLeftX + LandingPlatformSizeTiles; x++)
                {
                    map[z, x].mapDepth = LandingPlatformDepth;
                }
            }

            map[topLeftZ, topLeftX].crashBox = new SurfaceData.CrashBoxData
            {
                width = LandingPlatformSizeTiles,
                height = LandingPlatformSizeTiles,
                boxDepth = LandingPlatformDepth + 20
            };
        }

        private static void ResetSurfaceState(SurfaceData[,] map)
        {
            MapSetup.maxHeight = OutroMapMaxHeight;

            GameState.SurfaceState.Global2DMap = map;
            GameState.SurfaceState.GlobalMapPosition = CreateInitialMapPosition(map);
            GameState.SurfaceState.GlobalMapPixels = null;
            GameState.SurfaceState.SurfaceHash = 0;
            GameState.SurfaceState.SurfaceFilePath = null;
            GameState.SurfaceState.SurfaceViewportObject = null;
            GameState.SurfaceState.SceneBiome = SceneBiomeTypes.HillsWoods;
            GameState.SurfaceState.AiObjects.Clear();
            GameState.SurfaceState.DirtyTiles.Clear();
            GameState.SurfaceState.FishJumpAreas.Clear();
            GameState.SurfaceState.PendingLocalInfectionSpread.Clear();
            GameState.SurfaceState.ScreenEcoMetas = CreateEcoMap(map);

            GameState.GamePlayState.TotalBioTiles = CountBioTiles(GameState.SurfaceState.ScreenEcoMetas);
        }

        private static Vector3 CreateInitialMapPosition(SurfaceData[,] map)
        {
            int viewPortSize = SurfaceSetup.viewPortSize;
            int rowLimit = (int)(viewPortSize / 1.5f) + 2;
            int centerZ = map.GetLength(0) / 2;
            int centerX = map.GetLength(1) / 2;

            return new Vector3
            {
                x = (centerX - (viewPortSize / 2)) * SurfaceSetup.tileSize,
                y = 0,
                z = (centerZ - (rowLimit / 2)) * SurfaceSetup.tileSize
            };
        }

        private static ScreenEcoMeta[,] CreateEcoMap(SurfaceData[,] map)
        {
            var ecoMap = new ScreenEcoMeta[MapSetup.screensPrMap, MapSetup.screensPrMap];
            int tilesPerScreen = SurfaceSetup.viewPortSize;
            int tileSize = SurfaceSetup.tileSize;
            int screenCount = 0;

            for (int screenZ = 0; screenZ < ecoMap.GetLength(0); screenZ++)
            {
                for (int screenX = 0; screenX < ecoMap.GetLength(1); screenX++)
                {
                    screenCount++;
                    ecoMap[screenZ, screenX].ScreenCount = screenCount;
                    ecoMap[screenZ, screenX].BioTiles = new List<TileCoord>();
                }
            }

            for (int z = 1; z < map.GetLength(0) - 1; z++)
            {
                for (int x = 1; x < map.GetLength(1) - 1; x++)
                {
                    var terrainType = GamePlayHelpers.GetTerrainType(map[z, x].mapDepth, MapSetup.maxHeight);
                    if (terrainType != GamePlayHelpers.TerrainType.Grassland &&
                        terrainType != GamePlayHelpers.TerrainType.Highlands)
                    {
                        continue;
                    }

                    int screenZ = z / tilesPerScreen;
                    int screenX = x / tilesPerScreen;
                    if ((uint)screenZ >= (uint)ecoMap.GetLength(0) || (uint)screenX >= (uint)ecoMap.GetLength(1))
                        continue;

                    ecoMap[screenZ, screenX].BioTileCount++;
                    ecoMap[screenZ, screenX].BioTiles.Add(new TileCoord
                    {
                        Y = z * tileSize,
                        X = x * tileSize
                    });
                }
            }

            return ecoMap;
        }

        private static int CountBioTiles(ScreenEcoMeta[,] ecoMap)
        {
            int total = 0;
            for (int z = 0; z < ecoMap.GetLength(0); z++)
            {
                for (int x = 0; x < ecoMap.GetLength(1); x++)
                {
                    total += ecoMap[z, x].BioTileCount;
                }
            }

            return total;
        }

        private static _3dObject CreateSurfaceObject(Surface surface)
        {
            var surfaceObject = (_3dObject)surface.GetSurfaceViewPort();
            surfaceObject.ObjectName = "Surface";
            surfaceObject.ObjectOffsets = CreateInitialSurfaceOffset();
            surfaceObject.Rotation = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 };
            surfaceObject.WorldPosition = new Vector3 { };
            surfaceObject.Movement = new GroundControls();
            surfaceObject.ParentSurface = surface;
            surfaceObject.ImpactStatus = new ImpactStatus { };
            surfaceObject.CrashBoxDebugMode = false;
            surfaceObject.CrashBoxesFollowRotation = false;
            return surfaceObject;
        }

        private static void AddBackgroundStars(I3dWorld world)
        {
            var stars = OutroGroundStars.CreateStarField();
            stars.ObjectOffsets = CreateRevealInitialOffset(CreateFinalGroundStarsOffset());
            world.WorldInhabitants.Add(stars);
        }

        private static void AddPlatform(I3dWorld world, Surface surface)
        {
            var platform = OutroLandingPlatform.CreatePlatform(surface);
            platform.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            platform.SurfaceBasedId = null;
            platform.ObjectOffsets = CreateRevealInitialOffset(CreateFinalPlatformOffset());
            platform.ImpactStatus = new ImpactStatus { };
            platform.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(platform);
        }

        private static void SeedVisibleLandBasedIds(Surface surface, _3dObject surfaceObject)
        {
            surface.LandBasedIds.Clear();
            surface.RotatedSurfaceTriangleByLandId.Clear();

            foreach (var part in surfaceObject.ObjectParts)
            {
                foreach (var triangle in part.Triangles)
                {
                    surface.LandBasedIds.Add(triangle.landBasedPosition);
                    if (triangle.landBasedPosition.HasValue)
                        surface.RotatedSurfaceTriangleByLandId[triangle.landBasedPosition.Value] = triangle;
                }
            }
        }

        private static void AddTrees(I3dWorld world, Surface surface, SurfaceData[,] map)
        {
            int centerZ = map.GetLength(0) / 2;
            int centerX = map.GetLength(1) / 2;
            var placements = new (int x, int z)[]
            {
                (centerX - 8, centerZ - 6),
                (centerX - 6, centerZ - 6),
                (centerX - 4, centerZ - 6),
                (centerX + 5, centerZ - 6),
                (centerX + 7, centerZ - 6),
                (centerX - 8, centerZ - 5),
                (centerX - 6, centerZ - 5),
                (centerX - 7, centerZ + 5),
                (centerX - 3, centerZ + 6),
                (centerX + 4, centerZ - 6),
                (centerX + 7, centerZ - 4),
                (centerX + 7, centerZ + 5),
                (centerX - 8, centerZ + 1),
                (centerX + 3, centerZ + 6),
                (centerX - 8, centerZ + 6),
                (centerX - 6, centerZ + 6),
                (centerX - 4, centerZ + 6),
                (centerX + 5, centerZ + 6),
                (centerX + 7, centerZ + 6),
                (centerX - 8, centerZ + 3),
                (centerX + 7, centerZ + 3),
                (centerX - 8, centerZ - 2),
                (centerX + 7, centerZ - 2),
                (centerX - 3, centerZ - 6),
                (centerX + 3, centerZ - 6)
            };

            foreach (var placement in placements)
            {
                FlattenPlacement(map, placement.x, placement.z, radius: 1, depth: 24);
                var tree = Tree.CreateTree(surface);
                tree.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                tree.SurfaceBasedId = map[placement.z, placement.x].mapId;
                map[placement.z, placement.x].hasLandbasedObject = true;
                tree.ObjectOffsets = CreateRevealInitialOffset(CreateFinalTreeOffset());
                tree.ObjectName = "Tree";
                tree.Movement = new TreeControls();
                tree.ImpactStatus = new ImpactStatus { };
                tree.CrashBoxDebugMode = false;
                world.WorldInhabitants.Add(tree);
            }
        }

        private static void AddHouses(I3dWorld world, Surface surface, SurfaceData[,] map)
        {
            int centerZ = map.GetLength(0) / 2;
            int centerX = map.GetLength(1) / 2;
            var placements = new (int x, int z)[]
            {
                (centerX - 7, centerZ + 4),
                (centerX + 6, centerZ - 4),
                (centerX - 7, centerZ - 4),
                (centerX + 6, centerZ + 4)
            };

            foreach (var placement in placements)
            {
                FlattenPlacement(map, placement.x, placement.z, radius: 1, depth: 25);
                var house = House.CreateHouse(surface);
                house.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                house.SurfaceBasedId = map[placement.z, placement.x].mapId;
                map[placement.z, placement.x].hasLandbasedObject = true;
                house.ObjectOffsets = CreateRevealInitialOffset(CreateFinalHouseOffset());
                house.ObjectName = "House";
                house.Movement = new HouseControls();
                house.ImpactStatus = new ImpactStatus { };
                house.CrashBoxDebugMode = false;
                world.WorldInhabitants.Add(house);
            }
        }

        private static void AddBanner(I3dWorld world, Surface surface, SurfaceData[,] map)
        {
            int centerZ = map.GetLength(0) / 2;
            int centerX = map.GetLength(1) / 2;

            FlattenPlacement(map, centerX, centerZ, radius: 1, depth: LandingPlatformDepth);

            var banner = OutroLandingBanner.CreateBanner(surface);
            banner.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            banner.SurfaceBasedId = null;
            banner.ObjectOffsets = CreateRevealInitialOffset(CreateFinalBannerOffset());
            banner.ObjectName = "OutroLandingBanner";
            banner.ImpactStatus = new ImpactStatus { };
            banner.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(banner);
        }

        public void AddLandingShip(I3dWorld world)
        {
            ArgumentNullException.ThrowIfNull(world);

            var parentSurface = GameState.SurfaceState.SurfaceViewportObject?.ParentSurface;
            var initialOffset = CreateInitialLandingShipOffset();
            var finalOffset = CreateFinalLandingShipOffset();
            var ship = Ship.CreateShip(parentSurface, new OutroLandingShipControls(initialOffset, finalOffset));
            ship.ObjectName = "Ship";
            ship.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            ship.SurfaceBasedId = null;
            ship.ObjectOffsets = initialOffset;
            ship.Rotation = OutroLandingShipControls.CreateInitialLandingRotation();
            ship.ZSortBias = OutroLandingShipControls.LandingZSortBias;
            ship.CrashBoxes = new List<List<IVector3>>();
            ship.CrashBoxDebugMode = false;
            ship.CrashBoxesFollowRotation = false;
            ship.ImpactStatus = new ImpactStatus { };
            world.WorldInhabitants.Add(ship);
        }

        public void AddAstronaut(I3dWorld world)
        {
            ArgumentNullException.ThrowIfNull(world);

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                if (world.WorldInhabitants[i].ObjectName == OutroAstronaut.ObjectName)
                    return;
            }

            var parentSurface = GameState.SurfaceState.SurfaceViewportObject?.ParentSurface;
            var astronaut = OutroAstronaut.CreateAstronaut(parentSurface);
            astronaut.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            astronaut.SurfaceBasedId = null;
            astronaut.ObjectOffsets = CreateFinalAstronautOffset();
            astronaut.Rotation = OutroLandingShipControls.CreateFinalLandingRotation();
            astronaut.CrashBoxDebugMode = false;
            astronaut.CrashBoxesFollowRotation = false;
            astronaut.ImpactStatus = new ImpactStatus { };
            world.WorldInhabitants.Add(astronaut);
        }

        public void AddFireworks(I3dWorld world)
        {
            ArgumentNullException.ThrowIfNull(world);

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                if (world.WorldInhabitants[i].ObjectName == OutroFireworks.ObjectName)
                    return;
            }

            var fireworks = OutroFireworks.CreateFireworks();
            fireworks.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            fireworks.SurfaceBasedId = null;
            fireworks.ObjectOffsets = CreateFinalFireworksOffset();
            fireworks.Rotation = new Vector3();
            fireworks.CrashBoxDebugMode = false;
            fireworks.CrashBoxesFollowRotation = false;
            fireworks.ImpactStatus = new ImpactStatus { };
            world.WorldInhabitants.Add(fireworks);
        }

        private static void FlattenPlacement(SurfaceData[,] map, int centerX, int centerZ, int radius, int depth)
        {
            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (z < 0 || x < 0 || z >= map.GetLength(0) || x >= map.GetLength(1))
                        continue;

                    map[z, x].mapDepth = depth;
                }
            }
        }
    }
}
