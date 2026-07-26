using CommonUtilities.CommonGlobalState;
using CommonUtilities.Events;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Controls.KamikazeDroneControls;
using System;

namespace _3dRotations.Scenes.Tutorial
{
    public class TutorialSceneDirector : ISceneDirector
    {
        private IGameEventBus? _eventBus;
        private I3dWorld? _world;
        private bool _dronesActivated;

        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }

        public void Initialize(IGameEventBus eventBus, I3dWorld world)
        {
            _eventBus = eventBus;
            _world = world;
            _dronesActivated = false;
            IsVictory = false;
            IsDefeat = false;
        }

        public void Update()
        {
            if (IsVictory || IsDefeat)
                return;

            ActivateDroneWhenReady();
            CheckVictoryCondition();
        }

        private void ActivateDroneWhenReady()
        {
            if (_dronesActivated)
                return;

            if (!GameState.GamePlayState.IsDecoyUnlocked)
                return;

            if (!GameState.TutorialState.DecoySelectCueSpoken)
                return;

            if (CountLiveSeeders() > 0)
                return;

            var aiObjs = GameState.SurfaceState.AiObjects;
            int activeDrones = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                var obj = aiObjs[i];
                if (obj.ObjectName != "KamikazeDrone")
                    continue;

                if (!obj.IsActive)
                    obj.IsActive = true;

                if (obj.Movement is KamikazeDroneControls droneControls)
                    droneControls.StartHuntDateTime = DateTime.Now;

                activeDrones++;
            }

            if (activeDrones <= 0)
                return;

            GameState.GamePlayState.SeedersRemaining = 0;
            GameState.GamePlayState.DronesRemaining = activeDrones;
            _dronesActivated = true;
        }

        private void CheckVictoryCondition()
        {
            if (!GameState.TutorialState.CompleteCueSpoken)
                return;

            if (CountLiveSeeders() > 0 || CountLiveDrones() > 0)
                return;

            TutorialProgressService.MarkTutorialCompleted(GameState.GamePlayState.PlayerName);
            PublishTrainingCompleted();
            IsVictory = true;
        }

        private void PublishTrainingCompleted()
        {
            var gameplay = GameState.GamePlayState;
            _eventBus?.Publish(new GameEvent
            {
                Type = GameEventType.SceneCompleted,
                ObjectName = SceneTypes.Tutorial.ToString(),
                SceneType = SceneTypes.Tutorial,
                SceneIndex = gameplay.SceneIndex,
                Score = gameplay.Score,
                TotalKills = gameplay.TotalKills,
                TotalShotsFired = gameplay.TotalShotsFired,
                TotalDeaths = gameplay.TotalDeaths,
                Accuracy = gameplay.Accuracy,
                PowerUpsCollected = gameplay.PowerUpsCollected,
                SpeedPowerUpLevel = gameplay.SpeedPowerUpLevel
            });
        }

        private static int CountLiveSeeders()
        {
            var aiObjs = GameState.SurfaceState.AiObjects;
            int count = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (IsLiveEnemy(aiObjs[i], "Seeder", requireActive: true))
                    count++;
            }

            return count;
        }

        private static int CountLiveDrones()
        {
            var aiObjs = GameState.SurfaceState.AiObjects;
            int count = 0;
            for (int i = 0; i < aiObjs.Count; i++)
            {
                if (IsLiveEnemy(aiObjs[i], "KamikazeDrone", requireActive: true))
                    count++;
            }

            return count;
        }

        private static bool IsLiveEnemy(I3dObject obj, string objectName, bool requireActive)
        {
            if (obj.ObjectName != objectName)
                return false;

            if (requireActive && !obj.IsActive)
                return false;

            if (obj.ImpactStatus?.HasExploded == true)
                return false;

            if ((obj.ImpactStatus?.ObjectHealth ?? 1) <= 0)
                return false;

            return true;
        }

        public void Dispose()
        {
            _eventBus = null;
            _world = null;
            _dronesActivated = false;
            IsVictory = false;
            IsDefeat = false;
        }
    }
}
