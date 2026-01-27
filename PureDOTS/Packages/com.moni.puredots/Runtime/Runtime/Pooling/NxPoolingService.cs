using System;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Pooling
{
    internal sealed class NxPoolingService : IDisposable
    {
        private NativeList<EntityCommandBuffer> _commandBuffers;
        private NativeList<bool> _commandBufferUsage;
        private Allocator _allocator;

        public bool IsCreated => _commandBuffers.IsCreated;

        public void Initialise(in PoolingSettingsConfig settings)
        {
            _allocator = Allocator.Persistent;

            NativeListPoolRegistry<byte>.EnsureConfigured(_allocator, settings.NativeListCapacity);
            NativeListPoolRegistry<int>.EnsureConfigured(_allocator, settings.NativeListCapacity);
            NativeQueuePoolRegistry<int>.EnsureConfigured(_allocator, settings.NativeQueueCapacity);

            _commandBuffers = new NativeList<EntityCommandBuffer>(settings.EcbPoolCapacity, _allocator);
            _commandBufferUsage = new NativeList<bool>(settings.EcbPoolCapacity, _allocator);

            for (int i = 0; i < settings.EcbPoolCapacity; i++)
            {
                _commandBuffers.Add(new EntityCommandBuffer(_allocator));
                _commandBufferUsage.Add(false);
            }
        }

        public NativeList<T> BorrowNativeList<T>(int capacity) where T : unmanaged => NativeListPoolRegistry<T>.Borrow(capacity);

        public void ReturnNativeList<T>(ref NativeList<T> list) where T : unmanaged => NativeListPoolRegistry<T>.Return(ref list);

        public NativeQueue<T> BorrowNativeQueue<T>() where T : unmanaged => NativeQueuePoolRegistry<T>.Borrow();

        public void ReturnNativeQueue<T>(ref NativeQueue<T> queue) where T : unmanaged => NativeQueuePoolRegistry<T>.Return(ref queue);

        public EntityCommandBuffer BorrowCommandBuffer()
        {
            for (int i = 0; i < _commandBuffers.Length; i++)
            {
                if (!_commandBufferUsage[i])
                {
                    RefreshCommandBuffer(i);
                    _commandBufferUsage[i] = true;
                    return _commandBuffers[i];
                }
            }

            var extra = new EntityCommandBuffer(_allocator);
            _commandBuffers.Add(extra);
            _commandBufferUsage.Add(true);
            return extra;
        }

        public void ReturnCommandBuffer(EntityCommandBuffer buffer)
        {
            for (int i = 0; i < _commandBuffers.Length; i++)
            {
                if (_commandBuffers[i].Equals(buffer))
                {
                    _commandBufferUsage[i] = false;
                    return;
                }
            }

            buffer.Dispose();
        }

        public PoolingDiagnostics GatherDiagnostics()
        {
            var statsByte = NativeListPoolRegistry<byte>.GetStats();
            var statsInt = NativeListPoolRegistry<int>.GetStats();
            var statsQueue = NativeQueuePoolRegistry<int>.GetStats();

            int borrowedEcb = 0;
            for (int i = 0; i < _commandBufferUsage.Length; i++)
            {
                if (_commandBufferUsage[i])
                {
                    borrowedEcb++;
                }
            }

            return new PoolingDiagnostics
            {
                NativeListsBorrowed = statsByte.Borrowed + statsInt.Borrowed,
                NativeListsAvailable = statsByte.Available + statsInt.Available,
                NativeQueuesBorrowed = statsQueue.Borrowed,
                NativeQueuesAvailable = statsQueue.Available,
                CommandBuffersBorrowed = borrowedEcb,
                CommandBuffersAvailable = math.max(0, _commandBuffers.Length - borrowedEcb),
                EntityPoolCount = 0,
                EntityInstancesAvailable = 0,
                EntityInstancesBorrowed = 0,
                PendingDisposals = 0
            };
        }

        public void ResetPools()
        {
            NativeListPoolRegistry<byte>.Reset();
            NativeListPoolRegistry<int>.Reset();
            NativeQueuePoolRegistry<int>.Reset();

            for (int i = 0; i < _commandBuffers.Length; i++)
            {
                if (_commandBufferUsage[i])
                {
                    RefreshCommandBuffer(i);
                    _commandBufferUsage[i] = false;
                }
            }
        }

        public void Dispose()
        {
            NativeListPoolRegistry<byte>.Reset();
            NativeListPoolRegistry<int>.Reset();
            NativeQueuePoolRegistry<int>.Reset();

            if (_commandBuffers.IsCreated)
            {
                for (int i = 0; i < _commandBuffers.Length; i++)
                {
                    _commandBuffers[i].Dispose();
                }
                _commandBuffers.Dispose();
            }

            if (_commandBufferUsage.IsCreated)
            {
                _commandBufferUsage.Dispose();
            }
        }

        private void RefreshCommandBuffer(int index)
        {
            var buffer = _commandBuffers[index];
            buffer.Dispose();
            _commandBuffers[index] = new EntityCommandBuffer(_allocator);
        }
    }
}

