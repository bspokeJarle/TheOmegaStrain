using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public delegate TClassification CollisionObjectClassifier<TObject, TClassification>(TObject obj)
        where TObject : class;

    public delegate bool CollisionObjectIncludePredicate<TObject>(TObject obj)
        where TObject : class;

    public delegate bool CollisionPairFilter<TObject, TClassification>(
        in CollisionPairContext<TObject, TClassification> context)
        where TObject : class;

    public delegate void CollisionPairHandler<TObject, TClassification>(
        in CollisionPairContext<TObject, TClassification> context)
        where TObject : class;

    public readonly record struct CollisionPairContext<TObject, TClassification>(
        int IndexA,
        int IndexB,
        TObject A,
        TObject B,
        TClassification ClassificationA,
        TClassification ClassificationB)
        where TObject : class;

    public sealed class CollisionPairScanner<TObject, TClassification>
        where TObject : class
    {
        private readonly Dictionary<TObject, TClassification> classificationCache =
            new(ReferenceEqualityComparer<TObject>.Instance);

        public int ClassificationCacheHits { get; private set; }
        public int ClassificationCacheMisses { get; private set; }
        public int PairsVisited { get; private set; }
        public int PairsHandled { get; private set; }
        public int PairsSkipped { get; private set; }

        public void ResetFrame()
        {
            classificationCache.Clear();
            ClassificationCacheHits = 0;
            ClassificationCacheMisses = 0;
            PairsVisited = 0;
            PairsHandled = 0;
            PairsSkipped = 0;
        }

        public void Scan(
            IReadOnlyList<TObject> objects,
            CollisionObjectClassifier<TObject, TClassification> classify,
            CollisionPairHandler<TObject, TClassification> handlePair,
            CollisionPairFilter<TObject, TClassification>? shouldSkipPair = null,
            CollisionObjectIncludePredicate<TObject>? includeObject = null)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            if (classify == null)
                throw new ArgumentNullException(nameof(classify));

            if (handlePair == null)
                throw new ArgumentNullException(nameof(handlePair));

            ResetFrame();

            int count = objects.Count;
            for (int i = 0; i < count; i++)
            {
                var a = objects[i];
                if (a == null || includeObject?.Invoke(a) == false)
                    continue;

                for (int j = i + 1; j < count; j++)
                {
                    var b = objects[j];
                    if (b == null || includeObject?.Invoke(b) == false)
                        continue;

                    var context = new CollisionPairContext<TObject, TClassification>(
                        i,
                        j,
                        a,
                        b,
                        GetClassification(a, classify),
                        GetClassification(b, classify));

                    PairsVisited++;

                    if (shouldSkipPair != null && shouldSkipPair(in context))
                    {
                        PairsSkipped++;
                        continue;
                    }

                    handlePair(in context);
                    PairsHandled++;
                }
            }
        }

        private TClassification GetClassification(
            TObject obj,
            CollisionObjectClassifier<TObject, TClassification> classify)
        {
            if (classificationCache.TryGetValue(obj, out var classification))
            {
                ClassificationCacheHits++;
                return classification;
            }

            ClassificationCacheMisses++;
            classification = classify(obj);
            classificationCache[obj] = classification;
            return classification;
        }
    }
}
