using _3dRotations.Helpers;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Runtime.Collision
{
    public static partial class CrashDetection
    {
        private static List<string> LogFilter = ["PowerUp", "Ship"];

        public static bool LocalEnableLogging = false;
        public static bool LogOnlyCollisions = true;
        public static bool LogCollisionDetails = true;
        public static bool LogSkippedCollisions = false;
        public static bool SkipParticleLogging = true;

        public static double MaxCrashDistance = 625.0;

        private static bool ShouldLogAny => Logger.ShouldLog(LocalEnableLogging);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldLogPair(_3dObject a, _3dObject b)
        {
            if (!ShouldLogAny) return false;
            return CheckLogFilter(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogNonCollision(_3dObject a, _3dObject b, string message)
        {
            if (LogOnlyCollisions) return;
            if (!ShouldLogPair(a, b)) return;
            Logger.Log(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogCollision(_3dObject a, _3dObject b, string message)
        {
            if (!ShouldLogPair(a, b)) return;
            Logger.Log(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogCollisionDetail(_3dObject a, _3dObject b, string message)
        {
            if (!LogCollisionDetails) return;
            if (!ShouldLogPair(a, b)) return;
            Logger.Log(message);
        }

        private static bool CheckLogFilter(I3dObject activOobject, I3dObject otherObject)
        {
            if (LogFilter.Count == 0) return true;
            if (LogFilter.Contains(activOobject.ObjectName) || LogFilter.Contains(otherObject.ObjectName)) return true;
            return false;
        }

        private static void LogCrashBoxWorldPoints(string title, List<Vector3> points)
        {
            if (!ShouldLogAny || points == null || points.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
                if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
            }

            var center = new Vector3(
                (minX + maxX) / 2f,
                (minY + maxY) / 2f,
                (minZ + maxZ) / 2f
            );

            static string F(float v) =>
                v.ToString("0.##", CultureInfo.InvariantCulture);

            Logger.Log(title);
            Logger.Log(
                $"  AABB Min=({F(minX)},{F(minY)},{F(minZ)}) " +
                $"Max=({F(maxX)},{F(maxY)},{F(maxZ)}) " +
                $"Center=({F(center.x)},{F(center.y)},{F(center.z)})"
            );

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                Logger.Log($"  P{i}: ({F(p.x)},{F(p.y)},{F(p.z)})");
            }
        }

        public static void LogSnapShots(_3dObject inhabitant, _3dObject otherInhabitant)
        {
            if (!ShouldLogAny) return;

            if (inhabitant == null || otherInhabitant == null)
            {
                Logger.Log("[SNAPSHOT] One or both objects are null.");
                return;
            }

            LogObject("Inhabitant", inhabitant);
            LogObject("OtherInhabitant", otherInhabitant);
            Logger.Flush();
        }

        private static void LogObject(string role, _3dObject obj)
        {
            Logger.Log($"[SNAPSHOT] --- {role}: {obj.ObjectName} ---");

            if (obj.ObjectOffsets != null)
                Logger.Log($"[SNAPSHOT] ObjectOffsets: (x={obj.ObjectOffsets.x:0.##}, y={obj.ObjectOffsets.y:0.##}, z={obj.ObjectOffsets.z:0.##})");

            if (GameState.SurfaceState.GlobalMapPosition != null)
                Logger.Log($"[SNAPSHOT] GlobalMapPosition: (x={GameState.SurfaceState.GlobalMapPosition.x:0.##}, z={GameState.SurfaceState.GlobalMapPosition.z:0.##})");

            var calculated = obj.CalculatedCrashOffset ?? new Vector3(0, 0, 0);
            Logger.Log($"[SNAPSHOT] CalculatedCrashOffset: (x={calculated.x:0.##}, y={calculated.y:0.##}, z={calculated.z:0.##})");

            var effectiveOffset = obj.GetEffectiveCrashOffset();
            Logger.Log($"[SNAPSHOT] EffectiveCrashOffset: (x={effectiveOffset.x:0.##}, y={effectiveOffset.y:0.##}, z={effectiveOffset.z:0.##})");

            var crashBoxes = obj.CrashBoxes;
            if (crashBoxes == null || crashBoxes.Count == 0)
            {
                Logger.Log("[SNAPSHOT] CrashBoxes: <none>");
                return;
            }

            Logger.Log($"[SNAPSHOT] CrashBoxes count: {crashBoxes.Count}");

            for (int i = 0; i < crashBoxes.Count; i++)
            {
                var box = crashBoxes[i];
                if (box == null)
                {
                    Logger.Log($"[SNAPSHOT] CrashBox[{i}]: <null>");
                    continue;
                }

                Logger.Log($"[SNAPSHOT] CrashBox[{i}] LOCAL:");

                var localBox = ((System.Collections.IEnumerable)box).ToCrashWorldPoints(new Vector3(0, 0, 0));
                ObjectPlacementHelpers.LogCrashboxAnalysis(
                    $"[SNAPSHOT] [FRAME:{numFrame}] {role}:{obj.ObjectName} Box[{i}] LOCAL",
                    localBox
                );

                var worldBox = ((System.Collections.IEnumerable)box).ToCrashWorldPoints(effectiveOffset);

                ObjectPlacementHelpers.LogCrashboxAnalysis(
                    $"[SNAPSHOT] [FRAME:{numFrame}] {role}:{obj.ObjectName} Box[{i}] WORLD (EffectiveCrashOffset)",
                    worldBox
                );

                var center = GetCenterOfBox(worldBox);
                Logger.Log($"[SNAPSHOT] CrashBox[{i}] WORLD Center: (x={center.x:0.##}, y={center.y:0.##}, z={center.z:0.##})");
            }
        }
    }
}
