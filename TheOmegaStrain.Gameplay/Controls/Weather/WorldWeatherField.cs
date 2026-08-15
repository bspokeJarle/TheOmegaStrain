using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;

namespace TheOmegaStrain.Gameplay.Controls.Weather
{
    public sealed class WorldWeatherField
    {
        private readonly Random _random;
        private readonly WeatherFieldSettings _settings;
        private bool _hasLastMapPosition;
        private bool _isMoving;
        private bool _hasTravelDirection;
        private float _lastMapX;
        private float _lastMapZ;
        private float _travelX;
        private float _travelZ;
        private int _spawnSequence;

        public WorldWeatherField(Random random, WeatherFieldSettings settings)
        {
            _random = random;
            _settings = settings;
        }

        public bool HasTravelDirection => _hasTravelDirection;
        public float HalfVisibleSpread => ScreenSetup.screenSizeX * _settings.VisibleSpreadScreenMultiplier;
        public float HalfWorldSpread => ScreenSetup.screenSizeX * _settings.WorldSpreadScreenMultiplier;

        public void UpdateTravelDirection(IVector3 mapPosition)
        {
            if (!_hasLastMapPosition)
            {
                _lastMapX = mapPosition.x;
                _lastMapZ = mapPosition.z;
                _hasLastMapPosition = true;
                return;
            }

            float dx = mapPosition.x - _lastMapX;
            float dz = mapPosition.z - _lastMapZ;
            _lastMapX = mapPosition.x;
            _lastMapZ = mapPosition.z;

            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= 0.05f)
            {
                _isMoving = false;
                return;
            }

            _travelX = dx / distance;
            _travelZ = dz / distance;
            _isMoving = true;
            _hasTravelDirection = true;
        }

        public bool ShouldSpawnAhead()
        {
            if (!_isMoving || !_hasTravelDirection)
                return false;

            _spawnSequence++;
            return _spawnSequence % _settings.DirectionalSpawnModulo != 0;
        }

        public bool ShouldRecycle(float worldX, float worldZ, IVector3 mapPosition)
        {
            float relativeX = worldX - mapPosition.x;
            float relativeZ = worldZ - mapPosition.z;
            float halfWorldSpread = HalfWorldSpread;

            if (MathF.Abs(relativeX) > halfWorldSpread + _settings.OffscreenMargin)
                return true;
            if (relativeZ < -_settings.BehindSpread - _settings.OffscreenMargin)
                return true;
            if (relativeZ > _settings.MaxDepthZ + _settings.OffscreenMargin)
                return true;

            if (!_isMoving || !_hasTravelDirection)
                return false;

            float alongTravel = relativeX * _travelX + relativeZ * _travelZ;
            float lateral = MathF.Abs(relativeX * -_travelZ + relativeZ * _travelX);

            return alongTravel < -_settings.TravelBehindRecycleDistance
                || alongTravel > _settings.TravelAheadRecycleDistance
                || lateral > halfWorldSpread + _settings.OffscreenMargin;
        }

        public (float x, float z) GetAmbientSpawnOffset(bool preferOutside)
        {
            float halfWorldSpread = HalfWorldSpread;
            if (preferOutside && _random.NextDouble() < _settings.OutsideSpawnChance)
            {
                if (_random.NextDouble() < 0.5d)
                {
                    float sign = _random.NextDouble() < 0.5d ? -1f : 1f;
                    return (
                        sign * RandomRange(HalfVisibleSpread, halfWorldSpread),
                        RandomRange(-_settings.BehindSpread, _settings.MaxDepthZ));
                }

                float z = _random.NextDouble() < 0.5d
                    ? RandomRange(-_settings.BehindSpread, _settings.DepthStartZ)
                    : RandomRange(_settings.VisibleDepthEndZ, _settings.MaxDepthZ);

                return (RandomRange(-halfWorldSpread, halfWorldSpread), z);
            }

            return (
                RandomRange(-HalfVisibleSpread, HalfVisibleSpread),
                RandomRange(_settings.DepthStartZ, _settings.VisibleDepthEndZ));
        }

        public (float x, float z) GetDirectionalSpawnOffset()
        {
            float forward = RandomRange(_settings.DirectionalSpawnAheadMin, _settings.DirectionalSpawnAheadMax);
            float lateral = RandomRange(
                -HalfWorldSpread * _settings.DirectionalLateralSpreadFactor,
                HalfWorldSpread * _settings.DirectionalLateralSpreadFactor);

            float x = _travelX * forward + -_travelZ * lateral;
            float z = _travelZ * forward + _travelX * lateral;

            return (
                x,
                Math.Clamp(z, -_settings.BehindSpread, _settings.MaxDepthZ));
        }

        public void Reset()
        {
            _hasLastMapPosition = false;
            _isMoving = false;
            _hasTravelDirection = false;
            _spawnSequence = 0;
        }

        public static void SyncEmitterToGround(I3dObject theObject, IVector3 mapPosition)
        {
            var offsets = theObject.ObjectOffsets ?? new Vector3();
            theObject.ObjectOffsets = new Vector3
            {
                x = offsets.x,
                y = mapPosition.y * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY,
                z = offsets.z
            };
        }

        public static float GetProjectionScale(float z, float objectZ)
        {
            float denominator = -z + objectZ + ScreenSetup.perspectiveAdjustment;
            if (denominator <= 1f)
                denominator = 1f;

            return ScreenSetup.perspectiveAdjustment / denominator * ScreenSetup.defaultObjectZoom;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}
