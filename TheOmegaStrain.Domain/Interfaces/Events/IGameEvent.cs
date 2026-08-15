namespace TheOmegaStrain.Domain
{
    public interface IGameEvent
    {
        GameEventType Type { get; }
        I3dObject? Source { get; }
        string? ObjectName { get; }
        bool HasPowerUp { get; }
        PowerUpType PowerUpType { get; }
        SceneTypes SceneType { get; }
        int SceneIndex { get; }
        long Score { get; }
        int AwardedScore { get; }
        string? StyleBonusType { get; }
        int TotalKills { get; }
        int TotalShotsFired { get; }
        int TotalDeaths { get; }
        float Accuracy { get; }
        int PowerUpsCollected { get; }
        int SpeedPowerUpLevel { get; }
        bool HadCollision { get; }
    }
}
