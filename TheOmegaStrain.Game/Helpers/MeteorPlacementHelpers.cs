using System;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects.EarthObject;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Game.Helpers
{
    /// <summary>
    /// Adds the shared distant-sky meteor used by gameplay scenes.
    /// It is a visual background event and is not part of enemy or collision state.
    /// </summary>
    public static class MeteorPlacementHelpers
    {
        private const float MinimumWaitSeconds = 20f;
        private const float MaximumWaitSeconds = 35f;
        private const float MeteorDepth = ScreenSetup.RenderFarZ - 100f;

        public static void AddOccasionalMeteor(I3dWorld world, int randomSeed)
        {
            var meteor = AsteroidObject.CreateAsteroid(
                colorPalette: new[] { "FFB347", "FF6A00", "7A2E00" },
                // Compensate only the mesh size for the extreme background depth.
                size: 46f,
                startOffsetX: 0f,
                startOffsetY: -ScreenSetup.screenSizeY,
                depth: MeteorDepth,
                rng: new Random(randomSeed));
            var controls = new AsteroidControls(
                new Random(randomSeed + 1),
                MeteorDepth,
                startImmediately: false)
            {
                EmitTrailParticles = true,
                SpeedMultiplier = 1.15f
            };
            controls.ConfigureTopDownCrossings(MinimumWaitSeconds, MaximumWaitSeconds);
            meteor.Movement = controls;
            meteor.Particles = new ParticlesAI
            {
                MaxParticlesOverride = 42,
                LifeMultiplier = 0.58f,
                SizeMultiplier = 1.5f,
                GravityStrength = 12f,
                ThrottleDurationFactor = 0.18f,
                ColorStartOverride = "FFE46A",
                ColorMidOverride = "FF5A00",
                ColorEndOverride = "7A0800"
            };
            world.WorldInhabitants.Add(meteor);
        }
    }
}
