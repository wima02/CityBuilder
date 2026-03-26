// ================================================================
// FILE: ObjectPool.cs
// Generic object pool — avoids GC pressure for frequently
// created/destroyed objects (agents, road nodes, UI elements).
// Refine: wire up to AgentSpawnerSystem for citizen entities.
// ================================================================
using System;
using System.Collections.Generic;

namespace CityBuilder.Utils
{
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T>  _available;
        private readonly Func<T>   _factory;
        private readonly Action<T> _onGet;    // reset state before reuse
        private readonly Action<T> _onReturn; // optional cleanup on return

        public int TotalCreated { get; private set; }
        public int AvailableCount => _available.Count;

        public ObjectPool(
            Func<T>   factory,
            Action<T> onGet    = null,
            Action<T> onReturn = null,
            int       initialSize = 0)
        {
            _factory  = factory  ?? throw new ArgumentNullException(nameof(factory));
            _onGet    = onGet;
            _onReturn = onReturn;
            _available = new Stack<T>(initialSize);

            // Pre-warm the pool
            for (int i = 0; i < initialSize; i++)
                _available.Push(_CreateNew());
        }

        /// Retrieves an instance from the pool, creating one if empty.
        public T Get()
        {
            T item = _available.Count > 0 ? _available.Pop() : _CreateNew();
            _onGet?.Invoke(item);
            return item;
        }

        /// Returns an instance to the pool for reuse.
        public void Return(T item)
        {
            if (item == null) return;
            _onReturn?.Invoke(item);
            _available.Push(item);
        }

        private T _CreateNew()
        {
            TotalCreated++;
            return _factory();
        }
    }
}
