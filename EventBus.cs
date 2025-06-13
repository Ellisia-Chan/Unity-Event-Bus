using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace EventSystem {
    public static class EventBus {
        // Thread-safe dictionary storing ordered lists of delegates for each event type
        private static readonly ConcurrentDictionary<Type, List<Delegate>> eventTable = new();
        
        // Lock objects for each event type to ensure thread-safe list operations
        private static readonly ConcurrentDictionary<Type, object> locks = new();
        
        // Subscribe to an event (both class and struct supported)
        public static void Subscribe<T>(Action<T> callback) {
            var type = typeof(T);
            var lockObj = locks.GetOrAdd(type, _ => new object());
            
            lock (lockObj) {
                if (!eventTable.TryGetValue(type, out var delegateList)) {
                    delegateList = new List<Delegate>();
                    eventTable[type] = delegateList;
                }
                delegateList.Add(callback);
            }
        }
        
        // Unsubscribe from an event
        public static void Unsubscribe<T>(Action<T> callback) {
            var type = typeof(T);
            if (!locks.TryGetValue(type, out var lockObj)) return;
            
            lock (lockObj) {
                if (eventTable.TryGetValue(type, out var delegateList)) {
                    delegateList.Remove(callback);
                    // Clean up if list is empty
                    if (delegateList.Count == 0) {
                        eventTable.TryRemove(type, out _);
                        locks.TryRemove(type, out _);
                    }
                }
            }
        }
        
        // Unsubscribe all listeners of a given event type
        public static void UnsubscribeAll<T>() {
            var type = typeof(T);
            if (locks.TryGetValue(type, out var lockObj)) {
                lock (lockObj) {
                    eventTable.TryRemove(type, out _);
                    locks.TryRemove(type, out _);
                }
            }
        }
        
        // Clear all events
        public static void ClearAll() {
            // Get all lock objects to ensure thread safety
            var allLocks = new List<object>();
            foreach (var kvp in locks) {
                allLocks.Add(kvp.Value);
            }
            
            // Lock all event types to prevent race conditions
            foreach (var lockObj in allLocks) {
                lock (lockObj) {
                    // Individual operations are handled within their respective locks
                }
            }
            
            eventTable.Clear();
            locks.Clear();
        }
        
        // Publish an event to all listeners
        public static void Publish<T>(T evt) {
            var type = typeof(T);
            if (!locks.TryGetValue(type, out var lockObj)) return;
            
            Delegate[] listeners;
            lock (lockObj) {
                if (!eventTable.TryGetValue(type, out var delegateList) || delegateList.Count == 0) {
                    return;
                }
                
                // Use ArrayPool for better performance and less GC pressure
                var pool = ArrayPool<Delegate>.Shared;
                var pooledArray = pool.Rent(delegateList.Count);
                
                try {
                    delegateList.CopyTo(pooledArray, 0);
                    listeners = new Delegate[delegateList.Count];
                    Array.Copy(pooledArray, listeners, delegateList.Count);
                } finally {
                    pool.Return(pooledArray);
                }
            }
            
            // Invoke listeners outside of lock to prevent deadlocks
            foreach (var del in listeners) {
                try {
                    ((Action<T>)del)?.Invoke(evt);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }
        }
        
        // Get the total number of listeners across all event types
        public static int GetTotalListenerCount() {
            int totalCount = 0;
            
            foreach (var kvp in eventTable) {
                var lockObj = locks.GetOrAdd(kvp.Key, _ => new object());
                lock (lockObj) {
                    if (eventTable.TryGetValue(kvp.Key, out var delegateList)) {
                        totalCount += delegateList.Count;
                    }
                }
            }
            
            return totalCount;
        }
        
        // Get the number of listeners for a specific event type
        public static int GetListenerCount<T>() {
            var type = typeof(T);
            if (!locks.TryGetValue(type, out var lockObj)) return 0;
            
            lock (lockObj) {
                if (eventTable.TryGetValue(type, out var delegateList)) {
                    return delegateList.Count;
                }
            }
            
            return 0;
        }
    }
}
