using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public class BomberBomb
    {
        private const float ZoomRatio = 1f;
        private const float CrashboxSize = 6f;

        // ----------------------------------------------------
        //  SIZE / PROPORTIONS
        //  Designed to fit ZeppelinBomber hatch:
        //  bayHalfWidth ≈ 4.6, so bomb stays compact.
        // ----------------------------------------------------

        private static int bodySegments = 8;

        private static float noseTipX = 6.2f;
        private static float noseBaseX = 3.8f;

        private static float bodyFrontX = 3.8f;
        private static float bodyBackX = -4.8f;

        private static float bodyRadiusY = 1.7f;
        private static float bodyRadiusZ = 1.7f;

        private static float tailBaseX = -4.8f;
        private static float tailEndX = -6.9f;

        private static float tailRingRadiusY = 1.25f;
        private static float tailRingRadiusZ = 1.25f;

        // ----------------------------------------------------
        //  TAIL FINS
        // ----------------------------------------------------

        private static float finRootX = -5.0f;
        private static float finTipX = -7.8f;

        private static float finRootOffset = 0.72f;
        private static float finTipOffset = 2.0f;

        private static float finHeight = 1.8f;
        private static float finThickness = 0.16f;

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string bodyColor = "4D5C3A";
        private static string bodyColorLight = "64774B";
        private static string bodyColorDark = "38432B";
        private static string noseColor = "717C5A";
        private static string finColor = "586442";
        private static string finDark = "313828";
        private static string tailBandColor = "9A9378";

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0f, y = 0f, z = 0f };

        public static _3dObject CreateBomberBomb(ISurface parentSurface)
        {
            var nose = BombNose();
            var body = BombBody();
            var tailTransition = BombTailTransition();
            var fins = BombTailFins();

            var crashBoxes = BombCrashBoxes();

            var bomb = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "BomberBomb"
            };

            AddPart(bomb, "BombNose", nose, true);
            AddPart(bomb, "BombBody", body, true);
            AddPart(bomb, "BombTailTransition", tailTransition, true);
            AddPart(bomb, "BombTailFins", fins, true);

            bomb.Particles = new ParticlesAI();
            bomb.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            bomb.ParentSurface = parentSurface;
            bomb.HasShadow = true;

            if (crashBoxes != null)
                bomb.CrashBoxes = crashBoxes;

            _3dObjectHelpers.ApplyScaleToObject(bomb, ZoomRatio);
            _3dObjectHelpers.AddSimplifiedShadowPart(bomb, useFlatQuad: true);

            return bomb;
        }

        // ----------------------------------------------------
        //  NOSE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BombNose()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var baseRing = GenerateEllipseRing(bodySegments, noseBaseX, bodyRadiusY, bodyRadiusZ);
            var tip = new Vector3 { x = noseTipX, y = 0f, z = 0f };

            for (int i = 0; i < bodySegments; i++)
            {
                int next = (i + 1) % bodySegments;

                string color = GetRadialColor(baseRing[i], baseRing[next], noseColor, bodyColorLight, bodyColorDark);
                tris.Add(CreateTriangleOutward(tip, baseRing[next], baseRing[i], BodyCenter, color));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BombBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var frontRing = GenerateEllipseRing(bodySegments, bodyFrontX, bodyRadiusY, bodyRadiusZ);
            var backRing = GenerateEllipseRing(bodySegments, bodyBackX, bodyRadiusY * 0.98f, bodyRadiusZ * 0.98f);

            for (int i = 0; i < bodySegments; i++)
            {
                int next = (i + 1) % bodySegments;

                string color = GetRadialColor(frontRing[i], frontRing[next], bodyColorLight, bodyColor, bodyColorDark);
                AddQuadOutward(tris, frontRing[i], frontRing[next], backRing[next], backRing[i], BodyCenter, color);
            }

            // Simple tail band to break up the body visually
            float bandFrontX = -3.7f;
            float bandBackX = -4.2f;

            var bandFront = GenerateEllipseRing(bodySegments, bandFrontX, bodyRadiusY * 1.01f, bodyRadiusZ * 1.01f);
            var bandBack = GenerateEllipseRing(bodySegments, bandBackX, bodyRadiusY * 1.03f, bodyRadiusZ * 1.03f);

            for (int i = 0; i < bodySegments; i++)
            {
                int next = (i + 1) % bodySegments;
                AddQuadOutward(tris, bandFront[i], bandFront[next], bandBack[next], bandBack[i], BodyCenter, tailBandColor);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  TAIL TRANSITION
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BombTailTransition()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var bodyRing = GenerateEllipseRing(bodySegments, tailBaseX, bodyRadiusY * 0.98f, bodyRadiusZ * 0.98f);
            var tailRing = GenerateEllipseRing(bodySegments, tailEndX, tailRingRadiusY, tailRingRadiusZ);

            for (int i = 0; i < bodySegments; i++)
            {
                int next = (i + 1) % bodySegments;
                string color = GetRadialColor(bodyRing[i], bodyRing[next], finColor, finColor, finDark);
                AddQuadOutward(tris, bodyRing[i], bodyRing[next], tailRing[next], tailRing[i], BodyCenter, color);
            }

            // Tail closure cap
            var tailTip = new Vector3 { x = tailEndX - 0.6f, y = 0f, z = 0f };
            for (int i = 0; i < bodySegments; i++)
            {
                int next = (i + 1) % bodySegments;
                tris.Add(CreateTriangleOutward(tailRing[i], tailRing[next], tailTip, BodyCenter, finDark));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  TAIL FINS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BombTailFins()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddVerticalTopFin(tris);
            AddVerticalBottomFin(tris);
            AddHorizontalRightFin(tris);
            AddHorizontalLeftFin(tris);

            return tris;
        }

        private static void AddVerticalTopFin(List<ITriangleMeshWithColor> tris)
        {
            var a = new Vector3 { x = finRootX, y = -finThickness, z = finRootOffset };
            var b = new Vector3 { x = finRootX, y = finThickness, z = finRootOffset };
            var c = new Vector3 { x = finTipX, y = finThickness, z = finTipOffset };
            var d = new Vector3 { x = finTipX, y = -finThickness, z = finTipOffset };

            var a2 = new Vector3 { x = finRootX, y = -finThickness, z = finRootOffset + finHeight };
            var b2 = new Vector3 { x = finRootX, y = finThickness, z = finRootOffset + finHeight };
            var c2 = new Vector3 { x = finTipX, y = finThickness, z = finTipOffset };
            var d2 = new Vector3 { x = finTipX, y = -finThickness, z = finTipOffset };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, finColor);
            AddQuadOutward(tris, a2, d2, c2, b2, BodyCenter, finDark);
            AddQuadOutward(tris, a, d, d2, a2, BodyCenter, finColor);
            AddQuadOutward(tris, b, b2, c2, c, BodyCenter, finDark);
            AddQuadOutward(tris, a, a2, b2, b, BodyCenter, finDark);
        }

        private static void AddVerticalBottomFin(List<ITriangleMeshWithColor> tris)
        {
            var a = new Vector3 { x = finRootX, y = -finThickness, z = -finRootOffset };
            var b = new Vector3 { x = finRootX, y = finThickness, z = -finRootOffset };
            var c = new Vector3 { x = finTipX, y = finThickness, z = -finTipOffset };
            var d = new Vector3 { x = finTipX, y = -finThickness, z = -finTipOffset };

            var a2 = new Vector3 { x = finRootX, y = -finThickness, z = -finRootOffset - finHeight };
            var b2 = new Vector3 { x = finRootX, y = finThickness, z = -finRootOffset - finHeight };
            var c2 = new Vector3 { x = finTipX, y = finThickness, z = -finTipOffset };
            var d2 = new Vector3 { x = finTipX, y = -finThickness, z = -finTipOffset };

            AddQuadOutward(tris, a, d, c, b, BodyCenter, finColor);
            AddQuadOutward(tris, a2, b2, c2, d2, BodyCenter, finDark);
            AddQuadOutward(tris, a, a2, d2, d, BodyCenter, finColor);
            AddQuadOutward(tris, b, c, c2, b2, BodyCenter, finDark);
            AddQuadOutward(tris, a, b, b2, a2, BodyCenter, finDark);
        }

        private static void AddHorizontalRightFin(List<ITriangleMeshWithColor> tris)
        {
            var a = new Vector3 { x = finRootX, y = finRootOffset, z = -finThickness };
            var b = new Vector3 { x = finRootX, y = finRootOffset, z = finThickness };
            var c = new Vector3 { x = finTipX, y = finTipOffset, z = finThickness };
            var d = new Vector3 { x = finTipX, y = finTipOffset, z = -finThickness };

            var a2 = new Vector3 { x = finRootX, y = finRootOffset + finHeight, z = -finThickness };
            var b2 = new Vector3 { x = finRootX, y = finRootOffset + finHeight, z = finThickness };
            var c2 = new Vector3 { x = finTipX, y = finTipOffset, z = finThickness };
            var d2 = new Vector3 { x = finTipX, y = finTipOffset, z = -finThickness };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, finColor);
            AddQuadOutward(tris, a2, d2, c2, b2, BodyCenter, finDark);
            AddQuadOutward(tris, a, d, d2, a2, BodyCenter, finColor);
            AddQuadOutward(tris, b, b2, c2, c, BodyCenter, finDark);
            AddQuadOutward(tris, a, a2, b2, b, BodyCenter, finDark);
        }

        private static void AddHorizontalLeftFin(List<ITriangleMeshWithColor> tris)
        {
            var a = new Vector3 { x = finRootX, y = -finRootOffset, z = -finThickness };
            var b = new Vector3 { x = finRootX, y = -finRootOffset, z = finThickness };
            var c = new Vector3 { x = finTipX, y = -finTipOffset, z = finThickness };
            var d = new Vector3 { x = finTipX, y = -finTipOffset, z = -finThickness };

            var a2 = new Vector3 { x = finRootX, y = -finRootOffset - finHeight, z = -finThickness };
            var b2 = new Vector3 { x = finRootX, y = -finRootOffset - finHeight, z = finThickness };
            var c2 = new Vector3 { x = finTipX, y = -finTipOffset, z = finThickness };
            var d2 = new Vector3 { x = finTipX, y = -finTipOffset, z = -finThickness };

            AddQuadOutward(tris, a, d, c, b, BodyCenter, finColor);
            AddQuadOutward(tris, a2, b2, c2, d2, BodyCenter, finDark);
            AddQuadOutward(tris, a, a2, d2, d, BodyCenter, finColor);
            AddQuadOutward(tris, b, c, c2, b2, BodyCenter, finDark);
            AddQuadOutward(tris, a, b, b2, a2, BodyCenter, finDark);
        }

        // ----------------------------------------------------
        //  COLLISION
        // ----------------------------------------------------

        public static List<List<IVector3>>? BombCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var mainBounds = ScaleCrashBoxBounds(
                new Vector3 { x = bodyBackX, y = -bodyRadiusY - 0.25f, z = -bodyRadiusZ - 0.25f },
                new Vector3 { x = noseTipX, y = bodyRadiusY + 0.25f, z = bodyRadiusZ + 0.25f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(mainBounds.min, mainBounds.max));

            var tailBounds = ScaleCrashBoxBounds(
                new Vector3 { x = finTipX - 0.2f, y = -finTipOffset - finHeight, z = -finTipOffset - finHeight },
                new Vector3 { x = finRootX + 0.4f, y = finTipOffset + finHeight, z = finTipOffset + finHeight });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(tailBounds.min, tailBounds.max));

            return boxes;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> GenerateEllipseRing(int segments, float x, float radiusY, float radiusZ)
        {
            var points = new List<Vector3>(segments);
            float step = (float)(2 * Math.PI / segments);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;
                points.Add(new Vector3
                {
                    x = x,
                    y = radiusY * (float)Math.Cos(angle),
                    z = radiusZ * (float)Math.Sin(angle)
                });
            }

            return points;
        }

        private static string GetRadialColor(Vector3 a, Vector3 b, string topColor, string midColor, string bottomColor)
        {
            float avgZ = (a.z + b.z) * 0.5f;
            float avgY = (Math.Abs(a.y) + Math.Abs(b.y)) * 0.5f;

            if (avgZ > avgY * 0.18f)
                return topColor;

            if (avgZ < -avgY * 0.18f)
                return bottomColor;

            return midColor;
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

        private static (Vector3 min, Vector3 max) ScaleCrashBoxBounds(Vector3 min, Vector3 max)
        {
            var center = new Vector3
            {
                x = (min.x + max.x) * 0.5f,
                y = (min.y + max.y) * 0.5f,
                z = (min.z + max.z) * 0.5f
            };

            var halfSize = new Vector3
            {
                x = (max.x - min.x) * 0.5f * CrashboxSize,
                y = (max.y - min.y) * 0.5f * CrashboxSize,
                z = (max.z - min.z) * 0.5f * CrashboxSize
            };

            return (
                new Vector3
                {
                    x = center.x - halfSize.x,
                    y = center.y - halfSize.y,
                    z = center.z - halfSize.z
                },
                new Vector3
                {
                    x = center.x + halfSize.x,
                    y = center.y + halfSize.y,
                    z = center.z + halfSize.z
                }
            );
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
