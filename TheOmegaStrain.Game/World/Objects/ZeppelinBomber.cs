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
    public class ZeppelinBomber
    {
        private const float ZoomRatio = 4.0f;
        private const float CrashboxSize = 1.5f;

        // ----------------------------------------------------
        //  HULL / BODY
        // ----------------------------------------------------

        private static int hullSegments = 10;

        private static float noseTipX = 44f;
        private static float frontRingX = 28f;
        private static float upperMidFrontX = 12f;
        private static float centerMidX = -6f;
        private static float rearMidX = -24f;
        private static float tailRingX = -38f;
        private static float tailTipX = -48f;

        private static float frontRadiusY = 8.0f;
        private static float frontRadiusZ = 7.0f;

        private static float upperMidFrontRadiusY = 11.5f;
        private static float upperMidFrontRadiusZ = 10.0f;

        private static float centerMidRadiusY = 13.0f;
        private static float centerMidRadiusZ = 11.5f;

        private static float rearMidRadiusY = 11.5f;
        private static float rearMidRadiusZ = 10.0f;

        private static float tailRadiusY = 7.0f;
        private static float tailRadiusZ = 6.0f;

        private static float hullBottomFlatten = 0.88f;
        private static float hullTopLift = 1.04f;

        // ----------------------------------------------------
        //  BOMB BAY
        // ----------------------------------------------------

        private static float bayFrontX = 0f;
        private static float bayBackX = -16f;
        private static float bayHalfWidth = 4.6f;
        private static float bayInsetZ = -11.5f;
        private static float bayLipZ = -10.0f;

        // ----------------------------------------------------
        //  REAR / PROPELLER
        // ----------------------------------------------------

        private static float propHubFrontX = -46f;
        private static float propHubBackX = -50f;
        private static float propHubRadius = 2.0f;

        private static float propBladeLength = 8.0f;
        private static float propBladeWidth = 1.7f;
        private static float propBladeThickness = 0.45f;

        // ----------------------------------------------------
        //  TAIL FINS
        // ----------------------------------------------------

        private static float finBaseX = -31f;
        private static float finTipX = -41f;
        private static float finHalfThickness = 0.9f;
        private static float topFinHeight = 7.5f;
        private static float sideFinOffsetY = 8.8f;
        private static float sideFinHeight = 3.2f;

        // ----------------------------------------------------
        //  OPTIONAL FRONT SENSOR / COCKPIT
        // ----------------------------------------------------

        private static float cockpitFrontX = 21f;
        private static float cockpitMidX = 10f;
        private static float cockpitBackX = 0f;

        private static float cockpitHalfWidthFront = 2.4f;
        private static float cockpitHalfWidthMid = 3.1f;
        private static float cockpitHalfWidthBack = 2.3f;

        private static float cockpitBaseLiftFront = 1.3f;
        private static float cockpitBaseLiftMid = 1.8f;
        private static float cockpitBaseLiftBack = 1.0f;

        private static float cockpitHeightFront = 1.7f;
        private static float cockpitHeightMid = 2.2f;
        private static float cockpitHeightBack = 1.2f;

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string hullColorLight = "B7BDC7";
        private static string hullColorMid = "868E9A";
        private static string hullColorDark = "575F6B";
        private static string undersideColor = "2D323A";
        private static string bayFrameColor = "4A505A";
        private static string bayInsideColor = "15181D";
        private static string cockpitColor = "1F242D";
        private static string cockpitSoft = "303743";
        private static string finColor = "6B7380";
        private static string propHubColor = "7B838F";
        private static string propBladeColor = "C8CFD9";
        private static string engineGlowColor = "FF9A33";

        private static readonly Vector3 BodyCenter = new Vector3 { x = -5f, y = 0f, z = 0f };

        public static _3dObject CreateZeppelinBomber(ISurface parentSurface)
        {
            var nose = BomberNose();
            var body = BomberHullBody();
            var tailClosure = BomberTailClosure();
            var bombBay = BomberBombBay();
            var cockpit = BomberCockpit();
            var rearMount = BomberRearMount();
            var propeller = BomberPropeller();
            var fins = BomberFins();

            var crashBoxes = BomberCrashBoxes();
            var guide = ParticlesDirectionGuide();
            var startGuide = ParticlesStartGuide();
            var bombDropStart = BombDropStartGuide();
            var bombDropEnd = BombDropEndGuide();

            var bomber = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "ZeppelinBomber"
            };

            AddPart(bomber, "BomberNose", nose, true);
            AddPart(bomber, "BomberHullBody", body, true);
            AddPart(bomber, "BomberTailClosure", tailClosure, true);
            AddPart(bomber, "BomberBombBay", bombBay, true);
            AddPart(bomber, "BomberCockpit", cockpit, true);
            AddPart(bomber, "BomberRearMount", rearMount, true);
            AddPart(bomber, "BomberPropeller", propeller, true);
            AddPart(bomber, "BomberFins", fins, true);

            AddPart(bomber, "BomberParticlesGuide", guide, false);
            AddPart(bomber, "BomberParticlesStartGuide", startGuide, false);
            AddPart(bomber, "BomberBombDropStart", bombDropStart, false);
            AddPart(bomber, "BomberBombDropEnd", bombDropEnd, false);

            bomber.Particles = new ParticlesAI();
            bomber.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            bomber.ParentSurface = parentSurface;
            bomber.HasShadow = true;

            if (crashBoxes != null)
                bomber.CrashBoxes = crashBoxes;

            _3dObjectHelpers.ApplyScaleToObject(bomber, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(bomber, useFlatQuad: true);

            return bomber;
        }

        // ----------------------------------------------------
        //  NOSE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberNose()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var frontRing = GenerateHullRing(frontRingX, frontRadiusY, frontRadiusZ);

            var topTip = new Vector3 { x = noseTipX, y = 0f, z = 0.8f };
            var bottomTip = new Vector3 { x = noseTipX - 1.6f, y = 0f, z = -1.0f };

            for (int i = 0; i < frontRing.Count; i++)
            {
                int next = (i + 1) % frontRing.Count;

                string topColor = GetHullColor(frontRing[i], frontRing[next], preferLight: true);
                string bottomColor = GetHullColor(frontRing[i], frontRing[next], preferLight: false);

                tris.Add(CreateTriangleOutward(topTip, frontRing[next], frontRing[i], BodyCenter, topColor));
                tris.Add(CreateTriangleOutward(bottomTip, frontRing[i], frontRing[next], BodyCenter, bottomColor));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  MAIN HULL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberHullBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ring1 = GenerateHullRing(frontRingX, frontRadiusY, frontRadiusZ);
            var ring2 = GenerateHullRing(upperMidFrontX, upperMidFrontRadiusY, upperMidFrontRadiusZ);
            var ring3 = GenerateHullRing(centerMidX, centerMidRadiusY, centerMidRadiusZ);
            var ring4 = GenerateHullRing(rearMidX, rearMidRadiusY, rearMidRadiusZ);
            var ring5 = GenerateHullRing(tailRingX, tailRadiusY, tailRadiusZ);

            StitchRings(tris, ring1, ring2);
            StitchRings(tris, ring2, ring3);
            StitchRings(tris, ring3, ring4);
            StitchRings(tris, ring4, ring5);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? BomberTailClosure()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var tailRing = GenerateHullRing(tailRingX, tailRadiusY, tailRadiusZ);
            var tailTip = new Vector3 { x = tailTipX, y = 0f, z = 0f };

            for (int i = 0; i < tailRing.Count; i++)
            {
                int next = (i + 1) % tailRing.Count;
                string color = GetHullColor(tailRing[i], tailRing[next], preferLight: false);
                tris.Add(CreateTriangleOutward(tailRing[i], tailRing[next], tailTip, BodyCenter, color));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  BOMB BAY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberBombBay()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Outer lip
            var fl = new Vector3 { x = bayFrontX, y = -bayHalfWidth, z = bayLipZ };
            var fr = new Vector3 { x = bayFrontX, y = bayHalfWidth, z = bayLipZ };
            var bl = new Vector3 { x = bayBackX, y = -bayHalfWidth, z = bayLipZ };
            var br = new Vector3 { x = bayBackX, y = bayHalfWidth, z = bayLipZ };

            // Inner opening
            var ifl = new Vector3 { x = bayFrontX - 1.2f, y = -bayHalfWidth + 1.1f, z = bayInsetZ };
            var ifr = new Vector3 { x = bayFrontX - 1.2f, y = bayHalfWidth - 1.1f, z = bayInsetZ };
            var ibl = new Vector3 { x = bayBackX + 1.2f, y = -bayHalfWidth + 1.1f, z = bayInsetZ };
            var ibr = new Vector3 { x = bayBackX + 1.2f, y = bayHalfWidth - 1.1f, z = bayInsetZ };

            // Bay frame
            AddQuadOutward(tris, fl, fr, ifr, ifl, BodyCenter, bayFrameColor);
            AddQuadOutward(tris, bl, ibl, ibr, br, BodyCenter, bayFrameColor);
            AddQuadOutward(tris, fl, ifl, ibl, bl, BodyCenter, bayFrameColor);
            AddQuadOutward(tris, fr, br, ibr, ifr, BodyCenter, bayFrameColor);

            // Inner bay walls
            AddQuadOutward(tris, ifl, ifr, ibr, ibl, BodyCenter, bayInsideColor);

            // Center divider / simple doors impression
            var mFrontTop = new Vector3 { x = bayFrontX - 1.0f, y = 0f, z = bayInsetZ + 0.1f };
            var mBackTop = new Vector3 { x = bayBackX + 1.0f, y = 0f, z = bayInsetZ + 0.1f };
            var mFrontBottom = new Vector3 { x = bayFrontX - 1.0f, y = 0f, z = bayInsetZ - 0.7f };
            var mBackBottom = new Vector3 { x = bayBackX + 1.0f, y = 0f, z = bayInsetZ - 0.7f };

            AddQuadOutward(tris, mFrontTop, mBackTop, mBackBottom, mFrontBottom, BodyCenter, bayFrameColor);

            return tris;
        }

        // ----------------------------------------------------
        //  COCKPIT / SENSOR BUMP
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberCockpit()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float baseFrontZ = GetTopAtX(cockpitFrontX) + cockpitBaseLiftFront;
            float baseMidZ = GetTopAtX(cockpitMidX) + cockpitBaseLiftMid;
            float baseBackZ = GetTopAtX(cockpitBackX) + cockpitBaseLiftBack;

            var lf = new Vector3 { x = cockpitFrontX, y = -cockpitHalfWidthFront, z = baseFrontZ };
            var rf = new Vector3 { x = cockpitFrontX, y = cockpitHalfWidthFront, z = baseFrontZ };

            var lm = new Vector3 { x = cockpitMidX, y = -cockpitHalfWidthMid, z = baseMidZ };
            var rm = new Vector3 { x = cockpitMidX, y = cockpitHalfWidthMid, z = baseMidZ };

            var lb = new Vector3 { x = cockpitBackX, y = -cockpitHalfWidthBack, z = baseBackZ };
            var rb = new Vector3 { x = cockpitBackX, y = cockpitHalfWidthBack, z = baseBackZ };

            var tf = new Vector3 { x = cockpitFrontX + 0.5f, y = 0f, z = baseFrontZ + cockpitHeightFront };
            var tm = new Vector3 { x = cockpitMidX, y = 0f, z = baseMidZ + cockpitHeightMid };
            var tb = new Vector3 { x = cockpitBackX, y = 0f, z = baseBackZ + cockpitHeightBack };

            tris.Add(CreateTriangleOutward(lf, tf, lm, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(tf, tm, lm, BodyCenter, cockpitColor));

            tris.Add(CreateTriangleOutward(tf, rf, rm, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(tf, rm, tm, BodyCenter, cockpitColor));

            tris.Add(CreateTriangleOutward(lm, tm, lb, BodyCenter, cockpitSoft));
            tris.Add(CreateTriangleOutward(tm, tb, lb, BodyCenter, cockpitSoft));

            tris.Add(CreateTriangleOutward(tm, rm, rb, BodyCenter, cockpitSoft));
            tris.Add(CreateTriangleOutward(tm, rb, tb, BodyCenter, cockpitSoft));

            tris.Add(CreateTriangleOutward(lf, rf, tf, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(lm, tm, rm, BodyCenter, cockpitSoft));
            tris.Add(CreateTriangleOutward(lb, tb, rb, BodyCenter, cockpitSoft));

            return tris;
        }

        // ----------------------------------------------------
        //  REAR MOUNT + PROPELLER
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberRearMount()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ftl = new Vector3 { x = propHubFrontX, y = -2.4f, z = 2.1f };
            var ftr = new Vector3 { x = propHubFrontX, y = 2.4f, z = 2.1f };
            var fbl = new Vector3 { x = propHubFrontX, y = -2.4f, z = -2.1f };
            var fbr = new Vector3 { x = propHubFrontX, y = 2.4f, z = -2.1f };

            var btl = new Vector3 { x = propHubBackX + 1.0f, y = -1.6f, z = 1.4f };
            var btr = new Vector3 { x = propHubBackX + 1.0f, y = 1.6f, z = 1.4f };
            var bbl = new Vector3 { x = propHubBackX + 1.0f, y = -1.6f, z = -1.4f };
            var bbr = new Vector3 { x = propHubBackX + 1.0f, y = 1.6f, z = -1.4f };

            AddQuadOutward(tris, ftl, ftr, btr, btl, BodyCenter, hullColorDark);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, BodyCenter, undersideColor);
            AddQuadOutward(tris, ftr, fbr, bbr, btr, BodyCenter, hullColorDark);
            AddQuadOutward(tris, fbl, ftl, btl, bbl, BodyCenter, hullColorDark);
            AddQuadOutward(tris, btl, btr, bbr, bbl, BodyCenter, engineGlowColor);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? BomberPropeller()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Hub
            float xFront = propHubBackX + 1.2f;
            float xBack = propHubBackX - 1.2f;
            float r = propHubRadius;

            var ftl = new Vector3 { x = xFront, y = -r, z = r };
            var ftr = new Vector3 { x = xFront, y = r, z = r };
            var fbl = new Vector3 { x = xFront, y = -r, z = -r };
            var fbr = new Vector3 { x = xFront, y = r, z = -r };

            var btl = new Vector3 { x = xBack, y = -r * 0.9f, z = r * 0.9f };
            var btr = new Vector3 { x = xBack, y = r * 0.9f, z = r * 0.9f };
            var bbl = new Vector3 { x = xBack, y = -r * 0.9f, z = -r * 0.9f };
            var bbr = new Vector3 { x = xBack, y = r * 0.9f, z = -r * 0.9f };

            AddQuadOutward(tris, ftl, ftr, btr, btl, BodyCenter, propHubColor);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, BodyCenter, propHubColor);
            AddQuadOutward(tris, ftr, fbr, bbr, btr, BodyCenter, propHubColor);
            AddQuadOutward(tris, fbl, ftl, btl, bbl, BodyCenter, propHubColor);
            AddQuadOutward(tris, btl, btr, bbr, bbl, BodyCenter, propHubColor);

            // Three blades evenly spaced at 120° intervals in the YZ plane
            float bx = xBack - 0.3f;
            float len = propBladeLength;
            var bladeCenter = new Vector3 { x = bx, y = 0f, z = 0f };

            // Blade 1 — 0° (straight up +Z)
            AddBlade(tris, bladeCenter,
                new Vector3 { x = bx, y = 0f, z = len },
                propBladeWidth, propBladeThickness, propBladeColor);

            // Blade 2 — 120°
            AddBlade(tris, bladeCenter,
                new Vector3 { x = bx, y = len * 0.866f, z = len * -0.5f },
                propBladeWidth, propBladeThickness, propBladeColor);

            // Blade 3 — 240°
            AddBlade(tris, bladeCenter,
                new Vector3 { x = bx, y = len * -0.866f, z = len * -0.5f },
                propBladeWidth, propBladeThickness, propBladeColor);

            return tris;
        }

        // ----------------------------------------------------
        //  FINS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BomberFins()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Top fin
            {
                var a = new Vector3 { x = finBaseX, y = 0f, z = 4.7f };
                var b = new Vector3 { x = finTipX, y = 0f, z = 4.7f + topFinHeight };
                var c = new Vector3 { x = finTipX, y = 0f, z = 5.4f };

                var a2 = new Vector3 { x = finBaseX, y = finHalfThickness, z = 4.7f };
                var b2 = new Vector3 { x = finTipX, y = finHalfThickness, z = 4.7f + topFinHeight };
                var c2 = new Vector3 { x = finTipX, y = finHalfThickness, z = 5.4f };

                var a3 = new Vector3 { x = finBaseX, y = -finHalfThickness, z = 4.7f };
                var b3 = new Vector3 { x = finTipX, y = -finHalfThickness, z = 4.7f + topFinHeight };
                var c3 = new Vector3 { x = finTipX, y = -finHalfThickness, z = 5.4f };

                tris.Add(CreateTriangleOutward(a3, b3, c3, BodyCenter, finColor));
                tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, hullColorDark));

                AddQuadOutward(tris, a3, a2, b2, b3, BodyCenter, finColor);
                AddQuadOutward(tris, a3, c3, c2, a2, BodyCenter, hullColorDark);
                AddQuadOutward(tris, c3, b3, b2, c2, BodyCenter, hullColorDark);
            }

            // Left side fin
            {
                float y = -sideFinOffsetY;

                var a = new Vector3 { x = finBaseX + 1f, y = y, z = 0.8f };
                var b = new Vector3 { x = finTipX + 2f, y = y - 2.0f, z = 1.8f + sideFinHeight };
                var c = new Vector3 { x = finTipX + 2f, y = y - 2.0f, z = -0.8f };

                var a2 = new Vector3 { x = finBaseX + 1f, y = y + finHalfThickness, z = 0.8f };
                var b2 = new Vector3 { x = finTipX + 2f, y = y - 2.0f + finHalfThickness, z = 1.8f + sideFinHeight };
                var c2 = new Vector3 { x = finTipX + 2f, y = y - 2.0f + finHalfThickness, z = -0.8f };

                tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, finColor));
                tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, hullColorDark));
                AddQuadOutward(tris, a, a2, b2, b, BodyCenter, finColor);
                AddQuadOutward(tris, a, c, c2, a2, BodyCenter, hullColorDark);
                AddQuadOutward(tris, c, b, b2, c2, BodyCenter, hullColorDark);
            }

            // Right side fin
            {
                float y = sideFinOffsetY;

                var a = new Vector3 { x = finBaseX + 1f, y = y, z = 0.8f };
                var b = new Vector3 { x = finTipX + 2f, y = y + 2.0f, z = 1.8f + sideFinHeight };
                var c = new Vector3 { x = finTipX + 2f, y = y + 2.0f, z = -0.8f };

                var a2 = new Vector3 { x = finBaseX + 1f, y = y - finHalfThickness, z = 0.8f };
                var b2 = new Vector3 { x = finTipX + 2f, y = y + 2.0f - finHalfThickness, z = 1.8f + sideFinHeight };
                var c2 = new Vector3 { x = finTipX + 2f, y = y + 2.0f - finHalfThickness, z = -0.8f };

                tris.Add(CreateTriangleOutward(a, c, b, BodyCenter, finColor));
                tris.Add(CreateTriangleOutward(a2, b2, c2, BodyCenter, hullColorDark));
                AddQuadOutward(tris, a, b, b2, a2, BodyCenter, finColor);
                AddQuadOutward(tris, a, a2, c2, c, BodyCenter, hullColorDark);
                AddQuadOutward(tris, c, c2, b2, b, BodyCenter, hullColorDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  GUIDES / COLLISION
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ParticlesDirectionGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = propHubBackX - 2f, y =  5f, z =  1.5f },
                    new Vector3 { x = propHubBackX - 2f, y = -5f, z =  1.5f },
                    new Vector3 { x = propHubBackX - 14f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? ParticlesStartGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = tailRingX + 2f, y =  4f, z =  1.0f },
                    new Vector3 { x = tailRingX + 2f, y = -4f, z =  1.0f },
                    new Vector3 { x = propHubFrontX - 1f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? BombDropStartGuide()
        {
            float centerX = (bayFrontX + bayBackX) * 0.5f;
            float centerZ = bayInsetZ;
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = centerX + 2f, y =  2f, z = centerZ },
                    new Vector3 { x = centerX + 2f, y = -2f, z = centerZ },
                    new Vector3 { x = centerX - 2f, y =  0f, z = centerZ },
                    BodyCenter,
                    "FF3333",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? BombDropEndGuide()
        {
            float centerX = (bayFrontX + bayBackX) * 0.5f;
            float endY = 30f;
            float endZ = bayInsetZ - 55f;
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = centerX + 8f, y = endY + 8f, z = endZ },
                    new Vector3 { x = centerX + 8f, y = endY - 8f, z = endZ },
                    new Vector3 { x = centerX - 8f, y = endY,      z = endZ },
                    BodyCenter,
                    "33FF33",
                    noHidden: true)
            };
        }

        public static List<List<IVector3>>? BomberCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var frontBounds = ScaleCrashBoxBounds(
                new Vector3 { x = frontRingX - 3f, y = -frontRadiusY - 1f, z = -frontRadiusZ - 1f },
                new Vector3 { x = noseTipX, y = frontRadiusY + 1f, z = frontRadiusZ + 1.5f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(frontBounds.min, frontBounds.max));

            var midBounds = ScaleCrashBoxBounds(
                new Vector3 { x = rearMidX, y = -centerMidRadiusY - 1.2f, z = -centerMidRadiusZ - 1.2f },
                new Vector3 { x = upperMidFrontX + 2f, y = centerMidRadiusY + 1.2f, z = centerMidRadiusZ + 1.5f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(midBounds.min, midBounds.max));

            var bayBounds = ScaleCrashBoxBounds(
                new Vector3 { x = bayBackX - 1f, y = -bayHalfWidth - 0.8f, z = bayInsetZ - 0.8f },
                new Vector3 { x = bayFrontX + 1f, y = bayHalfWidth + 0.8f, z = bayLipZ + 1.2f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bayBounds.min, bayBounds.max));

            var rearBounds = ScaleCrashBoxBounds(
                new Vector3 { x = tailRingX - 2f, y = -tailRadiusY - 2f, z = -tailRadiusZ - 1.5f },
                new Vector3 { x = propHubFrontX + 1f, y = tailRadiusY + 2f, z = topFinHeight + 6.5f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rearBounds.min, rearBounds.max));

            return boxes;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> GenerateHullRing(float x, float radiusY, float radiusZ)
        {
            var points = new List<Vector3>(hullSegments);
            float step = (float)(2 * Math.PI / hullSegments);

            for (int i = 0; i < hullSegments; i++)
            {
                float angle = i * step;
                float y = radiusY * (float)Math.Cos(angle);
                float z = radiusZ * (float)Math.Sin(angle);

                if (z < 0f)
                    z *= hullBottomFlatten;
                else
                    z *= hullTopLift;

                points.Add(new Vector3
                {
                    x = x,
                    y = y,
                    z = z
                });
            }

            return points;
        }

        private static void StitchRings(List<ITriangleMeshWithColor> tris, List<Vector3> ringA, List<Vector3> ringB)
        {
            for (int i = 0; i < ringA.Count; i++)
            {
                int next = (i + 1) % ringA.Count;
                string color = GetHullColor(ringA[i], ringA[next], preferLight: true);
                AddQuadOutward(tris, ringA[i], ringA[next], ringB[next], ringB[i], BodyCenter, color);
            }
        }

        private static string GetHullColor(Vector3 a, Vector3 b, bool preferLight)
        {
            float avgZ = (a.z + b.z) * 0.5f;
            float avgY = (Math.Abs(a.y) + Math.Abs(b.y)) * 0.5f;

            if (avgZ > avgY * 0.18f)
                return preferLight ? hullColorLight : hullColorMid;

            if (avgZ < -avgY * 0.18f)
                return undersideColor;

            return hullColorDark;
        }

        private static float GetTopAtX(float x)
        {
            if (x >= upperMidFrontX)
                return Lerp(bodyStart: frontRadiusZ * hullTopLift, bodyEnd: upperMidFrontRadiusZ * hullTopLift, x, frontRingX, upperMidFrontX);

            if (x >= centerMidX)
                return Lerp(bodyStart: upperMidFrontRadiusZ * hullTopLift, bodyEnd: centerMidRadiusZ * hullTopLift, x, upperMidFrontX, centerMidX);

            if (x >= rearMidX)
                return Lerp(bodyStart: centerMidRadiusZ * hullTopLift, bodyEnd: rearMidRadiusZ * hullTopLift, x, centerMidX, rearMidX);

            return Lerp(bodyStart: rearMidRadiusZ * hullTopLift, bodyEnd: tailRadiusZ * hullTopLift, x, rearMidX, tailRingX);
        }

        private static float Lerp(float bodyStart, float bodyEnd, float x, float x1, float x2)
        {
            if (Math.Abs(x2 - x1) < 0.0001f)
                return bodyStart;

            float t = (x - x1) / (x2 - x1);
            return bodyStart + (bodyEnd - bodyStart) * t;
        }

        private static void AddBlade(
            List<ITriangleMeshWithColor> tris,
            Vector3 center,
            Vector3 tip,
            float width,
            float thickness,
            string color)
        {
            // Simple thick lowpoly blade
            bool alongY = Math.Abs(tip.y - center.y) > Math.Abs(tip.z - center.z);

            Vector3 a, b, c, d, a2, b2, c2, d2;

            if (alongY)
            {
                a = new Vector3 { x = center.x, y = center.y + width * 0.4f, z = center.z + thickness };
                b = new Vector3 { x = center.x, y = center.y - width * 0.4f, z = center.z + thickness };
                c = new Vector3 { x = tip.x, y = tip.y - width, z = tip.z + thickness * 0.7f };
                d = new Vector3 { x = tip.x, y = tip.y + width, z = tip.z + thickness * 0.7f };

                a2 = new Vector3 { x = center.x - thickness, y = center.y + width * 0.4f, z = center.z - thickness };
                b2 = new Vector3 { x = center.x - thickness, y = center.y - width * 0.4f, z = center.z - thickness };
                c2 = new Vector3 { x = tip.x - thickness, y = tip.y - width, z = tip.z - thickness * 0.7f };
                d2 = new Vector3 { x = tip.x - thickness, y = tip.y + width, z = tip.z - thickness * 0.7f };
            }
            else
            {
                a = new Vector3 { x = center.x, y = center.y + width * 0.4f, z = center.z + thickness };
                b = new Vector3 { x = center.x, y = center.y - width * 0.4f, z = center.z + thickness };
                c = new Vector3 { x = tip.x, y = tip.y - width * 0.7f, z = tip.z - width };
                d = new Vector3 { x = tip.x, y = tip.y + width * 0.7f, z = tip.z + width };

                a2 = new Vector3 { x = center.x - thickness, y = center.y + width * 0.4f, z = center.z - thickness };
                b2 = new Vector3 { x = center.x - thickness, y = center.y - width * 0.4f, z = center.z - thickness };
                c2 = new Vector3 { x = tip.x - thickness, y = tip.y - width * 0.7f, z = tip.z - width };
                d2 = new Vector3 { x = tip.x - thickness, y = tip.y + width * 0.7f, z = tip.z + width };
            }

            AddQuadOutward(tris, a, b, c, d, BodyCenter, color);
            AddQuadOutward(tris, a2, d2, c2, b2, BodyCenter, hullColorDark);
            AddQuadOutward(tris, a, d, d2, a2, BodyCenter, color);
            AddQuadOutward(tris, b, b2, c2, c, BodyCenter, hullColorDark);
            AddQuadOutward(tris, a, a2, b2, b, BodyCenter, hullColorDark);
            AddQuadOutward(tris, d, c, c2, d2, BodyCenter, color);
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
