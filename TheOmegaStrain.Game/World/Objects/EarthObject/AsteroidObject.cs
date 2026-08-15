using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects.EarthObject
{
    public static class AsteroidObject
    {
        public static _3dObject CreateAsteroid(string[] colorPalette, float size, float startOffsetX, float startOffsetY, float depth, Random rng)
        {
            var tris = BuildAsteroidGeometry(colorPalette, size, rng);

            var obj = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "Asteroid",
                ObjectOffsets = new Vector3 { x = startOffsetX, y = startOffsetY, z = depth },
                Rotation = new Vector3 { x = WorldViewSetup.WorldPitchDegrees, y = 0f, z = 90f },
                WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
                CrashBoxes = new List<List<IVector3>>(),
                IsActive = true
            };

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "AsteroidBody",
                Triangles = tris,
                IsVisible = true
            });

            obj.ImpactStatus = new ImpactStatus();
            return obj;
        }

        private static List<ITriangleMeshWithColor> BuildAsteroidGeometry(string[] palette, float size, Random rng)
        {
            // Force greyscale regardless of the supplied palette — asteroids
            // should read as dirty rocks, not coloured candy. We still take
            // 'palette' to preserve the existing public API.
            string[] greys =
            {
                "5A5A5A", "6E6E6E", "808080", "949494",
                "A6A6A6", "B8B8B8", "4F4F4F", "747474"
            };
            string G() => greys[rng.Next(greys.Length)];

            float j = size * 0.18f;
            float Jit() => (float)(rng.NextDouble() - 0.5) * j * 2f;

            // Build a rocket-like 8-sided body running along the local Y axis,
            // with a pointed nose at -Y, a barrel mid section, and a tapered
            // tail at +Y. Vertices are jittered so each asteroid is unique
            // and reads as a lumpy rock rather than a clean rocket.
            float nose  = -size * 1.50f;
            float front = -size * 0.55f;
            float mid   =  size * 0.20f;
            float rear  =  size * 0.95f;
            float tail  =  size * 1.45f;

            float rFront = size * 0.55f;
            float rMid   = size * 0.85f;
            float rRear  = size * 0.70f;

            const int Sides = 8;
            var ring = new (float cos, float sin)[Sides];
            for (int i = 0; i < Sides; i++)
            {
                double a = i * (2.0 * Math.PI / Sides);
                ring[i] = ((float)Math.Cos(a), (float)Math.Sin(a));
            }

            Vector3 Ring(float y, float radius, int i)
            {
                return new Vector3
                {
                    x = ring[i].cos * radius + Jit(),
                    y = y + Jit() * 0.4f,
                    z = ring[i].sin * radius + Jit()
                };
            }

            var noseTip = new Vector3 { x = Jit() * 0.3f, y = nose,  z = Jit() * 0.3f };
            var tailTip = new Vector3 { x = Jit() * 0.3f, y = tail,  z = Jit() * 0.3f };

            var rFrontRing = new Vector3[Sides];
            var rMidRing   = new Vector3[Sides];
            var rRearRing  = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                rFrontRing[i] = Ring(front, rFront, i);
                rMidRing[i]   = Ring(mid,   rMid,   i);
                rRearRing[i]  = Ring(rear,  rRear,  i);
            }

            var tris = new List<ITriangleMeshWithColor>(Sides * 8);

            // Nose cone: noseTip -> rFrontRing
            for (int i = 0; i < Sides; i++)
            {
                int j2 = (i + 1) % Sides;
                tris.Add(Tri(noseTip, rFrontRing[j2], rFrontRing[i], G()));
            }
            // Front-to-mid hull
            for (int i = 0; i < Sides; i++)
            {
                int j2 = (i + 1) % Sides;
                tris.Add(Tri(rFrontRing[i], rFrontRing[j2], rMidRing[j2], G()));
                tris.Add(Tri(rFrontRing[i], rMidRing[j2],   rMidRing[i],  G()));
            }
            // Mid-to-rear hull
            for (int i = 0; i < Sides; i++)
            {
                int j2 = (i + 1) % Sides;
                tris.Add(Tri(rMidRing[i], rMidRing[j2], rRearRing[j2], G()));
                tris.Add(Tri(rMidRing[i], rRearRing[j2], rRearRing[i], G()));
            }
            // Tail cone: rRearRing -> tailTip
            for (int i = 0; i < Sides; i++)
            {
                int j2 = (i + 1) % Sides;
                tris.Add(Tri(rRearRing[i], rRearRing[j2], tailTip, G()));
            }

            return tris;
        }

        private static TriangleMeshWithColor Tri(Vector3 v1, Vector3 v2, Vector3 v3, string color) =>
            new TriangleMeshWithColor { Color = color, vert1 = v1, vert2 = v2, vert3 = v3, noHidden = true };
    }
}
