using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using RetroMesh.Engine;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleParticleCollision(OmegaObject3D a, OmegaObject3D b)
        {
            var particle = a.ObjectName == "Particle" ? a : b;
            var other = particle == a ? b : a;

            if (!CollisionBoxScanner.TryFindFirstParticleCollision(
                    particle,
                    other,
                    particle.ImpactStatus.SourceParticle?.Physics?.Velocity,
                    GetWorldBoxPointsCached,
                    out ParticleCollisionScanResult<OmegaObject3D, Vector3> scanResult))
            {
                return false;
            }

            var collision = scanResult.Collision;
            var center = CreateVector(collision.ParticleCenter.x, collision.ParticleCenter.y, collision.ParticleCenter.z);
            var min = CreateVector(collision.TargetMin.x, collision.TargetMin.y, collision.TargetMin.z);
            var max = CreateVector(collision.TargetMax.x, collision.TargetMax.y, collision.TargetMax.z);
            var direction = collision.TargetDirection;
            var particleDirection = collision.ParticleDirection;
            int pb = scanResult.ParticleBoxIndex;
            int ob = scanResult.TargetBoxIndex;

            ImpactStateMarker.MarkImpact(
                particle.ImpactStatus,
                particle.ImpactStatus.ObjectName,
                particleDirection);

            if (particle.ImpactStatus.SourceParticle?.ImpactStatus != null)
            {
                // Tell the source what it hit (the other object's name)
                ImpactStateMarker.MarkImpact(
                    particle.ImpactStatus.SourceParticle.ImpactStatus,
                    other.ObjectName,
                    particleDirection);
            }

            if (other.ImpactStatus != null)
            {
                // Tell the other object what hit it (the weapon name stored on the particle)
                ImpactStateMarker.MarkObjectImpact(
                    other,
                    particle.ImpactStatus?.ObjectName ?? particle.ObjectName,
                    direction,
                    ob,
                    GetImpactState);
            }

            if (!SkipParticleLogging)
            {
                LogCollision(a, b,
                    $"[FRAME:{numFrame}] [PARTICLE COLLISION] {particle.ObjectName} <-> {other.ObjectName} | Dir:{direction} ParticleDir:{particleDirection} | ParticleBox:{pb} OtherBox:{ob}");
            }

            if (LogCollisionDetails)
            {
                if (!SkipParticleLogging) LogCollisionDetail(a, b,
                    $"[PARTICLE CENTER] ({center.x:0.##},{center.y:0.##},{center.z:0.##})");

                if (!SkipParticleLogging) LogCollisionDetail(a, b,
                    $"[OTHER AABB] Min=({min.x:0.##},{min.y:0.##},{min.z:0.##}) Max=({max.x:0.##},{max.y:0.##},{max.z:0.##})");

                if (!SkipParticleLogging)
                    LogCrashBoxWorldPoints($"[PARTICLE BOX WORLD] {particle.ObjectName} Box[{pb}]", scanResult.ParticleWorldBox);

                if (!SkipParticleLogging)
                    LogCrashBoxWorldPoints($"[OTHER BOX WORLD] {other.ObjectName} Box[{ob}]", scanResult.TargetWorldBox);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleGeneralCollision(OmegaObject3D a, OmegaObject3D b)
        {
            return CollisionBoxScanner.ScanBoxCollisions(
                a,
                b,
                GetCollisionMargins(),
                GetWorldBoxPointsCached,
                HandleGeneralBoxCollision,
                LogGeneralBoxPair,
                LogGeneralOverlapCheck);

            void LogGeneralBoxPair(in CollisionBoxPairContext<OmegaObject3D, Vector3> context)
            {
                if (ShouldLogAny && !LogOnlyCollisions && CheckLogFilter(a, b) && LogCollisionDetails)
                {
                    Logger.Log($"PTS {a.ObjectName}[{context.BoxIndexA}] vs {b.ObjectName}[{context.BoxIndexB}] A:{string.Join(";", context.WorldBoxA.Select(p => $"({p.x:0.#},{p.y:0.#},{p.z:0.#})"))} | B:{string.Join(";", context.WorldBoxB.Select(p => $"({p.x:0.#},{p.y:0.#},{p.z:0.#})"))}");
                }
            }

            void LogGeneralOverlapCheck(in CollisionBoxOverlapContext<OmegaObject3D, Vector3> context)
            {
                LogCollisionBoxCheck(context.OverlapCheck, a.ObjectName, b.ObjectName);
            }

            bool HandleGeneralBoxCollision(in CollisionBoxScanResult<OmegaObject3D, Vector3> scanResult)
            {
                int ai = scanResult.BoxIndexA;
                int bi = scanResult.BoxIndexB;
                var collision = scanResult.Collision;
                var centerA = CreateVector(collision.CenterA.x, collision.CenterA.y, collision.CenterA.z);
                var centerB = CreateVector(collision.CenterB.x, collision.CenterB.y, collision.CenterB.z);
                var centerDistance = collision.CenterDistance;
                bool isKamikazeShipPair =
                    (a.ObjectName == "KamikazeDrone" && b.ObjectName == "Ship") ||
                    (a.ObjectName == "Ship" && b.ObjectName == "KamikazeDrone");

                if (isKamikazeShipPair && centerDistance > GameSetup.MaxKamikazeShipCenterCollisionDistance)
                {
                    if (LogSkippedCollisions)
                    {
                        LogCollisionDetail(a, b,
                            $"[COLLISION SKIPPED] {a.ObjectName} <-> {b.ObjectName} | CenterDistance:{centerDistance:0.##} | Max:{GameSetup.MaxKamikazeShipCenterCollisionDistance:0.##}");
                    }

                    return false;
                }

                if (TryMarkTerrainAvoidanceContact(a, b, ai, bi, centerA, centerB, centerDistance))
                {
                    return true;
                }

                ImpactStateMarker.MarkCollisionPair(
                    a,
                    b,
                    collision,
                    GetImpactState,
                    IsCombatCollisionName);

                LogCollision(a, b,
                    $"[FRAME:{numFrame}] [GENERAL COLLISION] {a.ObjectName} <-> {b.ObjectName} | ABox:{ai} BBox:{bi} | CenterDistance:{centerDistance:0.##}");

                if (LogCollisionDetails)
                {
                    var offsetA = GetOffsetCached(a);
                    var offsetB = GetOffsetCached(b);

                    LogCollisionDetail(a, b,
                        $"[COLLISION OFFSETS] AEffective=({offsetA.x:0.##},{offsetA.y:0.##},{offsetA.z:0.##}) " +
                        $"BEffective=({offsetB.x:0.##},{offsetB.y:0.##},{offsetB.z:0.##})");

                    LogCollisionDetail(a, b,
                        $"[COLLISION CENTERS] A=({centerA.x:0.##},{centerA.y:0.##},{centerA.z:0.##}) " +
                        $"B=({centerB.x:0.##},{centerB.y:0.##},{centerB.z:0.##})");

                    LogCollisionDetail(a, b,
                        $"[COLLISION DISTANCE] CenterToCenter={centerDistance:0.##}");

                    LogCollisionDetail(a, b,
                        $"[COLLISION DIR] {a.ObjectName}->{b.ObjectName}:{a.ImpactStatus.ImpactDirection} | " +
                        $"{b.ObjectName}->{a.ObjectName}:{b.ImpactStatus.ImpactDirection}");

                    LogCrashBoxWorldPoints($"[A BOX WORLD] {a.ObjectName} Box[{ai}]", scanResult.WorldBoxA);
                    LogCrashBoxWorldPoints($"[B BOX WORLD] {b.ObjectName} Box[{bi}]", scanResult.WorldBoxB);
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryMarkTerrainAvoidanceContact(
            OmegaObject3D a,
            OmegaObject3D b,
            int boxIndexA,
            int boxIndexB,
            Vector3 centerA,
            Vector3 centerB,
            float centerDistance)
        {
            var flagsA = GetTypeFlagsCached(a);
            var flagsB = GetTypeFlagsCached(b);

            bool aIsAvoidingAi = IsTerrainAvoidanceAiObject(a, flagsA);
            bool bIsAvoidingAi = IsTerrainAvoidanceAiObject(b, flagsB);
            bool aIsObstacle = IsTerrainObstacle(flagsA);
            bool bIsObstacle = IsTerrainObstacle(flagsB);

            if (!(aIsAvoidingAi && bIsObstacle) && !(bIsAvoidingAi && aIsObstacle))
                return false;

            var ai = aIsAvoidingAi ? a : b;
            var obstacle = aIsAvoidingAi ? b : a;
            var aiCenter = aIsAvoidingAi ? centerA : centerB;
            var obstacleCenter = aIsAvoidingAi ? centerB : centerA;
            int aiBoxIndex = aIsAvoidingAi ? boxIndexA : boxIndexB;

            if (ai.ImpactStatus != null &&
                (ai.ImpactStatus.HasCrashed != true || !IsCombatCollisionName(ai.ImpactStatus.ObjectName)))
            {
                ImpactStateMarker.MarkObjectImpact(
                    ai,
                    obstacle.ObjectName,
                    CollisionDirectionMath.EstimateDirection(aiCenter, obstacleCenter),
                    aiBoxIndex,
                    GetImpactState,
                    IsCombatCollisionName);
            }

            LogCollision(ai, obstacle,
                $"[FRAME:{numFrame}] [AI TERRAIN CONTACT] {ai.ObjectName} -> {obstacle.ObjectName} | CenterDistance:{centerDistance:0.##}");

            return true;
        }

        private static void HandleDecoyBlastDamage(List<OmegaObject3D> activeWorld)
        {
            float blastRadius = TheOmegaStrain.Common.CommonSetup.GameSetup.DecoyBlastRadius;

            RadialObjectScanner.Scan(
                activeWorld,
                blastRadius,
                IsDecoyBlastSource,
                GetObjectWorldPosition,
                IsDecoyBlastTarget,
                GetObjectWorldPosition,
                HandleDecoyBlastHit);

            bool IsDecoyBlastSource(OmegaObject3D candidate)
            {
                if (candidate.ObjectName != "DroneDecoy") return false;
                if (candidate.ImpactStatus?.HasExploded == true) return false;
                if (candidate.CrashBoxes != null && candidate.CrashBoxes.Count > 0) return false;
                if (candidate.ObjectParts == null || candidate.ObjectParts.Count == 0) return false;

                return _processedDecoyBlasts.Add(candidate.ObjectId);
            }

            bool IsDecoyBlastTarget(OmegaObject3D source, OmegaObject3D target)
            {
                if (target.CrashBoxes == null || target.CrashBoxes.Count == 0) return false;
                if (target.ImpactStatus?.HasExploded == true) return false;
                if (target.ImpactStatus?.HasCrashed == true) return false;

                var flags = GetTypeFlagsCached(target);
                return !flags.IsShip &&
                       !flags.IsSurface &&
                       !flags.IsParticle &&
                       !flags.IsLazer &&
                       !flags.IsStatic;
            }

            void HandleDecoyBlastHit(in RadialHitContext<OmegaObject3D, Vector3> context)
            {
                if (context.Target.ImpactStatus != null)
                {
                    ImpactStateMarker.MarkImpact(
                        context.Target.ImpactStatus,
                        "DroneDecoy",
                        direction: null);
                }

                LogCollision(context.Source, context.Target,
                    $"[FRAME:{numFrame}] [DECOY BLAST] {context.Source.ObjectName} -> {context.Target.ObjectName} | Distance:{context.Distance:0.##} | BlastRadius:{context.Radius:0.##}");
            }
        }

        private static void HandleBomberBombBlastDamage(List<OmegaObject3D> activeWorld)
        {
            float blastRadius = TheOmegaStrain.Common.CommonSetup.GameSetup.BomberBombBlastRadius;
            float blastDamage = TheOmegaStrain.Common.CommonSetup.GameSetup.BomberBombBlastDamage;

            RadialObjectScanner.Scan(
                activeWorld,
                blastRadius,
                IsBombBlastSource,
                GetObjectWorldPosition,
                IsBombBlastTarget,
                GetShipCrashCenterWorldPosition,
                HandleBombBlastHit);

            bool IsBombBlastSource(OmegaObject3D bomb)
            {
                if (bomb.ObjectName != "BomberBomb") return false;
                if (bomb.ImpactStatus?.HasExploded == true) return false;
                // Bomb is exploding when its crashboxes have been cleared
                if (bomb.CrashBoxes != null && bomb.CrashBoxes.Count > 0) return false;
                if (bomb.ObjectParts == null || bomb.ObjectParts.Count == 0) return false;

                return _processedBombBlasts.Add(bomb.ObjectId);
            }

            static bool IsBombBlastTarget(OmegaObject3D source, OmegaObject3D target)
            {
                return target.ObjectName == "Ship";
            }

            void HandleBombBlastHit(in RadialHitContext<OmegaObject3D, Vector3> context)
            {
                TheOmegaStrain.Common.CommonGlobalState.GameState.GamePlayState?.ApplyDamage(blastDamage);

                LogCollision(context.Source, context.Target,
                    $"[FRAME:{numFrame}] [BOMB BLAST] {context.Source.ObjectName} -> {context.Target.ObjectName} | Distance:{context.Distance:0.##} | BlastRadius:{context.Radius:0.##} | Damage:{blastDamage:0.##}");
            }
        }

        private static Vector3? GetObjectWorldPosition(OmegaObject3D obj)
        {
            return obj.WorldPosition as Vector3;
        }

        private static Vector3? GetShipCrashCenterWorldPosition(OmegaObject3D obj)
        {
            var shipPosRaw = TheOmegaStrain.Common.CommonGlobalState.GameState.ShipState?.ShipCrashCenterWorldPosition;
            return shipPosRaw == null
                ? null
                : new Vector3 { x = shipPosRaw.x, y = shipPosRaw.y, z = shipPosRaw.z };
        }

        private static IImpactState? GetImpactState(OmegaObject3D obj)
        {
            return obj.ImpactStatus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetCenterOfBox(List<Vector3> points)
        {
            var center = GeometryMath.GetCenterOfBox(points);
            return new Vector3(center.x, center.y, center.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CollisionMargins GetCollisionMargins()
        {
            return new CollisionMargins(
                -GameSetup.CollisionMarginX,
                GameSetup.CollisionMarginY,
                GameSetup.CollisionMarginZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogCollisionBoxCheck(
            CollisionBoxOverlapCheck check,
            string? nameA = null,
            string? nameB = null)
        {
            if (Logger.ShouldLog(LocalEnableLogging) && nameA != null && nameB != null)
            {
                Logger.Log(
                    $"AABBCHK {nameA} vs {nameB} | " +
                    $"X:{check.XOverlaps} " +
                    $"Y:{check.YOverlaps} " +
                    $"Z:{check.ZOverlaps} | " +
                    $"A[min=({check.BoundsA.MinX:0.#},{check.BoundsA.MinY:0.#},{check.BoundsA.MinZ:0.#}) max=({check.BoundsA.MaxX:0.#},{check.BoundsA.MaxY:0.#},{check.BoundsA.MaxZ:0.#})] " +
                    $"B[min=({check.BoundsB.MinX:0.#},{check.BoundsB.MinY:0.#},{check.BoundsB.MinZ:0.#}) max=({check.BoundsB.MaxX:0.#},{check.BoundsB.MaxY:0.#},{check.BoundsB.MaxZ:0.#})]");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCombatCollisionName(string objectName) =>
            objectName == "Ship" ||
            EnemySetup.IsEnemyTypeValid(objectName) ||
            WeaponSetup.IsWeaponTypeValid(objectName);
    }
}
