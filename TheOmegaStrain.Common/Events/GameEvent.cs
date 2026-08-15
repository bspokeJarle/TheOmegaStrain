using Domain;

namespace CommonUtilities.Events
{
    public class GameEvent : IGameEvent
    {
        public GameEventType Type { get; init; }
        public I3dObject? Source { get; init; }
        public string? ObjectName { get; init; }
        public bool HasPowerUp { get; init; }
        public PowerUpType PowerUpType { get; init; } = PowerUpType.Standard;
        public SceneTypes SceneType { get; init; } = SceneTypes.Intro;
        public int SceneIndex { get; init; }
        public long Score { get; init; }
        public int AwardedScore { get; init; }
        public string? StyleBonusType { get; init; }
        public int TotalKills { get; init; }
        public int TotalShotsFired { get; init; }
        public int TotalDeaths { get; init; }
        public float Accuracy { get; init; }
        public int PowerUpsCollected { get; init; }
        public int SpeedPowerUpLevel { get; init; }
        public bool HadCollision { get; init; }
    }
}
