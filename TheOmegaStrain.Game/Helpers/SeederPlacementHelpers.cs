using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.SeederControls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Helpers
{
    public static class SeederPlacementHelpers
    {
        public static List<Vector3> CreateRingSeederPositions(
            int count,
            Vector3 center,
            int seed,
            int nearSeederCount = 4,
            float firstRingRadius = 8000f,
            float ringRadiusStep = 11500f,
            float radiusJitter = 1800f,
            float angleJitterDegrees = 12f,
            float? minSeederDistance = null)
        {
            var positions = new List<Vector3>(Math.Max(0, count));
            if (count <= 0)
                return positions;

            var random = new Random(seed);
            int nearCount = Math.Min(count, Math.Max(1, nearSeederCount));
            int farCount = count - nearCount;

            // Default a little over half a screen between seeders, derived from the ring layout.
            // firstRingRadius scales with the scene layout, so half of it is a good
            // proxy for the on-screen footprint of two adjacent seeders.
            float minDistanceUnscaled = minSeederDistance ?? (firstRingRadius * 0.6f);
            float scaledMinDistance = Math.Max(0f, minDistanceUnscaled) * SurfaceSetup.WorldScale;

            AddRing(positions, center, nearCount, firstRingRadius, random, radiusJitter, angleJitterDegrees, angleOffsetDegrees: -18f, scaledMinDistance: scaledMinDistance);

            const int farRingCapacity = 6;
            int placedFar = 0;
            int ringIndex = 1;
            while (placedFar < farCount)
            {
                int ringCount = Math.Min(farRingCapacity + ringIndex, farCount - placedFar);
                float radius = firstRingRadius + (ringRadiusStep * ringIndex);
                AddRing(positions, center, ringCount, radius, random, radiusJitter, angleJitterDegrees, angleOffsetDegrees: ringIndex * 23f, scaledMinDistance: scaledMinDistance);
                placedFar += ringCount;
                ringIndex++;
            }

            return positions;
        }

        public static List<Vector3> CreateRandomSeederPositions(
            int count,
            Vector3 center,
            int seed,
            float minRadius = 12000f,
            float maxRadius = 38000f,
            IReadOnlyCollection<Vector3>? avoidPositions = null,
            float minDistance = 3500f)
        {
            var positions = new List<Vector3>(Math.Max(0, count));
            if (count <= 0)
                return positions;

            var occupied = avoidPositions != null
                ? new List<Vector3>(avoidPositions)
                : new List<Vector3>();
            float scaledMinDistance = Math.Max(0f, minDistance) * SurfaceSetup.WorldScale;
            var random = new Random(seed);
            int attempts = 0;
            int maxAttempts = Math.Max(count * 80, 100);
            while (positions.Count < count && attempts < maxAttempts)
            {
                attempts++;
                double angle = random.NextDouble() * Math.PI * 2.0;
                float radius = minRadius + ((float)random.NextDouble() * (maxRadius - minRadius));
                var candidate = CreatePosition(center, radius * SurfaceSetup.WorldScale, angle);
                if (HasPositionWithinDistance(occupied, candidate, scaledMinDistance))
                    continue;

                positions.Add(candidate);
                occupied.Add(candidate);
            }

            int fallbackAttempt = 0;
            int fallbackSlotsPerRing = Math.Max(1, count);
            float fallbackRingStep = Math.Max(2500f, minDistance);
            while (positions.Count < count)
            {
                int fallbackRing = fallbackAttempt / fallbackSlotsPerRing;
                int fallbackSlot = fallbackAttempt % fallbackSlotsPerRing;
                float radius = (maxRadius + (fallbackRing * fallbackRingStep)) * SurfaceSetup.WorldScale;
                double angle = ((Math.PI * 2.0) / fallbackSlotsPerRing) * fallbackSlot + (seed * 0.017) + (fallbackRing * 0.37);
                var fallback = CreatePosition(center, radius, angle);
                fallbackAttempt++;
                if (HasPositionWithinDistance(occupied, fallback, scaledMinDistance))
                    continue;

                positions.Add(fallback);
                occupied.Add(fallback);
            }

            return positions;
        }

        public static List<Vector3> CreateCheckpointSeederPositions(
            int count,
            Vector3 center,
            int seed,
            float firstRingRadius = 7500f,
            float ringRadiusStep = 11500f,
            IReadOnlyCollection<Vector3>? avoidPositions = null,
            float minDistance = 4500f)
        {
            float minRadius = firstRingRadius + Math.Max(4500f, ringRadiusStep * 0.45f);
            float maxRadius = firstRingRadius + Math.Max(minRadius - firstRingRadius + 2500f, ringRadiusStep * 1.45f);

            return CreateRandomSeederPositions(
                count,
                center,
                seed,
                minRadius,
                maxRadius,
                avoidPositions,
                minDistance);
        }

        public static void AddSeederGroup(
            I3dWorld world,
            ISurface surface,
            Vector3 center,
            int totalSeederCount,
            int regularSeed,
            int nearSeederCount,
            float firstRingRadius = 7500f,
            float ringRadiusStep = 11500f,
            PowerUpType? firstKillPowerUpType = null,
            float? minSeederDistance = null)
        {
            int safeTotal = Math.Max(0, totalSeederCount);
            var positions = CreateRingSeederPositions(
                safeTotal,
                center,
                regularSeed,
                nearSeederCount,
                firstRingRadius,
                ringRadiusStep,
                minSeederDistance: minSeederDistance);

            foreach (var seederPosition in positions)
            {
                AddSeeder(world, surface, seederPosition, hasPowerUp: false);
            }

            // All campaign powerups are assigned at kill time. Scenes may type and
            // advance the first scheduled drop to kill one; later drops keep their
            // existing distributed thresholds.
            int powerUpCount = GetPowerUpCountForSeeders(safeTotal);
            PowerUpDropPolicy.ConfigureForWave(safeTotal, powerUpCount, firstKillPowerUpType);
        }

        /// <summary>
        /// Returns the number of powerups a wave of <paramref name="totalSeederCount"/>
        /// seeders should award. Brackets are calibrated against Scene4 (15 seeders -> 1),
        /// with one extra powerup per ~8 additional seeders.
        /// </summary>
        public static int GetPowerUpCountForSeeders(int totalSeederCount)
        {
            if (totalSeederCount <= 0) return 0;
            if (totalSeederCount <= 15) return 1;
            if (totalSeederCount <= 22) return 2;
            return 3;
        }

        private static void AddRing(
            List<Vector3> positions,
            Vector3 center,
            int count,
            float radius,
            Random random,
            float radiusJitter,
            float angleJitterDegrees,
            float angleOffsetDegrees,
            float scaledMinDistance = 0f)
        {
            if (count <= 0)
                return;

            double angleOffset = angleOffsetDegrees * Math.PI / 180.0;
            double angleJitter = angleJitterDegrees * Math.PI / 180.0;

            const int maxAttemptsPerPosition = 12;

            for (int i = 0; i < count; i++)
            {
                double baseAngle = angleOffset + (Math.PI * 2.0 * i / count);

                Vector3? chosen = null;

                for (int attempt = 0; attempt < maxAttemptsPerPosition; attempt++)
                {
                    double angle = baseAngle + ((random.NextDouble() * 2.0 - 1.0) * angleJitter);
                    float jitteredRadius = radius + ((float)random.NextDouble() * 2f - 1f) * radiusJitter;
                    var candidate = CreatePosition(center, Math.Max(1000f, jitteredRadius) * SurfaceSetup.WorldScale, angle);

                    if (!HasPositionWithinDistance(positions, candidate, scaledMinDistance))
                    {
                        chosen = candidate;
                        break;
                    }
                }

                if (chosen == null)
                    chosen = FindFallbackRingPosition(
                        positions,
                        center,
                        radius,
                        radiusJitter,
                        angleOffset,
                        i,
                        count,
                        scaledMinDistance);

                positions.Add(chosen);
            }
        }

        private static Vector3 FindFallbackRingPosition(
            List<Vector3> positions,
            Vector3 center,
            float radius,
            float radiusJitter,
            double angleOffset,
            int positionIndex,
            int ringCount,
            float scaledMinDistance)
        {
            float scaledRadius = Math.Max(1000f, radius + Math.Max(0f, radiusJitter)) * SurfaceSetup.WorldScale;
            float ringStep = Math.Max(scaledMinDistance, SurfaceSetup.tileSize * 2f);

            for (int fallbackRing = 0; fallbackRing < 96; fallbackRing++)
            {
                int slots = Math.Max(12, ringCount * 3 + fallbackRing);
                float fallbackRadius = scaledRadius + (fallbackRing * ringStep);
                double ringPhase = fallbackRing * 0.173;

                for (int slotOffset = 0; slotOffset < slots; slotOffset++)
                {
                    int slot = (positionIndex + slotOffset) % slots;
                    double angle = angleOffset + ringPhase + (Math.PI * 2.0 * slot / slots);
                    var candidate = CreatePosition(center, fallbackRadius, angle);
                    if (!HasPositionWithinDistance(positions, candidate, scaledMinDistance))
                        return candidate;
                }
            }

            double finalAngle = angleOffset + (positionIndex * 1.61803398875);
            float finalRadius = scaledRadius + (96 * ringStep);
            return CreatePosition(center, finalRadius, finalAngle);
        }

        private static Vector3 CreatePosition(Vector3 center, float radius, double angle)
        {
            return new Vector3
            {
                x = center.x + (MathF.Cos((float)angle) * radius),
                y = center.y,
                z = center.z + (MathF.Sin((float)angle) * radius)
            };
        }

        private static bool HasPositionWithinDistance(List<Vector3> positions, Vector3 candidate, float minDistance)
        {
            if (minDistance <= 0f)
                return false;

            float minDistanceSquared = minDistance * minDistance;
            foreach (var position in positions)
            {
                float dx = position.x - candidate.x;
                float dz = position.z - candidate.z;
                if ((dx * dx) + (dz * dz) < minDistanceSquared)
                    return true;
            }

            return false;
        }

        private static void AddSeeder(I3dWorld world, ISurface surface, Vector3 worldPosition, bool hasPowerUp)
        {
            var seeder = Seeder.CreateSeeder(surface);
            seeder.Rotation = new Vector3 { };
            seeder.WorldPosition = worldPosition;
            seeder.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 600 };
            seeder.ObjectName = "Seeder";
            seeder.Movement = new SeederControls();
            seeder.CrashBoxDebugMode = false;
            seeder.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.SeederHealth };
            seeder.HasPowerUp = hasPowerUp;
            seeder.PowerUpType = PowerUpType.Standard;
            world.WorldInhabitants.Add(seeder);
            GameState.SurfaceState.AiObjects.Add(seeder);
        }
    }

    /// <summary>
    /// Scene-scoped policy that decides which seeder kill (by 1-based kill index)
    /// should drop a powerup. Thresholds are spread evenly across the wave so
    /// drops arrive predictably instead of bunching up at the last surviving
    /// seeders.
    /// </summary>
    public static class PowerUpDropPolicy
    {
        private static readonly object _gate = new();
        private static int _totalSeeders;
        private static int _powerUpCount;
        private static List<int> _thresholds = new();
        private static int _seederKillsObserved;
        private static int _powerUpsAwarded;
        private static PowerUpType? _firstKillPowerUpType;
        private static bool _firstKillPowerUpResolved;

        public static IReadOnlyList<int> CurrentThresholds
        {
            get { lock (_gate) return _thresholds.ToArray(); }
        }

        public static int SeederKillsObserved
        {
            get { lock (_gate) return _seederKillsObserved; }
        }

        public static PowerUpType? FirstKillPowerUpType
        {
            get { lock (_gate) return _firstKillPowerUpType; }
        }

        /// <summary>
        /// Resets the policy for a new wave. Called by SeederPlacementHelpers.AddSeederGroup
        /// every time the scene is built (initial setup or after ResetActiveScene).
        /// </summary>
        public static void ConfigureForWave(
            int totalSeeders,
            int powerUpCount,
            PowerUpType? firstKillPowerUpType = null)
        {
            lock (_gate)
            {
                _totalSeeders = Math.Max(0, totalSeeders);
                _powerUpCount = Math.Max(0, Math.Min(powerUpCount, _totalSeeders));
                _thresholds = ComputeThresholds(_totalSeeders, _powerUpCount);
                _seederKillsObserved = 0;
                _powerUpsAwarded = 0;
                _firstKillPowerUpType = firstKillPowerUpType;
                _firstKillPowerUpResolved = false;
            }
        }

        /// <summary>
        /// Records a seeder kill and returns true when the new kill index matches a
        /// drop threshold. Callers should mark that seeder's HasPowerUp = true so the
        /// existing drop pipeline in LiveGameLoop creates the PowerUp.
        /// </summary>
        public static bool TryConsumeDrop(bool canAward = true)
        {
            return TryConsumeDrop(out _, canAward);
        }

        public static bool TryConsumeDrop(out PowerUpType powerUpType, bool canAward = true)
        {
            lock (_gate)
            {
                powerUpType = PowerUpType.Standard;
                _seederKillsObserved++;

                if (!_firstKillPowerUpResolved && _firstKillPowerUpType.HasValue)
                {
                    var configuredType = _firstKillPowerUpType.Value;
                    if (IsAlreadyOwned(configuredType))
                    {
                        _firstKillPowerUpResolved = true;
                        ConsumeScheduledDropSlot();
                    }
                    else if (canAward)
                    {
                        _firstKillPowerUpResolved = true;
                        ConsumeScheduledDropSlot();
                        powerUpType = configuredType;
                        return true;
                    }
                }

                if (_powerUpsAwarded >= _thresholds.Count)
                    return false;

                int nextThreshold = _thresholds[_powerUpsAwarded];
                if (_seederKillsObserved < nextThreshold)
                    return false;

                if (!canAward)
                    return false;

                _powerUpsAwarded++;
                return true;
            }
        }

        private static bool IsAlreadyOwned(PowerUpType powerUpType)
        {
            int ownedSpeedLevel = GameState.GamePlayState.SpeedPowerUpLevel;
            return powerUpType switch
            {
                PowerUpType.TravelSpeedLevel1 => ownedSpeedLevel >= 1,
                PowerUpType.TravelSpeedLevel2 => ownedSpeedLevel >= 2,
                _ => false
            };
        }

        private static void ConsumeScheduledDropSlot()
        {
            if (_powerUpsAwarded < _thresholds.Count)
                _powerUpsAwarded++;
        }

        internal static List<int> ComputeThresholds(int totalSeeders, int powerUpCount)
        {
            // Exposed via the public CurrentThresholds for game code; the pure helper
            // is also reachable from tests in the same assembly.
            return ComputeThresholdsCore(totalSeeders, powerUpCount);
        }

        /// <summary>
        /// Pure helper that returns the kill-index thresholds for a given wave size and
        /// powerup count. Exposed for unit tests in other assemblies.
        /// </summary>
        public static List<int> CalculateThresholds(int totalSeeders, int powerUpCount)
        {
            return ComputeThresholdsCore(totalSeeders, powerUpCount);
        }

        private static List<int> ComputeThresholdsCore(int totalSeeders, int powerUpCount)
        {
            var thresholds = new List<int>(Math.Max(0, powerUpCount));
            if (totalSeeders <= 0 || powerUpCount <= 0)
                return thresholds;

            // Spread drops evenly across the wave. For K drops in N seeders, place the
            // i-th drop (1-based) at ceil(N * i / (K + 1)). That guarantees the last
            // drop happens before the very last seeder is killed and that drops are
            // evenly distributed in between.
            int previous = 0;
            for (int i = 1; i <= powerUpCount; i++)
            {
                int threshold = (int)Math.Ceiling(totalSeeders * (double)i / (powerUpCount + 1));
                if (threshold <= previous) threshold = previous + 1;
                if (threshold > totalSeeders) threshold = totalSeeders;
                thresholds.Add(threshold);
                previous = threshold;
            }

            return thresholds;
        }
    }
}
