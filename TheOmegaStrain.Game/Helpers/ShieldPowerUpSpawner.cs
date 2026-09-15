using System;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Game.Helpers
{
    /// <summary>
    /// Keeps a rolling supply of Shield powerups on the map: at least one is always
    /// available, and extras appear at randomized intervals and randomized world
    /// positions (measured as a distance/bearing from the ship) up to a cap.
    /// Driven from a scene director's per-frame Update.
    /// </summary>
    public sealed class ShieldPowerUpSpawner
    {
        private const int MaxConcurrentShields = 3;
        private const float MinSpawnIntervalSeconds = 25f;
        private const float MaxSpawnIntervalSeconds = 55f;

        // Spawn ring around the ship, in screen widths, so a new pickup is never
        // dropped in the player's lap nor placed unreachably far away.
        private const float MinSpawnDistanceInScreens = 1.8f;
        private const float MaxSpawnDistanceInScreens = 5.0f;

        private readonly Random _random;
        private float _secondsUntilNextSpawn;
        private DateTime _lastUpdate = DateTime.MinValue;

        public ShieldPowerUpSpawner(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _secondsUntilNextSpawn = NextInterval();
        }

        public void Update(I3dWorld? world)
        {
            if (world == null) return;

            var surface = GameState.SurfaceState?.SurfaceViewportObject?.ParentSurface;
            if (surface == null) return;

            var now = DateTime.Now;
            if (_lastUpdate == DateTime.MinValue)
                _lastUpdate = now;

            float deltaSeconds = Math.Clamp((float)(now - _lastUpdate).TotalSeconds, 0f, 0.5f);
            _lastUpdate = now;

            int liveShields = CountLiveShields();
            if (liveShields >= MaxConcurrentShields)
                return;

            // The map should never be completely without a shield to chase.
            if (liveShields == 0)
            {
                Spawn(world, surface);
                _secondsUntilNextSpawn = NextInterval();
                return;
            }

            _secondsUntilNextSpawn -= deltaSeconds;
            if (_secondsUntilNextSpawn > 0f)
                return;

            Spawn(world, surface);
            _secondsUntilNextSpawn = NextInterval();
        }

        private static int CountLiveShields()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return 0;

            int count = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (obj.ObjectName != "PowerUp") continue;
                if (obj.PowerUpType != PowerUpType.Shield) continue;
                if (obj.ImpactStatus?.HasExploded == true) continue;
                // A collected powerup clears its parts while the explosion plays out.
                if (obj.ObjectParts == null || obj.ObjectParts.Count == 0) continue;

                count++;
            }

            return count;
        }

        private void Spawn(I3dWorld world, ISurface surface)
        {
            var shield = PowerUp.CreatePowerup(surface, PowerUpType.Shield);
            shield.WorldPosition = PickSpawnPosition(surface);
            shield.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 600 };
            shield.ImpactStatus = new ImpactStatus { ObjectName = "PowerUp" };
            shield.Movement = new PowerUpControls();
            shield.CrashBoxDebugMode = false;
            shield.IsActive = true;

            world.WorldInhabitants.Add(shield);
            GameState.SurfaceState.AiObjects.Add(shield);
        }

        private Vector3 PickSpawnPosition(ISurface surface)
        {
            float mapExtent = Math.Max(1f, surface.GlobalMapSize() * surface.TileSize());

            var shipWorld = GameState.ShipState?.ShipWorldPosition
                ?? GameState.SurfaceState.GlobalMapPosition;

            float bearing = (float)(_random.NextDouble() * Math.PI * 2d);
            float distance = ScreenSetup.screenSizeX *
                (MinSpawnDistanceInScreens +
                 (float)_random.NextDouble() * (MaxSpawnDistanceInScreens - MinSpawnDistanceInScreens));

            return new Vector3
            {
                x = Wrap(shipWorld.x + MathF.Cos(bearing) * distance, mapExtent),
                y = 0f,
                z = Wrap(shipWorld.z + MathF.Sin(bearing) * distance, mapExtent)
            };
        }

        private float NextInterval()
        {
            return MinSpawnIntervalSeconds +
                   (float)_random.NextDouble() * (MaxSpawnIntervalSeconds - MinSpawnIntervalSeconds);
        }

        private static float Wrap(float value, float extent)
        {
            float wrapped = value % extent;
            return wrapped < 0f ? wrapped + extent : wrapped;
        }
    }
}
