using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheOmegaStrain.Game.Helpers
{
    public static class ObjectPlacementHelpers
    {
        public static bool EnablePlacementLogging = false;

        private static Vector3 CreateVector(float x, float y, float z) => new(x, y, z);

        private static ITriangleMeshWithColor? GetSurfaceTriangle(OmegaObject3D obj)
        {
            var surface = obj?.ParentSurface;
            if (surface == null || obj?.SurfaceBasedId == null)
                return null;

            if (surface.RotatedSurfaceTriangleByLandId.TryGetValue(obj.SurfaceBasedId.Value, out var cachedTriangle))
                return cachedTriangle;

            return surface.RotatedSurfaceTriangles
                .FirstOrDefault(t => t.landBasedPosition == obj.SurfaceBasedId);
        }

        public static (float x, float y, float z) GetCrashBoxCenter(List<List<IVector3>> crashBoxes)
        {
            return ObjectPlacementMath.GetCrashBoxCenter(crashBoxes);
        }

        public static bool TryGetRenderPosition(OmegaObject3D obj, int screenCenterX, int screenCenterY, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (obj == null) return false;

            var localWorldPosition = obj.GetLocalWorldPosition();
            IVector3? surfaceAnchor = null;

            if (localWorldPosition == null && obj.SurfaceBasedId > 0)
            {
                var triangle = GetSurfaceTriangle(obj);
                if (triangle == null) return false;

                surfaceAnchor = triangle.vert1;
            }

            return ObjectPlacementMath.TryGetRenderPosition(
                obj,
                localWorldPosition,
                surfaceAnchor,
                screenCenterX,
                screenCenterY,
                CreateVector,
                out x,
                out y,
                out z);
        }

        public static void CenterObjectAt(I3dObject obj, IVector3 targetPosition)
        {
            ObjectPlacementMath.CenterObjectAt(obj, targetPosition, CreateVector);
        }

        public static void CenterCrashBoxesAt(OmegaObject3D obj, IVector3 targetPosition)
        {
            ObjectPlacementMath.CenterCrashBoxesAt(obj, targetPosition, CreateVector);
        }

        public static IVector3 GetObjectGeometricCenter(I3dObject obj, bool snapToBottomY = false)
        {
            return ObjectPlacementMath.GetObjectGeometricCenter(obj, snapToBottomY, CreateVector);
        }

        public static void CenterCrashBoxAt(List<Vector3> crashBox, IVector3 targetPosition, IVector3 crashboxOffsets)
        {
            if (targetPosition == null)
                return;

            ObjectPlacementMath.CenterCrashBoxAt(
                crashBox,
                targetPosition,
                crashboxOffsets,
                CreateVector,
                out float shiftY);

            if (Logger.ShouldLog(EnablePlacementLogging))
            {
                Logger.Log("[CenterAt] Target Y: " + targetPosition.y + ", CrashBoxOffsetY: " + crashboxOffsets.y + ", Final ShiftY: " + shiftY);
            }
        }


        public static void LogCrashboxAnalysis(string label, List<Vector3> box)
        {
            if (!Logger.ShouldLog(EnablePlacementLogging) || box == null || box.Count == 0) return;

            float yMin = box.Min(p => p.y);
            float yMax = box.Max(p => p.y);
            float xMin = box.Min(p => p.x);
            float xMax = box.Max(p => p.x);
            float zMin = box.Min(p => p.z);
            float zMax = box.Max(p => p.z);

            // Average center (what you have today)
            var avgCenter = new Vector3
            {
                x = box.Average(p => p.x),
                y = box.Average(p => p.y),
                z = box.Average(p => p.z)
            };

            // AABB center (matches CrashDetection GetCenterOfBox)
            var aabbCenter = new Vector3
            {
                x = (xMin + xMax) / 2f,
                y = (yMin + yMax) / 2f,
                z = (zMin + zMax) / 2f
            };

            string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            Logger.Log("--- " + label + " ---");
            Logger.Log("Y-range: [" + F(yMin) + "-" + F(yMax) + "], X-range: [" + F(xMin) + "-" + F(xMax) + "], Z-range: [" + F(zMin) + "-" + F(zMax) + "]");
            Logger.Log("Center(AABB): (x=" + F(aabbCenter.x) + ", y=" + F(aabbCenter.y) + ", z=" + F(aabbCenter.z) + ")");
            Logger.Log("Center(AVG):  (x=" + F(avgCenter.x) + ", y=" + F(avgCenter.y) + ", z=" + F(avgCenter.z) + ")");

            foreach (var p in box)
                Logger.Log("(x=" + F(p.x) + ", y=" + F(p.y) + ", z=" + F(p.z) + ")");

            Logger.Log("--- End of " + label + " ---\n");
        }
    }
}
