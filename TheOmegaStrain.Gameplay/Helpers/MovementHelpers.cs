using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    internal static class MovementHelpers
    {
        internal readonly struct MoveVector
        {
            public readonly Vector3 Direction; // normalized, world space
            public readonly float Length;      // world distance

            public MoveVector(Vector3 direction, float length)
            {
                Direction = direction;
                Length = length;
            }
        }

        /// <summary>
        /// "Smell" based movement: finds the best smelling screen (BioTileCount) around current screen.
        /// Returns a normalized direction vector (world space) and distance (world units).
        ///
        /// IMPORTANT: Map plane is X/Z. (Y is height.)
        /// Eco arrays use [screenY, screenX] where screenY is along Z and screenX is along X.
        /// </summary>
        internal static MoveVector FindMostSmellyDirection(
            I3dObject obj,
            in bool isOnScreen,
            int smellRadiusScreensOnScreen = 2,
            int smellRadiusScreensOffScreen = 6)
        {
            var ecoMap = GameState.SurfaceState.ScreenEcoMetas;
            if (ecoMap == null)
                return new MoveVector(new Vector3 { x = 0, y = 0, z = 0 }, 0);

            // Convert world position -> current screen
            GetScreenIndexFromWorldXZ((Vector3)obj.WorldPosition, out int currentScreenY, out int currentScreenX);

            int radius = isOnScreen ? smellRadiusScreensOnScreen : smellRadiusScreensOffScreen;

            // Find best screen near you; fallback to global best
            if (!TryFindBestScreenInRadius(ecoMap, currentScreenY, currentScreenX, radius, out int bestY, out int bestX))
            {
                FindGlobalBestScreen(ecoMap, out bestY, out bestX);
            }

            // Target: center of the chosen screen in world space (X/Z plane)
            Vector3 targetWorld = GetScreenCenterWorldXZ(bestY, bestX);

            // Return direction + length in world space
            return GetDirectionAndDistanceWorld((Vector3)obj.WorldPosition, targetWorld);
        }

        // ------------------------------------------------------------
        // Conversions: world (X/Z) -> tile -> screen
        // ------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetScreenIndexFromWorldXZ(Vector3 worldPos, out int screenY, out int screenX)
        {
            // World X/Z represent map plane, Y is height.
            // NOTE: This assumes worldPos.x and worldPos.z are non-negative.
            // If you can have negatives, replace with FloorDiv.
            int tileX = (int)(worldPos.x / SurfaceSetup.tileSize);
            int tileY = (int)(worldPos.z / SurfaceSetup.tileSize);

            int tilesPerScreen = SurfaceSetup.viewPortSize; // already "tiles per screen"

            screenX = tileX / tilesPerScreen;
            screenY = tileY / tilesPerScreen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector3 GetScreenCenterWorldXZ(int screenY, int screenX)
        {
            int tilesPerScreen = SurfaceSetup.viewPortSize;

            int centerTileX = (screenX * tilesPerScreen) + (tilesPerScreen / 2);
            int centerTileY = (screenY * tilesPerScreen) + (tilesPerScreen / 2);

            return new Vector3
            {
                x = centerTileX * SurfaceSetup.tileSize,
                y = 0, // height not used here
                z = centerTileY * SurfaceSetup.tileSize
            };
        }

        // ------------------------------------------------------------
        // Vector math in world space
        // ------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MoveVector GetDirectionAndDistanceWorld(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float dz = to.z - from.z;

            float lenSq = (dx * dx) + (dy * dy) + (dz * dz);
            if (lenSq <= 0.00001f)
                return new MoveVector(new Vector3 { x = 0, y = 0, z = 0 }, 0);

            float len = (float)Math.Sqrt(lenSq);

            return new MoveVector(
                new Vector3 { x = dx / len, y = dy / len, z = dz / len },
                len
            );
        }

        // ------------------------------------------------------------
        // Eco sniffing (BioTileCount)
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
                    int distSq = (dx * dx) + (dy * dy);

                    // prioritize smell, break ties with closeness
                    int score = (smell * 1000) - distSq;

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

        internal static void FindGlobalBestScreen(ScreenEcoMeta[,] ecoMap, out int bestY, out int bestX)
        {
            bestY = 0;
            bestX = 0;

            int bestSmell = -1;

            int maxY = ecoMap.GetLength(0);
            int maxX = ecoMap.GetLength(1);

            for (int y = 0; y < maxY; y++)
            {
                for (int x = 0; x < maxX; x++)
                {
                    int smell = ecoMap[y, x].BioTileCount;
                    if (smell > bestSmell)
                    {
                        bestSmell = smell;
                        bestY = y;
                        bestX = x;
                    }
                }
            }
        }
    }
}
