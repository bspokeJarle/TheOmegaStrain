using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public class JumpingFish
    {
        private const float Scale = 1.45f;
        private const float CrashboxSize = 1.15f;
        private const int BodySegments = 8;
        private const int AnimationFrames = 6;

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0f, y = 0f, z = 0f };
        private static readonly Vector3 TailCenter = new Vector3 { x = -28f, y = 0f, z = 0f };

        // Colors
        private static string backDark = "263322";
        private static string backMid = "3C4C34";
        private static string sideGreen = "55634B";
        private static string sideLight = "798069";
        private static string belly = "C0A891";
        private static string bellyDark = "8A6E5A";
        private static string finDark = "39402B";
        private static string finMid = "596142";
        private static string eyeGold = "D89E2B";
        private static string eyeBlack = "050607";
        private static string mouthDark = "120C0A";

        public static _3dObject CreateJumpingFish(ISurface parentSurface)
        {
            var fish = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "JumpingFish"
            };

            AddPart(fish, "FishBody", CreateBody(), true);
            AddPart(fish, "FishBelly", CreateBellyPanels(), true);
            AddPart(fish, "FishMouth", CreateMouth(), true);
            AddPart(fish, "FishEyes", CreateEyes(), true);

            AddPart(fish, "TopFin", CreateTopFin(), true);
            AddPart(fish, "BottomFin", CreateBottomFin(), true);
            AddPart(fish, "LeftPectoralFin", CreatePectoralFin(false), true);
            AddPart(fish, "RightPectoralFin", CreatePectoralFin(true), true);

            for (int frame = 0; frame < AnimationFrames; frame++)
            {
                bool visible = frame == 0;
                AddPart(fish, $"TailBase_Frame{frame}", CreateTailBase(frame), visible);
                AddPart(fish, $"TailTip_Frame{frame}", CreateTailTip(frame), visible);
            }

            AddPart(fish, "JumpStartGuide", JumpStartGuide(), false);
            AddPart(fish, "JumpEndGuide", JumpEndGuide(), false);

            fish.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            fish.ParentSurface = parentSurface;
            fish.HasShadow = true;
            fish.Particles = new ParticlesAI
            {
                LifeMultiplier = 0.45f,
                MaxParticlesOverride = 70,
                ColorStartOverride = "d8f6ff",
                ColorMidOverride = "3db7ff",
                ColorEndOverride = "114cbb",
                ExplosionStartYOffset = 0f
            };
            fish.CrashBoxes = FishCrashBoxes();
            fish.CrashBoxNames = new List<string?> { "FishBody" };

            _3dObjectHelpers.ApplyScaleToObject(fish, Scale);
            _3dObjectHelpers.AddSimplifiedShadowPart(fish, useFlatQuad: true);

            return fish;
        }

        // ----------------------------------------------------
        //  ANIMATION CONTROL
        // ----------------------------------------------------

        public static void SetSwimFrame(_3dObject fish, int frame)
        {
            int normalizedFrame = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;

            foreach (var part in fish.ObjectParts)
            {
                if (part.PartName.StartsWith("TailBase_Frame") || part.PartName.StartsWith("TailTip_Frame"))
                    part.IsVisible = part.PartName.EndsWith($"Frame{normalizedFrame}");
            }
        }

        // ----------------------------------------------------
        //  BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? CreateBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var nose = new Vector3 { x = 28f, y = 0f, z = 0.5f };
            var r1 = GenerateRing(20f, 5.2f, 5.8f);
            var r2 = GenerateRing(10f, 8.4f, 7.2f);
            var r3 = GenerateRing(-4f, 9.8f, 7.5f);
            var r4 = GenerateRing(-17f, 6.2f, 5.1f);
            var tailJoin = GenerateRing(-24f, 2.8f, 2.8f);

            // Nose
            for (int i = 0; i < BodySegments; i++)
            {
                int next = (i + 1) % BodySegments;
                string color = GetBodyColor(r1[i], r1[next]);
                tris.Add(CreateTriangleOutward(nose, r1[next], r1[i], BodyCenter, color));
            }

            StitchRings(tris, r1, r2);
            StitchRings(tris, r2, r3);
            StitchRings(tris, r3, r4);
            StitchRings(tris, r4, tailJoin);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateBellyPanels()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddQuadOutward(
                tris,
                new Vector3 { x = 20f, y = -2.8f, z = -4.6f },
                new Vector3 { x = 20f, y = 2.8f, z = -4.6f },
                new Vector3 { x = 4f, y = 4.4f, z = -6.6f },
                new Vector3 { x = 4f, y = -4.4f, z = -6.6f },
                BodyCenter,
                belly);

            AddQuadOutward(
                tris,
                new Vector3 { x = 4f, y = -4.4f, z = -6.6f },
                new Vector3 { x = 4f, y = 4.4f, z = -6.6f },
                new Vector3 { x = -15f, y = 3.2f, z = -4.7f },
                new Vector3 { x = -15f, y = -3.2f, z = -4.7f },
                BodyCenter,
                bellyDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateMouth()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var upper = new Vector3 { x = 28.3f, y = 0f, z = 1.2f };
            var lower = new Vector3 { x = 27.2f, y = 0f, z = -2.2f };
            var left = new Vector3 { x = 23.6f, y = -3.4f, z = -0.8f };
            var right = new Vector3 { x = 23.6f, y = 3.4f, z = -0.8f };

            tris.Add(CreateTriangleOutward(upper, right, left, BodyCenter, mouthDark));
            tris.Add(CreateTriangleOutward(lower, left, right, BodyCenter, mouthDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateEyes()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddEye(tris, false);
            AddEye(tris, true);

            return tris;
        }

        private static void AddEye(List<ITriangleMeshWithColor> tris, bool right)
        {
            float s = right ? 1f : -1f;
            var center = new Vector3 { x = 22.2f, y = s * 4.9f, z = 2.4f };

            var top = new Vector3 { x = center.x + 0.2f, y = center.y, z = center.z + 1.2f };
            var bottom = new Vector3 { x = center.x - 0.2f, y = center.y, z = center.z - 1.2f };
            var front = new Vector3 { x = center.x + 1.2f, y = center.y, z = center.z };
            var back = new Vector3 { x = center.x - 1.2f, y = center.y, z = center.z };

            tris.Add(CreateTriangleOutward(top, front, back, center, eyeGold));
            tris.Add(CreateTriangleOutward(bottom, back, front, center, eyeGold));

            var pupilTop = new Vector3 { x = center.x + 0.55f, y = center.y + s * 0.05f, z = center.z + 0.5f };
            var pupilBottom = new Vector3 { x = center.x + 0.45f, y = center.y + s * 0.05f, z = center.z - 0.5f };
            var pupilFront = new Vector3 { x = center.x + 1.0f, y = center.y + s * 0.05f, z = center.z };

            tris.Add(CreateTriangleOutward(pupilTop, pupilFront, pupilBottom, center, eyeBlack));
        }

        // ----------------------------------------------------
        //  FINS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? CreateTopFin()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var a = new Vector3 { x = 4f, y = 0f, z = 7.4f };
            var b = new Vector3 { x = -8f, y = 0f, z = 13.2f };
            var c = new Vector3 { x = -14f, y = 0f, z = 5.6f };

            var a2 = new Vector3 { x = 4f, y = 1.0f, z = 7.2f };
            var b2 = new Vector3 { x = -8f, y = 1.0f, z = 12.5f };
            var c2 = new Vector3 { x = -14f, y = 1.0f, z = 5.4f };

            tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, finMid));
            tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, finDark));
            AddQuadOutward(tris, a, a2, b2, b, BodyCenter, finMid);
            AddQuadOutward(tris, b, b2, c2, c, BodyCenter, finDark);
            AddQuadOutward(tris, c, c2, a2, a, BodyCenter, finDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateBottomFin()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var a = new Vector3 { x = -1f, y = 0f, z = -6.8f };
            var b = new Vector3 { x = -8f, y = 0f, z = -12.0f };
            var c = new Vector3 { x = -14f, y = 0f, z = -5.2f };

            var a2 = new Vector3 { x = -1f, y = -1.0f, z = -6.6f };
            var b2 = new Vector3 { x = -8f, y = -1.0f, z = -11.3f };
            var c2 = new Vector3 { x = -14f, y = -1.0f, z = -5.0f };

            tris.Add(CreateTriangleOutward(a, c, b, BodyCenter, finDark));
            tris.Add(CreateTriangleOutward(a2, b2, c2, BodyCenter, finMid));
            AddQuadOutward(tris, a, b, b2, a2, BodyCenter, finDark);
            AddQuadOutward(tris, b, c, c2, b2, BodyCenter, finDark);
            AddQuadOutward(tris, c, a, a2, c2, BodyCenter, finDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreatePectoralFin(bool right)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = right ? 1f : -1f;

            var rootA = new Vector3 { x = 7f, y = s * 7.5f, z = -0.8f };
            var rootB = new Vector3 { x = -2f, y = s * 7.0f, z = -2.2f };
            var tip = new Vector3 { x = 3f, y = s * 17.0f, z = -5.8f };

            var rootA2 = new Vector3 { x = 7f, y = s * 7.0f, z = -1.2f };
            var rootB2 = new Vector3 { x = -2f, y = s * 6.5f, z = -2.5f };
            var tip2 = new Vector3 { x = 3f, y = s * 16.0f, z = -6.2f };

            if (right)
            {
                tris.Add(CreateTriangleOutward(rootA, tip, rootB, BodyCenter, finMid));
                tris.Add(CreateTriangleOutward(rootA2, rootB2, tip2, BodyCenter, finDark));
            }
            else
            {
                tris.Add(CreateTriangleOutward(rootA, rootB, tip, BodyCenter, finMid));
                tris.Add(CreateTriangleOutward(rootA2, tip2, rootB2, BodyCenter, finDark));
            }

            AddQuadOutward(tris, rootA, rootA2, tip2, tip, BodyCenter, finDark);
            AddQuadOutward(tris, rootB, tip, tip2, rootB2, BodyCenter, finDark);

            return tris;
        }

        // ----------------------------------------------------
        //  ANIMATED TAIL FRAMES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? CreateTailBase(int frame)
        {
            var tris = new List<ITriangleMeshWithColor>();

            float bend = GetTailBend(frame);
            float bend2 = GetTailBend(frame + 1) * 0.65f;

            var rootTop = new Vector3 { x = -22f, y = 0f, z = 2.6f };
            var rootBottom = new Vector3 { x = -22f, y = 0f, z = -2.6f };
            var rootLeft = new Vector3 { x = -22f, y = -2.8f, z = 0f };
            var rootRight = new Vector3 { x = -22f, y = 2.8f, z = 0f };

            var midTop = new Vector3 { x = -32f, y = bend, z = 2.0f };
            var midBottom = new Vector3 { x = -32f, y = bend, z = -2.0f };
            var midLeft = new Vector3 { x = -32f, y = bend - 2.0f, z = 0f };
            var midRight = new Vector3 { x = -32f, y = bend + 2.0f, z = 0f };

            var endTop = new Vector3 { x = -39f, y = bend + bend2, z = 1.4f };
            var endBottom = new Vector3 { x = -39f, y = bend + bend2, z = -1.4f };
            var endLeft = new Vector3 { x = -39f, y = bend + bend2 - 1.4f, z = 0f };
            var endRight = new Vector3 { x = -39f, y = bend + bend2 + 1.4f, z = 0f };

            AddQuadOutward(tris, rootTop, midTop, midRight, rootRight, TailCenter, backDark);
            AddQuadOutward(tris, rootTop, rootLeft, midLeft, midTop, TailCenter, backMid);
            AddQuadOutward(tris, rootBottom, rootRight, midRight, midBottom, TailCenter, bellyDark);
            AddQuadOutward(tris, rootBottom, midBottom, midLeft, rootLeft, TailCenter, bellyDark);

            AddQuadOutward(tris, midTop, endTop, endRight, midRight, TailCenter, backDark);
            AddQuadOutward(tris, midTop, midLeft, endLeft, endTop, TailCenter, backMid);
            AddQuadOutward(tris, midBottom, midRight, endRight, endBottom, TailCenter, bellyDark);
            AddQuadOutward(tris, midBottom, endBottom, endLeft, midLeft, TailCenter, bellyDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateTailTip(int frame)
        {
            var tris = new List<ITriangleMeshWithColor>();

            float bend = GetTailBend(frame);
            float bend2 = GetTailBend(frame + 1) * 0.65f;
            float tailY = bend + bend2;

            var root = new Vector3 { x = -39f, y = tailY, z = 0f };
            var upperTip = new Vector3 { x = -51f, y = tailY + GetTailBend(frame + 2) * 0.4f, z = 7.8f };
            var lowerTip = new Vector3 { x = -51f, y = tailY + GetTailBend(frame + 2) * 0.4f, z = -7.8f };
            var backTip = new Vector3 { x = -55f, y = tailY + GetTailBend(frame + 2) * 0.7f, z = 0f };

            var rootL = new Vector3 { x = -39f, y = tailY - 1.3f, z = 0f };
            var rootR = new Vector3 { x = -39f, y = tailY + 1.3f, z = 0f };

            tris.Add(CreateTriangleOutward(rootL, upperTip, backTip, TailCenter, finMid));
            tris.Add(CreateTriangleOutward(rootR, backTip, upperTip, TailCenter, finDark));
            tris.Add(CreateTriangleOutward(rootL, backTip, lowerTip, TailCenter, finMid));
            tris.Add(CreateTriangleOutward(rootR, lowerTip, backTip, TailCenter, finDark));

            return tris;
        }

        private static float GetTailBend(int frame)
        {
            int f = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;

            return f switch
            {
                0 => 0f,
                1 => 3.0f,
                2 => 5.0f,
                3 => 0f,
                4 => -5.0f,
                5 => -3.0f,
                _ => 0f
            };
        }

        // ----------------------------------------------------
        //  JUMP GUIDES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? JumpStartGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = 0f, y =  5f, z = -8f },
                    new Vector3 { x = 0f, y = -5f, z = -8f },
                    new Vector3 { x = 8f, y =  0f, z = -12f },
                    BodyCenter,
                    "33AAFF",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? JumpEndGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = 18f, y =  8f, z = 38f },
                    new Vector3 { x = 18f, y = -8f, z = 38f },
                    new Vector3 { x = 34f, y =  0f, z = 26f },
                    BodyCenter,
                    "33FF99",
                    noHidden: true)
            };
        }

        // ----------------------------------------------------
        //  CRASHBOX
        // ----------------------------------------------------

        public static List<List<IVector3>>? FishCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var bounds = ScaleCrashBoxBounds(
                new Vector3 { x = -24f, y = -10f, z = -8f },
                new Vector3 { x = 28f, y = 10f, z = 9f });

            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bounds.min, bounds.max));

            return boxes;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> GenerateRing(float x, float radiusY, float radiusZ)
        {
            var points = new List<Vector3>(BodySegments);
            float step = (float)(2 * Math.PI / BodySegments);

            for (int i = 0; i < BodySegments; i++)
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

        private static void StitchRings(List<ITriangleMeshWithColor> tris, List<Vector3> ringA, List<Vector3> ringB)
        {
            for (int i = 0; i < BodySegments; i++)
            {
                int next = (i + 1) % BodySegments;
                string color = GetBodyColor(ringA[i], ringA[next]);
                AddQuadOutward(tris, ringA[i], ringA[next], ringB[next], ringB[i], BodyCenter, color);
            }
        }

        private static string GetBodyColor(Vector3 a, Vector3 b)
        {
            float avgZ = (a.z + b.z) * 0.5f;
            float avgY = (Math.Abs(a.y) + Math.Abs(b.y)) * 0.5f;

            if (avgZ > avgY * 0.22f)
                return avgZ > 4f ? backDark : backMid;

            if (avgZ < -avgY * 0.20f)
                return belly;

            return avgY > 6f ? sideGreen : sideLight;
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
