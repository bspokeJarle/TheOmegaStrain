using CommonUtilities.CommonGlobalState;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Audio.Services;

namespace _3dRotations.Scenes.SceneSimulation
{
    public class SceneSimulationDirector : ISceneDirector
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
            _motherShipActivated = false;
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

            var aiObjs = GameState.SurfaceState.AiObjects;
            int liveSeeders = 0;
            int liveDrones = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (aiObjs[i].ImpactStatus?.HasExploded == true) continue;
                if (aiObjs[i].ObjectName == "Seeder") liveSeeders++;
                else if (aiObjs[i].ObjectName == "KamikazeDrone" && aiObjs[i].IsActive) liveDrones++;
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
                var gps = GameState.GamePlayState;
                gps.SeedersRemaining = liveSeeders;
                gps.DronesRemaining = liveDrones;
                gps.MotherShipsRemaining = msCount;
                gps.ShowMotherShipHealthBar = true;
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
            {
                _motherShipActivated = true;
                GameState.GamePlayState.ShowMotherShipHealthBar = true;
            }
        }

        private void CheckVictoryCondition()
        {
            var aiObjs = GameState.SurfaceState.AiObjects;
            int liveEnemies = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (aiObjs[i].ImpactStatus?.HasExploded == true) continue;
                var name = aiObjs[i].ObjectName;
                if (name == "Seeder" || name == "KamikazeDrone" || IsMotherShip(name))
                    liveEnemies++;
            }

            if (liveEnemies == 0 && _motherShipActivated)
            {
                IsVictory = true;
                GameState.GamePlayState.Phase = GamePhase.Outro;
            }
        }

        private static bool IsMotherShip(string name) =>
            name == "MotherShipSmall" || name == "MotherShipMedium" || name == "MotherShipLarge";

        public void Dispose()
        {
            _eventBus = null;
            _world = null;
        }
    }
}
