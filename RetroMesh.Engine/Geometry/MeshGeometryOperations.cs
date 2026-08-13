using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain
{
    public static class MeshGeometryOperations
    {
        public const string ShadowColorHex = "000000";

        public static void ApplyScaleToTriangles(IReadOnlyList<ITriangleMeshWithColor>? triangles, float scale)
        {
            if (triangles == null || triangles.Count == 0)
                return;

            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                ScaleVertex(tri.vert1, scale);
                ScaleVertex(tri.vert2, scale);
                ScaleVertex(tri.vert3, scale);
            }
        }

        public static void ApplyScaleToObject(
            IRenderable3dObject? actualObject,
            float scale,
            Func<IVector3, IVector3> vectorFactory)
        {
            if (actualObject == null || actualObject.ObjectParts.Count == 0)
                return;

            var scaled = new HashSet<IVector3>(ReferenceEqualityComparer.Instance);

            foreach (var part in actualObject.ObjectParts)
            {
                if (part.Triangles == null || part.Triangles.Count == 0)
                    continue;

                foreach (var tri in part.Triangles)
                {
                    ScaleVertexOnce(scaled, tri.vert1, scale);
                    ScaleVertexOnce(scaled, tri.vert2, scale);
                    ScaleVertexOnce(scaled, tri.vert3, scale);
                }
            }

            foreach (var crashBox in actualObject.CrashBoxes)
            {
                for (int i = 0; i < crashBox.Count; i++)
                    crashBox[i] = vectorFactory(new EngineVector3(
                        crashBox[i].x * scale,
                        crashBox[i].y * scale,
                        crashBox[i].z * scale));
            }
        }

        public static void NormalizeSurfaceFootprintPivot(
            IRenderable3dObject? actualObject,
            Func<IVector3, IVector3> vectorFactory)
        {
            if (actualObject == null || actualObject.ObjectParts == null || actualObject.ObjectParts.Count == 0)
                return;

            var referenceVerts = new List<EngineVector3>(128);
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (var part in actualObject.ObjectParts)
            {
                if (!part.IsVisible || part.PartName == "Shadow" || part.Triangles == null)
                    continue;

                foreach (var tri in part.Triangles)
                {
                    AddPivotReferenceVertex(referenceVerts, tri.vert1, ref minZ, ref maxZ);
                    AddPivotReferenceVertex(referenceVerts, tri.vert2, ref minZ, ref maxZ);
                    AddPivotReferenceVertex(referenceVerts, tri.vert3, ref minZ, ref maxZ);
                }
            }

            if (referenceVerts.Count == 0)
                return;

            float height = Math.Max(0f, maxZ - minZ);
            float bottomBand = Math.Max(0.001f, height * 0.04f);
            float sumX = 0f;
            float sumY = 0f;
            int count = 0;

            foreach (var vertex in referenceVerts)
            {
                if (vertex.z > minZ + bottomBand)
                    continue;

                sumX += vertex.x;
                sumY += vertex.y;
                count++;
            }

            if (count == 0)
                return;

            var pivot = new EngineVector3(sumX / count, sumY / count, minZ);
            TranslateObject(actualObject, -pivot.x, -pivot.y, -pivot.z, vectorFactory);
            actualObject.UseSurfaceFootprintPivot = true;
        }

        public static void AddSimplifiedShadowPart(
            IRenderable3dObject? actualObject,
            Func<I3dObjectPart> objectPartFactory,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<float, float, float, IVector3> vectorFactory,
            bool useFlatQuad = false,
            int layers = 2)
        {
            if (actualObject == null || actualObject.ObjectParts == null || actualObject.ObjectParts.Count == 0)
                return;

            for (int i = 0; i < actualObject.ObjectParts.Count; i++)
            {
                if (actualObject.ObjectParts[i].PartName == "Shadow")
                    return;
            }

            var verts = new List<EngineVector3>(256);
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (var part in actualObject.ObjectParts)
            {
                if (!part.IsVisible || part.Triangles == null || part.Triangles.Count == 0)
                    continue;

                for (int t = 0; t < part.Triangles.Count; t++)
                {
                    var tri = part.Triangles[t];
                    AddVert(verts, tri.vert1, ref minZ, ref maxZ);
                    AddVert(verts, tri.vert2, ref minZ, ref maxZ);
                    AddVert(verts, tri.vert3, ref minZ, ref maxZ);
                }
            }

            if (verts.Count < 3)
                return;

            if (useFlatQuad)
            {
                var hull = SimplifyHullEvenly(ConvexHullXY(verts), maxVerts: 16);
                if (hull.Count < 3)
                    return;

                AddShadowPart(
                    actualObject,
                    objectPartFactory,
                    FanTriangulateXY(hull, z: 0f, ShadowColorHex, triangleFactory, vectorFactory));
                return;
            }

            int layerCount = Math.Max(2, layers);
            float zRange = maxZ - minZ;
            if (zRange <= 1e-4f)
            {
                var flatHull = SimplifyHullEvenly(ConvexHullXY(verts), maxVerts: 16);
                if (flatHull.Count < 3)
                    return;

                AddShadowPart(
                    actualObject,
                    objectPartFactory,
                    FanTriangulateXY(flatHull, z: minZ, ShadowColorHex, triangleFactory, vectorFactory));
                return;
            }

            float bandH = zRange / layerCount;
            float overlap = bandH * 0.20f;
            var buckets = new List<EngineVector3>[layerCount];
            for (int i = 0; i < layerCount; i++)
                buckets[i] = new List<EngineVector3>(32);

            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                for (int b = 0; b < layerCount; b++)
                {
                    float zLo = minZ + b * bandH - overlap;
                    float zHi = minZ + (b + 1) * bandH + overlap;
                    if (v.z >= zLo && v.z <= zHi)
                        buckets[b].Add(v);
                }
            }

            var rings = new List<(float x, float y)>?[layerCount];
            for (int b = 0; b < layerCount; b++)
            {
                if (buckets[b].Count >= 3)
                    rings[b] = SimplifyHullEvenly(ConvexHullXY(buckets[b]), maxVerts: 12);
            }

            for (int b = 1; b < layerCount; b++)
            {
                if (rings[b] == null || rings[b]!.Count < 3)
                    rings[b] = rings[b - 1];
            }

            for (int b = layerCount - 2; b >= 0; b--)
            {
                if (rings[b] == null || rings[b]!.Count < 3)
                    rings[b] = rings[b + 1];
            }

            if (rings[0] == null || rings[0]!.Count < 3)
                return;

            int sideCount = 10;
            for (int b = 0; b < layerCount; b++)
                sideCount = Math.Min(sideCount, Math.Max(3, rings[b]!.Count));

            var resampled = new List<(float x, float y)>[layerCount];
            for (int b = 0; b < layerCount; b++)
                resampled[b] = ResampleHullByAngle(rings[b]!, sideCount);

            var ringZ = new float[layerCount];
            for (int b = 0; b < layerCount; b++)
                ringZ[b] = minZ + (b + 0.5f) * bandH;

            ringZ[0] = minZ;
            ringZ[layerCount - 1] = maxZ;

            var tris = new List<ITriangleMeshWithColor>(sideCount * 2 * (layerCount - 1) + sideCount * 2);

            tris.AddRange(FanTriangulateXY(resampled[0], z: ringZ[0], ShadowColorHex, triangleFactory, vectorFactory));
            tris.AddRange(FanTriangulateXY(resampled[layerCount - 1], z: ringZ[layerCount - 1], ShadowColorHex, triangleFactory, vectorFactory));

            for (int b = 0; b < layerCount - 1; b++)
            {
                var lower = resampled[b];
                var upper = resampled[b + 1];
                float zLo = ringZ[b];
                float zHi = ringZ[b + 1];
                for (int i = 0; i < sideCount; i++)
                {
                    int next = (i + 1) % sideCount;
                    var bl = vectorFactory(lower[i].x, lower[i].y, zLo);
                    var br = vectorFactory(lower[next].x, lower[next].y, zLo);
                    var tr = vectorFactory(upper[next].x, upper[next].y, zHi);
                    var tl = vectorFactory(upper[i].x, upper[i].y, zHi);
                    tris.Add(CreateTriangle(bl, br, tr, ShadowColorHex, triangleFactory, noHidden: true));
                    tris.Add(CreateTriangle(bl, tr, tl, ShadowColorHex, triangleFactory, noHidden: true));
                }
            }

            AddShadowPart(actualObject, objectPartFactory, tris);
        }

        public static void AddCustomShadowPart(
            IRenderable3dObject? actualObject,
            IReadOnlyList<ITriangleMeshWithColor>? triangles,
            Func<I3dObjectPart> objectPartFactory)
        {
            if (actualObject == null || actualObject.ObjectParts == null || triangles == null || triangles.Count == 0)
                return;

            for (int i = 0; i < actualObject.ObjectParts.Count; i++)
            {
                if (actualObject.ObjectParts[i].PartName == "Shadow")
                    return;
            }

            AddShadowPart(actualObject, objectPartFactory, triangles.ToList());
        }

        public static List<TVector> GenerateAabbCrashBoxFromRotated<TVector>(
            IReadOnlyList<IVector3>? rotatedPoints,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            if (rotatedPoints == null || rotatedPoints.Count < 2)
                return new List<TVector>();

            var min = new EngineVector3(
                rotatedPoints.Min(p => p.x),
                rotatedPoints.Min(p => p.y),
                rotatedPoints.Min(p => p.z));

            var max = new EngineVector3(
                rotatedPoints.Max(p => p.x),
                rotatedPoints.Max(p => p.y),
                rotatedPoints.Max(p => p.z));

            return GenerateCrashBoxCorners(min, max, vectorFactory);
        }

        public static List<TVector> GenerateCrashBoxCorners<TVector>(
            IVector3 min,
            IVector3 max,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            return new List<TVector>
            {
                vectorFactory(min.x, max.y, min.z),
                vectorFactory(max.x, max.y, min.z),
                vectorFactory(max.x, min.y, min.z),
                vectorFactory(min.x, min.y, min.z),
                vectorFactory(min.x, max.y, max.z),
                vectorFactory(max.x, max.y, max.z),
                vectorFactory(max.x, min.y, max.z),
                vectorFactory(min.x, min.y, max.z)
            };
        }

        public static bool CheckAabbOverlap(
            IReadOnlyList<IVector3> boxA,
            IReadOnlyList<IVector3> boxB,
            float marginX,
            float marginY,
            float marginZ,
            out AabbBounds boundsA,
            out AabbBounds boundsB)
        {
            boundsA = AabbBounds.FromPoints(boxA);
            boundsB = AabbBounds.FromPoints(boxB);

            if (boxA.Count == 0 || boxB.Count == 0)
                return false;

            bool overlapX = (boundsA.MaxX + marginX) >= (boundsB.MinX - marginX)
                && (boundsA.MinX - marginX) <= (boundsB.MaxX + marginX);
            bool overlapY = (boundsA.MaxY + marginY) >= (boundsB.MinY - marginY)
                && (boundsA.MinY - marginY) <= (boundsB.MaxY + marginY);
            bool overlapZ = (boundsA.MaxZ + marginZ) >= (boundsB.MinZ - marginZ)
                && (boundsA.MinZ - marginZ) <= (boundsB.MaxZ + marginZ);

            return overlapX && overlapY && overlapZ;
        }

        public static List<ITriangleMeshWithColor> ConvertToTrianglesWithColor(
            IReadOnlyList<ITriangleMesh> triangles,
            string color,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<IVector3, IVector3> vectorFactory)
        {
            var trianglesWithColor = new List<ITriangleMeshWithColor>(triangles.Count);
            foreach (var triangle in triangles)
            {
                var copy = triangleFactory();
                copy.vert1 = vectorFactory(triangle.vert1);
                copy.vert2 = vectorFactory(triangle.vert2);
                copy.vert3 = vectorFactory(triangle.vert3);
                copy.normal1 = vectorFactory(triangle.normal1);
                copy.normal2 = vectorFactory(triangle.normal2);
                copy.normal3 = vectorFactory(triangle.normal3);
                copy.angle = triangle.angle;
                copy.landBasedPosition = triangle.landBasedPosition;
                copy.noHidden = triangle.noHidden;
                copy.Color = color;
                trianglesWithColor.Add(copy);
            }

            return trianglesWithColor;
        }

        public static void AddQuadOutward<TTriangle>(
            IList<ITriangleMeshWithColor> tris,
            IVector3 v1,
            IVector3 v2,
            IVector3 v3,
            IVector3 v4,
            IVector3 center,
            string color,
            Func<TTriangle> triangleFactory,
            bool noHidden = false)
            where TTriangle : ITriangleMeshWithColor
        {
            tris.Add(CreateTriangleOutward(v1, v2, v3, center, color, triangleFactory, noHidden));
            tris.Add(CreateTriangleOutward(v1, v3, v4, center, color, triangleFactory, noHidden));
        }

        public static TTriangle CreateTriangleOutward<TTriangle>(
            IVector3 v1,
            IVector3 v2,
            IVector3 v3,
            IVector3 center,
            string color,
            Func<TTriangle> triangleFactory,
            bool noHidden = false)
            where TTriangle : ITriangleMeshWithColor
        {
            var edge1 = Subtract(v2, v1);
            var edge2 = Subtract(v3, v1);
            var normal = Normalize(Cross(edge1, edge2));

            var mid = new EngineVector3(
                (v1.x + v2.x + v3.x) / 3f,
                (v1.y + v2.y + v3.y) / 3f,
                (v1.z + v2.z + v3.z) / 3f);

            var desired = Normalize(Subtract(mid, center));
            float dot = Dot(normal, desired);

            if (dot < 0f)
                (v2, v3) = (v3, v2);

            return CreateTriangle(v1, v2, v3, color, triangleFactory, noHidden);
        }

        public static EngineVector3 Subtract(IVector3 a, IVector3 b)
            => new(a.x - b.x, a.y - b.y, a.z - b.z);

        public static EngineVector3 Add(IVector3 a, IVector3 b)
            => new(a.x + b.x, a.y + b.y, a.z + b.z);

        public static EngineVector3 Scale(IVector3 v, float s)
            => new(v.x * s, v.y * s, v.z * s);

        public static EngineVector3 Cross(IVector3 a, IVector3 b)
            => new(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);

        public static float Dot(IVector3 a, IVector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

        public static EngineVector3 Normalize(IVector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;
            if (lenSq <= 1e-6f)
                return new EngineVector3();

            float invLen = 1.0f / MathF.Sqrt(lenSq);
            return new EngineVector3(v.x * invLen, v.y * invLen, v.z * invLen);
        }

        private static void ScaleVertex(IVector3 vertex, float scale)
        {
            vertex.x *= scale;
            vertex.y *= scale;
            vertex.z *= scale;
        }

        private static void ScaleVertexOnce(HashSet<IVector3> scaled, IVector3 vertex, float scale)
        {
            if (vertex != null && scaled.Add(vertex))
                ScaleVertex(vertex, scale);
        }

        private static void AddPivotReferenceVertex(List<EngineVector3> sink, IVector3 vertex, ref float minZ, ref float maxZ)
        {
            if (vertex == null)
                return;

            var v = new EngineVector3(vertex.x, vertex.y, vertex.z);
            sink.Add(v);
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        private static void TranslateObject(
            IRenderable3dObject actualObject,
            float shiftX,
            float shiftY,
            float shiftZ,
            Func<IVector3, IVector3> vectorFactory)
        {
            var translated = new HashSet<IVector3>(ReferenceEqualityComparer.Instance);

            foreach (var part in actualObject.ObjectParts)
            {
                if (part.Triangles == null)
                    continue;

                float partShiftZ = part.PartName == "Shadow" ? 0f : shiftZ;

                foreach (var tri in part.Triangles)
                {
                    TranslateVertexOnce(translated, tri.vert1, shiftX, shiftY, partShiftZ);
                    TranslateVertexOnce(translated, tri.vert2, shiftX, shiftY, partShiftZ);
                    TranslateVertexOnce(translated, tri.vert3, shiftX, shiftY, partShiftZ);
                }
            }

            if (actualObject.CrashBoxes == null)
                return;

            foreach (var crashBox in actualObject.CrashBoxes)
            {
                for (int i = 0; i < crashBox.Count; i++)
                    crashBox[i] = vectorFactory(new EngineVector3(
                        crashBox[i].x + shiftX,
                        crashBox[i].y + shiftY,
                        crashBox[i].z + shiftZ));
            }
        }

        private static void TranslateVertexOnce(
            HashSet<IVector3> translated,
            IVector3 vertex,
            float shiftX,
            float shiftY,
            float shiftZ)
        {
            if (vertex == null || !translated.Add(vertex))
                return;

            vertex.x += shiftX;
            vertex.y += shiftY;
            vertex.z += shiftZ;
        }

        private static void AddShadowPart(
            IRenderable3dObject obj,
            Func<I3dObjectPart> objectPartFactory,
            List<ITriangleMeshWithColor> tris)
        {
            NormalizeShadowGroundPlane(tris);
            var part = objectPartFactory();
            part.PartName = "Shadow";
            part.Triangles = tris;
            part.IsVisible = false;
            obj.ObjectParts.Add(part);
        }

        private static void NormalizeShadowGroundPlane(List<ITriangleMeshWithColor> triangles)
        {
            float minZ = float.MaxValue;
            foreach (var triangle in triangles)
            {
                minZ = Math.Min(minZ, triangle.vert1.z);
                minZ = Math.Min(minZ, triangle.vert2.z);
                minZ = Math.Min(minZ, triangle.vert3.z);
            }

            if (minZ == float.MaxValue || Math.Abs(minZ) <= 0.0001f)
                return;

            var normalized = new HashSet<IVector3>(ReferenceEqualityComparer.Instance);
            foreach (var triangle in triangles)
            {
                NormalizeShadowVertexOnce(normalized, triangle.vert1, minZ);
                NormalizeShadowVertexOnce(normalized, triangle.vert2, minZ);
                NormalizeShadowVertexOnce(normalized, triangle.vert3, minZ);
            }
        }

        private static void NormalizeShadowVertexOnce(HashSet<IVector3> normalized, IVector3 vertex, float minZ)
        {
            if (vertex != null && normalized.Add(vertex))
                vertex.z -= minZ;
        }

        private static void AddVert(List<EngineVector3> sink, IVector3 v, ref float minZ, ref float maxZ)
        {
            sink.Add(new EngineVector3(v.x, v.y, v.z));
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        private static List<(float x, float y)> ConvexHullXY(List<EngineVector3> pts)
        {
            int n = pts.Count;
            if (n < 3)
                return new List<(float, float)>();

            var arr = new (float x, float y)[n];
            for (int i = 0; i < n; i++)
                arr[i] = (pts[i].x, pts[i].y);

            Array.Sort(arr, (a, b) =>
            {
                int c = a.x.CompareTo(b.x);
                return c != 0 ? c : a.y.CompareTo(b.y);
            });

            var hull = new (float x, float y)[2 * n];
            int k = 0;

            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Cross2D(hull[k - 2], hull[k - 1], arr[i]) <= 0)
                    k--;
                hull[k++] = arr[i];
            }

            int t = k + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (k >= t && Cross2D(hull[k - 2], hull[k - 1], arr[i]) <= 0)
                    k--;
                hull[k++] = arr[i];
            }

            var result = new List<(float, float)>(k - 1);
            for (int i = 0; i < k - 1; i++)
                result.Add(hull[i]);
            return result;
        }

        private static float Cross2D((float x, float y) o, (float x, float y) a, (float x, float y) b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static List<(float x, float y)> SimplifyHullEvenly(List<(float x, float y)> hull, int maxVerts)
        {
            if (hull.Count <= maxVerts)
                return hull;

            var simplified = new List<(float, float)>(maxVerts);
            float step = (float)hull.Count / maxVerts;
            for (int i = 0; i < maxVerts; i++)
            {
                int idx = (int)(i * step);
                if (idx >= hull.Count)
                    idx = hull.Count - 1;
                simplified.Add(hull[idx]);
            }

            return simplified;
        }

        private static List<(float x, float y)> ResampleHullByAngle(List<(float x, float y)> hull, int count)
        {
            if (hull.Count == 0 || count <= 0)
                return hull;

            if (hull.Count == count)
                return hull;

            var edgeLen = new float[hull.Count];
            float perimeter = 0f;
            for (int i = 0; i < hull.Count; i++)
            {
                int n = (i + 1) % hull.Count;
                float dx = hull[n].x - hull[i].x;
                float dy = hull[n].y - hull[i].y;
                edgeLen[i] = MathF.Sqrt(dx * dx + dy * dy);
                perimeter += edgeLen[i];
            }

            if (perimeter <= 1e-6f)
                return hull;

            float step = perimeter / count;
            var result = new List<(float, float)>(count);

            int edge = 0;
            float edgeStart = 0f;

            for (int i = 0; i < count; i++)
            {
                float target = i * step;
                while (edge < hull.Count && edgeStart + edgeLen[edge] < target)
                {
                    edgeStart += edgeLen[edge];
                    edge++;
                }

                if (edge >= hull.Count)
                {
                    result.Add(hull[hull.Count - 1]);
                    continue;
                }

                float localT = edgeLen[edge] > 1e-6f ? (target - edgeStart) / edgeLen[edge] : 0f;
                int next = (edge + 1) % hull.Count;
                float x = hull[edge].x + (hull[next].x - hull[edge].x) * localT;
                float y = hull[edge].y + (hull[next].y - hull[edge].y) * localT;
                result.Add((x, y));
            }

            return result;
        }

        private static List<ITriangleMeshWithColor> FanTriangulateXY(
            List<(float x, float y)> hull,
            float z,
            string color,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<float, float, float, IVector3> vectorFactory)
        {
            var tris = new List<ITriangleMeshWithColor>(hull.Count);
            float cx = 0f;
            float cy = 0f;
            for (int i = 0; i < hull.Count; i++)
            {
                cx += hull[i].x;
                cy += hull[i].y;
            }

            cx /= hull.Count;
            cy /= hull.Count;

            var center = vectorFactory(cx, cy, z);
            for (int i = 0; i < hull.Count; i++)
            {
                int next = (i + 1) % hull.Count;
                var a = vectorFactory(hull[i].x, hull[i].y, z);
                var b = vectorFactory(hull[next].x, hull[next].y, z);
                tris.Add(CreateTriangle(center, a, b, color, triangleFactory, noHidden: true));
            }

            return tris;
        }

        private static TTriangle CreateTriangle<TTriangle>(
            IVector3 a,
            IVector3 b,
            IVector3 c,
            string color,
            Func<TTriangle> triangleFactory,
            bool noHidden)
            where TTriangle : ITriangleMeshWithColor
        {
            var triangle = triangleFactory();
            triangle.Color = color;
            triangle.vert1 = a;
            triangle.vert2 = b;
            triangle.vert3 = c;
            triangle.noHidden = noHidden;
            return triangle;
        }
    }
}
