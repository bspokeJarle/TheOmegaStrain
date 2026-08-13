using CommonUtilities.CommonGlobalState;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Audio.Services;

namespace _3dRotations.Scenes.Scene4
{
    public class Scene4Director : ISceneDirector
    {
        private IGameEventBus? _eventBus;
        private I3dWorld? _world;
        private bool _dronesActivated;
        private bool _motherShipActivated;

        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }

        public void Initialize(IGameEventBus eventBus, I3dWorld world)
        {
            _eventBus = eventBus;
            _world = world;
            _dronesActivated = false;
            _motherShipActivated = IsRestoredMotherShipPhase();
            IsVictory = false;
            IsDefeat = false;
        }

        public void Update()
        {
            if (IsVictory || IsDefeat) return;

            CheckDroneActivation();
            CheckMotherShipActivation();
            CheckVictoryCondition();
        }

        private void CheckDroneActivation()
        {
            if (_dronesActivated) return;
            if (!GameState.GamePlayState.IsDecoyUnlocked) return;

            var aiObjs = GameState.SurfaceState.AiObjects;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (aiObjs[i].ObjectName == "KamikazeDrone" && !aiObjs[i].IsActive)
                    aiObjs[i].IsActive = true;
            }
            _dronesActivated = true;
        }

        private void CheckMotherShipActivation()
        {
            if (_motherShipActivated) return;

            var gps = GameState.GamePlayState;

            var aiObjs = GameState.SurfaceState.AiObjects;
            int liveSeeders = 0;
            int liveDrones = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (aiObjs[i].ImpactStatus?.HasExploded == true) continue;

                if (aiObjs[i].ObjectName == "Seeder")
                    liveSeeders++;
                else if (aiObjs[i].ObjectName == "KamikazeDrone" && aiObjs[i].IsActive)
                    liveDrones++;
            }

            if (liveSeeders > 0 || liveDrones > 0) return;

            bool activated = false;
            int msCount = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (IsMotherShip(aiObjs[i].ObjectName) && !aiObjs[i].IsActive)
                {
                    aiObjs[i].IsActive = true;
                    activated = true;
                }
                if (IsMotherShip(aiObjs[i].ObjectName) && aiObjs[i].IsActive)
                    msCount++;
            }

            if (activated)
            {
                gps.SeedersRemaining = liveSeeders;
                gps.DronesRemaining = liveDrones;
                gps.MotherShipsRemaining = msCount;
                gps.SaveCheckpoint();
                try
                {
                    GameStatePersistence.SaveGameState();
                    ShipAiVoiceService.Shared.RequestGameplaySaveConfirmation();
                }
                catch { }
                try { HighscoreService.SubmitFromGamePlay(gps); } catch { }
            }

            if (activated || msCount > 0)
                _motherShipActivated = true;
        }

        private void CheckVictoryCondition()
        {
            if (!_motherShipActivated) return;

            var gps = GameState.GamePlayState;
            if (gps.DronesRemaining != 0 || gps.SeedersRemaining != 0 || gps.MotherShipsRemaining != 0) return;

            var aiObjs = GameState.SurfaceState.AiObjects;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                var obj = aiObjs[i];
                if (obj.ImpactStatus?.HasExploded == true) continue;
                if (obj.ObjectName == "Seeder" || (obj.ObjectName == "KamikazeDrone" && obj.IsActive) || IsMotherShip(obj.ObjectName))
                    return;
            }

            IsVictory = true;
        }

        private static bool IsMotherShip(string objectName) =>
            objectName == "MotherShipSmall" || objectName == "MotherShipMedium" || objectName == "MotherShipLarge";

        private static bool IsRestoredMotherShipPhase()
        {
            var gps = GameState.GamePlayState;
            if (gps.SeedersRemaining != 0 || gps.DronesRemaining != 0)
                return false;
            if (gps.MotherShipsRemaining > 0 || gps.InitialMotherShips > 0)
                return true;

            var aiObjs = GameState.SurfaceState.AiObjects;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (IsMotherShip(aiObjs[i].ObjectName) && aiObjs[i].IsActive && aiObjs[i].ImpactStatus?.HasExploded != true)
                    return true;
            }

            return false;
        }

        public void Dispose()
        {
            _eventBus = null;
            _world = null;
            _dronesActivated = false;
            _motherShipActivated = false;
            IsVictory = false;
            IsDefeat = false;
        }
    }
}
