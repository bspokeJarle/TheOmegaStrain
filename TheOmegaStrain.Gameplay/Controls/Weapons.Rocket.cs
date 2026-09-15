using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Gameplay.Controls
{
    // Rocket orchestration stays in Weapons; this file follows launch -> flight -> explosion.
    public partial class Weapons
    {
        private readonly List<ActiveWeapon> _explodingRockets = new();
        private SoundDefinition? _rocketExplosionSound;

        private void LaunchRocket(IVector3 trajectory, IVector3 startPosition, IVector3 worldPosition, I3dObject parentShip)
        {
            if (FireAsEnemyWeapon && !parentShip.IsOnScreen)
                return;

            var template = _weaponObjects.Find(w => w.ObjectName == "Rocket");
            if (template == null)
                return;

            var shipOffsets = parentShip.ObjectOffsets ?? new Vector3();
            var rocketStart = Add(startPosition, shipOffsets);
            // Initial aim. Enemy rockets may retarget until they enter the final approach.
            // Movement below changes ObjectOffsets, so convert only the world direction's Z.
            IVector3 direction;
            bool guidanceLocked = false;
            if (FireAsEnemyWeapon)
            {
                if (GameState.ShipState?.ShipWorldPosition == null || GameState.ShipState.ShipObjectOffsets == null)
                    return;
                var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(rocketStart);
                var launch = RocketFlightHelpers.CalculateLaunchSolution(ToVector3(worldPosition), target);
                direction = new Vector3(launch.Direction.x, launch.Direction.y, -launch.Direction.z);
                guidanceLocked = launch.DistanceToShip <= WeaponSetup.RocketGuidanceLockDistance;
            }
            else
            {
                direction = Normalize(ToVector3(VectorMath.Subtract(trajectory, startPosition)));
            }
            if (Magnitude(direction) <= 0.00001f)
                return;

            var instance = OmegaObjectHelpers.DeepCopySingleObject(template);
            instance.ObjectId = GameState.ObjectIdCounter++;
            instance.ObjectName = FireAsEnemyWeapon ? "EnemyRocket" : "Rocket";
            instance.ImpactStatus = new ImpactStatus { ObjectHealth = 100 };
            var rotation = GetRocketRotation(direction);
            instance.Rotation = rotation;
            SetObjectOffsets(instance, rocketStart);
            SetWorldPosition(instance, ToVector3(worldPosition));
            InitializeWeaponGeometry(instance, rotation, 0);

            // Frame copies share Weapons. Every launch gets independent physics and exhaust.
            var physics = new Physics.Physics
            {
                Velocity = Scale(direction, WeaponSetup.RocketVelocity),
                GravityStrength = WeaponSetup.RocketGravityStrength,
                Friction = 0f,
                Thrust = 0f
            };
            instance.Movement = new RocketControls { Physics = physics, ParentObject = instance };
            // Reuse AttackShip's warm exhaust palette and gravity tuning.
            instance.Particles = new ParticlesAI
            {
                GravityStrength = 34f,
                ColorStartOverride = "fff8c8",
                ColorMidOverride = "ff8a20",
                ColorEndOverride = "5a1800",
                ExplosionStartYOffset = 0f
            };

            var firedUtc = DateTime.UtcNow;
            var activeRocket = new ActiveWeapon
            {
                WeaponType = WeaponType.Rocket,
                WeaponObject = instance,
                FiredTime = firedUtc,
                LastUpdateUtc = firedUtc,
                Velocity = WeaponSetup.RocketVelocity,
                Trajectory = direction,
                RocketGuidanceLocked = guidanceLocked,
                RocketState = new RocketFlightState { Physics = physics },
                // Impact ends flight; a spent rocket also explodes outside screen bounds.
                // Travel distance and launch age do not stop the motor.
                MaxRange = float.PositiveInfinity,
                LifetimeSeconds = double.PositiveInfinity
            };
            ActiveWeapons.Add(activeRocket);
        }

        private static Vector3 GetRocketRotation(IVector3 direction)
        {
            // Rocket's nose is +X; use the same Z -> Y rotation for launch and retargeting.
            float depthHeading = GeometryMath.GetHeadingFromDirection(direction.x, direction.z, 0f).Z;
            float sideLength = VectorMath.Length(new Vector3(direction.x, 0f, direction.z));
            float verticalHeading = GeometryMath.GetHeadingFromDirection(sideLength, direction.y, 0f).Z;
            return new Vector3(0f, -depthHeading, verticalHeading);
        }

        private void UpdateRocketGuidance(ActiveWeapon weapon, bool allowRetarget)
        {
            if (weapon.WeaponType != WeaponType.Rocket || weapon.WeaponObject.ObjectName != "EnemyRocket" ||
                weapon.RocketGuidanceLocked || GameState.ShipState?.ShipWorldPosition == null ||
                GameState.ShipState.ShipObjectOffsets == null)
                return;

            // Reuse launch-space conversion with the rocket's CURRENT offset, not its owner's muzzle.
            var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(weapon.WeaponObject.ObjectOffsets);
            var aim = RocketFlightHelpers.CalculateLaunchSolution(ToVector3(weapon.WeaponObject.WorldPosition), target);
            if (aim.DistanceToShip <= WeaponSetup.RocketGuidanceLockDistance)
            {
                weapon.RocketGuidanceLocked = true;
                return;
            }
            if (!allowRetarget)
                return;

            var direction = new Vector3(aim.Direction.x, aim.Direction.y, -aim.Direction.z);
            SetRocketDirection(weapon, direction);
        }

        private void SetRocketDirection(ActiveWeapon weapon, IVector3 direction)
        {
            if (Magnitude(VectorMath.Subtract(direction, weapon.Trajectory)) <= 0.00001f)
                return;
            var template = _weaponObjects.Find(w => w.ObjectName == "Rocket");
            if (template == null)
                return;

            // Weapons arrive at the renderer already rotated. Rebuild from an unrotated copy,
            // not last frame's geometry, and preserve this projectile's identity and impact state.
            var geometry = OmegaObjectHelpers.DeepCopySingleObject(template);
            var rotation = GetRocketRotation(direction);
            InitializeWeaponGeometry(geometry, rotation, 0);
            weapon.WeaponObject.ObjectParts = geometry.ObjectParts;
            weapon.WeaponObject.CrashBoxes = geometry.CrashBoxes;
            weapon.WeaponObject.Rotation = rotation;
            weapon.Trajectory = direction;
        }

        private void UpdateRocket(ActiveWeapon rocket, DateTime now)
        {
            if (rocket.RocketState == null) return;
            if (rocket.WeaponObject.ImpactStatus.HasCrashed)
            {
                StartRocketExplosion(rocket);
                return;
            }

            float dt = rocket.LastUpdateUtc == default ? 0f : (float)(now - rocket.LastUpdateUtc).TotalSeconds;
            rocket.LastUpdateUtc = now;
            float stepSeconds = FrameTimingMath.ClampDeltaTime(dt, fallbackDeltaTime: 0f);
            MoveRocket(rocket, stepSeconds);
        }

        private void MoveRocket(ActiveWeapon rocket, float deltaSeconds)
        {
            // Split the frame at fuel depletion so no frame gets both a full powered
            // step and a full gravity step. Guidance never resumes after the transition.
            var state = rocket.RocketState!;
            float poweredSeconds = 0f;
            if (!state.IsFalling)
            {
                float fuelRemaining = MathF.Max(0f, WeaponSetup.RocketFuelSeconds - state.PoweredSeconds);
                poweredSeconds = MathF.Min(deltaSeconds, fuelRemaining);
                if (poweredSeconds > 0f)
                    MovePoweredRocket(rocket, poweredSeconds);

                // Only elapsed motor time stops thrust. Visibility and distance do not.
                if (!RocketFlightHelpers.HasFuel(state.PoweredSeconds, WeaponSetup.RocketFuelSeconds))
                    BeginRocketFall(rocket);
            }

            float fallingSeconds = deltaSeconds - poweredSeconds;
            if (state.IsFalling && fallingSeconds > 0f)
                MoveFallingRocket(rocket, fallingSeconds);

            // A missed rocket must not occupy its owner's launch slot forever.
            // Inside bounds it keeps falling; outside, only a spent rocket detonates.
            if (state.IsFalling && IsRocketOutsideScreenBounds(rocket))
            {
                StartRocketExplosion(rocket);
                return;
            }

            if (rocket.WeaponObject.Movement is RocketControls controls)
                controls.UpdateWeaponParticles(rocket.WeaponObject, deltaSeconds, !state.IsFalling);
        }

        private static bool IsRocketOutsideScreenBounds(ActiveWeapon rocket)
        {
            if (rocket.WeaponObject is not OmegaObject3D obj)
                return false;

            // Same world/local conversion as rendering, centred at zero for screen bounds.
            // Includes map scrolling and the rocket's travel since its world-space launch.
            // Keep the existing X/Y cleanup margin, but not the laser's Z limits:
            // depth changes perspective size and does not mean the rocket is off screen.
            return ObjectPlacementMath.TryGetRenderPosition(
                obj, obj.GetLocalWorldPosition(), null, 0, 0,
                static (x, y, z) => new Vector3(x, y, z),
                out double x, out double y, out _) &&
                (x < minX || x > maxX || y < minY || y > maxY);
        }

        private void MovePoweredRocket(ActiveWeapon rocket, float deltaSeconds)
        {
            UpdateRocketGuidance(rocket, allowRetarget: true);
            var travel = Scale(rocket.Trajectory, rocket.Velocity * deltaSeconds);
            SetRocketPosition(rocket, Add(rocket.WeaponObject.ObjectOffsets,
                Add(travel, Scale(ParentVelocityLocal, deltaSeconds))));
            rocket.DistanceTraveled += Magnitude(travel);
            rocket.RocketState!.PoweredSeconds = MathF.Min(WeaponSetup.RocketFuelSeconds,
                rocket.RocketState.PoweredSeconds + deltaSeconds);
            // Lock on the frame we enter the last 100 units, before Ship can move again.
            UpdateRocketGuidance(rocket, allowRetarget: false);
        }

        private void BeginRocketFall(ActiveWeapon rocket)
        {
            var state = rocket.RocketState!;
            if (state.IsFalling) return;

            state.IsFalling = true;
            rocket.RocketGuidanceLocked = true;
            state.Physics.Thrust = 0f;
            // Seed momentum ONCE. Physics owns velocity and gravity from this point onward.
            state.Physics.Velocity = Add(Scale(rocket.Trajectory, rocket.Velocity), ParentVelocityLocal);
        }

        private void MoveFallingRocket(ActiveWeapon rocket, float deltaSeconds)
        {
            var physics = rocket.RocketState!.Physics;
            // Bound the existing physics integrator's steps at the gameplay baseline.
            // This also keeps a slow frame from producing a disproportionately large fall.
            float remaining = deltaSeconds;
            while (remaining > 0f)
            {
                float step = MathF.Min(remaining, GameState.GameplayBaselineDeltaTime);
                var before = rocket.WeaponObject.ObjectOffsets;
                var after = physics.ApplyForces(before, step);
                SetRocketPosition(rocket, after);
                rocket.DistanceTraveled += Magnitude(VectorMath.Subtract(after, before));
                remaining = MathF.Max(0f, remaining - step);
            }

            SetRocketDirection(rocket, Normalize(physics.Velocity));
        }

        private static void SetRocketPosition(ActiveWeapon rocket, IVector3 position)
        {
            var obj = rocket.WeaponObject;
            var delta = VectorMath.Subtract(position, obj.ObjectOffsets);
            // ParticleManager adds the emitter's CURRENT offset. Preserve old exhaust's
            // birth position when this projectile moves through offsets instead of WorldPosition.
            if (obj.Particles != null)
            {
                lock (obj.Particles)
                {
                    foreach (var particle in obj.Particles.Particles)
                        particle.Position = ToVector3(VectorMath.Subtract(particle.Position, delta));
                }
            }
            SetObjectOffsets(obj, position);
        }

        private void StartRocketExplosion(ActiveWeapon rocket)
        {
            var state = rocket.RocketState!;
            if (state.IsExploding) return;
            state.IsExploding = true;
            state.IsFalling = true;
            state.Physics.Thrust = 0f;
            state.ExplosionStarted = DateTime.Now;

            var obj = rocket.WeaponObject;
            obj.ImpactStatus.HasCrashed = true;
            // Guides are authoring aids, not exploding pieces of the hull.
            obj.ObjectParts.RemoveAll(part => part.PartName?.Contains("Guide", StringComparison.OrdinalIgnoreCase) == true);
            ExplosionParticleHelpers.ReleaseExplosionParticles(obj, obj.Movement!);
            state.Physics.ExplodeObject(obj, 200f);
            obj.CrashBoxes = new List<List<IVector3>>();

            if (_audio != null && _rocketExplosionSound != null)
            {
                var position = ((OmegaObject3D)obj).GetAudioPosition();
                _audio.Play(_rocketExplosionSound, AudioPlayMode.OneShot, new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(position.x, position.y, position.z)
                });
            }

            // Free the launch slot now, but retain the visual effect until it has finished.
            _explodingRockets.Add(rocket);
        }

        private void UpdateRocketExplosions()
        {
            for (int i = _explodingRockets.Count - 1; i >= 0; i--)
            {
                var rocket = _explodingRockets[i];
                var obj = rocket.WeaponObject;
                if (!obj.ImpactStatus.HasExploded)
                    rocket.RocketState!.Physics.UpdateExplosion(obj, rocket.RocketState.ExplosionStarted);
                ExplosionParticleHelpers.MoveParticles(obj);
                if (!obj.ImpactStatus.HasExploded) continue;

                obj.ObjectParts.Clear();
                if (obj.Particles?.Particles.Count > 0) continue;
                _explodingRockets.RemoveAt(i);
            }
        }
    }
}
