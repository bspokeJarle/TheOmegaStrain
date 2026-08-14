using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroMesh.Engine
{
    public static class ObjectPlacementMath
    {
        public static (float x, float y, float z) GetCrashBoxCenter(IReadOnlyList<List<IVector3>>? crashBoxes)
        {
            if (crashBoxes == null || crashBoxes.Count == 0)
                return (0f, 0f, 0f);

            float sumX = 0f;
            float sumY = 0f;
            float sumZ = 0f;
            int count = 0;

            foreach (var box in crashBoxes)
            {
                if (box == null)
                    continue;

                foreach (var point in box)
                {
                    if (point == null)
                        continue;

                    sumX += point.x;
                    sumY += point.y;
                    sumZ += point.z;
                    count++;
                }
            }

            return count == 0
                ? (0f, 0f, 0f)
                : (sumX / count, sumY / count, sumZ / count);
        }

        public static bool TryGetRenderPosition(
            IRenderable3dObject? obj,
            IVector3? localWorldPosition,
            IVector3? surfaceAnchor,
            int screenCenterX,
            int screenCenterY,
            Func<float, float, float, IVector3> vectorFactory,
            out double x,
            out double y,
            out double z)
        {
            x = y = z = 0;
            if (obj == null)
                return false;

            var objectOffsets = obj.ObjectOffsets;
            if (objectOffsets == null)
                return false;

            if (localWorldPosition == null)
            {
                if (surfaceAnchor != null)
                {
                    CenterObjectAt(obj, surfaceAnchor, vectorFactory);
                    CenterCrashBoxesAt(obj, surfaceAnchor, vectorFactory);
                }

                x = screenCenterX + objectOffsets.x;
                y = screenCenterY + objectOffsets.y;
                z = objectOffsets.z;
                return true;
            }

            obj.CalculatedCrashOffset = vectorFactory(
                -localWorldPosition.x + objectOffsets.x,
                -localWorldPosition.y + objectOffsets.y,
                localWorldPosition.z + objectOffsets.z);

            x = screenCenterX - localWorldPosition.x + objectOffsets.x;
            y = screenCenterY - localWorldPosition.y + objectOffsets.y;
            z = localWorldPosition.z + objectOffsets.z;
            return true;
        }

        public static void CenterObjectAt(
            IRenderable3dObject? obj,
            IVector3? targetPosition,
            Func<float, float, float, IVector3> vectorFactory)
        {
            if (obj == null || targetPosition == null)
                return;

            IVector3 objectCenter = obj.UseSurfaceFootprintPivot
                ? vectorFactory(0f, 0f, 0f)
                : GetObjectGeometricCenter(obj, snapToBottomY: true, vectorFactory);

            float shiftX = targetPosition.x - objectCenter.x;
            float shiftY = targetPosition.y - objectCenter.y;
            float shiftZ = targetPosition.z - objectCenter.z;

            TranslateObjectVertices(obj, shiftX, shiftY, shiftZ);
        }

        public static void CenterCrashBoxesAt(
            IRenderable3dObject? obj,
            IVector3? targetPosition,
            Func<float, float, float, IVector3> vectorFactory)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0 || targetPosition == null)
                return;

            int count = 0;
            float sumX = 0f;
            float sumZ = 0f;
            float minY = float.MaxValue;

            foreach (var box in obj.CrashBoxes)
            {
                if (box == null)
                    continue;

                foreach (var point in box)
                {
                    if (point == null)
                        continue;

                    sumX += point.x;
                    sumZ += point.z;
                    minY = Math.Min(minY, point.y);
                    count++;
                }
            }

            if (count == 0)
                return;

            var center = obj.UseSurfaceFootprintPivot
                ? vectorFactory(0f, 0f, 0f)
                : vectorFactory(sumX / count, minY, sumZ / count);

            float shiftX = targetPosition.x - center.x;
            float shiftY = targetPosition.y - center.y;
            float shiftZ = targetPosition.z - center.z;

            TranslateCrashBoxes(obj.CrashBoxes, shiftX, shiftY, shiftZ, vectorFactory);
        }

        public static IVector3 GetObjectGeometricCenter(
            IRenderable3dObject? obj,
            bool snapToBottomY,
            Func<float, float, float, IVector3> vectorFactory)
        {
            if (obj == null)
                return vectorFactory(0f, 0f, 0f);

            float sumX = 0f;
            float sumY = 0f;
            float sumZ = 0f;
            int count = 0;
            float minY = float.MaxValue;

            foreach (var part in obj.ObjectParts)
            {
                if (part?.Triangles == null)
                    continue;

                foreach (var triangle in part.Triangles)
                {
                    AddVertex(triangle.vert1);
                    AddVertex(triangle.vert2);
                    AddVertex(triangle.vert3);
                }
            }

            if (count == 0)
                return vectorFactory(0f, 0f, 0f);

            if (snapToBottomY &&
                TryGetBottomGeometricCenter(obj, minY, vectorFactory, out var bottomCenter))
            {
                return bottomCenter;
            }

            return vectorFactory(sumX / count, sumY / count, sumZ / count);

            void AddVertex(IVector3? vertex)
            {
                if (vertex == null)
                    return;

                sumX += vertex.x;
                sumY += vertex.y;
                sumZ += vertex.z;
                count++;

                if (snapToBottomY)
                    minY = Math.Min(minY, vertex.y);
            }
        }

        public static void CenterCrashBoxAt<TVector>(
            IList<TVector>? crashBox,
            IVector3? targetPosition,
            IVector3 crashboxOffsets,
            Func<float, float, float, TVector> vectorFactory,
            out float shiftY)
            where TVector : IVector3
        {
            shiftY = 0f;
            if (crashBox == null || crashBox.Count == 0 || targetPosition == null)
                return;

            float minY = crashBox.Min(p => p.y);
            shiftY = targetPosition.y - minY + crashboxOffsets.y;

            for (int i = 0; i < crashBox.Count; i++)
            {
                crashBox[i] = vectorFactory(
                    crashBox[i].x,
                    crashBox[i].y + shiftY,
                    crashBox[i].z);
            }
        }

        private static bool TryGetBottomGeometricCenter(
            IRenderable3dObject obj,
            float minY,
            Func<float, float, float, IVector3> vectorFactory,
            out IVector3 center)
        {
            float bottomSumX = 0f;
            float bottomSumY = 0f;
            float bottomSumZ = 0f;
            int bottomCount = 0;
            const float bottomTolerance = 0.001f;

            foreach (var part in obj.ObjectParts)
            {
                if (part?.Triangles == null)
                    continue;

                foreach (var triangle in part.Triangles)
                {
                    AddBottomVertex(triangle.vert1);
                    AddBottomVertex(triangle.vert2);
                    AddBottomVertex(triangle.vert3);
                }
            }

            if (bottomCount > 0)
            {
                center = vectorFactory(
                    bottomSumX / bottomCount,
                    bottomSumY / bottomCount,
                    bottomSumZ / bottomCount);
                return true;
            }

            center = vectorFactory(0f, 0f, 0f);
            return false;

            void AddBottomVertex(IVector3? vertex)
            {
                if (vertex == null || Math.Abs(vertex.y - minY) > bottomTolerance)
                    return;

                bottomSumX += vertex.x;
                bottomSumY += vertex.y;
                bottomSumZ += vertex.z;
                bottomCount++;
            }
        }

        private static void TranslateObjectVertices(
            IRenderable3dObject obj,
            float shiftX,
            float shiftY,
            float shiftZ)
        {
            foreach (var part in obj.ObjectParts)
            {
                if (part?.Triangles == null)
                    continue;

                foreach (var triangle in part.Triangles)
                {
                    TranslateVertex(triangle.vert1, shiftX, shiftY, shiftZ);
                    TranslateVertex(triangle.vert2, shiftX, shiftY, shiftZ);
                    TranslateVertex(triangle.vert3, shiftX, shiftY, shiftZ);
                }
            }
        }

        private static void TranslateCrashBoxes(
            IReadOnlyList<List<IVector3>> crashBoxes,
            float shiftX,
            float shiftY,
            float shiftZ,
            Func<float, float, float, IVector3> vectorFactory)
        {
            for (int i = 0; i < crashBoxes.Count; i++)
            {
                var crashBox = crashBoxes[i];
                for (int j = 0; j < crashBox.Count; j++)
                {
                    var p = crashBox[j];
                    crashBox[j] = vectorFactory(
                        p.x + shiftX,
                        p.y + shiftY,
                        p.z + shiftZ);
                }
            }
        }

        private static void TranslateVertex(IVector3? vertex, float shiftX, float shiftY, float shiftZ)
        {
            if (vertex == null)
                return;

            vertex.x += shiftX;
            vertex.y += shiftY;
            vertex.z += shiftZ;
        }
    }
}
