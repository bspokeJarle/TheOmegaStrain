using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;

namespace TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls
{
    // A drone that was just dropped from the MotherShipLarge hatch.
    // For the first DropDurationSeconds it descends straight down without attacking.
    // Its mothership shield reduces incoming damage while it clears the hatch.
    // After the drop phase it hands all logic to an inner KamikazeDroneControls instance
    // so the rest of the behaviour is identical to a normally-spawned kamikaze drone.
    public class HatchDroppedDroneControls : IObjectMovement
    {
        // -------------------------------------------------------
        //  Drop phase config
        // -------------------------------------------------------
        private const float DropDurationSeconds       = 2.0f;   // diagonal escape window before homing
        private const float DropDescentUnitsPerSecond = 180f;   // ObjectOffsets.y units/s (positive = downward on screen)
        private const float MinDropHorizontalUnitsPerSecond = 90f;
        private const float MaxDropHorizontalUnitsPerSecond = 210f;
        private const float HomingBlendDurationSeconds = 1.0f;
        private const float ShieldDurationSeconds     = 3.0f;
        private const float ShieldDamageMultiplier    = 0.2f;

        // -------------------------------------------------------
        //  Interface
        // -------------------------------------------------------
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // -------------------------------------------------------
        //  State
        // -------------------------------------------------------
        private readonly KamikazeDroneControls _inner = new();
        private readonly float _initialDelaySeconds;   // stagger: drone stays frozen until this elapses
        private readonly float _dropHorizontalUnitsPerSecond;
        private float _delayTimer    = 0f;
        private bool  _delayComplete = false;
        private bool  _dropComplete  = false;
        private float _dropTimer     = 0f;
        private float _homingBlendTimer = 0f;
        private float _shieldTimer   = 0f;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private readonly Func<DateTime> _now;

        public HatchDroppedDroneControls(
            float initialDelaySeconds = 0f,
            Func<DateTime>? now = null,
            float? dropHorizontalUnitsPerSecond = null)
        {
            _initialDelaySeconds = initialDelaySeconds;
            _now = now ?? (() => DateTime.Now);
            _dropHorizontalUnitsPerSecond = dropHorizontalUnitsPerSecond ?? CreateRandomDropHorizontalSpeed();
            // If there is no delay the drop phase starts immediately.
            _delayComplete = initialDelaySeconds <= 0f;
        }

        // -------------------------------------------------------
        //  MoveObject
        // -------------------------------------------------------
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            var now = _now();
            if (_lastMoveTime == DateTime.MinValue)
                _lastMoveTime = now;

            float deltaSeconds = Math.Max(0f, (float)(now - _lastMoveTime).TotalSeconds);
            _lastMoveTime = now;
            _shieldTimer += deltaSeconds;

            // The hatch shield reduces weapon damage, but a ram must consume the
            // drone like a normal KamikazeDrone so it cannot hit Ship every frame.
            if (theObject.ImpactStatus?.HasCrashed == true && theObject.ImpactStatus.ObjectName == "Ship")
            {
                _inner.ConfigureAudio(audioPlayer, soundRegistry);
                return _inner.MoveObject(theObject, audioPlayer, soundRegistry);
            }

            if (_shieldTimer < ShieldDurationSeconds)
            {
                bool destroyed = ApplyShieldedDamage(theObject);
                SyncToOriginal(theObject);
                if (destroyed)
                {
                    _inner.ConfigureAudio(audioPlayer, soundRegistry);
                    return _inner.MoveObject(theObject, audioPlayer, soundRegistry);
                }
            }

            if (_shieldTimer >= ShieldDurationSeconds && theObject.ImpactStatus?.HasCrashed == true)
            {
                _inner.ConfigureAudio(audioPlayer, soundRegistry);
                return _inner.MoveObject(theObject, audioPlayer, soundRegistry);
            }

            if (!_dropComplete)
            {
                // Configure audio as early as possible so the drone sound is ready.
                _inner.ConfigureAudio(audioPlayer, soundRegistry);

                // --- Initial delay: drone is frozen at its spawn position ---
                if (!_delayComplete)
                {
                    _delayTimer += deltaSeconds;
                    if (_delayTimer >= _initialDelaySeconds)
                        _delayComplete = true;
                    SyncToOriginal(theObject);
                    return theObject;
                }

                // --- Drop phase: escape diagonally down before homing ---
                if (theObject.ObjectOffsets != null)
                {
                    theObject.ObjectOffsets.x += _dropHorizontalUnitsPerSecond * deltaSeconds;
                    theObject.ObjectOffsets.y += DropDescentUnitsPerSecond * deltaSeconds;
                }

                _dropTimer += deltaSeconds;

                if (_dropTimer >= DropDurationSeconds)
                {
                    _dropComplete = true;
                    _inner.StartHuntDateTime = now;
                }

                SyncToOriginal(theObject);
                return theObject;
            }

            // Drop phase complete — smoothly trade escape momentum for homing movement.
            _inner.StartCoordinates = StartCoordinates;
            _inner.GuideCoordinates = GuideCoordinates;
            _inner.ParentObject     = ParentObject;
            _inner.Physics          = Physics;
            return MoveWithSmoothHandoff(theObject, audioPlayer, soundRegistry, deltaSeconds);
        }

