using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Helpers;
using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class DecoyBeaconControls : IObjectMovement
    {
        private static readonly OmegaMeshRotation Rotate3d = new();
        private readonly OmegaMeshRotation _rotate = new();
        private const bool enableLogging = false;
        private const string WheelPartName = "DecoyFrontPulsePanel";
        private const float WheelRotationDegreesPerSecond = 540f;
        // Deployed decoy fuse: timeout explosion only. Direct hits explode immediately.
        private const float DeployedDecoyLifetimeSeconds = 2.25f;
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        //Initial rotation angles for the drone, pointing towards the camera. Adjust as needed based on the drone model's default orientation.
        private float Yrotation = 0;
        private float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float Zrotation = 90;
        private float TargetYrotation = 0;
        private float TargetXrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float TargetZrotation = 90;

        const int DroneSpeedScreenPrSecond = 3; //How many seconds it should take for the drone to cross the entire screen at its current speed. Adjust as needed.
        private const float RotationDegreesPerSecond = 180f;
        private const float DirectionUpdateIntervalSeconds = 1f;
        private const int LogEveryNthFrame = 10;

        private DateTime LastDirectionUpdateDateTime = DateTime.MinValue;
        private DateTime LastMovementDateTime = DateTime.MinValue;
        private IVector3 DirectionVelocity = new Vector3 { x = 0, y = 0, z = 0 }; // Initially standing still until the first direction is calculated
        private bool _syncInitialized = false;
        private float _syncY = 0;
        private int _logFrameCounter = 0;
        private int _trackedObjectId = -1;
        private bool _storedWorldPositionInitialized = false;
        private Vector3 _storedWorldPosition = new Vector3();
        private bool _audioConfigured = false;
        private bool _isExploding = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private DateTime _pulseStartTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private bool _pulseInitialized = false;
        private float _pulseBaseZ = 0f;
        private float _wheelRotationDegrees = 0f;
        private bool _isDeployedDecoy = false;
        private DateTime _deployedAt = DateTime.MinValue;
        private Vector3 _deployedWorldPosition = new Vector3();

        // Launch rise: decoy rises 50 units over 2 seconds after deployment
        private const float LaunchRiseUnits = 50f;
        private const float LaunchRiseDurationSeconds = 2f;

        private const float PulseAmplitudeZ = 150f;
        private const float PulseCyclesPerSecond = 0.25f;

        private static void SafeLog(string message)
        {
            try
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log(message, "KamikazeDrone");
            }
            catch
            {
            }
        }

        private static string FormatVector(Vector3 v)
        {
            return string.Create(CultureInfo.InvariantCulture, $"x={v.x:0.##};y={v.y:0.##};z={v.z:0.##}");
        }

        private static Vector3 ToVector3(IVector3? v)
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

        private static _3dObject? GetAuthoritativeDrone(I3dObject obj)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
            {
                return null;
            }

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var aiObject = aiObjects[i];
                if (aiObject.ObjectId == obj.ObjectId)
                {
                    return aiObject;
                }
            }

            return null;
        }

        private static void SyncAuthoritativeDroneState(I3dObject source)
        {
            var authoritativeDrone = GetAuthoritativeDrone(source);
            if (authoritativeDrone == null || ReferenceEquals(authoritativeDrone, source))
            {
                return;
            }

            authoritativeDrone.WorldPosition = ToVector3(source.WorldPosition);
            authoritativeDrone.ObjectOffsets = ToVector3(source.ObjectOffsets);
            authoritativeDrone.Rotation = ToVector3(source.Rotation);

            // Sync impact status so minimap markers and other systems see the death state
            if (source.ImpactStatus != null && authoritativeDrone.ImpactStatus != null)
            {
                authoritativeDrone.ImpactStatus.HasExploded = source.ImpactStatus.HasExploded;
                authoritativeDrone.ImpactStatus.HasCrashed = source.ImpactStatus.HasCrashed;
            }
        }

        private static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;
            if (lenSq <= 1e-6f)
            {
                return new Vector3 { x = 0, y = 0, z = 0 };
            }

            float invLen = 1f / MathF.Sqrt(lenSq);
            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }

        private static float Length(Vector3 v)
        {
            return MathF.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        private static float Dot(Vector3 a, Vector3 b)
        {
            return (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        private static float MoveAngleTowards(float current, float target, float maxDelta)
        {
            float delta = NormalizeAngle(target - current);
            if (MathF.Abs(delta) <= maxDelta)
            {
                return current + delta;
            }

            return current + MathF.Sign(delta) * maxDelta;
        }

        private static Vector3 GetPartCenter(List<ITriangleMeshWithColor> triangles)
        {
            if (triangles == null || triangles.Count == 0)
            {
                return new Vector3();
            }

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var triangle in triangles)
            {
                var vertices = new[] { (Vector3)triangle.vert1, (Vector3)triangle.vert2, (Vector3)triangle.vert3 };
                foreach (var vertex in vertices)
                {
                    if (vertex.x < minX) minX = vertex.x;
                    if (vertex.y < minY) minY = vertex.y;
                    if (vertex.z < minZ) minZ = vertex.z;
                    if (vertex.x > maxX) maxX = vertex.x;
                    if (vertex.y > maxY) maxY = vertex.y;
                    if (vertex.z > maxZ) maxZ = vertex.z;
                }
            }

            return new Vector3
            {
                x = (minX + maxX) * 0.5f,
                y = (minY + maxY) * 0.5f,
                z = (minZ + maxZ) * 0.5f
            };
        }

        private static List<ITriangleMeshWithColor> TranslateMesh(List<ITriangleMeshWithColor> triangles, Vector3 offset)
        {
            var translated = new List<ITriangleMeshWithColor>(triangles.Count);

            foreach (var triangle in triangles)
            {
                translated.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    vert1 = new Vector3 { x = triangle.vert1.x + offset.x, y = triangle.vert1.y + offset.y, z = triangle.vert1.z + offset.z },
                    vert2 = new Vector3 { x = triangle.vert2.x + offset.x, y = triangle.vert2.y + offset.y, z = triangle.vert2.z + offset.z },
                    vert3 = new Vector3 { x = triangle.vert3.x + offset.x, y = triangle.vert3.y + offset.y, z = triangle.vert3.z + offset.z },
                    normal1 = new Vector3 { x = triangle.normal1.x, y = triangle.normal1.y, z = triangle.normal1.z },
                    normal2 = new Vector3 { x = triangle.normal2.x, y = triangle.normal2.y, z = triangle.normal2.z },
                    normal3 = new Vector3 { x = triangle.normal3.x, y = triangle.normal3.y, z = triangle.normal3.z }
                });
            }

            return translated;
        }

        private void RotateWheelAnimation(double deltaSeconds)
        {
            if (ParentObject == null)
            {
                return;
            }

            var wheel = ParentObject.ObjectParts.Find(part => part.PartName == WheelPartName);
            if (wheel?.Triangles == null || wheel.Triangles.Count == 0)
            {
                return;
            }

            _wheelRotationDegrees = NormalizeAngle(_wheelRotationDegrees + (WheelRotationDegreesPerSecond * (float)deltaSeconds));

            var wheelCenter = GetPartCenter(wheel.Triangles);
            var translatedToOrigin = TranslateMesh(wheel.Triangles, new Vector3
            {
                x = -wheelCenter.x,
                y = -wheelCenter.y,
                z = -wheelCenter.z
            });

            var rotated = _rotate.RotateXMesh(translatedToOrigin, _wheelRotationDegrees);
            wheel.Triangles = TranslateMesh(rotated, wheelCenter);
        }

        private static Vector3 GetLocalCrashCenter(I3dObject obj)
        {
            if (obj.CrashBoxes == null || obj.CrashBoxes.Count == 0)
            {
                return new Vector3();
            }

            var localPoints = new List<Vector3>();
            foreach (var box in obj.CrashBoxes)
            {
                foreach (var point in box)
                {
                    localPoints.Add((Vector3)point);
                }
            }

            return localPoints.Count > 0
                ? OmegaObjectHelpers.GetCenterOfBox(localPoints)
                : new Vector3();
        }

        private static Vector3 RotateLocalPoint(Vector3 point, IVector3? rotation)
        {
            if (rotation is not Vector3 rotationVector)
            {
                return point;
            }

            var rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.z, point, 'Z');
            rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.y, rotatedPoint, 'Y');
            rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.x, rotatedPoint, 'X');
            return rotatedPoint;
        }

        private static Vector3 GetRotatedLocalCrashCenter(I3dObject obj)
        {
            return RotateLocalPoint(GetLocalCrashCenter(obj), obj.Rotation);
        }

        private static Vector3 GetDroneCrashCenterWorldPosition(I3dObject obj)
        {
            var rotatedLocalCrashCenter = GetRotatedLocalCrashCenter(obj);

            var worldPosition = obj.WorldPosition;
            var objectOffsets = obj.ObjectOffsets;

            return new Vector3
            {
                x = (worldPosition?.x ?? 0f) + (objectOffsets?.x ?? 0f) + rotatedLocalCrashCenter.x,
                y = (worldPosition?.y ?? 0f) + (objectOffsets?.y ?? 0f) + rotatedLocalCrashCenter.y,
                z = (worldPosition?.z ?? 0f) + (objectOffsets?.z ?? 0f) + rotatedLocalCrashCenter.z
            };
        }

        private static Vector3? GetShipCrashCenterWorldPosition()
        {
            if (GameState.ShipState?.ShipCrashCenterWorldPosition is Vector3 shipCrashCenter)
            {
                return new Vector3
                {
                    x = shipCrashCenter.x - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeX / 2f),
                    y = shipCrashCenter.y,
                    z = shipCrashCenter.z
                };
            }

            if (GameState.ShipState?.ShipWorldPosition is Vector3 shipWorldPosition)
            {
                return new Vector3
                {
                    x = shipWorldPosition.x - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeX / 2f),
                    y = shipWorldPosition.y,
                    z = shipWorldPosition.z
                };
            }

            return null;
        }

        private void UpdateRotationTowardsTarget(Vector3 directionToTarget)
        {
            var normalizedDirection = Normalize(directionToTarget);
            if (normalizedDirection.x == 0 && normalizedDirection.y == 0 && normalizedDirection.z == 0)
            {
                return;
            }

            float headingDegrees = MathF.Atan2(normalizedDirection.x, normalizedDirection.z) * 180f / MathF.PI;
            float pitchDegrees = MathF.Atan2(-normalizedDirection.y, MathF.Sqrt(normalizedDirection.x * normalizedDirection.x + normalizedDirection.z * normalizedDirection.z)) * 180f / MathF.PI;

            TargetZrotation = 270f + headingDegrees;
            TargetXrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees + pitchDegrees;
            TargetYrotation = 0f;
        }

        private void UpdateCurrentRotation(double deltaSeconds)
        {
            float maxDelta = RotationDegreesPerSecond * (float)deltaSeconds;
            if (maxDelta <= 0f)
            {
                return;
            }

            Xrotation = MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
            Yrotation = MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
            Zrotation = MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets?.y ?? 0f;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY);
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
            {
                return;
            }

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.KamikazeDroneHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => EnemySetup.KamikazeDroneHealth,
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => currentHealth
            };

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            StartExplosion(theObject);
            theObject.ImpactStatus.HasCrashed = false;
        }

        private void StartExplosion(I3dObject theObject)
        {
            if (_isExploding)
            {
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

            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = theObject.WorldPosition as Vector3 ?? ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = theObject.ObjectOffsets as Vector3 ?? ToVector3(theObject.ObjectOffsets);

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, 200f);
            theObject.CrashBoxes = new List<List<IVector3>>();
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);

            ParentObject = theObject;

            if (!_pulseInitialized)
            {
                _pulseInitialized = true;
                _pulseBaseZ = theObject.ObjectOffsets?.z ?? 0f;
                _pulseStartTime = DateTime.Now;
                _isDeployedDecoy = theObject.WorldPosition is Vector3 deployedWorldPosition &&
                    (deployedWorldPosition.x != 0f || deployedWorldPosition.y != 0f || deployedWorldPosition.z != 0f);
                if (_isDeployedDecoy)
                {
                    _deployedAt = DateTime.Now;
                    _deployedWorldPosition = ToVector3(theObject.WorldPosition);
                }
            }

            if (_isDeployedDecoy)
            {
                // Launch rise: smoothly move upward (−y) over the first 2 seconds
                float elapsedSinceDeploy = (float)(DateTime.Now - _deployedAt).TotalSeconds;
                float riseProgress = Math.Clamp(elapsedSinceDeploy / LaunchRiseDurationSeconds, 0f, 1f);
                // Ease-out: fast at start, slows down
                float easedRise = 1f - (1f - riseProgress) * (1f - riseProgress);

                theObject.WorldPosition = new Vector3
                {
                    x = _deployedWorldPosition.x,
                    y = _deployedWorldPosition.y - easedRise * LaunchRiseUnits,
                    z = _deployedWorldPosition.z
                };
                SyncMovement(theObject);
            }
            else
            {
                theObject.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                SyncMovement(theObject);
            }

            var now = DateTime.Now;
            bool fuseElapsed = !_isDeployedDecoy || (now - _deployedAt).TotalSeconds >= DeployedDecoyLifetimeSeconds;

            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            if (_isDeployedDecoy && !_isExploding && fuseElapsed)
            {
                StartExplosion(theObject);
            }

            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                {
                    theObject.WorldPosition = _explosionWorldPosition;
                }

                if (_explosionObjectOffsets != null)
                {
                    theObject.ObjectOffsets = _explosionObjectOffsets;
                }

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                }

                SyncAuthoritativeDroneState(theObject);
                LastMovementDateTime = DateTime.Now;
                return theObject;
            }

            if (LastMovementDateTime == DateTime.MinValue)
            {
                LastMovementDateTime = now;
            }

            var deltaSeconds = (now - LastMovementDateTime).TotalSeconds;
            float pulsePhase = (float)((now - _pulseStartTime).TotalSeconds * PulseCyclesPerSecond * 2d * Math.PI);
            float pulseOffsetZ = MathF.Sin(pulsePhase) * PulseAmplitudeZ;

            theObject.ObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets?.x ?? 0f,
                y = theObject.ObjectOffsets?.y ?? 0f,
                z = _pulseBaseZ + pulseOffsetZ
            };

            RotateWheelAnimation(deltaSeconds);

            Zrotation = NormalizeAngle(Zrotation + (RotationDegreesPerSecond * 0.25f * (float)deltaSeconds));

            if (theObject.Rotation != null)
            {
                var rotation = theObject.Rotation;
                rotation.y = Yrotation;
                rotation.x = Xrotation;
                rotation.z = Zrotation;
            }

            SyncAuthoritativeDroneState(theObject);
            LastMovementDateTime = now;

            return theObject;
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            _audioConfigured = false;
            _isExploding = false;
            _syncInitialized = false;
            _syncY = 0;
            _trackedObjectId = -1;
            _storedWorldPositionInitialized = false;
            _storedWorldPosition = new Vector3();
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            _pulseInitialized = false;
            _pulseBaseZ = 0f;
            _pulseStartTime = DateTime.Now;
            _wheelRotationDegrees = 0f;
            _isDeployedDecoy = false;
            _deployedAt = DateTime.MinValue;
            _deployedWorldPosition = new Vector3();
            StartCoordinates = null;
            GuideCoordinates = null;
            DirectionVelocity = new Vector3 { x = 0, y = 0, z = 0 };
            LastDirectionUpdateDateTime = DateTime.MinValue;
            LastMovementDateTime = DateTime.MinValue;
            _audio = null;
            _explosionSound = null;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
            {
                return;
            }

            if (audioPlayer == null || soundRegistry == null)
            {
                return;
            }

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
            _audioConfigured = true;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }
    }
}
