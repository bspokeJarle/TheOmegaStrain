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
    /// Ranged pursuit: curve in toward Ship with an eased turn, hold a firing ring,
    /// launch one rocket, then reload after cleanup. This controller survives the
    /// render loop's per-frame object copies.
    /// </summary>
    public class AttackShipControls : IObjectMovement
    {
        private readonly OmegaMeshRotation _rotate = new();

        internal static Vector3 ToVector3(IVector3? v)
        {
            if (v is null)
            {
                return new Vector3();
            }

            return new Vector3
            {
                x = v.x,
                y = v.y,
                z = v.z
            };
        }

        // How fast the ship swings toward its heading; lower = lazier turns.
        private const float TurnSpeedDegreesPerSecond = 50f;
        // Fraction of speed retained when facing 90+ degrees away from the target,
        // so the ship slows through a hard turn but never fully stalls.
        private const float MinTurnThrottle = 0.15f;
        private const float MaximumMovementDeltaSeconds = 0.1f;
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 10;

        private const int IdealFiringDistance = 500;
        // Distance outside the firing ring over which the ship eases from full speed
        // down to a stop, so it coasts in and holds station instead of overshooting.
        private const float ApproachSlowdownBand = 400f;
        private const float SecondsPerScreenCrossing = 2f;

        public Vector3? ShipTargetWorldPosition { get; private set; }
        public Vector3? DirectionToShip { get; private set; }
        private float? _initialOffsetY;
        private DateTime? _lastMovementTime;
        private int _framesSinceRelease;
        private Vector3? _trackedWorldPosition;
        private Vector3? _trackedRotation;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; } = null!;
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // Weapon / reload state.
        private bool _hadActiveRocket;
        private DateTime? _lastRocketRemovedUtc;
        /// <summary>Seconds to wait after the previous rocket leaves ActiveWeapons.</summary>
        public float RocketReloadDelaySeconds { get; set; } = EnemySetup.AttackShipRocketReloadDelaySeconds;
        private ITriangleMeshWithColorAndTexture? _weaponStartGuide;
        private ITriangleMeshWithColorAndTexture? _weaponDirectionGuide;

        // Explosion / crash state.
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private bool _isExploding;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private Vector3? _explosionRotation;

        private float GetMovementDeltaSeconds(DateTime now)
        {
            // Off-screen AI updates are less frequent; measure elapsed time locally.
            float dt = _lastMovementTime.HasValue
                ? Math.Clamp((float)(now - _lastMovementTime.Value).TotalSeconds, 0f, MaximumMovementDeltaSeconds)
                : 0f;
            _lastMovementTime = now;
            return dt;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            ConfigureAudio(audioPlayer, soundRegistry);
            // Existing projectiles must still advance/clean up while their owner explodes.
            theObject.WeaponSystems?.MoveWeapon(audioPlayer, soundRegistry);
            // Handle the collision transform before navigation can move the object.
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
            var deltaSeconds = GetMovementDeltaSeconds(now);
            RestoreMovementState(theObject);
            PursueShip(theObject, deltaSeconds);
            SyncAuthoritativePosition(theObject);

            // Apply the view correction after navigation and before emitting exhaust.
            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject, WorldViewSetup.SurfacePitchDegrees);
            UpdateFire(theObject, DateTime.UtcNow);

            ReleaseParticles(theObject);
            if(theObject.Particles?.Particles.Count > 0)
            {
                theObject.Particles.MoveParticles();
            }

            return theObject;
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
            {
                return;
            }

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

        private void RestoreMovementState(I3dObject theObject)
        {
            _initialOffsetY ??= theObject.ObjectOffsets.y;

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(
                theObject, _initialOffsetY.Value);
            // Capture the scene's placement once, as the other movement controllers do.
            if (_trackedWorldPosition == null && theObject.WorldPosition != null)
                _trackedWorldPosition = new Vector3(
                    theObject.WorldPosition.x, theObject.WorldPosition.y, theObject.WorldPosition.z);
            if (_trackedWorldPosition != null)
                theObject.WorldPosition = new Vector3(
                    _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);

            // Persist rotation across frames so the eased turn continues where it left off.
            if (_trackedRotation == null && theObject.Rotation is Vector3 startRotation)
                _trackedRotation = new Vector3(startRotation.x, startRotation.y, startRotation.z);
            if (_trackedRotation != null)
                theObject.Rotation = new Vector3(
                    _trackedRotation.x, _trackedRotation.y, _trackedRotation.z);
        }

        private void PursueShip(I3dObject theObject, float deltaSeconds)
        {
            // Use the Drone's converted Ship crash centre, including its fallback
            // to ShipWorldPosition. Raw ShipState coordinates use a different origin.
            ShipTargetWorldPosition = KamikazeDroneMovementHelpers.GetShipCrashCenterWorldPosition();
            DirectionToShip = ShipTargetWorldPosition == null
                ? null
                : MovementHelpers.GetVectorToTarget(
                    KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject),
                    ShipTargetWorldPosition);

            if (ShipTargetWorldPosition != null && _trackedWorldPosition != null)
            {
                var centre = KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject);
                var velocity = MovementHelpers.GetVelocityTowardsTarget(centre, ShipTargetWorldPosition,
                    MovementHelpers.GetScreenCrossingSpeed(SecondsPerScreenCrossing));
                var step = MovementHelpers.GetPursuitStep(centre, ShipTargetWorldPosition, velocity, deltaSeconds);
                if (step.HasMovement)
                {
                    // Ease the rotation toward the target heading first, then fly along
                    // whichever way the ship is now actually pointing so it curves in.
                    var heading = MovementHelpers.GetHeadingFromMovementDirection(step.MovementDirection);
                    var current = theObject.Rotation as Vector3 ?? new Vector3();
                    var eased = MovementHelpers.MoveRotationTowards(
                        current.x, current.y, current.z,
                        heading.X, heading.Y, heading.Z,
                        TurnSpeedDegreesPerSecond,
                        deltaSeconds);
                    _trackedRotation = new Vector3(eased.X, eased.Y, eased.Z);
                    theObject.Rotation = new Vector3(eased.X, eased.Y, eased.Z);

                    // Move forward along the current heading. Slow down mid-turn: the more
                    // the heading is off from the target, the shorter the step.
                    var forward = MovementHelpers.GetForwardDirectionFromHeading(_trackedRotation);
                    var turnThrottle = MathF.Max(
                        MovementHelpers.Dot(forward, step.MovementDirection), MinTurnThrottle);

                    // Hold station at the firing distance: ease speed to zero as the ship
                    // closes the gap to the ideal ring, and never step inside it.
                    var distanceToRing = MovementHelpers.GetLength(DirectionToShip!) - IdealFiringDistance;
                    var distanceThrottle = Math.Clamp(distanceToRing / ApproachSlowdownBand, 0f, 1f);
                    var maxStep = MathF.Max(distanceToRing, 0f);

                    _trackedWorldPosition = MovementHelpers.MoveAlongDirection(
                        _trackedWorldPosition, forward,
                        MathF.Min(step.MoveDistance * turnThrottle * distanceThrottle, maxStep));
                    theObject.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                }
            }

        }

        private void SyncAuthoritativePosition(I3dObject theObject)
        {
            // Keep the original used by visibility checks in sync with the moving copy.
            foreach (var original in GameState.SurfaceState.AiObjects)
                if (original.ObjectId == theObject.ObjectId)
                {
                    if (ReferenceEquals(original, theObject)) return;
                    if (_trackedWorldPosition != null)
                        original.WorldPosition = new Vector3(
                            _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    original.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);
                    if (_trackedRotation != null)
                        original.Rotation = new Vector3(
                            _trackedRotation.x, _trackedRotation.y, _trackedRotation.z);
                }
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
            // LiveGameLoop supplies start and direction in separate calls.
            if (StartCoord != null) _weaponStartGuide = StartCoord;
            if (GuideCoord != null) _weaponDirectionGuide = GuideCoord;
        }

        private void UpdateFire(I3dObject theObject, DateTime nowUtc)
        {
            var weapons = theObject.WeaponSystems;
            if (weapons == null) return;
            int activeRockets = weapons.ActiveWeapons.Count(w =>
                w is ActiveWeapon active && active.WeaponType == WeaponType.Rocket);
            // Track cleanup even while off-screen or missing guides. Do not restart the timer
            // on every empty frame. This controller is shared by the owner's render copies.
            if (_hadActiveRocket && activeRockets == 0)
                _lastRocketRemovedUtc = nowUtc;
            _hadActiveRocket = activeRockets > 0;

            if (!theObject.IsOnScreen || !theObject.IsActive || theObject.WorldPosition == null ||
                GameState.ShipState?.ShipWorldPosition == null)
                return;
            // Wait until LiveGameLoop has bound the pair, as MotherShipMedium does.
            if (_weaponStartGuide == null || _weaponDirectionGuide == null)
                return;

            // Use the same offset-compensated target as navigation, not raw Ship coordinates.
            float distanceToShip = MovementHelpers.GetDirectionAndDistanceWorld(
                KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(theObject)).Length;
            if (distanceToShip > WeaponSetup.RocketMaxRange)
                return;

            float elapsed = _lastRocketRemovedUtc.HasValue
                ? (float)(nowUtc - _lastRocketRemovedUtc.Value).TotalSeconds
                : float.PositiveInfinity;
            if (!RocketFireHelpers.CanFireAfterReload(theObject.IsOnScreen, elapsed,
                    RocketReloadDelaySeconds, activeRockets))
                return;

            // The loop binds guides after movement; use this frame's orientation at launch.
            _weaponStartGuide = GetCurrentFrameRotatedGuide(theObject, "WeaponStartGuide");
            _weaponDirectionGuide = GetCurrentFrameRotatedGuide(theObject, "WeaponDirectionGuide");
            if (_weaponStartGuide == null || _weaponDirectionGuide == null)
                return;

            int activeBefore = weapons.ActiveWeapons.Count;
            weapons.FireWeapon(_weaponDirectionGuide.vert1, _weaponStartGuide.vert1,
                theObject.WorldPosition, WeaponType.Rocket, theObject, 0);
            // Failed launches do not consume a reload; wait for a successful rocket's cleanup.
            if (weapons.ActiveWeapons.Count > activeBefore)
                _hadActiveRocket = true;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audio != null || audioPlayer == null || soundRegistry == null) return;
            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
        }

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
            _audio = null;
            _explosionSound = null;
        }
    }
}
