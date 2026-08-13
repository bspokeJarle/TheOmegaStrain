using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Ai;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class Bullet
    {
        private const float ZoomRatio = 1f;

        public static _3dObject CreateBullet(ISurface parentSurface)
        {
            var body = BulletBody();
            var crash = BulletCrashBoxes();

            var bullet = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                Particles = new ParticlesAI(),
                ParentSurface = parentSurface,
                ObjectName = "Bullet",
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "Bullet" }
            };

            if (body != null)
            {
                bullet.ObjectParts.Add(new _3dObjectPart
                {
                    PartName = "BulletBody",
                    Triangles = body,
                    IsVisible = true
                });
            }

            if (crash != null)
                bullet.CrashBoxes = crash;

            _3dObjectHelpers.ApplyScaleToObject(bullet, ZoomRatio);

            return bullet;
        }

        // ----------------------------------------------------
        //  CRASH BOX
        // ----------------------------------------------------

        public static List<List<IVector3>>? BulletCrashBoxes()
        {
            var min = new Vector3
            {
                x = WeaponSetup.BulletCrashBoxMinX,
                y = WeaponSetup.BulletCrashBoxMinY,
                z = WeaponSetup.BulletCrashBoxMinZ
            };

            var max = new Vector3
            {
                x = WeaponSetup.BulletCrashBoxMaxX,
                y = WeaponSetup.BulletCrashBoxMaxY,
                z = WeaponSetup.BulletCrashBoxMaxZ
            };

            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(min, max)
            };
        }

        // ----------------------------------------------------
        //  BULLET GEOMETRY
        // ----------------------------------------------------
        //
        // 12 triangles total:
        // - 3 front tip triangles
        // - 6 side triangles
        // - 3 rear tip triangles
        //
        // Axis is along -Y
        // Centered around X=0, Z=28
        //

        public static List<ITriangleMeshWithColor>? BulletBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Forward direction is -Y
            float yFrontTip = -28f;
            float yFrontBase = -20f;
            float yRearBase = -8f;
            float yRearTip = -2f;

            // Cross-section radii
            float radiusFrontBase = 2.2f;
            float radiusRearBase = 1.6f;

            float centerX = 0f;
            float centerZ = 28f;

            // Equilateral triangle ring in X/Z plane
            float[] ca = { 1f, -0.5f, -0.5f };
            float s3 = 0.8660254f;
            float[] sa = { 0f, s3, -s3 };

            // Front tip
            var frontTip = new Vector3
            {
                x = centerX,
                y = yFrontTip,
                z = centerZ
            };

            // Rear tip
            var rearTip = new Vector3
            {
                x = centerX,
                y = yRearTip,
                z = centerZ
            };

            // Front ring
            var frontRing = new Vector3[3];
            // Rear ring
            var rearRing = new Vector3[3];

            for (int i = 0; i < 3; i++)
            {
                frontRing[i] = new Vector3
                {
                    x = centerX + radiusFrontBase * ca[i],
                    y = yFrontBase,
                    z = centerZ + radiusFrontBase * sa[i]
                };

                rearRing[i] = new Vector3
                {
                    x = centerX + radiusRearBase * ca[i],
                    y = yRearBase,
                    z = centerZ + radiusRearBase * sa[i]
                };
            }

            // -------------------------
            // Front tip (3 triangles)
            // -------------------------
            tris.Add(new TriangleMeshWithColor { Color = "A8FFFF", vert1 = frontTip, vert2 = frontRing[1], vert3 = frontRing[0] });
            tris.Add(new TriangleMeshWithColor { Color = "7FEFFF", vert1 = frontTip, vert2 = frontRing[2], vert3 = frontRing[1] });
            tris.Add(new TriangleMeshWithColor { Color = "5FD8F0", vert1 = frontTip, vert2 = frontRing[0], vert3 = frontRing[2] });

            // -------------------------
            // Side body (6 triangles)
            // -------------------------
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;

                tris.Add(new TriangleMeshWithColor
                {
                    Color = "36C6E0",
                    vert1 = frontRing[i],
                    vert2 = frontRing[j],
                    vert3 = rearRing[j]
                });

                tris.Add(new TriangleMeshWithColor
                {
                    Color = "1299B8",
                    vert1 = frontRing[i],
                    vert2 = rearRing[j],
                    vert3 = rearRing[i]
                });
            }

            // -------------------------
            // Rear tip (3 triangles)
            // -------------------------
            tris.Add(new TriangleMeshWithColor { Color = "0D6F8A", vert1 = rearTip, vert2 = rearRing[0], vert3 = rearRing[1] });
            tris.Add(new TriangleMeshWithColor { Color = "0A5970", vert1 = rearTip, vert2 = rearRing[1], vert3 = rearRing[2] });
            tris.Add(new TriangleMeshWithColor { Color = "08485C", vert1 = rearTip, vert2 = rearRing[2], vert3 = rearRing[0] });

            return tris;
        }
    }
}