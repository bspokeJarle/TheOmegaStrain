using _3dRotations.World.Objects;
using _3dRotations.World.Objects.EarthObject;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using System;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Outro
{
    public class Outro : IScene
    {
        public GameModes GameMode { get; } = GameModes.Live;
        public string SceneMusic { get; } = "music_outro";
        public SceneTypes SceneType { get; } = SceneTypes.Outro;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.HillsWoods;
        public ISceneDirector Director { get; } = new OutroDirector();

        private static readonly Random _rng = new Random(77);

        public void SetupScene(I3dWorld world)
        {
            var earth = EarthObject.CreateEarth();
            earth.Movement = new EarthObjectControls();
            world.WorldInhabitants.Add(earth);

            var ship = Ship.CreateShip(null, new OutroShipControls());
            ship.ObjectName = "Ship";
            ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
            ship.ObjectOffsets = OutroShipControls.CreateInitialOffset();
            ship.Rotation = OutroShipControls.CreateInitialRotation();
            ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
            ship.CrashBoxDebugMode = false;
            ship.WeaponSystems = null;
            ship.ParentSurface = null;
            ship.HasShadow = false;
            world.WorldInhabitants.Add(ship);

            // White icy asteroid — crosses the screen entirely ABOVE the ship
            // (both endpoints stay in the upper half of the screen) so it
            // does not look like it nearly clips the ship at mid-screen.
            var asteroid1 = AsteroidObject.CreateAsteroid(
                colorPalette: new[] { "E8E8E8", "FFFFFF", "C8D8E8", "B0C8D8" },
                size: 28f,
                startOffsetX: -ScreenSetup.screenSizeX * 0.6f,
                startOffsetY: -ScreenSetup.screenSizeY * 0.55f,
                depth: 385f,
                rng: _rng);
            var ctrl1 = new AsteroidControls(new Random(13), 385f, startImmediately: true)
            {
                EmitTrailParticles = true,
                SpeedMultiplier = 1.25f
            };
            ctrl1.ForceScreenPath(-0.96f, -0.80f, 0.84f, -0.30f);
            asteroid1.Movement = ctrl1;
            asteroid1.Particles = new ParticlesAI
            {
                MaxParticlesOverride = 42,
                LifeMultiplier = 0.62f,
                ThrottleDurationFactor = 0.18f,
                ColorStartOverride = "E8FAFF",
                ColorMidOverride = "8FD7FF",
                ColorEndOverride = "304860"
            };
            world.WorldInhabitants.Add(asteroid1);

            // Fiery yellow-red asteroid — crosses the screen entirely BELOW
            // the ship (both endpoints stay in the lower half of the screen)
            // so it never threads the middle where the ship is flying.
            var asteroid2 = AsteroidObject.CreateAsteroid(
                colorPalette: new[] { "FF8800", "FF4400", "FFCC00", "CC3300", "FF6600" },
                size: 26f,
                startOffsetX:  ScreenSetup.screenSizeX * 0.6f,
                startOffsetY:  ScreenSetup.screenSizeY * 0.35f,
                depth: 365f,
                rng: _rng);
            var ctrl2 = new AsteroidControls(new Random(37), 365f, startImmediately: true)
            {
                EmitTrailParticles = true,
                SpeedMultiplier = 1.18f
            };
            ctrl2.ForceScreenPath(0.96f, 0.32f, -0.88f, 0.78f);
            asteroid2.Movement = ctrl2;
            asteroid2.Particles = new ParticlesAI
            {
                MaxParticlesOverride = 42,
                LifeMultiplier = 0.58f,
                ThrottleDurationFactor = 0.18f,
                ColorStartOverride = "FFE46A",
                ColorMidOverride = "FF6A00",
                ColorEndOverride = "5A2200"
            };
            world.WorldInhabitants.Add(asteroid2);
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;
            o.Type = ScreenOverlayType.Outro;
            o.ShowOverlay = false;
            o.AutoHide = false;
            o.ShowDebugOverlay = false;
        }

        public void SetupGameOverlay() { }

        public void SetupVideoOverlay(string fileName) { }
    }
}
