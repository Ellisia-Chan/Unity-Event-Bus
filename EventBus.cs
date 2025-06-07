using System;
using System.Collections.Generic;

namespace EventSystem {
    public static class EventBus {
        // Dictionary storing ordered lists of delegates for each event type
        private static readonly Dictionary<Type, List<Delegate>> eventTable = new();

        // Subscribe to an event (both class and struct supported)
        public static void Subscribe<T>(Action<T> callback) {
            var type = typeof(T);

            if (!eventTable.TryGetValue(type, out var delegateList)) {
                delegateList = new List<Delegate>();
                eventTable[type] = delegateList;
            }

            delegateList.Add(callback);
        }

        // Unsubscribe from an event
        public static void Unsubscribe<T>(Action<T> callback) {
            var type = typeof(T);

            if (eventTable.TryGetValue(type, out var delegateList)) {
                delegateList.Remove(callback);

                // Clean up if list is empty
                if (delegateList.Count == 0) {
                    eventTable.Remove(type);
                }
            }
        }

        // Unsubscribe all listeners of a given event type
        public static void UnsubscribeAll<T>() {
            eventTable.Remove(typeof(T));
        }

        // Clear all events
        public static void ClearAll() {
            eventTable.Clear();
        }

        // Publish an event to all listeners
        public static void Publish<T>(T evt) {
            var type = typeof(T);

            if (eventTable.TryGetValue(type, out var delegateList)) {
                // Clone list to prevent modification during iteration
                var listeners = delegateList.ToArray();
                foreach (var del in listeners) {
                    ((Action<T>)del)?.Invoke(evt);
                }
            }
        }
    }
}
