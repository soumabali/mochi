using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MochiV2.Core.Events
{
    /// <summary>
    /// Type-safe in-process publish/subscribe event bus. PRD §11, DESIGN D-5.
    /// Thread-safe via <see cref="ConcurrentDictionary"/> of handler lists.
    /// Used to decouple modules (CursorPoller, FSM, AnimationManager, …).
    /// </summary>
    public sealed class EventBus
    {
        // One list of handlers per event type. The list itself is locked on
        // subscribe/unsubscribe; publish iterates a snapshot so handlers can be
        // removed mid-publish without invalidating the enumeration.
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

        /// <summary>
        /// Subscribe a handler for events of type <typeparamref name="T"/>.
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : notnull
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            var list = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribe a previously-registered handler. No-op if the handler was
        /// not registered. Returns true if a handler was removed.
        /// </summary>
        public bool Unsubscribe<T>(Action<T> handler) where T : notnull
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list)) return false;
            lock (list)
            {
                return list.Remove(handler);
            }
        }

        /// <summary>
        /// Publish <paramref name="event"/> to all subscribers of its type.
        /// Handlers are invoked synchronously on the publishing thread. A
        /// snapshot of the handler list is taken so unsubscribing during publish
        /// is safe. Exceptions in one handler do not block the others; the first
        /// exception is re-thrown after all handlers have been attempted.
        /// </summary>
        public void Publish<T>(T evt) where T : notnull
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;

            Delegate[] snapshot;
            lock (list)
            {
                snapshot = list.Count == 0 ? Array.Empty<Delegate>() : list.ToArray();
            }
            Exception? first = null;
            foreach (var del in snapshot)
            {
                try
                {
                    ((Action<T>)del).Invoke(evt);
                }
                catch (Exception ex)
                {
                    first ??= ex;
                }
            }
            if (first is not null) throw first;
        }

        /// <summary>
        /// Remove all subscribers of a specific event type. Returns the number
        /// of handlers removed.
        /// </summary>
        public int Clear<T>() where T : notnull
        {
            if (!_handlers.TryRemove(typeof(T), out var list)) return 0;
            lock (list) { return list.Count; }
        }

        /// <summary>
        /// Remove all subscribers for all event types. Intended for test
        /// teardown between tests.
        /// </summary>
        public void ClearAll()
        {
            // Drain by removing each key; avoids reallocating the dictionary.
            foreach (var key in _handlers.Keys)
            {
                _handlers.TryRemove(key, out _);
            }
        }

        /// <summary>Number of handlers currently registered for a type.</summary>
        public int HandlerCount<T>() where T : notnull
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return 0;
            lock (list) { return list.Count; }
        }
    }
}