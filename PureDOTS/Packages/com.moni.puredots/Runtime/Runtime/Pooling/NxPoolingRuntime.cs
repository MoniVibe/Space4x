using System;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Pooling
{
    public static class NxPoolingRuntime
    {
        private static NxPoolingService _service;
        private static bool _initialised;

        public static bool IsInitialised => _initialised && _service != null && _service.IsCreated;

        public static void Initialise(in PoolingSettingsConfig config)
        {
            if (IsInitialised)
            {
                return;
            }

            _service = new NxPoolingService();
            _service.Initialise(config);
            _initialised = true;
        }

        public static NativeList<T> BorrowNativeList<T>(int capacity) where T : unmanaged
        {
            EnsureInitialised();
            return _service.BorrowNativeList<T>(capacity);
        }

        public static void ReturnNativeList<T>(ref NativeList<T> list) where T : unmanaged
        {
            if (!IsInitialised)
            {
                list.Dispose();
                return;
            }

            _service.ReturnNativeList(ref list);
        }

        public static NativeQueue<T> BorrowNativeQueue<T>() where T : unmanaged
        {
            EnsureInitialised();
            return _service.BorrowNativeQueue<T>();
        }

        public static void ReturnNativeQueue<T>(ref NativeQueue<T> queue) where T : unmanaged
        {
            if (!IsInitialised)
            {
                queue.Dispose();
                return;
            }

            _service.ReturnNativeQueue(ref queue);
        }

        public static EntityCommandBuffer BorrowCommandBuffer()
        {
            EnsureInitialised();
            return _service.BorrowCommandBuffer();
        }

        public static void ReturnCommandBuffer(EntityCommandBuffer buffer)
        {
            if (!IsInitialised)
            {
                buffer.Dispose();
                return;
            }

            _service.ReturnCommandBuffer(buffer);
        }

        public static PoolingDiagnostics GatherDiagnostics()
        {
            return IsInitialised ? _service.GatherDiagnostics() : default;
        }

        public static void ResetPools()
        {
            if (IsInitialised)
            {
                _service.ResetPools();
            }
        }

        public static void Dispose()
        {
            if (!IsInitialised)
            {
                return;
            }

            _service.Dispose();
            _service = null;
            _initialised = false;
        }

        private static void EnsureInitialised()
        {
            if (!IsInitialised)
            {
                throw new InvalidOperationException("NxPoolingRuntime has not been initialised. Ensure PoolingCoordinatorSystem has run before borrowing pooled resources.");
            }
        }
    }
}
