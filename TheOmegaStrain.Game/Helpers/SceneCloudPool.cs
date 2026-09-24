using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Game.Helpers;

// A fixed set of clouds follows the viewport by recycling only far-edge slots.
// Each slot owns a grid residue, so clouds already in view never drift or jump.
public sealed class SceneCloudPool
{
    public const int ForestGridSize = 10;
    public const int DesertGridSize = 6;
    public const int ForestSpacingXTiles = 62;
    public const int ForestSpacingZTiles = 48;
    private const float PlatformClearance = 2f * Cloud.MaximumWidth;

    private readonly List<OmegaObject3D> _clouds;
    private readonly int _gridSize;
    private readonly int _spacingXTiles;
    private readonly int _spacingZTiles;
    private readonly float _platformX;
    private readonly float _platformZ;
    private int _lastCellX = int.MinValue;
    private int _lastCellZ = int.MinValue;

    private SceneCloudPool(List<OmegaObject3D> clouds, int gridSize,
        int spacingXTiles, int spacingZTiles, float platformX, float platformZ)
    {
        _clouds = clouds;
        _gridSize = gridSize;
        _spacingXTiles = spacingXTiles;
        _spacingZTiles = spacingZTiles;
        _platformX = platformX;
        _platformZ = platformZ;
    }

    public static SceneCloudPool? AddClouds(I3dWorld world, ISurface? surface, SceneBiomeTypes biome)
    {
        if (surface == null)
            return null;

        int gridSize = biome == SceneBiomeTypes.Desert ? DesertGridSize : ForestGridSize;
        int spacingX = ForestSpacingXTiles * ForestGridSize / gridSize;
        int spacingZ = ForestSpacingZTiles * ForestGridSize / gridSize;
        var viewport = GameState.SurfaceState.GlobalMapPosition;
        float centreOffset = MapSetup.viewPortCenterOffsetX;
        var random = new Random(3033);
        var clouds = new List<OmegaObject3D>(gridSize * gridSize);
        for (int row = 0; row < gridSize; row++)
        {
            for (int column = 0; column < gridSize; column++)
            {
                var variant = (CloudVariant)((row * gridSize + column) % 3);
                var cloud = Cloud.CreateCloud(surface, variant, biome);
                cloud.ObjectOffsets = new Vector3(0f,
                    -(220f + random.Next(231)) * 1.21f * ScreenSetup.ScreenScaleY, 400f);
                cloud.Movement = new CloudControls();
                clouds.Add(cloud);
                world.WorldInhabitants.Add(cloud);
            }
        }

        var pool = new SceneCloudPool(clouds, gridSize, spacingX, spacingZ,
            viewport.x + centreOffset, viewport.z + centreOffset);
        pool.Update(viewport);
        return pool;
    }

    public void Update(IVector3 viewport)
    {
        int tileSize = SurfaceSetup.tileSize;
        int centreX = (int)MathF.Floor((viewport.x + MapSetup.viewPortCenterOffsetX - _platformX)
            / (tileSize * _spacingXTiles));
        int centreZ = (int)MathF.Floor((viewport.z + MapSetup.viewPortCenterOffsetX - _platformZ)
            / (tileSize * _spacingZTiles));
        if (centreX == _lastCellX && centreZ == _lastCellZ)
            return;

        _lastCellX = centreX;
        _lastCellZ = centreZ;
        int firstX = centreX - _gridSize / 2;
        int firstZ = centreZ - _gridSize / 2;
        for (int row = 0; row < _gridSize; row++)
        {
            int cellZ = firstZ + PositiveMod(row - firstZ, _gridSize);
            for (int column = 0; column < _gridSize; column++)
            {
                int cellX = firstX + PositiveMod(column - firstX, _gridSize);
                var cloud = _clouds[row * _gridSize + column];
                // Offset Z keeps one sky in the opening view without placing it over the platform.
                float x = _platformX + (cellX * _spacingXTiles
                    + CellJitter(cellX, cellZ, 0)) * tileSize;
                float z = _platformZ + ((cellZ - 0.25f) * _spacingZTiles
                    + CellJitter(cellX, cellZ, 1)) * tileSize;
                if (cloud.WorldPosition.x != x || cloud.WorldPosition.z != z)
                    cloud.WorldPosition = new Vector3(x, 0f, z);

                float dx = x - _platformX;
                float dz = z - _platformZ;
                cloud.IsActive = dx * dx + dz * dz > PlatformClearance * PlatformClearance;
            }
        }
    }

    private static int PositiveMod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private static float CellJitter(int cellX, int cellZ, int axis)
    {
        var random = new Random(unchecked(cellX * 73856093 ^ cellZ * 19349663 ^ axis * 83492791 ^ 3033));
        return (float)(random.NextDouble() * 2d - 1d);
    }
}
