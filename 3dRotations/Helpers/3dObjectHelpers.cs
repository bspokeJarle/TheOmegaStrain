using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Helpers
{
    public static class _3dObjectHelpers
    {
        public static bool _localLoggingEnabled = false;
        public static void ApplyScaleToTriangles(List<ITriangleMeshWithColor> triangles, float scale)
        {
            if (triangles == null || triangles.Count == 0) return;

            foreach (var tri in triangles)
            {
                // Assumes that vert1/vert2/vert3 are IVector3 with settable x/y/z
                tri.vert1.x *= scale;
                tri.vert1.y *= scale;
                tri.vert1.z *= scale;

                tri.vert2.x *= scale;
                tri.vert2.y *= scale;
                tri.vert2.z *= scale;

                tri.vert3.x *= scale;
                tri.vert3.y *= scale;
                tri.vert3.z *= scale;
            }
        }
        public static void ApplyScaleToObject(I3dObject actualObject, float scale)
        {
            if (actualObject == null || actualObject.ObjectParts.Count == 0) return;

            // Track already-scaled vertices to avoid scaling shared Vector3 instances multiple times
            var scaled = new HashSet<IVector3>(ReferenceEqualityComparer.Instance);

            foreach (var part in actualObject.ObjectParts)
            {
                if (part.Triangles == null || part.Triangles.Count == 0) continue;

                foreach (var tri in part.Triangles)
                {
                    if (scaled.Add(tri.vert1)) { tri.vert1.x *= scale; tri.vert1.y *= scale; tri.vert1.z *= scale; }
                    if (scaled.Add(tri.vert2)) { tri.vert2.x *= scale; tri.vert2.y *= scale; tri.vert2.z *= scale; }
                    if (scaled.Add(tri.vert3)) { tri.vert3.x *= scale; tri.vert3.y *= scale; tri.vert3.z *= scale; }
                }
            }
            foreach (var crashBox in actualObject.CrashBoxes)
            {
                for (int i = 0; i < crashBox.Count; i++)
                {
                    crashBox[i] = new Vector3
                    {
                        x = crashBox[i].x * scale,
                        y = crashBox[i].y * scale,
                        z = crashBox[i].z * scale
                    };
                }
            }
        }

        public static void NormalizeSurfaceFootprintPivot(I3dObject actualObject)
        {
            if (actualObject == null || actualObject.ObjectParts == null || actualObject.ObjectParts.Count == 0)
                return;

            var referenceVerts = new List<Vector3>(128);
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

            var pivot = new Vector3
            {
                x = sumX / count,
                y = sumY / count,
                z = minZ
            };

            TranslateObject(actualObject, -pivot.x, -pivot.y, -pivot.z);
            actualObject.UseSurfaceFootprintPivot = true;
        }

        private static void AddPivotReferenceVertex(List<Vector3> sink, IVector3 vertex, ref float minZ, ref float maxZ)
        {
            if (vertex == null)
                return;

            var v = new Vector3 { x = vertex.x, y = vertex.y, z = vertex.z };
            sink.Add(v);
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        private static void TranslateObject(I3dObject actualObject, float shiftX, float shiftY, float shiftZ)
        {
            var translated = new HashSet<IVector3>(ReferenceEqualityComparer.Instance);

            foreach (var part in actualObject.ObjectParts)
            {
                if (part.Triangles == null)
                    continue;

                // Shadow silhouettes use z = 0 as an explicit ground-plane invariant.
                // They still follow the footprint pivot laterally, but translating their
                // Z together with raised model geometry pushes parts of the shadow below
                // the surface after projection.
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
                {
                    crashBox[i] = new Vector3
                    {
                        x = crashBox[i].x + shiftX,
                        y = crashBox[i].y + shiftY,
                        z = crashBox[i].z + shiftZ
                    };
                }
            }
        }

        private static void TranslateVertexOnce(HashSet<IVector3> translated, IVector3 vertex, float shiftX, float shiftY, float shiftZ)
        {
            if (vertex == null || !translated.Add(vertex))
                return;

            vertex.x += shiftX;
            vertex.y += shiftY;
            vertex.z += shiftZ;
        }

        // ----------------------------------------------------
        //  SIMPLIFIED SHADOW PART
        // ----------------------------------------------------
        // Adds a low-poly "Shadow" part used by ObjectShadowManager instead of
        // projecting the full mesh (hundreds of triangles -> 10..30).
        // Coordinate convention: X lateral, Y depth, Z vertical.
        //
        // The shadow is a real silhouette — the 2D convex hull of the object's
        // top-down (XY) footprint, fan-triangulated. Two modes:
        //
        // - useFlatQuad = true  -> single flat hull at z = 0 (free-flying objects:
        //                          ships, drones, swans, bombers...).
        //                          Triangle count = hull vertex count (<= ~16).
        //
        // - useFlatQuad = false -> N-layer stacked silhouette: the Z range is
        //                          sliced into `layers` horizontal bands, a convex
        //                          hull is built from the verts in each band, and
        //                          adjacent rings are connected by side quads. This
        //                          preserves the object's real vertical profile so
        //                          projected shadows actually look like the object:
        //                            layers = 2  -> tower trunk + head (default)
        //                            layers = 5+ -> tree (trunk -> foliage -> tip),
        //                                           houses with roofs, etc.
        //
        // Call AFTER ApplyScaleToObject so the silhouette matches the final scaled
        // geometry. The part is added with IsVisible = false; only the shadow
        // renderer reads it.
        public static void AddSimplifiedShadowPart(I3dObject actualObject, bool useFlatQuad = false, int layers = 2)
        {
            if (actualObject == null || actualObject.ObjectParts == null || actualObject.ObjectParts.Count == 0)
                return;

            // Idempotent
            for (int i = 0; i < actualObject.ObjectParts.Count; i++)
            {
                if (actualObject.ObjectParts[i].PartName == "Shadow")
                    return;
            }

            // Collect all visible verts. Skip guide / helper parts.
            var verts = new List<Vector3>(256);
            float minZ = float.MaxValue, maxZ = float.MinValue;

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

            if (verts.Count < 3) return;

            const string ShadowColor = "000000";

            if (useFlatQuad)
            {
                // Top-down silhouette in XY, laid flat at z = 0.
                var hull = ConvexHullXY(verts);
                hull = SimplifyHullEvenly(hull, maxVerts: 16);
                if (hull.Count < 3) return;

                var tris = FanTriangulate(hull, z: 0f, ShadowColor);
                AddShadowPart(actualObject, tris);
            }
            else
            {
                // N-layer stacked silhouette. Slice the Z range into `layers`
                // horizontal bands, build a convex hull from the verts in each
                // band, resample all rings to the same vertex count (so side
                // quads connect cleanly), and stitch them together with cap +
                // side triangles. Captures true profiles like tree
                // (narrow trunk -> wide foliage -> narrow tip) or house
                // (rectangular walls -> pitched roof).
                int layerCount = Math.Max(2, layers);
                float zRange = maxZ - minZ;
                if (zRange <= 1e-4f)
                {
                    // Degenerate flat object — fall back to a single flat hull.
                    var flatHull = SimplifyHullEvenly(ConvexHullXY(verts), maxVerts: 16);
                    if (flatHull.Count < 3) return;
                    AddShadowPart(actualObject, FanTriangulate(flatHull, z: minZ, ShadowColor));
                    return;
                }

                // Bucket verts into layer bands. Each band has a small overlap
                // (20% of band height) so rings built from thin geometry
                // (e.g. a single ring of foliage tris at exactly z=H) still
                // capture enough points.
                float bandH = zRange / layerCount;
                float overlap = bandH * 0.20f;
                var buckets = new List<Vector3>[layerCount];
                for (int i = 0; i < layerCount; i++) buckets[i] = new List<Vector3>(32);

                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i];
                    for (int b = 0; b < layerCount; b++)
                    {
                        float zLo = minZ + b * bandH - overlap;
                        float zHi = minZ + (b + 1) * bandH + overlap;
                        if (v.z >= zLo && v.z <= zHi) buckets[b].Add(v);
                    }
                }

                // Build a hull per band. Empty / degenerate bands inherit the
                // nearest non-empty neighbour so the silhouette stays closed.
                var rings = new List<(float x, float y)>[layerCount];
                for (int b = 0; b < layerCount; b++)
                {
                    if (buckets[b].Count >= 3)
                        rings[b] = SimplifyHullEvenly(ConvexHullXY(buckets[b]), maxVerts: 12);
                    else
                        rings[b] = null;
                }
                // Forward fill, then backward fill, so no null rings remain.
                for (int b = 1; b < layerCount; b++)
                    if (rings[b] == null || rings[b].Count < 3) rings[b] = rings[b - 1];
                for (int b = layerCount - 2; b >= 0; b--)
                    if (rings[b] == null || rings[b].Count < 3) rings[b] = rings[b + 1];
                if (rings[0] == null || rings[0].Count < 3) return;

                // Resample every ring to the same vertex count so side quads
                // connect index-aligned points between adjacent levels.
                int sideCount = 10;
                for (int b = 0; b < layerCount; b++) sideCount = Math.Min(sideCount, Math.Max(3, rings[b].Count));
                var resampled = new List<(float x, float y)>[layerCount];
                for (int b = 0; b < layerCount; b++) resampled[b] = ResampleHullByAngle(rings[b], sideCount);

                // Band centre Z values — use the midpoint of each band rather
                // than the band edge so the shadow silhouette reflects where the
                // bulk of each layer actually sits.
                var ringZ = new float[layerCount];
                for (int b = 0; b < layerCount; b++)
                    ringZ[b] = minZ + (b + 0.5f) * bandH;
                // Pin the first/last rings to the real min/max so the shadow
                // base is at z=0 and the tip reaches the true object top.
                ringZ[0] = minZ;
                ringZ[layerCount - 1] = maxZ;

                var tris = new List<ITriangleMeshWithColor>(sideCount * 2 * (layerCount - 1) + sideCount * 2);

                // Bottom cap
                tris.AddRange(FanTriangulateXY(resampled[0], z: ringZ[0], ShadowColor));
                // Top cap
                tris.AddRange(FanTriangulateXY(resampled[layerCount - 1], z: ringZ[layerCount - 1], ShadowColor));

                // Side quads between adjacent rings.
                for (int b = 0; b < layerCount - 1; b++)
                {
                    var lower = resampled[b];
                    var upper = resampled[b + 1];
                    float zLo = ringZ[b];
                    float zHi = ringZ[b + 1];
                    for (int i = 0; i < sideCount; i++)
                    {
                        int next = (i + 1) % sideCount;
                        var bl = new Vector3 { x = lower[i].x, y = lower[i].y, z = zLo };
                        var br = new Vector3 { x = lower[next].x, y = lower[next].y, z = zLo };
                        var tr = new Vector3 { x = upper[next].x, y = upper[next].y, z = zHi };
                        var tl = new Vector3 { x = upper[i].x, y = upper[i].y, z = zHi };
                        tris.Add(MakeShadowTri(bl, br, tr, ShadowColor));
                        tris.Add(MakeShadowTri(bl, tr, tl, ShadowColor));
                    }
                }

                AddShadowPart(actualObject, tris);
            }
        }

        // Caller-supplied shadow geometry. Use this when a handful of hand-
        // placed triangles produce a better silhouette than the auto-generated
        // convex-hull stack (e.g. tree = 5 tris, house = 5 tris). Saves
        // hundreds of triangles per object.
        //
        // Conventions for the supplied triangles:
        //   - Model-space coordinates (same as the object's ObjectParts).
        //   - z = 0 verts stay pinned to the ground; z > 0 verts are projected
        //     along the light direction by ObjectShadowManager.
        //   - Use ShadowColorHex (or any value — renderer overrides per-frame).
        public const string ShadowColorHex = "000000";
        public static void AddCustomShadowPart(I3dObject actualObject, List<ITriangleMeshWithColor> triangles)
        {
            if (actualObject == null || actualObject.ObjectParts == null) return;
            if (triangles == null || triangles.Count == 0) return;

            for (int i = 0; i < actualObject.ObjectParts.Count; i++)
            {
                if (actualObject.ObjectParts[i].PartName == "Shadow") return; // idempotent
            }
            AddShadowPart(actualObject, triangles);
        }

        private static void AddShadowPart(I3dObject obj, List<ITriangleMeshWithColor> tris)
        {
            NormalizeShadowGroundPlane(tris);
            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "Shadow",
                Triangles = tris,
                IsVisible = false
            });
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

        private static void AddVert(List<Vector3> sink, IVector3 v, ref float minZ, ref float maxZ)
        {
            sink.Add(new Vector3 { x = v.x, y = v.y, z = v.z });
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        // Andrew's monotone chain convex hull in XY plane. O(n log n).
        private static List<(float x, float y)> ConvexHullXY(List<Vector3> pts)
        {
            int n = pts.Count;
            if (n < 3) return new List<(float, float)>();

            var arr = new (float x, float y)[n];
            for (int i = 0; i < n; i++) arr[i] = (pts[i].x, pts[i].y);

            Array.Sort(arr, (a, b) =>
            {
                int c = a.x.CompareTo(b.x);
                return c != 0 ? c : a.y.CompareTo(b.y);
            });

            var hull = new (float x, float y)[2 * n];
            int k = 0;

            // Lower hull
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Cross2D(hull[k - 2], hull[k - 1], arr[i]) <= 0) k--;
                hull[k++] = arr[i];
            }
            // Upper hull
            int t = k + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (k >= t && Cross2D(hull[k - 2], hull[k - 1], arr[i]) <= 0) k--;
                hull[k++] = arr[i];
            }

            var result = new List<(float, float)>(k - 1);
            for (int i = 0; i < k - 1; i++) result.Add(hull[i]);
            return result;
        }

        private static float Cross2D((float x, float y) o, (float x, float y) a, (float x, float y) b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        // If the hull has more verts than maxVerts, walk it and keep evenly spaced ones
        // along the perimeter. Cheap and preserves overall shape.
        private static List<(float x, float y)> SimplifyHullEvenly(List<(float x, float y)> hull, int maxVerts)
        {
            if (hull.Count <= maxVerts) return hull;
            var simplified = new List<(float, float)>(maxVerts);
            float step = (float)hull.Count / maxVerts;
            for (int i = 0; i < maxVerts; i++)
            {
                int idx = (int)(i * step);
                if (idx >= hull.Count) idx = hull.Count - 1;
                simplified.Add(hull[idx]);
            }
            return simplified;
        }

        // Resample a closed polygon into exactly `count` points spaced evenly
        // along its perimeter. Preserves the original corners because, when a
        // sample lands on an edge, we interpolate between the two endpoints.
        // Used to give the lower/upper tower hulls matching index-aligned rings
        // so side quads connect cleanly.
        private static List<(float x, float y)> ResampleHullByAngle(List<(float x, float y)> hull, int count)
        {
            if (hull.Count == 0 || count <= 0) return hull;
            if (hull.Count == count) return hull;

            // Edge lengths + total perimeter
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
            if (perimeter <= 1e-6f) return hull;

            float step = perimeter / count;
            var result = new List<(float, float)>(count);

            int edge = 0;
            float edgeStart = 0f; // cumulative distance at start of current edge

            for (int i = 0; i < count; i++)
            {
                float target = i * step;
                // Advance to the edge containing `target`
                while (edge < hull.Count && edgeStart + edgeLen[edge] < target)
                {
                    edgeStart += edgeLen[edge];
                    edge++;
                }
                if (edge >= hull.Count) { result.Add(hull[hull.Count - 1]); continue; }

                float localT = edgeLen[edge] > 1e-6f ? (target - edgeStart) / edgeLen[edge] : 0f;
                int next = (edge + 1) % hull.Count;
                float x = hull[edge].x + (hull[next].x - hull[edge].x) * localT;
                float y = hull[edge].y + (hull[next].y - hull[edge].y) * localT;
                result.Add((x, y));
            }
            return result;
        }

        private static float NormalizeAngle(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        private static List<ITriangleMeshWithColor> FanTriangulate(List<(float x, float y)> hull, float z, string color)
        {
            return FanTriangulateXY(hull, z, color);
        }

        private static List<ITriangleMeshWithColor> FanTriangulateXY(List<(float x, float y)> hull, float z, string color)
        {
            var tris = new List<ITriangleMeshWithColor>(hull.Count);
            float cx = 0, cy = 0;
            for (int i = 0; i < hull.Count; i++) { cx += hull[i].x; cy += hull[i].y; }
            cx /= hull.Count; cy /= hull.Count;

            var center = new Vector3 { x = cx, y = cy, z = z };
            for (int i = 0; i < hull.Count; i++)
            {
                int next = (i + 1) % hull.Count;
                var a = new Vector3 { x = hull[i].x, y = hull[i].y, z = z };
                var b = new Vector3 { x = hull[next].x, y = hull[next].y, z = z };
                tris.Add(MakeShadowTri(center, a, b, color));
            }
            return tris;
        }

        private static TriangleMeshWithColor MakeShadowTri(Vector3 a, Vector3 b, Vector3 c, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = a,
                vert2 = b,
                vert3 = c,
                noHidden = true
            };
        }
        public static List<IVector3> GenerateAabbCrashBoxFromRotated(List<IVector3> rotatedPoints)
        {
            if (rotatedPoints == null || rotatedPoints.Count < 2)
                return new List<IVector3>();

            var min = new Vector3
            {
                x = rotatedPoints.Min(p => p.x),
                y = rotatedPoints.Min(p => p.y),
                z = rotatedPoints.Min(p => p.z)
            };

            var max = new Vector3
            {
                x = rotatedPoints.Max(p => p.x),
                y = rotatedPoints.Max(p => p.y),
                z = rotatedPoints.Max(p => p.z)
            };

            return GenerateCrashBoxCorners(min, max);
        }
        public static List<IVector3> GenerateCrashBoxCorners(Vector3 min, Vector3 max)
        {
            return new List<IVector3>
            {
                new Vector3 { x = min.x, y = max.y, z = min.z }, // Corner 0
                new Vector3 { x = max.x, y = max.y, z = min.z }, // Corner 1
                new Vector3 { x = max.x, y = min.y, z = min.z }, // Corner 2
                new Vector3 { x = min.x, y = min.y, z = min.z }, // Corner 3
                new Vector3 { x = min.x, y = max.y, z = max.z }, // Corner 4
                new Vector3 { x = max.x, y = max.y, z = max.z }, // Corner 5
                new Vector3 { x = max.x, y = min.y, z = max.z }, // Corner 6
                new Vector3 { x = min.x, y = min.y, z = max.z }  // Corner 7
            };
        }
 
        public static double GetDistance(Vector3 point1, Vector3 point2)
        {
            float dx = point1.x - point2.x;
            float dy = point1.y - point2.y;
            float dz = point1.z - point2.z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public struct CosSin
        {
            public float CosRes { get; set; }
            public float SinRes { get; set; }
        }

        public static bool CheckCollisionBoxVsBox(
            List<Vector3> boxA,
            List<Vector3> boxB,
            string? nameA = null,
            string? nameB = null
        )
        {
            var minA = new Vector3(boxA.Min(p => p.x), boxA.Min(p => p.y), boxA.Min(p => p.z));
            var maxA = new Vector3(boxA.Max(p => p.x), boxA.Max(p => p.y), boxA.Max(p => p.z));

            var minB = new Vector3(boxB.Min(p => p.x), boxB.Min(p => p.y), boxB.Min(p => p.z));
            var maxB = new Vector3(boxB.Max(p => p.x), boxB.Max(p => p.y), boxB.Max(p => p.z));

            float marginX = -GameSetup.CollisionMarginX;
            float marginY = GameSetup.CollisionMarginY;
            float marginZ = GameSetup.CollisionMarginZ;

            bool overlapX = (maxA.x + marginX) >= (minB.x - marginX) && (minA.x - marginX) <= (maxB.x + marginX);
            bool overlapY = (maxA.y + marginY) >= (minB.y - marginY) && (minA.y - marginY) <= (maxB.y + marginY);
            bool overlapZ = (maxA.z + marginZ) >= (minB.z - marginZ) && (minA.z - marginZ) <= (maxB.z + marginZ);

            if (Logger.ShouldLog(_localLoggingEnabled) && nameA != null && nameB != null)
            {
                Logger.Log(
                    $"AABBCHK {nameA} vs {nameB} | " +
                    $"X:{overlapX} Y:{overlapY} Z:{overlapZ} | " +
                    $"A[min=({minA.x:0.#},{minA.y:0.#},{minA.z:0.#}) max=({maxA.x:0.#},{maxA.y:0.#},{maxA.z:0.#})] " +
                    $"B[min=({minB.x:0.#},{minB.y:0.#},{minB.z:0.#}) max=({maxB.x:0.#},{maxB.y:0.#},{maxB.z:0.#})]"
                );
            }

            return overlapX && overlapY && overlapZ;
        }

        public static List<ITriangleMeshWithColor> ConvertToTrianglesWithColor(List<TriangleMesh> triangles, string color)
        {
            var triangleswithcolor = new List<ITriangleMeshWithColor>();
            foreach (var triangle in triangles)
            {
                triangleswithcolor.Add(new TriangleMeshWithColor
                {
                    vert1 = new Vector3 { x = triangle.vert1.x, y = triangle.vert1.y, z = triangle.vert1.z },
                    vert2 = new Vector3 { x = triangle.vert2.x, y = triangle.vert2.y, z = triangle.vert2.z },
                    vert3 = new Vector3 { x = triangle.vert3.x, y = triangle.vert3.y, z = triangle.vert3.z },
                    normal1 = new Vector3 { x = triangle.normal1.x, y = triangle.normal1.y, z = triangle.normal1.z },
                    normal2 = new Vector3 { x = triangle.normal2.x, y = triangle.normal2.y, z = triangle.normal2.z },
                    normal3 = new Vector3 { x = triangle.normal3.x, y = triangle.normal3.y, z = triangle.normal3.z },
                    angle = triangle.angle,
                    Color = color
                });
            }
            return triangleswithcolor;
        }

        // ----------------------------------------------------
        //  RIGHT-HAND RULE GEOMETRY HELPERS
        // ----------------------------------------------------

        public static void AddQuadOutward(
            List<ITriangleMeshWithColor> tris,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 v4,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            tris.Add(CreateTriangleOutward(v1, v2, v3, center, color, noHidden));
            tris.Add(CreateTriangleOutward(v1, v3, v4, center, color, noHidden));
        }

        public static TriangleMeshWithColor CreateTriangleOutward(
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            var edge1 = Subtract(v2, v1);
            var edge2 = Subtract(v3, v1);
            var normal = Normalize(Cross(edge1, edge2));

            var mid = new Vector3
            {
                x = (v1.x + v2.x + v3.x) / 3f,
                y = (v1.y + v2.y + v3.y) / 3f,
                z = (v1.z + v2.z + v3.z) / 3f
            };

            var desired = Normalize(Subtract(mid, center));
            float dot = Dot(normal, desired);

            if (dot < 0f)
            {
                var temp = v2;
                v2 = v3;
                v3 = temp;
            }

            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = v1,
                vert2 = v2,
                vert3 = v3,
                noHidden = noHidden
            };
        }

        public static Vector3 Subtract(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.x - b.x,
                y = a.y - b.y,
                z = a.z - b.z
            };
        }

        public static Vector3 Add(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.x + b.x,
                y = a.y + b.y,
                z = a.z + b.z
            };
        }

        public static Vector3 Scale(Vector3 v, float s)
        {
            return new Vector3
            {
                x = v.x * s,
                y = v.y * s,
                z = v.z * s
            };
        }

        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.y * b.z - a.z * b.y,
                y = a.z * b.x - a.x * b.z,
                z = a.x * b.y - a.y * b.x
            };
        }

        public static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;
            if (lenSq <= 1e-6f)
                return new Vector3 { x = 0, y = 0, z = 0 };

            float invLen = 1.0f / (float)Math.Sqrt(lenSq);
            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }
    }
}
