using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using static Domain._3dSpecificsImplementations;
using static _3dRotations.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public class MotherShipSmall
    {
        private const float ZoomRatio = 1.3f;

        // ----------------------------------------------------
        //  GEOMETRY PARAMETERS
        // ----------------------------------------------------

        private static float noseTipX = 104f;
        private static float frontX = 56f;
        private static float midFrontX = 16f;
        private static float midBackX = -28f;
        private static float backX = -68f;

        private static float frontHalfWidth = 24f;
        private static float midFrontHalfWidth = 60f;
        private static float midBackHalfWidth = 68f;
        private static float backHalfWidth = 40f;

        private static float topFront = 11f;
        private static float topMidFront = 17f;
        private static float topMidBack = 14.4f;
        private static float topBack = 9.6f;

        private static float bottomFront = -8.4f;
        private static float bottomMidFront = -12.0f;
        private static float bottomMidBack = -13.6f;
        private static float bottomBack = -9.2f;

        private static float wingRootFrontX = 32f;
        private static float wingRootBackX = -16f;
        private static float wingTipFrontX = 8f;
        private static float wingTipBackX = -20f;
        private static float wingTipY = 112f;
        private static float wingTopZ = 7.2f;
        private static float wingBottomZ = -5.6f;

        private static float towerFrontX = -2f;
        private static float towerBackX = -20f;
        private static float towerHalfWidthFront = 16f;
        private static float towerHalfWidthBack = 12f;
        private static float towerBaseZ = 20.4f; // lifted slightly to avoid coplanar overlap
        private static float towerTopZ = 48f;

        private static float weakSpotCenterX = -11f;
        private static float weakSpotRadius = 20.0f;
        private static float weakSpotCenterZ = 58.0f;

        private static float ventDepth = 3.6f;
        private static float ventHalfHeight = 4.6f;
        private static float ventHalfWidth = 9.6f;

        private const float CrashboxSize = 1.3f;

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string hullColorLight = "C9CDD5";
        private static string hullColorMid = "8E96A2";
        private static string hullColorDark = "59616C";
        private static string hullColorVeryDark = "30363E";
        private static string undersideColor = "252A31";

        private static string accentPanelColor = "2FA4A0";
        private static string accentPanelColorDark = "1E6F6C";
        private static string wingTipColor = "D98B2B";
        private static string wingTipColorDark = "8D5615";

        private static string towerColor = "747C87";
        private static string towerColorDark = "49505B";

        private static string weakSpotColor = "FF5A24";
        private static string weakSpotColorDark = "9B240D";

        private static string ventGlowColor = "FF6A1A";
        private static string ventGlowColorDark = "8A310B";

        private static string noseHighlightColor = "B0C4D8";
        private static string wingPanelColor = "6E7D8E";
        private static string wingPanelColorDark = "4D5B6A";
        private static string bridgeWindowColor = "1C3D5E";
        private static string engineGlowBright = "FFB347";

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        public static _3dObject CreateMotherShipSmall(ISurface parentSurface)
        {
            var hullTop = MotherShipHullTop();
            var hullBottom = MotherShipHullBottom();
            var hullSides = MotherShipHullSides();
            var wingsTop = MotherShipWingsTop();
            var wingsBottom = MotherShipWingsBottom();
            var wingSides = MotherShipWingSides();
            var topAccentPanels = MotherShipTopAccentPanels();
            var sideVents = MotherShipSideVents();
            var rearFace = MotherShipRearFace();
            var tower = MotherShipTower();
            var weakSpot = MotherShipWeakSpot();

            var crashBoxes = MotherShipCrashBoxes();

            var ship = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "MotherShipSmall"
            };

            AddPart(ship, "MotherShipHullTop", hullTop, true);
            AddPart(ship, "MotherShipHullBottom", hullBottom, true);
            AddPart(ship, "MotherShipHullSides", hullSides, true);
            AddPart(ship, "MotherShipWingsTop", wingsTop, true);
            AddPart(ship, "MotherShipWingsBottom", wingsBottom, true);
            AddPart(ship, "MotherShipWingSides", wingSides, true);
            AddPart(ship, "MotherShipTopAccentPanels", topAccentPanels, true);
            AddPart(ship, "MotherShipSideVents", sideVents, true);
            AddPart(ship, "MotherShipRearFace", rearFace, true);
            AddPart(ship, "MotherShipTower", tower, true);
            AddPart(ship, "MotherShipWeakSpot", weakSpot, true);

            ship.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            ship.ParentSurface = parentSurface;
            ship.HasShadow = true;

            if (crashBoxes != null)
                ship.CrashBoxes = crashBoxes;

            ship.CrashBoxNames = MotherShipCrashBoxNames();

            _3dObjectHelpers.ApplyScaleToObject(ship, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);

            return ship;
        }

        // ----------------------------------------------------
        //  SHARED RINGS
        // ----------------------------------------------------

        private static List<Vector3> GetFrontRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = frontX, y =  0f,                 z =  topFront + 1.6f },                  // 0 top
                new Vector3 { x = frontX, y =  frontHalfWidth*0.55f, z =  topFront*0.80f },               // 1
                new Vector3 { x = frontX, y =  frontHalfWidth,     z =  0.4f },                           // 2
                new Vector3 { x = frontX, y =  frontHalfWidth*0.72f, z =  bottomFront*0.10f },            // 3
                new Vector3 { x = frontX, y =  0f,                 z =  bottomFront + 1.8f },             // 4
                new Vector3 { x = frontX, y = -frontHalfWidth*0.72f, z =  bottomFront*0.10f },            // 5
                new Vector3 { x = frontX, y = -frontHalfWidth,     z =  0.4f },                           // 6
                new Vector3 { x = frontX, y = -frontHalfWidth*0.55f, z =  topFront*0.80f }               // 7
            };
        }

        private static List<Vector3> GetMidFrontRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = midFrontX, y =  0f,                        z = topMidFront + 2.2f }, // 0
                new Vector3 { x = midFrontX, y =  midFrontHalfWidth*0.55f,  z = topMidFront + 0.8f }, // 1
                new Vector3 { x = midFrontX, y =  midFrontHalfWidth,        z = 0.8f },               // 2
                new Vector3 { x = midFrontX, y =  midFrontHalfWidth*0.75f,  z = bottomMidFront*0.08f }, // 3
                new Vector3 { x = midFrontX, y =  0f,                        z = bottomMidFront + 1.8f }, // 4
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth*0.75f,  z = bottomMidFront*0.08f }, // 5
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth,        z = 0.8f },               // 6
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth*0.55f,  z = topMidFront + 0.8f }  // 7
            };
        }

        private static List<Vector3> GetMidBackRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = midBackX, y =  0f,                       z = topMidBack + 1.8f }, // 0
                new Vector3 { x = midBackX, y =  midBackHalfWidth*0.55f,  z = topMidBack + 0.5f }, // 1
                new Vector3 { x = midBackX, y =  midBackHalfWidth,        z = 0.6f },               // 2
                new Vector3 { x = midBackX, y =  midBackHalfWidth*0.76f,  z = bottomMidBack*0.07f }, // 3
                new Vector3 { x = midBackX, y =  0f,                       z = bottomMidBack + 2.0f }, // 4
                new Vector3 { x = midBackX, y = -midBackHalfWidth*0.76f,  z = bottomMidBack*0.07f }, // 5
                new Vector3 { x = midBackX, y = -midBackHalfWidth,        z = 0.6f },               // 6
                new Vector3 { x = midBackX, y = -midBackHalfWidth*0.55f,  z = topMidBack + 0.5f }  // 7
            };
        }

        private static List<Vector3> GetBackRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = backX, y =  0f,                  z = topBack + 1.2f },               // 0
                new Vector3 { x = backX, y =  backHalfWidth*0.55f, z = topBack + 0.3f },             // 1
                new Vector3 { x = backX, y =  backHalfWidth,       z = 0.4f },                         // 2
                new Vector3 { x = backX, y =  backHalfWidth*0.7f,  z = bottomBack*0.06f },            // 3
                new Vector3 { x = backX, y =  0f,                  z = bottomBack + 1.6f },           // 4
                new Vector3 { x = backX, y = -backHalfWidth*0.7f,  z = bottomBack*0.06f },            // 5
                new Vector3 { x = backX, y = -backHalfWidth,       z = 0.4f },                         // 6
                new Vector3 { x = backX, y = -backHalfWidth*0.55f, z = topBack + 0.3f }              // 7
            };
        }

        // ----------------------------------------------------
        //  HULL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MotherShipHullTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var midFront = GetMidFrontRing();
            var midBack = GetMidBackRing();
            var back = GetBackRing();

            var noseTop = new Vector3 { x = noseTipX, y = 0f, z = 7.6f };
            tris.Add(CreateTriangleOutward(front[7], noseTop, front[1], BodyCenter, noseHighlightColor));
            tris.Add(CreateTriangleOutward(front[7], front[1], front[6], BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(front[1], front[2], front[6], BodyCenter, hullColorMid));

            var centerFront = new Vector3 { x = frontX + 6f, y = 0f, z = topFront + 3.6f };
            var centerMidFront = new Vector3 { x = midFrontX + 2f, y = 0f, z = topMidFront + 2.6f };
            var centerMidBack = new Vector3 { x = midBackX + 2f, y = 0f, z = topMidBack + 2.0f };
            var centerBack = new Vector3 { x = backX + 6f, y = 0f, z = topBack + 1.4f };

            tris.Add(CreateTriangleOutward(front[7], centerFront, midFront[7], BodyCenter, hullColorLight));
            tris.Add(CreateTriangleOutward(front[1], midFront[1], centerFront, BodyCenter, hullColorLight));
            tris.Add(CreateTriangleOutward(centerFront, midFront[1], centerMidFront, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerFront, centerMidFront, midFront[7], BodyCenter, hullColorMid));

            tris.Add(CreateTriangleOutward(midFront[7], centerMidFront, midBack[7], BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerMidFront, centerMidBack, midBack[7], BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(midFront[1], midBack[1], centerMidFront, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerMidFront, midBack[1], centerMidBack, BodyCenter, hullColorMid));

            tris.Add(CreateTriangleOutward(midBack[7], centerMidBack, back[7], BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerMidBack, centerBack, back[7], BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(midBack[1], back[1], centerMidBack, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerMidBack, back[1], centerBack, BodyCenter, hullColorDark));

            tris.Add(CreateTriangleOutward(midFront[6], midFront[7], centerMidFront, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(midFront[1], centerMidFront, midFront[2], BodyCenter, hullColorMid));

            tris.Add(CreateTriangleOutward(midBack[6], centerMidBack, midBack[7], BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(midBack[1], midBack[2], centerMidBack, BodyCenter, hullColorDark));

            tris.Add(CreateTriangleOutward(back[6], centerBack, back[7], BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(back[1], back[2], centerBack, BodyCenter, hullColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MotherShipHullBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var midFront = GetMidFrontRing();
            var midBack = GetMidBackRing();
            var back = GetBackRing();

            var noseBottom = new Vector3 { x = noseTipX - 8f, y = 0f, z = -5.0f };
            tris.Add(CreateTriangleOutward(front[5], front[4], front[3], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[5], noseBottom, front[4], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[4], noseBottom, front[3], BodyCenter, undersideColor));

            var centerFront = new Vector3 { x = frontX + 8f, y = 0f, z = bottomFront - 2.0f };
            var centerMidFront = new Vector3 { x = midFrontX + 2f, y = 0f, z = bottomMidFront - 2.2f };
            var centerMidBack = new Vector3 { x = midBackX + 2f, y = 0f, z = bottomMidBack - 1.8f };
            var centerBack = new Vector3 { x = backX + 6f, y = 0f, z = bottomBack - 1.4f };

            tris.Add(CreateTriangleOutward(front[5], centerFront, midFront[5], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[3], midFront[3], centerFront, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, centerMidFront, midFront[5], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, midFront[3], centerMidFront, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(midFront[5], centerMidFront, centerMidBack, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midFront[5], centerMidBack, midBack[5], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midFront[3], centerMidBack, centerMidFront, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midFront[3], midBack[3], centerMidBack, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(midBack[5], centerMidBack, centerBack, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midBack[5], centerBack, back[5], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midBack[3], centerBack, centerMidBack, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(midBack[3], back[3], centerBack, BodyCenter, undersideColor));

            AddQuadOutward(tris, back[5], back[3], back[2], back[6], BodyCenter, hullColorVeryDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MotherShipHullSides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var midFront = GetMidFrontRing();
            var midBack = GetMidBackRing();
            var back = GetBackRing();

            AddQuadOutward(tris, front[7], midFront[7], midFront[6], front[6], BodyCenter, hullColorDark);
            AddQuadOutward(tris, midFront[7], midBack[7], midBack[6], midFront[6], BodyCenter, hullColorDark);
            AddQuadOutward(tris, midBack[7], back[7], back[6], midBack[6], BodyCenter, hullColorVeryDark);

            AddQuadOutward(tris, front[6], midFront[6], midFront[5], front[5], BodyCenter, hullColorVeryDark);
            AddQuadOutward(tris, midFront[6], midBack[6], midBack[5], midFront[5], BodyCenter, hullColorVeryDark);
            AddQuadOutward(tris, midBack[6], back[6], back[5], midBack[5], BodyCenter, hullColorVeryDark);

            AddQuadOutward(tris, front[1], front[2], midFront[2], midFront[1], BodyCenter, hullColorDark);
            AddQuadOutward(tris, midFront[1], midFront[2], midBack[2], midBack[1], BodyCenter, hullColorDark);
            AddQuadOutward(tris, midBack[1], midBack[2], back[2], back[1], BodyCenter, hullColorVeryDark);

            AddQuadOutward(tris, front[2], front[3], midFront[3], midFront[2], BodyCenter, hullColorVeryDark);
            AddQuadOutward(tris, midFront[2], midFront[3], midBack[3], midBack[2], BodyCenter, hullColorVeryDark);
            AddQuadOutward(tris, midBack[2], midBack[3], back[3], back[2], BodyCenter, hullColorVeryDark);

            return tris;
        }

        // ----------------------------------------------------
        //  WINGS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MotherShipWingsTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left
            {
                var a = new Vector3 { x = wingRootFrontX, y = -midFrontHalfWidth + 2f, z = wingTopZ };
                var b = new Vector3 { x = wingRootBackX, y = -midBackHalfWidth + 2f, z = wingTopZ - 0.8f };
                var c = new Vector3 { x = wingTipFrontX, y = -wingTipY + 16f, z = wingTopZ - 0.2f };
                var d = new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingTopZ - 1.4f };
                var e = new Vector3 { x = 20f, y = -96f, z = wingTopZ + 0.4f };

                tris.Add(CreateTriangleOutward(a, e, b, BodyCenter, wingPanelColor));
                tris.Add(CreateTriangleOutward(e, c, d, BodyCenter, wingPanelColorDark));
                tris.Add(CreateTriangleOutward(e, d, b, BodyCenter, wingPanelColor));
                tris.Add(CreateTriangleOutward(a, c, e, BodyCenter, wingPanelColorDark));
            }

            // Right
            {
                var a = new Vector3 { x = wingRootFrontX, y = midFrontHalfWidth - 2f, z = wingTopZ };
                var b = new Vector3 { x = wingRootBackX, y = midBackHalfWidth - 2f, z = wingTopZ - 0.8f };
                var c = new Vector3 { x = wingTipFrontX, y = wingTipY - 16f, z = wingTopZ - 0.2f };
                var d = new Vector3 { x = wingTipBackX, y = wingTipY, z = wingTopZ - 1.4f };
                var e = new Vector3 { x = 20f, y = 96f, z = wingTopZ + 0.4f };

                tris.Add(CreateTriangleOutward(a, b, e, BodyCenter, wingPanelColor));
                tris.Add(CreateTriangleOutward(e, d, c, BodyCenter, wingPanelColorDark));
                tris.Add(CreateTriangleOutward(e, b, d, BodyCenter, wingPanelColor));
                tris.Add(CreateTriangleOutward(a, e, c, BodyCenter, wingPanelColorDark));
            }

            // Colored wing tip caps
            {
                var lt1 = new Vector3 { x = wingTipFrontX, y = -wingTipY + 16f, z = wingTopZ - 0.2f };
                var lt2 = new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingTopZ - 1.4f };
                var lt3 = new Vector3 { x = wingTipBackX + 4f, y = -wingTipY + 10f, z = 0.8f };
                tris.Add(CreateTriangleOutward(lt1, lt2, lt3, BodyCenter, wingTipColor));

                var rt1 = new Vector3 { x = wingTipFrontX, y = wingTipY - 16f, z = wingTopZ - 0.2f };
                var rt2 = new Vector3 { x = wingTipBackX, y = wingTipY, z = wingTopZ - 1.4f };
                var rt3 = new Vector3 { x = wingTipBackX + 4f, y = wingTipY - 10f, z = 0.8f };
                tris.Add(CreateTriangleOutward(rt1, rt3, rt2, BodyCenter, wingTipColor));
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MotherShipWingsBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left
            {
                var a = new Vector3 { x = wingRootFrontX, y = -midFrontHalfWidth + 2f, z = wingBottomZ };
                var b = new Vector3 { x = wingRootBackX, y = -midBackHalfWidth + 2f, z = wingBottomZ - 0.4f };
                var c = new Vector3 { x = wingTipFrontX, y = -wingTipY + 16f, z = wingBottomZ + 0.4f };
                var d = new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingBottomZ + 0.8f };
                var e = new Vector3 { x = 20f, y = -96f, z = wingBottomZ - 0.4f };

                tris.Add(CreateTriangleOutward(a, b, e, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(e, d, c, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(e, b, d, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(a, e, c, BodyCenter, undersideColor));
            }

            // Right
            {
                var a = new Vector3 { x = wingRootFrontX, y = midFrontHalfWidth - 2f, z = wingBottomZ };
                var b = new Vector3 { x = wingRootBackX, y = midBackHalfWidth - 2f, z = wingBottomZ - 0.4f };
                var c = new Vector3 { x = wingTipFrontX, y = wingTipY - 16f, z = wingBottomZ + 0.4f };
                var d = new Vector3 { x = wingTipBackX, y = wingTipY, z = wingBottomZ + 0.8f };
                var e = new Vector3 { x = 20f, y = 96f, z = wingBottomZ - 0.4f };

                tris.Add(CreateTriangleOutward(a, e, b, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(e, c, d, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(e, d, b, BodyCenter, undersideColor));
                tris.Add(CreateTriangleOutward(a, c, e, BodyCenter, undersideColor));
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MotherShipWingSides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left wing edges
            {
                var ta = new Vector3 { x = wingRootFrontX, y = -midFrontHalfWidth + 2f, z = wingTopZ };
                var tb = new Vector3 { x = wingRootBackX, y = -midBackHalfWidth + 2f, z = wingTopZ - 0.8f };
                var tc = new Vector3 { x = wingTipFrontX, y = -wingTipY + 16f, z = wingTopZ - 0.2f };
                var td = new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingTopZ - 1.4f };

                var ba = new Vector3 { x = wingRootFrontX, y = -midFrontHalfWidth + 2f, z = wingBottomZ };
                var bb = new Vector3 { x = wingRootBackX, y = -midBackHalfWidth + 2f, z = wingBottomZ - 0.4f };
                var bc = new Vector3 { x = wingTipFrontX, y = -wingTipY + 16f, z = wingBottomZ + 0.4f };
                var bd = new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingBottomZ + 0.8f };

                AddQuadOutward(tris, ta, tc, bc, ba, BodyCenter, hullColorDark);
                AddQuadOutward(tris, tc, td, bd, bc, BodyCenter, wingTipColorDark);
                AddQuadOutward(tris, td, tb, bb, bd, BodyCenter, hullColorVeryDark);
            }

            // Right wing edges
            {
                var ta = new Vector3 { x = wingRootFrontX, y = midFrontHalfWidth - 2f, z = wingTopZ };
                var tb = new Vector3 { x = wingRootBackX, y = midBackHalfWidth - 2f, z = wingTopZ - 0.8f };
                var tc = new Vector3 { x = wingTipFrontX, y = wingTipY - 16f, z = wingTopZ - 0.2f };
                var td = new Vector3 { x = wingTipBackX, y = wingTipY, z = wingTopZ - 1.4f };

                var ba = new Vector3 { x = wingRootFrontX, y = midFrontHalfWidth - 2f, z = wingBottomZ };
                var bb = new Vector3 { x = wingRootBackX, y = midBackHalfWidth - 2f, z = wingBottomZ - 0.4f };
                var bc = new Vector3 { x = wingTipFrontX, y = wingTipY - 16f, z = wingBottomZ + 0.4f };
                var bd = new Vector3 { x = wingTipBackX, y = wingTipY, z = wingBottomZ + 0.8f };

                AddQuadOutward(tris, ta, ba, bc, tc, BodyCenter, hullColorDark);
                AddQuadOutward(tris, tc, bc, bd, td, BodyCenter, wingTipColorDark);
                AddQuadOutward(tris, td, bd, bb, tb, BodyCenter, hullColorVeryDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  ACCENT PANELS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MotherShipTopAccentPanels()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Center top teal panel - raised well above hull ridge
            var a = new Vector3 { x = 20f, y = -10f, z = 26.0f };
            var b = new Vector3 { x = 4f, y = -14f, z = 27.0f };
            var c = new Vector3 { x = 0f, y = 0f, z = 27.6f };
            var d = new Vector3 { x = 4f, y = 14f, z = 27.0f };
            var e = new Vector3 { x = 20f, y = 10f, z = 26.0f };

            tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, accentPanelColor));
            tris.Add(CreateTriangleOutward(a, c, e, BodyCenter, accentPanelColor));
            tris.Add(CreateTriangleOutward(c, d, e, BodyCenter, accentPanelColorDark));

            // Side top accent strips - raised to clear hull and tower base
            var l1 = new Vector3 { x = 4f, y = -40f, z = 24.4f };
            var l2 = new Vector3 { x = -12f, y = -48f, z = 23.4f };
            var l3 = new Vector3 { x = -16f, y = -36f, z = 23.2f };
            tris.Add(CreateTriangleOutward(l1, l2, l3, BodyCenter, accentPanelColorDark));

            var r1 = new Vector3 { x = 4f, y = 40f, z = 24.4f };
            var r2 = new Vector3 { x = -12f, y = 48f, z = 23.4f };
            var r3 = new Vector3 { x = -16f, y = 36f, z = 23.2f };
            tris.Add(CreateTriangleOutward(r1, r3, r2, BodyCenter, accentPanelColorDark));

            return tris;
        }

        // ----------------------------------------------------
        //  SIDE VENTS + REAR
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MotherShipSideVents()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddSideVent(tris, midBackX + 4f, -midBackHalfWidth + 2.2f, false);
            AddSideVent(tris, midBackX + 4f, midBackHalfWidth - 2.2f, true);

            return tris;
        }

        private static void AddSideVent(List<ITriangleMeshWithColor> tris, float x, float yOuter, bool rightSide)
        {
            float yInner = rightSide ? yOuter - ventDepth : yOuter + ventDepth;

            var topOuter = new Vector3 { x = x, y = yOuter, z = ventHalfHeight };
            var botOuter = new Vector3 { x = x, y = yOuter, z = -ventHalfHeight };
            var topInner = new Vector3 { x = x - 18f, y = yInner, z = ventHalfHeight - 0.6f };
            var botInner = new Vector3 { x = x - 18f, y = yInner, z = -ventHalfHeight + 0.6f };

            var topOuter2 = new Vector3 { x = x, y = yOuter, z = ventHalfHeight + 9.0f };
            var botOuter2 = new Vector3 { x = x, y = yOuter, z = -ventHalfHeight - 1.6f };
            var topInner2 = new Vector3 { x = x - 18f, y = yInner, z = ventHalfHeight + 7.6f };
            var botInner2 = new Vector3 { x = x - 18f, y = yInner, z = -ventHalfHeight - 0.8f };

            AddQuadOutward(tris, topOuter2, topOuter, botOuter, botOuter2, BodyCenter, hullColorVeryDark);
            AddQuadOutward(tris, topOuter, topInner, botInner, botOuter, BodyCenter, ventGlowColor);
            AddQuadOutward(tris, topInner2, topInner, botInner, botInner2, BodyCenter, ventGlowColorDark);
        }

        public static List<ITriangleMeshWithColor>? MotherShipRearFace()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var topLeft = new Vector3 { x = backX, y = -backHalfWidth, z = topBack };
            var topRight = new Vector3 { x = backX, y = backHalfWidth, z = topBack };
            var bottomLeft = new Vector3 { x = backX, y = -backHalfWidth, z = bottomBack };
            var bottomRight = new Vector3 { x = backX, y = backHalfWidth, z = bottomBack };

            AddQuadOutward(tris, topLeft, topRight, bottomRight, bottomLeft, BodyCenter, hullColorVeryDark);

            // Rear engine glow blocks, slightly recessed
            AddRearGlow(tris, -24f, -10f);
            AddRearGlow(tris, -8f, 8f, engineGlowBright);
            AddRearGlow(tris, 10f, 24f);

            return tris;
        }

        private static void AddRearGlow(List<ITriangleMeshWithColor> tris, float yMin, float yMax, string? color = null)
        {
            var tl = new Vector3 { x = backX - 0.2f, y = yMin, z = 3.6f };
            var tr = new Vector3 { x = backX - 0.2f, y = yMax, z = 3.6f };
            var bl = new Vector3 { x = backX - 0.2f, y = yMin, z = -3.6f };
            var br = new Vector3 { x = backX - 0.2f, y = yMax, z = -3.6f };

            AddQuadOutward(tris, tl, tr, br, bl, BodyCenter, color ?? ventGlowColor);
        }

        // ----------------------------------------------------
        //  TOWER + WEAK SPOT
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MotherShipTower()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var fl = new Vector3 { x = towerFrontX, y = -towerHalfWidthFront, z = towerBaseZ };
            var fr = new Vector3 { x = towerFrontX, y = towerHalfWidthFront, z = towerBaseZ };
            var bl = new Vector3 { x = towerBackX, y = -towerHalfWidthBack, z = towerBaseZ };
            var br = new Vector3 { x = towerBackX, y = towerHalfWidthBack, z = towerBaseZ };

            var tfl = new Vector3 { x = towerFrontX - 2f, y = -9.6f, z = towerTopZ };
            var tfr = new Vector3 { x = towerFrontX - 2f, y = 9.6f, z = towerTopZ };
            var tbl = new Vector3 { x = towerBackX + 1f, y = -7.6f, z = towerTopZ - 2.0f };
            var tbr = new Vector3 { x = towerBackX + 1f, y = 7.6f, z = towerTopZ - 2.0f };

            AddQuadOutward(tris, fl, fr, tfr, tfl, BodyCenter, bridgeWindowColor);
            AddQuadOutward(tris, fr, br, tbr, tfr, BodyCenter, towerColorDark);
            AddQuadOutward(tris, bl, tbl, tbr, br, BodyCenter, towerColorDark);
            AddQuadOutward(tris, fl, tfl, tbl, bl, BodyCenter, towerColorDark);

            var roofFront = new Vector3 { x = towerFrontX - 1f, y = 0f, z = towerTopZ + 3.6f };
            var roofBack = new Vector3 { x = towerBackX + 1.6f, y = 0f, z = towerTopZ + 2.2f };

            tris.Add(CreateTriangleOutward(tfl, roofFront, tbl, BodyCenter, towerColor));
            tris.Add(CreateTriangleOutward(roofFront, roofBack, tbl, BodyCenter, towerColorDark));
            tris.Add(CreateTriangleOutward(roofFront, tfr, tbr, BodyCenter, towerColor));
            tris.Add(CreateTriangleOutward(roofFront, tbr, roofBack, BodyCenter, towerColorDark));

            tris.Add(CreateTriangleOutward(tfl, tfr, roofFront, BodyCenter, towerColor));
            tris.Add(CreateTriangleOutward(tbl, roofBack, tbr, BodyCenter, towerColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MotherShipWeakSpot()
        {
            var tris = new List<ITriangleMeshWithColor>();

            const int latBands = 6;
            const int lonSlices = 8;
            float cx = weakSpotCenterX;
            float cz = weakSpotCenterZ;
            float r = weakSpotRadius;

            var topPole = new Vector3 { x = cx, y = 0f, z = cz + r };
            var bottomPole = new Vector3 { x = cx, y = 0f, z = cz - r };
            var center = new Vector3 { x = cx, y = 0f, z = cz };

            // Build latitude ring vertices (rings between the poles)
            var rings = new Vector3[latBands - 1][];
            for (int b = 0; b < latBands - 1; b++)
            {
                float phi = MathF.PI * (b + 1) / latBands;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);
                rings[b] = new Vector3[lonSlices];
                for (int s = 0; s < lonSlices; s++)
                {
                    float theta = 2f * MathF.PI * s / lonSlices;
                    rings[b][s] = new Vector3
                    {
                        x = cx + r * sinPhi * MathF.Cos(theta),
                        y = r * sinPhi * MathF.Sin(theta),
                        z = cz + r * cosPhi
                    };
                }
            }

            // Top cap: top pole to first ring
            for (int s = 0; s < lonSlices; s++)
            {
                int ns = (s + 1) % lonSlices;
                string color = (s % 2 == 0) ? weakSpotColor : weakSpotColorDark;
                tris.Add(CreateTriangleOutward(topPole, rings[0][ns], rings[0][s], center, color));
            }

            // Middle bands
            for (int b = 0; b < latBands - 2; b++)
            {
                for (int s = 0; s < lonSlices; s++)
                {
                    int ns = (s + 1) % lonSlices;
                    string color = ((b + s) % 2 == 0) ? weakSpotColor : weakSpotColorDark;
                    tris.Add(CreateTriangleOutward(rings[b][s], rings[b + 1][ns], rings[b + 1][s], center, color));
                    tris.Add(CreateTriangleOutward(rings[b][s], rings[b][ns], rings[b + 1][ns], center, color));
                }
            }

            // Bottom cap: last ring to bottom pole
            int lastRing = latBands - 2;
            for (int s = 0; s < lonSlices; s++)
            {
                int ns = (s + 1) % lonSlices;
                string color = (s % 2 == 0) ? weakSpotColorDark : weakSpotColor;
                tris.Add(CreateTriangleOutward(bottomPole, rings[lastRing][s], rings[lastRing][ns], center, color));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  COLLISION
        // ----------------------------------------------------

        public static List<List<IVector3>>? MotherShipCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var frontHullBounds = ScaleCrashBoxBounds(
                new Vector3 { x = midFrontX, y = -midFrontHalfWidth, z = bottomMidFront - 2f },
                new Vector3 { x = noseTipX, y = midFrontHalfWidth, z = topMidFront + 4f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(frontHullBounds.min, frontHullBounds.max));

            var rearHullBounds = ScaleCrashBoxBounds(
                new Vector3 { x = backX, y = -midBackHalfWidth, z = bottomMidBack - 2f },
                new Vector3 { x = midBackX + 8f, y = midBackHalfWidth, z = topMidBack + 4f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rearHullBounds.min, rearHullBounds.max));

            var leftWingBounds = ScaleCrashBoxBounds(
                new Vector3 { x = wingTipBackX, y = -wingTipY, z = wingBottomZ - 1f },
                new Vector3 { x = wingRootFrontX + 2f, y = -midFrontHalfWidth + 2f, z = wingTopZ + 1f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(leftWingBounds.min, leftWingBounds.max));

            var rightWingBounds = ScaleCrashBoxBounds(
                new Vector3 { x = wingTipBackX, y = midFrontHalfWidth - 2f, z = wingBottomZ - 1f },
                new Vector3 { x = wingRootFrontX + 2f, y = wingTipY, z = wingTopZ + 1f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rightWingBounds.min, rightWingBounds.max));

            var towerBounds = ScaleCrashBoxBounds(
                new Vector3 { x = towerBackX - 2f, y = -towerHalfWidthFront, z = towerBaseZ },
                new Vector3 { x = towerFrontX + 2f, y = towerHalfWidthFront, z = towerTopZ + 5f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(towerBounds.min, towerBounds.max));

            float weakSpotBoxRadius = weakSpotRadius * 1.5f;
            var weakSpotBounds = ScaleCrashBoxBounds(
                new Vector3 { x = weakSpotCenterX - weakSpotBoxRadius, y = -weakSpotBoxRadius, z = weakSpotCenterZ - weakSpotBoxRadius },
                new Vector3 { x = weakSpotCenterX + weakSpotBoxRadius, y = weakSpotBoxRadius, z = weakSpotCenterZ + weakSpotBoxRadius });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(weakSpotBounds.min, weakSpotBounds.max));

            return boxes;
        }

        public static List<string?> MotherShipCrashBoxNames()
        {
            return new List<string?>
            {
                "FrontHull",
                "RearHull",
                "LeftWing",
                "RightWing",
                "Tower",
                "WeakSpot"
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

            }
        }