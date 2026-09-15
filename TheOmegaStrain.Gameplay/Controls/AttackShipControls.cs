using System;
using System.Collections.Generic;
using System.Linq;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Ranged pursuit: hold a firing ring around Ship, launch one rocket, then reload
    /// after cleanup. Passes self-reference to rockets to enable turnaround attacks.
    /// </summary>
    public sealed class AttackShipControls : IObjectMovement
    {
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 3;
        private const float SecondsPerScreenCrossing = 3f;
        private const float MaximumMovementDeltaSeconds = 0.1f;
        private bool _hadActiveRocket;
        private DateTime? _lastRocketRemovedUtc;

        public float RocketReloadDelaySeconds { get; set; } = EnemySetup.AttackShipRocketReloadDelaySeconds;
        private ITriangleMeshWithColorAndTexture? _weaponStartGuide;
        private ITriangleMeshWithColorAndTexture? _weaponDirectionGuide;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; } = null!;
        public IPhysics Physics { get; set; } = new Physics.Physics();
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private bool _isExploding;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private Vector3? _explosionRotation;

        private float? _initialOffsetY;
        private DateTime? _lastMovementTime;

        private int _framesSinceRelease;
        private readonly OmegaMeshRotation _rotate = new();
        private Vector3? _trackedWorldPosition;

        public Vector3? ShipTargetWorldPosition { get; private set; }
        public Vector3? DirectionToShip { get; private set; }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            ConfigureAudio(audioPlayer, soundRegistry);

            theObject.WeaponSystems?.MoveWeapon(audioPlayer, soundRegistry);

            if (!_isExploding && theObject.ImpactStatus?.HasCrashed == true)
                HandleCrash(theObject);

            if (_isExploding)
            {
                theObject.WorldPosition = KamikazeDroneMovementHelpers.ToVector3(_explosionWorldPosition);
                theObject.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(_explosionObjectOffsets);
                theObject.Rotation = KamikazeDroneMovementHelpers.ToVector3(_explosionRotation);
                theObject.CrashBoxes = new List<List<IVector3>>();
                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                    theObject.ObjectParts = new List<I3dObjectPart>();
                return theObject;
            }

            var now = DateTime.Now;
            float deltaSeconds = GetMovementDeltaSeconds(now);
            RestoreMovementState(theObject);
            PursueShip(theObject, deltaSeconds);

            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject, WorldViewSetup.SurfacePitchDegrees);
            SyncAuthoritativeTransform(theObject);
            UpdateFire(theObject, DateTime.UtcNow);

            ReleaseParticles(theObject);
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            return theObject;
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null) return;

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.AttackShipHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => currentHealth,
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => currentHealth
            };

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            if (_audio != null && _explosionSound != null)
            {
                var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();
                _audio.Play(
                    _explosionSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }

            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);
            _explosionRotation = KamikazeDroneMovementHelpers.ToVector3(theObject.Rotation);

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, 200f);
            theObject.CrashBoxes = new List<List<IVector3>>();
            theObject.ImpactStatus.HasCrashed = false;
        }

        private float GetMovementDeltaSeconds(DateTime now)
        {
            float dt = _lastMovementTime.HasValue
                ? Math.Clamp((float)(now - _lastMovementTime.Value).TotalSeconds, 0f, MaximumMovementDeltaSeconds)
                : 0f;
            _lastMovementTime = now;
            return dt;
        }

        private void RestoreMovementState(I3dObject theObject)
        {
            _initialOffsetY ??= theObject.ObjectOffsets.y;

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(
                theObject, _initialOffsetY.Value);

            if (_trackedWorldPosition == null && theObject.WorldPosition != null)
                _trackedWorldPosition = new Vector3(
                    theObject.WorldPosition.x, theObject.WorldPosition.y, theObject.WorldPosition.z);
            if (_trackedWorldPosition != null)
                theObject.WorldPosition = new Vector3(
                    _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
        }

        private void PursueShip(I3dObject theObject, float deltaSeconds)
        {
            ShipTargetWorldPosition = GameState.ShipState?.ShipCrashCenterWorldPosition == null &&
                GameState.ShipState?.ShipWorldPosition == null
                ? null
                : SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(theObject);

            if (ShipTargetWorldPosition != null && theObject.IsOnScreen)
            {
                ShipTargetWorldPosition.y -= SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(
                    theObject, WorldViewSetup.SurfacePitchDegrees);
            }

            DirectionToShip = ShipTargetWorldPosition == null
                ? null
                : MovementHelpers.GetVectorToTarget(
                    KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                    ShipTargetWorldPosition);

            if (ShipTargetWorldPosition != null && _trackedWorldPosition != null)
            {
                var position = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                var toShipHorizontal = new Vector3(DirectionToShip!.x, 0f, DirectionToShip.z);
                var horizontalLength = MovementHelpers.GetLength(toShipHorizontal);
                var approachDirection = horizontalLength > 0.001f
                    ? KamikazeDroneMovementHelpers.ToVector3(VectorMath.Normalize(toShipHorizontal))
                    : new Vector3(1f, 0f, 0f);

                float firingDistance = EnemySetup.AttackShipFiringDistance * ScreenSetup.ScreenScaleX;
                var station = MovementHelpers.MoveAlongDirection(
                    ShipTargetWorldPosition, approachDirection, -firingDistance);
                var stationDistance = MovementHelpers.GetDirectionAndDistanceWorld(position, station).Length;
                float throttle = stationDistance <= EnemySetup.AttackShipPositionTolerance ? 0f :
                    Math.Clamp(stationDistance / EnemySetup.AttackShipApproachSlowdownDistance, 0f, 1f);

                var velocity = MovementHelpers.GetVelocityTowardsTarget(position, station,
                    MovementHelpers.GetScreenCrossingSpeed(SecondsPerScreenCrossing) * throttle);
                var step = MovementHelpers.GetPursuitStep(position, station, velocity, deltaSeconds);

                if (step.HasMovement)
                {
                    _trackedWorldPosition = MovementHelpers.MoveAlongDirection(
                        _trackedWorldPosition, step.MovementDirection,
                        MathF.Min(step.MoveDistance, step.DistanceToTarget));
                    theObject.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                }

                var heading = MovementHelpers.GetHeadingFromMovementDirection(DirectionToShip);
                theObject.Rotation = new Vector3(heading.X, heading.Y, heading.Z);
            }
        }

        private void SyncAuthoritativeTransform(I3dObject theObject)
        {
            foreach (var original in GameState.SurfaceState.AiObjects)
            {
                if (original.ObjectId == theObject.ObjectId && _trackedWorldPosition != null)
                {
                    if (ReferenceEquals(original, theObject)) return;
                    original.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    original.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);
                    original.Rotation = KamikazeDroneMovementHelpers.ToVector3(theObject.Rotation);
                    return;
                }
            }
        }

        public void Dispose()
        {
            _audio = null;
            _explosionSound = null;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audio != null || audioPlayer == null || soundRegistry == null) return;
            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (++_framesSinceRelease < FramesBetweenReleases)
                return;

            var leftStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineStartGuide");
            var leftGuide = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineDirectionGuide");
            var rightStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineStartGuide");
            var rightGuide = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineDirectionGuide");

            bool leftReady = leftStart != null && leftGuide != null;
            bool rightReady = rightStart != null && rightGuide != null;
            if (!leftReady && !rightReady)
                return;

            _framesSinceRelease = 0;

            var worldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };

            if (leftReady)
                theObject.Particles?.ReleaseParticles(
                    leftGuide!, leftStart!, worldPosition, this, ParticleThrust, null);

            if (rightReady)
                theObject.Particles?.ReleaseParticles(
                    rightGuide!, rightStart!, worldPosition, this, ParticleThrust, null);
        }

        private ITriangleMeshWithColorAndTexture? GetCurrentFrameRotatedGuide(I3dObject theObject, string partName)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part?.Triangles == null || part.Triangles.Count == 0)
                return null;

            var rotation = theObject.Rotation ?? new Vector3();
            var mesh = new List<ITriangleMeshWithColorAndTexture>
            {
                OmegaObjectHelpers.CopyTriangle(part.Triangles[0])
            };

            mesh = _rotate.RotateZMesh(mesh, rotation.z);
            mesh = _rotate.RotateYMesh(mesh, rotation.y);
            mesh = _rotate.RotateXMesh(mesh, rotation.x);

            return mesh[0];
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) RearEngineStartCoordinates = StartCoord;
            if (GuideCoord != null) RearEngineGuideCoordinates = GuideCoord;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) _weaponStartGuide = StartCoord;
            if (GuideCoord != null) _weaponDirectionGuide = GuideCoord;
        }

        private void UpdateFire(I3dObject theObject, DateTime nowUtc)
        {
            var weapons = theObject.WeaponSystems;
            if (weapons == null) return;

            int activeRockets = weapons.ActiveWeapons.Count(w => w is ActiveWeapon active && active.WeaponType == WeaponType.Rocket);

            if (_hadActiveRocket && activeRockets == 0)
                _lastRocketRemovedUtc = nowUtc;
            _hadActiveRocket = activeRockets > 0;

            if (!theObject.IsOnScreen || !theObject.IsActive || theObject.WorldPosition == null || GameState.ShipState?.ShipWorldPosition == null)
                return;

            float distanceToShip = MovementHelpers.GetDirectionAndDistanceWorld(
                KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(theObject)).Length;

            if (distanceToShip > WeaponSetup.RocketMaxRange) return;

            float elapsed = _lastRocketRemovedUtc.HasValue ? (float)(nowUtc - _lastRocketRemovedUtc.Value).TotalSeconds : float.PositiveInfinity;
            if (!RocketFireHelpers.CanFireAfterReload(theObject.IsOnScreen, elapsed, RocketReloadDelaySeconds, activeRockets))
                return;

            var leftStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineStartGuide");
            var leftDir = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineDirectionGuide");
            var rightStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineStartGuide");
            var rightDir = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineDirectionGuide");

            int activeBefore = weapons.ActiveWeapons.Count;

            if (leftStart != null && leftDir != null)
                weapons.FireWeapon(leftDir.vert1, leftStart.vert1, theObject.WorldPosition, WeaponType.Rocket, theObject, 0);

            if (rightStart != null && rightDir != null)
                weapons.FireWeapon(rightDir.vert1, rightStart.vert1, theObject.WorldPosition, WeaponType.Rocket, theObject, 1);

            if (weapons.ActiveWeapons.Count > activeBefore)
            {
                _hadActiveRocket = true;

                // Pass reference of this AttackShip to newly spawned rockets
                foreach (var weapon in weapons.ActiveWeapons)
                {
                    if (weapon is ActiveWeapon activeWpn && activeWpn.MovementController is RocketControls rocketControls)
                    {
                        rocketControls.TargetShip ??= theObject;
                    }
                }
            }
        }
    }
}