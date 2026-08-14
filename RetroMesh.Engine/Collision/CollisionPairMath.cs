using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public readonly record struct CollisionMargins(float X, float Y, float Z);

    public readonly record struct CollisionBoxOverlapCheck(
        bool Overlaps,
        bool XOverlaps,
        bool YOverlaps,
        bool ZOverlaps,
        AabbBounds BoundsA,
        AabbBounds BoundsB);

    public readonly record struct CollisionBoxPairResult(
        int BoxIndexA,
        int BoxIndexB,
        AabbBounds BoundsA,
        AabbBounds BoundsB,
        EngineVector3 CenterA,
        EngineVector3 CenterB,
        float CenterDistance,
        ImpactDirection DirectionA,
        ImpactDirection DirectionB);

    public readonly record struct ParticleCollisionResult(
        EngineVector3 ParticleCenter,
        AabbBounds TargetBounds,
        EngineVector3 TargetMin,
        EngineVector3 TargetMax,
        ImpactDirection TargetDirection,
        ImpactDirection ParticleDirection);

    public static class CollisionPairMath
    {
        public static CollisionBoxOverlapCheck CheckBoxOverlap(
            IReadOnlyList<IVector3>? boxA,
            IReadOnlyList<IVector3>? boxB,
            CollisionMargins margins)
        {
            var boundsA = boxA == null || boxA.Count == 0
                ? new AabbBounds()
                : AabbBounds.FromPoints(boxA);
            var boundsB = boxB == null || boxB.Count == 0
                ? new AabbBounds()
                : AabbBounds.FromPoints(boxB);

            if (boxA == null || boxA.Count == 0 || boxB == null || boxB.Count == 0)
                return new CollisionBoxOverlapCheck(false, false, false, false, boundsA, boundsB);

            bool xOverlaps = CollisionBoxMath.RangesOverlap(
                boundsA.MinX,
                boundsA.MaxX,
                boundsB.MinX,
                boundsB.MaxX,
                margins.X);
            bool yOverlaps = CollisionBoxMath.RangesOverlap(
                boundsA.MinY,
                boundsA.MaxY,
                boundsB.MinY,
                boundsB.MaxY,
                margins.Y);
            bool zOverlaps = CollisionBoxMath.RangesOverlap(
                boundsA.MinZ,
                boundsA.MaxZ,
                boundsB.MinZ,
                boundsB.MaxZ,
                margins.Z);

            return new CollisionBoxOverlapCheck(
                xOverlaps && yOverlaps && zOverlaps,
                xOverlaps,
                yOverlaps,
                zOverlaps,
                boundsA,
                boundsB);
        }

        public static bool TryCreateBoxCollision(
            int boxIndexA,
            int boxIndexB,
            IReadOnlyList<IVector3>? boxA,
            IReadOnlyList<IVector3>? boxB,
            CollisionMargins margins,
            out CollisionBoxPairResult result)
        {
            var check = CheckBoxOverlap(boxA, boxB, margins);
            if (!check.Overlaps)
            {
                result = default;
                return false;
            }

            result = CreateBoxCollision(boxIndexA, boxIndexB, check);
            return true;
        }

        public static CollisionBoxPairResult CreateBoxCollision(
            int boxIndexA,
            int boxIndexB,
            CollisionBoxOverlapCheck check)
        {
            var centerA = CollisionBoxMath.GetCenter(check.BoundsA);
            var centerB = CollisionBoxMath.GetCenter(check.BoundsB);
            return new CollisionBoxPairResult(
                boxIndexA,
                boxIndexB,
                check.BoundsA,
                check.BoundsB,
                centerA,
                centerB,
                (float)GeometryMath.GetDistance(centerA, centerB),
                CollisionDirectionMath.EstimateDirection(centerA, centerB),
                CollisionDirectionMath.EstimateDirection(centerB, centerA));
        }

        public static bool TryCreateParticleCollision(
            IReadOnlyList<IVector3>? particleBox,
            IReadOnlyList<IVector3>? targetBox,
            IVector3? particleVelocity,
            out ParticleCollisionResult result)
        {
            if (particleBox == null || particleBox.Count == 0)
            {
                result = default;
                return false;
            }

            var particleCenter = GeometryMath.GetCenterOfBox(particleBox);
            if (!CollisionBoxMath.ContainsPoint(targetBox, particleCenter, out var targetBounds))
            {
                result = default;
                return false;
            }

            var min = new EngineVector3(targetBounds.MinX, targetBounds.MinY, targetBounds.MinZ);
            var max = new EngineVector3(targetBounds.MaxX, targetBounds.MaxY, targetBounds.MaxZ);
            var targetDirection = CollisionDirectionMath.EstimateDirectionFromAabb(particleCenter, min, max);
            var particleDirection = CollisionDirectionMath.EstimateDirectionFromVisibleMovement(
                particleVelocity,
                targetDirection);

            result = new ParticleCollisionResult(
                particleCenter,
                targetBounds,
                min,
                max,
                targetDirection,
                particleDirection);
            return true;
        }
    }
}
