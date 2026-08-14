using CommonUtilities.CommonSetup;
using Domain;
using RetroMesh.Engine;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleParticleCollision(_3dObject a, _3dObject b)
        {
            var particle = a.ObjectName == "Particle" ? a : b;
            var other = particle == a ? b : a;

            for (int pb = 0; pb < particle.CrashBoxes.Count; pb++)
            {
                var particleBox = particle.CrashBoxes[pb];

                var worldParticlePoints = GetWorldBoxPointsCached(particle, pb, particleBox);
                if (worldParticlePoints.Count == 0) continue;

                for (int ob = 0; ob < other.CrashBoxes.Count; ob++)
                {
                    var otherBox = other.CrashBoxes[ob];

                    var worldOtherPoints = GetWorldBoxPointsCached(other, ob, otherBox);
                    if (worldOtherPoints.Count == 0) continue;

                    if (CollisionPairMath.TryCreateParticleCollision(
                        worldParticlePoints,
                        worldOtherPoints,
                        particle.ImpactStatus.SourceParticle?.Physics?.Velocity,
                        out var collision))
                    {
                        var center = CreateVector(collision.ParticleCenter.x, collision.ParticleCenter.y, collision.ParticleCenter.z);
                        var min = CreateVector(collision.TargetMin.x, collision.TargetMin.y, collision.TargetMin.z);
                        var max = CreateVector(collision.TargetMax.x, collision.TargetMax.y, collision.TargetMax.z);
                        var direction = collision.TargetDirection;
                        var particleDirection = collision.ParticleDirection;

                        particle.ImpactStatus.HasCrashed = true;
                        particle.ImpactStatus.ImpactDirection = particleDirection;

                        if (particle.ImpactStatus.SourceParticle?.ImpactStatus != null)
                        {
                            particle.ImpactStatus.SourceParticle.ImpactStatus.HasCrashed = true;
                            particle.ImpactStatus.SourceParticle.ImpactStatus.ImpactDirection = particleDirection;
                            // Tell the source what it hit (the other object's name)
                            particle.ImpactStatus.SourceParticle.ImpactStatus.ObjectName = other.ObjectName;
                        }

                        if (other.ImpactStatus != null)
                        {
                            other.ImpactStatus.HasCrashed = true;
                            other.ImpactStatus.ImpactDirection = direction;
                            // Tell the other object what hit it (the weapon name stored on the particle)
                            other.ImpactStatus.ObjectName = particle.ImpactStatus?.ObjectName
                                                            ?? particle.ObjectName;
                            other.ImpactStatus.CrashBoxName = other.CrashBoxNames != null && ob < other.CrashBoxNames.Count ? other.CrashBoxNames[ob] : null;
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
                                LogCrashBoxWorldPoints($"[PARTICLE BOX WORLD] {particle.ObjectName} Box[{pb}]", worldParticlePoints);

                            if (!SkipParticleLogging)
                                LogCrashBoxWorldPoints($"[OTHER BOX WORLD] {other.ObjectName} Box[{ob}]", worldOtherPoints);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleGeneralCollision(_3dObject a, _3dObject b)
        {
            bool aWasAlreadyCrashed = a.ImpactStatus?.HasCrashed == true;
            bool bWasAlreadyCrashed = b.ImpactStatus?.HasCrashed == true;

            for (int ai = 0; ai < a.CrashBoxes.Count; ai++)
            {
                var boxA = a.CrashBoxes[ai];

                var safeBoxA = GetWorldBoxPointsCached(a, ai, boxA);
                if (safeBoxA.Count == 0) continue;

                for (int bi = 0; bi < b.CrashBoxes.Count; bi++)
                {
                    var boxB = b.CrashBoxes[bi];

                    var safeBoxB = GetWorldBoxPointsCached(b, bi, boxB);

                    if (ShouldLogAny && !LogOnlyCollisions && CheckLogFilter(a, b) && LogCollisionDetails)
                    {
                        Logger.Log($"PTS {a.ObjectName}[{ai}] vs {b.ObjectName}[{bi}] A:{string.Join(";", safeBoxA.Select(p => $"({p.x:0.#},{p.y:0.#},{p.z:0.#})"))} | B:{string.Join(";", safeBoxB.Select(p => $"({p.x:0.#},{p.y:0.#},{p.z:0.#})"))}");
                    }

                    if (safeBoxB.Count == 0) continue;

                    var overlapCheck = CollisionPairMath.CheckBoxOverlap(
                        safeBoxA,
                        safeBoxB,
                        GetCollisionMargins());
                    LogCollisionBoxCheck(overlapCheck, a.ObjectName, b.ObjectName);

                    if (overlapCheck.Overlaps)
                    {
                        var collision = CollisionPairMath.CreateBoxCollision(ai, bi, overlapCheck);
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
                            continue;
                        }

                        if (TryMarkTerrainAvoidanceContact(a, b, ai, bi, centerA, centerB, centerDistance))
                        {
                            return true;
                        }

                        a.ImpactStatus.HasCrashed = true;
                        b.ImpactStatus.HasCrashed = true;

                        if (!aWasAlreadyCrashed || !IsCombatCollisionName(a.ImpactStatus.ObjectName))
                        {
                            a.ImpactStatus.ObjectName = b.ObjectName;
                            a.ImpactStatus.ImpactDirection = collision.DirectionA;
                            a.ImpactStatus.CrashBoxName = a.CrashBoxNames != null && ai < a.CrashBoxNames.Count ? a.CrashBoxNames[ai] : null;
                        }

                        if (!bWasAlreadyCrashed || !IsCombatCollisionName(b.ImpactStatus.ObjectName))
                        {
                            b.ImpactStatus.ObjectName = a.ObjectName;
                            b.ImpactStatus.ImpactDirection = collision.DirectionB;
                            b.ImpactStatus.CrashBoxName = b.CrashBoxNames != null && bi < b.CrashBoxNames.Count ? b.CrashBoxNames[bi] : null;
                        }

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

                            LogCrashBoxWorldPoints($"[A BOX WORLD] {a.ObjectName} Box[{ai}]", safeBoxA);
                            LogCrashBoxWorldPoints($"[B BOX WORLD] {b.ObjectName} Box[{bi}]", safeBoxB);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryMarkTerrainAvoidanceContact(
            _3dObject a,
            _3dObject b,
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
                ai.ImpactStatus.HasCrashed = true;
                ai.ImpactStatus.ObjectName = obstacle.ObjectName;
                ai.ImpactStatus.ImpactDirection = CollisionDirectionMath.EstimateDirection(aiCenter, obstacleCenter);
                ai.ImpactStatus.CrashBoxName = ai.CrashBoxNames != null && aiBoxIndex < ai.CrashBoxNames.Count ? ai.CrashBoxNames[aiBoxIndex] : null;
            }

            LogCollision(ai, obstacle,
                $"[FRAME:{numFrame}] [AI TERRAIN CONTACT] {ai.ObjectName} -> {obstacle.ObjectName} | CenterDistance:{centerDistance:0.##}");

            return true;
        }

        private static void HandleDecoyBlastDamage(List<_3dObject> activeWorld)
        {
            float blastRadius = CommonUtilities.CommonSetup.GameSetup.DecoyBlastRadius;
            int count = activeWorld.Count;

            for (int i = 0; i < count; i++)
            {
                var candidate = activeWorld[i];
                if (candidate == null) continue;
                if (candidate.ObjectName != "DroneDecoy") continue;
                if (candidate.ImpactStatus?.HasExploded == true) continue;
                if (candidate.CrashBoxes != null && candidate.CrashBoxes.Count > 0) continue;
                if (candidate.ObjectParts == null || candidate.ObjectParts.Count == 0) continue;

                if (!_processedDecoyBlasts.Add(candidate.ObjectId)) continue;

                // Use WorldPosition for blast distance so objects with different
                // ObjectOffsets are compared in the same coordinate space.
                var blastCenter = candidate.WorldPosition as Vector3;
                if (blastCenter == null) continue;

                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    var target = activeWorld[j];
                    if (target == null) continue;
                    if (target.CrashBoxes == null || target.CrashBoxes.Count == 0) continue;
                    if (target.ImpactStatus?.HasExploded == true) continue;
                    if (target.ImpactStatus?.HasCrashed == true) continue;

                    var flags = GetTypeFlagsCached(target);
                    if (flags.IsShip || flags.IsSurface || flags.IsParticle || flags.IsLazer || flags.IsStatic) continue;

                    var targetPos = target.WorldPosition as Vector3;
                    if (targetPos == null) continue;
                    float distance = (float)GeometryMath.GetDistance(blastCenter, targetPos);

                    if (distance <= blastRadius)
                    {
                        if (target.ImpactStatus != null)
                        {
                            target.ImpactStatus.HasCrashed = true;
                            target.ImpactStatus.ObjectName = "DroneDecoy";
                        }

                        LogCollision(candidate, target,
                            $"[FRAME:{numFrame}] [DECOY BLAST] {candidate.ObjectName} -> {target.ObjectName} | Distance:{distance:0.##} | BlastRadius:{blastRadius:0.##}");
                    }
                }
            }
        }

        private static void HandleBomberBombBlastDamage(List<_3dObject> activeWorld)
        {
            float blastRadius = CommonUtilities.CommonSetup.GameSetup.BomberBombBlastRadius;
            float blastDamage = CommonUtilities.CommonSetup.GameSetup.BomberBombBlastDamage;
            int count = activeWorld.Count;

            for (int i = 0; i < count; i++)
            {
                var bomb = activeWorld[i];
                if (bomb == null) continue;
                if (bomb.ObjectName != "BomberBomb") continue;
                if (bomb.ImpactStatus?.HasExploded == true) continue;
                // Bomb is exploding when its crashboxes have been cleared
                if (bomb.CrashBoxes != null && bomb.CrashBoxes.Count > 0) continue;
                if (bomb.ObjectParts == null || bomb.ObjectParts.Count == 0) continue;

                if (!_processedBombBlasts.Add(bomb.ObjectId)) continue;

                var blastCenter = bomb.WorldPosition as Vector3;
                if (blastCenter == null) continue;

                // Find the ship and check blast distance using ObjectOffsets
                // (ship WorldPosition is 0,0,0; bombs use world-synced offsets)
                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    var target = activeWorld[j];
                    if (target == null) continue;
                    if (target.ObjectName != "Ship") continue;

                    // Use the ship's crash center world position for distance
                    var shipPosRaw = CommonUtilities.CommonGlobalState.GameState.ShipState?.ShipCrashCenterWorldPosition;
                    if (shipPosRaw == null) continue;
                    var shipPos = new Vector3 { x = shipPosRaw.x, y = shipPosRaw.y, z = shipPosRaw.z };

                    float distance = (float)GeometryMath.GetDistance(blastCenter, shipPos);

                    if (distance <= blastRadius)
                    {
                        CommonUtilities.CommonGlobalState.GameState.GamePlayState?.ApplyDamage(blastDamage);

                        LogCollision(bomb, target,
                            $"[FRAME:{numFrame}] [BOMB BLAST] {bomb.ObjectName} -> {target.ObjectName} | Distance:{distance:0.##} | BlastRadius:{blastRadius:0.##} | Damage:{blastDamage:0.##}");
                    }
                }
            }
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
