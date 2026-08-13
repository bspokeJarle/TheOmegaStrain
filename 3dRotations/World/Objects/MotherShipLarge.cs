using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public class MotherShipLarge
    {
        private const float ZoomRatio = 3.2f;
        private const float CrashboxSize = 1.15f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = -5f, y = 0f, z = 0f };
        private static readonly Vector3 LeftPodCenter = new Vector3 { x = -6f, y = -30f, z = 0f };
        private static readonly Vector3 RightPodCenter = new Vector3 { x = -6f, y = 30f, z = 0f };
        private static readonly Vector3 ReactorCenter = new Vector3 { x = -16f, y = 0f, z = 23f };
        private static readonly Vector3 BayCenter = new Vector3 { x = -12f, y = 0f, z = -13f };
        private static readonly Vector3 RearEngineCenter = new Vector3 { x = -62f, y = 0f, z = 0f };

        // Main carrier hull
        private static float noseX = 72f;
        private static float frontX = 44f;
        private static float midFrontX = 18f;
        private static float midBackX = -28f;
        private static float rearX = -58f;

        private static float frontHalfWidth = 18f;
        private static float midFrontHalfWidth = 34f;
        private static float midBackHalfWidth = 42f;
        private static float rearHalfWidth = 31f;

        private static float frontTop = 5.5f;
        private static float midFrontTop = 10.5f;
        private static float midBackTop = 11.5f;
        private static float rearTop = 8.5f;

        private static float frontBottom = -5.0f;
        private static float midFrontBottom = -8.5f;
        private static float midBackBottom = -10.5f;
        private static float rearBottom = -7.8f;

        // Drone bay
        private static float bayFrontX = 16f;
        private static float bayBackX = -34f;
        private static float bayHalfWidth = 18f;
        private static float bayLipZ = -10.8f;
        private static float bayInsideZ = -16.5f;

        // Side pods
        private static float podFrontX = 10f;
        private static float podBackX = -48f;
        private static float podInnerY = 44f;
        private static float podOuterY = 72f;
        private static float podTopZ = 7.5f;
        private static float podBottomZ = -7.0f;

        // Rear engine block
        private static float engineFrontX = -58f;
        private static float engineBackX = -74f;
        private static float engineHalfWidth = 22f;
        private static float engineTopZ = 8f;
        private static float engineBottomZ = -8f;

        // Reactor / weak spot
        private static float reactorFrontX = 3f;
        private static float reactorMidX = -16f;
        private static float reactorBackX = -36f;
        private static float reactorBaseZ = 14f;
        private static float reactorTopZ = 29f;
        private static float reactorHalfWidth = 6f;

        // Turret / tower
        private static float towerFrontX = -22f;
        private static float towerBackX = -42f;
        private static float towerHalfWidth = 10f;
        private static float towerBaseZ = 13f;
        private static float towerTopZ = 24f;

        // Colors
        private static string hullLight = "A8B0BA";
        private static string hullMid = "6F7986";
        private static string hullDark = "3C4652";
        private static string hullVeryDark = "1B2028";
        private static string underside = "11161D";

        private static string armor = "4E5966";
        private static string armorDark = "2B333D";
        private static string orange = "FF6A1F";
        private static string orangeDark = "A52A0C";
        private static string orangeBright = "FFD166";
        private static string reactor = "FF4A1C";
        private static string reactorDark = "8E1608";
        private static string teal = "2A8F92";
        private static string tealDark = "1A5E63";
        private static string podColor = "566371";
        private static string podDark = "252D36";
        private static string antenna = "C7D0DA";

        private static string hatchFrame   = "FFFFFF";   // white hatch panels
        private static string hatchInner   = "FFFFFF";   // white hatch interior
        private static string hatchGlow    = "FFFFFF";   // white glow bar
        private static string hatchGlowDim = "CCCCCC";   // white dim bar
        private static string accentTeal   = "2A8F92";
        private static string accentTealDk = "17555A";
        private static string accentStripe = "FFFFFF";   // white seam

        public static _3dObject CreateMotherShipLarge(ISurface parentSurface)
        {
            var ship = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "MotherShipLarge"
            };

            AddPart(ship, "WarCarrierHullTop", WarCarrierHullTop(), true);
            AddPart(ship, "WarCarrierHullBottom", WarCarrierHullBottom(), true);
            AddPart(ship, "WarCarrierHullSides", WarCarrierHullSides(), true);
            AddPart(ship, "WarCarrierNoseBlades", WarCarrierNoseBlades(), true);

            AddPart(ship, "DroneHangarBay", DroneHangarBay(), true);
            AddPart(ship, "HangarHatch", HangarHatch(), true);
            AddPart(ship, "LeftEnginePod", BuildSidePod(false), true);
            AddPart(ship, "RightEnginePod", BuildSidePod(true), true);
            AddPart(ship, "LeftPodConnector", BuildPodConnector(false), true);
            AddPart(ship, "RightPodConnector", BuildPodConnector(true), true);

            AddPart(ship, "RearEngineBlock", RearEngineBlock(), true);
            AddPart(ship, "TopReactorWeakSpot", TopReactorWeakSpot(), true);
            AddPart(ship, "CommandTower", CommandTower(), true);
            AddPart(ship, "ArmorPanels", ArmorPanels(), true);
            AddPart(ship, "FinsAndAntennas", FinsAndAntennas(), true);

            AddPart(ship, "DroneDropStartGuide", DroneDropStartGuide(), false);
            AddPart(ship, "DroneDropEndGuide", DroneDropEndGuide(), false);
            AddPart(ship, "ParticlesDirectionGuide", ParticlesDirectionGuide(), false);
            AddPart(ship, "ParticlesStartGuide", ParticlesStartGuide(), false);

            AddPart(ship, "LeftWingEngineStart",  WingEngineStart(isRight: false), false);
            AddPart(ship, "RightWingEngineStart", WingEngineStart(isRight: true),  false);
            AddPart(ship, "LeftWingEngineGuide",  WingEngineGuide(isRight: false), false);
            AddPart(ship, "RightWingEngineGuide", WingEngineGuide(isRight: true),  false);

            ship.Particles = new ParticlesAI();
            ship.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            ship.ParentSurface = parentSurface;
            ship.HasShadow = true;

            ship.CrashBoxes = MotherShipLargeCrashBoxes();
            ship.CrashBoxNames = MotherShipLargeCrashBoxNames();

            _3dObjectHelpers.ApplyScaleToObject(ship, ZoomRatio);
            _3dObjectHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);

            return ship;
        }

        // ----------------------------------------------------
        //  MAIN HULL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? WarCarrierHullTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var nose = new Vector3 { x = noseX, y = 0f, z = 2.5f };

            var fl = new Vector3 { x = frontX, y = -frontHalfWidth, z = frontTop };
            var fr = new Vector3 { x = frontX, y = frontHalfWidth, z = frontTop };

            var mfl = new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontTop };
            var mfr = new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontTop };

            var mbl = new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackTop };
            var mbr = new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackTop };

            var rl = new Vector3 { x = rearX, y = -rearHalfWidth, z = rearTop };
            var rr = new Vector3 { x = rearX, y = rearHalfWidth, z = rearTop };

            var cf = new Vector3 { x = frontX + 4f, y = 0f, z = frontTop + 3f };
            var cmf = new Vector3 { x = midFrontX + 2f, y = 0f, z = midFrontTop + 3f };
            var cmb = new Vector3 { x = midBackX + 2f, y = 0f, z = midBackTop + 2f };
            var cr = new Vector3 { x = rearX + 5f, y = 0f, z = rearTop + 1f };

            tris.Add(CreateTriangleOutward(fl, nose, cf, BodyCenter, hullMid));
            tris.Add(CreateTriangleOutward(nose, fr, cf, BodyCenter, hullMid));
            tris.Add(CreateTriangleOutward(fl, cf, mfl, BodyCenter, hullLight));
            tris.Add(CreateTriangleOutward(cf, cmf, mfl, BodyCenter, hullLight));
            tris.Add(CreateTriangleOutward(cf, fr, mfr, BodyCenter, hullLight));
            tris.Add(CreateTriangleOutward(cf, mfr, cmf, BodyCenter, hullLight));

            tris.Add(CreateTriangleOutward(mfl, cmf, mbl, BodyCenter, hullMid));
            tris.Add(CreateTriangleOutward(cmf, cmb, mbl, BodyCenter, tealDark));
            tris.Add(CreateTriangleOutward(cmf, mfr, mbr, BodyCenter, hullMid));
            tris.Add(CreateTriangleOutward(cmf, mbr, cmb, BodyCenter, tealDark));

            tris.Add(CreateTriangleOutward(mbl, cmb, rl, BodyCenter, hullDark));
            tris.Add(CreateTriangleOutward(cmb, cr, rl, BodyCenter, hullDark));
            tris.Add(CreateTriangleOutward(cmb, mbr, rr, BodyCenter, hullDark));
            tris.Add(CreateTriangleOutward(cmb, rr, cr, BodyCenter, hullDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? WarCarrierHullBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var nose = new Vector3 { x = noseX - 6f, y = 0f, z = -3f };

            var fl = new Vector3 { x = frontX, y = -frontHalfWidth, z = frontBottom };
            var fr = new Vector3 { x = frontX, y = frontHalfWidth, z = frontBottom };

            var mfl = new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontBottom };
            var mfr = new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontBottom };

            var mbl = new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackBottom };
            var mbr = new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackBottom };

            var rl = new Vector3 { x = rearX, y = -rearHalfWidth, z = rearBottom };
            var rr = new Vector3 { x = rearX, y = rearHalfWidth, z = rearBottom };

            var cf = new Vector3 { x = frontX + 3f, y = 0f, z = frontBottom - 2f };
            var cmf = new Vector3 { x = midFrontX + 2f, y = 0f, z = midFrontBottom - 2f };
            var cmb = new Vector3 { x = midBackX + 2f, y = 0f, z = midBackBottom - 2f };
            var cr = new Vector3 { x = rearX + 5f, y = 0f, z = rearBottom - 1f };

            tris.Add(CreateTriangleOutward(fl, cf, nose, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(nose, cf, fr, BodyCenter, underside));

            tris.Add(CreateTriangleOutward(fl, mfl, cf, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cf, mfl, cmf, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cf, mfr, fr, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cf, cmf, mfr, BodyCenter, underside));

            tris.Add(CreateTriangleOutward(mfl, mbl, cmf, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cmf, mbl, cmb, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cmf, mbr, mfr, BodyCenter, underside));
            tris.Add(CreateTriangleOutward(cmf, cmb, mbr, BodyCenter, underside));

            tris.Add(CreateTriangleOutward(mbl, rl, cmb, BodyCenter, hullVeryDark));
            tris.Add(CreateTriangleOutward(cmb, rl, cr, BodyCenter, hullVeryDark));
            tris.Add(CreateTriangleOutward(cmb, rr, mbr, BodyCenter, hullVeryDark));
            tris.Add(CreateTriangleOutward(cmb, cr, rr, BodyCenter, hullVeryDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? WarCarrierHullSides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left side
            AddQuadOutward(tris,
                new Vector3 { x = frontX, y = -frontHalfWidth, z = frontTop },
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontTop },
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontBottom },
                new Vector3 { x = frontX, y = -frontHalfWidth, z = frontBottom },
                BodyCenter, hullDark);

            AddQuadOutward(tris,
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontTop },
                new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackTop },
                new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackBottom },
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = midFrontBottom },
                BodyCenter, hullVeryDark);

            AddQuadOutward(tris,
                new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackTop },
                new Vector3 { x = rearX, y = -rearHalfWidth, z = rearTop },
                new Vector3 { x = rearX, y = -rearHalfWidth, z = rearBottom },
                new Vector3 { x = midBackX, y = -midBackHalfWidth, z = midBackBottom },
                BodyCenter, hullVeryDark);

            // Right side
            AddQuadOutward(tris,
                new Vector3 { x = frontX, y = frontHalfWidth, z = frontTop },
                new Vector3 { x = frontX, y = frontHalfWidth, z = frontBottom },
                new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontBottom },
                new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontTop },
                BodyCenter, hullDark);

            AddQuadOutward(tris,
                new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontTop },
                new Vector3 { x = midFrontX, y = midFrontHalfWidth, z = midFrontBottom },
                new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackBottom },
                new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackTop },
                BodyCenter, hullVeryDark);

            AddQuadOutward(tris,
                new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackTop },
                new Vector3 { x = midBackX, y = midBackHalfWidth, z = midBackBottom },
                new Vector3 { x = rearX, y = rearHalfWidth, z = rearBottom },
                new Vector3 { x = rearX, y = rearHalfWidth, z = rearTop },
                BodyCenter, hullVeryDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? WarCarrierNoseBlades()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Front fork / war-prongs
            AddProng(tris, -9f);
            AddProng(tris, 9f);

            return tris;
        }

        private static void AddProng(List<ITriangleMeshWithColor> tris, float y)
        {
            var baseTopL = new Vector3 { x = frontX + 2f, y = y - 2.2f, z = 2.2f };
            var baseTopR = new Vector3 { x = frontX + 2f, y = y + 2.2f, z = 2.2f };
            var baseBotL = new Vector3 { x = frontX + 2f, y = y - 2.2f, z = -1.8f };
            var baseBotR = new Vector3 { x = frontX + 2f, y = y + 2.2f, z = -1.8f };
            var tip = new Vector3 { x = noseX + 16f, y = y, z = 0f };

            tris.Add(CreateTriangleOutward(baseTopL, tip, baseTopR, BodyCenter, armor));
            tris.Add(CreateTriangleOutward(baseBotL, baseBotR, tip, BodyCenter, armorDark));
            AddQuadOutward(tris, baseTopL, baseBotL, tip, baseTopR, BodyCenter, armorDark);
            AddQuadOutward(tris, baseTopR, tip, baseBotR, baseBotL, BodyCenter, armorDark);
        }

        // ----------------------------------------------------
        //  DRONE BAY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DroneHangarBay()
        {
            // All bay geometry (walls, floor, lid) lives in HangarHatch so it rotates with the door.
            // This part is intentionally empty — kept for future decals added from the outside.
            return new List<ITriangleMeshWithColor>();
        }

        // ----------------------------------------------------
        //  HANGAR HATCH  (animated door over the drone bay)
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? HangarHatch()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Outer lip corners
            var fl = new Vector3 { x = bayFrontX,       y = -bayHalfWidth,        z = bayLipZ    };
            var fr = new Vector3 { x = bayFrontX,       y =  bayHalfWidth,        z = bayLipZ    };
            var bl = new Vector3 { x = bayBackX,        y = -bayHalfWidth,        z = bayLipZ    };
            var br = new Vector3 { x = bayBackX,        y =  bayHalfWidth,        z = bayLipZ    };

            // Inner opening corners (match bay interior)
            var ifl = new Vector3 { x = bayFrontX - 2f, y = -bayHalfWidth + 2.2f, z = bayInsideZ };
            var ifr = new Vector3 { x = bayFrontX - 2f, y =  bayHalfWidth - 2.2f, z = bayInsideZ };
            var ibl = new Vector3 { x = bayBackX  + 2f, y = -bayHalfWidth + 2.2f, z = bayInsideZ };
            var ibr = new Vector3 { x = bayBackX  + 2f, y =  bayHalfWidth - 2.2f, z = bayInsideZ };

            // Frame walls going inward — gives the part Z-depth so RotateXMesh produces a visible swing
            AddQuadOutward(tris, fl, fr, ifr, ifl, BayCenter, armor);
            AddQuadOutward(tris, bl, ibl, ibr, br, BayCenter, armor);
            AddQuadOutward(tris, fl, ifl, ibl, bl, BayCenter, armorDark);
            AddQuadOutward(tris, fr, br, ibr, ifr, BayCenter, armorDark);

            // Inner floor — black hole revealed when hatch opens
            AddQuadOutward(tris, ifl, ifr, ibr, ibl, BayCenter, "000000");

            // Outer lid panel — the visible door face
            AddQuadOutward(tris, fl, fr, br, bl, BayCenter, "FFFFFF");

            // Centre seam on the lid
            var mf = new Vector3 { x = bayFrontX - 0.5f, y = 0f, z = bayLipZ + 0.1f };
            var mb = new Vector3 { x = bayBackX  + 0.5f, y = 0f, z = bayLipZ + 0.1f };
            tris.Add(CreateTriangleOutward(mf, new Vector3 { x = mf.x, y = -0.6f, z = mf.z }, mb, BayCenter, "AAAAAA"));
            tris.Add(CreateTriangleOutward(mf, mb, new Vector3 { x = mb.x, y =  0.6f, z = mb.z }, BayCenter, "AAAAAA"));

            return tris;
        }

        // ----------------------------------------------------
        //  SIDE PODS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BuildSidePod(bool isRight)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = isRight ? 1f : -1f;
            var center = isRight ? RightPodCenter : LeftPodCenter;

            var fti = new Vector3 { x = podFrontX, y = s * podInnerY, z = podTopZ };
            var fto = new Vector3 { x = podFrontX, y = s * podOuterY, z = podTopZ - 1.5f };
            var fbi = new Vector3 { x = podFrontX, y = s * podInnerY, z = podBottomZ };
            var fbo = new Vector3 { x = podFrontX, y = s * podOuterY, z = podBottomZ + 1.2f };

            var bti = new Vector3 { x = podBackX, y = s * (podInnerY + 2f), z = podTopZ + 1f };
            var bto = new Vector3 { x = podBackX, y = s * podOuterY, z = podTopZ - 1f };
            var bbi = new Vector3 { x = podBackX, y = s * (podInnerY + 2f), z = podBottomZ };
            var bbo = new Vector3 { x = podBackX, y = s * podOuterY, z = podBottomZ + 1f };

            AddQuadOutward(tris, fti, fto, bto, bti, center, podColor);
            AddQuadOutward(tris, fbi, bbi, bbo, fbo, center, podDark);

            if (isRight)
            {
                AddQuadOutward(tris, fti, bti, bbi, fbi, center, podDark);
                AddQuadOutward(tris, fto, fbo, bbo, bto, center, podDark);
                AddQuadOutward(tris, fti, fbi, fbo, fto, center, podColor);
            }
            else
            {
                AddQuadOutward(tris, fti, fbi, bbi, bti, center, podDark);
                AddQuadOutward(tris, fto, bto, bbo, fbo, center, podDark);
                AddQuadOutward(tris, fti, fto, fbo, fbi, center, podColor);
            }

            AddQuadOutward(tris, bti, bto, bbo, bbi, center, orange);

            // Inner glow plate
            AddQuadOutward(tris,
                new Vector3 { x = podBackX - 0.2f, y = s * (podOuterY - 4f), z = 3.8f },
                new Vector3 { x = podBackX - 0.2f, y = s * (podInnerY + 5f), z = 3.2f },
                new Vector3 { x = podBackX - 0.2f, y = s * (podInnerY + 5f), z = -3.2f },
                new Vector3 { x = podBackX - 0.2f, y = s * (podOuterY - 4f), z = -3.8f },
                center,
                orangeBright);

            // Top armored cap
            var capA = new Vector3 { x = podFrontX - 6f, y = s * (podInnerY + 7f), z = podTopZ + 2.2f };
            var capB = new Vector3 { x = podBackX + 10f, y = s * (podInnerY + 9f), z = podTopZ + 2.8f };
            var capC = new Vector3 { x = podBackX + 4f, y = s * (podOuterY - 5f), z = podTopZ + 1.0f };
            var capD = new Vector3 { x = podFrontX - 2f, y = s * (podOuterY - 4f), z = podTopZ + 0.5f };

            if (isRight)
                AddQuadOutward(tris, capA, capD, capC, capB, center, armor);
            else
                AddQuadOutward(tris, capA, capB, capC, capD, center, armor);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? BuildPodConnector(bool isRight)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = isRight ? 1f : -1f;
            var center = isRight ? RightPodCenter : LeftPodCenter;

            var a = new Vector3 { x = 6f, y = s * 34f, z = 4f };
            var b = new Vector3 { x = -18f, y = s * 39f, z = 5f };
            var c = new Vector3 { x = -22f, y = s * podInnerY, z = 2f };
            var d = new Vector3 { x = 8f, y = s * podInnerY, z = 1f };

            var a2 = new Vector3 { x = 6f, y = s * 34f, z = -4f };
            var b2 = new Vector3 { x = -18f, y = s * 39f, z = -4f };
            var c2 = new Vector3 { x = -22f, y = s * podInnerY, z = -3f };
            var d2 = new Vector3 { x = 8f, y = s * podInnerY, z = -3f };

            if (isRight)
            {
                AddQuadOutward(tris, a, d, c, b, center, armorDark);
                AddQuadOutward(tris, a2, b2, c2, d2, center, hullVeryDark);
                AddQuadOutward(tris, a, a2, d2, d, center, armorDark);
                AddQuadOutward(tris, b, c, c2, b2, center, armorDark);
            }
            else
            {
                AddQuadOutward(tris, a, b, c, d, center, armorDark);
                AddQuadOutward(tris, a2, d2, c2, b2, center, hullVeryDark);
                AddQuadOutward(tris, a, d, d2, a2, center, armorDark);
                AddQuadOutward(tris, b, b2, c2, c, center, armorDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  REAR ENGINE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? RearEngineBlock()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ftl = new Vector3 { x = engineFrontX, y = -engineHalfWidth, z = engineTopZ };
            var ftr = new Vector3 { x = engineFrontX, y = engineHalfWidth, z = engineTopZ };
            var fbl = new Vector3 { x = engineFrontX, y = -engineHalfWidth, z = engineBottomZ };
            var fbr = new Vector3 { x = engineFrontX, y = engineHalfWidth, z = engineBottomZ };

            var btl = new Vector3 { x = engineBackX, y = -engineHalfWidth * 0.72f, z = engineTopZ * 0.8f };
            var btr = new Vector3 { x = engineBackX, y = engineHalfWidth * 0.72f, z = engineTopZ * 0.8f };
            var bbl = new Vector3 { x = engineBackX, y = -engineHalfWidth * 0.72f, z = engineBottomZ * 0.8f };
            var bbr = new Vector3 { x = engineBackX, y = engineHalfWidth * 0.72f, z = engineBottomZ * 0.8f };

            AddQuadOutward(tris, ftl, ftr, btr, btl, RearEngineCenter, armor);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, RearEngineCenter, podDark);
            AddQuadOutward(tris, ftr, fbr, bbr, btr, RearEngineCenter, podDark);
            AddQuadOutward(tris, fbl, ftl, btl, bbl, RearEngineCenter, podDark);
            AddQuadOutward(tris, btl, btr, bbr, bbl, RearEngineCenter, orange);

            AddQuadOutward(tris,
                new Vector3 { x = engineBackX - 0.3f, y = -8f, z = 4.5f },
                new Vector3 { x = engineBackX - 0.3f, y = 8f, z = 4.5f },
                new Vector3 { x = engineBackX - 0.3f, y = 8f, z = -4.5f },
                new Vector3 { x = engineBackX - 0.3f, y = -8f, z = -4.5f },
                RearEngineCenter,
                orangeBright);

            return tris;
        }

        // ----------------------------------------------------
        //  TOP REACTOR / TOWER / ARMOR
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? TopReactorWeakSpot()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var lf = new Vector3 { x = reactorFrontX, y = -reactorHalfWidth, z = reactorBaseZ };
            var rf = new Vector3 { x = reactorFrontX, y = reactorHalfWidth, z = reactorBaseZ };
            var lm = new Vector3 { x = reactorMidX, y = -reactorHalfWidth - 1f, z = reactorBaseZ + 2f };
            var rm = new Vector3 { x = reactorMidX, y = reactorHalfWidth + 1f, z = reactorBaseZ + 2f };
            var lb = new Vector3 { x = reactorBackX, y = -reactorHalfWidth, z = reactorBaseZ };
            var rb = new Vector3 { x = reactorBackX, y = reactorHalfWidth, z = reactorBaseZ };

            var tf = new Vector3 { x = reactorFrontX - 2f, y = 0f, z = reactorTopZ - 3f };
            var tm = new Vector3 { x = reactorMidX, y = 0f, z = reactorTopZ };
            var tb = new Vector3 { x = reactorBackX + 2f, y = 0f, z = reactorTopZ - 4f };

            tris.Add(CreateTriangleOutward(lf, tf, lm, ReactorCenter, orangeBright));
            tris.Add(CreateTriangleOutward(tf, tm, lm, ReactorCenter, reactor));
            tris.Add(CreateTriangleOutward(tf, rf, rm, ReactorCenter, orangeBright));
            tris.Add(CreateTriangleOutward(tf, rm, tm, ReactorCenter, reactor));

            tris.Add(CreateTriangleOutward(lm, tm, lb, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(tm, tb, lb, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(tm, rm, rb, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(tm, rb, tb, ReactorCenter, reactorDark));

            // raised core
            var coreTop = new Vector3 { x = reactorMidX, y = 0f, z = reactorTopZ + 5f };
            var coreLeft = new Vector3 { x = reactorMidX, y = -4f, z = reactorTopZ - 1f };
            var coreRight = new Vector3 { x = reactorMidX, y = 4f, z = reactorTopZ - 1f };
            var coreFront = new Vector3 { x = reactorMidX + 6f, y = 0f, z = reactorTopZ - 1f };
            var coreBack = new Vector3 { x = reactorMidX - 6f, y = 0f, z = reactorTopZ - 1f };
            var coreBottom = new Vector3 { x = reactorMidX, y = 0f, z = reactorTopZ - 6f };

            tris.Add(CreateTriangleOutward(coreTop, coreRight, coreFront, ReactorCenter, reactor));
            tris.Add(CreateTriangleOutward(coreTop, coreFront, coreLeft, ReactorCenter, reactor));
            tris.Add(CreateTriangleOutward(coreTop, coreLeft, coreBack, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(coreTop, coreBack, coreRight, ReactorCenter, reactorDark));

            tris.Add(CreateTriangleOutward(coreBottom, coreFront, coreRight, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(coreBottom, coreLeft, coreFront, ReactorCenter, reactorDark));
            tris.Add(CreateTriangleOutward(coreBottom, coreBack, coreLeft, ReactorCenter, reactor));
            tris.Add(CreateTriangleOutward(coreBottom, coreRight, coreBack, ReactorCenter, reactor));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? CommandTower()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var fl = new Vector3 { x = towerFrontX, y = -towerHalfWidth, z = towerBaseZ };
            var fr = new Vector3 { x = towerFrontX, y = towerHalfWidth, z = towerBaseZ };
            var bl = new Vector3 { x = towerBackX, y = -towerHalfWidth * 0.8f, z = towerBaseZ };
            var br = new Vector3 { x = towerBackX, y = towerHalfWidth * 0.8f, z = towerBaseZ };

            var tfl = new Vector3 { x = towerFrontX - 2f, y = -towerHalfWidth * 0.65f, z = towerTopZ };
            var tfr = new Vector3 { x = towerFrontX - 2f, y = towerHalfWidth * 0.65f, z = towerTopZ };
            var tbl = new Vector3 { x = towerBackX + 3f, y = -towerHalfWidth * 0.5f, z = towerTopZ - 2f };
            var tbr = new Vector3 { x = towerBackX + 3f, y = towerHalfWidth * 0.5f, z = towerTopZ - 2f };

            AddQuadOutward(tris, fl, fr, tfr, tfl, BodyCenter, armor);
            AddQuadOutward(tris, fr, br, tbr, tfr, BodyCenter, armorDark);
            AddQuadOutward(tris, bl, tbl, tbr, br, BodyCenter, armorDark);
            AddQuadOutward(tris, fl, tfl, tbl, bl, BodyCenter, armorDark);

            AddQuadOutward(tris,
                new Vector3 { x = towerFrontX - 1f, y = -5f, z = towerTopZ - 2f },
                new Vector3 { x = towerFrontX - 1f, y = 5f, z = towerTopZ - 2f },
                new Vector3 { x = towerFrontX - 1f, y = 4f, z = towerTopZ - 5f },
                new Vector3 { x = towerFrontX - 1f, y = -4f, z = towerTopZ - 5f },
                BodyCenter,
                tealDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? ArmorPanels()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // large raised panels, deliberately above hull
            AddPanel(tris, 32f, -8f, 16f, -22f, 6.8f, armor);
            AddPanel(tris, 32f, 8f, 16f, 22f, 6.8f, armor);
            AddPanel(tris, 8f, -12f, -12f, -28f, 12.8f, armorDark);
            AddPanel(tris, 8f, 12f, -12f, 28f, 12.8f, armorDark);
            AddPanel(tris, -22f, -14f, -48f, -26f, 13.2f, armor);
            AddPanel(tris, -22f, 14f, -48f, 26f, 13.2f, armor);

            // Teal accent strips flanking the reactor ridge — z lifted well above armour layer (max ~13.6f)
            AddPanel(tris, 10f, -6f, -8f, -18f, 15.2f, accentTeal);
            AddPanel(tris, 10f, 6f, -8f, 18f, 15.2f, accentTeal);

            // Gold stripe along mid-body centreline — z lifted above teal layer
            AddPanel(tris, 18f, -2.5f, -28f, -2.5f, 15.8f, accentStripe);
            AddPanel(tris, 18f, 2.5f, -28f, 2.5f, 15.8f, accentStripe);

            return tris;
        }

        private static void AddPanel(List<ITriangleMeshWithColor> tris, float x1, float y1, float x2, float y2, float z, string color)
        {
            float width = 4f;

            var a = new Vector3 { x = x1, y = y1 - width, z = z };
            var b = new Vector3 { x = x1, y = y1 + width, z = z + 0.4f };
            var c = new Vector3 { x = x2, y = y2 + width, z = z + 0.2f };
            var d = new Vector3 { x = x2, y = y2 - width, z = z - 0.2f };

            AddQuadOutward(tris, a, b, c, d, BodyCenter, color);
        }

        public static List<ITriangleMeshWithColor>? FinsAndAntennas()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddFin(tris, -36f, -24f, false);
            AddFin(tris, -36f, 24f, true);
            AddAntenna(tris, new Vector3 { x = -30f, y = -6f, z = towerTopZ }, new Vector3 { x = -36f, y = -12f, z = towerTopZ + 14f });
            AddAntenna(tris, new Vector3 { x = -30f, y = 6f, z = towerTopZ }, new Vector3 { x = -36f, y = 12f, z = towerTopZ + 14f });

            return tris;
        }

        private static void AddFin(List<ITriangleMeshWithColor> tris, float x, float y, bool right)
        {
            float s = right ? 1f : -1f;

            var a = new Vector3 { x = x, y = y, z = 11f };
            var b = new Vector3 { x = x - 14f, y = y + s * 3f, z = 28f };
            var c = new Vector3 { x = x - 16f, y = y + s * 2f, z = 10f };
            var a2 = new Vector3 { x = x, y = y - s * 1.2f, z = 11f };
            var b2 = new Vector3 { x = x - 14f, y = y + s * 1.8f, z = 28f };
            var c2 = new Vector3 { x = x - 16f, y = y + s * 0.8f, z = 10f };

            tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, armor));
            tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, armorDark));
            AddQuadOutward(tris, a, a2, b2, b, BodyCenter, armor);
            AddQuadOutward(tris, a, c, c2, a2, BodyCenter, armorDark);
            AddQuadOutward(tris, c, b, b2, c2, BodyCenter, armorDark);
        }

        private static void AddAntenna(List<ITriangleMeshWithColor> tris, Vector3 basePoint, Vector3 tip)
        {
            float w = 0.5f;
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = basePoint.x, y = basePoint.y - w, z = basePoint.z },
                new Vector3 { x = basePoint.x, y = basePoint.y + w, z = basePoint.z },
                tip,
                BodyCenter,
                antenna));

            tris.Add(CreateTriangleOutward(
                new Vector3 { x = basePoint.x - w, y = basePoint.y, z = basePoint.z - w },
                tip,
                new Vector3 { x = basePoint.x, y = basePoint.y + w, z = basePoint.z },
                BodyCenter,
                antenna));
        }

        // ----------------------------------------------------
        //  GUIDES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DroneDropStartGuide()
        {
            float centerX = (bayFrontX + bayBackX) * 0.5f;
            float centerZ = bayInsideZ - 1f;

            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = centerX + 4f, y =  5f, z = centerZ },
                    new Vector3 { x = centerX + 4f, y = -5f, z = centerZ },
                    new Vector3 { x = centerX - 5f, y =  0f, z = centerZ },
                    BayCenter,
                    "FF3333",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? DroneDropEndGuide()
        {
            float centerX = (bayFrontX + bayBackX) * 0.5f;
            float endZ = bayInsideZ - 90f;

            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = centerX + 14f, y =  14f, z = endZ },
                    new Vector3 { x = centerX + 14f, y = -14f, z = endZ },
                    new Vector3 { x = centerX - 14f, y =  0f,  z = endZ },
                    BayCenter,
                    "33FF33",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? ParticlesDirectionGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = engineBackX - 2f, y =  8f, z =  2f },
                    new Vector3 { x = engineBackX - 2f, y = -8f, z =  2f },
                    new Vector3 { x = engineBackX - 28f, y =  0f, z =  0f },
                    RearEngineCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? ParticlesStartGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = engineFrontX + 2f, y =  8f, z =  2f },
                    new Vector3 { x = engineFrontX + 2f, y = -8f, z =  2f },
                    new Vector3 { x = engineBackX + 2f, y =  0f, z =  0f },
                    RearEngineCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        // Start: triangle at the glow plate centre on the pod rear face.
        // Guide: same triangle pushed in local X along engine exhaust direction.
        private const float WingEngineGuideOffsetX = 100f;

        public static List<ITriangleMeshWithColor>? WingEngineStart(bool isRight)
        {
            float s = isRight ? 1f : -1f;
            float cx = podBackX - 0.2f;                                  // rear glow-plate face
            float cy = s * (podInnerY + 5f + podOuterY - 4f) * 0.5f;  // s*58.5 glow plate centre
            float cz = 0f;

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor
                {
                    Color = "ff8800",
                    noHidden = true,
                    vert1 = new Vector3 { x = cx, y = cy, z = cz },
                    vert2 = new Vector3 { x = cx, y = cy, z = cz },
                    vert3 = new Vector3 { x = cx, y = cy, z = cz },
                }
            };
        }

        public static List<ITriangleMeshWithColor>? WingEngineGuide(bool isRight)
        {
            float s = isRight ? 1f : -1f;
            float cx = (podBackX - 0.2f) + WingEngineGuideOffsetX;
            float cy = s * (podInnerY + 5f + podOuterY - 4f) * 0.5f;
            float cz = 0f;

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor
                {
                    Color = "ffffff",
                    noHidden = true,
                    vert1 = new Vector3 { x = cx, y = cy, z = cz },
                    vert2 = new Vector3 { x = cx, y = cy, z = cz },
                    vert3 = new Vector3 { x = cx, y = cy, z = cz },
                }
            };
        }

        // ----------------------------------------------------
        //  CRASHBOXES
        // ----------------------------------------------------

        public static List<List<IVector3>>? MotherShipLargeCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            AddCrashBox(boxes,
                new Vector3 { x = frontX, y = -frontHalfWidth, z = frontBottom },
                new Vector3 { x = noseX + 16f, y = frontHalfWidth, z = frontTop + 5f });

            AddCrashBox(boxes,
                new Vector3 { x = rearX, y = -midBackHalfWidth, z = midBackBottom - 2f },
                new Vector3 { x = midFrontX + 4f, y = midBackHalfWidth, z = midBackTop + 5f });

            AddCrashBox(boxes,
                new Vector3 { x = bayBackX - 2f, y = -bayHalfWidth - 2f, z = bayInsideZ - 2f },
                new Vector3 { x = bayFrontX + 2f, y = bayHalfWidth + 2f, z = bayLipZ + 2f });

            AddCrashBox(boxes,
                new Vector3 { x = podBackX, y = -podOuterY, z = podBottomZ - 2f },
                new Vector3 { x = podFrontX, y = -podInnerY, z = podTopZ + 4f });

            AddCrashBox(boxes,
                new Vector3 { x = podBackX, y = podInnerY, z = podBottomZ - 2f },
                new Vector3 { x = podFrontX, y = podOuterY, z = podTopZ + 4f });

            AddCrashBox(boxes,
                new Vector3 { x = engineBackX, y = -engineHalfWidth, z = engineBottomZ - 2f },
                new Vector3 { x = engineFrontX, y = engineHalfWidth, z = engineTopZ + 2f });

            AddCrashBox(boxes,
                new Vector3 { x = reactorBackX - 4f, y = -reactorHalfWidth - 3f, z = reactorBaseZ - 1f },
                new Vector3 { x = reactorFrontX + 4f, y = reactorHalfWidth + 3f, z = reactorTopZ + 7f });

            return boxes;
        }

        private static void AddCrashBox(List<List<IVector3>> boxes, Vector3 min, Vector3 max)
        {
            var bounds = ScaleCrashBoxBounds(min, max);
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bounds.min, bounds.max));
        }

        public static List<string?> MotherShipLargeCrashBoxNames()
        {
            return new List<string?>
            {
                "FrontWarHull",
                "MainHull",
                "DroneBay",
                "LeftEnginePod",
                "RightEnginePod",
                "RearEngine",
                "ReactorWeakSpot"
            };
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

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