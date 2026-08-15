using Domain;
using System;
using System.Collections.Generic;

namespace CommonUtilities.Events
{
    public class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<GameEventType, List<Action<IGameEvent>>> _handlers = new();

        public void Publish(IGameEvent gameEvent)
        {
            if (!_handlers.TryGetValue(gameEvent.Type, out var handlers))
                return;

            // Snapshot the list to allow safe subscribe/unsubscribe during dispatch
            var snapshot = new List<Action<IGameEvent>>(handlers);
            foreach (var handler in snapshot)
            {
                handler(gameEvent);
            }
        }

        public void Subscribe(GameEventType eventType, Action<IGameEvent> handler)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Action<IGameEvent>>();
                _handlers[eventType] = handlers;
            }
            handlers.Add(handler);
        }

        public void Unsubscribe(GameEventType eventType, Action<IGameEvent> handler)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
