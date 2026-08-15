using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class PowerUpControls : IObjectMovement
    {
        // Visual rotation:
        // - BaseXRotation tilts to face the camera (same approach as SeederControls).
        // - Yrotation increments each frame for a slow spin around the Y axis.
        private const float BaseXRotation = 90f;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 0f;
        private const float BaseYRotationIncrementPerFrame = 0.8f;

        // Sync offsets:
        // - SyncFactorY: scales how much the Y-offset follows the surface's GlobalMapPosition.y.
        private const float SyncFactorY = 2.5f;

        // Diagnostics:
        private const bool enableLogging = false;
        private const int LogEveryNthFrame = 60;

        // Explosion:
        private const float ExplosionForce = 100f;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private float Yrotation = BaseYRotation;
        private float Xrotation = BaseXRotation;
        private float Zrotation = BaseZRotation;

        private bool _syncInitialized = false;
        private float _syncY = 0;
        private int _logFrameCounter = 0;

        // Spawn zoom-in animation
        private bool _spawnAnimating = true;
        private float _spawnFrame = 0f;
        private const int SpawnAnimationFrames = 45;
        private const float SpawnStartZExtra = 800f;
        private float _spawnTargetZ;
        private bool _spawnTargetCaptured = false;
        private List<List<IVector3>>? _savedCrashBoxes;

        private bool _isExploding = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;

        private static void SafeLog(string message)
        {
            try
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log(message, "PowerUp");
            }
            catch { }
        }

        private static string FormatVector(Vector3 v)
        {
            return string.Create(CultureInfo.InvariantCulture, $"x={v.x:0.##};y={v.y:0.##};z={v.z:0.##}");
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            // Handle collision: start explosion animation (no explosion sound — powerup sound is played by ShipControls)
            if (theObject.ImpactStatus.HasCrashed == true && !_isExploding)
            {
                SafeLog($"[PowerUp] HasCrashed=true, ObjectName='{theObject.ImpactStatus.ObjectName}', starting explosion. Id={theObject.ObjectId}");
                _isExploding = true;
                _explosionDeltaTime = DateTime.Now;
                _explosionWorldPosition = CopyVector(theObject.WorldPosition);
                _explosionObjectOffsets = CopyVector(theObject.ObjectOffsets);
                Physics.ExplosionColorOverride = "4488FF";
                ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
                Physics.ExplodeObject(theObject, ExplosionForce);
                RestoreExplosionTransform(theObject);
                theObject.CrashBoxes = new List<List<IVector3>>();
            }

            if (_isExploding)
            {
                RestoreExplosionTransform(theObject);

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                RestoreExplosionTransform(theObject);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                    theObject.ObjectParts = new List<I3dObjectPart>();

                SyncToOriginal(theObject);
                return theObject;
            }

            if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
            if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
            if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;

            // Visual spin around Y
            Yrotation += BaseYRotationIncrementPerFrame * GameState.FrameScale90;

            // Keep offsets visually in sync with surface scrolling
            SyncMovement(theObject);

            // Spawn zoom-in: start high and ease down to target Z
            if (_spawnAnimating)
            {
                if (!_spawnTargetCaptured)
                {
                    _spawnTargetZ = theObject.ObjectOffsets.z;
                    _savedCrashBoxes = theObject.CrashBoxes;
                    theObject.CrashBoxes = new List<List<IVector3>>();
                    _spawnTargetCaptured = true;
                }

                _spawnFrame += GameState.FrameScale90;
                float t = Math.Min(1f, (float)_spawnFrame / SpawnAnimationFrames);
                float eased = 1f - (1f - t) * (1f - t);
                theObject.ObjectOffsets.z = _spawnTargetZ + SpawnStartZExtra * (1f - eased);

                if (t >= 1f)
                {
                    _spawnAnimating = false;
                    theObject.ObjectOffsets.z = _spawnTargetZ;
                    theObject.CrashBoxes = _savedCrashBoxes;
                }
            }

            // Push positions back to original in AiObjects
            SyncToOriginal(theObject);

            if (Logger.ShouldLog(enableLogging))
            {
                _logFrameCounter++;
                if (_logFrameCounter % LogEveryNthFrame == 0)
                {
                    var offsets = theObject.ObjectOffsets != null ? FormatVector((Vector3)theObject.ObjectOffsets) : "null";
                    var world = theObject.WorldPosition != null ? FormatVector((Vector3)theObject.WorldPosition) : "null";
                    int crashBoxCount = theObject.CrashBoxes?.Count ?? 0;
                    SafeLog($"[PowerUp] Id={theObject.ObjectId} Offsets={offsets} World={world} CrashBoxes={crashBoxCount} HasCrashed={theObject.ImpactStatus?.HasCrashed} HasExploded={theObject.ImpactStatus?.HasExploded}");
                }
            }

            return theObject;
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

        private void RestoreExplosionTransform(I3dObject theObject)
        {
            if (_explosionWorldPosition != null)
                theObject.WorldPosition = CopyVector(_explosionWorldPosition);

            if (_explosionObjectOffsets != null)
                theObject.ObjectOffsets = CopyVector(_explosionObjectOffsets);
        }

        private static Vector3 CopyVector(IVector3 source)
        {
            return new Vector3
            {
                x = source.x,
                y = source.y,
                z = source.z
            };
        }

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void Dispose()
        {
            _syncInitialized = false;
            _syncY = 0;
            _spawnAnimating = true;
            _spawnFrame = 0f;
            _spawnTargetCaptured = false;
            _savedCrashBoxes = null;
            _isExploding = false;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            StartCoordinates = null;
            GuideCoordinates = null;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }
    }
}
