using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public delegate List<TVector> CollisionWorldBoxProvider<TObject, TVector>(
        TObject obj,
        int boxIndex,
        List<IVector3> localBox)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public delegate void CollisionBoxPairProbe<TObject, TVector>(
        in CollisionBoxPairContext<TObject, TVector> context)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public delegate void CollisionBoxOverlapProbe<TObject, TVector>(
        in CollisionBoxOverlapContext<TObject, TVector> context)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public delegate bool CollisionBoxCollisionHandler<TObject, TVector>(
        in CollisionBoxScanResult<TObject, TVector> result)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public readonly record struct CollisionBoxPairContext<TObject, TVector>(
        TObject A,
        TObject B,
        int BoxIndexA,
        int BoxIndexB,
        List<TVector> WorldBoxA,
        List<TVector> WorldBoxB)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public readonly record struct CollisionBoxOverlapContext<TObject, TVector>(
        TObject A,
        TObject B,
        int BoxIndexA,
        int BoxIndexB,
        List<TVector> WorldBoxA,
        List<TVector> WorldBoxB,
        CollisionBoxOverlapCheck OverlapCheck)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public readonly record struct CollisionBoxScanResult<TObject, TVector>(
        TObject A,
        TObject B,
        int BoxIndexA,
        int BoxIndexB,
        List<TVector> WorldBoxA,
        List<TVector> WorldBoxB,
        CollisionBoxPairResult Collision)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public readonly record struct ParticleCollisionScanResult<TObject, TVector>(
        TObject Particle,
        TObject Target,
        int ParticleBoxIndex,
        int TargetBoxIndex,
        List<TVector> ParticleWorldBox,
        List<TVector> TargetWorldBox,
        ParticleCollisionResult Collision)
        where TObject : class, IRenderable3dObject
        where TVector : class, IVector3;

    public static class CollisionBoxScanner
    {
        public static bool ScanBoxCollisions<TObject, TVector>(
            TObject a,
            TObject b,
            CollisionMargins margins,
            CollisionWorldBoxProvider<TObject, TVector> getWorldBox,
            CollisionBoxCollisionHandler<TObject, TVector> handleCollision,
            CollisionBoxPairProbe<TObject, TVector>? onBoxPair = null,
            CollisionBoxOverlapProbe<TObject, TVector>? onOverlapCheck = null)
            where TObject : class, IRenderable3dObject
            where TVector : class, IVector3
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            if (b == null)
                throw new ArgumentNullException(nameof(b));

            if (getWorldBox == null)
                throw new ArgumentNullException(nameof(getWorldBox));

            if (handleCollision == null)
                throw new ArgumentNullException(nameof(handleCollision));

            if (a.CrashBoxes == null || b.CrashBoxes == null)
                return false;

            for (int ai = 0; ai < a.CrashBoxes.Count; ai++)
            {
                var boxA = a.CrashBoxes[ai];
                if (boxA == null)
                    continue;

                var worldBoxA = getWorldBox(a, ai, boxA);
                if (worldBoxA.Count == 0)
                    continue;

                for (int bi = 0; bi < b.CrashBoxes.Count; bi++)
                {
                    var boxB = b.CrashBoxes[bi];
                    if (boxB == null)
                        continue;

                    var worldBoxB = getWorldBox(b, bi, boxB);
                    var pairContext = new CollisionBoxPairContext<TObject, TVector>(
                        a,
                        b,
                        ai,
                        bi,
                        worldBoxA,
                        worldBoxB);

                    onBoxPair?.Invoke(in pairContext);

                    if (worldBoxB.Count == 0)
                        continue;

                    var overlapCheck = CollisionPairMath.CheckBoxOverlap(
                        worldBoxA,
                        worldBoxB,
                        margins);

                    var overlapContext = new CollisionBoxOverlapContext<TObject, TVector>(
                        a,
                        b,
                        ai,
                        bi,
                        worldBoxA,
                        worldBoxB,
                        overlapCheck);

                    onOverlapCheck?.Invoke(in overlapContext);

                    if (!overlapCheck.Overlaps)
                        continue;

                    var result = new CollisionBoxScanResult<TObject, TVector>(
                        a,
                        b,
                        ai,
                        bi,
                        worldBoxA,
                        worldBoxB,
                        CollisionPairMath.CreateBoxCollision(ai, bi, overlapCheck));

                    if (handleCollision(in result))
                        return true;
                }
            }

            return false;
        }

        public static bool TryFindFirstBoxCollision<TObject, TVector>(
            TObject a,
            TObject b,
            CollisionMargins margins,
            CollisionWorldBoxProvider<TObject, TVector> getWorldBox,
            out CollisionBoxScanResult<TObject, TVector> result,
            CollisionBoxPairProbe<TObject, TVector>? onBoxPair = null,
            CollisionBoxOverlapProbe<TObject, TVector>? onOverlapCheck = null)
            where TObject : class, IRenderable3dObject
            where TVector : class, IVector3
        {
            CollisionBoxScanResult<TObject, TVector> firstResult = default;

            bool found = ScanBoxCollisions(
                a,
                b,
                margins,
                getWorldBox,
                (in CollisionBoxScanResult<TObject, TVector> collision) =>
                {
                    firstResult = collision;
                    return true;
                },
                onBoxPair,
                onOverlapCheck);

            result = firstResult;
            return found;
        }

        public static bool TryFindFirstParticleCollision<TObject, TVector>(
            TObject particle,
            TObject target,
            IVector3? particleVelocity,
            CollisionWorldBoxProvider<TObject, TVector> getWorldBox,
            out ParticleCollisionScanResult<TObject, TVector> result)
            where TObject : class, IRenderable3dObject
            where TVector : class, IVector3
        {
            if (particle == null)
                throw new ArgumentNullException(nameof(particle));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (getWorldBox == null)
                throw new ArgumentNullException(nameof(getWorldBox));

            if (particle.CrashBoxes == null || target.CrashBoxes == null)
            {
                result = default;
                return false;
            }

            for (int particleBoxIndex = 0; particleBoxIndex < particle.CrashBoxes.Count; particleBoxIndex++)
            {
                var particleBox = particle.CrashBoxes[particleBoxIndex];
                if (particleBox == null)
                    continue;

                var worldParticlePoints = getWorldBox(particle, particleBoxIndex, particleBox);
                if (worldParticlePoints.Count == 0)
                    continue;

                for (int targetBoxIndex = 0; targetBoxIndex < target.CrashBoxes.Count; targetBoxIndex++)
                {
                    var targetBox = target.CrashBoxes[targetBoxIndex];
                    if (targetBox == null)
                        continue;

                    var worldTargetPoints = getWorldBox(target, targetBoxIndex, targetBox);
                    if (worldTargetPoints.Count == 0)
                        continue;

                    if (!CollisionPairMath.TryCreateParticleCollision(
                            worldParticlePoints,
                            worldTargetPoints,
                            particleVelocity,
                            out var collision))
                    {
                        continue;
                    }

                    result = new ParticleCollisionScanResult<TObject, TVector>(
                        particle,
                        target,
                        particleBoxIndex,
                        targetBoxIndex,
                        worldParticlePoints,
                        worldTargetPoints,
                        collision);
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
