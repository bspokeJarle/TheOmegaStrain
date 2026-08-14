using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public sealed class CollisionFrameCache<TObject, TVector>
        where TObject : class, IRenderable3dObject
        where TVector : IVector3
    {
        private readonly Dictionary<TObject, TVector> _offsetCache = new(ReferenceEqualityComparer<TObject>.Instance);
        private readonly Dictionary<TObject, List<TVector>> _worldPointsCache = new(ReferenceEqualityComparer<TObject>.Instance);
        private readonly Dictionary<TObject, TVector> _centerCache = new(ReferenceEqualityComparer<TObject>.Instance);
        private readonly Dictionary<CollisionBoxCacheKey<TObject>, List<TVector>> _worldBoxCache = new();

        public int CacheHits { get; private set; }
        public int CacheMisses { get; private set; }

        public void ResetFrame()
        {
            _offsetCache.Clear();
            _worldPointsCache.Clear();
            _centerCache.Clear();
            _worldBoxCache.Clear();
        }

        public TVector GetOffset(
            TObject obj,
            Func<float, float, float, TVector> vectorFactory)
        {
            if (_offsetCache.TryGetValue(obj, out var offset))
            {
                CacheHits++;
                return offset;
            }

            CacheMisses++;
            offset = CrashBoxTransform.GetEffectiveCrashOffset(obj, vectorFactory);
            _offsetCache[obj] = offset;
            return offset;
        }

        public List<TVector> GetWorldPoints(
            TObject obj,
            Func<float, float, float, TVector> vectorFactory)
        {
            if (_worldPointsCache.TryGetValue(obj, out var points))
            {
                CacheHits++;
                return points;
            }

            CacheMisses++;
            var offset = GetOffset(obj, vectorFactory);
            points = CrashBoxTransform.GetAllCrashPointsWorld(obj, offset, vectorFactory);
            _worldPointsCache[obj] = points;
            return points;
        }

        public TVector GetCenter(
            TObject obj,
            Func<float, float, float, TVector> vectorFactory)
        {
            if (_centerCache.TryGetValue(obj, out var center))
            {
                CacheHits++;
                return center;
            }

            CacheMisses++;
            var points = GetWorldPoints(obj, vectorFactory);
            var rawCenter = GetCenterOfPoints(points);
            center = vectorFactory(rawCenter.x, rawCenter.y, rawCenter.z);
            _centerCache[obj] = center;
            return center;
        }

        public List<TVector> GetWorldBoxPoints(
            TObject obj,
            int boxIndex,
            List<IVector3> box,
            Func<float, float, float, TVector> vectorFactory)
        {
            var key = new CollisionBoxCacheKey<TObject>(obj, boxIndex);
            if (_worldBoxCache.TryGetValue(key, out var points))
            {
                CacheHits++;
                return points;
            }

            CacheMisses++;
            var offset = GetOffset(obj, vectorFactory);
            points = CrashBoxTransform.ToCrashWorldPoints(box, offset, vectorFactory);
            _worldBoxCache[key] = points;
            return points;
        }

        private static EngineVector3 GetCenterOfPoints(IReadOnlyList<TVector>? points)
        {
            if (points == null || points.Count == 0)
                return new EngineVector3();

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                minX = Math.Min(minX, point.x);
                maxX = Math.Max(maxX, point.x);
                minY = Math.Min(minY, point.y);
                maxY = Math.Max(maxY, point.y);
                minZ = Math.Min(minZ, point.z);
                maxZ = Math.Max(maxZ, point.z);
            }

            return new EngineVector3(
                (minX + maxX) / 2f,
                (minY + maxY) / 2f,
                (minZ + maxZ) / 2f);
        }

        private readonly struct CollisionBoxCacheKey<T>
            where T : class
        {
            private readonly T _object;
            private readonly int _boxIndex;

            public CollisionBoxCacheKey(T obj, int boxIndex)
            {
                _object = obj;
                _boxIndex = boxIndex;
            }

            public override bool Equals(object? obj)
            {
                return obj is CollisionBoxCacheKey<T> other &&
                       ReferenceEquals(_object, other._object) &&
                       _boxIndex == other._boxIndex;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_object),
                    _boxIndex);
            }
        }
    }
}
