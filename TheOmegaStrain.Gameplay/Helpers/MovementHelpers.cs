using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Runtime.CompilerServices;

namespace TheOmegaStrain.Gameplay.Helpers
{
    public static class MovementHelpers
    {
        public readonly struct MoveVector
        {
            public readonly Vector3 Direction; // normalized, world space
            public readonly float Length;      // world distance

            public MoveVector(Vector3 direction, float length)
            {
                Direction = direction;
                Length = length;
            }
        }

        public readonly struct PursuitStep
        {
            public readonly Vector3 DirectionToTarget;
            public readonly Vector3 MovementDirection;
            public readonly float DistanceToTarget;
            public readonly float SpeedPerSecond;
            public readonly float MoveDistance;
            public readonly bool ShouldStartOvershoot;

            public bool HasMovement => MoveDistance > 0f;

            public PursuitStep(
                Vector3 directionToTarget,
                Vector3 movementDirection,
                float distanceToTarget,
                float speedPerSecond,
                float moveDistance,
                bool shouldStartOvershoot)
            {
                DirectionToTarget = directionToTarget;
                MovementDirection = movementDirection;
                DistanceToTarget = distanceToTarget;
                SpeedPerSecond = speedPerSecond;
                MoveDistance = moveDistance;
                ShouldStartOvershoot = shouldStartOvershoot;
            }
        }

        // ------------------------------------------------------------
        // Pursuit movement: reusable workshop pattern for enemies.
        // ------------------------------------------------------------

        public static float GetScreenCrossingSpeed(float secondsPerScreen)
        {
            return secondsPerScreen <= 0f
                ? 0f
                : ScreenSetup.screenSizeX / secondsPerScreen;
        }

        public static Vector3 GetVelocityTowardsTarget(Vector3 from, Vector3 target, float speedPerSecond)
        {
            if (speedPerSecond <= 0f)
            {
                return new Vector3();
            }

            var moveVector = GetDirectionAndDistanceWorld(from, target);
            return new Vector3
            {
                x = moveVector.Direction.x * speedPerSecond,
                y = moveVector.Direction.y * speedPerSecond,
                z = moveVector.Direction.z * speedPerSecond
            };
        }

        public static Vector3 GetVectorToTarget(IVector3 from, IVector3 target)
        {
            return new Vector3
            {
                x = target.x - from.x,
                y = target.y - from.y,
                z = target.z - from.z
            };
        }

        public static bool ShouldRefreshPursuitVelocity(
            DateTime lastDirectionUpdate,
            DateTime now,
            float updateIntervalSeconds,
            Vector3 currentVelocity,
            Vector3 directionToTarget,
            bool isOvershooting)
        {
            if (isOvershooting)
            {
                return false;
            }

            return lastDirectionUpdate == DateTime.MinValue ||
                (now - lastDirectionUpdate).TotalSeconds >= updateIntervalSeconds ||
                Dot(currentVelocity, directionToTarget) <= 0f;
        }

        public static PursuitStep GetPursuitStep(
            IVector3 currentPosition,
            IVector3 targetPosition,
            IVector3 currentVelocity,
            float deltaSeconds,
            IVector3? forcedMovementDirection = null)
        {
            var targetVector = GetDirectionAndDistanceWorld(currentPosition, targetPosition);
            var speedPerSecond = GetLength(currentVelocity);

            if (deltaSeconds <= 0f || speedPerSecond <= 0f)
            {
                return new PursuitStep(
                    targetVector.Direction,
                    new Vector3(),
                    targetVector.Length,
                    speedPerSecond,
                    0f,
                    false);
            }

            var moveDirection = forcedMovementDirection is Vector3 forcedDirection
                ? Normalize(forcedDirection)
                : targetVector.Direction;

            if (GetLength(moveDirection) <= 0.00001f ||
                (targetVector.Length <= 0f && forcedMovementDirection == null))
            {
                return new PursuitStep(
                    targetVector.Direction,
                    new Vector3(),
                    targetVector.Length,
                    speedPerSecond,
                    0f,
                    false);
            }

            var moveDistance = speedPerSecond * deltaSeconds;
            var shouldStartOvershoot = forcedMovementDirection == null &&
                targetVector.Length > 0f &&
                moveDistance >= targetVector.Length;

            return new PursuitStep(
                targetVector.Direction,
                moveDirection,
                targetVector.Length,
                speedPerSecond,
                moveDistance,
                shouldStartOvershoot);
        }

        public static Vector3 MoveAlongDirection(Vector3 currentPosition, Vector3 movementDirection, float moveDistance)
        {
            return new Vector3
            {
                x = currentPosition.x + (movementDirection.x * moveDistance),
                y = currentPosition.y + (movementDirection.y * moveDistance),
                z = currentPosition.z + (movementDirection.z * moveDistance)
            };
        }

        public static Vector3 MoveAlongDirection(IVector3 currentPosition, Vector3 movementDirection, float moveDistance)
        {
            return MoveAlongDirection(
                new Vector3 { x = currentPosition.x, y = currentPosition.y, z = currentPosition.z },
                movementDirection,
                moveDistance);
        }

        public static (float X, float Y, float Z) GetHeadingFromMovementDirection(Vector3 movementDirection)
        {
            return OmegaObjectHelpers.GetHeadingFromDirection(movementDirection.x, movementDirection.z);
        }

        public static (float X, float Y, float Z) MoveRotationTowards(
            float currentX,
            float currentY,
            float currentZ,
            float targetX,
            float targetY,
            float targetZ,
            float degreesPerSecond,
            double deltaSeconds)
        {
            var maxDelta = degreesPerSecond * (float)deltaSeconds;
            if (maxDelta <= 0f)
            {
                return (currentX, currentY, currentZ);
            }

            return (
                OmegaObjectHelpers.MoveAngleTowards(currentX, targetX, maxDelta),
                OmegaObjectHelpers.MoveAngleTowards(currentY, targetY, maxDelta),
                OmegaObjectHelpers.MoveAngleTowards(currentZ, targetZ, maxDelta));
        }

        public static Vector3 Normalize(IVector3 vector)
        {
            var length = GetLength(vector);
            if (length <= 0.00001f)
            {
                return new Vector3();
            }

            return new Vector3
            {
                x = vector.x / length,
                y = vector.y / length,
                z = vector.z / length
            };
        }

        public static float GetLength(IVector3 vector)
        {
            return MathF.Sqrt((vector.x * vector.x) + (vector.y * vector.y) + (vector.z * vector.z));
        }

        public static float Dot(IVector3 a, IVector3 b)
        {
            return (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
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
        public static MoveVector GetDirectionAndDistanceWorld(IVector3 from, IVector3 to)
        {
            var vectorToTarget = GetVectorToTarget(from, to);

            float dx = vectorToTarget.x;
            float dy = vectorToTarget.y;
            float dz = vectorToTarget.z;

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
