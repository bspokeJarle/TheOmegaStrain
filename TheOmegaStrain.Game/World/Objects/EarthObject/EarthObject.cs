using System;
using System.Collections.Generic;
using System.Globalization;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;
using MathF = System.MathF;
using _3dRotations.World.Objects;

namespace _3dRotations.World.Objects.EarthObject
{
    public static class EarthObject
    {
        private const float RenderDepth = 520f;
        private const float RenderYOffset = 0f;
        private const float CrashboxRadius = 200f;
        private const float CrashboxScale = 1.04f;

        private const int StarCount = 250;
        private const float StarFieldRadius = 560f;   // distance from globe centre
        private const float StarSize = 5f;
        private const int StarRandomSeed = 42;
        private const string OceanColor = "#061B49";
        private const string LandColor = "#2F8F43";
        private const string HighlandsColor = "#66A85A";
        private const float TerrainStartRadius = 181.5f;
        private const float MountainStartRadius = 188f;
        private const float MountainPeakRadius = 190.2f;

        private const float GlobeRadius = 180f;
        private const float SurfaceObjectOffset = 5f;   // how far above globe surface objects sit
        private const int TreeCount = 15;
        private const int HouseCount = 10;
        private const int IglooCount = 6;

        private static readonly string[] StarColors = { "FFFFFF", "FFF7CC", "CCE5FF", "FFD8D8", "E6FFE6" };

        public static _3dObject CreateEarth()
        {
            var parts = new List<I3dObjectPart>
            {
                CreatePart("EarthGlobe", ParseTriangles(EarthModelData.EarthTrianglesData))
            };

            var rng = new Random(StarRandomSeed);

            // Add stars as extra parts so they rotate with the globe
            for (int i = 0; i < StarCount; i++)
                parts.Add(CreateStarPart(i, rng));

            // Surface miniatures — placed on land/highland triangles, rotate with globe
            AddSurfaceMiniatures(parts, rng);

            var obj = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "Earth",
                ObjectParts = parts,
                CrashBoxes = BuildCrashBoxes(CrashboxRadius, CrashboxScale),
                Rotation = new Vector3 { x = WorldViewSetup.WorldPitchDegrees, y = 0f, z = 90f },
                WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
                ObjectOffsets = new Vector3
                {
                    x = 0f,
                    y = RenderYOffset,
                    z = RenderDepth
                },
                IsActive = true
            };

            obj.ImpactStatus = new ImpactStatus();
            return obj;
        }

        // ----------------------------------------------------------------
        // Surface miniatures (trees, houses, igloos) embedded as parts
        // ----------------------------------------------------------------

