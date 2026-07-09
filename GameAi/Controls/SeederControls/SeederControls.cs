using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Helpers;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.SeederControls
{
    public class SeederControls : IObjectMovement
    {
        // ============================
        // CONTROLS CONFIGURATION
        // ============================
        // Visual rotation:
        // - Base rotations applied each frame to the seeder object.
        private const float BaseYRotation = 0f;
        private const float BaseXRotation = 90f;
        private const float BaseZRotationIncrementPerFrame = 2f;

        // Sync offsets:
        // - SyncFactorY: scales how much the seeder's Y-offset follows the surface's GlobalMapPosition.y.
        // - SyncInitializedStartY: initial additive offset captured from the object when first synced (auto-set).
        private const float SyncFactorY = 2.5f;

        // Audio:
        // - ExplosionForce: force factor passed to physics when exploding.
        // - EnableControlsLogging: local logging toggle for controls-only events (crash/explosion).
        private const float ExplosionForce = 200f;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // Audio references are initialized lazily from ConfigureAudio.
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _seederEngineSound;
        private IAudioInstance? _seederEngineInstance;
        private SoundDefinition? _seederSeedingSound;
        private IAudioInstance? _seederSeedingInstance;
        private bool _audioConfigured = false;

        private float Yrotation = BaseYRotation;
        private float Xrotation = BaseXRotation;
        private float Zrotation = 0;

        private bool _syncInitialized = false;
        private float _syncY = 0;
        private bool enableLogging = false;
        private bool isExploding = false;
        private bool _visualPhaseInitialized = false;
        private DateTime ExplosionDeltaTime;
        private Vector3? explosionWorldPosition;
        private Vector3? explosionObjectOffsets;
        private Vector3? explosionRotation;
        private Vector3? _trackedWorldPosition;

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
            if (soundRegistry.TryGet("seeder_engine", out var engineSound))
            {
                _seederEngineSound = engineSound;
            }
            if (soundRegistry.TryGet("seeder_seeding", out var seedingSound))
            {
                _seederSeedingSound = seedingSound;
            }
            _audioConfigured = true;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            // Lazily initialize audio the first time MoveObject runs.
            ConfigureAudio(audioPlayer, soundRegistry);
            if (!isExploding)
            {
                TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);
            }

            // Skip AI movement when a crash is pending or already exploding,
            // so the explosion stays anchored at the position where the hit occurred.
            if (!isExploding && theObject.ImpactStatus.HasCrashed != true)
            {
                var aiPos = SeederAi.MoveWorldPositionAccordingToAi(theObject.IsOnScreen, theObject);
                if (aiPos != null)
                {
                    _trackedWorldPosition = aiPos;
                }
            }

            // Always apply the tracked position so the deep copy's stale
            // WorldPosition (copied from the original) is replaced with the
            // control-class's authoritative value.
            if (_trackedWorldPosition != null)
            {
                theObject.WorldPosition = new Vector3
                {
                    x = _trackedWorldPosition.x,
                    y = _trackedWorldPosition.y,
                    z = _trackedWorldPosition.z
                };
            }
      
            //Set parent object
            ParentObject = theObject;
            EnsureVisualPhase(theObject);
            if (isExploding && explosionRotation != null)
            {
                theObject.Rotation = CopyVector(explosionRotation);
            }
            else
            {
                if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
                if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
                if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;
            }

            //Handle impact status, trigger explosion if health is 0
            if (theObject.ImpactStatus.HasCrashed == true && isExploding == false)
            {
                HandleCrash(theObject);
            }

            // 3D engine sound: play when alive, position-track each frame
            if (_audio != null && _seederEngineSound != null && !isExploding)
            {
                if (theObject.IsOnScreen)
                {
                    var audioPosition = ((_3dObject)theObject).GetAudioPosition();

                    if (_seederEngineInstance == null || !_seederEngineInstance.IsPlaying)
                    {
                        _seederEngineInstance = _audio.Play(
                            _seederEngineSound,
                            AudioPlayMode.SegmentedLoop,
                            new AudioPlayOptions
                            {
                                WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                            });
                    }

                    _seederEngineInstance.SetVolume(_seederEngineSound.Settings.Volume);
                    _seederEngineInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
                }
                else
                {
                    var globalPos = GameState.SurfaceState?.GlobalMapPosition;
                    var seederWorldPos = theObject.WorldPosition;

                    if (globalPos != null && seederWorldPos != null)
                    {
                        float distSq = Common3dObjectHelpers.GetDistanceSquared(globalPos, seederWorldPos);
                        float maxDist = AudioSetup.OffscreenAiAudioMaxDistance;
                        float maxDistSq = maxDist * maxDist;

                        if (distSq <= maxDistSq)
                        {
                            float distance = MathF.Sqrt(distSq);
                            float normalized = distance / maxDist;
                            float volume = _seederEngineSound.Settings.Volume *
                                MathF.Pow(1f - normalized, AudioSetup.OffscreenAiAudioCurveExponent);

                            if (_seederEngineInstance == null || !_seederEngineInstance.IsPlaying)
                            {
                                _seederEngineInstance = _audio.Play(
                                    _seederEngineSound,
                                    AudioPlayMode.SegmentedLoop,
                                    new AudioPlayOptions
                                    {
                                        WorldPosition = System.Numerics.Vector3.Zero
                                    });
                            }

                            float dx = seederWorldPos.x - globalPos.x;
                            _seederEngineInstance.SetWorldPosition(new System.Numerics.Vector3(dx, 0, 0));
                            _seederEngineInstance.SetVolume(volume);
                        }
                        else if (_seederEngineInstance != null)
                        {
                            _seederEngineInstance.Stop(playEndSegment: false);
                            _seederEngineInstance = null;
                        }
                    }
                    else if (_seederEngineInstance != null)
                    {
                        _seederEngineInstance.Stop(playEndSegment: false);
                        _seederEngineInstance = null;
                    }
                }
            }

            // Seeding sound: plays while particles are active (visual seeding effect)
            if (_audio != null && _seederSeedingSound != null && !isExploding)
            {
                bool hasActiveParticles = ParentObject.Particles?.Particles.Count > 0;

                if (hasActiveParticles && theObject.IsOnScreen)
                {
                    var audioPosition = ((_3dObject)theObject).GetAudioPosition();

                    if (_seederSeedingInstance == null || !_seederSeedingInstance.IsPlaying)
                    {
                        _seederSeedingInstance = _audio.Play(
                            _seederSeedingSound,
                            AudioPlayMode.SegmentedLoop,
                            new AudioPlayOptions
                            {
                                WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                            });
                    }

                    _seederSeedingInstance.SetVolume(_seederSeedingSound.Settings.Volume);
                    _seederSeedingInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
                }
                else if (_seederSeedingInstance != null)
                {
                    _seederSeedingInstance.Stop(playEndSegment: false);
                    _seederSeedingInstance = null;
                }
            }

            if (isExploding)
            {
                RestoreExplosionTransform(theObject);

                //Update explosion
                Physics.UpdateExplosion(theObject, ExplosionDeltaTime);
                RestoreExplosionTransform(theObject);

                if (theObject.ImpactStatus.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                }
            }

            //If there are particles, move them
            if (ParentObject.Particles?.Particles.Count > 0)
            {
                ParentObject.Particles.MoveParticles();
            }

            // Visual spin
            if (!isExploding)
                Zrotation += BaseZRotationIncrementPerFrame * GameState.FrameScale90;

            if (!isExploding)
            {
                SyncMovement(theObject);
                if (TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, GameState.ClampedDeltaTime) && theObject.WorldPosition != null)
                {
                    _trackedWorldPosition = new Vector3
                    {
                        x = theObject.WorldPosition.x,
                        y = theObject.WorldPosition.y,
                        z = theObject.WorldPosition.z
                    };
                }
            }

            // Push the deep copy's authoritative positions back to the original
            // object so shadow casting and other systems that read from AiObjects
            // stay in sync with the AI-tracked location.
            SyncToOriginal(theObject);

            return theObject;
        }

        public void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
                return;

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.SeederHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => currentHealth,
                "DroneDecoy" => EnemySetup.DroneDecoyCollisionDamage,
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => 0
            };

            if (damage <= 0)
            {
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;
            if (theObject.ImpactStatus.ObjectHealth <= 0)
            {
                if (_seederEngineInstance != null)
                {
                    _seederEngineInstance.Stop(playEndSegment: false);
                    _seederEngineInstance = null;
                }
                if (_seederSeedingInstance != null)
                {
                    _seederSeedingInstance.Stop(playEndSegment: false);
                    _seederSeedingInstance = null;
                }
                if (_audio != null && _explosionSound != null)
                {
                    var audioPosition = ((_3dObject)theObject).GetAudioPosition();
                    //Play the explosion sound
                    _audio.Play(
                        _explosionSound,
                        AudioPlayMode.OneShot,
                        new AudioPlayOptions
                        {
                            WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                        });
                }

                ExplosionDeltaTime = DateTime.Now;
                isExploding = true;
                // Copy values — not references — so UpdateExplosion cannot drift the anchor.
                var wp = theObject.WorldPosition;
                explosionWorldPosition = new Vector3 { x = wp.x, y = wp.y, z = wp.z };
                var oo = theObject.ObjectOffsets;
                explosionObjectOffsets = new Vector3 { x = oo.x, y = oo.y, z = oo.z };
                EnsureExplosionSyncAnchor(explosionObjectOffsets);
                var rotation = theObject.Rotation ?? new Vector3();
                explosionRotation = new Vector3 { x = rotation.x, y = rotation.y, z = rotation.z };
                ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
                // Handle object destruction or other logic here
                var explodedVersion = Physics.ExplodeObject(theObject, ExplosionForce);
                RestoreExplosionTransform(theObject);
                //Remove Crash boxes to avoid further collisions
                theObject.CrashBoxes = new List<List<IVector3>>();
                //Remove AI state to stop movement and other logic
                SeederAi.RemoveAiState(theObject.ObjectId);
                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Seeder has exploded.");
            }
            else
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                theObject.ImpactStatus.HasCrashed = false;
            }

            if (Logger.ShouldLog(enableLogging)) Logger.Log($"Seeder has crashed, current health {theObject.ImpactStatus.ObjectHealth}. CrashedWith:{theObject.ImpactStatus.ObjectName} ObjectId:{theObject.ObjectId}");
        }

        public void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets.y;
            }

            theObject.ObjectOffsets = CommonUtilities._3DHelpers.SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY, SyncFactorY);
        }

        private void EnsureVisualPhase(I3dObject theObject)
        {
            if (_visualPhaseInitialized)
                return;

            Zrotation = (theObject.ObjectId * 47) % 360;
            _visualPhaseInitialized = true;
        }

        private void EnsureExplosionSyncAnchor(IVector3 syncedObjectOffsets)
        {
            if (_syncInitialized)
                return;

            _syncInitialized = true;
            _syncY = syncedObjectOffsets.y - GameState.SurfaceState.GlobalMapPosition.y * SyncFactorY;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (StartCoordinates == null || GuideCoordinates == null) return;

            var worldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };

            ParentObject?.Particles?.ReleaseParticles(
                GuideCoordinates,
                StartCoordinates,
                worldPosition,
                this,
                3,
                null);
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            if (_seederEngineInstance != null)
            {
                _seederEngineInstance.Stop(playEndSegment: false);
                _seederEngineInstance = null;
            }
            if (_seederSeedingInstance != null)
            {
                _seederSeedingInstance.Stop(playEndSegment: false);
                _seederSeedingInstance = null;
            }

            if (ParentObject != null)
            {
                SeederAi.RemoveAiState(ParentObject.ObjectId);
            }

            isExploding = false;
            _visualPhaseInitialized = false;
            Zrotation = 0;
            _syncInitialized = false;
            _syncY = 0;
            _audioConfigured = false;
            explosionWorldPosition = null;
            explosionObjectOffsets = null;
            explosionRotation = null;
            StartCoordinates = null;
            GuideCoordinates = null;
            _audio = null;
            _explosionSound = null;
            _seederEngineSound = null;
            _seederSeedingSound = null;
        }

        /// <summary>
        /// Process delayed cascading infection spread. Call once per frame from the game loop.
        /// </summary>
        public static void ProcessLocalInfectionSpread(CommonUtilities.CommonGlobalState.States.SurfaceState surfaceState)
        {
            SeederMovementHelpers.ProcessLocalInfectionSpread(surfaceState);
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            //No implementation needed, Seeder have no weapons
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

        private static Vector3 CopyVector(IVector3 source)
        {
            return new Vector3
            {
                x = source.x,
                y = source.y,
                z = source.z
            };
        }

        private void RestoreExplosionTransform(I3dObject theObject)
        {
            if (explosionWorldPosition != null)
            {
                theObject.WorldPosition = CopyVector(explosionWorldPosition);
            }

            if (explosionObjectOffsets != null)
            {
                theObject.ObjectOffsets = CopyVector(explosionObjectOffsets);
            }

            if (explosionRotation != null)
            {
                theObject.Rotation = CopyVector(explosionRotation);
            }
        }
    }
}
