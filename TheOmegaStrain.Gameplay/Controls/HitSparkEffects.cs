using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    public static class HitSparkEffects
    {
        private const int BalancedSparkThrust = 1;
        private const int HighSparkThrust = 2;
        private const int ExplosionParticlesPerThrust = 10;
        private const float SparkLifeMultiplier = 0.16f;
        private const float SparkSizeMultiplier = 1.15f;
        private const float SparkGravityStrength = 34f;
        private const float SparkUpwardVelocityBoost = 2.5f;

        public static int GetSparkParticleCountForCurrentQuality()
        {
            return GetSparkThrustForCurrentQuality() * ExplosionParticlesPerThrust;
        }

        public static bool IsWeaponHit(string? objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            if (objectName.Contains("Laser", StringComparison.OrdinalIgnoreCase))
                return true;

            return WeaponSetup.WeaponTypes.Any(weapon =>
                string.Equals(objectName, weapon.Item1, StringComparison.OrdinalIgnoreCase) ||
                objectName.Contains(weapon.Item1, StringComparison.OrdinalIgnoreCase));
        }

        public static void ReleaseHitSparks(I3dObject theObject, IObjectMovement parentMovement, string? objectName)
        {
            if (theObject.Particles == null || !IsWeaponHit(objectName))
                return;

            int thrust = GetSparkThrustForCurrentQuality();
            if (thrust <= 0)
                return;

            if (!TryCreateHitSparkGuides(theObject, out var start, out var guide))
                return;

            if (theObject.Particles is ParticlesAI particles)
            {
                ReleaseStyledHitSparks(theObject, parentMovement, start, guide, particles, thrust, objectName);
                return;
            }

            theObject.Particles.ReleaseParticles(
                guide,
                start,
                ToVector3(theObject.WorldPosition),
                parentMovement,
                thrust,
                true,
                SparkUpwardVelocityBoost);
            theObject.Particles.MoveParticles();
        }

        public static void MoveHitSparks(I3dObject theObject)
        {
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();
        }

        private static void ReleaseStyledHitSparks(
            I3dObject theObject,
            IObjectMovement parentMovement,
            ITriangleMeshWithColor start,
            ITriangleMeshWithColor guide,
            ParticlesAI particles,
            int thrust,
            string? objectName)
        {
            string? previousStart = particles.ColorStartOverride;
            string? previousMid = particles.ColorMidOverride;
            string? previousEnd = particles.ColorEndOverride;
            float previousLifeMultiplier = particles.LifeMultiplier;
            float previousSizeMultiplier = particles.SizeMultiplier;
            float previousGravityStrength = particles.GravityStrength;
            float? previousExplosionParticleMultiplierOverride = particles.ExplosionParticleMultiplierOverride;
            float previousExplosionStartYOffset = particles.ExplosionStartYOffset;
            int previousMaxParticlesOverride = particles.MaxParticlesOverride;

            try
            {
                ApplySparkStyle(particles, objectName);
                int sparkBudget = GetSparkParticleCountForCurrentQuality();
                particles.MaxParticlesOverride = Math.Max(
                    previousMaxParticlesOverride,
                    particles.Particles.Count + sparkBudget + 8);

                particles.ReleaseParticles(
                    guide,
                    start,
                    ToVector3(theObject.WorldPosition),
                    parentMovement,
                    thrust,
                    true,
                    SparkUpwardVelocityBoost);
            }
            finally
            {
                particles.ColorStartOverride = previousStart;
                particles.ColorMidOverride = previousMid;
                particles.ColorEndOverride = previousEnd;
                particles.LifeMultiplier = previousLifeMultiplier;
                particles.SizeMultiplier = previousSizeMultiplier;
                particles.GravityStrength = previousGravityStrength;
                particles.ExplosionParticleMultiplierOverride = previousExplosionParticleMultiplierOverride;
                particles.ExplosionStartYOffset = previousExplosionStartYOffset;
                particles.MaxParticlesOverride = previousMaxParticlesOverride;
            }

            particles.MoveParticles();
        }

        private static void ApplySparkStyle(ParticlesAI particles, string? objectName)
        {
            if (objectName?.Contains("Lazer", StringComparison.OrdinalIgnoreCase) == true ||
                objectName?.Contains("Laser", StringComparison.OrdinalIgnoreCase) == true)
            {
                particles.ColorStartOverride = "e8fbff";
                particles.ColorMidOverride = "5be7ff";
                particles.ColorEndOverride = "0d5dff";
            }
            else
            {
                particles.ColorStartOverride = "fff8c8";
                particles.ColorMidOverride = "ffb02e";
                particles.ColorEndOverride = "d65a10";
            }

            particles.LifeMultiplier = SparkLifeMultiplier;
            particles.SizeMultiplier = SparkSizeMultiplier;
            particles.GravityStrength = SparkGravityStrength;
            particles.ExplosionParticleMultiplierOverride = 2.0f;
            particles.ExplosionStartYOffset = 0f;
        }

        private static int GetSparkThrustForCurrentQuality()
        {
            return (GameState.SettingsState?.GraphicsQuality ?? GraphicsQualityPreset.Balanced) switch
            {
                GraphicsQualityPreset.Low => 0,
                GraphicsQualityPreset.High => HighSparkThrust,
                _ => BalancedSparkThrust
            };
        }

        private static bool TryCreateHitSparkGuides(I3dObject theObject, out ITriangleMeshWithColor start, out ITriangleMeshWithColor guide)
        {
            start = null!;
            guide = null!;

            var points = new List<IVector3>();
            CollectPoints(theObject, visibleOnly: true, points);

            if (points.Count == 0)
                CollectPoints(theObject, visibleOnly: false, points);

            if (points.Count == 0)
                return false;

            var center = GetCenter(points);
            start = CreatePointTriangle(center);
            guide = CreatePointTriangle(new Vector3 { x = center.x, y = center.y - 1f, z = center.z });
            return true;
        }

        private static void CollectPoints(I3dObject theObject, bool visibleOnly, List<IVector3> points)
        {
            foreach (var part in theObject.ObjectParts)
            {
                if (visibleOnly && !part.IsVisible) continue;
                if (part.PartName?.Contains("Guide", StringComparison.OrdinalIgnoreCase) == true) continue;
                if (part.Triangles == null) continue;

                foreach (var triangle in part.Triangles)
                {
                    points.Add(triangle.vert1);
                    points.Add(triangle.vert2);
                    points.Add(triangle.vert3);
                }
            }
        }

        private static Vector3 GetCenter(List<IVector3> points)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;

            foreach (var point in points)
            {
                x += point.x;
                y += point.y;
                z += point.z;
            }

            float count = points.Count;
            return new Vector3 { x = x / count, y = y / count, z = z / count };
        }

        private static ITriangleMeshWithColor CreatePointTriangle(Vector3 point)
        {
            return new TriangleMeshWithColor
            {
                Color = "ffffff",
                noHidden = true,
                vert1 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert2 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert3 = new Vector3 { x = point.x, y = point.y, z = point.z }
            };
        }

        private static Vector3 ToVector3(IVector3? vector)
        {
            if (vector == null) return new Vector3();
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }
    }
}
