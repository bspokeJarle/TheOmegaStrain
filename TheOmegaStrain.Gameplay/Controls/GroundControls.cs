using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class GroundControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ITriangleMeshWithColor? GuideCoordinates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public I3dObject ParentObject { get; set; }

        public float zPosition { get; set; } = 0;
        public IPhysics Physics { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private bool enableLogging = false;
        private readonly HashSet<int> _processedBombCraters = new();
        private DateTime _lastWaterWaveFrame = DateTime.MinValue;
        private float _waterWaveTimeSeconds;
        private const int BombCraterRadiusTiles = 2;
        private const int WaterWaveEdgePaddingTiles = 1;
        private const float WaterWaveTilePhaseJitterRadians = 0.20944f; // about 12 degrees

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            if (theObject.ImpactStatus.HasCrashed == true)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log($"GroundControls: {theObject.ImpactStatus.ObjectName} has crashed! Handle the crash.");
            }

            DetectBombCraters();

            if (theObject != null && theObject.ParentSurface != null)
            {
                List<WaterDepthBackup>? waterDepthBackups = null;
                try
                {
                    waterDepthBackups = ApplyTransientWaterDepthAnimation(theObject.ParentSurface);

                    // Replace the surfaces from the new viewport - other objects might have moved surface position.
                    var newViewPort = theObject.ParentSurface.GetSurfaceViewPort();
                    theObject.ObjectParts = newViewPort.ObjectParts;
                    theObject.CrashBoxes = newViewPort.CrashBoxes;
                    theObject.CrashBoxNames = newViewPort.CrashBoxNames;
                }
                finally
                {
                    RestoreTransientWaterDepthAnimation(waterDepthBackups);
                }
            }

            return theObject!;
        }

        private List<WaterDepthBackup>? ApplyTransientWaterDepthAnimation(ISurface surface)
        {
            if (SurfaceAnimationSetup.WaterWaveAmplitude <= 0f) return null;

            var surfaceState = GameState.SurfaceState;
            var global2DMap = surfaceState?.Global2DMap;
            if (global2DMap == null) return null;

            var visibleWaterTiles = GetVisibleWaterTiles(
                global2DMap,
                surfaceState.GlobalMapPosition,
                surface.TileSize(),
                surface.ViewPortSize());

            if (visibleWaterTiles.Count < Math.Max(1, SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles))
                return null;

            var animatedWaterTiles = GetLargeVisibleWaterBodies(visibleWaterTiles, global2DMap);
            if (animatedWaterTiles.Count == 0) return null;

            int maxAnimatedWaterDepth = GetMaxAnimatedWaterDepth();
            if (maxAnimatedWaterDepth <= 0) return null;

            AdvanceWaterWaveTime();

            var backups = new List<WaterDepthBackup>(animatedWaterTiles.Count);
            foreach (var tileCoord in animatedWaterTiles)
            {
                ref var tile = ref global2DMap[tileCoord.z, tileCoord.x];
                int originalDepth = tile.mapDepth;
                int animatedDepth = CalculateAnimatedWaterDepth(originalDepth, tileCoord.x, tileCoord.z, maxAnimatedWaterDepth);

                if (animatedDepth == originalDepth) continue;

                backups.Add(new WaterDepthBackup(tileCoord.x, tileCoord.z, originalDepth));
                tile.mapDepth = animatedDepth;
            }

            return backups.Count == 0 ? null : backups;
        }

        private static void RestoreTransientWaterDepthAnimation(List<WaterDepthBackup>? backups)
        {
            if (backups == null || backups.Count == 0) return;

            var global2DMap = GameState.SurfaceState?.Global2DMap;
            if (global2DMap == null) return;

            int mapHeight = global2DMap.GetLength(0);
            int mapWidth = global2DMap.GetLength(1);

            foreach (var backup in backups)
            {
                if ((uint)backup.Z >= (uint)mapHeight || (uint)backup.X >= (uint)mapWidth)
                    continue;

                global2DMap[backup.Z, backup.X].mapDepth = backup.OriginalDepth;
            }
        }

        private void AdvanceWaterWaveTime()
        {
            var now = DateTime.Now;
            float deltaSeconds = GameState.GameplayBaselineDeltaTime;
            if (_lastWaterWaveFrame != DateTime.MinValue)
            {
                deltaSeconds = (float)(now - _lastWaterWaveFrame).TotalSeconds;
                deltaSeconds = Math.Clamp(deltaSeconds, 0f, 0.1f);
            }

            _lastWaterWaveFrame = now;
            _waterWaveTimeSeconds += deltaSeconds;
        }

        private int CalculateAnimatedWaterDepth(int originalDepth, int tileX, int tileZ, int maxAnimatedWaterDepth)
        {
            float waveLength = Math.Max(0.25f, SurfaceAnimationSetup.WaterWaveLengthInTiles);
            float speed = SurfaceAnimationSetup.WaterWaveSpeedRadiansPerSecond;
            float phaseA = ((tileX + tileZ) / waveLength) * (2f * MathF.PI);
            float phaseB = ((tileX - tileZ) / (waveLength * 1.7f)) * (2f * MathF.PI);
            float tilePhaseOffset = GetTilePhaseOffset(tileX, tileZ);

            float wave =
                (MathF.Sin(phaseA + (_waterWaveTimeSeconds * speed) + tilePhaseOffset) * 0.72f) +
                (MathF.Sin(phaseB + (_waterWaveTimeSeconds * speed * 0.61f) - (tilePhaseOffset * 0.7f)) * 0.28f);

            float normalizedWave = (wave + 1f) * 0.5f;
            int depthOffset = (int)MathF.Round(normalizedWave * SurfaceAnimationSetup.WaterWaveAmplitude);
            return Math.Clamp(originalDepth + depthOffset, 0, maxAnimatedWaterDepth);
        }

        private static float GetTilePhaseOffset(int tileX, int tileZ)
        {
            float phaseNoise =
                (MathF.Sin((tileX * 0.61f) + (tileZ * 0.37f)) * 0.65f) +
                (MathF.Sin((tileX * 0.19f) - (tileZ * 0.73f)) * 0.35f);

            return phaseNoise * WaterWaveTilePhaseJitterRadians;
        }

        private static int GetMaxAnimatedWaterDepth()
        {
            int maxHeight = Math.Max(1, MapSetup.maxHeight);
            return Math.Max(0, (int)MathF.Ceiling(maxHeight * 0.15f) - 1);
        }

        private static List<(int x, int z)> GetVisibleWaterTiles(
            SurfaceData[,] global2DMap,
            IVector3 globalMapPosition,
            int tileSize,
            int viewPortSize)
        {
            int mapHeight = global2DMap.GetLength(0);
            int mapWidth = global2DMap.GetLength(1);
            int safeTileSize = Math.Max(1, tileSize);
            int safeViewPortSize = Math.Max(1, viewPortSize);
            int rowLimit = (int)(safeViewPortSize / 1.5f) + 2;
            int mapZIndex = GetViewportMapIndex(globalMapPosition.z, safeTileSize, mapHeight);
            int mapXIndex = GetViewportMapIndex(globalMapPosition.x, safeTileSize, mapWidth);

            var visibleWaterTiles = new List<(int x, int z)>();
            var seen = new HashSet<int>();

            for (int i = 1; i < rowLimit; i++)
            {
                int currentMapY = MapCoordinateHelpers.WrapIndex(mapZIndex + i, mapHeight);

                for (int j = 0; j < safeViewPortSize - 1; j++)
                {
                    int currentMapX = MapCoordinateHelpers.WrapIndex(mapXIndex + j, mapWidth);
                    int key = GetTileKey(currentMapX, currentMapY, mapWidth);

                    if (!seen.Add(key)) continue;
                    if (!IsWaterTerrain(global2DMap[currentMapY, currentMapX].mapDepth)) continue;

                    visibleWaterTiles.Add((currentMapX, currentMapY));
                }
            }

            return visibleWaterTiles;
        }

        private static int GetViewportMapIndex(float worldCoordinate, int tileSize, int mapSize)
        {
            int index = ((int)worldCoordinate / tileSize) % mapSize;
            return index < 0 ? index + mapSize : index;
        }

        private static List<(int x, int z)> GetLargeVisibleWaterBodies(List<(int x, int z)> visibleWaterTiles, SurfaceData[,] global2DMap)
        {
            var animatedTiles = new List<(int x, int z)>();
            var visibleWaterKeys = new HashSet<int>();
            var visited = new HashSet<int>();
            int mapWidth = global2DMap.GetLength(1);

            foreach (var tile in visibleWaterTiles)
                visibleWaterKeys.Add(GetTileKey(tile.x, tile.z, mapWidth));

            foreach (var tile in visibleWaterTiles)
            {
                int tileKey = GetTileKey(tile.x, tile.z, mapWidth);
                if (visited.Contains(tileKey)) continue;

                var component = GetVisibleWaterComponent(tile, visibleWaterKeys, global2DMap);
                foreach (var componentTile in component)
                    visited.Add(GetTileKey(componentTile.x, componentTile.z, mapWidth));

                if (component.Count < Math.Max(1, SurfaceAnimationSetup.WaterWaveMinimumVisibleTiles))
                    continue;

                foreach (var componentTile in component)
                {
                    if (HasWaterBufferOnAllSides(componentTile.x, componentTile.z, global2DMap))
                        animatedTiles.Add(componentTile);
                }
            }

            return animatedTiles;
        }

        private static bool HasWaterBufferOnAllSides(int tileX, int tileZ, SurfaceData[,] global2DMap)
        {
            int mapHeight = global2DMap.GetLength(0);
            int mapWidth = global2DMap.GetLength(1);

            for (int dz = -WaterWaveEdgePaddingTiles; dz <= WaterWaveEdgePaddingTiles; dz++)
            {
                for (int dx = -WaterWaveEdgePaddingTiles; dx <= WaterWaveEdgePaddingTiles; dx++)
                {
                    int wrappedX = MapCoordinateHelpers.WrapIndex(tileX + dx, mapWidth);
                    int wrappedZ = MapCoordinateHelpers.WrapIndex(tileZ + dz, mapHeight);

                    if (!IsWaterTerrain(global2DMap[wrappedZ, wrappedX].mapDepth))
                        return false;
                }
            }

            return true;
        }

        private static List<(int x, int z)> GetVisibleWaterComponent(
            (int x, int z) startCoordinate,
            HashSet<int> visibleWaterKeys,
            SurfaceData[,] global2DMap)
        {
            int mapHeight = global2DMap.GetLength(0);
            int mapWidth = global2DMap.GetLength(1);
            var component = new List<(int x, int z)>();
            var queued = new HashSet<int> { GetTileKey(startCoordinate.x, startCoordinate.z, mapWidth) };
            var queue = new Queue<(int x, int z)>();
            queue.Enqueue(startCoordinate);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                TryQueueNeighbor(current.x + 1, current.z);
                TryQueueNeighbor(current.x - 1, current.z);
                TryQueueNeighbor(current.x, current.z + 1);
                TryQueueNeighbor(current.x, current.z - 1);
            }

            return component;

            void TryQueueNeighbor(int x, int z)
            {
                int wrappedX = MapCoordinateHelpers.WrapIndex(x, mapWidth);
                int wrappedZ = MapCoordinateHelpers.WrapIndex(z, mapHeight);
                int neighborKey = GetTileKey(wrappedX, wrappedZ, mapWidth);

                if (!visibleWaterKeys.Contains(neighborKey)) return;
                if (!queued.Add(neighborKey)) return;

                queue.Enqueue((wrappedX, wrappedZ));
            }
        }

        private static int GetTileKey(int x, int z, int mapWidth)
        {
            return (z * mapWidth) + x;
        }

        private static bool IsWaterTerrain(int mapDepth)
        {
            var terrain = GamePlayHelpers.GetTerrainType(mapDepth, MapSetup.maxHeight);
            return terrain == GamePlayHelpers.TerrainType.DeepWater ||
                   terrain == GamePlayHelpers.TerrainType.Coast;
        }

        private readonly struct WaterDepthBackup
        {
            public WaterDepthBackup(int x, int z, int originalDepth)
            {
                X = x;
                Z = z;
                OriginalDepth = originalDepth;
            }

            public int X { get; }
            public int Z { get; }
            public int OriginalDepth { get; }
        }

        private void DetectBombCraters()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            var global2DMap = GameState.SurfaceState?.Global2DMap;
            if (aiObjects == null || global2DMap == null) return;

            int mapHeight = global2DMap.GetLength(0);
            int mapWidth = global2DMap.GetLength(1);
            var rnd = new Random();

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (obj.ObjectName != "BomberBomb") continue;
                if (obj.ImpactStatus?.HasCrashed != true && obj.ImpactStatus?.HasExploded != true) continue;
                if (obj.ImpactStatus?.ObjectName != "Surface") continue;
                if (_processedBombCraters.Contains(obj.ObjectId)) continue;

                _processedBombCraters.Add(obj.ObjectId);

                var wp = obj.WorldPosition;
                if (wp == null) continue;

                int centerX = MapCoordinateHelpers.WorldXToTileIndex(wp.x, global2DMap);
                int centerZ = MapCoordinateHelpers.WorldZToTileIndex(wp.z, global2DMap);

                for (int dz = -BombCraterRadiusTiles; dz <= BombCraterRadiusTiles; dz++)
                {
                    for (int dx = -BombCraterRadiusTiles; dx <= BombCraterRadiusTiles; dx++)
                    {
                        if ((dx * dx) + (dz * dz) > BombCraterRadiusTiles * BombCraterRadiusTiles)
                            continue;

                        int tileZ = MapCoordinateHelpers.WrapIndex(centerZ + dz, mapHeight);
                        int tileX = MapCoordinateHelpers.WrapIndex(centerX + dx, mapWidth);

                        ref var tile = ref global2DMap[tileZ, tileX];
                        if (!tile.isCratered && IsCraterableTerrain(tile.mapDepth))
                        {
                            tile.isCratered = true;
                            tile.mapDepth = Math.Max(GetMinimumDryCraterDepth(), tile.mapDepth - rnd.Next(10, 21));
                        }
                    }
                }

                if (Logger.ShouldLog(enableLogging))
                    Logger.Log($"GroundControls: Bomb crater at tile x={centerX}; z={centerZ}");
            }
        }

        private static bool IsCraterableTerrain(int mapDepth)
        {
            var terrain = GamePlayHelpers.GetTerrainType(mapDepth, MapSetup.maxHeight);
            return terrain == GamePlayHelpers.TerrainType.Grassland ||
                   terrain == GamePlayHelpers.TerrainType.Highlands ||
                   terrain == GamePlayHelpers.TerrainType.Mountains;
        }

        private static int GetMinimumDryCraterDepth()
        {
            return (int)Math.Ceiling(MapSetup.maxHeight * 0.15f);
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }
    }
}
