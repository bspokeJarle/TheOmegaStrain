using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class BomberBombControls : IObjectMovement
    {
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 270f;

        private const float InitialFallSpeed = 50f;
        private const float GravityAcceleration = 300f;
        private const float MaxFallTimeSeconds = 4f;
        private const float ExplosionForce = 225f;

        private float _currentFallSpeed;
        private DateTime _lastFrameTime = DateTime.MinValue;
        private DateTime _spawnTime = DateTime.MinValue;
        private bool _syncInitialized = false;
        private float _syncY = 0f;
        private float _initialZ = 0f;
        private float _targetZ = 0f;

        private bool _isExploding = false;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;

        private bool _audioConfigured = false;
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _howlSound;
        private IAudioInstance? _howlInstance;
        private float _howlSoundDuration;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;

            if (_spawnTime == DateTime.MinValue)
            {
                _spawnTime = DateTime.Now;
                _currentFallSpeed = InitialFallSpeed;
                StartHowl(theObject);
            }

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = BaseXRotation;
                theObject.Rotation.y = BaseYRotation;
                theObject.Rotation.z = BaseZRotation;
            }

            float deltaSeconds = 0f;
            if (_lastFrameTime != DateTime.MinValue)
            {
                deltaSeconds = (float)(DateTime.Now - _lastFrameTime).TotalSeconds;
                deltaSeconds = Math.Clamp(deltaSeconds, 0f, 0.1f);
            }
            _lastFrameTime = DateTime.Now;

            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                StartExplosion(theObject);
            }

            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                    theObject.WorldPosition = _explosionWorldPosition;
                if (_explosionObjectOffsets != null)
                    theObject.ObjectOffsets = _explosionObjectOffsets;

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                    if (theObject.ImpactStatus != null)
                    {
                        // Keep crash source for crater detection after explosion completes.
                        if (string.IsNullOrEmpty(theObject.ImpactStatus.ObjectName))
                            theObject.ImpactStatus.ObjectName = "Surface";
                    }
                }
                SyncToOriginal(theObject);
                return theObject;
            }

            _currentFallSpeed += GravityAcceleration * deltaSeconds;

            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets?.y ?? 0f;
                _initialZ = theObject.ObjectOffsets?.z ?? 0f;
                _targetZ = GameState.SurfaceState.SurfaceViewportObject?.ObjectOffsets?.z ?? _initialZ;
            }

            _syncY += _currentFallSpeed * deltaSeconds;
            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY);

            float elapsed = (float)(DateTime.Now - _spawnTime).TotalSeconds;
            float fallProgress = Math.Clamp(elapsed / MaxFallTimeSeconds, 0f, 1f);
            float currentZ = _initialZ + (_targetZ - _initialZ) * fallProgress;
            theObject.ObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets?.x ?? 0f,
                y = theObject.ObjectOffsets?.y ?? 0f,
                z = currentZ
            };

            UpdateHowl(theObject, fallProgress);

            if (elapsed >= MaxFallTimeSeconds)
            {
                StartExplosion(theObject);
            }

            SyncToOriginal(theObject);
            return theObject;
        }

        private void StartHowl(I3dObject theObject)
        {
            if (_audio == null || _howlSound == null) return;

            _howlSoundDuration = (float)(_howlSound.Segments.LoopEnd - _howlSound.Segments.Start);
            if (_howlSoundDuration <= 0f) _howlSoundDuration = 1f;

            float speed = _howlSoundDuration / MaxFallTimeSeconds;

            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _howlInstance = _audio.Play(
                _howlSound,
                AudioPlayMode.OneShot,
                new AudioPlayOptions
                {
                    SpeedOverride = speed,
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private void UpdateHowl(I3dObject theObject, float fallProgress)
        {
            if (_howlInstance == null || !_howlInstance.IsPlaying) return;

            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _howlInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));

            float volumeRamp = 0.6f + 0.4f * fallProgress;
            _howlInstance.SetVolume((_howlSound?.Settings.Volume ?? 1f) * volumeRamp);
        }

        private void StartExplosion(I3dObject theObject)
        {
            if (_isExploding) return;

            if (_howlInstance?.IsPlaying == true)
                _howlInstance.Stop(false);

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

            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = theObject.WorldPosition as Vector3 ?? ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = theObject.ObjectOffsets as Vector3 ?? ToVector3(theObject.ObjectOffsets);

            if (theObject.ImpactStatus == null)
                theObject.ImpactStatus = new ImpactStatus();

            // Mark surface impact immediately so GroundControls crater detection can process
            // the bomb even after the visual explosion animation completes.
            theObject.ImpactStatus.HasCrashed = true;
            theObject.ImpactStatus.ObjectName = "Surface";

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, ExplosionForce);
            theObject.CrashBoxes = new List<List<IVector3>>();
        }

        private static Vector3 ToVector3(IVector3? v)
        {
            if (v is null) return new Vector3();
            return new Vector3 { x = v.x, y = v.y, z = v.z };
        }

        private static void SyncToOriginal(I3dObject source)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == source.ObjectId)
                {
                    var target = aiObjects[i];
                    if (ReferenceEquals(target, source)) return;

                    target.WorldPosition = ToVector3(source.WorldPosition);
                    target.ObjectOffsets = ToVector3(source.ObjectOffsets);

                    if (source.ImpactStatus != null && target.ImpactStatus != null)
                    {
                        target.ImpactStatus.HasExploded = source.ImpactStatus.HasExploded;
                        target.ImpactStatus.HasCrashed = source.ImpactStatus.HasCrashed;
                    }
                    return;
                }
            }
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured) return;
            if (audioPlayer == null || soundRegistry == null) return;

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");

            if (soundRegistry.TryGet("diving_bomb", out var howlDef))
                _howlSound = howlDef;

            _audioConfigured = true;
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void ReleaseParticles(I3dObject theObject) { }

        public void Dispose()
        {
            _syncInitialized = false;
            _syncY = 0f;
            _initialZ = 0f;
            _targetZ = 0f;
            _currentFallSpeed = 0f;
            _spawnTime = DateTime.MinValue;
            _lastFrameTime = DateTime.MinValue;
            _isExploding = false;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            if (_howlInstance?.IsPlaying == true)
                _howlInstance.Stop(false);
            _howlInstance = null;
            _howlSound = null;
            _howlSoundDuration = 0f;
            _audioConfigured = false;
            _audio = null;
            _explosionSound = null;
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
