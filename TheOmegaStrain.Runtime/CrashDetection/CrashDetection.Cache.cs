using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using RetroMesh.Engine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        private static readonly Dictionary<_3dObject, List<List<Vector3>>> RotatedBoxCache = new();
        private static readonly Dictionary<_3dObject, Vector3> OffsetCache = new();
        private static readonly Dictionary<_3dObject, List<Vector3>> WorldPointsCache = new();
        private static readonly Dictionary<_3dObject, Vector3> CenterCache = new();
        private static readonly Dictionary<(_3dObject obj, int boxIndex), List<Vector3>> WorldBoxCache = new();
        private static readonly Dictionary<_3dObject, ObjectTypeFlags> TypeFlagCache = new();
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
                IsWeapon = CommonUtilities.CommonSetup.WeaponSetup.IsWeaponTypeValid(name);
                IsSeeder = name == "Seeder";
                IsShip = name == "Ship";
                IsSurface = name == "Surface";
                IsEnemy = CommonUtilities.CommonSetup.EnemySetup.IsEnemyTypeValid(name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetFrameCachesIfNeeded()
        {
            if (_cacheFrame == numFrame) return;

            _cacheFrame = numFrame;
            OffsetCache.Clear();
            WorldPointsCache.Clear();
            CenterCache.Clear();
            WorldBoxCache.Clear();
            TypeFlagCache.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ObjectTypeFlags GetTypeFlagsCached(_3dObject obj)
        {
            if (TypeFlagCache.TryGetValue(obj, out var flags))
            {
                CacheHits++;
                return flags;
            }

            CacheMisses++;
            var name = obj.ObjectName ?? string.Empty;
            flags = new ObjectTypeFlags(name);
            TypeFlagCache[obj] = flags;
            return flags;
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
            CommonUtilities.CommonSetup.TerrainAvoidanceSetup.IsTerrainObstacle(flags.Name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTerrainAvoidanceAiObject(_3dObject obj, ObjectTypeFlags flags)
        {
            if (!CommonUtilities.CommonSetup.TerrainAvoidanceSetup.IsAvoidanceCapableAi(flags.Name))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetOffsetCached(_3dObject obj)
        {
            if (OffsetCache.TryGetValue(obj, out var offset))
            {
                CacheHits++;
                return offset;
            }

            CacheMisses++;
            offset = obj.GetEffectiveCrashOffset();
            OffsetCache[obj] = offset;
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<Vector3> GetWorldPointsCached(_3dObject obj)
        {
            if (WorldPointsCache.TryGetValue(obj, out var points))
            {
                CacheHits++;
                return points;
            }

            CacheMisses++;
            var offset = GetOffsetCached(obj);
            points = obj.GetAllCrashPointsWorld(offset);
            WorldPointsCache[obj] = points;
            return points;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetCenterCached(_3dObject obj)
        {
            if (CenterCache.TryGetValue(obj, out var center))
            {
                CacheHits++;
                return center;
            }

            CacheMisses++;
            var points = GetWorldPointsCached(obj);
            center = GetCenterOfBox(points);
            CenterCache[obj] = center;
            return center;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<Vector3> GetWorldBoxPointsCached(_3dObject obj, int boxIndex, List<IVector3> box)
        {
            var key = (obj, boxIndex);
            if (WorldBoxCache.TryGetValue(key, out var points))
            {
                CacheHits++;
                return points;
            }

            CacheMisses++;
            var offset = GetOffsetCached(obj);
            points = box.ToCrashWorldPoints(offset);
            WorldBoxCache[key] = points;
            return points;
        }

        public static bool IsStatic(string objectName) =>
            IsStaticName(objectName);
    }
}
