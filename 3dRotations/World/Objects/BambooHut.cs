using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class BambooHut
    {
        private const float HutScale = 1.15f;
        private const int StiltSegments = 4;
        private const int FloorPlankCount = 4;
        private const int WallSlatSections = 3;
        private const int RoofStripCount = 3;

        private static float hutWidth = 34f;
        private static float hutDepth = 28f;
        private static float floorHeight = 8f;
        private static float wallHeight = 18f;
        private static float roofHeight = 13f;
        private static float roofOverhang = 5f;

        private static float stiltRadius = 1.4f;
        private static float bambooRadius = 0.8f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0f, y = 0f, z = 16f };

        // Colors
        private static string bambooLight = "C9A85A";
        private static string bambooMid = "9C7A36";
        private static string bambooDark = "5E3F1F";

        private static string wallLight = "B9934A";
        private static string wallDark = "6F4A22";

        private static string roofLight = "7B8F3A";
        private static string roofMid = "566B2B";
        private static string roofDark = "2F3A1B";

        private static string insideDark = "11100C";
        private static string lanternGlow = "FFB347";

        public static _3dObject CreateBambooHut(ISurface parentSurface)
        {
            var hut = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "BambooHut",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -12, y = 0, z = -12 }
            };

            AddPart(hut, "BambooHutStilts", Stilts(), true);
            AddPart(hut, "BambooHutFloor", Floor(), true);
            AddPart(hut, "BambooHutWalls", Walls(), true);
            AddPart(hut, "BambooHutDoorOpening", DoorOpening(), true);
            AddPart(hut, "BambooHutRoof", PalmLeafRoof(), true);
            AddPart(hut, "BambooHutBambooDetails", BambooDetails(), true);
            AddPart(hut, "BambooHutLantern", Lantern(), true);

            hut.CrashBoxes = BambooHutCrashBoxes();
            hut.CrashBoxNames = new List<string?> { "HutBody", "Roof" };

            _3dObjectHelpers.AddCustomShadowPart(hut, BambooHutShadow());
            _3dObjectHelpers.ApplyScaleToObject(hut, HutScale);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(hut);

            return hut;
        }

        // ----------------------------------------------------
        //  STILTS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? Stilts()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = hutWidth / 2 - 4f;
            float y = hutDepth / 2 - 4f;

            AddBambooPole(tris, new Vector3 { x = -x, y = -y, z = 0f }, floorHeight + 1f, stiltRadius, StiltSegments);
            AddBambooPole(tris, new Vector3 { x = x, y = -y, z = 0f }, floorHeight + 1f, stiltRadius, StiltSegments);
            AddBambooPole(tris, new Vector3 { x = x, y = y, z = 0f }, floorHeight + 1f, stiltRadius, StiltSegments);
            AddBambooPole(tris, new Vector3 { x = -x, y = y, z = 0f }, floorHeight + 1f, stiltRadius, StiltSegments);

            // Cross braces
            AddBeam(tris,
                new Vector3 { x = -x, y = -y, z = 2.5f },
                new Vector3 { x = x, y = -y, z = 6.2f },
                0.6f,
                bambooDark);

            AddBeam(tris,
                new Vector3 { x = x, y = y, z = 2.5f },
                new Vector3 { x = -x, y = y, z = 6.2f },
                0.6f,
                bambooDark);

            return tris;
        }

        // ----------------------------------------------------
        //  FLOOR
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? Floor()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = hutWidth / 2;
            float y = hutDepth / 2;
            float z = floorHeight;

            var a = new Vector3 { x = -x, y = -y, z = z };
            var b = new Vector3 { x = x, y = -y, z = z };
            var c = new Vector3 { x = x, y = y, z = z };
            var d = new Vector3 { x = -x, y = y, z = z };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, bambooMid);

            // Plank strips
            int plankCount = FloorPlankCount;
            float plankWidth = hutDepth / plankCount;

            for (int i = 0; i < plankCount; i++)
            {
                float y1 = -y + i * plankWidth;
                float y2 = y1 + plankWidth * 0.82f;
                string color = i % 2 == 0 ? bambooLight : bambooMid;

                AddQuadOutward(
                    tris,
                    new Vector3 { x = -x + 1f, y = y1, z = z + 0.25f },
                    new Vector3 { x = x - 1f, y = y1, z = z + 0.25f },
                    new Vector3 { x = x - 1f, y = y2, z = z + 0.25f },
                    new Vector3 { x = -x + 1f, y = y2, z = z + 0.25f },
                    BodyCenter,
                    color);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  WALLS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? Walls()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = hutWidth / 2;
            float y = hutDepth / 2;
            float z0 = floorHeight;
            float z1 = floorHeight + wallHeight;

            // Back wall
            AddWallPanel(tris,
                new Vector3 { x = -x, y = y, z = z0 },
                new Vector3 { x = x, y = y, z = z0 },
                new Vector3 { x = x, y = y, z = z1 },
                new Vector3 { x = -x, y = y, z = z1 },
                wallLight);

            // Left wall
            AddWallPanel(tris,
                new Vector3 { x = -x, y = -y, z = z0 },
                new Vector3 { x = -x, y = y, z = z0 },
                new Vector3 { x = -x, y = y, z = z1 },
                new Vector3 { x = -x, y = -y, z = z1 },
                wallDark);

            // Right wall
            AddWallPanel(tris,
                new Vector3 { x = x, y = y, z = z0 },
                new Vector3 { x = x, y = -y, z = z0 },
                new Vector3 { x = x, y = -y, z = z1 },
                new Vector3 { x = x, y = y, z = z1 },
                wallDark);

            // Front wall split around door
            float doorHalfWidth = 5.5f;
            float doorHeight = 13f;
            float yf = -y;

            // Left front section
            AddWallPanel(tris,
                new Vector3 { x = -x, y = yf, z = z0 },
                new Vector3 { x = -doorHalfWidth, y = yf, z = z0 },
                new Vector3 { x = -doorHalfWidth, y = yf, z = z1 },
                new Vector3 { x = -x, y = yf, z = z1 },
                wallLight);

            // Right front section
            AddWallPanel(tris,
                new Vector3 { x = doorHalfWidth, y = yf, z = z0 },
                new Vector3 { x = x, y = yf, z = z0 },
                new Vector3 { x = x, y = yf, z = z1 },
                new Vector3 { x = doorHalfWidth, y = yf, z = z1 },
                wallLight);

            // Top over-door section
            AddWallPanel(tris,
                new Vector3 { x = -doorHalfWidth, y = yf, z = z0 + doorHeight },
                new Vector3 { x = doorHalfWidth, y = yf, z = z0 + doorHeight },
                new Vector3 { x = doorHalfWidth, y = yf, z = z1 },
                new Vector3 { x = -doorHalfWidth, y = yf, z = z1 },
                wallDark);

            return tris;
        }

        private static void AddWallPanel(
            List<ITriangleMeshWithColor> tris,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            string baseColor)
        {
            AddQuadOutward(tris, a, b, c, d, BodyCenter, baseColor);

            // Bamboo slats on panel
            int slats = WallSlatSections;

            for (int i = 1; i < slats; i++)
            {
                float t = i / (float)slats;

                var bottom = LerpVector(a, b, t);
                var top = LerpVector(d, c, t);

                AddFlatBeam(tris, bottom, top, 0.45f, bambooDark);
            }
        }

        public static List<ITriangleMeshWithColor>? DoorOpening()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float y = -hutDepth / 2 - 0.35f;
            float z0 = floorHeight;
            float z1 = floorHeight + 13f;
            float x = 5.5f;

            AddQuadOutward(
                tris,
                new Vector3 { x = -x, y = y, z = z0 },
                new Vector3 { x = x, y = y, z = z0 },
                new Vector3 { x = x, y = y, z = z1 },
                new Vector3 { x = -x, y = y, z = z1 },
                BodyCenter,
                insideDark,
                noHidden: true);

            return tris;
        }

        // ----------------------------------------------------
        //  ROOF
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? PalmLeafRoof()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = hutWidth / 2 + roofOverhang;
            float y = hutDepth / 2 + roofOverhang;
            float baseZ = floorHeight + wallHeight;
            float ridgeZ = baseZ + roofHeight;

            var frontLeft = new Vector3 { x = -x, y = -y, z = baseZ };
            var frontRight = new Vector3 { x = x, y = -y, z = baseZ };
            var backLeft = new Vector3 { x = -x, y = y, z = baseZ };
            var backRight = new Vector3 { x = x, y = y, z = baseZ };

            var ridgeFront = new Vector3 { x = 0f, y = -y - 1.5f, z = ridgeZ };
            var ridgeBack = new Vector3 { x = 0f, y = y + 1.5f, z = ridgeZ };

            // Main roof sides
            AddQuadOutward(tris, frontLeft, ridgeFront, ridgeBack, backLeft, BodyCenter, roofMid);
            AddQuadOutward(tris, ridgeFront, frontRight, backRight, ridgeBack, BodyCenter, roofDark);

            // Gables
            tris.Add(CreateTriangleOutward(frontLeft, frontRight, ridgeFront, BodyCenter, roofLight));
            tris.Add(CreateTriangleOutward(backLeft, ridgeBack, backRight, BodyCenter, roofDark));

            // Layered palm leaves / thatch strips
            int strips = RoofStripCount;
            for (int i = 0; i < strips; i++)
            {
                float t1 = i / (float)strips;
                float t2 = (i + 0.62f) / strips;

                float leftZ1 = Lerp(baseZ + 0.5f, ridgeZ - 1.0f, t1);
                float leftZ2 = Lerp(baseZ + 0.5f, ridgeZ - 1.0f, t2);

                float leftX1 = Lerp(-x, -1.5f, t1);
                float leftX2 = Lerp(-x, -1.5f, t2);

                AddQuadOutward(
                    tris,
                    new Vector3 { x = leftX1, y = -y - 0.4f, z = leftZ1 },
                    new Vector3 { x = leftX2, y = -y - 0.4f, z = leftZ2 },
                    new Vector3 { x = leftX2, y = y + 0.4f, z = leftZ2 },
                    new Vector3 { x = leftX1, y = y + 0.4f, z = leftZ1 },
                    BodyCenter,
                    i % 2 == 0 ? roofLight : roofMid);

                float rightX1 = Lerp(x, 1.5f, t1);
                float rightX2 = Lerp(x, 1.5f, t2);

                AddQuadOutward(
                    tris,
                    new Vector3 { x = rightX2, y = -y - 0.45f, z = leftZ2 },
                    new Vector3 { x = rightX1, y = -y - 0.45f, z = leftZ1 },
                    new Vector3 { x = rightX1, y = y + 0.45f, z = leftZ1 },
                    new Vector3 { x = rightX2, y = y + 0.45f, z = leftZ2 },
                    BodyCenter,
                    i % 2 == 0 ? roofDark : roofMid);
            }

            // Roof ridge bamboo
            AddBeam(
                tris,
                new Vector3 { x = 0f, y = -y - 2f, z = ridgeZ + 0.25f },
                new Vector3 { x = 0f, y = y + 2f, z = ridgeZ + 0.25f },
                0.85f,
                bambooDark);

            return tris;
        }

        // ----------------------------------------------------
        //  DETAILS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BambooDetails()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = hutWidth / 2;
            float y = hutDepth / 2;
            float z0 = floorHeight;
            float z1 = floorHeight + wallHeight;

            // Front horizontal beams
            AddBeam(tris, new Vector3 { x = -x, y = -y - 0.7f, z = z0 + 2f }, new Vector3 { x = x, y = -y - 0.7f, z = z0 + 2f }, bambooRadius, bambooMid);
            AddBeam(tris, new Vector3 { x = -x, y = -y - 0.7f, z = z1 - 2f }, new Vector3 { x = x, y = -y - 0.7f, z = z1 - 2f }, bambooRadius, bambooMid);

            // Side beams
            AddBeam(tris, new Vector3 { x = -x - 0.7f, y = -y, z = z1 - 2f }, new Vector3 { x = -x - 0.7f, y = y, z = z1 - 2f }, bambooRadius, bambooDark);
            AddBeam(tris, new Vector3 { x = x + 0.7f, y = -y, z = z1 - 2f }, new Vector3 { x = x + 0.7f, y = y, z = z1 - 2f }, bambooRadius, bambooDark);

            // Small front walkway / step
            AddQuadOutward(
                tris,
                new Vector3 { x = -7f, y = -y - 9f, z = floorHeight - 0.3f },
                new Vector3 { x = 7f, y = -y - 9f, z = floorHeight - 0.3f },
                new Vector3 { x = 7f, y = -y, z = floorHeight - 0.3f },
                new Vector3 { x = -7f, y = -y, z = floorHeight - 0.3f },
                BodyCenter,
                bambooMid);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? Lantern()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float y = -hutDepth / 2 - 1.2f;
            float z = floorHeight + 11f;

            var top = new Vector3 { x = 10f, y = y, z = z + 2f };
            var bottom = new Vector3 { x = 10f, y = y, z = z - 2f };
            var left = new Vector3 { x = 8.5f, y = y, z = z };
            var right = new Vector3 { x = 11.5f, y = y, z = z };

            tris.Add(CreateTriangleOutward(top, right, left, BodyCenter, lanternGlow));
            tris.Add(CreateTriangleOutward(bottom, left, right, BodyCenter, lanternGlow));

            return tris;
        }

        // ----------------------------------------------------
        //  CRASHBOX
        // ----------------------------------------------------

        public static List<List<IVector3>> BambooHutCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -hutWidth / 2 - 3f, y = -hutDepth / 2 - 3f, z = 0f },
                    new Vector3 { x = hutWidth / 2 + 3f, y = hutDepth / 2 + 3f, z = floorHeight + wallHeight }
                ),

                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -hutWidth / 2 - roofOverhang, y = -hutDepth / 2 - roofOverhang, z = floorHeight + wallHeight },
                    new Vector3 { x = hutWidth / 2 + roofOverhang, y = hutDepth / 2 + roofOverhang, z = floorHeight + wallHeight + roofHeight }
                )
            };
        }

        // ----------------------------------------------------
        //  SHADOW
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor> BambooHutShadow()
        {
            var tris = new List<ITriangleMeshWithColor>(10);
            const string sc = _3dObjectHelpers.ShadowColorHex;

            // Body
            var a = new Vector3 { x = -18f, y = 0f, z = 0f };
            var b = new Vector3 { x = 18f, y = 0f, z = 0f };
            var c = new Vector3 { x = 18f, y = 0f, z = 26f };
            var d = new Vector3 { x = -18f, y = 0f, z = 26f };

            tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = a, vert2 = b, vert3 = c });
            tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = a, vert2 = c, vert3 = d });

            // Roof
            var rL = new Vector3 { x = -24f, y = 0f, z = 26f };
            var rR = new Vector3 { x = 24f, y = 0f, z = 26f };
            var rT = new Vector3 { x = 0f, y = 0f, z = 40f };

            tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = rL, vert2 = rR, vert3 = rT });

            // Stilts
            AddShadowRect(tris, -15f, -12f, 0f, 9f, sc);
            AddShadowRect(tris, 12f, 15f, 0f, 9f, sc);

            // Front walkway
            tris.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -7f, y = 0f, z = 0f },
                vert2 = new Vector3 { x = 7f, y = 0f, z = 0f },
                vert3 = new Vector3 { x = 0f, y = 0f, z = -12f }
            });

            return tris;
        }

        private static void AddShadowRect(List<ITriangleMeshWithColor> tris, float x1, float x2, float z1, float z2, string color)
        {
            var a = new Vector3 { x = x1, y = 0f, z = z1 };
            var b = new Vector3 { x = x2, y = 0f, z = z1 };
            var c = new Vector3 { x = x2, y = 0f, z = z2 };
            var d = new Vector3 { x = x1, y = 0f, z = z2 };

            tris.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = b, vert3 = c });
            tris.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = c, vert3 = d });
        }

        // ----------------------------------------------------
        //  LOW-LEVEL HELPERS
        // ----------------------------------------------------

        private static void AddBambooPole(
            List<ITriangleMeshWithColor> tris,
            Vector3 baseCenter,
            float height,
            float radius,
            int segments)
        {
            var lower = CreateCircle(baseCenter, radius, segments);
            var upper = CreateCircle(new Vector3 { x = baseCenter.x, y = baseCenter.y, z = baseCenter.z + height }, radius * 0.85f, segments);

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                string color = i % 2 == 0 ? bambooLight : bambooMid;

                AddQuadOutward(tris, lower[i], lower[next], upper[next], upper[i], BodyCenter, color);
            }
        }

        private static List<Vector3> CreateCircle(Vector3 center, float radius, int segments)
        {
            var points = new List<Vector3>(segments);
            float step = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                points.Add(new Vector3
                {
                    x = center.x + MathF.Cos(a) * radius,
                    y = center.y + MathF.Sin(a) * radius,
                    z = center.z
                });
            }

            return points;
        }

        private static void AddBeam(
            List<ITriangleMeshWithColor> tris,
            Vector3 start,
            Vector3 end,
            float thickness,
            string color)
        {
            // Low-poly square-ish beam. Works best for bamboo/wood details.
            float dx = end.x - start.x;
            float dy = end.y - start.y;

            float len = MathF.Sqrt(dx * dx + dy * dy);
            float nx = len < 0.001f ? 1f : -dy / len;
            float ny = len < 0.001f ? 0f : dx / len;

            var a = new Vector3 { x = start.x + nx * thickness, y = start.y + ny * thickness, z = start.z + thickness };
            var b = new Vector3 { x = start.x - nx * thickness, y = start.y - ny * thickness, z = start.z - thickness };
            var c = new Vector3 { x = end.x - nx * thickness, y = end.y - ny * thickness, z = end.z - thickness };
            var d = new Vector3 { x = end.x + nx * thickness, y = end.y + ny * thickness, z = end.z + thickness };

            var a2 = new Vector3 { x = a.x, y = a.y, z = a.z - thickness * 2f };
            var d2 = new Vector3 { x = d.x, y = d.y, z = d.z - thickness * 2f };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, color);
            AddQuadOutward(tris, a2, d2, c, b, BodyCenter, bambooDark);
            AddQuadOutward(tris, a, d, d2, a2, BodyCenter, color);
        }

        private static void AddFlatBeam(
            List<ITriangleMeshWithColor> tris,
            Vector3 start,
            Vector3 end,
            float thickness,
            string color)
        {
            float dx = end.x - start.x;
            float dy = end.y - start.y;

            float len = MathF.Sqrt(dx * dx + dy * dy);
            float nx = len < 0.001f ? 1f : -dy / len;
            float ny = len < 0.001f ? 0f : dx / len;

            var a = new Vector3 { x = start.x + nx * thickness, y = start.y + ny * thickness, z = start.z };
            var b = new Vector3 { x = start.x - nx * thickness, y = start.y - ny * thickness, z = start.z };
            var c = new Vector3 { x = end.x - nx * thickness, y = end.y - ny * thickness, z = end.z };
            var d = new Vector3 { x = end.x + nx * thickness, y = end.y + ny * thickness, z = end.z };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, color);
        }

        private static Vector3 LerpVector(Vector3 a, Vector3 b, float t)
        {
            return new Vector3
            {
                x = Lerp(a.x, b.x, t),
                y = Lerp(a.y, b.y, t),
                z = Lerp(a.z, b.z, t)
            };
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
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
            return new Vector3
            {
                x = a.x - b.x,
                y = a.y - b.y,
                z = a.z - b.z
            };
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
