using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Ai;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

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

        public static OmegaObject3D CreatePowerup(
            ISurface parentSurface,
            PowerUpType powerUpType = PowerUpType.Standard)
        {
            var body = powerUpType switch
            {
                PowerUpType.Standard => PlusSignBody(),
                PowerUpType.Shield => ShieldBody(),
                _ => TravelSpeedBody(powerUpType == PowerUpType.TravelSpeedLevel2 ? 3 : 2)
            };
            var crash = PlusSignCrashBoxes();

            var powerup = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 },
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
                powerup.ObjectParts.Add(new OmegaObjectPart3D
                {
                    PartName = powerUpType switch
                    {
                        PowerUpType.Standard => "PowerUpBody",
                        PowerUpType.Shield => "ShieldPowerUpBody",
                        _ => "TravelSpeedPowerUpBody"
                    },
                    Triangles = body,
                    IsVisible = true
                });
            }

            if (crash != null)
                powerup.CrashBoxes = crash;

            OmegaObject3DHelpers.ApplyScaleToObject(powerup, ZoomRatio);
            OmegaObject3DHelpers.AddSimplifiedShadowPart(powerup, useFlatQuad: true);

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
                OmegaObject3DHelpers.GenerateCrashBoxCorners(min, max)
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

        public static List<ITriangleMeshWithColorAndTexture>? PlusSignBody()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

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

        // ----------------------------------------------------
        //  SHIELD GEOMETRY
        // ----------------------------------------------------
        //
        // Heater-shield silhouette (rounded top, tapering to a bottom point).
        // Three outline rings extrude and taper along Y to give the plate real
        // depth plus a curved bevel between the flat rim and the domed face:
        //   outer ring  -> straight rim wall (the shield's edge thickness)
        //   bevel ring  -> inset + pushed forward, the curved transition
        //   apex point  -> pushed forward again, domes the inner face
        // A silver cross band and a gem quad sit on the inner face as symbols.
        //
        // 12 back + 24 rim quads + 24 bevel quads + 12 face + 2 gem = 74 triangles.
        //

        private const float ShieldEdgeDepth = 16f;
        private const float ShieldBevelInset = 6f;
        private const float ShieldApexPush = 4f;
        private const float ShieldBevelScale = 0.82f;

        private const string ShieldGoldLight = "E8C55A";
        private const string ShieldGoldMid = "D4AF37";
        private const string ShieldGoldDark = "9C7A22";
        private const string ShieldCrossColor = "ECEAF2";
        private const string ShieldGemColor = "B31B3D";

        private static readonly (float x, float z)[] ShieldOutline =
        {
            (-32f, 40f), (-18f, 46f), (0f, 48f), (18f, 46f), (32f, 40f),
            (34f, 10f), (26f, -18f), (12f, -40f), (0f, -52f),
            (-12f, -40f), (-26f, -18f), (-34f, 10f)
        };

        public static List<ITriangleMeshWithColorAndTexture> ShieldBody()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();
            int count = ShieldOutline.Length;

            float outerFrontY = -ShieldEdgeDepth;
            float bevelFrontY = -(ShieldEdgeDepth + ShieldBevelInset);
            float apexY = -(ShieldEdgeDepth + ShieldBevelInset + ShieldApexPush);
            float backY = ShieldEdgeDepth;

            var outerFront = new Vector3[count];
            var bevelFront = new Vector3[count];
            var outerBack = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var (x, z) = ShieldOutline[i];
                outerFront[i] = new Vector3 { x = x, y = outerFrontY, z = z };
                bevelFront[i] = new Vector3 { x = x * ShieldBevelScale, y = bevelFrontY, z = z * ShieldBevelScale };
                outerBack[i] = new Vector3 { x = x, y = backY, z = z };
            }

            var frontApex = new Vector3 { x = 0f, y = apexY, z = 5f };
            var backApex = new Vector3 { x = 0f, y = backY, z = 5f };
            var center = new Vector3 { x = 0f, y = -3f, z = 5f };

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;

                // Silver cross band through the top peak and bottom point
                bool cross = i is 1 or 2 or 3 or 7 or 8 or 9;
                string faceColor = cross ? ShieldCrossColor : ShieldGoldMid;

                // Domed inner face
                tris.Add(CreateTriangleOutward(frontApex, bevelFront[i], bevelFront[next], center, faceColor));

                // Flat back face
                tris.Add(CreateTriangleOutward(backApex, outerBack[next], outerBack[i], center, ShieldGoldDark));

                // Curved bevel between the outer rim and the domed face
                string bevelColor = i % 2 == 0 ? ShieldGoldLight : ShieldGoldMid;
                AddQuadOutward(tris, outerFront[i], outerFront[next], bevelFront[next], bevelFront[i], center, bevelColor);

                // Straight rim wall, giving the plate its edge thickness
                string rimColor = i % 2 == 0 ? ShieldGoldLight : ShieldGoldDark;
                AddQuadOutward(tris, outerFront[i], outerFront[next], outerBack[next], outerBack[i], center, rimColor);
            }

            // Gem symbol set just proud of the domed face
            float gemY = apexY - 2f;
            var gemTop = new Vector3 { x = 0f, y = gemY, z = 15f };
            var gemRight = new Vector3 { x = 6f, y = gemY, z = 5f };
            var gemBottom = new Vector3 { x = 0f, y = gemY, z = -5f };
            var gemLeft = new Vector3 { x = -6f, y = gemY, z = 5f };
            AddQuadOutward(tris, gemTop, gemRight, gemBottom, gemLeft, center, ShieldGemColor);

            return tris;
        }

        public static List<ITriangleMeshWithColorAndTexture> TravelSpeedBody(int boltCount)
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();
            int count = Math.Clamp(boltCount, 2, 3);
            float scale = count == 2 ? 0.72f : 0.58f;
            float spacing = count == 2 ? 22f : 24f;
            float startX = -spacing * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
                AddLightningBolt(tris, startX + (i * spacing), scale);

            return tris;
        }

        private static void AddLightningBolt(
            List<ITriangleMeshWithColorAndTexture> tris,
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
            List<ITriangleMeshWithColorAndTexture> tris,
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
