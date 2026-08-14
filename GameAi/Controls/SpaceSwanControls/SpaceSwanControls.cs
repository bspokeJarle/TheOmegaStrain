using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Helpers;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.SpaceSwanControls
{
    public class SpaceSwanControls : IObjectMovement
    {
        // Visual rotation:
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 90f;
        private const float RotationDegreesPerSecond = 120f;

        // Sync offsets:
        private const float SyncFactorY = 2.5f;

        // Explosion:
        private const float ExplosionForce = 200f;

        // Wing flap animation:
        private const float BaseFlapSpeed = 0.06f;
        private const float InnerAmplitude = 12.0f;
        private const float MidAmplitude = 10.0f;
        private const float OuterAmplitude = 8.0f;
        private const float MidPhaseOffset = 0.5f;
        private const float OuterPhaseOffset = 1.0f;

        // Wing pivot points (from SpaceSwan geometry, post-scale at ZoomRatio=1.9):
        private const float ShoulderY = 17.1f;
        private const float ShoulderZ = 3.8f;
        private const float ElbowY = 38.0f;
        private const float ElbowZ = 1.9f;
        private const float WristY = 58.9f;
        private const float WristZ = 0.38f;

        private float _flapPhase = 0f;
        private int _lastFlapSinSign = 0;
        private readonly OmegaMeshRotation _rotate = new();

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private float Yrotation = BaseYRotation;
        private float Xrotation = BaseXRotation;
        private float Zrotation = BaseZRotation;
        private float TargetYrotation = BaseYRotation;
        private float TargetXrotation = BaseXRotation;
        private float TargetZrotation = BaseZRotation;

        private bool _syncInitialized = false;
        private float _syncY = 0;
        private Vector3? _trackedWorldPosition;
        private DateTime _lastFrameTime = DateTime.MinValue;

        private bool _isExploding = false;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;

        private bool _audioConfigured = false;
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _wingFlapSound;

        private readonly SpaceSwanAi.SwanState _aiState = new();

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured) return;
            if (audioPlayer == null || soundRegistry == null) return;

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
            if (soundRegistry.TryGet("swan_wings", out var wingSound))
            {
                _wingFlapSound = wingSound;
            }
            _audioConfigured = true;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            SpaceSwanAi.Initialize(_aiState, theObject);

            // Initialize tracked position from the object on first call
            if (_trackedWorldPosition == null)
            {
                _trackedWorldPosition = new Vector3
                {
                    x = theObject.WorldPosition.x,
                    y = theObject.WorldPosition.y,
                    z = theObject.WorldPosition.z
                };
            }

            // Compute delta time
            var now = DateTime.Now;
            float deltaSeconds = 0f;
            if (_lastFrameTime != DateTime.MinValue)
                deltaSeconds = (float)(now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;
            if (deltaSeconds > 1f) deltaSeconds = 0f;

            TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);

            // AI movement
            if (!_isExploding && theObject.ImpactStatus?.HasCrashed != true)
            {
                SpaceSwanAi.UpdateMovement(_aiState, _trackedWorldPosition, deltaSeconds);
            }

            // Apply tracked position
            theObject.WorldPosition = new Vector3
            {
                x = _trackedWorldPosition.x,
                y = _trackedWorldPosition.y,
                z = _trackedWorldPosition.z
            };

            // Handle crash
            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            // Explosion
            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                {
                    theObject.WorldPosition = new Vector3
                    {
                        x = _explosionWorldPosition.x,
                        y = _explosionWorldPosition.y,
                        z = _explosionWorldPosition.z
                    };
                }
                if (_explosionObjectOffsets != null)
                {
                    theObject.ObjectOffsets = new Vector3
                    {
                        x = _explosionObjectOffsets.x,
                        y = _explosionObjectOffsets.y,
                        z = _explosionObjectOffsets.z
                    };
                }

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                }

                SyncToOriginal(theObject);
                return theObject;
            }

            // Compute heading from movement direction
            var heading = OmegaObjectHelpers.GetHeadingFromDirection(_aiState.DirectionX, _aiState.DirectionZ);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;

            // Smooth rotation toward heading
            float maxDelta = RotationDegreesPerSecond * deltaSeconds;
            if (maxDelta > 0f)
            {
                Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
                Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
                Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);
            }

            if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
            if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
            if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;

            // Advance flap phase every frame (needed for off-screen sound timing)
            float flapMultiplier = _aiState.CurrentSpeed / SpaceSwanAi.BaseFlySpeed;
            _flapPhase += BaseFlapSpeed * flapMultiplier * deltaSeconds * GameState.GameplayBaselineFps;

            // Wing geometry — only modify on rendering deep copies (on-screen).
            // Off-screen AI updates share the same Movement instance and would
            // corrupt the original's wing triangles, compounding across frames.
            if (theObject.IsOnScreen)
            {
                AnimateWingFlap();
            }

            // 3D wing flap sound — plays on-screen and off-screen when ship is close
            PlayWingFlapSound(theObject);

            // Surface sync
            SyncMovement(theObject);
            if (TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, deltaSeconds) && theObject.WorldPosition != null)
            {
                _trackedWorldPosition = new Vector3
                {
                    x = theObject.WorldPosition.x,
                    y = theObject.WorldPosition.y,
                    z = theObject.WorldPosition.z
                };
            }
            HitSparkEffects.MoveHitSparks(theObject);
            SyncToOriginal(theObject);

            return theObject;
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null) return;

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.SpaceSwanHealth;
            int damage = currentHealth;

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            if (_audio != null && _explosionSound != null)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();
                _audio.Play(
                    _explosionSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }

            _explosionDeltaTime = DateTime.Now;
            _isExploding = true;
            var wp = theObject.WorldPosition;
            _explosionWorldPosition = new Vector3 { x = wp.x, y = wp.y, z = wp.z };
            var oo = theObject.ObjectOffsets;
            _explosionObjectOffsets = new Vector3 { x = oo.x, y = oo.y, z = oo.z };
            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, ExplosionForce);
            theObject.CrashBoxes = new List<List<IVector3>>();
            theObject.ImpactStatus.HasCrashed = false;
            SpaceSwanAi.ResetState(_aiState);
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets.y;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY, SyncFactorY);
        }

        private void AnimateWingFlap()
        {
            if (ParentObject == null) return;

            float innerAngle = InnerAmplitude * MathF.Sin(_flapPhase);
            float midAngle = MidAmplitude * MathF.Sin(_flapPhase - MidPhaseOffset);
            float outerAngle = OuterAmplitude * MathF.Sin(_flapPhase - OuterPhaseOffset);

            // Left wing
            FlapWingChain(
                "SpaceSwanLeftWingInner", "SpaceSwanLeftWingMid", "SpaceSwanLeftWingOuter",
                -ShoulderY, ShoulderZ, -ElbowY, ElbowZ, -WristY, WristZ,
                innerAngle, midAngle, outerAngle);

            // Right wing (side=+1, vertices at +Y)
            FlapWingChain(
                "SpaceSwanRightWingInner", "SpaceSwanRightWingMid", "SpaceSwanRightWingOuter",
                ShoulderY, ShoulderZ, ElbowY, ElbowZ, WristY, WristZ,
                -innerAngle, -midAngle, -outerAngle);
        }

        private void PlayWingFlapSound(I3dObject theObject)
        {
            if (_audio == null || _wingFlapSound == null) return;

            // Detect downstroke: sin(_flapPhase) crossing from positive to negative
            float sinVal = MathF.Sin(_flapPhase);
            int sign = sinVal >= 0 ? 1 : -1;
            bool downstroke = _lastFlapSinSign > 0 && sign <= 0;
            _lastFlapSinSign = sign;

            if (!downstroke) return;

            // Compute volume based on on-screen vs off-screen distance
            float volume = 0f;
            System.Numerics.Vector3 audioWorldPos = System.Numerics.Vector3.Zero;

            if (theObject.IsOnScreen)
            {
                var audioPos = ((_3dObject)theObject).GetAudioPosition();
                audioWorldPos = new System.Numerics.Vector3(audioPos.x, audioPos.y, audioPos.z);
                volume = _wingFlapSound.Settings.Volume;
            }
            else
            {
                var globalPos = GameState.SurfaceState?.GlobalMapPosition;
                var swanWorldPos = theObject.WorldPosition;
                if (globalPos == null || swanWorldPos == null) return;

                float distSq = OmegaObjectHelpers.GetDistanceSquared(globalPos, swanWorldPos);
                float maxDist = AudioSetup.OffscreenAiAudioMaxDistance;
                if (distSq > maxDist * maxDist) return;

                float distance = MathF.Sqrt(distSq);
                float normalized = distance / maxDist;
                volume = _wingFlapSound.Settings.Volume *
                    MathF.Pow(1f - normalized, AudioSetup.OffscreenAiAudioCurveExponent);

                float dx = swanWorldPos.x - globalPos.x;
                audioWorldPos = new System.Numerics.Vector3(dx, 0, 0);
            }

            _audio.PlayOneShot(_wingFlapSound, new AudioPlayOptions
            {
                WorldPosition = audioWorldPos,
                VolumeOverride = volume
            });
        }

        private void FlapWingChain(
            string innerName, string midName, string outerName,
            float shoulderY, float shoulderZ,
            float elbowY, float elbowZ,
            float wristY, float wristZ,
            float innerAngle, float midAngle, float outerAngle)
        {
            var innerPart = ParentObject.ObjectParts.Find(p => p.PartName == innerName);
            var midPart = ParentObject.ObjectParts.Find(p => p.PartName == midName);
            var outerPart = ParentObject.ObjectParts.Find(p => p.PartName == outerName);

            // Inner: rotate around shoulder pivot
            if (innerPart != null)
                innerPart.Triangles = RotateAroundPivot(innerPart.Triangles, shoulderY, shoulderZ, innerAngle);

            // Elbow position after inner rotation
            var (newElbowY, newElbowZ) = RotatePointAroundPivot(elbowY, elbowZ, shoulderY, shoulderZ, innerAngle);

            // Mid: carry with inner rotation, then own rotation around new elbow
            if (midPart != null)
            {
                midPart.Triangles = RotateAroundPivot(midPart.Triangles, shoulderY, shoulderZ, innerAngle);
                midPart.Triangles = RotateAroundPivot(midPart.Triangles, newElbowY, newElbowZ, midAngle);
            }

            // Wrist position after inner + mid rotations
            var (wristAfterInnerY, wristAfterInnerZ) = RotatePointAroundPivot(wristY, wristZ, shoulderY, shoulderZ, innerAngle);
            var (newWristY, newWristZ) = RotatePointAroundPivot(wristAfterInnerY, wristAfterInnerZ, newElbowY, newElbowZ, midAngle);

            // Outer: carry with inner and mid rotations, then own rotation around new wrist
            if (outerPart != null)
            {
                outerPart.Triangles = RotateAroundPivot(outerPart.Triangles, shoulderY, shoulderZ, innerAngle);
                outerPart.Triangles = RotateAroundPivot(outerPart.Triangles, newElbowY, newElbowZ, midAngle);
                outerPart.Triangles = RotateAroundPivot(outerPart.Triangles, newWristY, newWristZ, outerAngle);
            }
        }

        private List<ITriangleMeshWithColor> RotateAroundPivot(List<ITriangleMeshWithColor> triangles, float pivotY, float pivotZ, float angleDegrees)
        {
            TranslateTrianglesYZ(triangles, -pivotY, -pivotZ);
            var rotated = _rotate.RotateXMesh(triangles, angleDegrees);
            if (!ReferenceEquals(rotated, triangles))
            {
                TranslateTrianglesYZ(triangles, pivotY, pivotZ);
            }
            TranslateTrianglesYZ(rotated, pivotY, pivotZ);
            return rotated;
        }

        private static void TranslateTrianglesYZ(List<ITriangleMeshWithColor> triangles, float dy, float dz)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                t.vert1.y += dy; t.vert1.z += dz;
                t.vert2.y += dy; t.vert2.z += dz;
                t.vert3.y += dy; t.vert3.z += dz;
            }
        }

        private static (float y, float z) RotatePointAroundPivot(float py, float pz, float pivotY, float pivotZ, float angleDegrees)
        {
            float relY = py - pivotY;
            float relZ = pz - pivotZ;
            float rad = MathF.PI * angleDegrees / 180f;
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            float newY = relY * cos - relZ * sin + pivotY;
            float newZ = relZ * cos + relY * sin + pivotZ;
            return (newY, newZ);
        }

        private static void SyncToOriginal(I3dObject deepCopy)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == deepCopy.ObjectId)
                {
                    var original = aiObjects[i];
                    if (ReferenceEquals(original, deepCopy)) return;

                    original.WorldPosition = new Vector3
                    {
                        x = deepCopy.WorldPosition.x,
                        y = deepCopy.WorldPosition.y,
                        z = deepCopy.WorldPosition.z
                    };
                    original.ObjectOffsets = new Vector3
                    {
                        x = deepCopy.ObjectOffsets.x,
                        y = deepCopy.ObjectOffsets.y,
                        z = deepCopy.ObjectOffsets.z
                    };
                    return;
                }
            }
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void Dispose()
        {
            _isExploding = false;
            _syncInitialized = false;
            _syncY = 0;
            _trackedWorldPosition = null;
            _lastFrameTime = DateTime.MinValue;
            _audioConfigured = false;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            StartCoordinates = null;
            GuideCoordinates = null;
            _audio = null;
            _explosionSound = null;
            _wingFlapSound = null;
            _lastFlapSinSign = 0;
            SpaceSwanAi.ResetState(_aiState);
        }
    }
}