        private I3dObject MoveWithSmoothHandoff(
            I3dObject theObject,
            IAudioPlayer? audioPlayer,
            ISoundRegistry? soundRegistry,
            float deltaSeconds)
        {
            var worldBefore = CopyVector(theObject.WorldPosition);
            var offsetsBefore = CopyVector(theObject.ObjectOffsets);
            var rotationBefore = CopyVector(theObject.Rotation);

            _inner.MoveObject(theObject, audioPlayer, soundRegistry);

            if (_homingBlendTimer < HomingBlendDurationSeconds)
            {
                _homingBlendTimer = Math.Min(HomingBlendDurationSeconds, _homingBlendTimer + deltaSeconds);
                float linearBlend = _homingBlendTimer / HomingBlendDurationSeconds;
                float homingBlend = linearBlend * linearBlend * (3f - 2f * linearBlend);
                float escapeBlend = 1f - homingBlend;

                var homingWorld = CopyVector(theObject.WorldPosition);
                var homingOffsets = CopyVector(theObject.ObjectOffsets);
                var homingRotation = CopyVector(theObject.Rotation);

                theObject.WorldPosition = Lerp(worldBefore, homingWorld, homingBlend);
                theObject.ObjectOffsets = new Vector3
                {
                    x = offsetsBefore.x + _dropHorizontalUnitsPerSecond * deltaSeconds * escapeBlend +
                        (homingOffsets.x - offsetsBefore.x) * homingBlend,
                    y = offsetsBefore.y + DropDescentUnitsPerSecond * deltaSeconds * escapeBlend +
                        (homingOffsets.y - offsetsBefore.y) * homingBlend,
                    z = offsetsBefore.z + (homingOffsets.z - offsetsBefore.z) * homingBlend
                };
                theObject.Rotation = Lerp(rotationBefore, homingRotation, homingBlend);
            }

            SyncToOriginal(theObject);
            return theObject;
        }

        private static Vector3 Lerp(Vector3 from, Vector3 to, float amount) => new()
        {
            x = from.x + (to.x - from.x) * amount,
            y = from.y + (to.y - from.y) * amount,
            z = from.z + (to.z - from.z) * amount
        };

        private static void SyncToOriginal(I3dObject source)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
                return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var target = aiObjects[i];
                if (target.ObjectId != source.ObjectId || ReferenceEquals(target, source))
                    continue;

                target.WorldPosition = CopyVector(source.WorldPosition);
                target.ObjectOffsets = CopyVector(source.ObjectOffsets);
                if (source.ImpactStatus != null && target.ImpactStatus != null)
                {
                    target.ImpactStatus.ObjectHealth = source.ImpactStatus.ObjectHealth;
                    target.ImpactStatus.HasCrashed = source.ImpactStatus.HasCrashed;
                    target.ImpactStatus.HasExploded = source.ImpactStatus.HasExploded;
                    target.ImpactStatus.ObjectName = source.ImpactStatus.ObjectName;
                }
                return;
            }
        }

        private static Vector3 CopyVector(IVector3? source) => new()
        {
            x = source?.x ?? 0f,
            y = source?.y ?? 0f,
            z = source?.z ?? 0f
        };

        private static float CreateRandomDropHorizontalSpeed()
        {
            float magnitude = MinDropHorizontalUnitsPerSecond +
                Random.Shared.NextSingle() * (MaxDropHorizontalUnitsPerSecond - MinDropHorizontalUnitsPerSecond);
            return Random.Shared.Next(2) == 0 ? -magnitude : magnitude;
        }

        private static bool ApplyShieldedDamage(I3dObject theObject)
        {
            if (theObject.ImpactStatus?.HasCrashed != true)
                return false;

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.KamikazeDroneHealth;
            int fullDamage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => EnemySetup.KamikazeDroneHealth,
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => currentHealth
            };
            int shieldedDamage = Math.Max(1, (int)MathF.Round(fullDamage * ShieldDamageMultiplier));

            theObject.ImpactStatus.ObjectHealth = currentHealth - shieldedDamage;
            if (theObject.ImpactStatus.ObjectHealth <= 0)
                return true;

            theObject.ImpactStatus.HasCrashed = false;
            return false;
        }

        // -------------------------------------------------------
        //  Interface pass-through
        // -------------------------------------------------------
        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
            => _inner.SetParticleGuideCoordinates(StartCoord, GuideCoord);

        public void ReleaseParticles(I3dObject theObject)
            => _inner.ReleaseParticles(theObject);

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
            => _inner.SetRearEngineGuideCoordinates(StartCoord, GuideCoord);

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
            => _inner.SetWeaponGuideCoordinates(StartCoord, GuideCoord);

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
            => _inner.ConfigureAudio(audioPlayer, soundRegistry);

        public void Dispose()
        {
            _inner.Dispose();
            _delayTimer    = 0f;
            _delayComplete = _initialDelaySeconds <= 0f;
            _dropComplete  = false;
            _dropTimer     = 0f;
            _homingBlendTimer = 0f;
            _shieldTimer   = 0f;
            _lastMoveTime  = DateTime.MinValue;
        }
    }
}
