using _3dRotations.World.Objects;
using CommonUtilities.GamePlayHelpers;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Helpers
{
    public static class LeafTreePlacementHelpers
    {
        private const int PlatformBufferTiles = 2;
        private const int NearPlatformMinSpacingTiles = 2;
        private const int WaterBufferRadius = 1;

        public static void AddLeafTrees(
            I3dWorld world,
            ISurface surface,
            SurfaceData[,]? map,
            int mapSize,
            int tileSize,
            int maxHeight,
            int placementMax,
            int nearPlatformTarget,
            int nearPlatformSearchRadius,
            float treeOffsetX,
            float treeOffsetY,
            params List<(int x, int y, int height)>[] reservedPlacementGroups)
        {
            if (map == null)
                return;

            var reservedTiles = CreateReservedTileSet(reservedPlacementGroups);
            var placements = new List<(int x, int y, int height)>();

            AddNearPlatformLeafTreePlacements(
                map,
                maxHeight,
                nearPlatformTarget,
                nearPlatformSearchRadius,
                reservedTiles,
                placements);

            var candidates = SurfaceGeneration.FindTreePlacementAreas(
                map,
                mapSize,
                tileSize,
                maxHeight,
                placementMax);

            foreach (var candidate in candidates)
            {
                TryAddLeafTreePlacement(
                    map,
                    maxHeight,
                    candidate.x,
                    candidate.y,
                    reservedTiles,
                    placements,
                    minSpacingTiles: 0);
            }

            SurfaceGeneration.FlattenTerrainAroundPlacements(map, maxHeight, placements, radius: 1);

            foreach (var placement in placements)
            {
                var leafTree = LeafTree.CreateLeafTree(surface);
                leafTree.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                leafTree.SurfaceBasedId = map[placement.y, placement.x].mapId;
                map[placement.y, placement.x].hasLandbasedObject = true;
                leafTree.ObjectOffsets = new Vector3 { x = treeOffsetX, y = treeOffsetY, z = 400 };
                leafTree.ObjectName = "LeafTree";
                leafTree.Movement = new TreeControls();
                leafTree.ImpactStatus = new ImpactStatus { };
                leafTree.CrashBoxDebugMode = false;
                if (leafTree.SurfaceBasedId > 0)
                    world.WorldInhabitants.Add(leafTree);
            }
        }

        private static void AddNearPlatformLeafTreePlacements(
            SurfaceData[,] map,
            int maxHeight,
            int targetCount,
            int searchRadius,
            HashSet<(int x, int y)> reservedTiles,
            List<(int x, int y, int height)> placements)
        {
            if (targetCount <= 0)
                return;

            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            int minRadius = LandingPlatformHelpers.LandingPlatformSizeTiles + PlatformBufferTiles + 1;
            int maxRadius = Math.Max(minRadius, searchRadius);
            int placed = 0;

            for (int radius = minRadius; radius <= maxRadius && placed < targetCount; radius++)
            {
                for (int yOffset = -radius; yOffset <= radius && placed < targetCount; yOffset++)
                {
                    for (int xOffset = -radius; xOffset <= radius && placed < targetCount; xOffset++)
                    {
                        if (Math.Abs(xOffset) != radius && Math.Abs(yOffset) != radius)
                            continue;

                        int x = platformCenter.x + xOffset;
                        int y = platformCenter.z + yOffset;
                        if (TryAddLeafTreePlacement(
                                map,
                                maxHeight,
                                x,
                                y,
                                reservedTiles,
                                placements,
                                NearPlatformMinSpacingTiles))
                        {
                            placed++;
                        }
                    }
                }
            }
        }

        private static bool TryAddLeafTreePlacement(
            SurfaceData[,] map,
            int maxHeight,
            int x,
            int y,
            HashSet<(int x, int y)> reservedTiles,
            List<(int x, int y, int height)> placements,
            int minSpacingTiles)
        {
            if (!IsValidLeafTreeTile(map, maxHeight, x, y))
                return false;

            if (reservedTiles.Contains((x, y)))
                return false;

            if (minSpacingTiles > 0 && HasReservedTileNearby(reservedTiles, x, y, minSpacingTiles))
                return false;

            reservedTiles.Add((x, y));
            placements.Add((x, y, map[y, x].mapDepth));
            return true;
        }

        private static bool IsValidLeafTreeTile(SurfaceData[,] map, int maxHeight, int x, int y)
        {
            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            if (x < 2 || y < 2 || x >= sizeX - 2 || y >= sizeY - 2)
                return false;

            if (LandingPlatformHelpers.IsLandingPlatformTile(map, x, y, PlatformBufferTiles))
                return false;

            var tile = map[y, x];
            if (tile.hasLandbasedObject || tile.isCratered || tile.isInfected)
                return false;

            if (!IsDryLand(tile.mapDepth, maxHeight))
                return false;

            if (HasWaterWithinRadius(map, maxHeight, x, y, WaterBufferRadius))
                return false;

            return QuadTopLeftIsDry(map, maxHeight, x, y);
        }

        private static bool IsDryLand(int mapDepth, int maxHeight)
        {
            var terrain = GamePlayHelpers.GetTerrainType(mapDepth, maxHeight);
            return terrain == GamePlayHelpers.TerrainType.Grassland ||
                   terrain == GamePlayHelpers.TerrainType.Highlands;
        }

        private static bool HasWaterWithinRadius(SurfaceData[,] map, int maxHeight, int centerX, int centerY, int radius)
        {
            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            for (int yOffset = -radius; yOffset <= radius; yOffset++)
            {
                for (int xOffset = -radius; xOffset <= radius; xOffset++)
                {
                    int x = centerX + xOffset;
                    int y = centerY + yOffset;
                    if (x < 0 || y < 0 || x >= sizeX || y >= sizeY)
                        continue;

                    if (IsWaterOrCoastDepth(map[y, x].mapDepth, maxHeight))
                        return true;
                }
            }

            return false;
        }

        private static bool IsWaterOrCoastDepth(int mapDepth, int maxHeight)
        {
            if (mapDepth <= 0)
                return true;

            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            return mapDepth <= coastCutoff;
        }

        private static bool QuadTopLeftIsDry(SurfaceData[,] map, int maxHeight, int x, int y)
        {
            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            if (x < 0 || y < 0 || x >= sizeX - 1 || y >= sizeY - 1)
                return false;

            return IsDryLand(map[y, x].mapDepth, maxHeight) &&
                   IsDryLand(map[y, x + 1].mapDepth, maxHeight) &&
                   IsDryLand(map[y + 1, x].mapDepth, maxHeight) &&
                   IsDryLand(map[y + 1, x + 1].mapDepth, maxHeight);
        }

        private static bool HasReservedTileNearby(HashSet<(int x, int y)> reservedTiles, int x, int y, int radius)
        {
            for (int yOffset = -radius; yOffset <= radius; yOffset++)
                for (int xOffset = -radius; xOffset <= radius; xOffset++)
                    if (reservedTiles.Contains((x + xOffset, y + yOffset)))
                        return true;

            return false;
        }

        private static HashSet<(int x, int y)> CreateReservedTileSet(params List<(int x, int y, int height)>[] placementGroups)
        {
            var reserved = new HashSet<(int x, int y)>();
            foreach (var placementGroup in placementGroups)
                foreach (var placement in placementGroup)
                    reserved.Add((placement.x, placement.y));

            return reserved;
        }
    }
}
