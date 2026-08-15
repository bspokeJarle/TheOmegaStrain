namespace Domain
{
    public interface ISceneDirector
    {
        void Initialize(IGameEventBus eventBus, I3dWorld world);
        void Update();
        bool IsVictory { get; }
        bool IsDefeat { get; }
        void Dispose();
    }
}
