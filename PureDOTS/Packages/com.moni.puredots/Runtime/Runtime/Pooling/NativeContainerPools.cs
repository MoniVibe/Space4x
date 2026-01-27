using System;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Pooling
{
    internal struct NativeListPool<T> where T : unmanaged
    {
        private NativeList<NativeList<T>> _storage;
        private Allocator _allocator;
        private int _borrowed;

        public void Initialise(int capacity, Allocator allocator)
        {
            _allocator = allocator;
            _storage = new NativeList<NativeList<T>>(math.max(1, capacity), allocator);
        }

        public NativeList<T> Borrow(int initialCapacity)
        {
            NativeList<T> list;
            if (_storage.IsCreated && _storage.Length > 0)
            {
                list = _storage[_storage.Length - 1];
                _storage.RemoveAtSwapBack(_storage.Length - 1);
            }
            else
            {
                list = new NativeList<T>(math.max(1, initialCapacity), _allocator);
            }

            list.Clear();
            EnsureCapacity(ref list, initialCapacity);
            _borrowed++;
            return list;
        }

        public void Return(ref NativeList<T> list)
        {
            list.Clear();
            if (!_storage.IsCreated)
            {
                _storage = new NativeList<NativeList<T>>(4, _allocator);
            }

            _storage.Add(list);
            _borrowed = math.max(0, _borrowed - 1);
        }

        public int BorrowedCount => _borrowed;
        public int AvailableCount => _storage.IsCreated ? _storage.Length : 0;

        public void Dispose()
        {
            if (_storage.IsCreated)
            {
                for (int i = 0; i < _storage.Length; i++)
                {
                    if (_storage[i].IsCreated)
                    {
                        _storage[i].Dispose();
                    }
                }
                _storage.Dispose();
            }

            _borrowed = 0;
        }

        private static void EnsureCapacity(ref NativeList<T> list, int capacity)
        {
            var desired = math.max(1, capacity);
            if (list.Capacity < desired)
            {
                list.Capacity = desired;
            }
        }
    }

    internal struct NativeQueuePool<T> where T : unmanaged
    {
        private NativeList<NativeQueue<T>> _storage;
        private Allocator _allocator;
        private int _borrowed;

        public void Initialise(int capacity, Allocator allocator)
        {
            _allocator = allocator;
            _storage = new NativeList<NativeQueue<T>>(math.max(1, capacity), allocator);
        }

        public NativeQueue<T> Borrow()
        {
            NativeQueue<T> queue;
            if (_storage.IsCreated && _storage.Length > 0)
            {
                queue = _storage[_storage.Length - 1];
                _storage.RemoveAtSwapBack(_storage.Length - 1);
            }
            else
            {
                queue = new NativeQueue<T>(_allocator);
            }

            queue.Clear();
            _borrowed++;
            return queue;
        }

        public void Return(ref NativeQueue<T> queue)
        {
            queue.Clear();
            if (!_storage.IsCreated)
            {
                _storage = new NativeList<NativeQueue<T>>(4, _allocator);
            }

            _storage.Add(queue);
            _borrowed = math.max(0, _borrowed - 1);
        }

        public int BorrowedCount => _borrowed;
        public int AvailableCount => _storage.IsCreated ? _storage.Length : 0;

        public void Dispose()
        {
            if (_storage.IsCreated)
            {
                for (int i = 0; i < _storage.Length; i++)
                {
                    if (_storage[i].IsCreated)
                    {
                        _storage[i].Dispose();
                    }
                }

                _storage.Dispose();
            }

            _borrowed = 0;
        }
    }

    internal static class NativeListPoolRegistry<T> where T : unmanaged
    {
        private static NativeListPool<T> s_pool;
        private static bool s_initialised;
        private static Allocator s_allocator;
        private static int s_defaultCapacity;

        public static void EnsureConfigured(Allocator allocator, int defaultCapacity)
        {
            if (s_initialised)
            {
                s_defaultCapacity = math.max(s_defaultCapacity, defaultCapacity);
                return;
            }

            s_allocator = allocator;
            s_defaultCapacity = math.max(4, defaultCapacity);
            s_pool = new NativeListPool<T>();
            s_pool.Initialise(s_defaultCapacity, allocator);
            s_initialised = true;
        }

        public static NativeList<T> Borrow(int requestedCapacity)
        {
            if (!s_initialised)
            {
                throw new InvalidOperationException($"NativeList pool for {typeof(T)} is not configured.");
            }

            var capacity = math.max(requestedCapacity, s_defaultCapacity);
            return s_pool.Borrow(capacity);
        }

        public static void Return(ref NativeList<T> list)
        {
            if (!s_initialised)
            {
                list.Dispose();
                return;
            }

            s_pool.Return(ref list);
        }

        public static (int Borrowed, int Available) GetStats()
        {
            if (!s_initialised)
            {
                return (0, 0);
            }

            return (s_pool.BorrowedCount, s_pool.AvailableCount);
        }

        public static void Reset()
        {
            if (!s_initialised)
            {
                return;
            }

            s_pool.Dispose();
            s_pool = default;
            s_defaultCapacity = 0;
            s_initialised = false;
        }
    }

    internal static class NativeQueuePoolRegistry<T> where T : unmanaged
    {
        private static NativeQueuePool<T> s_pool;
        private static bool s_initialised;
        private static Allocator s_allocator;
        private static int s_defaultCapacity;

        public static void EnsureConfigured(Allocator allocator, int defaultCapacity)
        {
            if (s_initialised)
            {
                s_defaultCapacity = math.max(s_defaultCapacity, defaultCapacity);
                return;
            }

            s_allocator = allocator;
            s_defaultCapacity = math.max(1, defaultCapacity);
            s_pool = new NativeQueuePool<T>();
            s_pool.Initialise(s_defaultCapacity, allocator);
            s_initialised = true;
        }

        public static NativeQueue<T> Borrow()
        {
            if (!s_initialised)
            {
                throw new InvalidOperationException($"NativeQueue pool for {typeof(T)} is not configured.");
            }

            return s_pool.Borrow();
        }

        public static void Return(ref NativeQueue<T> queue)
        {
            if (!s_initialised)
            {
                queue.Dispose();
                return;
            }

            s_pool.Return(ref queue);
        }

        public static (int Borrowed, int Available) GetStats()
        {
            if (!s_initialised)
            {
                return (0, 0);
            }

            return (s_pool.BorrowedCount, s_pool.AvailableCount);
        }

        public static void Reset()
        {
            if (!s_initialised)
            {
                return;
            }

            s_pool.Dispose();
            s_pool = default;
            s_defaultCapacity = 0;
            s_initialised = false;
        }
    }
}

