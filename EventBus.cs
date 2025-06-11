using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Buffers;
using UnityEngine;

namespace EventSystem {
    public static class EventBus {
        // Thread-safe dictionary storing ordered lists of delegates for each event type
        private static readonly ConcurrentDictionary<Type, List<Delegate>> eventTable = new();
        
        // Locks for thread-safe list operations
        private static readonly ConcurrentDictionary<Type, object> typeLocks = new();
        
        // Subscribe to an event (both class and struct supported)
        public static void Subscribe<T>(Action<T> callback) {
            if (callback == null) return;
            
            var type = typeof(T);
            var lockObj = typeLocks.GetOrAdd(type, _ => new object());
            
            lock (lockObj) {
                var delegateList = eventTable.GetOrAdd(type, _ => new List<Delegate>());
                delegateList.Add(callback);
            }
        }
        
        // Unsubscribe from an event
        public static void Unsubscribe<T>(Action<T> callback) {
            if (callback == null) return;
            
            var type = typeof(T);
            if (!typeLocks.TryGetValue(type, out var lockObj)) return;
            
            lock (lockObj) {
                if (eventTable.TryGetValue(type, out var delegateList)) {
                    delegateList.Remove(callback);
                    
                    // Clean up if list is empty
                    if (delegateList.Count == 0) {
                        eventTable.TryRemove(type, out _);
                        typeLocks.TryRemove(type, out _);
                    }
                }
            }
        }
        
        // Unsubscribe all listeners of a given event type
        public static void UnsubscribeAll<T>() {
            var type = typeof(T);
            eventTable.TryRemove(type, out _);
            typeLocks.TryRemove(type, out _);
        }
        
        // Clear all events
        public static void ClearAll() {
            eventTable.Clear();
            typeLocks.Clear();
        }
        
        // Publish an event to all listeners with error handling and performance optimization
        public static void Publish<T>(T evt) {
            var type = typeof(T);
            
            if (!eventTable.TryGetValue(type, out var delegateList)) return;
            if (!typeLocks.TryGetValue(type, out var lockObj)) return;
            
            Delegate[] listeners;
            
            // Create snapshot under lock
            lock (lockObj) {
                if (delegateList.Count == 0) return;
                
                // Use ArrayPool for better performance with frequent events
                var pool = ArrayPool<Delegate>.Shared;
                listeners = pool.Rent(delegateList.Count);
                
                try {
                    delegateList.CopyTo(listeners, 0);
                    var actualCount = delegateList.Count;
                    
                    // Invoke listeners outside of lock to prevent deadlocks
                    for (int i = 0; i < actualCount; i++) {
                        try {
                            if (listeners[i] is Action<T> action) {
                                action.Invoke(evt);
                            }
                        }
                        catch (Exception ex) {
                            // Log the exception but continue with other listeners
                            Debug.LogError($"Exception in event listener for {type.Name}: {ex}");
                        }
                    }
                }
                finally {
                    // Always return the array to the pool
                    pool.Return(listeners, clearArray: true);
                }
            }
        }
        
        // Unity-specific: Subscribe with automatic cleanup when MonoBehaviour is destroyed
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) {
            if (owner == null || callback == null) return;
            
            Subscribe(callback);
            
            // Create a wrapper that automatically unsubscribes when the MonoBehaviour is destroyed
            owner.StartCoroutine(UnsubscribeOnDestroy(owner, callback));
        }
        
        // Coroutine to handle automatic cleanup
        private static System.Collections.IEnumerator UnsubscribeOnDestroy<T>(MonoBehaviour owner, Action<T> callback) {
            yield return new WaitUntil(() => owner == null);
            Unsubscribe(callback);
        }
        
        // Get subscriber count for debugging/monitoring
        public static int GetSubscriberCount<T>() {
            var type = typeof(T);
            if (!eventTable.TryGetValue(type, out var delegateList)) return 0;
            if (!typeLocks.TryGetValue(type, out var lockObj)) return 0;
            
            lock (lockObj) {
                return delegateList?.Count ?? 0;
            }
        }
        
        // Check if there are any subscribers for an event type
        public static bool HasSubscribers<T>() {
            return GetSubscriberCount<T>() > 0;
        }
    }
}
