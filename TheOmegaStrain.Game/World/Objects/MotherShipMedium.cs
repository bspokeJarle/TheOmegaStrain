using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;
using static TheOmegaStrain.Game.Helpers._3dObjectHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    public class MotherShipMedium
    {
        private const float ZoomRatio = 1.38f;
        private const float CrashboxSize = 1.20f;

        // ----------------------------------------------------
        //  MAIN BODY / SHAPE
        // ----------------------------------------------------

        private static float noseTipX = 132f;
        private static float jawFrontX = 110f;
        private static float neckFrontX = 74f;
        private static float bodyMidX = 18f;
        private static float rearBodyX = -34f;
        private static float tailX = -74f;

        private static float jawHalfWidth = 26f;
        private static float neckHalfWidth = 18f;
        private static float bodyHalfWidth = 26f;
        private static float rearBodyHalfWidth = 38f;
        private static float tailHalfWidth = 24f;

        private static float noseTop = 10f;
        private static float noseBottom = -8f;
        private static float neckTop = 13f;
        private static float neckBottom = -9f;
        private static float bodyTop = 18f;
        private static float bodyBottom = -12f;
        private static float rearBodyTop = 20f;
        private static float rearBodyBottom = -13f;
        private static float tailTop = 14f;
        private static float tailBottom = -10f;

        // ----------------------------------------------------
        //  SIDE PODS
        // ----------------------------------------------------

        private static float podFrontX = 36f;
        private static float podBackX = -18f;
        private static float podOuterY = 108f;
        private static float podInnerYFront = 48f;
        private static float podInnerYBack = 58f;
        private static float podHalfHeightTop = 12f;
        private static float podHalfHeightBottom = -10f;

        // ----------------------------------------------------
        //  CONNECTORS
        // ----------------------------------------------------

        private static float connectorFrontX = 24f;
        private static float connectorBackX = -8f;
        private static float connectorTop = 6f;
        private static float connectorBottom = -4f;

        // ----------------------------------------------------
        //  LASER CANNON (integrated in hammerhead)
        // ----------------------------------------------------

        private static float cannonBackX = 88f;
        private static float cannonMidX = 108f;
        private static float cannonFrontX = 138f;

        private static float cannonBackHalfWidth = 18f;
        private static float cannonMidHalfWidth = 14f;
        private static float cannonFrontHalfWidth = 10f;

        private static float cannonTopBack = 12.0f;
        private static float cannonTopMid = 10.4f;
        private static float cannonTopFront = 9.2f;

        private static float cannonBottomBack = -12.0f;
        private static float cannonBottomMid = -10.4f;
        private static float cannonBottomFront = -9.2f;

        private static float muzzleX = 148f;
        private static float muzzleRadius = 11.6f;
        private static int muzzleSegments = 8;

        // ----------------------------------------------------
        //  SPINE / WEAK SPOT
        // ----------------------------------------------------

        private static float spineFrontX = -6f;
        private static float spineBackX = -42f;

        private static float spineBaseTopFront = 24f;
        private static float spineBaseTopBack = 22f;

        private static float spineTopFront = 36f;
        private static float spineTopMid = 44f;
        private static float spineTopBack = 34f;

        private static float spineHalfWidthFront = 10f;
        private static float spineHalfWidthMid = 9f;
        private static float spineHalfWidthBack = 8f;

        private static float weakSpotFrontX = -14f;
        private static float weakSpotBackX = -28f;
        private static float weakSpotCenterZ = 46f;
        private static float weakSpotHalfWidth = 5.5f;
        private static float weakSpotHalfHeight = 11f;

        // ----------------------------------------------------
        //  REAR THRUSTERS
        // ----------------------------------------------------

        private static float thrusterRadius = 7f;
        private static int thrusterSegments = 6;
        private static float thrusterDepth = 6f;

        // ----------------------------------------------------
        //  BELLY ARMOR PLATES
        // ----------------------------------------------------

        private static float bellyPlateZ = -16.5f;

        // ----------------------------------------------------
        //  POD NACELLE VENTS
        // ----------------------------------------------------

        private static float ventSlotHeight = 2.2f;
        private static float ventPush = 2.0f;

        // ----------------------------------------------------
        //  HULL PANEL LINES
        // ----------------------------------------------------

        private static float panelLineThickness = 2.2f;

        // ----------------------------------------------------
        //  BRIDGE COCKPIT
        // ----------------------------------------------------

        private static float bridgeFrontX = 96f;
        private static float bridgeBackX = 78f;
        private static float bridgeHalfWidth = 6f;
        private static float bridgeBaseZ = 7f;
        private static float bridgeTopZ = 14f;

        // ----------------------------------------------------
        //  JAW SENSOR ARRAY
        // ----------------------------------------------------

        private static float sensorBaseX = 108f;
        private static float sensorTipOffset = 5f;
        private static float sensorHalfSpacing = 8f;

        // ----------------------------------------------------
        //  WING ROOT FAIRINGS
        // ----------------------------------------------------

        // (uses existing connector/body/pod dimensions)

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string hullColorLight = "BFC7D2";
        private static string hullColorMid = "86909D";
        private static string hullColorDark = "58616C";
        private static string hullColorVeryDark = "2C323A";
        private static string undersideColor = "1F242B";

        private static string tealPanel = "2C9EA1";
        private static string tealPanelDark = "1C6B6F";
        private static string orangePanel = "D98C2E";
        private static string orangePanelDark = "8B5717";

        private static string podColor = "7D8793";
        private static string podColorDark = "4C5662";
        private static string connectorColor = "5A6571";

        private static string cannonColor = "A9B5C3";
        private static string cannonColorDark = "5E6976";
        private static string cannonGlow = "FF5A20";
        private static string cannonGlowBright = "FFD166";

        private static string weakSpotColor = "FF672A";
        private static string weakSpotColorDark = "A62B10";
        private static string weakSpotFrameColor = "FFB347";

        private static string rearGlowColor = "FF7B22";
        private static string bridgeColor = "2A465E";
        private static string bridgeGlass = "4A7FAA";
        private static string thrusterInnerColor = "FF9933";
        private static string thrusterRingColor = "3A3F48";
        private static string bellyArmorColor = "4A3828";
        private static string bellyArmorAccent = "6B4F2E";
        private static string panelSeamColor = "1A1E24";
        private static string sensorColor = "C8D4E0";
        private static string sensorTipColor = "FF4466";
        private static string fairingColor = "4C5662";

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        public static _3dObject CreateMotherShipMedium(ISurface parentSurface)
        {
            var hammerTop = HammerHeadTop();
            var hammerBottom = HammerHeadBottom();
            var hammerSides = HammerHeadSides();

            var bodyTop = MainBodyTop();
            var bodyBottom = MainBodyBottom();
            var bodySides = MainBodySides();

            var leftPod = LeftPod();
            var rightPod = RightPod();
            var leftConnector = LeftConnector();
            var rightConnector = RightConnector();

            var frontCannon = FrontCannonBody();
            var frontMuzzle = FrontCannonMuzzle();

            var topPanels = TopPanels();
            var rearFace = RearFace();

            var spine = EnergySpine();
            var weakSpot = WeakSpot();

            var laserGuide = LaserDirectionGuide();
            var laserStartGuide = LaserStartGuide();

            var chargeRing1 = CannonChargeRing1();
            var chargeRing2 = CannonChargeRing2();
            var chargeRing3 = CannonChargeRing3();

            var rearThrusters = RearThrusters();
            var bellyArmor = BellyArmorPlates();
            var podVents = PodNacelleVents();
            var leftWingEngine = WingEngine(isRight: false);
            var rightWingEngine = WingEngine(isRight: true);
            var leftWingEngineStart  = WingEngineStart(isRight: false);
            var rightWingEngineStart = WingEngineStart(isRight: true);
            var leftWingEngineGuide  = WingEngineGuide(isRight: false);
            var rightWingEngineGuide = WingEngineGuide(isRight: true);
            var hullPanelLines = HullPanelLines();
            var bridge = BridgeCockpit();
            var jawSensors = JawSensorArray();
            var wingFairings = WingRootFairings();

            var crashBoxes = MotherShipCrashBoxes();

            var ship = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "MotherShipMedium"
            };

            AddPart(ship, "HammerHeadTop", hammerTop, true);
            AddPart(ship, "HammerHeadBottom", hammerBottom, true);
            AddPart(ship, "HammerHeadSides", hammerSides, true);

            AddPart(ship, "MainBodyTop", bodyTop, true);
            AddPart(ship, "MainBodyBottom", bodyBottom, true);
            AddPart(ship, "MainBodySides", bodySides, true);

            AddPart(ship, "LeftPod", leftPod, true);
            AddPart(ship, "RightPod", rightPod, true);
            AddPart(ship, "LeftConnector", leftConnector, true);
            AddPart(ship, "RightConnector", rightConnector, true);

            AddPart(ship, "FrontCannonBody", frontCannon, true);
            AddPart(ship, "FrontCannonMuzzle", frontMuzzle, true);

            AddPart(ship, "TopPanels", topPanels, true);
            AddPart(ship, "RearFace", rearFace, true);

            AddPart(ship, "EnergySpine", spine, true);
            AddPart(ship, "WeakSpot", weakSpot, true);

            AddPart(ship, "WeaponDirectionGuide", laserGuide, false);
            AddPart(ship, "WeaponStartGuide", laserStartGuide, false);

            AddPart(ship, "CannonChargeRing1", chargeRing1, false);
            AddPart(ship, "CannonChargeRing2", chargeRing2, false);
            AddPart(ship, "CannonChargeRing3", chargeRing3, false);

            AddPart(ship, "RearThrusters", rearThrusters, true);
            AddPart(ship, "BellyArmorPlates", bellyArmor, true);
            AddPart(ship, "PodNacelleVents", podVents, true);
            AddPart(ship, "LeftWingEngine", leftWingEngine, true);
            AddPart(ship, "RightWingEngine", rightWingEngine, true);
            AddPart(ship, "LeftWingEngineStart", leftWingEngineStart, false);
            AddPart(ship, "RightWingEngineStart", rightWingEngineStart, false);
            AddPart(ship, "LeftWingEngineGuide", leftWingEngineGuide, false);
            AddPart(ship, "RightWingEngineGuide", rightWingEngineGuide, false);
            AddPart(ship, "HullPanelLines", hullPanelLines, true);
            AddPart(ship, "BridgeCockpit", bridge, true);
            AddPart(ship, "JawSensorArray", jawSensors, true);
            AddPart(ship, "WingRootFairings", wingFairings, true);

            ship.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            ship.ParentSurface = parentSurface;
            ship.HasShadow = true;
            ship.Particles = new ParticlesAI();

            if (crashBoxes != null)
                ship.CrashBoxes = crashBoxes;

            ship.CrashBoxNames = MotherShipCrashBoxNames();

            _3dObjectHelpers.ApplyScaleToObject(ship, ZoomRatio);
            _3dObjectHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);

            return ship;
        }

        // ----------------------------------------------------
        //  HAMMERHEAD FRONT
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? HammerHeadTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var nose = new Vector3 { x = noseTipX, y = 0f, z = noseTop };

            var jawLT = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = 5f };
            var jawRT = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = 5f };

            var neckLT = new Vector3 { x = neckFrontX, y = -neckHalfWidth, z = neckTop };
            var neckRT = new Vector3 { x = neckFrontX, y = neckHalfWidth, z = neckTop };

            var centerJaw = new Vector3 { x = 104f, y = 0f, z = 14f };
            var centerNeck = new Vector3 { x = 82f, y = 0f, z = 16f };

            tris.Add(CreateTriangleOutward(jawLT, nose, jawRT, BodyCenter, orangePanel));
            tris.Add(CreateTriangleOutward(jawLT, centerJaw, nose, BodyCenter, hullColorLight));
            tris.Add(CreateTriangleOutward(nose, centerJaw, jawRT, BodyCenter, hullColorLight));

            tris.Add(CreateTriangleOutward(jawLT, neckLT, centerJaw, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerJaw, neckLT, centerNeck, BodyCenter, tealPanelDark));

            tris.Add(CreateTriangleOutward(centerJaw, neckRT, jawRT, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerJaw, centerNeck, neckRT, BodyCenter, tealPanelDark));

            tris.Add(CreateTriangleOutward(neckLT, new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyTop }, centerNeck, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyTop }, new Vector3 { x = 34f, y = 0f, z = 20f }, BodyCenter, hullColorDark));

            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyTop }, neckRT, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = 34f, y = 0f, z = 20f }, new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyTop }, BodyCenter, hullColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? HammerHeadBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var nose = new Vector3 { x = noseTipX - 8f, y = 0f, z = noseBottom };

            var jawLB = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = -2f };
            var jawRB = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = -2f };

            var neckLB = new Vector3 { x = neckFrontX, y = -neckHalfWidth, z = neckBottom };
            var neckRB = new Vector3 { x = neckFrontX, y = neckHalfWidth, z = neckBottom };

            var centerJaw = new Vector3 { x = 102f, y = 0f, z = -12f };
            var centerNeck = new Vector3 { x = 80f, y = 0f, z = -13f };

            tris.Add(CreateTriangleOutward(jawLB, jawRB, nose, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(jawLB, nose, centerJaw, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(nose, jawRB, centerJaw, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(jawLB, centerJaw, neckLB, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(centerJaw, centerNeck, neckLB, BodyCenter, hullColorVeryDark));

            tris.Add(CreateTriangleOutward(centerJaw, jawRB, neckRB, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(centerJaw, neckRB, centerNeck, BodyCenter, hullColorVeryDark));

            tris.Add(CreateTriangleOutward(neckLB, centerNeck, new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyBottom }, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = 34f, y = 0f, z = -18f }, new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyBottom }, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyBottom }, neckRB, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerNeck, new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyBottom }, new Vector3 { x = 34f, y = 0f, z = -18f }, BodyCenter, undersideColor));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? HammerHeadSides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Nose front faces (connecting top jaw edge to bottom jaw edge)
            {
                var noseTop = new Vector3 { x = noseTipX, y = 0f, z = MotherShipMedium.noseTop };
                var noseBottom = new Vector3 { x = noseTipX - 8f, y = 0f, z = MotherShipMedium.noseBottom };
                var jawTopL = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = 5f };
                var jawBottomL = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = -2f };
                var jawTopR = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = 5f };
                var jawBottomR = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = -2f };

                // Left nose side
                tris.Add(CreateTriangleOutward(noseTop, jawBottomL, jawTopL, BodyCenter, hullColorMid));
                tris.Add(CreateTriangleOutward(noseTop, noseBottom, jawBottomL, BodyCenter, hullColorMid));
                // Right nose side
                tris.Add(CreateTriangleOutward(noseTop, jawTopR, jawBottomR, BodyCenter, hullColorMid));
                tris.Add(CreateTriangleOutward(noseTop, jawBottomR, noseBottom, BodyCenter, hullColorMid));
            }

            // Left
            {
                var jawTopL = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = 5f };
                var jawBottomL = new Vector3 { x = jawFrontX, y = -jawHalfWidth, z = -2f };
                var neckTopL = new Vector3 { x = neckFrontX, y = -neckHalfWidth, z = MotherShipMedium.neckTop };
                var neckBottomL = new Vector3 { x = neckFrontX, y = -neckHalfWidth, z = MotherShipMedium.neckBottom };
                var bodyTopL = new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyTop };
                var bodyBottomL = new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyBottom };

                AddQuadOutward(tris, jawTopL, neckTopL, neckBottomL, jawBottomL, BodyCenter, hullColorDark);
                AddQuadOutward(tris, neckTopL, bodyTopL, bodyBottomL, neckBottomL, BodyCenter, hullColorDark);
            }

            // Right
            {
                var jawTopR = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = 5f };
                var jawBottomR = new Vector3 { x = jawFrontX, y = jawHalfWidth, z = -2f };
                var neckTopR = new Vector3 { x = neckFrontX, y = neckHalfWidth, z = MotherShipMedium.neckTop };
                var neckBottomR = new Vector3 { x = neckFrontX, y = neckHalfWidth, z = MotherShipMedium.neckBottom };
                var bodyTopR = new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyTop };
                var bodyBottomR = new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyBottom };

                AddQuadOutward(tris, jawTopR, jawBottomR, neckBottomR, neckTopR, BodyCenter, hullColorDark);
                AddQuadOutward(tris, neckTopR, neckBottomR, bodyBottomR, bodyTopR, BodyCenter, hullColorDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  MAIN BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MainBodyTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var frontLeft = new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyTop };
            var frontRight = new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyTop };

            var rearLeft = new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyTop };
            var rearRight = new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyTop };

            var tailLeft = new Vector3 { x = tailX, y = -tailHalfWidth, z = tailTop };
            var tailRight = new Vector3 { x = tailX, y = tailHalfWidth, z = tailTop };

            var centerFront = new Vector3 { x = 10f, y = 0f, z = 22f };
            var centerRear = new Vector3 { x = -20f, y = 0f, z = 24f };
            var centerTail = new Vector3 { x = -58f, y = 0f, z = 18f };

            tris.Add(CreateTriangleOutward(frontLeft, centerFront, rearLeft, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerFront, centerRear, rearLeft, BodyCenter, tealPanelDark));

            tris.Add(CreateTriangleOutward(centerFront, frontRight, rearRight, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(centerFront, rearRight, centerRear, BodyCenter, tealPanelDark));

            tris.Add(CreateTriangleOutward(rearLeft, centerRear, tailLeft, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerRear, centerTail, tailLeft, BodyCenter, hullColorDark));

            tris.Add(CreateTriangleOutward(centerRear, rearRight, tailRight, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(centerRear, tailRight, centerTail, BodyCenter, hullColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MainBodyBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var frontLeft = new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyBottom };
            var frontRight = new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyBottom };

            var rearLeft = new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyBottom };
            var rearRight = new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyBottom };

            var tailLeft = new Vector3 { x = tailX, y = -tailHalfWidth, z = tailBottom };
            var tailRight = new Vector3 { x = tailX, y = tailHalfWidth, z = tailBottom };

            var centerFront = new Vector3 { x = 12f, y = 0f, z = -18f };
            var centerRear = new Vector3 { x = -20f, y = 0f, z = -18f };
            var centerTail = new Vector3 { x = -58f, y = 0f, z = -14f };

            tris.Add(CreateTriangleOutward(frontLeft, rearLeft, centerFront, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, rearLeft, centerRear, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(centerFront, rearRight, frontRight, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, centerRear, rearRight, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(rearLeft, tailLeft, centerRear, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(centerRear, tailLeft, centerTail, BodyCenter, hullColorVeryDark));

            tris.Add(CreateTriangleOutward(centerRear, tailRight, rearRight, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(centerRear, centerTail, tailRight, BodyCenter, hullColorVeryDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? MainBodySides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left side
            AddQuadOutward(
                tris,
                new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyTop },
                new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyTop },
                new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyBottom },
                new Vector3 { x = bodyMidX, y = -bodyHalfWidth, z = bodyBottom },
                BodyCenter,
                hullColorDark);

            AddQuadOutward(
                tris,
                new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyTop },
                new Vector3 { x = tailX, y = -tailHalfWidth, z = tailTop },
                new Vector3 { x = tailX, y = -tailHalfWidth, z = tailBottom },
                new Vector3 { x = rearBodyX, y = -rearBodyHalfWidth, z = rearBodyBottom },
                BodyCenter,
                hullColorVeryDark);

            // Right side
            AddQuadOutward(
                tris,
                new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyTop },
                new Vector3 { x = bodyMidX, y = bodyHalfWidth, z = bodyBottom },
                new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyBottom },
                new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyTop },
                BodyCenter,
                hullColorDark);

            AddQuadOutward(
                tris,
                new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyTop },
                new Vector3 { x = rearBodyX, y = rearBodyHalfWidth, z = rearBodyBottom },
                new Vector3 { x = tailX, y = tailHalfWidth, z = tailBottom },
                new Vector3 { x = tailX, y = tailHalfWidth, z = tailTop },
                BodyCenter,
                hullColorVeryDark);

            return tris;
        }

        // ----------------------------------------------------
        //  SIDE PODS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? LeftPod()
        {
            return BuildPod(isRight: false);
        }

        public static List<ITriangleMeshWithColor>? RightPod()
        {
            return BuildPod(isRight: true);
        }

        private static List<ITriangleMeshWithColor> BuildPod(bool isRight)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = isRight ? 1f : -1f;

            var ftl = new Vector3 { x = podFrontX, y = s * podInnerYFront, z = podHalfHeightTop };
            var ftr = new Vector3 { x = podFrontX, y = s * podOuterY, z = podHalfHeightTop - 2f };
            var fbl = new Vector3 { x = podFrontX, y = s * podInnerYFront, z = podHalfHeightBottom };
            var fbr = new Vector3 { x = podFrontX, y = s * podOuterY, z = podHalfHeightBottom + 1f };

            var btl = new Vector3 { x = podBackX, y = s * podInnerYBack, z = podHalfHeightTop + 1f };
            var btr = new Vector3 { x = podBackX, y = s * podOuterY, z = podHalfHeightTop - 1f };
            var bbl = new Vector3 { x = podBackX, y = s * podInnerYBack, z = podHalfHeightBottom };
            var bbr = new Vector3 { x = podBackX, y = s * podOuterY, z = podHalfHeightBottom + 1f };

            AddQuadOutward(tris, ftl, ftr, btr, btl, BodyCenter, podColor);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, BodyCenter, podColorDark);

            if (isRight)
            {
                AddQuadOutward(tris, ftl, btl, bbl, fbl, BodyCenter, podColorDark);
                AddQuadOutward(tris, ftr, fbr, bbr, btr, BodyCenter, podColorDark);
            }
            else
            {
                AddQuadOutward(tris, ftl, fbl, bbl, btl, BodyCenter, podColorDark);
                AddQuadOutward(tris, ftr, btr, bbr, fbr, BodyCenter, podColorDark);
            }

            // Front face of pod
            AddQuadOutward(tris, ftl, ftr, fbr, fbl, BodyCenter, podColor);

            AddQuadOutward(tris, btl, btr, bbr, bbl, BodyCenter, rearGlowColor);

            // small top hatch/panel
            var p1 = new Vector3 { x = 12f, y = s * (podInnerYFront + 14f), z = 14f };
            var p2 = new Vector3 { x = 0f, y = s * (podInnerYFront + 18f), z = 15f };
            var p3 = new Vector3 { x = -6f, y = s * (podInnerYFront + 10f), z = 14f };
            if (isRight)
                tris.Add(CreateTriangleOutward(p1, p3, p2, BodyCenter, tealPanel));
            else
                tris.Add(CreateTriangleOutward(p1, p2, p3, BodyCenter, tealPanel));

            return tris;
        }

        // ----------------------------------------------------
        //  CONNECTORS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? LeftConnector()
        {
            return BuildConnector(isRight: false);
        }

        public static List<ITriangleMeshWithColor>? RightConnector()
        {
            return BuildConnector(isRight: true);
        }

        private static List<ITriangleMeshWithColor> BuildConnector(bool isRight)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = isRight ? 1f : -1f;

            var bodyFrontTop = new Vector3 { x = connectorFrontX, y = s * 28f, z = connectorTop };
            var bodyBackTop = new Vector3 { x = connectorBackX, y = s * 34f, z = connectorTop + 1f };
            var podFrontTop = new Vector3 { x = connectorFrontX, y = s * podInnerYFront, z = connectorTop - 1f };
            var podBackTop = new Vector3 { x = connectorBackX, y = s * podInnerYBack, z = connectorTop };

            var bodyFrontBot = new Vector3 { x = connectorFrontX, y = s * 28f, z = connectorBottom };
            var bodyBackBot = new Vector3 { x = connectorBackX, y = s * 34f, z = connectorBottom };
            var podFrontBot = new Vector3 { x = connectorFrontX, y = s * podInnerYFront, z = connectorBottom - 1f };
            var podBackBot = new Vector3 { x = connectorBackX, y = s * podInnerYBack, z = connectorBottom };

            AddQuadOutward(tris, bodyFrontTop, podFrontTop, podBackTop, bodyBackTop, BodyCenter, connectorColor);
            AddQuadOutward(tris, bodyFrontBot, bodyBackBot, podBackBot, podFrontBot, BodyCenter, hullColorVeryDark);

            if (isRight)
            {
                AddQuadOutward(tris, bodyFrontTop, bodyFrontBot, podFrontBot, podFrontTop, BodyCenter, connectorColor);
                AddQuadOutward(tris, bodyBackTop, podBackTop, podBackBot, bodyBackBot, BodyCenter, connectorColor);
            }
            else
            {
                AddQuadOutward(tris, bodyFrontTop, podFrontTop, podFrontBot, bodyFrontBot, BodyCenter, connectorColor);
                AddQuadOutward(tris, bodyBackTop, bodyBackBot, podBackBot, podBackTop, BodyCenter, connectorColor);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  FRONT CANNON
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? FrontCannonBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var backTop = new Vector3 { x = cannonBackX, y = 0f, z = cannonTopBack };
            var backRT = new Vector3 { x = cannonBackX, y = cannonBackHalfWidth, z = 4.4f };
            var backRB = new Vector3 { x = cannonBackX, y = cannonBackHalfWidth, z = cannonBottomBack };
            var backBottom = new Vector3 { x = cannonBackX, y = 0f, z = cannonBottomBack };
            var backLB = new Vector3 { x = cannonBackX, y = -cannonBackHalfWidth, z = cannonBottomBack };
            var backLT = new Vector3 { x = cannonBackX, y = -cannonBackHalfWidth, z = 4.4f };

            var midTop = new Vector3 { x = cannonMidX, y = 0f, z = cannonTopMid };
            var midRT = new Vector3 { x = cannonMidX, y = cannonMidHalfWidth, z = 3.4f };
            var midRB = new Vector3 { x = cannonMidX, y = cannonMidHalfWidth, z = cannonBottomMid };
            var midBottom = new Vector3 { x = cannonMidX, y = 0f, z = cannonBottomMid };
            var midLB = new Vector3 { x = cannonMidX, y = -cannonMidHalfWidth, z = cannonBottomMid };
            var midLT = new Vector3 { x = cannonMidX, y = -cannonMidHalfWidth, z = 3.4f };

            var frontTop = new Vector3 { x = cannonFrontX, y = 0f, z = cannonTopFront };
            var frontRT = new Vector3 { x = cannonFrontX - 6f, y = cannonFrontHalfWidth, z = 2.8f };
            var frontRB = new Vector3 { x = cannonFrontX - 6f, y = cannonFrontHalfWidth, z = cannonBottomFront };
            var frontBottom = new Vector3 { x = cannonFrontX, y = 0f, z = cannonBottomFront };
            var frontLB = new Vector3 { x = cannonFrontX - 6f, y = -cannonFrontHalfWidth, z = cannonBottomFront };
            var frontLT = new Vector3 { x = cannonFrontX - 6f, y = -cannonFrontHalfWidth, z = 2.8f };

            tris.Add(CreateTriangleOutward(backLT, backTop, midLT, BodyCenter, cannonColor));
            tris.Add(CreateTriangleOutward(backTop, midTop, midLT, BodyCenter, cannonColor));
            tris.Add(CreateTriangleOutward(backTop, backRT, midRT, BodyCenter, cannonColor));
            tris.Add(CreateTriangleOutward(backTop, midRT, midTop, BodyCenter, cannonColor));

            tris.Add(CreateTriangleOutward(midLT, midTop, frontLT, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(midTop, frontTop, frontLT, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(midTop, midRT, frontRT, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(midTop, frontRT, frontTop, BodyCenter, cannonColorDark));

            tris.Add(CreateTriangleOutward(backLB, midLB, backBottom, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(backBottom, midLB, midBottom, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(backBottom, backRB, midRB, BodyCenter, cannonColorDark));
            tris.Add(CreateTriangleOutward(backBottom, midRB, midBottom, BodyCenter, cannonColorDark));

            tris.Add(CreateTriangleOutward(midLB, frontLB, midBottom, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(midBottom, frontLB, frontBottom, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(midBottom, midRB, frontRB, BodyCenter, hullColorVeryDark));
            tris.Add(CreateTriangleOutward(midBottom, frontRB, frontBottom, BodyCenter, hullColorVeryDark));

            AddQuadOutward(tris, backLT, midLT, midLB, backLB, BodyCenter, cannonColorDark);
            AddQuadOutward(tris, midLT, frontLT, frontLB, midLB, BodyCenter, cannonColorDark);

            AddQuadOutward(tris, backRT, backRB, midRB, midRT, BodyCenter, cannonColorDark);
            AddQuadOutward(tris, midRT, midRB, frontRB, frontRT, BodyCenter, cannonColorDark);

            AddQuadOutward(tris, frontLT, frontRT, frontRB, frontLB, BodyCenter, cannonGlow);

            // upper cradle tying cannon into hammerhead
            var cradleL1 = new Vector3 { x = 82f, y = -10f, z = 8f };
            var cradleL2 = new Vector3 { x = cannonBackX + 4f, y = -8f, z = 4f };
            var cradleL3 = new Vector3 { x = cannonBackX + 4f, y = -8f, z = -2f };
            var cradleL4 = new Vector3 { x = 82f, y = -12f, z = -3f };

            var cradleR1 = new Vector3 { x = 82f, y = 10f, z = 8f };
            var cradleR2 = new Vector3 { x = cannonBackX + 4f, y = 8f, z = 4f };
            var cradleR3 = new Vector3 { x = cannonBackX + 4f, y = 8f, z = -2f };
            var cradleR4 = new Vector3 { x = 82f, y = 12f, z = -3f };

            AddQuadOutward(tris, cradleL1, cradleL2, cradleL3, cradleL4, BodyCenter, hullColorDark);
            AddQuadOutward(tris, cradleR1, cradleR4, cradleR3, cradleR2, BodyCenter, hullColorDark);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? FrontCannonMuzzle()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var tip = new Vector3 { x = muzzleX, y = 0f, z = 0f };
            var ring = GenerateEllipseRing(muzzleSegments, cannonFrontX + 2f, muzzleRadius, muzzleRadius);

            for (int i = 0; i < muzzleSegments; i++)
            {
                int next = (i + 1) % muzzleSegments;
                string color = (i % 2 == 0) ? cannonGlow : cannonGlowBright;
                tris.Add(CreateTriangleOutward(tip, ring[next], ring[i], new Vector3 { x = cannonFrontX + 4f, y = 0f, z = 0f }, color));
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? LaserDirectionGuide()
        {
            // vert1 is the direction tip — placed FAR BEYOND the muzzle tip (muzzleX=148) along the cannon's +X axis
            // so the aiming vector is clearly visible ahead of the ship. FireWeapon normalizes (direction - start)
            // to get the trajectory vector, so the absolute X distance only affects visualization, not aim accuracy.
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = 280f, y =  4f, z =  2f },
                    new Vector3 { x = 280f, y = -4f, z =  2f },
                    new Vector3 { x = 290f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? LaserStartGuide()
        {
            // vert1 is the spawn point — placed BARELY OUTSIDE the muzzle tip (muzzleX=148) so the start guide
            // sits just in front of the cannon. Both guides are now clearly separated along +X so you can see them distinctly.
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = 156f, y =  4f, z =  2f },
                    new Vector3 { x = 156f, y = -4f, z =  2f },
                    new Vector3 { x = 164f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        // ----------------------------------------------------
        //  CANNON CHARGE RINGS
        // ----------------------------------------------------

        // Each ring is a flat hexagonal halo in the Y-Z plane, hidden by default.
        // The control class pulses them back-to-front to animate a charge travelling
        // up the barrel. Outer/inner radii are sized to wrap just outside the barrel.

        private static List<ITriangleMeshWithColor> BuildHexChargeRing(
            float xPos, float outerY, float outerZ, float innerY, float innerZ, string color)
        {
            var tris = new List<ITriangleMeshWithColor>();
            const int segments = 6;
            var outer = GenerateEllipseRing(segments, xPos, outerY, outerZ);
            var inner = GenerateEllipseRing(segments, xPos, innerY, innerZ);

            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                AddQuadOutward(tris, outer[i], outer[j], inner[j], inner[i], BodyCenter, color, noHidden: true);
            }
            return tris;
        }

        public static List<ITriangleMeshWithColor>? CannonChargeRing1()
            => BuildHexChargeRing(cannonBackX,  outerY: 30f, outerZ: 24f, innerY: 21f, innerZ: 15f, "44CCFF");

        public static List<ITriangleMeshWithColor>? CannonChargeRing2()
            => BuildHexChargeRing(cannonMidX,   outerY: 26f, outerZ: 20f, innerY: 17f, innerZ: 13f, "66DDFF");

        public static List<ITriangleMeshWithColor>? CannonChargeRing3()
            => BuildHexChargeRing(cannonFrontX, outerY: 22f, outerZ: 17f, innerY: 13f, innerZ: 11f, "88EEFF");

        // ----------------------------------------------------
        //  PANELS / REAR / SPINE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? TopPanels()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // long teal strip
            var a = new Vector3 { x = 64f, y = -9f, z = 19.0f };
            var b = new Vector3 { x = 10f, y = -12f, z = 23.0f };
            var c = new Vector3 { x = -8f, y = 0f, z = 25.0f };
            var d = new Vector3 { x = 10f, y = 12f, z = 23.0f };
            var e = new Vector3 { x = 64f, y = 9f, z = 19.0f };

            tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, tealPanel));
            tris.Add(CreateTriangleOutward(a, c, e, BodyCenter, tealPanel));
            tris.Add(CreateTriangleOutward(c, d, e, BodyCenter, tealPanelDark));

            // two small jaw panels
            var l1 = new Vector3 { x = 92f, y = -14f, z = 11.4f };
            var l2 = new Vector3 { x = 84f, y = -10f, z = 10.8f };
            var l3 = new Vector3 { x = 88f, y = -4f, z = 11.0f };
            tris.Add(CreateTriangleOutward(l1, l2, l3, BodyCenter, tealPanel));

            var r1 = new Vector3 { x = 92f, y = 14f, z = 11.4f };
            var r2 = new Vector3 { x = 84f, y = 10f, z = 10.8f };
            var r3 = new Vector3 { x = 88f, y = 4f, z = 11.0f };
            tris.Add(CreateTriangleOutward(r1, r3, r2, BodyCenter, tealPanel));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? RearFace()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var tl = new Vector3 { x = tailX, y = -tailHalfWidth, z = tailTop };
            var tr = new Vector3 { x = tailX, y = tailHalfWidth, z = tailTop };
            var bl = new Vector3 { x = tailX, y = -tailHalfWidth, z = tailBottom };
            var br = new Vector3 { x = tailX, y = tailHalfWidth, z = tailBottom };

            AddQuadOutward(tris, tl, tr, br, bl, BodyCenter, hullColorVeryDark);

            AddRearGlow(tris, -18f, -6f, tealPanel);
            AddRearGlow(tris, -4f, 4f, rearGlowColor);
            AddRearGlow(tris, 6f, 18f, orangePanel);

            return tris;
        }

        private static void AddRearGlow(List<ITriangleMeshWithColor> tris, float yMin, float yMax, string color)
        {
            var tl = new Vector3 { x = tailX - 0.2f, y = yMin, z = 3.6f };
            var tr = new Vector3 { x = tailX - 0.2f, y = yMax, z = 3.6f };
            var bl = new Vector3 { x = tailX - 0.2f, y = yMin, z = -3.6f };
            var br = new Vector3 { x = tailX - 0.2f, y = yMax, z = -3.6f };

            AddQuadOutward(tris, tl, tr, br, bl, BodyCenter, color);
        }

        public static List<ITriangleMeshWithColor>? EnergySpine()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var lf = new Vector3 { x = spineFrontX, y = -spineHalfWidthFront, z = spineBaseTopFront };
            var rf = new Vector3 { x = spineFrontX, y = spineHalfWidthFront, z = spineBaseTopFront };
            var lm = new Vector3 { x = -22f, y = -spineHalfWidthMid, z = rearBodyTop + 2f };
            var rm = new Vector3 { x = -22f, y = spineHalfWidthMid, z = rearBodyTop + 2f };
            var lb = new Vector3 { x = spineBackX, y = -spineHalfWidthBack, z = spineBaseTopBack };
            var rb = new Vector3 { x = spineBackX, y = spineHalfWidthBack, z = spineBaseTopBack };

            var tf = new Vector3 { x = spineFrontX - 2f, y = 0f, z = spineTopFront };
            var tm = new Vector3 { x = -20f, y = 0f, z = spineTopMid };
            var tb = new Vector3 { x = spineBackX + 2f, y = 0f, z = spineTopBack };

            tris.Add(CreateTriangleOutward(lf, tf, lm, BodyCenter, bridgeColor));
            tris.Add(CreateTriangleOutward(tf, tm, lm, BodyCenter, hullColorMid));
            tris.Add(CreateTriangleOutward(tf, rf, rm, BodyCenter, bridgeColor));
            tris.Add(CreateTriangleOutward(tf, rm, tm, BodyCenter, hullColorMid));

            tris.Add(CreateTriangleOutward(lm, tm, lb, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(tm, tb, lb, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(tm, rm, rb, BodyCenter, hullColorDark));
            tris.Add(CreateTriangleOutward(tm, rb, tb, BodyCenter, hullColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? WeakSpot()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = new Vector3 { x = weakSpotFrontX, y = 0f, z = weakSpotCenterZ };
            var back = new Vector3 { x = weakSpotBackX, y = 0f, z = weakSpotCenterZ };

            var leftFront = new Vector3 { x = weakSpotFrontX - 2f, y = -weakSpotHalfWidth, z = weakSpotCenterZ };
            var rightFront = new Vector3 { x = weakSpotFrontX - 2f, y = weakSpotHalfWidth, z = weakSpotCenterZ };
            var leftBack = new Vector3 { x = weakSpotBackX + 2f, y = -weakSpotHalfWidth + 1f, z = weakSpotCenterZ };
            var rightBack = new Vector3 { x = weakSpotBackX + 2f, y = weakSpotHalfWidth - 1f, z = weakSpotCenterZ };

            var topFront = new Vector3 { x = weakSpotFrontX - 1f, y = 0f, z = weakSpotCenterZ + weakSpotHalfHeight };
            var topBack = new Vector3 { x = weakSpotBackX + 1f, y = 0f, z = weakSpotCenterZ + weakSpotHalfHeight - 2f };

            var bottomFront = new Vector3 { x = weakSpotFrontX - 1f, y = 0f, z = weakSpotCenterZ - weakSpotHalfHeight };
            var bottomBack = new Vector3 { x = weakSpotBackX + 1f, y = 0f, z = weakSpotCenterZ - weakSpotHalfHeight + 2f };

            // frame-ish outer shell
            tris.Add(CreateTriangleOutward(topFront, rightFront, front, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(topFront, front, leftFront, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(topBack, back, rightBack, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(topBack, leftBack, back, BodyCenter, weakSpotFrameColor));

            tris.Add(CreateTriangleOutward(bottomFront, front, rightFront, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(bottomFront, leftFront, front, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(bottomBack, rightBack, back, BodyCenter, weakSpotFrameColor));
            tris.Add(CreateTriangleOutward(bottomBack, back, leftBack, BodyCenter, weakSpotFrameColor));

            // hot inner core
            tris.Add(CreateTriangleOutward(topFront, rightFront, topBack, BodyCenter, weakSpotColor));
            tris.Add(CreateTriangleOutward(topFront, topBack, leftFront, BodyCenter, weakSpotColor));
            tris.Add(CreateTriangleOutward(bottomFront, bottomBack, rightFront, BodyCenter, weakSpotColorDark));
            tris.Add(CreateTriangleOutward(bottomFront, leftFront, bottomBack, BodyCenter, weakSpotColorDark));

            tris.Add(CreateTriangleOutward(leftFront, topBack, leftBack, BodyCenter, weakSpotColor));
            tris.Add(CreateTriangleOutward(leftFront, leftBack, bottomBack, BodyCenter, weakSpotColorDark));
            tris.Add(CreateTriangleOutward(rightFront, rightBack, topBack, BodyCenter, weakSpotColor));
            tris.Add(CreateTriangleOutward(rightFront, bottomBack, rightBack, BodyCenter, weakSpotColorDark));

            return tris;
        }

        // ----------------------------------------------------
        //  REAR THRUSTERS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? RearThrusters()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Three thruster nozzles arranged vertically at tailX
            float[] offsets = { 10f, 0f, -10f };
            foreach (float yOff in offsets)
            {
                var nozzleCenter = new Vector3 { x = tailX - thrusterDepth * 0.5f, y = yOff, z = 0f };
                var ring = GenerateEllipseRing(thrusterSegments, tailX, thrusterRadius, thrusterRadius);
                var innerRing = GenerateEllipseRing(thrusterSegments, tailX - thrusterDepth, thrusterRadius * 0.55f, thrusterRadius * 0.55f);
                var glowCenter = new Vector3 { x = tailX - thrusterDepth, y = yOff, z = 0f };

                // Offset entire ring by yOff
                for (int i = 0; i < ring.Count; i++)
                    ring[i] = new Vector3 { x = ring[i].x, y = ring[i].y + yOff, z = ring[i].z };
                for (int i = 0; i < innerRing.Count; i++)
                    innerRing[i] = new Vector3 { x = innerRing[i].x, y = innerRing[i].y + yOff, z = innerRing[i].z };

                // Outer band (dark ring rim)
                for (int i = 0; i < thrusterSegments; i++)
                {
                    int next = (i + 1) % thrusterSegments;
                    AddQuadOutward(tris, ring[i], ring[next], innerRing[next], innerRing[i], nozzleCenter, thrusterRingColor);
                }

                // Inner glow disc (fan)
                for (int i = 0; i < thrusterSegments; i++)
                {
                    int next = (i + 1) % thrusterSegments;
                    string col = (i % 2 == 0) ? thrusterInnerColor : cannonGlowBright;
                    tris.Add(CreateTriangleOutward(glowCenter, innerRing[i], innerRing[next], new Vector3 { x = tailX - thrusterDepth - 2f, y = yOff, z = 0f }, col));
                }
            }

            return tris;
        }

        // ----------------------------------------------------
        //  BELLY ARMOR PLATES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BellyArmorPlates()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Front belly plate (under neck/jaw transition)
            AddQuadOutward(tris,
                new Vector3 { x = 60f,  y = -14f, z = bellyPlateZ },
                new Vector3 { x = 60f,  y =  14f, z = bellyPlateZ },
                new Vector3 { x = 30f,  y =  18f, z = bellyPlateZ },
                new Vector3 { x = 30f,  y = -18f, z = bellyPlateZ },
                BodyCenter, bellyArmorColor);

            // Central belly plate (mid-body)
            AddQuadOutward(tris,
                new Vector3 { x = 20f,  y = -20f, z = bellyPlateZ },
                new Vector3 { x = 20f,  y =  20f, z = bellyPlateZ },
                new Vector3 { x = -10f, y =  26f, z = bellyPlateZ },
                new Vector3 { x = -10f, y = -26f, z = bellyPlateZ },
                BodyCenter, bellyArmorAccent);

            // Rear belly plate (under tail)
            AddQuadOutward(tris,
                new Vector3 { x = -14f, y = -22f, z = bellyPlateZ },
                new Vector3 { x = -14f, y =  22f, z = bellyPlateZ },
                new Vector3 { x = -42f, y =  16f, z = bellyPlateZ },
                new Vector3 { x = -42f, y = -16f, z = bellyPlateZ },
                BodyCenter, bellyArmorColor);

            return tris;
        }

        // ----------------------------------------------------
        //  WING ENGINE NOZZLES (descending thrusters on pod undersides)
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? WingEngine(bool isRight)
        {
            var tris = new List<ITriangleMeshWithColor>();
            float s = isRight ? 1f : -1f;

            // Nozzle faces downward (-Z). Ring lies in the XY plane at nozzleZ.
            float nozzleX = (podFrontX + podBackX) * 0.5f;   // 9
            float nozzleY = s * (podOuterY - 18f);            // s*90
            float nozzleZ = podHalfHeightBottom - 1f;         // -11

            int segs = 6;
            float outerR = 6f;
            float innerR = 3.5f;
            float depth = 5f;
            float step = (float)(2 * Math.PI / segs);

            var ring      = new List<Vector3>(segs);
            var innerRing = new List<Vector3>(segs);
            var glowRing  = new List<Vector3>(segs);

            for (int i = 0; i < segs; i++)
            {
                float angle = i * step;
                float cosA  = MathF.Cos(angle);
                float sinA  = MathF.Sin(angle);
                ring.Add(     new Vector3 { x = nozzleX + outerR * cosA, y = nozzleY + outerR * sinA, z = nozzleZ });
                innerRing.Add(new Vector3 { x = nozzleX + innerR * cosA, y = nozzleY + innerR * sinA, z = nozzleZ });
                glowRing.Add( new Vector3 { x = nozzleX + innerR * cosA, y = nozzleY + innerR * sinA, z = nozzleZ - depth });
            }

            var nozzleCenter = new Vector3 { x = nozzleX, y = nozzleY, z = nozzleZ };
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                AddQuadOutward(tris, ring[i], ring[next], innerRing[next], innerRing[i], nozzleCenter, thrusterRingColor);
            }

            var discCenter = new Vector3 { x = nozzleX, y = nozzleY, z = nozzleZ - depth };
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                string col = (i % 2 == 0) ? thrusterInnerColor : cannonGlowBright;
                tris.Add(CreateTriangleOutward(discCenter, glowRing[i], glowRing[next],
                    new Vector3 { x = nozzleX, y = nozzleY, z = nozzleZ - depth - 2f }, col));
            }
            return tris;
        }

        // Particle guides for the wing engine:
        // start at the nozzle center, guide straight out along nozzle axis (-Z).
        private const float WingEngineGuideOffsetZ = 100f;

        public static List<ITriangleMeshWithColor>? WingEngineStart(bool isRight)
        {
            float s = isRight ? 1f : -1f;
            float cx = (podFrontX + podBackX) * 0.5f;  // 9  — nozzle X
            float cy = s * (podOuterY - 18f);           // s*90 — nozzle Y
            float cz = podHalfHeightBottom - 1f;        // -11 — nozzle Z

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
            float cx = (podFrontX + podBackX) * 0.5f;
            float cy = s * (podOuterY - 18f);
            float cz = (podHalfHeightBottom - 1f) - WingEngineGuideOffsetZ;

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
        //  POD NACELLE VENTS (vents on side pod outer faces)
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? PodNacelleVents()
        {
            var tris = new List<ITriangleMeshWithColor>();
            BuildPodVents(tris, isRight: false);
            BuildPodVents(tris, isRight: true);
            return tris;
        }

        private static void BuildPodVents(List<ITriangleMeshWithColor> tris, bool isRight)
        {
            float s = isRight ? 1f : -1f;
            float outerY = s * (podOuterY + ventPush);

            // Three horizontal vent slats spaced vertically on the outer face
            float[] ventZCenters = { 4f, 0f, -4f };
            float[] xPositions   = { podFrontX - 4f, podFrontX - 12f, podBackX + 4f };

            foreach (float xPos in xPositions)
            {
                float xEnd = xPos - 8f;
                foreach (float zc in ventZCenters)
                {
                    float z0 = zc - ventSlotHeight * 0.5f;
                    float z1 = zc + ventSlotHeight * 0.5f;

                    AddQuadOutward(tris,
                        new Vector3 { x = xPos, y = outerY, z = z0 },
                        new Vector3 { x = xPos, y = outerY, z = z1 },
                        new Vector3 { x = xEnd, y = outerY, z = z1 },
                        new Vector3 { x = xEnd, y = outerY, z = z0 },
                        BodyCenter, hullColorVeryDark);
                }
            }
        }

        // ----------------------------------------------------
        //  HULL PANEL LINES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? HullPanelLines()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float pz = bodyTop + panelLineThickness;

            // Left seam line along body top
            AddQuadOutward(tris,
                new Vector3 { x = bodyMidX,  y = -10f, z = pz },
                new Vector3 { x = rearBodyX, y = -14f, z = pz },
                new Vector3 { x = rearBodyX, y = -14f - panelLineThickness, z = pz },
                new Vector3 { x = bodyMidX,  y = -10f - panelLineThickness, z = pz },
                BodyCenter, panelSeamColor);

            // Right seam line along body top
            AddQuadOutward(tris,
                new Vector3 { x = rearBodyX, y =  14f, z = pz },
                new Vector3 { x = bodyMidX,  y =  10f, z = pz },
                new Vector3 { x = bodyMidX,  y =  10f + panelLineThickness, z = pz },
                new Vector3 { x = rearBodyX, y =  14f + panelLineThickness, z = pz },
                BodyCenter, panelSeamColor);

            // Transverse seam across body mid
            AddQuadOutward(tris,
                new Vector3 { x = -18f, y = -rearBodyHalfWidth + 2f, z = pz },
                new Vector3 { x = -18f, y =  rearBodyHalfWidth - 2f, z = pz },
                new Vector3 { x = -18f - panelLineThickness, y =  rearBodyHalfWidth - 2f, z = pz },
                new Vector3 { x = -18f - panelLineThickness, y = -rearBodyHalfWidth + 2f, z = pz },
                BodyCenter, panelSeamColor);

            // Forward seam near neck
            AddQuadOutward(tris,
                new Vector3 { x = 50f, y = -neckHalfWidth + 2f, z = pz - 2f },
                new Vector3 { x = 50f, y =  neckHalfWidth - 2f, z = pz - 2f },
                new Vector3 { x = 50f - panelLineThickness, y =  neckHalfWidth - 2f, z = pz - 2f },
                new Vector3 { x = 50f - panelLineThickness, y = -neckHalfWidth + 2f, z = pz - 2f },
                BodyCenter, panelSeamColor);

            return tris;
        }

        // ----------------------------------------------------
        //  BRIDGE COCKPIT
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? BridgeCockpit()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var bCenter = new Vector3 { x = (bridgeFrontX + bridgeBackX) * 0.5f, y = 0f, z = (bridgeBaseZ + bridgeTopZ) * 0.5f };

            // Top face
            AddQuadOutward(tris,
                new Vector3 { x = bridgeFrontX, y = -bridgeHalfWidth, z = bridgeTopZ },
                new Vector3 { x = bridgeFrontX, y =  bridgeHalfWidth, z = bridgeTopZ },
                new Vector3 { x = bridgeBackX,  y =  bridgeHalfWidth, z = bridgeTopZ - 2f },
                new Vector3 { x = bridgeBackX,  y = -bridgeHalfWidth, z = bridgeTopZ - 2f },
                bCenter, bridgeColor);

            // Front face (glass)
            AddQuadOutward(tris,
                new Vector3 { x = bridgeFrontX, y = -bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeFrontX, y =  bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeFrontX, y =  bridgeHalfWidth, z = bridgeTopZ },
                new Vector3 { x = bridgeFrontX, y = -bridgeHalfWidth, z = bridgeTopZ },
                bCenter, bridgeGlass);

            // Rear face
            AddQuadOutward(tris,
                new Vector3 { x = bridgeBackX, y =  bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeBackX, y = -bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeBackX, y = -bridgeHalfWidth, z = bridgeTopZ - 2f },
                new Vector3 { x = bridgeBackX, y =  bridgeHalfWidth, z = bridgeTopZ - 2f },
                bCenter, bridgeColor);

            // Left side
            AddQuadOutward(tris,
                new Vector3 { x = bridgeBackX,  y = -bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeFrontX, y = -bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeFrontX, y = -bridgeHalfWidth, z = bridgeTopZ },
                new Vector3 { x = bridgeBackX,  y = -bridgeHalfWidth, z = bridgeTopZ - 2f },
                bCenter, hullColorDark);

            // Right side
            AddQuadOutward(tris,
                new Vector3 { x = bridgeFrontX, y = bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeBackX,  y = bridgeHalfWidth, z = bridgeBaseZ },
                new Vector3 { x = bridgeBackX,  y = bridgeHalfWidth, z = bridgeTopZ - 2f },
                new Vector3 { x = bridgeFrontX, y = bridgeHalfWidth, z = bridgeTopZ },
                bCenter, hullColorDark);

            return tris;
        }

        // ----------------------------------------------------
        //  JAW SENSOR ARRAY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? JawSensorArray()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Four sensor spikes along the front jaw edge (two each side)
            float[] yPositions = { -sensorHalfSpacing, -sensorHalfSpacing * 0.35f,
                                    sensorHalfSpacing * 0.35f,  sensorHalfSpacing };

            foreach (float y in yPositions)
            {
                var baseL = new Vector3 { x = sensorBaseX,                y = y - 2.5f, z = 4f };
                var baseR = new Vector3 { x = sensorBaseX,                y = y + 2.5f, z = 4f };
                var baseB = new Vector3 { x = sensorBaseX,                y = y,        z = 0f };
                var tip   = new Vector3 { x = sensorBaseX + sensorTipOffset, y = y,     z = 5.5f };

                tris.Add(CreateTriangleOutward(baseL, tip,   baseR,  BodyCenter, sensorColor));
                tris.Add(CreateTriangleOutward(baseL, baseB, tip,    BodyCenter, sensorColor));
                tris.Add(CreateTriangleOutward(baseR, tip,   baseB,  BodyCenter, sensorTipColor));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  WING ROOT FAIRINGS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? WingRootFairings()
        {
            var tris = new List<ITriangleMeshWithColor>();
            BuildWingFairing(tris, isRight: false);
            BuildWingFairing(tris, isRight: true);
            return tris;
        }

        private static void BuildWingFairing(List<ITriangleMeshWithColor> tris, bool isRight)
        {
            float s = isRight ? 1f : -1f;

            // Smooth fillet between connector inner edge and body side
            // Front fairing triangle
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = connectorFrontX + 4f, y = s * bodyHalfWidth,       z = bodyTop - 2f },
                new Vector3 { x = connectorFrontX,      y = s * (bodyHalfWidth + 8f), z = connectorTop },
                new Vector3 { x = connectorFrontX + 4f, y = s * bodyHalfWidth,       z = connectorTop  },
                BodyCenter, fairingColor));

            // Back fairing triangle
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = connectorBackX,       y = s * rearBodyHalfWidth,        z = rearBodyTop - 2f },
                new Vector3 { x = connectorBackX,       y = s * (rearBodyHalfWidth + 8f), z = connectorTop     },
                new Vector3 { x = connectorBackX - 4f,  y = s * rearBodyHalfWidth,        z = connectorTop     },
                BodyCenter, fairingColor));

            // Top fill quad bridging front and back fairings
            AddQuadOutward(tris,
                new Vector3 { x = connectorFrontX + 4f, y = s * bodyHalfWidth,       z = bodyTop - 2f    },
                new Vector3 { x = connectorBackX,       y = s * rearBodyHalfWidth,   z = rearBodyTop - 2f },
                new Vector3 { x = connectorBackX,       y = s * (rearBodyHalfWidth + 8f), z = connectorTop },
                new Vector3 { x = connectorFrontX,      y = s * (bodyHalfWidth + 8f), z = connectorTop    },
                BodyCenter, fairingColor);
        }

        // ----------------------------------------------------
        //  COLLISION
        // ----------------------------------------------------

        public static List<List<IVector3>>? MotherShipCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var hammerBounds = ScaleCrashBoxBounds(
                new Vector3 { x = neckFrontX, y = -jawHalfWidth, z = noseBottom - 2f },
                new Vector3 { x = noseTipX, y = jawHalfWidth, z = noseTop + 4f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(hammerBounds.min, hammerBounds.max));

            var bodyBounds = ScaleCrashBoxBounds(
                new Vector3 { x = tailX, y = -rearBodyHalfWidth, z = bodyBottom - 2f },
                new Vector3 { x = bodyMidX + 6f, y = rearBodyHalfWidth, z = rearBodyTop + 5f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bodyBounds.min, bodyBounds.max));

            var leftPodBounds = ScaleCrashBoxBounds(
                new Vector3 { x = podBackX, y = -podOuterY, z = podHalfHeightBottom - 2f },
                new Vector3 { x = podFrontX, y = -podInnerYFront, z = podHalfHeightTop + 3f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(leftPodBounds.min, leftPodBounds.max));

            var rightPodBounds = ScaleCrashBoxBounds(
                new Vector3 { x = podBackX, y = podInnerYFront, z = podHalfHeightBottom - 2f },
                new Vector3 { x = podFrontX, y = podOuterY, z = podHalfHeightTop + 3f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rightPodBounds.min, rightPodBounds.max));

            var spineBounds = ScaleCrashBoxBounds(
                new Vector3 { x = spineBackX - 2f, y = -spineHalfWidthFront - 2f, z = spineBaseTopBack },
                new Vector3 { x = spineFrontX + 2f, y = spineHalfWidthFront + 2f, z = spineTopMid + 4f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(spineBounds.min, spineBounds.max));

            var weakSpotBounds = ScaleCrashBoxBounds(
                new Vector3 { x = weakSpotBackX - 3f, y = -weakSpotHalfWidth - 2f, z = weakSpotCenterZ - weakSpotHalfHeight - 2f },
                new Vector3 { x = weakSpotFrontX + 3f, y = weakSpotHalfWidth + 2f, z = weakSpotCenterZ + weakSpotHalfHeight + 2f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(weakSpotBounds.min, weakSpotBounds.max));

            var cannonBounds = ScaleCrashBoxBounds(
                new Vector3 { x = cannonBackX, y = -cannonBackHalfWidth - 2f, z = cannonBottomBack - 2f },
                new Vector3 { x = muzzleX + 2f, y = cannonBackHalfWidth + 2f, z = cannonTopBack + 2f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(cannonBounds.min, cannonBounds.max));

            return boxes;
        }

        public static List<string?> MotherShipCrashBoxNames()
        {
            return new List<string?>
            {
                "HammerHead",
                "MainBody",
                "LeftPod",
                "RightPod",
                "Spine",
                "WeakSpot",
                "FrontCannon"
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