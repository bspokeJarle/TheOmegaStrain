using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using GameAiAndControls.Controls.SeederControls;
using static Domain._3dSpecificsImplementations;
using static _3dRotations.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public class Seeder
    {
        private const float ZoomRatio = 1.2f;

        // --- Geometry parameters for the flying saucer ---
        private static float seederRadius = 40f;
        private static float seederThickness = 6.0f;  // Half thickness of the middle belt (z = ±seederThickness)
        private static float topDomeHeight = 18f;   // Upper dome apex above center (z)
        private static float bottomDomeHeight = 12f;   // Lower dome bottom below center (z)
        private static float centerModuleRadius = 10f;   // Radius of the center module under the saucer
        private static float centerModuleHeight = 7f;    // Height of the center module
        private static float particleGuideDistance = 100f;

        private static int mainSegments = 12;    // Segments around the main circle
        private static int centerSegments = 8;     // Segments for the center module
        private static int panelSegments = 6;     // Number of panels on the underside

        // Alien ball (sphere) parameters – bigger and smoother
        private static float alienRadius = 16f;   // Radius of the alien ball on top
        private static int alienLatSegments = 4;     // Latitude bands (low-poly)
        private static int alienLonSegments = 10;    // Longitude segments (around)

        // --- Color palette ---
        private static string bodyColorLight = "7A8A9A";
        private static string bodyColorDark = "3A4F63";
        private static string rimColorLight = "C0C0C0";
        private static string rimColorDark = "888888";
        private static string moduleColorDark = "333333";
        private static string panelColor = "88CCFF";

        // Alien sphere color tones (shaded reds)
        private static string alienRedBright = "FF3344";
        private static string alienRedMedium = "CC0000";
        private static string alienRedDark = "880011";

        private static string windowColor = "A0C4FF";  // Light blue for side windows
        private static string doorColor = "555555";  // Dark gray for door

        // Center of the main saucer body (used for outward normals)
        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        public static _3dObject CreateSeeder(ISurface parentSurface)
        {
            // Build each visible part of the saucer
            var topHull = SeederTopHull();
            var bottomHull = SeederBottomHull();
            var rimRing = SeederRimRing();
            var centerModule = SeederCenterModule();
            var undersidePanels = SeederPanels();
            var alienBall = SeederAlienBall();
            var sideWindows = SeederSideWindows();
            var sideDoor = SeederDoor();

            var seederCrashBox = SeederCrashBoxes();
            var seederGuide = ParticlesDirectionGuide();
            var seederStartGuide = ParticlesStartGuide();

            var seeder = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++ // Set to a valid unique ID as appropriate for your application
            };

            // Visible parts
            if (topHull != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederTopHull",
                    Triangles = topHull,
                    IsVisible = true
                });

            if (bottomHull != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederBottomHull",
                    Triangles = bottomHull,
                    IsVisible = true
                });

            if (rimRing != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederRimRing",
                    Triangles = rimRing,
                    IsVisible = true
                });

            if (centerModule != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederCenterModule",
                    Triangles = centerModule,
                    IsVisible = true
                });

            if (undersidePanels != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederPanels",
                    Triangles = undersidePanels,
                    IsVisible = true
                });

            if (alienBall != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederAlienBall",
                    Triangles = alienBall,
                    IsVisible = true
                });

            if (sideWindows != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederSideWindows",
                    Triangles = sideWindows,
                    IsVisible = true
                });

            if (sideDoor != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederDoor",
                    Triangles = sideDoor,
                    IsVisible = true
                });

            // Invisible helper parts for particles
            if (seederGuide != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederParticlesGuide",
                    Triangles = seederGuide,
                    IsVisible = false
                });

            if (seederStartGuide != null)
                seeder.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "SeederParticlesStartGuide",
                    Triangles = seederStartGuide,
                    IsVisible = false
                });

            // Movement and particles
            seeder.Movement = new SeederControls();
            seeder.Particles = new ParticlesAI
            {
                LifeMultiplier = 3.0f,
                MaxParticlesOverride = 60
            };

            // Default offsets and rotation
            seeder.ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 };
            seeder.Rotation = new Vector3 { x = 0, y = 0, z = 0 };

            if (seederCrashBox != null)
                seeder.CrashBoxes = seederCrashBox;

            seeder.ParentSurface = parentSurface;
            seeder.HasShadow = true;

            _3dObjectHelpers.ApplyScaleToObject(seeder, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(seeder, useFlatQuad: true);

            return seeder;
        }

        // ----------------------------------------------------
        //  GEOMETRY PARTS (all using outward-from-center winding)
        // ----------------------------------------------------

        // Upper dome – triangles point outward from BodyCenter
        public static List<ITriangleMeshWithColor>? SeederTopHull()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Apex of upper dome
            var top = new Vector3 { x = 0, y = 0, z = topDomeHeight };

            // Outer ring at top of belt
            var ring = GenerateCirclePoints(mainSegments, seederRadius, seederThickness);

            for (int i = 0; i < mainSegments; i++)
            {
                int next = (i + 1) % mainSegments;
                string color = (i % 2 == 0) ? bodyColorLight : bodyColorDark;

                var v1 = top;
                var v2 = ring[i];
                var v3 = ring[next];

                tris.Add(CreateTriangleOutward(v1, v2, v3, BodyCenter, color));
            }

            return tris;
        }

        // Lower dome – triangles point outward from BodyCenter
        public static List<ITriangleMeshWithColor>? SeederBottomHull()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Apex of lower dome
            var bottom = new Vector3 { x = 0, y = 0, z = -bottomDomeHeight };

            // Outer ring at bottom of belt
            var ring = GenerateCirclePoints(mainSegments, seederRadius, -seederThickness);

            for (int i = 0; i < mainSegments; i++)
            {
                int next = (i + 1) % mainSegments;
                string color = (i % 2 == 0) ? bodyColorDark : bodyColorLight;

                var v1 = bottom;
                var v2 = ring[next];
                var v3 = ring[i];

                tris.Add(CreateTriangleOutward(v1, v2, v3, BodyCenter, color));
            }

            return tris;
        }

        // Rim ring around the saucer – side quads, normals outward from BodyCenter
        public static List<ITriangleMeshWithColor>? SeederRimRing()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var topRing = GenerateCirclePoints(mainSegments, seederRadius, seederThickness);
            var bottomRing = GenerateCirclePoints(mainSegments, seederRadius, -seederThickness);

            for (int i = 0; i < mainSegments; i++)
            {
                int next = (i + 1) % mainSegments;

                var t1 = topRing[i];
                var t2 = topRing[next];
                var b1 = bottomRing[i];
                var b2 = bottomRing[next];

                string color = (i % 2 == 0) ? rimColorLight : rimColorDark;

                tris.Add(CreateTriangleOutward(t1, t2, b1, BodyCenter, color));
                tris.Add(CreateTriangleOutward(t2, b2, b1, BodyCenter, color));
            }

            return tris;
        }

        // Center module under the saucer – cylinder + bottom cap, outward from BodyCenter
        public static List<ITriangleMeshWithColor>? SeederCenterModule()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float topZ = -seederThickness;
            float bottomZ = -seederThickness - centerModuleHeight;

            var topRing = GenerateCirclePoints(centerSegments, centerModuleRadius, topZ);
            var bottomRing = GenerateCirclePoints(centerSegments, centerModuleRadius, bottomZ);
            var bottomCenter = new Vector3 { x = 0, y = 0, z = bottomZ };

            for (int i = 0; i < centerSegments; i++)
            {
                int next = (i + 1) % centerSegments;
                string sideColor = (i % 2 == 0) ? moduleColorDark : bodyColorDark;

                var t1 = topRing[i];
                var t2 = topRing[next];
                var b1 = bottomRing[i];
                var b2 = bottomRing[next];

                // Sides
                tris.Add(CreateTriangleOutward(t1, t2, b1, BodyCenter, sideColor));
                tris.Add(CreateTriangleOutward(t2, b2, b1, BodyCenter, sideColor));

                // Bottom cap (still outward from BodyCenter, i.e. mostly -Z)
                tris.Add(CreateTriangleOutward(bottomCenter, b2, b1, BodyCenter, moduleColorDark));
            }

            return tris;
        }

        // Rectangular-like panels on the underside, pushed to the absolute bottom
        // of the lower dome while still staying inside the hull volume.
        public static List<ITriangleMeshWithColor>? SeederPanels()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Radii that keep panels fully inside the hull outline
            float innerRadius = seederRadius * 0.25f;
            float outerRadius = seederRadius * 0.55f;

            // Place panels almost at the very bottom of the dome:
            // If dome bottom is -12, this becomes -11.9.
            float panelZ = -bottomDomeHeight + 0.1f;

            var innerRing = GenerateCirclePoints(panelSegments, innerRadius, panelZ);
            var outerRing = GenerateCirclePoints(panelSegments, outerRadius, panelZ);

            for (int i = 0; i < panelSegments; i++)
            {
                int next = (i + 1) % panelSegments;

                var i1 = innerRing[i];
                var i2 = innerRing[next];
                var o1 = outerRing[i];
                var o2 = outerRing[next];

                // Still enforce outward-facing normals
                tris.Add(CreateTriangleOutward(i1, o1, o2, BodyCenter, panelColor));
                tris.Add(CreateTriangleOutward(i1, o2, i2, BodyCenter, panelColor));
            }

            return tris;
        }


        // Alien ball on top – outward from its own center
        public static List<ITriangleMeshWithColor>? SeederAlienBall()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float centerZ = topDomeHeight + alienRadius * 0.6f; // Slight overlap into upper dome
            var sphereCenter = new Vector3 { x = 0, y = 0, z = centerZ };

            float latStep = (float)(Math.PI / alienLatSegments);
            float lonStep = (float)(2 * Math.PI / alienLonSegments);

            for (int lat = 0; lat < alienLatSegments; lat++)
            {
                float phi1 = -0.5f * (float)Math.PI + lat * latStep;
                float phi2 = -0.5f * (float)Math.PI + (lat + 1) * latStep;

                float cosPhi1 = (float)Math.Cos(phi1);
                float sinPhi1 = (float)Math.Sin(phi1);
                float cosPhi2 = (float)Math.Cos(phi2);
                float sinPhi2 = (float)Math.Sin(phi2);

                string bandColor = lat switch
                {
                    0 => alienRedDark,
                    1 => alienRedMedium,
                    2 => alienRedBright,
                    _ => alienRedBright
                };

                for (int lon = 0; lon < alienLonSegments; lon++)
                {
                    float theta1 = lon * lonStep;
                    float theta2 = (lon + 1) * lonStep;

                    float cosTheta1 = (float)Math.Cos(theta1);
                    float sinTheta1 = (float)Math.Sin(theta1);
                    float cosTheta2 = (float)Math.Cos(theta2);
                    float sinTheta2 = (float)Math.Sin(theta2);

                    string color = ((lat + lon) % 2 == 0) ? bandColor : alienRedMedium;

                    var p11 = new Vector3
                    {
                        x = alienRadius * cosPhi1 * cosTheta1,
                        y = alienRadius * cosPhi1 * sinTheta1,
                        z = centerZ + alienRadius * sinPhi1
                    };

                    var p12 = new Vector3
                    {
                        x = alienRadius * cosPhi1 * cosTheta2,
                        y = alienRadius * cosPhi1 * sinTheta2,
                        z = centerZ + alienRadius * sinPhi1
                    };

                    var p21 = new Vector3
                    {
                        x = alienRadius * cosPhi2 * cosTheta1,
                        y = alienRadius * cosPhi2 * sinTheta1,
                        z = centerZ + alienRadius * sinPhi2
                    };

                    var p22 = new Vector3
                    {
                        x = alienRadius * cosPhi2 * cosTheta2,
                        y = alienRadius * cosPhi2 * sinTheta2,
                        z = centerZ + alienRadius * sinPhi2
                    };

                    tris.Add(CreateTriangleOutward(p11, p21, p22, sphereCenter, color));
                    tris.Add(CreateTriangleOutward(p11, p22, p12, sphereCenter, color));
                }
            }

            return tris;
        }

        // Side windows – outward from BodyCenter (so hidden-face will behave correctly)
        public static List<ITriangleMeshWithColor>? SeederSideWindows()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float windowWidth = 6f;
            float windowHeight = 4f;
            float halfW = windowWidth / 2f;

            float bottomZ = -1f;
            float topZ = bottomZ + windowHeight;
            float offset = 0.4f;

            // +X side window
            {
                float x = seederRadius + offset;

                var v1 = new Vector3 { x = x, y = -halfW, z = bottomZ };
                var v2 = new Vector3 { x = x, y = halfW, z = bottomZ };
                var v3 = new Vector3 { x = x, y = -halfW, z = topZ };
                var v4 = new Vector3 { x = x, y = halfW, z = topZ };

                tris.Add(CreateTriangleOutward(v1, v2, v4, BodyCenter, windowColor));
                tris.Add(CreateTriangleOutward(v1, v4, v3, BodyCenter, windowColor));
            }

            // -X side window
            {
                float x = -seederRadius - offset;

                var v1 = new Vector3 { x = x, y = -halfW, z = bottomZ };
                var v2 = new Vector3 { x = x, y = halfW, z = bottomZ };
                var v3 = new Vector3 { x = x, y = -halfW, z = topZ };
                var v4 = new Vector3 { x = x, y = halfW, z = topZ };

                tris.Add(CreateTriangleOutward(v2, v1, v3, BodyCenter, windowColor));
                tris.Add(CreateTriangleOutward(v2, v3, v4, BodyCenter, windowColor));
            }

            // +Y side window
            {
                float y = seederRadius + offset;

                var v1 = new Vector3 { x = -halfW, y = y, z = bottomZ };
                var v2 = new Vector3 { x = halfW, y = y, z = bottomZ };
                var v3 = new Vector3 { x = -halfW, y = y, z = topZ };
                var v4 = new Vector3 { x = halfW, y = y, z = topZ };

                tris.Add(CreateTriangleOutward(v2, v1, v3, BodyCenter, windowColor));
                tris.Add(CreateTriangleOutward(v2, v3, v4, BodyCenter, windowColor));
            }

            // -Y side window
            {
                float y = -seederRadius - offset;

                var v1 = new Vector3 { x = -halfW, y = y, z = bottomZ };
                var v2 = new Vector3 { x = halfW, y = y, z = bottomZ };
                var v3 = new Vector3 { x = -halfW, y = y, z = topZ };
                var v4 = new Vector3 { x = halfW, y = y, z = topZ };

                tris.Add(CreateTriangleOutward(v1, v2, v4, BodyCenter, windowColor));
                tris.Add(CreateTriangleOutward(v1, v4, v3, BodyCenter, windowColor));
            }

            return tris;
        }

        // Door on the -Y side – outward from BodyCenter
        public static List<ITriangleMeshWithColor>? SeederDoor()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float doorWidth = 8f;
            float doorHeight = 7f;
            float halfW = doorWidth / 2f;

            float bottomZ = -2f;
            float topZ = bottomZ + doorHeight;
            float offset = 0.45f;

            float y = -seederRadius - offset;

            var d1 = new Vector3 { x = halfW, y = y, z = bottomZ };
            var d2 = new Vector3 { x = -halfW, y = y, z = bottomZ };
            var d3 = new Vector3 { x = -halfW, y = y, z = topZ };
            var d4 = new Vector3 { x = halfW, y = y, z = topZ };

            tris.Add(CreateTriangleOutward(d2, d1, d4, BodyCenter, doorColor));
            tris.Add(CreateTriangleOutward(d2, d4, d3, BodyCenter, doorColor));

            return tris;
        }

        // ----------------------------------------------------
        //  PARTICLE GUIDES (only these use noHidden = true)
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ParticlesDirectionGuide()
        {
            float guideZ = -seederThickness - centerModuleHeight - particleGuideDistance;
            var direction = new List<ITriangleMeshWithColor>
            {
                CreateParticleGuidePoint(guideZ)
            };
            return direction;
        }

        public static List<ITriangleMeshWithColor>? ParticlesStartGuide()
        {
            float startZ = -seederThickness - centerModuleHeight;
            var direction = new List<ITriangleMeshWithColor>
            {
                CreateParticleGuidePoint(startZ)
            };
            return direction;
        }

        private static TriangleMeshWithColor CreateParticleGuidePoint(float z)
        {
            var point = new Vector3 { x = 0, y = 0, z = z };

            return CreateTriangleOutward(
                point,
                new Vector3 { x = point.x, y = point.y, z = point.z },
                new Vector3 { x = point.x, y = point.y, z = point.z },
                BodyCenter,
                "ffffff",
                noHidden: true
            );
        }

        // ----------------------------------------------------
        //  CRASH BOX
        // ----------------------------------------------------

        public static List<List<IVector3>>? SeederCrashBoxes()
        {
            float expandXY = seederRadius * 0.15f;
            float expandZ = (topDomeHeight + centerModuleHeight + alienRadius) * 0.15f;

            // Z max extent — from top of dome + alien ball + margin
            float zMax = topDomeHeight + alienRadius + expandZ;

            // Mirror Z min to match Z max so the crash box is Z-symmetric.
            // The seeder's BaseXRotation = 90 transforms Z → Y; an asymmetric Z
            // shifts the crash box center away from the visual center, forcing the
            // player to aim below the seeder to register a hit.
            var min = new Vector3
            {
                x = -seederRadius - expandXY,
                y = -seederRadius - expandXY,
                z = -zMax
            };

            var max = new Vector3
            {
                x = seederRadius + expandXY,
                y = seederRadius + expandXY,
                z = zMax
            };

            var crashBoxCorners = _3dObjectHelpers.GenerateCrashBoxCorners(min, max);

            return new List<List<IVector3>> { crashBoxCorners };
        }

        // ----------------------------------------------------
        //  HELPER METHODS (Right-hand rule enforcement)
        // ----------------------------------------------------

        private static List<Vector3> GenerateCirclePoints(int segments, float radius, float z)
        {
            var points = new List<Vector3>(segments);
            float angleStep = (float)(2 * Math.PI / segments);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float x = radius * (float)Math.Cos(angle);
                float y = radius * (float)Math.Sin(angle);

                points.Add(new Vector3 { x = x, y = y, z = z });
            }

            return points;
        }
    }
}
