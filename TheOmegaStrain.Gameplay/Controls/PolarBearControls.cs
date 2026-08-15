using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class PolarBearControls : IObjectMovement
    {
        private const float BaseYRotation = 0f;
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float WalkRadiansPerSecond = 4.8f;
        private const float BodyBobAmplitude = 3.2f;
        private const float BodySwayAmplitude = 3.6f;
        private const float LegSwingAmplitude = 9.0f;
        private const float HeadNodAmplitude = 4.0f;
        private const float RightFacingZ = 0f;
        private const float LeftFacingZ = 180f;
        private const float TurnDegreesPerSecond = 220f;
        private const float PatrolSpeed = 65f;
        private const float PatrolHalfRange = 145f;
        private const float MaxVisualPatrolSpan = 180f;
        private const float SurfaceHeightBobAmplitude = 0.45f;
        private const float TurnPauseSeconds = 0.8f;
        private const float MouthOpenDegrees = 52f;
        private const float GrowlMouthTriggerDegrees = 7.5f;
        private const float GrowlMinIntervalSeconds = 3.5f;
        private const float TerrainLiftY = 8f;
        private const float RearUpYTilt = 32f;
        private static float TerrainLiftYScaled => TerrainLiftY * ScreenSetup.ScreenScaleY;
        private static int _visibleOnScreenBearId = -1;

        private readonly OmegaMeshRotation _rotate = new();
        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _baseTrianglesByPart = new();
        private readonly bool _hasPathBounds;
        private readonly float _minPathOffsetX;
        private readonly float _maxPathOffsetX;
        private DateTime _lastFrameUpdate = DateTime.MinValue;
        private float _walkTimeSeconds;
        private bool _baseInitialized;
        private float _baseOffsetX;
        private float _baseOffsetY;
        private float _baseOffsetZ;
        private float _currentOffsetX;
        private float _patrolMinX;
        private float _patrolMaxX;
        private int _walkDirection = 1;
        private int _pendingDirection = 1;
        private float _currentFacingZ;
        private float _targetFacingZ;
        private float _turnPauseRemainingSeconds;
        private bool _isTurning;
        private bool _growlPlayedThisPause;
        private DateTime _lastGrowlAt = DateTime.MinValue;

        private bool _audioConfigured;
        private IAudioPlayer? _audio;
        private SoundDefinition? _bearGrowlSound;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public PolarBearControls()
            : this(0f, 0f, hasPathBounds: false)
        {
        }

        public PolarBearControls(float minPathOffsetX, float maxPathOffsetX)
            : this(minPathOffsetX, maxPathOffsetX, hasPathBounds: true)
        {
        }

        private PolarBearControls(float minPathOffsetX, float maxPathOffsetX, bool hasPathBounds)
        {
            _minPathOffsetX = Math.Min(minPathOffsetX, maxPathOffsetX);
            _maxPathOffsetX = Math.Max(minPathOffsetX, maxPathOffsetX);
            _hasPathBounds = hasPathBounds && (_maxPathOffsetX - _minPathOffsetX) > 10f;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            InitializeBasePose(theObject);
            ConfigureAudio(audioPlayer, soundRegistry);

            if (!ShouldRenderBear(theObject))
            {
                SetObjectVisibility(theObject, false);
                return theObject;
            }

            SetObjectVisibility(theObject, true);

            float deltaSeconds = GetDeltaSeconds();
            _walkTimeSeconds += deltaSeconds;

            float bodyPhase = _walkTimeSeconds * WalkRadiansPerSecond;
            float bodyArc = MathF.Sin(bodyPhase);
            float legArc = MathF.Sin(bodyPhase + 0.55f);
            float headArc = MathF.Sin(bodyPhase + 1.1f);
            float mouthAngle = 0f;

            if (_turnPauseRemainingSeconds > 0f)
            {
                _turnPauseRemainingSeconds = MathF.Max(0f, _turnPauseRemainingSeconds - deltaSeconds);
                float pauseProgress = 1f - (_turnPauseRemainingSeconds / TurnPauseSeconds);
                pauseProgress = Math.Clamp(pauseProgress, 0f, 1f);
                float openClose = MathF.Sin(pauseProgress * MathF.PI);
                openClose = MathF.Pow(Math.Clamp(openClose, 0f, 1f), 0.72f);
                mouthAngle = openClose * MouthOpenDegrees;

                if (_turnPauseRemainingSeconds <= 0f)
                {
                    _isTurning = true;
                    _walkDirection = _pendingDirection;
                    _targetFacingZ = _walkDirection > 0 ? RightFacingZ : LeftFacingZ;
                }
            }
            else if (_isTurning)
            {
                _currentFacingZ = MoveTowardsAngle(_currentFacingZ, _targetFacingZ, TurnDegreesPerSecond * deltaSeconds);
                if (MathF.Abs(NormalizeAngle(_targetFacingZ - _currentFacingZ)) <= 0.1f)
                {
                    _currentFacingZ = _targetFacingZ;
                    _isTurning = false;
                }
            }
            else
            {
                _currentOffsetX += _walkDirection * PatrolSpeed * deltaSeconds;
                if (_currentOffsetX >= _patrolMaxX)
                {
                    _currentOffsetX = _patrolMaxX;
                    _pendingDirection = -1;
                    _turnPauseRemainingSeconds = TurnPauseSeconds;
                    _growlPlayedThisPause = false;
                }
                else if (_currentOffsetX <= _patrolMinX)
                {
                    _currentOffsetX = _patrolMinX;
                    _pendingDirection = 1;
                    _turnPauseRemainingSeconds = TurnPauseSeconds;
                    _growlPlayedThisPause = false;
                }
            }

            if (!_growlPlayedThisPause && mouthAngle >= GrowlMouthTriggerDegrees)
            {
                if ((DateTime.UtcNow - _lastGrowlAt).TotalSeconds >= GrowlMinIntervalSeconds)
                {
                    PlayBearGrowl(theObject);
                    _lastGrowlAt = DateTime.UtcNow;
                }

                _growlPlayedThisPause = true;
            }

            // Rear-up factor: follows the same sin arc as mouthAngle (0->1->0 over the pause).
            float rearUpFactor = mouthAngle / MouthOpenDegrees;

            var rotation = theObject.Rotation as Vector3 ?? new Vector3();
            float rearUpTilt = (_walkDirection > 0 ? -1f : 1f) * rearUpFactor * RearUpYTilt;
            rotation.y = BaseYRotation + (bodyArc * BodySwayAmplitude) + rearUpTilt;
            rotation.x = BaseXRotation;
            rotation.z = _currentFacingZ;
            theObject.Rotation = rotation;

            if (theObject.ObjectOffsets != null)
            {
                theObject.ObjectOffsets = new Vector3
                {
                    x = _currentOffsetX,
                    y = _baseOffsetY + LandBasedObjectSetup.GroundContactNudgeYScaled
                        - TerrainLiftYScaled
                        - (MathF.Abs(bodyArc) * SurfaceHeightBobAmplitude),
                    z = _baseOffsetZ
                };
            }

            float legSwing = (_turnPauseRemainingSeconds > 0f || _isTurning) ? 0f : (legArc * LegSwingAmplitude);
            ApplyPartAnimation(theObject, "PolarBearLegsAndPaws", legSwing, pivotY: 0f, pivotZ: 3.0f);
            ApplyPartAnimation(theObject, "PolarBearHead", -headArc * HeadNodAmplitude, pivotY: 0f, pivotZ: 13.0f);
            ApplyPartAnimation(theObject, "PolarBearNeck", -headArc * (HeadNodAmplitude * 0.6f), pivotY: 0f, pivotZ: 13.0f);
            ApplyPartAnimation(theObject, "PolarBearTail", bodyArc * (LegSwingAmplitude * 0.7f), pivotY: 0f, pivotZ: 11.0f);
            ApplyPartAnimation(theObject, "PolarBearUpperSnout", -mouthAngle * 0.22f, pivotY: 0f, pivotZ: 11.4f);
            ApplyPartAnimation(theObject, "PolarBearLowerJaw", mouthAngle, pivotY: 0f, pivotZ: 10.1f);

            return theObject;
        }

        private float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Clamp(GameState.DeltaTime, 0f, 0.1f);

            var now = DateTime.Now;
            float deltaSeconds = GameState.GameplayBaselineDeltaTime;
            if (_lastFrameUpdate != DateTime.MinValue)
                deltaSeconds = (float)(now - _lastFrameUpdate).TotalSeconds;

            _lastFrameUpdate = now;
            return Math.Clamp(deltaSeconds, 0f, 0.1f);
        }

        private void InitializeBasePose(I3dObject theObject)
        {
            if (_baseInitialized)
                return;

            _baseTrianglesByPart.Clear();
            for (int i = 0; i < theObject.ObjectParts.Count; i++)
            {
                var part = theObject.ObjectParts[i];
                if (part.PartName == null)
                    continue;

                if (part.PartName == "PolarBearLegsAndPaws"
                    || part.PartName == "PolarBearHead"
                    || part.PartName == "PolarBearNeck"
                    || part.PartName == "PolarBearTail"
                    || part.PartName == "PolarBearUpperSnout"
                    || part.PartName == "PolarBearLowerJaw")
                    _baseTrianglesByPart[part.PartName] = OmegaObjectHelpers.CopyTriangles(part.Triangles);
            }

            _baseOffsetX = theObject.ObjectOffsets?.x ?? 0f;
            _baseOffsetY = theObject.ObjectOffsets?.y ?? 0f;
            _baseOffsetZ = theObject.ObjectOffsets?.z ?? 0f;
            _currentOffsetX = _baseOffsetX;
            if (_hasPathBounds)
            {
                _patrolMinX = _minPathOffsetX;
                _patrolMaxX = _maxPathOffsetX;
                if (_patrolMaxX <= _patrolMinX)
                {
                    _patrolMinX = _baseOffsetX - (PatrolHalfRange * ScreenSetup.ScreenScaleX);
                    _patrolMaxX = _baseOffsetX + (PatrolHalfRange * ScreenSetup.ScreenScaleX);
                }

                float maxSpan = MaxVisualPatrolSpan * ScreenSetup.ScreenScaleX;
                if ((_patrolMaxX - _patrolMinX) > maxSpan)
                {
                    _patrolMinX = _baseOffsetX - (maxSpan * 0.5f);
                    _patrolMaxX = _baseOffsetX + (maxSpan * 0.5f);
                }
            }
            else
            {
                _patrolMinX = _baseOffsetX - (PatrolHalfRange * ScreenSetup.ScreenScaleX);
                _patrolMaxX = _baseOffsetX + (PatrolHalfRange * ScreenSetup.ScreenScaleX);
            }
            _walkDirection = 1;
            _pendingDirection = 1;
            _currentFacingZ = RightFacingZ;
            _targetFacingZ = RightFacingZ;
            _turnPauseRemainingSeconds = 0f;
            _isTurning = false;
            _growlPlayedThisPause = false;
            _baseInitialized = true;
        }

        private void PlayBearGrowl(I3dObject theObject)
        {
            if (_audio == null || _bearGrowlSound == null)
                return;

            var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();
            _audio.Play(
                _bearGrowlSound,
                AudioPlayMode.OneShot,
                new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private static bool ShouldRenderBear(I3dObject theObject)
        {
            if (!theObject.IsOnScreen)
            {
                if (_visibleOnScreenBearId == theObject.ObjectId)
                    _visibleOnScreenBearId = -1;
                return false;
            }

            if (_visibleOnScreenBearId == -1 || _visibleOnScreenBearId == theObject.ObjectId)
            {
                _visibleOnScreenBearId = theObject.ObjectId;
                return true;
            }

            return false;
        }

        private static void SetObjectVisibility(I3dObject theObject, bool visible)
        {
            for (int i = 0; i < theObject.ObjectParts.Count; i++)
            {
                theObject.ObjectParts[i].IsVisible = visible;
            }
        }

        private static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = NormalizeAngle(target - current);
            if (MathF.Abs(delta) <= maxDelta)
                return NormalizeAngle(target);

            current += MathF.Sign(delta) * maxDelta;
            return NormalizeAngle(current);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle <= -180f) angle += 360f;
            return angle;
        }

        private void ApplyPartAnimation(I3dObject theObject, string partName, float angleDegrees, float pivotY, float pivotZ)
        {
            if (!_baseTrianglesByPart.TryGetValue(partName, out var baseTriangles))
                return;

            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part == null)
                return;

            var shifted = OmegaObjectHelpers.CopyTriangles(baseTriangles);
            TranslateTrianglesYZ(shifted, -pivotY, -pivotZ);
            var rotated = _rotate.RotateXMesh(shifted, angleDegrees);
            TranslateTrianglesYZ(rotated, pivotY, pivotZ);
            part.Triangles = rotated;
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

        public void ReleaseParticles()
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            string[] candidateIds =
            {
                "bear_growl",
                "polarbear_growl",
                "polar_bear_growl",
                "beargrowl",
                "bearGrowl"
            };

            for (int i = 0; i < candidateIds.Length; i++)
            {
                if (soundRegistry.TryGet(candidateIds[i], out var growlSound))
                {
                    _bearGrowlSound = growlSound;
                    break;
                }
            }

            _audioConfigured = true;
        }

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
            _lastFrameUpdate = DateTime.MinValue;
            _walkTimeSeconds = 0f;
            _baseInitialized = false;
            _baseOffsetX = 0f;
            _baseOffsetY = 0f;
            _baseOffsetZ = 0f;
            _currentOffsetX = 0f;
            _patrolMinX = 0f;
            _patrolMaxX = 0f;
            _walkDirection = 1;
            _pendingDirection = 1;
            _currentFacingZ = 0f;
            _targetFacingZ = 0f;
            _turnPauseRemainingSeconds = 0f;
            _isTurning = false;
            _growlPlayedThisPause = false;
            _lastGrowlAt = DateTime.MinValue;
            _audioConfigured = false;
            _audio = null;
            _bearGrowlSound = null;
            _baseTrianglesByPart.Clear();
            _visibleOnScreenBearId = -1;
        }
    }
}