        private static void AddSurfaceMiniatures(List<I3dObjectPart> parts, Random rng)
        {
            // Collect candidate surface normals from the data
            var landTiles   = new List<Vector3>();
            var iglooTiles  = new List<Vector3>();

            var lines = EarthModelData.EarthTrianglesData.Split(
                ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                var split = line.Split('|', StringSplitOptions.TrimEntries);
                if (split.Length != 2) continue;
                var verts = split[0].Split(';', StringSplitOptions.TrimEntries);
                if (verts.Length != 3) continue;

                var v1 = ParseVertex(verts[0]);
                var v2 = ParseVertex(verts[1]);
                var v3 = ParseVertex(verts[2]);

                var center = new Vector3
                {
                    x = (v1.x + v2.x + v3.x) / 3f,
                    y = (v1.y + v2.y + v3.y) / 3f,
                    z = (v1.z + v2.z + v3.z) / 3f
                };

                float r = MathF.Sqrt(center.x * center.x + center.y * center.y + center.z * center.z);

                // land tiles: lowland / plains range
                if (r >= 181.5f && r < 188f)
                    landTiles.Add(center);
                // igloo tiles: very high (snowy peaks approximated by max radius)
                else if (r >= 188f)
                    iglooTiles.Add(center);
            }

            // Shuffle deterministically
            Shuffle(landTiles, rng);
            Shuffle(iglooTiles, rng);

            // Trees (first TreeCount land tiles)
            for (int i = 0; i < TreeCount && i < landTiles.Count; i++)
                parts.Add(BuildMiniTreePart(i, landTiles[i], rng));

            // Houses (next HouseCount land tiles)
            for (int i = TreeCount; i < TreeCount + HouseCount && i < landTiles.Count; i++)
                parts.Add(BuildMiniHousePart(i - TreeCount, landTiles[i], rng));

            // Igloos on snowy peaks
            for (int i = 0; i < IglooCount && i < iglooTiles.Count; i++)
                parts.Add(BuildMiniIglooPart(i, iglooTiles[i]));
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Builds a local coordinate frame (tangent1, tangent2, normal) for a point on the sphere.
        /// Normal points away from globe centre; tangents form the surface plane.
        /// </summary>
        private static (Vector3 n, Vector3 t1, Vector3 t2) SurfaceFrame(Vector3 center)
        {
            float r = MathF.Sqrt(center.x * center.x + center.y * center.y + center.z * center.z);
            if (r < 0.0001f) r = 1f;
            var n = new Vector3 { x = center.x / r, y = center.y / r, z = center.z / r };

            // Arbitrary tangent — avoid collinear with n
            var up = MathF.Abs(n.z) < 0.9f
                ? new Vector3 { x = 0f, y = 0f, z = 1f }
                : new Vector3 { x = 1f, y = 0f, z = 0f };

            var t1 = Cross(n, up);
            float t1len = MathF.Sqrt(t1.x * t1.x + t1.y * t1.y + t1.z * t1.z);
            t1 = new Vector3 { x = t1.x / t1len, y = t1.y / t1len, z = t1.z / t1len };
            var t2 = Cross(n, t1);
            return (n, t1, t2);
        }

        private static Vector3 Cross(Vector3 a, Vector3 b) =>
            new Vector3 { x = a.y * b.z - a.z * b.y, y = a.z * b.x - a.x * b.z, z = a.x * b.y - a.y * b.x };

        /// <summary>Maps local (u,v,h) → world coords using the surface frame at center.</summary>
        private static Vector3 SurfacePoint(Vector3 center, Vector3 n, Vector3 t1, Vector3 t2, float u, float v, float h)
        {
            float scale = (GlobeRadius + SurfaceObjectOffset + h) / GlobeRadius;
            return new Vector3
            {
                x = center.x * scale + t1.x * u + t2.x * v,
                y = center.y * scale + t1.y * u + t2.y * v,
                z = center.z * scale + t1.z * u + t2.z * v
            };
        }

        private static _3dObjectPart BuildMiniTreePart(int index, Vector3 center, Random rng)
        {
            var obj = Tree.CreateTree(null!);
            float scale = 0.22f;
            return EmbedObjectOnSurface($"MiniTree_{index}", obj, center, scale, rng);
        }

        private static _3dObjectPart BuildMiniHousePart(int index, Vector3 center, Random rng)
        {
            var obj = House.CreateHouse(null!);
            float scale = 0.20f;
            return EmbedObjectOnSurface($"MiniHouse_{index}", obj, center, scale, rng);
        }

        private static _3dObjectPart BuildMiniIglooPart(int index, Vector3 center)
        {
            var obj = Igloo.CreateSmallIgloo(null!);
            float scale = 0.25f;
            return EmbedObjectOnSurface($"MiniIgloo_{index}", obj, center, scale, new Random(index));
        }

        /// <summary>
        /// Extracts all visible (non-shadow) triangles from a real object, scales them,
        /// and transforms each vertex onto the globe surface using the surface frame at <paramref name="center"/>.
        /// </summary>
        private static _3dObjectPart EmbedObjectOnSurface(string partName, _3dObject obj, Vector3 center, float scale, Random rng)
        {
            var (n, t1, t2) = SurfaceFrame(center);
            float yaw = (float)(rng.NextDouble() * MathF.PI * 2f);
            float cosY = MathF.Cos(yaw), sinY = MathF.Sin(yaw);

            var tris = new List<ITriangleMeshWithColor>();

            foreach (var part in obj.ObjectParts)
            {
                if (!part.IsVisible) continue;
                if (part.PartName != null && part.PartName.StartsWith("Shadow", StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var tri in part.Triangles)
                {
                    var v1 = TransformVert(new Vector3(tri.vert1.x, tri.vert1.y, tri.vert1.z), center, n, t1, t2, scale, cosY, sinY);
                    var v2 = TransformVert(new Vector3(tri.vert2.x, tri.vert2.y, tri.vert2.z), center, n, t1, t2, scale, cosY, sinY);
                    var v3 = TransformVert(new Vector3(tri.vert3.x, tri.vert3.y, tri.vert3.z), center, n, t1, t2, scale, cosY, sinY);

                    tris.Add(new TriangleMeshWithColor
                    {
                        Color = (tri as TriangleMeshWithColor)?.Color ?? "FFFFFF",
                        vert1 = v1,
                        vert2 = v2,
                        vert3 = v3,
                        noHidden = false
                    });
                }
            }

            return new _3dObjectPart { PartName = partName, Triangles = tris, IsVisible = true };
        }

        /// <summary>
        /// Transforms a single local-space vertex onto the sphere surface.
        /// Local axes: x=right(t1), y=forward(t2), z=up(normal). A random yaw rotates the object around the surface normal.
        /// </summary>
        private static Vector3 TransformVert(Vector3 local, Vector3 center, Vector3 n, Vector3 t1, Vector3 t2,
            float scale, float cosY, float sinY)
        {
            // Apply yaw rotation in local XY plane
            float lx =  local.x * cosY - local.y * sinY;
            float ly =  local.x * sinY + local.y * cosY;
            float lz =  local.z;

            // Scale
            lx *= scale; ly *= scale; lz *= scale;

            // Map to world: lx→t1, ly→t2, lz→normal (up from surface)
            float surfaceR = GlobeRadius + SurfaceObjectOffset + lz;
            float baseScale = surfaceR / GlobeRadius;

            return new Vector3
            {
                x = center.x * baseScale + t1.x * lx + t2.x * ly,
                y = center.y * baseScale + t1.y * lx + t2.y * ly,
                z = center.z * baseScale + t1.z * lx + t2.z * ly
            };
        }

        private static TriangleMeshWithColor MakeSurfaceTri(Vector3 v1, Vector3 v2, Vector3 v3, string color) =>
            new TriangleMeshWithColor { Color = color, vert1 = v1, vert2 = v2, vert3 = v3, noHidden = false };

        // ----------------------------------------------------------------
        // Stars
        // ----------------------------------------------------------------

        private static _3dObjectPart CreateStarPart(int index, Random rng)
        {
            // Evenly distribute stars across the sphere surface using the Fibonacci sphere method
            float goldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));
            float t = (index + 0.5f) / StarCount;
            float inclination = MathF.Acos(1f - 2f * t);
            float azimuth = goldenAngle * index;

            float r = StarFieldRadius;
            float cx = r * MathF.Sin(inclination) * MathF.Cos(azimuth);
            float cy = r * MathF.Sin(inclination) * MathF.Sin(azimuth);
            float cz = r * MathF.Cos(inclination);

            // Random size variation and color
            float size = StarSize * (0.5f + (float)rng.NextDouble() * 1.0f);
            string color = StarColors[rng.Next(StarColors.Length)];
            float rotationRadians = (float)rng.NextDouble() * MathF.PI * 2f;

            var tris = BuildStarTriangles(cx, cy, cz, size, color, rotationRadians);

            return new _3dObjectPart
            {
                PartName = $"Star_{index}",
                Triangles = tris,
                IsVisible = true
            };
        }

