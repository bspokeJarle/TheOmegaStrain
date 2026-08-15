using System;

namespace TheOmegaStrain.Domain
{
    public interface IGameEventBus
    {
        void Publish(IGameEvent gameEvent);
        void Subscribe(GameEventType eventType, Action<IGameEvent> handler);
        void Unsubscribe(GameEventType eventType, Action<IGameEvent> handler);
        void Clear();
    }
}
