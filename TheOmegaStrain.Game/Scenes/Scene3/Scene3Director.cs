using CommonUtilities.CommonGlobalState;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Audio.Services;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Scene3
{
    public class Scene3Director : ISceneDirector
    {
        private const bool enableLogging = false;
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
            if (Logger.ShouldLog(enableLogging))
            {
                bool hasInactiveMotherShip = false;
                for (int i = 0; i < aiObjs.Count; i++)
                {
                    if (IsMotherShip(aiObjs[i].ObjectName) && !aiObjs[i].IsActive)
                    {
                        hasInactiveMotherShip = true;
                        break;
                    }
                }

                if (!hasInactiveMotherShip)
                {
                    Logger.Log($"Scene3 has no inactive mothership candidate. aiCount={aiObjs.Count}; mothers={GetMotherShipSummary(aiObjs)}", "Scene3Director");
                }
            }

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

            if (liveSeeders > 0 || liveDrones > 0)
            {
                if (Logger.ShouldLog(enableLogging))
                {
                    Logger.Log($"Scene3 blocked: liveSeeders={liveSeeders}; liveDrones={liveDrones}; blockers={GetBlockingEnemySummary(aiObjs)}", "Scene3Director");
                }
                return;
            }

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

            if (Logger.ShouldLog(enableLogging))
            {
                Logger.Log($"Scene3 MotherShip activation pass: activated={activated}; activeMotherShips={msCount}", "Scene3Director");
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

            if (Logger.ShouldLog(enableLogging))
            {
                Logger.Log("Scene3 victory reached", "Scene3Director");
            }
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

        private static string GetBlockingEnemySummary(List<_3dObject> aiObjs)
        {
            var seeders = new List<string>();
            var drones = new List<string>();

            for (int i = 0; i < aiObjs.Count; i++)
            {
                var obj = aiObjs[i];
                if (obj.ImpactStatus?.HasExploded == true)
                    continue;

                if (obj.ObjectName == "Seeder")
                    seeders.Add($"Seeder#{obj.ObjectId}");
                else if (obj.ObjectName == "KamikazeDrone" && obj.IsActive)
                    drones.Add($"Drone#{obj.ObjectId}");
            }

            string seedersPart = seeders.Count > 0 ? string.Join("|", seeders) : "none";
            string dronesPart = drones.Count > 0 ? string.Join("|", drones) : "none";
            return $"seeders={seedersPart}; drones={dronesPart}";
        }

        private static string GetMotherShipSummary(List<_3dObject> aiObjs)
        {
            var mothers = new List<string>();
            for (int i = 0; i < aiObjs.Count; i++)
            {
                var obj = aiObjs[i];
                if (!IsMotherShip(obj.ObjectName))
                    continue;

                bool exploded = obj.ImpactStatus?.HasExploded == true;
                mothers.Add($"{obj.ObjectName}#{obj.ObjectId}:active={obj.IsActive}:exploded={exploded}");
            }

            return mothers.Count > 0 ? string.Join("|", mothers) : "none";
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
