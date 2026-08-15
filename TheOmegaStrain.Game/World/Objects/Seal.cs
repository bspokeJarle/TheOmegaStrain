using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class Seal
    {
        private const int AnimationFrames = 6;
        private const int BodySegments = 10;
        private const float Scale = 1.35f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = -2f, y = 0f, z = 3f };
        private static readonly Vector3 HeadCenter = new Vector3 { x = 20f, y = 0f, z = 9f };
        private static readonly Vector3 TailCenter = new Vector3 { x = -29f, y = 0f, z = 0f };

        private static string furLight = "BFD0D0";
        private static string furMid = "8FA3A3";
        private static string furDark = "5F7477";
        private static string bellyColor = "D8DED8";
        private static string muzzleColor = "C9D4D0";
        private static string noseColor = "1A1A1A";
        private static string eyeColor = "070707";

        public static OmegaObject3D CreateSeal(ISurface parentSurface)
        {
            var seal = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "Seal",
                ParentSurface = parentSurface,
                Rotation = new Vector3(),
                ObjectOffsets = new Vector3(),
                HasShadow = true
            };

            AddPart(seal, "SealBody", BodyTriangles(), true);
            AddPart(seal, "SealChest", ChestTriangles(), true);
            AddPart(seal, "SealHead", HeadTriangles(), true);
            AddPart(seal, "SealMuzzle", MuzzleTriangles(), true);
            AddPart(seal, "SealEyesNose", DetailTriangles(), true);
            AddPart(seal, "LeftPectoralFin", CreatePectoralFin(false), true);
            AddPart(seal, "RightPectoralFin", CreatePectoralFin(true), true);

            for (int frame = 0; frame < AnimationFrames; frame++)
            {
                bool visible = frame == 0;
                AddPart(seal, $"TailBase_Frame{frame}", CreateTailBase(frame), visible);
                AddPart(seal, $"TailTip_Frame{frame}", CreateTailTip(frame), visible);
            }

            seal.CrashBoxes = SealCrashBoxes();

            seal.Particles = new ParticlesAI
            {
                LifeMultiplier = 0.45f,
                MaxParticlesOverride = 70,
                ColorStartOverride = "d8f6ff",
                ColorMidOverride = "3db7ff",
                ColorEndOverride = "114cbb",
                ExplosionStartYOffset = 0f
            };

            AddShadow(seal);
            OmegaObject3DHelpers.ApplyScaleToObject(seal, Scale * LandBasedObjectSetup.WinterSurfaceObjectScale);
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(seal);
            seal.Movement = new SealControls();

            return seal;
        }

        public static void SetSwimFrame(OmegaObject3D seal, int frame)
        {
            int normalized = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;

            foreach (var part in seal.ObjectParts)
            {
                if (part.PartName.StartsWith("TailBase_Frame") || part.PartName.StartsWith("TailTip_Frame"))
                    part.IsVisible = part.PartName.EndsWith($"Frame{normalized}");
            }
        }

        // ----------------------------------------------------
        // BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BodyTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GenerateRing(12f, 8.8f, 7.0f, zOffset: 2.5f);
            var belly = GenerateRing(0f, 11.5f, 8.2f, zOffset: 1.2f);
            var rear = GenerateRing(-14f, 10.0f, 6.4f, zOffset: 0.2f);
            var tailJoin = GenerateRing(-24f, 4.0f, 2.8f, zOffset: -0.2f);

            StitchRings(tris, front, belly, BodyCenter);
            StitchRings(tris, belly, rear, BodyCenter);
            StitchRings(tris, rear, tailJoin, BodyCenter);

            // Soft belly patch
            AddQuadOutward(
                tris,
                new Vector3 { x = 12f, y = -4.8f, z = -4.0f },
                new Vector3 { x = 12f, y = 4.8f, z = -4.0f },
                new Vector3 { x = -14f, y = 5.5f, z = -4.5f },
                new Vector3 { x = -14f, y = -5.5f, z = -4.5f },
                BodyCenter,
                bellyColor);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? ChestTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Raised upright chest, gives the cute sitting seal silhouette.
            var lower = GenerateRing(11f, 6.5f, 5.5f, zOffset: 4.5f);
            var upper = GenerateRing(17f, 5.8f, 6.8f, zOffset: 10.0f);

            StitchRings(tris, lower, upper, new Vector3 { x = 14f, y = 0f, z = 8f });

            return tris;
        }

        // ----------------------------------------------------
        // HEAD
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? HeadTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var neck = GenerateRing(17f, 5.8f, 5.2f, zOffset: 10.2f);
            var head = GenerateRing(23f, 7.0f, 6.5f, zOffset: 12.0f);
            var face = GenerateRing(29f, 5.2f, 4.6f, zOffset: 11.2f);

            StitchRings(tris, neck, head, HeadCenter);
            StitchRings(tris, head, face, HeadCenter);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MuzzleTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var baseRing = GenerateRing(29f, 4.2f, 3.4f, zOffset: 10.4f);
            var snoutRing = GenerateRing(34f, 3.2f, 2.4f, zOffset: 9.8f);
            var noseFrontTop = new Vector3 { x = 36.7f, y = 0f, z = 10.0f };
            var noseFrontBottom = new Vector3 { x = 36.7f, y = 0f, z = 9.2f };

            StitchRingsWithColor(tris, baseRing, snoutRing, HeadCenter, muzzleColor);

            for (int i = 0; i < snoutRing.Count; i++)
            {
                int next = (i + 1) % snoutRing.Count;
                bool upper = i < snoutRing.Count / 2;
                tris.Add(CreateTriangleOutward(
                    snoutRing[i],
                    snoutRing[next],
                    upper ? noseFrontTop : noseFrontBottom,
                    HeadCenter,
                    muzzleColor));
            }

            return tris;
        }

        // ----------------------------------------------------
        // FLIPPERS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? CreatePectoralFin(bool right)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = right ? 1f : -1f;

            // Long front flipper, angled forward like image.
            var rootA = new Vector3 { x = 11f, y = s * 6.6f, z = 2.3f };
            var rootB = new Vector3 { x = 2f, y = s * 8.0f, z = -1.8f };
            var mid = new Vector3 { x = 6f, y = s * 15.5f, z = -4.8f };
            var tip = new Vector3 { x = 17f, y = s * 24.5f, z = -6.3f };

            if (right)
            {
                tris.Add(CreateTriangleOutward(rootA, mid, rootB, BodyCenter, furDark, noHidden: true));
                tris.Add(CreateTriangleOutward(rootA, tip, mid, BodyCenter, furMid, noHidden: true));
            }
            else
            {
                tris.Add(CreateTriangleOutward(rootA, rootB, mid, BodyCenter, furDark, noHidden: true));
                tris.Add(CreateTriangleOutward(rootA, mid, tip, BodyCenter, furMid, noHidden: true));
            }

            // underside thickness
            var rootC = new Vector3 { x = 8f, y = s * 5.5f, z = -0.2f };
            tris.Add(CreateTriangleOutward(rootC, rootB, tip, BodyCenter, furDark, noHidden: true));

            return tris;
        }

        // ----------------------------------------------------
        // TAIL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? CreateTailBase(int frame)
        {
            var tris = new List<ITriangleMeshWithColor>();

            float bend = GetTailBend(frame);
            float bend2 = GetTailBend(frame + 1) * 0.55f;

            var rootTop = new Vector3 { x = -22f, y = 0f, z = 1.6f };
            var rootBottom = new Vector3 { x = -22f, y = 0f, z = -1.8f };
            var rootLeft = new Vector3 { x = -22f, y = -2.8f, z = -0.2f };
            var rootRight = new Vector3 { x = -22f, y = 2.8f, z = -0.2f };

            var midTop = new Vector3 { x = -29f, y = bend, z = 1.2f };
            var midBottom = new Vector3 { x = -29f, y = bend, z = -1.4f };
            var midLeft = new Vector3 { x = -29f, y = bend - 2.0f, z = -0.3f };
            var midRight = new Vector3 { x = -29f, y = bend + 2.0f, z = -0.3f };

            var endTop = new Vector3 { x = -34f, y = bend + bend2, z = 0.8f };
            var endBottom = new Vector3 { x = -34f, y = bend + bend2, z = -1.0f };
            var endLeft = new Vector3 { x = -34f, y = bend + bend2 - 1.4f, z = -0.2f };
            var endRight = new Vector3 { x = -34f, y = bend + bend2 + 1.4f, z = -0.2f };

            AddQuadOutward(tris, rootTop, midTop, midRight, rootRight, TailCenter, furDark);
            AddQuadOutward(tris, rootTop, rootLeft, midLeft, midTop, TailCenter, furMid);
            AddQuadOutward(tris, rootBottom, rootRight, midRight, midBottom, TailCenter, bellyColor);
            AddQuadOutward(tris, rootBottom, midBottom, midLeft, rootLeft, TailCenter, bellyColor);

            AddQuadOutward(tris, midTop, endTop, endRight, midRight, TailCenter, furDark);
            AddQuadOutward(tris, midTop, midLeft, endLeft, endTop, TailCenter, furMid);
            AddQuadOutward(tris, midBottom, midRight, endRight, endBottom, TailCenter, bellyColor);
            AddQuadOutward(tris, midBottom, endBottom, endLeft, midLeft, TailCenter, bellyColor);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CreateTailTip(int frame)
        {
            var tris = new List<ITriangleMeshWithColor>();

            float bend = GetTailBend(frame);
            float bend2 = GetTailBend(frame + 1) * 0.55f;
            float tailY = bend + bend2;

            var root = new Vector3 { x = -34f, y = tailY, z = -0.2f };
            var upperTip = new Vector3 { x = -43f, y = tailY + GetTailBend(frame + 2) * 0.35f, z = 4.5f };
            var lowerTip = new Vector3 { x = -43f, y = tailY + GetTailBend(frame + 2) * 0.35f, z = -4.5f };
            var backTip = new Vector3 { x = -47f, y = tailY + GetTailBend(frame + 2) * 0.6f, z = -0.2f };

            var rootL = new Vector3 { x = root.x, y = root.y - 1.3f, z = root.z };
            var rootR = new Vector3 { x = root.x, y = root.y + 1.3f, z = root.z };

            tris.Add(CreateTriangleOutward(rootL, upperTip, backTip, TailCenter, furDark));
            tris.Add(CreateTriangleOutward(rootR, backTip, upperTip, TailCenter, furMid));
            tris.Add(CreateTriangleOutward(rootL, backTip, lowerTip, TailCenter, bellyColor));
            tris.Add(CreateTriangleOutward(rootR, lowerTip, backTip, TailCenter, bellyColor));

            return tris;
        }

        private static float GetTailBend(int frame)
        {
            int normalized = ((frame % AnimationFrames) + AnimationFrames) % AnimationFrames;

            switch (normalized)
            {
                case 0: return 0f;
                case 1: return 2.2f;
                case 2: return 4.0f;
                case 3: return 0f;
                case 4: return -4.0f;
                case 5: return -2.2f;
                default: return 0f;
            }
        }

        // ----------------------------------------------------
        // DETAILS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DetailTriangles()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Bigger cute eyes
            AddEye(tris, -1f);
            AddEye(tris, 1f);

            // Nose
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = 38.2f, y = -2.1f, z = 10.6f },
                new Vector3 { x = 38.2f, y = 2.1f, z = 10.6f },
                new Vector3 { x = 40.2f, y = 0f, z = 8.6f },
                HeadCenter,
                noseColor));

            // Small dark mouth underside
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = 35.0f, y = -2.0f, z = 8.0f },
                new Vector3 { x = 35.0f, y = 2.0f, z = 8.0f },
                new Vector3 { x = 37.5f, y = 0f, z = 7.2f },
                HeadCenter,
                "4B3B35"));

            return tris;
        }

        private static void AddEye(List<ITriangleMeshWithColor> tris, float side)
        {
            var center = new Vector3 { x = 30.1f, y = side * 4.3f, z = 13.3f };

            tris.Add(CreateTriangleOutward(
                new Vector3 { x = center.x - 0.95f, y = center.y, z = center.z + 1.25f },
                new Vector3 { x = center.x + 1.25f, y = center.y, z = center.z + 0.1f },
                new Vector3 { x = center.x - 0.55f, y = center.y, z = center.z - 1.25f },
                HeadCenter,
                eyeColor));

            // Tiny highlight
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = center.x - 0.2f, y = center.y + side * 0.05f, z = center.z + 0.45f },
                new Vector3 { x = center.x + 0.2f, y = center.y + side * 0.05f, z = center.z + 0.2f },
                new Vector3 { x = center.x, y = center.y + side * 0.05f, z = center.z - 0.05f },
                HeadCenter,
                "EAF7FF"));
        }

        // ----------------------------------------------------
        // CRASHBOX / SHADOW
        // ----------------------------------------------------

        public static List<List<IVector3>> SealCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -34f, y = -13f, z = -6f },
                    new Vector3 { x = 39f, y = 13f, z = 19f })
            };
        }

        private static void AddShadow(OmegaObject3D seal)
        {
            var shadow = new List<ITriangleMeshWithColor>();
            string sc = OmegaObject3DHelpers.ShadowColorHex;

            shadow.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -38f, y = 0f, z = 0f },
                vert2 = new Vector3 { x = 30f, y = 0f, z = 0f },
                vert3 = new Vector3 { x = 14f, y = 0f, z = 14f }
            });

            shadow.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -38f, y = 0f, z = 0f },
                vert2 = new Vector3 { x = 14f, y = 0f, z = 14f },
                vert3 = new Vector3 { x = -12f, y = 0f, z = 12f }
            });

            OmegaObject3DHelpers.AddCustomShadowPart(seal, shadow);
        }

        // ----------------------------------------------------
        // HELPERS
        // ----------------------------------------------------

        private static List<Vector3> GenerateRing(float x, float radiusY, float radiusZ, float zOffset)
        {
            var points = new List<Vector3>();
            float step = (float)(Math.PI * 2 / BodySegments);

            for (int i = 0; i < BodySegments; i++)
            {
                float angle = i * step;

                points.Add(new Vector3
                {
                    x = x,
                    y = radiusY * MathF.Cos(angle),
                    z = zOffset + radiusZ * MathF.Sin(angle)
                });
            }

            return points;
        }

        private static void StitchRings(List<ITriangleMeshWithColor> tris, List<Vector3> ringA, List<Vector3> ringB, Vector3 center)
        {
            for (int i = 0; i < ringA.Count; i++)
            {
                int next = (i + 1) % ringA.Count;
                AddQuadOutward(tris, ringA[i], ringA[next], ringB[next], ringB[i], center, GetFurColor(i));
            }
        }

        private static void StitchRingsWithColor(List<ITriangleMeshWithColor> tris, List<Vector3> ringA, List<Vector3> ringB, Vector3 center, string color)
        {
            for (int i = 0; i < ringA.Count; i++)
            {
                int next = (i + 1) % ringA.Count;
                AddQuadOutward(tris, ringA[i], ringA[next], ringB[next], ringB[i], center, color);
            }
        }

        private static string GetFurColor(int i)
        {
            switch (i % 5)
            {
                case 0: return furLight;
                case 1: return furMid;
                case 2: return furDark;
                case 3: return bellyColor;
                default: return furMid;
            }
        }

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new OmegaObjectPart3D
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
