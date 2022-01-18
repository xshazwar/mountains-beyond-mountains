using System;
using System.Collections.Concurrent;

namespace xshazwar.Generic {
        public class ObjectPool<T>{
        protected readonly ConcurrentBag<T> _objects;
        protected readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

        public void Return(T item) => _objects.Add(item);
    }
}