        private static List<ITriangleMeshWithColor> BuildStarTriangles(float cx, float cy, float cz, float size, string color, float rotationRadians)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float h = size * 0.5f;
            float w = size * 0.12f;
            float cos = MathF.Cos(rotationRadians);
            float sin = MathF.Sin(rotationRadians);

            Vector3 Point(float localX, float localY)
            {
                return new Vector3
                {
                    x = cx + (localX * cos) - (localY * sin),
                    y = cy + (localX * sin) + (localY * cos),
                    z = cz
                };
            }

            // Two crossing sparkle arms, rotated around the star center so each star
            // keeps the same cheap triangle count without looking stamped.
            tris.Add(MakeStarTri(
                Point(-w, -h),
                Point(w, -h),
                Point(0f, h),
                color));
            tris.Add(MakeStarTri(
                Point(-h, -w),
                Point(h, -w),
                Point(0f, w),
                color));

            return tris;
        }

        private static TriangleMeshWithColor MakeStarTri(Vector3 v1, Vector3 v2, Vector3 v3, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = v1,
                vert2 = v2,
                vert3 = v3,
                noHidden = true
            };
        }

        private static List<ITriangleMeshWithColor> ParseTriangles(string data)
        {
            var result = new List<ITriangleMeshWithColor>(EarthModelData.TriangleCount);
            var lines = data.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                var colorSplit = line.Split('|', StringSplitOptions.TrimEntries);
                if (colorSplit.Length != 2)
                    throw new FormatException($"Invalid Earth triangle data line: {line}");

                var vertices = colorSplit[0].Split(';', StringSplitOptions.TrimEntries);
                if (vertices.Length != 3)
                    throw new FormatException($"Invalid Earth triangle vertex count: {line}");

                AddTriangle(
                    result,
                    ParseVertex(vertices[0]),
                    ParseVertex(vertices[1]),
                    ParseVertex(vertices[2]));
            }

            return result;
        }

        private static Vector3 ParseVertex(string value)
        {
            var parts = value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
                throw new FormatException($"Invalid Earth vertex data: {value}");

            return new Vector3
            {
                x = float.Parse(parts[0], CultureInfo.InvariantCulture),
                y = float.Parse(parts[1], CultureInfo.InvariantCulture),
                z = float.Parse(parts[2], CultureInfo.InvariantCulture)
            };
        }

        private static void AddTriangle(List<ITriangleMeshWithColor> result, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            var normal = CalculateNormal(v1, v2, v3);
            var center = new Vector3
            {
                x = (v1.x + v2.x + v3.x) / 3f,
                y = (v1.y + v2.y + v3.y) / 3f,
                z = (v1.z + v2.z + v3.z) / 3f
            };

            float outwardDot = (normal.x * center.x) + (normal.y * center.y) + (normal.z * center.z);
            if (outwardDot < 0f)
            {
                (v2, v3) = (v3, v2);
                normal.x = -normal.x;
                normal.y = -normal.y;
                normal.z = -normal.z;
            }

            result.Add(new TriangleMeshWithColor
            {
                Color = GetStylizedEarthColor(center),
                vert1 = v1,
                vert2 = v2,
                vert3 = v3,
                normal1 = normal,
                normal2 = normal,
                normal3 = normal
            });
        }

        public static string GetStylizedEarthColor(Vector3 center)
        {
            var (latitude, longitude) = GetLatitudeLongitude(center);
            float radius = GetRadius(center);

            if (radius >= MountainStartRadius)
                return GetMountainColor(radius);

            if (radius >= TerrainStartRadius)
                return GetHighlandsColor(radius);

            if (IsEuropeNordicLand(latitude, longitude)
                || IsAfricaLand(latitude, longitude)
                || IsAsiaLand(latitude, longitude)
                || IsAtlanticEdgeLand(latitude, longitude))
            {
                return LandColor;
            }

            return OceanColor;
        }

        private static float GetRadius(Vector3 point)
        {
            return MathF.Sqrt((point.x * point.x) + (point.y * point.y) + (point.z * point.z));
        }

        private static (float Latitude, float Longitude) GetLatitudeLongitude(Vector3 point)
        {
            float radius = GetRadius(point);
            if (radius <= 0.0001f)
                return (0f, 0f);

            float latitude = MathF.Asin(Math.Clamp(point.y / radius, -1f, 1f)) * 180f / MathF.PI;
            float longitude = MathF.Atan2(point.z, point.x) * 180f / MathF.PI;
            return (latitude, longitude);
        }

        private static string GetHighlandsColor(float radius)
        {
            float t = Normalize(radius, TerrainStartRadius, MountainStartRadius);
            return LerpColor(LandColor, HighlandsColor, t);
        }

        private static string GetMountainColor(float radius)
        {
            float t = Normalize(radius, MountainStartRadius, MountainPeakRadius);
            byte value = (byte)Math.Clamp(MathF.Round(112f + (122f * t)), 112f, 234f);
            return ToHex(value, value, value);
        }

        private static float Normalize(float value, float min, float max)
        {
            if (max <= min)
                return 0f;

            return Math.Clamp((value - min) / (max - min), 0f, 1f);
        }

        private static string LerpColor(string fromHex, string toHex, float t)
        {
            t = Math.Clamp(t, 0f, 1f);

            byte fr = Convert.ToByte(fromHex.Substring(1, 2), 16);
            byte fg = Convert.ToByte(fromHex.Substring(3, 2), 16);
            byte fb = Convert.ToByte(fromHex.Substring(5, 2), 16);
            byte tr = Convert.ToByte(toHex.Substring(1, 2), 16);
            byte tg = Convert.ToByte(toHex.Substring(3, 2), 16);
            byte tb = Convert.ToByte(toHex.Substring(5, 2), 16);

            byte r = (byte)Math.Clamp(MathF.Round(fr + ((tr - fr) * t)), 0f, 255f);
            byte g = (byte)Math.Clamp(MathF.Round(fg + ((tg - fg) * t)), 0f, 255f);
            byte b = (byte)Math.Clamp(MathF.Round(fb + ((tb - fb) * t)), 0f, 255f);

            return ToHex(r, g, b);
        }

        private static string ToHex(byte r, byte g, byte b)
        {
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static bool IsEuropeNordicLand(float latitude, float longitude)
        {
            bool europeMainland = IsInsideEllipse(latitude, longitude, 52f, 18f, 23f, 34f);
            bool westernEurope = IsInsideEllipse(latitude, longitude, 48f, -4f, 13f, 14f);
            bool scandinavia = IsInsideEllipse(latitude, longitude, 65f, 17f, 15f, 12f);
            bool finlandBaltic = IsInsideEllipse(latitude, longitude, 61f, 29f, 12f, 12f);
            bool britishIsles = IsInsideEllipse(latitude, longitude, 55f, -6f, 9f, 8f);
            bool mediterranean = IsInsideEllipse(latitude, longitude, 40f, 20f, 10f, 22f);

            return europeMainland
                || westernEurope
                || scandinavia
                || finlandBaltic
                || britishIsles
                || mediterranean;
        }

        private static bool IsAfricaLand(float latitude, float longitude)
        {
            bool northAfrica = IsInsideEllipse(latitude, longitude, 20f, 15f, 24f, 32f);
            bool centralAfrica = IsInsideEllipse(latitude, longitude, -3f, 18f, 24f, 28f);
            bool eastAfrica = IsInsideEllipse(latitude, longitude, -9f, 35f, 19f, 20f);

            return northAfrica || centralAfrica || eastAfrica;
        }

        private static bool IsAsiaLand(float latitude, float longitude)
        {
            bool westernAsia = IsInsideEllipse(latitude, longitude, 44f, 58f, 24f, 32f);
            bool centralAsia = IsInsideEllipse(latitude, longitude, 46f, 92f, 21f, 36f);
            bool northAsia = IsInsideEllipse(latitude, longitude, 59f, 92f, 13f, 45f);

            return westernAsia || centralAsia || northAsia;
        }

        private static bool IsAtlanticEdgeLand(float latitude, float longitude)
        {
            bool greenland = IsInsideEllipse(latitude, longitude, 73f, -42f, 10f, 18f);
            bool northAmericaHint = IsInsideEllipse(latitude, longitude, 48f, -86f, 19f, 29f);

            return greenland || northAmericaHint;
        }

        private static bool IsInsideEllipse(
            float latitude,
            float longitude,
            float centerLatitude,
            float centerLongitude,
            float latitudeRadius,
            float longitudeRadius)
        {
            float lat = (latitude - centerLatitude) / latitudeRadius;
            float lon = GetLongitudeDelta(longitude, centerLongitude) / longitudeRadius;
            return (lat * lat) + (lon * lon) <= 1f;
        }

        private static float GetLongitudeDelta(float longitude, float centerLongitude)
        {
            float delta = longitude - centerLongitude;
            while (delta > 180f) delta -= 360f;
            while (delta < -180f) delta += 360f;
            return delta;
        }

        private static Vector3 CalculateNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            var edge1 = new Vector3 { x = v2.x - v1.x, y = v2.y - v1.y, z = v2.z - v1.z };
            var edge2 = new Vector3 { x = v3.x - v1.x, y = v3.y - v1.y, z = v3.z - v1.z };
            var normal = new Vector3
            {
                x = edge1.y * edge2.z - edge1.z * edge2.y,
                y = edge1.z * edge2.x - edge1.x * edge2.z,
                z = edge1.x * edge2.y - edge1.y * edge2.x
            };

            float len = MathF.Sqrt(normal.x * normal.x + normal.y * normal.y + normal.z * normal.z);
            if (len > 0f)
            {
                normal.x /= len;
                normal.y /= len;
                normal.z /= len;
            }

            return normal;
        }

        private static List<List<IVector3>> BuildCrashBoxes(float radius, float scale)
        {
            float h = radius * scale;
            var box = new List<IVector3>
            {
                new Vector3 { x = -h, y = -h, z = -h },
                new Vector3 { x =  h, y = -h, z = -h },
                new Vector3 { x =  h, y =  h, z = -h },
                new Vector3 { x = -h, y =  h, z = -h },
                new Vector3 { x = -h, y = -h, z =  h },
                new Vector3 { x =  h, y = -h, z =  h },
                new Vector3 { x =  h, y =  h, z =  h },
                new Vector3 { x = -h, y =  h, z =  h }
            };

            return new List<List<IVector3>> { box };
        }

        private static _3dObjectPart CreatePart(string name, List<ITriangleMeshWithColor> triangles)
        {
            return new _3dObjectPart
            {
                PartName = name,
                Triangles = triangles,
                IsVisible = true
            };
        }
    }
}
