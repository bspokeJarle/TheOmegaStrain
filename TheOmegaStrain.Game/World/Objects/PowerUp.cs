using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Ai;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;
using static TheOmegaStrain.Game.Helpers._3dObjectHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class PowerUp
    {
        private const float ZoomRatio = 1f;

        // Plus sign dimensions – roughly seeder-sized (~70 unit span)
        private const float ArmLength = 35f;
        private const float ArmWidth = 8f;
        private const float ArmDepth = 12f;

        // Blue color palette
        private const string FrontColor = "4488FF";
        private const string BackColor = "3366CC";
        private const string TopColor = "66AAFF";
        private const string BottomColor = "2255BB";
        private const string SideLightColor = "5599EE";
        private const string SideDarkColor = "2266DD";

        public static _3dObject CreatePowerup(
            ISurface parentSurface,
            PowerUpType powerUpType = PowerUpType.Standard)
        {
            var body = powerUpType == PowerUpType.Standard
                ? PlusSignBody()
                : TravelSpeedBody(powerUpType == PowerUpType.TravelSpeedLevel2 ? 3 : 2);
            var crash = PlusSignCrashBoxes();

            var powerup = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                Particles = new ParticlesAI(),
                ParentSurface = parentSurface,
                ObjectName = "PowerUp",
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "PowerUp" },
                PowerUpType = powerUpType,
                HasShadow = true
            };

            if (body != null)
            {
                powerup.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = powerUpType == PowerUpType.Standard ? "PowerUpBody" : "TravelSpeedPowerUpBody",
                    Triangles = body,
                    IsVisible = true
                });
            }

            if (crash != null)
                powerup.CrashBoxes = crash;

            _3dObjectHelpers.ApplyScaleToObject(powerup, ZoomRatio);
            _3dObjectHelpers.AddSimplifiedShadowPart(powerup, useFlatQuad: true);

            return powerup;
        }

        // ----------------------------------------------------
        //  CRASH BOX – enlarged for easy pickup
        // ----------------------------------------------------

        private const float CrashBoxSizeMultiplier = 6f;

        public static List<List<IVector3>>? PlusSignCrashBoxes()
        {
            float extent = ArmLength * CrashBoxSizeMultiplier;
            float depth = ArmDepth * CrashBoxSizeMultiplier;
            var min = new Vector3 { x = -extent, y = -depth, z = -extent };
            var max = new Vector3 { x = extent, y = depth, z = extent };

            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(min, max)
            };
        }

        // ----------------------------------------------------
        //  PLUS SIGN GEOMETRY
        // ----------------------------------------------------
        //
        // Two intersecting rectangular prisms forming a 3D cross:
        //   Horizontal arm: extends along X, narrow in Z
        //   Vertical arm:   extends along Z, narrow in X
        //
        // 24 triangles per arm × 2 arms = 48 triangles total.
        // Outward normals enforced via CreateTriangleOutward.
        //

        public static List<ITriangleMeshWithColor>? PlusSignBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Horizontal arm (extends in X, narrow in Z)
            AddBox(tris,
                -ArmLength, ArmLength,
                -ArmWidth, ArmWidth,
                -ArmDepth, ArmDepth,
                FrontColor, BackColor, TopColor, BottomColor, SideLightColor, SideDarkColor);

            // Vertical arm (extends in Z, narrow in X)
            AddBox(tris,
                -ArmWidth, ArmWidth,
                -ArmLength, ArmLength,
                -ArmDepth, ArmDepth,
                FrontColor, BackColor, TopColor, BottomColor, SideDarkColor, SideLightColor);

            return tris;
        }

        public static List<ITriangleMeshWithColor> TravelSpeedBody(int boltCount)
        {
            var tris = new List<ITriangleMeshWithColor>();
            int count = Math.Clamp(boltCount, 2, 3);
            float scale = count == 2 ? 0.72f : 0.58f;
            float spacing = count == 2 ? 22f : 24f;
            float startX = -spacing * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
                AddLightningBolt(tris, startX + (i * spacing), scale);

            return tris;
        }

        private static void AddLightningBolt(
            List<ITriangleMeshWithColor> tris,
            float offsetX,
            float scale)
        {
            const float depth = 9f;
            var outline = new[]
            {
                new Vector3 { x = offsetX + (-5f * scale), y = -depth, z = -36f * scale },
                new Vector3 { x = offsetX + (16f * scale), y = -depth, z = -36f * scale },
                new Vector3 { x = offsetX + (5f * scale), y = -depth, z = -7f * scale },
                new Vector3 { x = offsetX + (19f * scale), y = -depth, z = -7f * scale },
                new Vector3 { x = offsetX + (-13f * scale), y = -depth, z = 36f * scale },
                new Vector3 { x = offsetX + (-3f * scale), y = -depth, z = 7f * scale },
                new Vector3 { x = offsetX + (-18f * scale), y = -depth, z = 7f * scale }
            };

            var center = new Vector3 { x = offsetX, y = 0f, z = 0f };
            int[,] faces =
            {
                { 0, 1, 2 },
                { 0, 2, 6 },
                { 6, 2, 5 },
                { 5, 2, 3 },
                { 5, 3, 4 }
            };

            for (int i = 0; i < faces.GetLength(0); i++)
            {
                var a = outline[faces[i, 0]];
                var b = outline[faces[i, 1]];
                var c = outline[faces[i, 2]];
                tris.Add(CreateTriangleOutward(a, b, c, center, i == 3 ? "FFAA22" : FrontColor));

                var backA = new Vector3 { x = a.x, y = depth, z = a.z };
                var backB = new Vector3 { x = b.x, y = depth, z = b.z };
                var backC = new Vector3 { x = c.x, y = depth, z = c.z };
                tris.Add(CreateTriangleOutward(backA, backC, backB, center, BackColor));
            }

            for (int i = 0; i < outline.Length; i++)
            {
                var frontA = outline[i];
                var frontB = outline[(i + 1) % outline.Length];
                var backA = new Vector3 { x = frontA.x, y = depth, z = frontA.z };
                var backB = new Vector3 { x = frontB.x, y = depth, z = frontB.z };
                string color = i % 2 == 0 ? TopColor : SideDarkColor;
                tris.Add(CreateTriangleOutward(frontA, frontB, backB, center, color));
                tris.Add(CreateTriangleOutward(frontA, backB, backA, center, color));
            }
        }

        // Generates 12 triangles (6 faces) for an axis-aligned box
        private static void AddBox(
            List<ITriangleMeshWithColor> tris,
            float minX, float maxX,
            float minZ, float maxZ,
            float minY, float maxY,
            string frontCol, string backCol,
            string topCol, string bottomCol,
            string leftRightCol, string endCol)
        {
            //  f = front (y=minY), b = back (y=maxY)
            //  t = top   (z=maxZ), b = bottom (z=minZ)
            //  l = left  (x=minX), r = right  (x=maxX)
            var fbl = new Vector3 { x = minX, y = minY, z = minZ };
            var fbr = new Vector3 { x = maxX, y = minY, z = minZ };
            var ftl = new Vector3 { x = minX, y = minY, z = maxZ };
            var ftr = new Vector3 { x = maxX, y = minY, z = maxZ };
            var bbl = new Vector3 { x = minX, y = maxY, z = minZ };
            var bbr = new Vector3 { x = maxX, y = maxY, z = minZ };
            var btl = new Vector3 { x = minX, y = maxY, z = maxZ };
            var btr = new Vector3 { x = maxX, y = maxY, z = maxZ };

            var center = new Vector3
            {
                x = (minX + maxX) / 2f,
                y = (minY + maxY) / 2f,
                z = (minZ + maxZ) / 2f
            };

            // Front face (y = minY, facing -Y)
            tris.Add(CreateTriangleOutward(ftl, ftr, fbr, center, frontCol));
            tris.Add(CreateTriangleOutward(ftl, fbr, fbl, center, frontCol));

            // Back face (y = maxY, facing +Y)
            tris.Add(CreateTriangleOutward(btr, btl, bbl, center, backCol));
            tris.Add(CreateTriangleOutward(btr, bbl, bbr, center, backCol));

            // Top face (z = maxZ, facing +Z)
            tris.Add(CreateTriangleOutward(ftl, btl, btr, center, topCol));
            tris.Add(CreateTriangleOutward(ftl, btr, ftr, center, topCol));

            // Bottom face (z = minZ, facing -Z)
            tris.Add(CreateTriangleOutward(fbr, bbr, bbl, center, bottomCol));
            tris.Add(CreateTriangleOutward(fbr, bbl, fbl, center, bottomCol));

            // Right face (x = maxX)
            tris.Add(CreateTriangleOutward(ftr, btr, bbr, center, endCol));
            tris.Add(CreateTriangleOutward(ftr, bbr, fbr, center, endCol));

            // Left face (x = minX)
            tris.Add(CreateTriangleOutward(btl, ftl, fbl, center, leftRightCol));
            tris.Add(CreateTriangleOutward(btl, fbl, bbl, center, leftRightCol));
        }

            }
        }
