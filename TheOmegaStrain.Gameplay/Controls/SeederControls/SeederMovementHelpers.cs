using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using System;
using static CommonUtilities.GamePlayHelpers.GamePlayHelpers;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    internal static class SeederMovementHelpers
    {
        // ============================
        // AI MOVEMENT CONFIGURATION
        // ============================
        // Tile and screen geometry:
        // - TileSize: world units per tile (pulled from SurfaceSetup).
        // - TilesPerScreen: number of tiles visible per screen viewport.
        private static int TileSize => SurfaceSetup.tileSize;            // e.g., 75 world units
        private static int TilesPerScreen => SurfaceSetup.viewPortSize;  // e.g., 18 tiles

        // Global roam behavior:
        // - RoamTilesDefault: how many tiles the random roam jumps when no screen target is found.
        private const int RoamTilesDefault = 10;

        // Screen sniffing behavior:
        // - SmellScoreWeight: multiplier for BioTileCount dominance when scoring screens.
        // - DistancePenaltyWeight: penalty per Manhattan distance step to break ties toward closer screens.
        private const int SmellScoreWeight = 1000;
        private const int DistancePenaltyWeight = 10;

        // Logging:
        // - EnableLogging: toggles helper-level logging via global Logger.
        private const bool EnableLogging = false;

        // World bounds derived from map setup:
        private static float MaxWorld => (MapSetup.globalMapSize - 1) * (float)TileSize;

        private static readonly Random _rng = new Random();

        internal static bool IsBioTerrain(SurfaceData tile)
        {
            var terrain = GamePlayHelpers.GetTerrainType(tile.mapDepth, MapSetup.maxHeight);
            return terrain == TerrainType.Grassland || terrain == TerrainType.Highlands;
        }

        internal static bool ClearInfectionIfTerrainCannotHostIt(SurfaceState surfaceState, int tileY, int tileX)
        {
            var map = surfaceState.Global2DMap;
            if (map == null) return false;

            int wrappedY = MapCoordinateHelpers.WrapIndex(tileY, map.GetLength(0));
            int wrappedX = MapCoordinateHelpers.WrapIndex(tileX, map.GetLength(1));
            var tile = map[wrappedY, wrappedX];

            if (IsBioTerrain(tile)) return false;

            RemoveBioTileFromEcoMap(surfaceState, wrappedY, wrappedX);
            if (!tile.isInfected) return false;

            tile.isInfected = false;
            map[wrappedY, wrappedX] = tile;
            surfaceState.DirtyTiles.Add(new Vector3 { x = wrappedX, y = 0, z = wrappedY });
            return true;
        }

        internal readonly struct MoveVector
        {
            public readonly Vector3 Direction; // normalized
            public readonly float Length;      // world units

            public MoveVector(Vector3 direction, float length)
            {
                Direction = direction;
                Length = length;
            }
        }

        // ------------------------------------------------------------
        // WORLD -> TILE INDEX -> SCREEN INDEX
        // ------------------------------------------------------------
        internal static void GetScreenIndexFromWorldXZ(Vector3 worldPos, out int screenY, out int screenX)
        {
            int tileX = (int)(worldPos.x / TileSize);
            int tileY = (int)(worldPos.z / TileSize);

            screenX = tileX / TilesPerScreen;
            screenY = tileY / TilesPerScreen;
        }

        internal static Vector3 GetScreenCenterWorldXZ(int screenY, int screenX, float keepY)
        {
            int centerTileX = (screenX * TilesPerScreen) + (TilesPerScreen / 2);
            int centerTileY = (screenY * TilesPerScreen) + (TilesPerScreen / 2);

            return new Vector3
            {
                x = centerTileX * TileSize,
                y = keepY,
                z = centerTileY * TileSize
            };
        }

        // ------------------------------------------------------------
        // GLOBAL SNIFF: pick best screen within radius (by BioTileCount)
        // ------------------------------------------------------------
        internal static bool TryFindBestScreenInRadius(
            ScreenEcoMeta[,] ecoMap,
            int currentY,
            int currentX,
            int radius,
            out int bestY,
            out int bestX)
        {
            bestY = currentY;
            bestX = currentX;

            int bestScore = int.MinValue;

            int maxY = ecoMap.GetLength(0);
            int maxX = ecoMap.GetLength(1);

            int startY = Math.Max(0, currentY - radius);
            int endY = Math.Min(maxY - 1, currentY + radius);

            int startX = Math.Max(0, currentX - radius);
            int endX = Math.Min(maxX - 1, currentX + radius);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int smell = ecoMap[y, x].BioTileCount;
                    if (smell <= 0) continue;

                    int dy = y - currentY;
                    int dx = x - currentX;
                    int dist = Math.Abs(dy) + Math.Abs(dx);

                    // smell dominates, distance breaks ties (tunable weights above)
                    int score = (smell * SmellScoreWeight) - (dist * DistancePenaltyWeight);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestY = y;
                        bestX = x;
                    }
                }
            }

            return bestScore != int.MinValue;
        }

        // ------------------------------------------------------------
        // ROAM: N tiles in random direction (clamped)
        // ------------------------------------------------------------
        internal static Vector3 GetRandomRoamTargetWorldXZ(Vector3 fromWorld, int roamTiles)
        {
            int dir = _rng.Next(0, 8);

            int dx = 0, dz = 0;
            switch (dir)
            {
                case 0: dx = 0; dz = -1; break;
                case 1: dx = 1; dz = -1; break;
                case 2: dx = 1; dz = 0; break;
                case 3: dx = 1; dz = 1; break;
                case 4: dx = 0; dz = 1; break;
                case 5: dx = -1; dz = 1; break;
                case 6: dx = -1; dz = 0; break;
                case 7: dx = -1; dz = -1; break;
            }

            // Allow callers to pass 0 to use default roam distance
            int tiles = roamTiles > 0 ? roamTiles : RoamTilesDefault;
            float step = tiles * TileSize;

            float x = fromWorld.x + (dx * step);
            float z = fromWorld.z + (dz * step);

            if (x < 0) x = 0;
            if (z < 0) z = 0;
            if (x > MaxWorld) x = MaxWorld;
            if (z > MaxWorld) z = MaxWorld;

            return new Vector3 { x = x, y = fromWorld.y, z = z };
        }

        // ------------------------------------------------------------
        // LOCAL HUNT: pick BioTile inside CURRENT screen.
        // BioTiles contain GLOBAL WORLD coords:
        //   tile.X = worldX, tile.Y = worldZ
        // For map validation: tileIndex = world / TileSize
        // ------------------------------------------------------------
        internal static bool TryPickLocalBioTargetWorldXZ(
            SurfaceState surfaceState,
            I3dObject obj,
            Func<SurfaceData, TerrainType> getTerrainType,
            out Vector3 targetWorld,
            ref int localPickCursor,
            int maxAttemptsPerCall = 32,
            bool validateAgainstMap = true)
        {
            targetWorld = default;

            var ecoMap = surfaceState.ScreenEcoMetas;
            var map = surfaceState.Global2DMap;

            // Screen indices use raw world coordinates to match the meta map.
            // Do NOT use GetSurfaceAlignedWorldPosition — it applies visual offsets
            // (surfaceOO − objectOO) that shift the tile lookup by several tiles.
            var rawWorld = (Vector3)obj.WorldPosition;

            GetScreenIndexFromWorldXZ(rawWorld, out int screenY, out int screenX);

            if ((uint)screenY >= (uint)ecoMap.GetLength(0) || (uint)screenX >= (uint)ecoMap.GetLength(1))
                return false;

            var bioTiles = ecoMap[screenY, screenX].BioTiles;
            if (bioTiles == null || bioTiles.Count == 0)
                return false;

            int attempts = Math.Min(maxAttemptsPerCall, bioTiles.Count);

            int skippedOutOfRange = 0;
            int skippedNotBio = 0;

            for (int i = 0; i < attempts; i++)
            {
                if (localPickCursor >= bioTiles.Count)
                    localPickCursor = 0;

                var tile = bioTiles[localPickCursor++]; // world coords

                // World bounds check
                if (tile.X < 0 || tile.Y < 0 || tile.X > MaxWorld || tile.Y > MaxWorld)
                {
                    skippedOutOfRange++;
                    continue;
                }

                // Convert WORLD -> tile indices for map validation.
                int tileX = MapCoordinateHelpers.WorldXToTileIndex(tile.X, map);
                int tileY = MapCoordinateHelpers.WorldZToTileIndex(tile.Y, map);

                if ((uint)tileX >= (uint)map.GetLength(1) || (uint)tileY >= (uint)map.GetLength(0))
                {
                    skippedOutOfRange++;
                    continue;
                }

                if (validateAgainstMap && map != null)
                {
                    var mapTile = map[tileY, tileX];
                    var terrain = getTerrainType(mapTile);
                    bool stillBio = terrain == TerrainType.Grassland || terrain == TerrainType.Highlands;
                    if (!stillBio)
                    {
                        ClearInfectionIfTerrainCannotHostIt(surfaceState, tileY, tileX);
                        skippedNotBio++;
                        continue;
                    }

                    if (mapTile.isInfected)
                    {
                        skippedNotBio++;
                        continue;
                    }
                }

                // IMPORTANT: BioTiles already are world coords -> do NOT multiply by tileSize again
                targetWorld = new Vector3
                {
                    x = tile.X,
                    y = rawWorld.y,
                    z = tile.Y
                };

                if (Logger.ShouldLog(EnableLogging))
                    Logger.Log($"AI:LOCAL_PICK onScreen={obj.IsOnScreen} screen=[{screenY},{screenX}] world=({tile.X},{tile.Y})");

                return true;
            }

            if (Logger.ShouldLog(EnableLogging))
                Logger.Log($"AI:LOCAL_EMPTY onScreen={obj.IsOnScreen} screen=[{screenY},{screenX}] list={bioTiles.Count} tried={attempts} outOfRange={skippedOutOfRange} notBio={skippedNotBio}");

            return false;
        }

        // ------------------------------------------------------------
        // ECO COUNT: decrement screen bio count for the screen containing (tileY,tileX)
        // Inputs are TILE INDICES (not world coords).
        // ------------------------------------------------------------
        internal static int? DecrementBioCountForTile(SurfaceState surfaceState, int tileY, int tileX)
        {
            int screenY = tileY / TilesPerScreen;
            int screenX = tileX / TilesPerScreen;

            var eco = surfaceState.ScreenEcoMetas;
            if ((uint)screenY >= (uint)eco.GetLength(0) || (uint)screenX >= (uint)eco.GetLength(1))
                return null;

            var meta = eco[screenY, screenX];
            if (meta.BioTileCount > 0) meta.BioTileCount--;
            eco[screenY, screenX] = meta;
            return meta.BioTileCount;
        }

        internal static bool RemoveBioTileFromEcoMap(SurfaceState surfaceState, int tileY, int tileX)
        {
            int screenY = tileY / TilesPerScreen;
            int screenX = tileX / TilesPerScreen;

            var eco = surfaceState.ScreenEcoMetas;
            if ((uint)screenY >= (uint)eco.GetLength(0) || (uint)screenX >= (uint)eco.GetLength(1))
                return false;

            var meta = eco[screenY, screenX];
            var bioList = meta.BioTiles;
            if (bioList == null) return false;

            int worldX = tileX * TileSize;
            int worldZ = tileY * TileSize;
            for (int bi = bioList.Count - 1; bi >= 0; bi--)
            {
                if (bioList[bi].X == worldX && bioList[bi].Y == worldZ)
                {
                    bioList.RemoveAt(bi);
                    if (meta.BioTileCount > 0) meta.BioTileCount--;
                    eco[screenY, screenX] = meta;
                    return true;
                }
            }

            return false;
        }

        internal static Vector3 StepTowardTargetWorldXZ(Vector3 current, Vector3 target, float step)
        {
            float dx = target.x - current.x;
            float dz = target.z - current.z;

            float lenSq = dx * dx + dz * dz;
            if (lenSq <= 0.00001f)
                return current;

            float len = (float)Math.Sqrt(lenSq);
            if (step > len) step = len;

            float nx = dx / len;
            float nz = dz / len;

            return new Vector3
            {
                x = current.x + nx * step,
                y = current.y,
                z = current.z + nz * step
            };
        }

        // ------------------------------------------------------------
        // LOCAL INFECTION SPREAD: process pending tiles whose delay has elapsed.
        // Each tile spreads infection to its 8 immediate neighbors. Newly
        // infected neighbors are added back to the pending list so infection
        // cascades outward over time. Call once per frame from the game loop.
        // ------------------------------------------------------------
        private static readonly int[][] SpreadNeighbors =
        [
            [0, -1], [1, 0], [0, 1], [-1, 0],
            [-1, -1], [1, -1], [1, 1], [-1, 1]
        ];

        internal static void ProcessLocalInfectionSpread(SurfaceState surfaceState)
        {
            float delaySec = GameState.GamePlayState.LocalInfectionSpreadDelaySec;
            if (delaySec <= 0f) return; // disabled for this scene

            var pending = surfaceState.PendingLocalInfectionSpread;
            if (pending.Count == 0) return;

            var map = surfaceState.Global2DMap;
            if (map == null) return;

            int mapHeight = map.GetLength(0);
            int mapWidth = map.GetLength(1);
            long now = DateTime.Now.Ticks;
            long delayTicks = (long)(delaySec * TimeSpan.TicksPerSecond);

            int tilesPerScreen = TilesPerScreen;
            int tileSz = TileSize;
            var eco = surfaceState.ScreenEcoMetas;

            // Cache alive seeder positions once per call (cheap: ~40 objects max)
            float spreadRadius = GameState.GamePlayState.LocalInfectionSpreadRadius;
            bool checkRange = spreadRadius > 0f;
            float radiusSq = spreadRadius * spreadRadius;
            int seederCount = 0;
            float[] seederXs = null;
            float[] seederZs = null;

            if (checkRange)
            {
                var aiObjects = surfaceState.AiObjects;
                seederXs = new float[aiObjects.Count];
                seederZs = new float[aiObjects.Count];
                for (int s = 0; s < aiObjects.Count; s++)
                {
                    if (aiObjects[s].ObjectName == "Seeder")
                    {
                        seederXs[seederCount] = aiObjects[s].WorldPosition.x;
                        seederZs[seederCount] = aiObjects[s].WorldPosition.z;
                        seederCount++;
                    }
                }
            }

            // Process from end so removals are safe
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (tx, tz, infectedTick) = pending[i];
                if (now - infectedTick < delayTicks) continue;

                // Remove this tile from pending — it will not be checked again
                pending[i] = pending[^1];
                pending.RemoveAt(pending.Count - 1);

                // Skip spread if no seeder is within range (killing seeders halts cascade)
                if (checkRange)
                {
                    float worldX = tx * tileSz + tileSz * 0.5f;
                    float worldZ = tz * tileSz + tileSz * 0.5f;
                    bool seederNearby = false;
                    for (int s = 0; s < seederCount; s++)
                    {
                        float dx = worldX - seederXs[s];
                        float dz = worldZ - seederZs[s];
                        if (dx * dx + dz * dz <= radiusSq) { seederNearby = true; break; }
                    }
                    if (!seederNearby) continue;
                }

                // Spread to 8 neighbors
                foreach (var d in SpreadNeighbors)
                {
                    int nx = MapCoordinateHelpers.WrapIndex(tx + d[0], mapWidth);
                    int nz = MapCoordinateHelpers.WrapIndex(tz + d[1], mapHeight);

                    var t = map[nz, nx];
                    if (!IsBioTerrain(t))
                    {
                        ClearInfectionIfTerrainCannotHostIt(surfaceState, nz, nx);
                        continue;
                    }

                    if (t.isInfected) continue;

                    // Infect the neighbor tile
                    GameState.GamePlayState.InfectionLevel += 1;
                    t.isInfected = true;
                    map[nz, nx] = t;
                    surfaceState.DirtyTiles.Add(new Vector3 { x = nx, y = 0, z = nz });
                    DecrementBioCountForTile(surfaceState, nz, nx);

                    // Remove from the screen's BioTiles list
                    int scrY = nz / tilesPerScreen;
                    int scrX = nx / tilesPerScreen;
                    if ((uint)scrY < (uint)eco.GetLength(0) && (uint)scrX < (uint)eco.GetLength(1))
                    {
                        var bioList = eco[scrY, scrX].BioTiles;
                        int worldX = nx * tileSz;
                        int worldZ = nz * tileSz;
                        for (int bi = bioList.Count - 1; bi >= 0; bi--)
                        {
                            if (bioList[bi].X == worldX && bioList[bi].Y == worldZ)
                            { bioList.RemoveAt(bi); break; }
                        }
                    }

                    // Add newly infected tile to pending so it cascades further
                    pending.Add((nx, nz, now));
                }
            }
        }
    }
}
