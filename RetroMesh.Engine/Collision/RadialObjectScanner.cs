using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public delegate bool RadialSourcePredicate<TObject>(TObject source)
        where TObject : class;

    public delegate bool RadialTargetPredicate<TObject>(TObject source, TObject target)
        where TObject : class;

    public delegate TVector? RadialPositionProvider<TObject, TVector>(TObject obj)
        where TObject : class
        where TVector : class, IVector3;

    public delegate void RadialHitHandler<TObject, TVector>(
        in RadialHitContext<TObject, TVector> context)
        where TObject : class
        where TVector : class, IVector3;

    public readonly record struct RadialHitContext<TObject, TVector>(
        int SourceIndex,
        int TargetIndex,
        TObject Source,
        TObject Target,
        TVector SourcePosition,
        TVector TargetPosition,
        float Distance,
        float Radius)
        where TObject : class
        where TVector : class, IVector3;

    public static class RadialObjectScanner
    {
        public static int Scan<TObject, TVector>(
            IReadOnlyList<TObject> objects,
            float radius,
            RadialSourcePredicate<TObject> isSource,
            RadialPositionProvider<TObject, TVector> getSourcePosition,
            RadialTargetPredicate<TObject> isTarget,
            RadialPositionProvider<TObject, TVector> getTargetPosition,
            RadialHitHandler<TObject, TVector> handleHit,
            bool allowSelf = false)
            where TObject : class
            where TVector : class, IVector3
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            if (isSource == null)
                throw new ArgumentNullException(nameof(isSource));

            if (getSourcePosition == null)
                throw new ArgumentNullException(nameof(getSourcePosition));

            if (isTarget == null)
                throw new ArgumentNullException(nameof(isTarget));

            if (getTargetPosition == null)
                throw new ArgumentNullException(nameof(getTargetPosition));

            if (handleHit == null)
                throw new ArgumentNullException(nameof(handleHit));

            float radiusSquared = radius * radius;
            int hitCount = 0;
            int count = objects.Count;

            for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
            {
                var source = objects[sourceIndex];
                if (source == null || !isSource(source))
                    continue;

                var sourcePosition = getSourcePosition(source);
                if (sourcePosition == null)
                    continue;

                for (int targetIndex = 0; targetIndex < count; targetIndex++)
                {
                    if (!allowSelf && targetIndex == sourceIndex)
                        continue;

                    var target = objects[targetIndex];
                    if (target == null || !isTarget(source, target))
                        continue;

                    var targetPosition = getTargetPosition(target);
                    if (targetPosition == null)
                        continue;

                    float distanceSquared = GeometryMath.GetDistanceSquared(sourcePosition, targetPosition);
                    if (distanceSquared > radiusSquared)
                        continue;

                    var context = new RadialHitContext<TObject, TVector>(
                        sourceIndex,
                        targetIndex,
                        source,
                        target,
                        sourcePosition,
                        targetPosition,
                        MathF.Sqrt(distanceSquared),
                        radius);

                    handleHit(in context);
                    hitCount++;
                }
            }

            return hitCount;
        }
    }
}
