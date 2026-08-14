using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Helpers;
using RetroMesh.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        public static DateTime staticOjectLastCheck { get; set; } = new DateTime();
        private static DateTime _lastStaticCheck = DateTime.MinValue;
        private static bool _skipParticles = false;
        private static bool _shouldCheckStaticObjectsThisFrame;
        private static bool _isPausedThisFrame;

        public static void HandleCrashboxes(List<_3dObject> activeWorld, bool isPaused)
        {
            numFrame++;
            ResetFrameCachesIfNeeded();

            _shouldCheckStaticObjectsThisFrame = (DateTime.Now - _lastStaticCheck).TotalMilliseconds > 100;
            _isPausedThisFrame = isPaused;
            _skipParticles = !_skipParticles;

            GameState.ShipState.BestCandidateStates.Clear();

            PairScanner.Scan(
                activeWorld,
                CreateTypeFlags,
                HandleCollisionPair,
                ShouldSkipCollisionPair,
                CanParticipateInCollision);

            HandleDecoyBlastDamage(activeWorld);
            HandleBomberBombBlastDamage(activeWorld);

            if (ShouldLogAny && !LogOnlyCollisions)
            {
                Logger.Log($"[CACHE] Hits: {TotalCacheHits}, Misses: {TotalCacheMisses}, Efficiency: {(TotalCacheHits + TotalCacheMisses == 0 ? 0 : (int)(100.0 * TotalCacheHits / (TotalCacheHits + TotalCacheMisses)))}%");
                Logger.Log($"[DISTANCE SKIP] Skipped {SkippedByDistance} pairs due to distance > {MaxCrashDistance}");
            }
        }

        private static bool CanParticipateInCollision(_3dObject obj)
        {
            return obj.CrashBoxes != null &&
                   obj.CrashBoxes.Count != 0 &&
                   obj.ImpactStatus?.HasExploded != true;
        }

        private static bool ShouldSkipCollisionPair(
            in CollisionPairContext<_3dObject, ObjectTypeFlags> context)
        {
            var flagsA = context.ClassificationA;
            var flagsB = context.ClassificationB;
            var a = context.A;
            var b = context.B;

            if ((flagsA.IsShip || flagsB.IsShip) &&
                GameState.ShipState.ShipCrashDetectionDisabledUntilUtc > DateTime.UtcNow)
            {
                return true;
            }

            bool isInhabitantStatic = flagsA.IsStatic;
            bool isOtherStatic = flagsB.IsStatic;
            bool isInhabitantParticle = flagsA.IsParticle;
            bool isOtherParticle = flagsB.IsParticle;
            bool isInhabitantLazer = flagsA.IsLazer;
            bool isOtherLazer = flagsB.IsLazer;
            bool isInhabitantSeeder = flagsA.IsSeeder;
            bool isOtherSeeder = flagsB.IsSeeder;
            bool isSeeder = isInhabitantSeeder || isOtherSeeder;
            bool isParticle = isInhabitantParticle || isOtherParticle;
            bool isLazer = isInhabitantLazer || isOtherLazer;
            bool isWeaponShipPair =
                (flagsA.IsWeapon && flagsB.IsShip) ||
                (flagsB.IsWeapon && flagsA.IsShip);
            bool isEnemyLazerEnemyPair =
                ((flagsA.Name == "EnemyLazer" || flagsA.Name == "EnemyLazerMedium") && flagsB.IsEnemy) ||
                ((flagsB.Name == "EnemyLazer" || flagsB.Name == "EnemyLazerMedium") && flagsA.IsEnemy);
            bool isEnemyLazerSurfacePair =
                ((flagsA.Name == "EnemyLazer" || flagsA.Name == "EnemyLazerMedium") && flagsB.IsSurface) ||
                ((flagsB.Name == "EnemyLazer" || flagsB.Name == "EnemyLazerMedium") && flagsA.IsSurface);
            bool isBothParticles = isInhabitantParticle && isOtherParticle;
            bool isShip = flagsA.IsShip || flagsB.IsShip;
            bool isSurface = flagsA.IsSurface || flagsB.IsSurface;
            bool isShipSurfacePair = isShip && isSurface;
            bool isDecoySurfacePair =
                (flagsA.Name == "DroneDecoy" && flagsB.IsSurface) ||
                (flagsB.Name == "DroneDecoy" && flagsA.IsSurface);
            bool isDecoyShipPair =
                (flagsA.Name == "DroneDecoy" && flagsB.IsShip) ||
                (flagsB.Name == "DroneDecoy" && flagsA.IsShip);
            bool isDecoyParticlePair =
                (flagsA.Name == "DroneDecoy" && flagsB.IsParticle) ||
                (flagsB.Name == "DroneDecoy" && flagsA.IsParticle);
            bool isPowerUp = flagsA.Name == "PowerUp" || flagsB.Name == "PowerUp";
            bool isPowerUpShipPair =
                (flagsA.Name == "PowerUp" && flagsB.IsShip) ||
                (flagsB.Name == "PowerUp" && flagsA.IsShip);
            bool isEnemySurfacePair = (flagsA.IsEnemy && flagsB.IsSurface) || (flagsB.IsEnemy && flagsA.IsSurface);
            bool isBomberBombSurfacePair =
                (flagsA.Name == "BomberBomb" && flagsB.IsSurface) ||
                (flagsB.Name == "BomberBomb" && flagsA.IsSurface);
            bool isTerrainAvoidanceAiObstaclePair =
                (IsTerrainAvoidanceAiObject(a, flagsA) && IsTerrainObstacle(flagsB)) ||
                (IsTerrainAvoidanceAiObject(b, flagsB) && IsTerrainObstacle(flagsA));
            bool isBothEnemies = flagsA.IsEnemy && flagsB.IsEnemy;

            if (string.IsNullOrEmpty(flagsA.Name) || string.IsNullOrEmpty(flagsB.Name)) return true;
            if (flagsA.Name == flagsB.Name) return true;
            if (isInhabitantStatic && isOtherStatic) return true;
            if ((isInhabitantStatic || isOtherStatic) &&
                !_shouldCheckStaticObjectsThisFrame &&
                !isTerrainAvoidanceAiObstaclePair &&
                !isShipSurfacePair) return true;
            if (isParticle && isShip) return true;
            if (isParticle && (flagsA.IsEnemy || flagsB.IsEnemy)) return true;
            if (isBothParticles) return true;
            if (isParticle && _skipParticles) return true;
            if (isEnemySurfacePair && !isBomberBombSurfacePair && !isTerrainAvoidanceAiObstaclePair) return true;
            if (isBothEnemies) return true;
            if (isDecoySurfacePair) return true;
            if (isDecoyShipPair) return true;
            if (isDecoyParticlePair) return true;
            if (isPowerUp && !isPowerUpShipPair) return true;
            if (isWeaponShipPair) return true;
            if (isEnemyLazerEnemyPair) return true;
            if (isEnemyLazerSurfacePair) return true;
            if (isLazer && isParticle || isSeeder && isParticle) return true;

            return false;
        }

        private static void HandleCollisionPair(
            in CollisionPairContext<_3dObject, ObjectTypeFlags> context)
        {
            var inhabitant = context.A;
            var otherInhabitant = context.B;
            var flagsA = context.ClassificationA;
            var flagsB = context.ClassificationB;

            bool isInhabitantStatic = flagsA.IsStatic;
            bool isOtherStatic = flagsB.IsStatic;
            bool isInhabitantParticle = flagsA.IsParticle;
            bool isOtherParticle = flagsB.IsParticle;
            bool isInhabitantLazer = flagsA.IsLazer;
            bool isOtherLazer = flagsB.IsLazer;
            bool isParticle = isInhabitantParticle || isOtherParticle;
            bool isLazer = isInhabitantLazer || isOtherLazer;
            bool isShip = flagsA.IsShip || flagsB.IsShip;
            bool isSurface = flagsA.IsSurface || flagsB.IsSurface;
            bool isBomberBombSurfacePair =
                (flagsA.Name == "BomberBomb" && flagsB.IsSurface) ||
                (flagsB.Name == "BomberBomb" && flagsA.IsSurface);
            bool isTerrainAvoidanceAiObstaclePair =
                (IsTerrainAvoidanceAiObject(inhabitant, flagsA) && IsTerrainObstacle(flagsB)) ||
                (IsTerrainAvoidanceAiObject(otherInhabitant, flagsB) && IsTerrainObstacle(flagsA));

            if ((isInhabitantStatic || isOtherStatic) && _shouldCheckStaticObjectsThisFrame)
                _lastStaticCheck = DateTime.Now;

            if (_isPausedThisFrame && !LogOnlyCollisions && CheckLogFilter(inhabitant, otherInhabitant))
            {
                LogSnapShots(inhabitant, otherInhabitant);
            }

            var centerA = GetCenterCached(inhabitant);
            var centerB = GetCenterCached(otherInhabitant);

            double distance = GeometryMath.GetDistance(centerA, centerB);

            if (CommonUtilities.CommonSetup.EnemySetup.IsEnemyTypeValid(inhabitant.ObjectName) && distance < MaxCrashDistance * 2)
            {
                CommonUtilities.CommonGlobalState.GameState.ShipState.BestCandidateStates.Add(new BestCandidateState
                {
                    BestEnemyCandidate = new EnemyCandidateInfo
                    {
                        EnemyObject = inhabitant,
                        EnemyCenterPosition = centerA
                    },
                    TimeStampUtc = DateTime.UtcNow
                });
            }

            LogNonCollision(inhabitant, otherInhabitant,
                $"[DISTANCE CHECK] [FRAME:{numFrame}] {inhabitant.ObjectName} vs {otherInhabitant.ObjectName} = {distance:F2}");

            if (isTerrainAvoidanceAiObstaclePair &&
                TryStartTerrainProximityRecovery(inhabitant, otherInhabitant, flagsA, flagsB, centerA, centerB, (float)distance))
            {
                return;
            }

            if (isLazer || isOtherLazer)
            {
                if (!LogOnlyCollisions && ShouldLogPair(inhabitant, otherInhabitant))
                {
                    var inhabitantCrashText = string.Join(" | ",
                        inhabitant.CrashBoxes.Select((box, idx) =>
                            $"Box{idx}: " + string.Join(", ", box.Select(v => $"({v.x:F2},{v.y:F2},{v.z:F2})"))));

                    var otherCrashText = string.Join(" | ",
                        otherInhabitant.CrashBoxes.Select((box, idx) =>
                            $"Box{idx}: " + string.Join(", ", box.Select(v => $"({v.x:F2},{v.y:F2},{v.z:F2})"))));

                    Logger.Log($"[CHECKLAZER] {inhabitant.ObjectName} CrashBox: {inhabitantCrashText} and {otherInhabitant.ObjectName} LocalCrash: {otherCrashText}");
                }
            }

            var effectiveMaxCrashDistance = isLazer ? MaxCrashDistance * 2 : MaxCrashDistance;
            bool isShipEnemyPair = isShip && !isSurface && !isParticle && !isLazer;
            if (!isShipEnemyPair && !isBomberBombSurfacePair && !isTerrainAvoidanceAiObstaclePair && distance > effectiveMaxCrashDistance)
            {
                SkippedByDistance++;
                return;
            }

            if (isParticle)
            {
                HandleParticleCollision(inhabitant, otherInhabitant);
            }
            else
            {
                HandleGeneralCollision(inhabitant, otherInhabitant);
            }
        }

        private static bool TryStartTerrainProximityRecovery(
            _3dObject a,
            _3dObject b,
            ObjectTypeFlags flagsA,
            ObjectTypeFlags flagsB,
            Vector3 centerA,
            Vector3 centerB,
            float centerDistance)
        {
            bool aIsAvoidingAi = IsTerrainAvoidanceAiObject(a, flagsA);
            bool bIsAvoidingAi = IsTerrainAvoidanceAiObject(b, flagsB);
            bool aIsProactiveObstacle = CommonUtilities.CommonSetup.TerrainAvoidanceSetup.IsProactiveTerrainObstacle(flagsA.Name);
            bool bIsProactiveObstacle = CommonUtilities.CommonSetup.TerrainAvoidanceSetup.IsProactiveTerrainObstacle(flagsB.Name);

            _3dObject? ai = null;
            _3dObject? obstacle = null;
            Vector3 aiCenter = new();
            Vector3 obstacleCenter = new();

            if (aIsAvoidingAi && bIsProactiveObstacle)
            {
                ai = a;
                obstacle = b;
                aiCenter = centerA;
                obstacleCenter = centerB;
            }
            else if (bIsAvoidingAi && aIsProactiveObstacle)
            {
                ai = b;
                obstacle = a;
                aiCenter = centerB;
                obstacleCenter = centerA;
            }

            if (ai == null || obstacle == null)
                return false;

            float proactiveAvoidanceDistance = CommonUtilities.CommonSetup.TerrainAvoidanceSetup.GetProactiveAvoidanceDistance(ai.ObjectName);
            if (centerDistance > proactiveAvoidanceDistance)
                return false;

            bool started = TerrainAvoidanceHelpers.TryStartTerrainProximityRecovery(
                ai,
                obstacle.ObjectName,
                aiCenter,
                obstacleCenter);

            if (started)
            {
                LogCollision(ai, obstacle,
                    $"[FRAME:{numFrame}] [AI TERRAIN PROXIMITY] {ai.ObjectName} -> {obstacle.ObjectName} | CenterDistance:{centerDistance:0.##}");
            }

            return started;
        }
    }
}
