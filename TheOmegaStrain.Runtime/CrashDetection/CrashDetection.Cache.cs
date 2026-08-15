using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using RetroMesh.Engine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        private static readonly CollisionPairScanner<OmegaObject3D, ObjectTypeFlags> PairScanner = new();
        private static readonly CollisionFrameCache<OmegaObject3D, Vector3> FrameCache = new();
        private static readonly Dictionary<OmegaObject3D, ObjectTypeFlags> TypeFlagCache = new();
        private static int _cacheFrame = -1;

        private static int CacheHits = 0;
        private static int CacheMisses = 0;
        private static int SkippedByDistance = 0;
        private static int numFrame = 0;
        private static readonly HashSet<int> _processedDecoyBlasts = new();
        private static readonly HashSet<int> _processedBombBlasts = new();

        private readonly struct ObjectTypeFlags
        {
            public readonly bool IsStatic;
            public readonly bool IsParticle;
            public readonly bool IsLazer;
            public readonly bool IsWeapon;
            public readonly bool IsSeeder;
            public readonly bool IsShip;
            public readonly bool IsSurface;
            public readonly bool IsEnemy;
            public readonly string Name;

            public ObjectTypeFlags(string name)
            {
                Name = name;
                IsStatic = IsStaticName(name);
                IsParticle = name == "Particle";
                IsLazer = name == "Lazer" || name == "EnemyLazer" || name == "EnemyLazerMedium";
                IsWeapon = TheOmegaStrain.Common.CommonSetup.WeaponSetup.IsWeaponTypeValid(name);
                IsSeeder = name == "Seeder";
                IsShip = name == "Ship";
                IsSurface = name == "Surface";
                IsEnemy = TheOmegaStrain.Common.CommonSetup.EnemySetup.IsEnemyTypeValid(name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetFrameCachesIfNeeded()
        {
            if (_cacheFrame == numFrame) return;

            _cacheFrame = numFrame;
            FrameCache.ResetFrame();
            TypeFlagCache.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ObjectTypeFlags GetTypeFlagsCached(OmegaObject3D obj)
        {
            if (TypeFlagCache.TryGetValue(obj, out var flags))
            {
                CacheHits++;
                return flags;
            }

            CacheMisses++;
            flags = CreateTypeFlags(obj);
            TypeFlagCache[obj] = flags;
            return flags;
        }

        private static ObjectTypeFlags CreateTypeFlags(OmegaObject3D obj)
        {
            return new ObjectTypeFlags(obj.ObjectName ?? string.Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsStaticName(string objectName) =>
            objectName == "Tree" ||
            objectName == "LeafTree" ||
            objectName == "LargePalm" ||
            objectName == "SmallPalm" ||
            objectName == "BambooHut" ||
            objectName == "Surface" ||
            objectName == "House" ||
            objectName == "Tower" ||
            objectName == "SnowTower" ||
            objectName == "SmallIgloo" ||
            objectName == "LargeIgloo";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTerrainObstacle(ObjectTypeFlags flags) =>
            TheOmegaStrain.Common.CommonSetup.TerrainAvoidanceSetup.IsTerrainObstacle(flags.Name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTerrainAvoidanceAiObject(OmegaObject3D obj, ObjectTypeFlags flags)
        {
            if (!TheOmegaStrain.Common.CommonSetup.TerrainAvoidanceSetup.IsAvoidanceCapableAi(flags.Name))
                return false;

            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
                return false;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == obj.ObjectId)
                    return true;
            }

            return false;
        }

        private static Vector3 CreateVector(float x, float y, float z) => new(x, y, z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetOffsetCached(OmegaObject3D obj)
        {
            return FrameCache.GetOffset(obj, CreateVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<Vector3> GetWorldPointsCached(OmegaObject3D obj)
        {
            return FrameCache.GetWorldPoints(obj, CreateVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetCenterCached(OmegaObject3D obj)
        {
            return FrameCache.GetCenter(obj, CreateVector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<Vector3> GetWorldBoxPointsCached(OmegaObject3D obj, int boxIndex, List<IVector3> box)
        {
            return FrameCache.GetWorldBoxPoints(obj, boxIndex, box, CreateVector);
        }

        private static int TotalCacheHits =>
            CacheHits + FrameCache.CacheHits + PairScanner.ClassificationCacheHits;

        private static int TotalCacheMisses =>
            CacheMisses + FrameCache.CacheMisses + PairScanner.ClassificationCacheMisses;

        public static bool IsStatic(string objectName) =>
            IsStaticName(objectName);
    }
}
