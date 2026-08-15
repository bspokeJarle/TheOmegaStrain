using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using RetroMesh.Engine;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

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
        private static bool ShouldLogPair(OmegaObject3D a, OmegaObject3D b)
        {
            if (!ShouldLogAny) return false;
            return CheckLogFilter(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogNonCollision(OmegaObject3D a, OmegaObject3D b, string message)
        {
            if (LogOnlyCollisions) return;
            if (!ShouldLogPair(a, b)) return;
            Logger.Log(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogCollision(OmegaObject3D a, OmegaObject3D b, string message)
        {
            if (!ShouldLogPair(a, b)) return;
            Logger.Log(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogCollisionDetail(OmegaObject3D a, OmegaObject3D b, string message)
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

            var bounds = AabbBounds.FromPoints(points);
            var center = CollisionBoxMath.GetCenter(bounds);

            static string F(float v) =>
                v.ToString("0.##", CultureInfo.InvariantCulture);

            Logger.Log(title);
            Logger.Log(
                $"  AABB Min=({F(bounds.MinX)},{F(bounds.MinY)},{F(bounds.MinZ)}) " +
                $"Max=({F(bounds.MaxX)},{F(bounds.MaxY)},{F(bounds.MaxZ)}) " +
                $"Center=({F(center.x)},{F(center.y)},{F(center.z)})"
            );

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                Logger.Log($"  P{i}: ({F(p.x)},{F(p.y)},{F(p.z)})");
            }
        }

        public static void LogSnapShots(OmegaObject3D inhabitant, OmegaObject3D otherInhabitant)
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

        private static void LogObject(string role, OmegaObject3D obj)
        {
            Logger.Log($"[SNAPSHOT] --- {role}: {obj.ObjectName} ---");

            if (obj.ObjectOffsets != null)
                Logger.Log($"[SNAPSHOT] ObjectOffsets: (x={obj.ObjectOffsets.x:0.##}, y={obj.ObjectOffsets.y:0.##}, z={obj.ObjectOffsets.z:0.##})");

            if (GameState.SurfaceState.GlobalMapPosition != null)
                Logger.Log($"[SNAPSHOT] GlobalMapPosition: (x={GameState.SurfaceState.GlobalMapPosition.x:0.##}, z={GameState.SurfaceState.GlobalMapPosition.z:0.##})");

            var calculated = obj.CalculatedCrashOffset ?? new Vector3(0, 0, 0);
            Logger.Log($"[SNAPSHOT] CalculatedCrashOffset: (x={calculated.x:0.##}, y={calculated.y:0.##}, z={calculated.z:0.##})");

            var effectiveOffset = CrashBoxTransform.GetEffectiveCrashOffset(obj, CreateVector);
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

                var localBox = CrashBoxTransform.ToCrashWorldPoints(box, new Vector3(0, 0, 0), CreateVector);
                LogCrashboxAnalysis(
                    $"[SNAPSHOT] [FRAME:{numFrame}] {role}:{obj.ObjectName} Box[{i}] LOCAL",
                    localBox
                );

                var worldBox = CrashBoxTransform.ToCrashWorldPoints(box, effectiveOffset, CreateVector);

                LogCrashboxAnalysis(
                    $"[SNAPSHOT] [FRAME:{numFrame}] {role}:{obj.ObjectName} Box[{i}] WORLD (EffectiveCrashOffset)",
                    worldBox
                );

                var center = GetCenterOfBox(worldBox);
                Logger.Log($"[SNAPSHOT] CrashBox[{i}] WORLD Center: (x={center.x:0.##}, y={center.y:0.##}, z={center.z:0.##})");
            }
        }

        private static void LogCrashboxAnalysis(string label, List<Vector3> box)
        {
            if (!ShouldLogAny || box == null || box.Count == 0)
                return;

            var bounds = AabbBounds.FromPoints(box);
            var aabbCenter = CollisionBoxMath.GetCenter(bounds);
            float avgX = box.Average(p => p.x);
            float avgY = box.Average(p => p.y);
            float avgZ = box.Average(p => p.z);

            static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            Logger.Log("--- " + label + " ---");
            Logger.Log("Y-range: [" + F(bounds.MinY) + "-" + F(bounds.MaxY) + "], X-range: [" + F(bounds.MinX) + "-" + F(bounds.MaxX) + "], Z-range: [" + F(bounds.MinZ) + "-" + F(bounds.MaxZ) + "]");
            Logger.Log("Center(AABB): (x=" + F(aabbCenter.x) + ", y=" + F(aabbCenter.y) + ", z=" + F(aabbCenter.z) + ")");
            Logger.Log("Center(AVG):  (x=" + F(avgX) + ", y=" + F(avgY) + ", z=" + F(avgZ) + ")");

            foreach (var p in box)
                Logger.Log("(x=" + F(p.x) + ", y=" + F(p.y) + ", z=" + F(p.z) + ")");

            Logger.Log("--- End of " + label + "---\n");
        }
    }
}
