using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls.SeederControls;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class TutorialSeederControls : IObjectMovement
    {
        private const float IdleSpinDegreesPerSecond = 45f;
        private const float SyncFactorY = SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY;

        private readonly SeederControls.SeederControls _combatControls = new();
        private bool _syncInitialized;
        private float _syncY;
        private float _zRotation;
        private bool _useCombatControls;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_useCombatControls)
            {
                return _combatControls.MoveObject(theObject, audioPlayer, soundRegistry);
            }

            if (theObject.ImpactStatus?.HasCrashed == true)
            {
                SyncMovement(theObject);
                SyncToOriginal(theObject);

                _combatControls.ConfigureAudio(audioPlayer, soundRegistry);
                _combatControls.HandleCrash(theObject);

                if (theObject.ImpactStatus?.ObjectHealth <= 0 || theObject.ImpactStatus?.HasExploded == true)
                {
                    _useCombatControls = true;
                    return _combatControls.MoveObject(theObject, audioPlayer, soundRegistry);
                }

                return theObject;
            }

            ApplyIdlePose(theObject);
            SyncMovement(theObject);
            SyncToOriginal(theObject);
            return theObject;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            _combatControls.ConfigureAudio(audioPlayer, soundRegistry);
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            _combatControls.ReleaseParticles(theObject);
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            StartCoordinates = StartCoord;
            GuideCoordinates = GuideCoord;
            _combatControls.SetParticleGuideCoordinates(StartCoord, GuideCoord);
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void Dispose()
        {
            _combatControls.Dispose();
            StartCoordinates = null;
            GuideCoordinates = null;
            _syncInitialized = false;
            _syncY = 0f;
            _zRotation = 0f;
            _useCombatControls = false;
        }

        private void ApplyIdlePose(I3dObject theObject)
        {
            theObject.Rotation ??= new _3dSpecificsImplementations.Vector3();
            float dt = GameState.DeltaTime > 0f ? GameState.DeltaTime : GameState.GameplayBaselineDeltaTime;
            _zRotation += IdleSpinDegreesPerSecond * dt;

            theObject.Rotation.x = 90f;
            theObject.Rotation.y = 0f;
            theObject.Rotation.z = _zRotation;
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets?.y ?? 0f;
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

                    original.WorldPosition = new _3dSpecificsImplementations.Vector3
                    {
                        x = deepCopy.WorldPosition.x,
                        y = deepCopy.WorldPosition.y,
                        z = deepCopy.WorldPosition.z
                    };
                    original.ObjectOffsets = new _3dSpecificsImplementations.Vector3
                    {
                        x = deepCopy.ObjectOffsets.x,
                        y = deepCopy.ObjectOffsets.y,
                        z = deepCopy.ObjectOffsets.z
                    };
                    return;
                }
            }
        }
    }
}
