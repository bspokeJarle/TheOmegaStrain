using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class SnowTower
    {
        private static float iglooRadius = 40f;
        private static float iglooHeight = 25f;
        private static float entranceWidth = 14f;
        private static float entranceDepth = 15f;
        private static float entranceHeight = 10f;

        private static float shaftBottomRadius = 11f;
        private static float shaftTopRadius = 8f;
        private static float shaftHeight = 72f;

        private static float headBottomRadius = 15f;
        private static float headTopRadius = 18f;
        private static float headHeight = 18f;

        private static float glassHeight = 8f;
        private static float glassInset = 1.4f;

        private static float snowLidHeight = 8f;
        private static float mastHeight = 9f;
        private static float mastHalfSize = 1.5f;

        private static int domeSegments = 12;
        private static int domeRings = 4;
        private static int towerSegments = 8;

        private static readonly Vector3 BaseCenter = new Vector3 { x = 0f, y = 0f, z = 10f };
        private static readonly Vector3 TowerCenter = new Vector3 { x = 0f, y = 0f, z = 70f };

        private static string snowLight = "EAF7FF";
        private static string snowMid = "C9E4F2";
        private static string snowDark = "91B7C8";
        private static string iceBlue = "8FD8FF";
        private static string iceDark = "4F8FA8";
        private static string entranceDark = "101820";
        private static string glassBlue = "78D8FF";
        private static string towerIce = "B7E8F7";
        private static string towerIceDark = "6EAFC5";
        private static string antennaRed = "FF3344";

        public static _3dObject CreateSnowTower(ISurface parentSurface)
        {
            var tower = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SnowTower",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -12f, y = 0f, z = -12f }
            };

            AddPart(tower, "SnowTowerIglooBase", IglooBase(), true);
            AddPart(tower, "SnowTowerBaseEntrance", IglooEntrance(), true);
            AddPart(tower, "SnowTowerBlockLines", IglooBlockLines(), true);

            AddPart(tower, "SnowTowerShaft", TowerShaft(), true);
            AddPart(tower, "SnowTowerHeadFrame", TowerHeadFrame(), true);
            AddPart(tower, "SnowTowerGlass", TowerGlass(), true);
            AddPart(tower, "SnowTowerSnowLid", SnowLid(), true);
            AddPart(tower, "SnowTowerAntenna", Antenna(), true);

            tower.CrashBoxes = SnowTowerCrashBoxes();
            tower.CrashBoxNames = new List<string?> { "IglooBase", "TowerShaft", "TowerHead" };

            _3dObjectHelpers.AddCustomShadowPart(tower, SnowTowerShadow());
            _3dObjectHelpers.ApplyScaleToObject(tower, LandBasedObjectSetup.WinterSurfaceObjectScale);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(tower);

            return tower;
        }

        // ----------------------------------------------------
        //  IGLOO BASE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? IglooBase()
        {
            var tris = new List<ITriangleMeshWithColor>();
            var rings = new List<List<Vector3>>();

            for (int r = 0; r <= domeRings; r++)
            {
                float t = r / (float)domeRings;
                float z = MathF.Sin(t * MathF.PI * 0.5f) * iglooHeight;
                float radius = MathF.Cos(t * MathF.PI * 0.5f) * iglooRadius;

                if (r == domeRings)
                    radius = 0.2f;

                rings.Add(CreateRing(0f, 0f, z, radius, domeSegments));
            }

            for (int r = 0; r < domeRings; r++)
            {
                var lower = rings[r];
                var upper = rings[r + 1];

                for (int i = 0; i < domeSegments; i++)
                {
                    int next = (i + 1) % domeSegments;
                    string color = GetSnowColor(r, i);

                    if (r == domeRings - 1)
                        tris.Add(CreateTriangleOutward(upper[i], lower[next], lower[i], BaseCenter, color));
                    else
                        AddQuadOutward(tris, lower[i], lower[next], upper[next], upper[i], BaseCenter, color);
                }
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? IglooEntrance()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x1 = -entranceWidth / 2f;
            float x2 = entranceWidth / 2f;
            float yFront = -iglooRadius - entranceDepth;
            float yBack = -iglooRadius * 0.55f;

            var frontLeftBottom = new Vector3 { x = x1, y = yFront, z = 0f };
            var frontRightBottom = new Vector3 { x = x2, y = yFront, z = 0f };
            var frontLeftTop = new Vector3 { x = x1, y = yFront, z = entranceHeight };
            var frontRightTop = new Vector3 { x = x2, y = yFront, z = entranceHeight };

            var backLeftBottom = new Vector3 { x = x1 * 0.82f, y = yBack, z = 0f };
            var backRightBottom = new Vector3 { x = x2 * 0.82f, y = yBack, z = 0f };
            var backLeftTop = new Vector3 { x = x1 * 0.65f, y = yBack, z = entranceHeight + 1.5f };
            var backRightTop = new Vector3 { x = x2 * 0.65f, y = yBack, z = entranceHeight + 1.5f };

            AddQuadOutward(tris, frontLeftBottom, backLeftBottom, backLeftTop, frontLeftTop, BaseCenter, snowDark);
            AddQuadOutward(tris, frontRightBottom, frontRightTop, backRightTop, backRightBottom, BaseCenter, snowMid);
            AddQuadOutward(tris, frontLeftTop, backLeftTop, backRightTop, frontRightTop, BaseCenter, snowLight);

            var archTop = new Vector3 { x = 0f, y = yFront - 0.5f, z = entranceHeight + 4f };
            tris.Add(CreateTriangleOutward(frontLeftTop, archTop, frontRightTop, BaseCenter, snowLight));

            AddQuadOutward(
                tris,
                new Vector3 { x = x1 * 0.52f, y = yFront - 0.8f, z = 0f },
                new Vector3 { x = x2 * 0.52f, y = yFront - 0.8f, z = 0f },
                new Vector3 { x = x2 * 0.42f, y = yFront - 0.8f, z = entranceHeight * 0.74f },
                new Vector3 { x = x1 * 0.42f, y = yFront - 0.8f, z = entranceHeight * 0.74f },
                BaseCenter,
                entranceDark,
                noHidden: true);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? IglooBlockLines()
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int r = 1; r <= domeRings; r++)
            {
                float t = r / (float)(domeRings + 1);
                float z = MathF.Sin(t * MathF.PI * 0.5f) * iglooHeight;
                float radius = MathF.Cos(t * MathF.PI * 0.5f) * (iglooRadius + 0.5f);

                for (int i = 0; i < domeSegments; i += 2)
                {
                    float a1 = i * MathF.PI * 2f / domeSegments;
                    float a2 = (i + 1.2f) * MathF.PI * 2f / domeSegments;

                    var p1 = new Vector3 { x = MathF.Cos(a1) * radius, y = MathF.Sin(a1) * radius, z = z };
                    var p2 = new Vector3 { x = MathF.Cos(a2) * radius, y = MathF.Sin(a2) * radius, z = z + 0.35f };
                    var p3 = new Vector3 { x = MathF.Cos(a2) * (radius - 1.5f), y = MathF.Sin(a2) * (radius - 1.5f), z = z + 0.55f };

                    tris.Add(CreateTriangleOutward(p1, p2, p3, BaseCenter, snowDark));
                }
            }

            return tris;
        }

        // ----------------------------------------------------
        //  TOWER
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? TowerShaft()
        {
            float z0 = iglooHeight - 1f;
            float z1 = z0 + shaftHeight;

            var center = new Vector3 { x = 0f, y = 0f, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                towerSegments,
                shaftBottomRadius,
                shaftTopRadius,
                z0,
                z1,
                center,
                towerIce,
                capBottom: false,
                capTop: false);
        }

        public static List<ITriangleMeshWithColor>? TowerHeadFrame()
        {
            float z0 = iglooHeight - 1f + shaftHeight;
            float z1 = z0 + headHeight;

            var center = new Vector3 { x = 0f, y = 0f, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                towerSegments,
                headBottomRadius,
                headTopRadius,
                z0,
                z1,
                center,
                towerIceDark,
                capBottom: false,
                capTop: false);
        }

        public static List<ITriangleMeshWithColor>? TowerGlass()
        {
            float headBaseZ = iglooHeight - 1f + shaftHeight;
            float z0 = headBaseZ + (headHeight - glassHeight) * 0.5f;
            float z1 = z0 + glassHeight;

            float r0 = Math.Max(1f, headBottomRadius - glassInset);
            float r1 = Math.Max(1f, headTopRadius - glassInset);

            var center = new Vector3 { x = 0f, y = 0f, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                towerSegments,
                r0,
                r1,
                z0,
                z1,
                center,
                glassBlue,
                capBottom: false,
                capTop: false,
                flatColor: true);
        }

        public static List<ITriangleMeshWithColor>? SnowLid()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float z0 = iglooHeight - 1f + shaftHeight + headHeight;
            float z1 = z0 + snowLidHeight;

            float r0 = headTopRadius + 5f;
            float r1 = headTopRadius * 0.42f;

            var center = new Vector3 { x = 0f, y = 0f, z = (z0 + z1) * 0.5f };

            tris.AddRange(CreateFrustum(
                towerSegments,
                r0,
                r1,
                z0,
                z1,
                center,
                snowLight,
                capBottom: false,
                capTop: true));

            // Snow drip / uneven overhang
            for (int i = 0; i < towerSegments; i += 2)
            {
                float a = i * MathF.PI * 2f / towerSegments;
                float a2 = (i + 1) * MathF.PI * 2f / towerSegments;

                var p1 = new Vector3 { x = MathF.Cos(a) * r0, y = MathF.Sin(a) * r0, z = z0 + 0.2f };
                var p2 = new Vector3 { x = MathF.Cos(a2) * r0, y = MathF.Sin(a2) * r0, z = z0 + 0.2f };
                var drip = new Vector3 { x = MathF.Cos((a + a2) * 0.5f) * (r0 - 1.5f), y = MathF.Sin((a + a2) * 0.5f) * (r0 - 1.5f), z = z0 - 4f };

                tris.Add(CreateTriangleOutward(p1, p2, drip, center, snowMid));
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? Antenna()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float roofTopZ = iglooHeight - 1f + shaftHeight + headHeight + snowLidHeight;

            var min = new Vector3 { x = -mastHalfSize, y = -mastHalfSize, z = roofTopZ };
            var max = new Vector3 { x = mastHalfSize, y = mastHalfSize, z = roofTopZ + mastHeight };

            var center = new Vector3 { x = 0f, y = 0f, z = roofTopZ + mastHeight * 0.5f };

            tris.AddRange(CreateBox(min, max, center, iceDark));

            var tip = new Vector3 { x = 0f, y = 0f, z = roofTopZ + mastHeight + 4f };
            var a = new Vector3 { x = -2.2f, y = 0f, z = roofTopZ + mastHeight };
            var b = new Vector3 { x = 2.2f, y = 0f, z = roofTopZ + mastHeight };
            var c = new Vector3 { x = 0f, y = -2.2f, z = roofTopZ + mastHeight };
            var d = new Vector3 { x = 0f, y = 2.2f, z = roofTopZ + mastHeight };

            tris.Add(CreateTriangleOutward(a, tip, b, center, antennaRed));
            tris.Add(CreateTriangleOutward(c, d, tip, center, antennaRed));

            return tris;
        }

        // ----------------------------------------------------
        //  CRASHBOXES / SHADOW
        // ----------------------------------------------------

        public static List<List<IVector3>> SnowTowerCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -iglooRadius, y = -iglooRadius - entranceDepth, z = 0f },
                new Vector3 { x = iglooRadius, y = iglooRadius, z = iglooHeight + 2f }));

            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -shaftBottomRadius - 2f, y = -shaftBottomRadius - 2f, z = iglooHeight },
                new Vector3 { x = shaftBottomRadius + 2f, y = shaftBottomRadius + 2f, z = iglooHeight + shaftHeight }));

            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -headTopRadius - 6f, y = -headTopRadius - 6f, z = iglooHeight + shaftHeight },
                new Vector3 { x = headTopRadius + 6f, y = headTopRadius + 6f, z = iglooHeight + shaftHeight + headHeight + snowLidHeight + mastHeight }));

            return boxes;
        }

        private static List<ITriangleMeshWithColor> SnowTowerShadow()
        {
            var tris = new List<ITriangleMeshWithColor>();
            const string sc = _3dObjectHelpers.ShadowColorHex;

            var center = new Vector3 { x = 0f, y = 0f, z = 0f };

            for (int i = 0; i < 10; i++)
            {
                float a1 = i * MathF.PI * 2f / 10f;
                float a2 = (i + 1) * MathF.PI * 2f / 10f;

                var p1 = new Vector3 { x = MathF.Cos(a1) * iglooRadius, y = 0f, z = MathF.Sin(a1) * iglooRadius * 0.35f };
                var p2 = new Vector3 { x = MathF.Cos(a2) * iglooRadius, y = 0f, z = MathF.Sin(a2) * iglooRadius * 0.35f };

                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = center, vert2 = p1, vert3 = p2 });
            }

            tris.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -8f, y = 0f, z = iglooHeight },
                vert2 = new Vector3 { x = 8f, y = 0f, z = iglooHeight },
                vert3 = new Vector3 { x = 0f, y = 0f, z = iglooHeight + shaftHeight + headHeight }
            });

            return tris;
        }

        // ----------------------------------------------------
        //  GEOMETRY HELPERS
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor> CreateBox(Vector3 min, Vector3 max, Vector3 center, string color)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var p000 = new Vector3 { x = min.x, y = min.y, z = min.z };
            var p001 = new Vector3 { x = min.x, y = min.y, z = max.z };
            var p010 = new Vector3 { x = min.x, y = max.y, z = min.z };
            var p011 = new Vector3 { x = min.x, y = max.y, z = max.z };
            var p100 = new Vector3 { x = max.x, y = min.y, z = min.z };
            var p101 = new Vector3 { x = max.x, y = min.y, z = max.z };
            var p110 = new Vector3 { x = max.x, y = max.y, z = min.z };
            var p111 = new Vector3 { x = max.x, y = max.y, z = max.z };

            AddQuadOutward(tris, p001, p101, p111, p011, center, color);
            AddQuadOutward(tris, p100, p000, p010, p110, center, color);
            AddQuadOutward(tris, p101, p100, p110, p111, center, color);
            AddQuadOutward(tris, p000, p001, p011, p010, center, color);
            AddQuadOutward(tris, p011, p111, p110, p010, center, color);
            AddQuadOutward(tris, p100, p101, p001, p000, center, color);

            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateFrustum(
            int segments,
            float radiusBottom,
            float radiusTop,
            float zBottom,
            float zTop,
            Vector3 center,
            string baseColor,
            bool capBottom,
            bool capTop,
            bool flatColor = false)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var bottom = CreateRing(0f, 0f, zBottom, radiusBottom, segments);
            var top = CreateRing(0f, 0f, zTop, radiusTop, segments);

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                string color = flatColor ? baseColor : ShadeByIndex(baseColor, i);

                AddQuadOutward(tris, bottom[i], bottom[next], top[next], top[i], center, color);
            }

            if (capBottom)
            {
                var c = new Vector3 { x = 0f, y = 0f, z = zBottom };
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    tris.Add(CreateTriangleOutward(c, bottom[next], bottom[i], center, baseColor));
                }
            }

            if (capTop)
            {
                var c = new Vector3 { x = 0f, y = 0f, z = zTop };
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    tris.Add(CreateTriangleOutward(c, top[i], top[next], center, baseColor));
                }
            }

            return tris;
        }

        private static List<Vector3> CreateRing(float x, float y, float z, float radius, int segments)
        {
            var points = new List<Vector3>(segments);
            float step = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                points.Add(new Vector3
                {
                    x = x + MathF.Cos(a) * radius,
                    y = y + MathF.Sin(a) * radius,
                    z = z
                });
            }

            return points;
        }

        private static string GetSnowColor(int ring, int segment)
        {
            int v = (ring + segment) % 4;

            if (v == 0) return snowLight;
            if (v == 1) return snowMid;
            if (v == 2) return iceBlue;
            return snowDark;
        }

        private static string ShadeByIndex(string color, int index)
        {
            int v = index % 4;

            if (v == 0) return color;
            if (v == 1) return snowMid;
            if (v == 2) return iceBlue;
            return towerIceDark;
        }

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }

        private static void AddQuadOutward(
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

        private static TriangleMeshWithColor CreateTriangleOutward(
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

        private static Vector3 Subtract(Vector3 a, Vector3 b)
        {
            return new Vector3 { x = a.x - b.x, y = a.y - b.y, z = a.z - b.z };
        }

        private static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.y * b.z - a.z * b.y,
                y = a.z * b.x - a.x * b.z,
                z = a.x * b.y - a.y * b.x
            };
        }

        private static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        private static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;

            if (lenSq <= 1e-6f)
                return new Vector3 { x = 0, y = 0, z = 0 };

            float invLen = 1.0f / MathF.Sqrt(lenSq);

            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }
    }
}
