using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;
using static _3dRotations.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public static class Ship
    {
        private const float ZoomRatio = 1f;
        private const float ShipCrashBoxSizeMultiplier = 0.95f;
        private static readonly Vector3 ShipCrashBoxPadding = new() { x = 0f, y = 8f, z = 0.5f };
        private const float TopCannonCrashBoxSizeMultiplier = 0.35f;
        private static readonly Vector3 TopCannonCrashBoxPadding = new() { x = 0.5f, y = 2f, z = 1f };

        public static _3dObject CreateShip(ISurface? parentSurface, IObjectMovement? movement = null)
        {
            var upperTriangles = UpperTriangles();
            var lowerTriangles = LowerTriangles();
            var rearTriangles = RearTriangles();
            var rearEngineTriangles = RearEngineTriangles();
            var jetMotorTriangle = JetMotorTriangle();
            var jetMotorDirectionGuide = JetMotorDirectionGuide();
            var rearEngineDirectionGuide = RearEngineDirectionGuide();
            var cannon = TopCannonTriangles();
            var topCannonDirectionGuide = CannonDirectionGuide();
            var muzzleFlash = MuzzleFlashTriangles();
            var winglets = WingletTriangles();


            // Add orb as an inhabitant
            var ship = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            if (upperTriangles == null || lowerTriangles == null || rearTriangles == null) return ship;
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "UpperPart", Triangles = upperTriangles, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "LowerPart", Triangles = lowerTriangles, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "RearPart", Triangles = rearTriangles, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "RearEngine", Triangles = rearEngineTriangles!, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "JetMotor", Triangles = jetMotorTriangle!, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "JetMotorDirectionGuide", Triangles = jetMotorDirectionGuide!, IsVisible = false });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "RearEngineDirectionGuide", Triangles = rearEngineDirectionGuide!, IsVisible = false });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "TopCannon", Triangles = cannon!, IsVisible = true });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "WeaponDirectionGuide", Triangles = topCannonDirectionGuide!, IsVisible = false });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "WeaponStartGuide", Triangles = CannonStartGuide()!, IsVisible = false });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "MuzzleFlash", Triangles = muzzleFlash!, IsVisible = false });
            ship.ObjectParts.Add(new _3dObjectPart { PartName = "Winglets", Triangles = winglets!, IsVisible = true });

            var crashBoxes = new List<List<IVector3>>();
            crashBoxes.Add(CreateCrashBoxFromTriangles(
                upperTriangles
                    .Concat(lowerTriangles)
                    .Concat(rearTriangles)
                    .Concat(rearEngineTriangles!)
                    .Concat(jetMotorTriangle!)
                    .Concat(winglets!),
                ShipCrashBoxSizeMultiplier,
                ShipCrashBoxPadding));
            crashBoxes.Add(CreateCrashBoxFromTriangles(
                cannon!,
                TopCannonCrashBoxSizeMultiplier,
                TopCannonCrashBoxPadding));

            ship.ObjectOffsets = new Vector3 { };
            ship.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            ship.Movement = movement ?? new ShipControls();
            ship.Particles = new ParticlesAI();
            ship.ParentSurface = parentSurface;
            ship.CrashBoxes = crashBoxes;
            ship.HasShadow = true;

            _3dObjectHelpers.ApplyScaleToObject(ship, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);

            return ship;
        }

        private static List<IVector3> CreateCrashBoxFromTriangles(
            IEnumerable<ITriangleMeshWithColor> triangles,
            float sizeMultiplier,
            Vector3 padding)
        {
            var vertices = triangles
                .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
                .Cast<Vector3>()
                .ToList();

            if (vertices.Count == 0)
            {
                return new List<IVector3>();
            }

            float minX = vertices.Min(v => v.x);
            float maxX = vertices.Max(v => v.x);
            float minY = vertices.Min(v => v.y);
            float maxY = vertices.Max(v => v.y);
            float minZ = vertices.Min(v => v.z);
            float maxZ = vertices.Max(v => v.z);

            var center = new Vector3
            {
                x = (minX + maxX) / 2f,
                y = (minY + maxY) / 2f,
                z = (minZ + maxZ) / 2f
            };

            float halfX = ((maxX - minX) / 2f) * sizeMultiplier + padding.x;
            float halfY = ((maxY - minY) / 2f) * sizeMultiplier + padding.y;
            float halfZ = ((maxZ - minZ) / 2f) * sizeMultiplier + padding.z;

            var min = new Vector3 { x = center.x - halfX, y = center.y - halfY, z = center.z - halfZ };
            var max = new Vector3 { x = center.x + halfX, y = center.y + halfY, z = center.z + halfZ };

            return _3dObjectHelpers.GenerateCrashBoxCorners(min, max);
        }

        public static List<ITriangleMeshWithColor>? TopCannonTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var back = new Vector3 { x = 0f, y = 20f, z = 28f };   // thick end
            var mid = new Vector3 { x = 0f, y = -10f, z = 28f };   // mid ring
            var front = new Vector3 { x = 0f, y = -45f, z = 28f }; // muzzle

            float rxB = 10f, ryB = 5f;
            float rxM = 6f, ryM = 3.5f;
            float rxF = 3f, ryF = 2f;

            float dx = front.x - back.x, dy = front.y - back.y, dz = front.z - back.z;
            float len = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-4f) return tris;
            float wx = dx / len, wy = dy / len, wz = dz / len;

            float tx = 1f, ty = 0f, tz = 0f;
            if (System.Math.Abs(wx) > 0.9f) { tx = 0f; ty = 1f; tz = 0f; }

            float ux = wy * tz - wz * ty;
            float uy = wz * tx - wx * tz;
            float uz = wx * ty - wy * tx;
            float ulen = (float)System.Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ulen; uy /= ulen; uz /= ulen;

            float vx = wy * uz - wz * uy;
            float vy = wz * ux - wx * uz;
            float vz = wx * uy - wy * ux;
            if (vz > 0f) { vx = -vx; vy = -vy; vz = -vz; }

            float[] ca = { 1f, 0.70710678f, 0f, -0.70710678f, -1f, -0.70710678f, 0f, 0.70710678f };
            float[] sa = { 0f, -0.70710678f, -1f, -0.70710678f, 0f, 0.70710678f, 1f, 0.70710678f };

            Vector3[] B = new Vector3[8];
            Vector3[] M = new Vector3[8];
            Vector3[] F = new Vector3[8];

            for (int i = 0; i < 8; i++)
            {
                B[i] = new Vector3
                {
                    x = back.x + ux * (rxB * ca[i]) + vx * (ryB * sa[i]),
                    y = back.y + uy * (rxB * ca[i]) + vy * (ryB * sa[i]),
                    z = back.z + uz * (rxB * ca[i]) + vz * (ryB * sa[i])
                };
                M[i] = new Vector3
                {
                    x = mid.x + ux * (rxM * ca[i]) + vx * (ryM * sa[i]),
                    y = mid.y + uy * (rxM * ca[i]) + vy * (ryM * sa[i]),
                    z = mid.z + uz * (rxM * ca[i]) + vz * (ryM * sa[i])
                };
                F[i] = new Vector3
                {
                    x = front.x + ux * (rxF * ca[i]) + vx * (ryF * sa[i]),
                    y = front.y + uy * (rxF * ca[i]) + vy * (ryF * sa[i]),
                    z = front.z + uz * (rxF * ca[i]) + vz * (ryF * sa[i])
                };
            }

            var barrelCenter = new Vector3
            {
                x = (back.x + front.x) / 2f,
                y = (back.y + front.y) / 2f,
                z = (back.z + front.z) / 2f
            };

            string[] backToMidColors = { "8A8A8A", "777777" };
            string[] midToFrontColors = { "909090", "7A7A7A" };
            string frontCapColor = "B8B8B8";
            string mountColor = "445A77";

            // Back-to-mid barrel section (right-hand rule enforced)
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                string col = backToMidColors[i % 2];
                tris.Add(CreateTriangleOutward(B[i], B[j], M[j], barrelCenter, col));
                tris.Add(CreateTriangleOutward(B[i], M[j], M[i], barrelCenter, col));
            }

            // Mid-to-front barrel section (right-hand rule enforced)
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                string col = midToFrontColors[i % 2];
                tris.Add(CreateTriangleOutward(M[i], M[j], F[j], barrelCenter, col));
                tris.Add(CreateTriangleOutward(M[i], F[j], F[i], barrelCenter, col));
            }

            // Front cap (right-hand rule enforced)
            var Fc = new Vector3 { x = front.x + wx * 0.2f, y = front.y + wy * 0.2f, z = front.z + wz * 0.2f };
            int[] idx = { 0, 2, 4, 6 };
            for (int k = 0; k < 4; k++)
            {
                int a = idx[k], b = idx[(k + 1) % 4];
                tris.Add(CreateTriangleOutward(F[a], F[b], Fc, barrelCenter, frontCapColor));
            }

            // Mount bracket
            float footHalfX = 6f;
            float footUp = 2f;
            float footDown = 4.5f;
            float footAhead = 6f;

            var T1 = new Vector3 { x = back.x + ux * (+footHalfX) + vx * (-footUp), y = back.y + uy * (+footHalfX) + vy * (-footUp), z = back.z + uz * (+footHalfX) + vz * (-footUp) };
            var T2 = new Vector3 { x = back.x + ux * (-footHalfX) + vx * (-footUp), y = back.y + uy * (-footHalfX) + vy * (-footUp), z = back.z + uz * (-footHalfX) + vz * (-footUp) };
            var T3 = new Vector3 { x = back.x + wx * (footAhead) + vx * (-footUp), y = back.y + wy * (footAhead) + vy * (-footUp), z = back.z + wz * (footAhead) + vz * (-footUp) };

            var B1p = new Vector3 { x = T1.x + vx * (footDown + footUp), y = T1.y + vy * (footDown + footUp), z = T1.z + vz * (footDown + footUp) };
            var B2p = new Vector3 { x = T2.x + vx * (footDown + footUp), y = T2.y + vy * (footDown + footUp), z = T2.z + vz * (footDown + footUp) };
            var B3p = new Vector3 { x = T3.x + vx * (footDown + footUp), y = T3.y + vy * (footDown + footUp), z = T3.z + vz * (footDown + footUp) };

            var mountCenter = new Vector3
            {
                x = (T1.x + T2.x + T3.x + B1p.x + B2p.x + B3p.x) / 6f,
                y = (T1.y + T2.y + T3.y + B1p.y + B2p.y + B3p.y) / 6f,
                z = (T1.z + T2.z + T3.z + B1p.z + B2p.z + B3p.z) / 6f
            };

            // Side faces (right-hand rule enforced)
            tris.Add(CreateTriangleOutward(T1, T2, B2p, mountCenter, mountColor));
            tris.Add(CreateTriangleOutward(T1, B2p, B1p, mountCenter, mountColor));

            tris.Add(CreateTriangleOutward(T2, T3, B3p, mountCenter, mountColor));
            tris.Add(CreateTriangleOutward(T2, B3p, B2p, mountCenter, mountColor));

            tris.Add(CreateTriangleOutward(T3, T1, B1p, mountCenter, mountColor));
            tris.Add(CreateTriangleOutward(T3, B1p, B3p, mountCenter, mountColor));

            // Top and bottom caps
            tris.Add(CreateTriangleOutward(T1, T3, T2, mountCenter, mountColor));
            tris.Add(CreateTriangleOutward(B1p, B2p, B3p, mountCenter, mountColor));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? UpperTriangles()
        {
            var noseLeft = new Vector3 { x = -50, y = -50, z = 0 };
            var noseRight = new Vector3 { x = 50, y = -50, z = 0 };
            var peak = new Vector3 { x = 0, y = 50, z = 25 };
            var wingTipLeft = new Vector3 { x = -65, y = 45, z = 0 };
            var wingTipRight = new Vector3 { x = 65, y = 45, z = 0 };

            // Edge midpoints pushed outward for rounding
            var noseFront = new Vector3 { x = 0, y = -50, z = 0 };
            var leftInner = new Vector3 { x = -27, y = 0, z = 16 };
            var rightInner = new Vector3 { x = 27, y = 0, z = 16 };
            var leftWingEdge = new Vector3 { x = -60, y = -3, z = 0 };
            var rightWingEdge = new Vector3 { x = 60, y = -3, z = 0 };
            var leftRearWing = new Vector3 { x = -35, y = 48, z = 15 };
            var rightRearWing = new Vector3 { x = 35, y = 48, z = 15 };

            var upper = new List<ITriangleMeshWithColor>
            {
                // Center panels
                new TriangleMeshWithColor { Color = "00cc00", vert1 = noseLeft, vert2 = noseFront, vert3 = leftInner },
                new TriangleMeshWithColor { Color = "00cc00", vert1 = noseFront, vert2 = noseRight, vert3 = rightInner },
                new TriangleMeshWithColor { Color = "00dd00", vert1 = noseFront, vert2 = rightInner, vert3 = leftInner },
                new TriangleMeshWithColor { Color = "00ee00", vert1 = leftInner, vert2 = rightInner, vert3 = peak },
                // Left wing panels
                new TriangleMeshWithColor { Color = "008800", vert1 = noseLeft, vert2 = leftInner, vert3 = leftWingEdge },
                new TriangleMeshWithColor { Color = "008800", vert1 = leftInner, vert2 = peak, vert3 = leftRearWing },
                new TriangleMeshWithColor { Color = "007700", vert1 = leftInner, vert2 = leftRearWing, vert3 = leftWingEdge },
                new TriangleMeshWithColor { Color = "006600", vert1 = leftWingEdge, vert2 = leftRearWing, vert3 = wingTipLeft },
                // Right wing panels
                new TriangleMeshWithColor { Color = "008800", vert1 = noseRight, vert2 = rightWingEdge, vert3 = rightInner },
                new TriangleMeshWithColor { Color = "006600", vert1 = rightWingEdge, vert2 = wingTipRight, vert3 = rightRearWing },
                new TriangleMeshWithColor { Color = "007700", vert1 = rightWingEdge, vert2 = rightRearWing, vert3 = rightInner },
                new TriangleMeshWithColor { Color = "008800", vert1 = rightInner, vert2 = rightRearWing, vert3 = peak },
            };
            return upper;
        }
        public static List<ITriangleMeshWithColor>? LowerTriangles()
        {
            var noseLeft = new Vector3 { x = -50, y = -50, z = 0 };
            var noseRight = new Vector3 { x = 50, y = -50, z = 0 };
            var noseMid = new Vector3 { x = 0, y = -50, z = 0 };
            var bottomPeak = new Vector3 { x = 0, y = 50, z = -25 };
            var wingTipLeft = new Vector3 { x = -65, y = 45, z = 0 };
            var wingTipRight = new Vector3 { x = 65, y = 45, z = 0 };
            var keelLeft = new Vector3 { x = -25, y = 0, z = -12 };
            var keelRight = new Vector3 { x = 25, y = 0, z = -12 };

            // Edge midpoints pushed outward (downward) for rounding
            var leftLowerWingEdge = new Vector3 { x = -60, y = -3, z = 0 };
            var leftLowerInner = new Vector3 { x = -27, y = 0, z = -15 };
            var leftLowerRear = new Vector3 { x = -35, y = 48, z = -15 };
            var rightLowerWingEdge = new Vector3 { x = 60, y = -3, z = 0 };
            var rightLowerInner = new Vector3 { x = 27, y = 0, z = -15 };
            var rightLowerRear = new Vector3 { x = 35, y = 48, z = -15 };

            var lower = new List<ITriangleMeshWithColor>
            {
                // Left lower wing panels
                new TriangleMeshWithColor { Color = "006688", vert1 = wingTipLeft, vert2 = leftLowerRear, vert3 = leftLowerWingEdge },
                new TriangleMeshWithColor { Color = "007799", vert1 = leftLowerRear, vert2 = bottomPeak, vert3 = leftLowerInner },
                new TriangleMeshWithColor { Color = "006688", vert1 = leftLowerRear, vert2 = leftLowerInner, vert3 = leftLowerWingEdge },
                new TriangleMeshWithColor { Color = "007799", vert1 = leftLowerWingEdge, vert2 = leftLowerInner, vert3 = noseLeft },
                // Right lower wing panels
                new TriangleMeshWithColor { Color = "007799", vert1 = noseRight, vert2 = rightLowerInner, vert3 = rightLowerWingEdge },
                new TriangleMeshWithColor { Color = "007799", vert1 = rightLowerInner, vert2 = bottomPeak, vert3 = rightLowerRear },
                new TriangleMeshWithColor { Color = "006688", vert1 = rightLowerInner, vert2 = rightLowerRear, vert3 = rightLowerWingEdge },
                new TriangleMeshWithColor { Color = "006688", vert1 = rightLowerWingEdge, vert2 = rightLowerRear, vert3 = wingTipRight },
                // Front bottom keel panels
                new TriangleMeshWithColor { Color = "00ff99", vert1 = noseLeft, vert2 = keelLeft, vert3 = noseMid },
                new TriangleMeshWithColor { Color = "00ff99", vert1 = noseMid, vert2 = keelRight, vert3 = noseRight },
                new TriangleMeshWithColor { Color = "00dd88", vert1 = keelLeft, vert2 = keelRight, vert3 = noseMid },
                // Belly panels connecting keel to wing undersurface
                // (engine-adjacent belly panels moved to JetMotor part for thrust-based color control)
                new TriangleMeshWithColor { Color = "00dd88", vert1 = noseLeft, vert2 = leftLowerInner, vert3 = keelLeft },
                new TriangleMeshWithColor { Color = "00dd88", vert1 = noseRight, vert2 = keelRight, vert3 = rightLowerInner },
            };
            return lower;
        }
        public static List<ITriangleMeshWithColor>? JetMotorTriangle()
        {
            var jet = new List<ITriangleMeshWithColor>
            {
                // Main engine triangle
                new TriangleMeshWithColor { Color = "ffff00", vert1 = { x = 25, y = 0, z = -12 }, vert2 = { x = -25, y = 0, z = -12 }, vert3 = { x = 0, y = 50, z = -25 } },
                // Left engine glow panel (keelLeft -> leftLowerInner -> bottomPeak)
                new TriangleMeshWithColor { Color = "ffff00", vert1 = { x = -25, y = 0, z = -12 }, vert2 = { x = -27, y = 0, z = -15 }, vert3 = { x = 0, y = 50, z = -25 } },
                // Right engine glow panel (keelRight -> bottomPeak -> rightLowerInner)
                new TriangleMeshWithColor { Color = "ffff00", vert1 = { x = 25, y = 0, z = -12 }, vert2 = { x = 0, y = 50, z = -25 }, vert3 = { x = 27, y = 0, z = -15 } },
            };
            return jet;
        }

        public static List<ITriangleMeshWithColor>? JetMotorDirectionGuide()
        {
            var jet = new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor { Color = "ffffff", vert1 = { x = 12, y = 0, z = -100 }, vert2 = { x = -12, y = 0, z = -100 }, vert3 = { x = 0, y = 50, z = -100 } },
            };
            return jet;
        }

        public static List<ITriangleMeshWithColor>? CannonStartGuide()
        {
            // 30 units *inside* the cannon tip: front (muzzle) is at y = -45 → -45 + 30 = -15
            const float yInside = 40f;   // inside the barrel (toward +Y)
            const float widthX = 8f;     // narrower than muzzle to stay well inside
            const float zBase = 14f;    // cannon height
            const float zTipUp = 50f;    // vertical tip for visibility

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor
                {
                    Color = "ffffff",
                    // Flat, far-style guide: all verts share same Y (yInside)
                    vert1 = { x =  widthX, y = yInside, z =  zBase  },
                    vert2 = { x = -widthX, y = yInside, z =  zBase  },
                    vert3 = { x =       0, y = yInside, z =  zTipUp }
                }
            };
        }

        public static List<ITriangleMeshWithColor>? CannonDirectionGuide()
        {
            const float yFar = -200f; // far ahead of the muzzle along -Y
            const float widthX = 12f;   // half-width in X
            const float zBase = 14f;   // cannon height
            const float zTipUp = 58f;   // tip offset in Z to form a tall triangle

            var guide = new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor
                {
                    Color = "ffffff",
                    // Base edge (left→right) and an upward tip in Z, all at the same far Y
                    vert1 = { x =  widthX, y = yFar, z =  zBase     },
                    vert2 = { x = -widthX, y = yFar, z =  zBase     },
                    vert3 = { x =       0, y = yFar, z =  zTipUp    }
                }
            };
            return guide;
        }

        public static List<ITriangleMeshWithColor>? MuzzleFlashTriangles()
        {
            var center = new Vector3 { x = 0f, y = -48f, z = 28f };
            var front = new Vector3 { x = 0f, y = -72f, z = 28f };
            var left = new Vector3 { x = -9f, y = -58f, z = 29f };
            var right = new Vector3 { x = 9f, y = -58f, z = 29f };
            var top = new Vector3 { x = 0f, y = -58f, z = 39f };
            var bottom = new Vector3 { x = 0f, y = -58f, z = 17f };

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor { Color = "fff8c8", vert1 = center, vert2 = left, vert3 = front, noHidden = true },
                new TriangleMeshWithColor { Color = "ffb020", vert1 = center, vert2 = front, vert3 = right, noHidden = true },
                new TriangleMeshWithColor { Color = "fff8c8", vert1 = center, vert2 = front, vert3 = top, noHidden = true },
                new TriangleMeshWithColor { Color = "ff7a12", vert1 = center, vert2 = bottom, vert3 = front, noHidden = true },
            };
        }

        public static List<ITriangleMeshWithColor>? RearTriangles()
        {
            var bottomPeak = new Vector3 { x = 0, y = 50, z = -25 };
            var topPeak = new Vector3 { x = 0, y = 50, z = 25 };
            var wingTipLeft = new Vector3 { x = -65, y = 45, z = 0 };
            var wingTipRight = new Vector3 { x = 65, y = 45, z = 0 };

            // Edge midpoints pushed outward for rounding
            var leftRearMid = new Vector3 { x = -38, y = 60, z = 0 };
            var rightRearMid = new Vector3 { x = 38, y = 60, z = 0 };

            // Boundary midpoints matching upper/lower hull for seamless edges
            var leftRearWing = new Vector3 { x = -35, y = 48, z = 15 };
            var rightRearWing = new Vector3 { x = 35, y = 48, z = 15 };
            var leftLowerRear = new Vector3 { x = -35, y = 48, z = -15 };
            var rightLowerRear = new Vector3 { x = 35, y = 48, z = -15 };

            var rear = new List<ITriangleMeshWithColor>
            {
                // Lower-right rear (center engine triangles in separate RearEngine part)
                new TriangleMeshWithColor { Color = "ff0000", vert1 = bottomPeak, vert2 = rightRearMid, vert3 = rightLowerRear },
                new TriangleMeshWithColor { Color = "ff0000", vert1 = rightLowerRear, vert2 = rightRearMid, vert3 = wingTipRight },
                // Lower-left rear
                new TriangleMeshWithColor { Color = "ff0000", vert1 = wingTipLeft, vert2 = leftRearMid, vert3 = leftLowerRear },
                new TriangleMeshWithColor { Color = "cc0000", vert1 = leftLowerRear, vert2 = leftRearMid, vert3 = bottomPeak },
                // Upper-right rear (center engine triangles in separate RearEngine part)
                new TriangleMeshWithColor { Color = "ee2200", vert1 = rightRearMid, vert2 = topPeak, vert3 = rightRearWing },
                new TriangleMeshWithColor { Color = "ee2200", vert1 = rightRearMid, vert2 = rightRearWing, vert3 = wingTipRight },
                // Upper-left rear
                new TriangleMeshWithColor { Color = "ee2200", vert1 = wingTipLeft, vert2 = leftRearWing, vert3 = leftRearMid },
                new TriangleMeshWithColor { Color = "ee2200", vert1 = leftRearWing, vert2 = topPeak, vert3 = leftRearMid },
            };
            return rear;
        }

        public static List<ITriangleMeshWithColor>? RearEngineTriangles()
        {
            var bottomPeak = new Vector3 { x = 0, y = 50, z = -25 };
            var topPeak = new Vector3 { x = 0, y = 50, z = 25 };
            var tail = new Vector3 { x = 0, y = 70, z = 0 };
            var leftRearMid = new Vector3 { x = -38, y = 60, z = 0 };
            var rightRearMid = new Vector3 { x = 38, y = 60, z = 0 };

            return new List<ITriangleMeshWithColor>
            {
                // Lower rear engine
                new TriangleMeshWithColor { Color = "ffff00", vert1 = bottomPeak, vert2 = tail, vert3 = rightRearMid },
                new TriangleMeshWithColor { Color = "ffff00", vert1 = leftRearMid, vert2 = tail, vert3 = bottomPeak },
                // Upper rear engine
                new TriangleMeshWithColor { Color = "ffff00", vert1 = tail, vert2 = topPeak, vert3 = rightRearMid },
                new TriangleMeshWithColor { Color = "ffff00", vert1 = leftRearMid, vert2 = topPeak, vert3 = tail },
            };
        }

        public static List<ITriangleMeshWithColor>? RearEngineDirectionGuide()
        {
            var guide = new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor { Color = "ffffff", vert1 = { x = 12, y = 200, z = 0 }, vert2 = { x = -12, y = 200, z = 0 }, vert3 = { x = 0, y = 200, z = 25 } },
            };
            return guide;
        }

        public static List<ITriangleMeshWithColor>? WingletTriangles()
        {
            var lBase1 = new Vector3 { x = -58, y = 35, z = 0 };
            var lBase2 = new Vector3 { x = -65, y = 45, z = 0 };
            var lTop = new Vector3 { x = -62, y = 42, z = 15 };

            var rBase1 = new Vector3 { x = 58, y = 35, z = 0 };
            var rBase2 = new Vector3 { x = 65, y = 45, z = 0 };
            var rTop = new Vector3 { x = 62, y = 42, z = 15 };

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor { Color = "005522", vert1 = lBase1, vert2 = lBase2, vert3 = lTop },
                new TriangleMeshWithColor { Color = "007733", vert1 = lBase2, vert2 = lBase1, vert3 = lTop },
                new TriangleMeshWithColor { Color = "005522", vert1 = rBase2, vert2 = rBase1, vert3 = rTop },
                new TriangleMeshWithColor { Color = "007733", vert1 = rBase1, vert2 = rBase2, vert3 = rTop },
            };
        }
    }
}
