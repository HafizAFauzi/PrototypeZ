using System;
using System.Collections.Generic;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Type-safe, high-performance static Event Bus for inter-module communication.
    /// Allows decoupled communication between GameManager, ExperimentManager, CombatManager,
    /// TelemetryService, and UI components without direct references.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Subscribers = new();

        /// <summary>
        /// Subscribes a listener to events of type T.
        /// </summary>
        public static void Subscribe<T>(Action<T> listener)
        {
            if (listener == null) return;

            Type type = typeof(T);
            if (!Subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                Subscribers[type] = list;
            }

            if (!list.Contains(listener))
            {
                list.Add(listener);
            }
        }

        /// <summary>
        /// Unsubscribes a listener from events of type T.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> listener)
        {
            if (listener == null) return;

            Type type = typeof(T);
            if (Subscribers.TryGetValue(type, out var list))
            {
                list.Remove(listener);
                if (list.Count == 0)
                {
                    Subscribers.Remove(type);
                }
            }
        }

        /// <summary>
        /// Publishes an event of type T to all subscribed listeners.
        /// </summary>
        public static void Publish<T>(T eventData)
        {
            Type type = typeof(T);
            if (Subscribers.TryGetValue(type, out var list))
            {
                // Create a temporary copy to prevent issues if a subscriber modifies the list during execution
                var listenersCopy = list.ToArray();
                for (int i = 0; i < listenersCopy.Length; i++)
                {
                    try
                    {
                        ((Action<T>)listenersCopy[i])?.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[EventBus] Exception while invoking listener for event {typeof(T).Name}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Clears all event subscriptions of type T.
        /// </summary>
        public static void Clear<T>()
        {
            Subscribers.Remove(typeof(T));
        }

        /// <summary>
        /// Clears all event subscriptions across all types.
        /// Useful during scene unloads, session resets, or unit testing.
        /// </summary>
        public static void ClearAll()
        {
            Subscribers.Clear();
        }
    }
}
