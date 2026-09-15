using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Game.Helpers;

public static class AttackShipPlacementHelpers
{
    public static void AddAttackShipGroup(I3dWorld world, ISurface surface, int count,
        int spawnSpread, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spawnSpread);
        random ??= new Random();

        for (int i = 0; i < count; i++)
        {
            var attackShip = AttackShip.CreateAttackShip(surface);
            attackShip.WorldPosition = FindSpawnPosition(spawnSpread, random);
            attackShip.Rotation = new Vector3();
            attackShip.ObjectOffsets = new Vector3(0f, 150f, 100f);
            attackShip.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.AttackShipHealth };
            attackShip.CrashBoxDebugMode = false;
            attackShip.HasPowerUp = false;
            attackShip.IsActive = true;
            attackShip.Movement = new AttackShipControls();
            // Each owner needs its own template, controller and ActiveWeapons list.
            var weapons = new List<I3dObject> { Rocket.CreateRocket(surface) };
            attackShip.WeaponSystems = new Weapons(weapons, attackShip.Movement, attackShip)
            {
                ShowAimAssist = false,
                FireAsEnemyWeapon = true
            };
            world.WorldInhabitants.Add(attackShip);
            GameState.SurfaceState.AiObjects.Add(attackShip);
        }
    }

    private static Vector3 FindSpawnPosition(int spread, Random random)
    {
        var map = GameState.SurfaceState.Global2DMap;
        float ws = SurfaceSetup.WorldScale;
        for (int attempt = 0; attempt < 128; attempt++)
        {
            // Same authored centre and square distribution as the campaign drones.
            var position = new Vector3(
                (95700 + random.Next(-spread, spread)) * ws, 0f,
                (92000 + random.Next(-spread, spread)) * ws);
            if (!LandingPlatformHelpers.IsLandingPlatformTile(map,
                MapCoordinateHelpers.WorldXToTileIndex(position.x, map),
                MapCoordinateHelpers.WorldZToTileIndex(position.z, map), bufferTiles: 2))
                return position;
        }

        throw new InvalidOperationException("Could not place AttackShip outside the landing platform.");
    }
}
