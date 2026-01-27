#nullable enable
using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.History;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Systems
{
    internal struct PhysicsWorldSnapshot
    {
        public uint Tick;
        public PhysicsWorld World;
        public bool Valid;

        public void Dispose()
        {
            if (Valid)
            {
                World.Dispose();
                Valid = false;
                Tick = 0;
            }
        }
    }

    [UpdateInGroup(typeof(HistoryPhaseGroup))]
    [UpdateBefore(typeof(HistorySystemGroup))]
    public sealed partial class PhysicsHistoryCaptureSystem : SystemBase
    {
        private NativeArray<PhysicsWorldSnapshot> _buffer;
        private int _bufferLength;
        private int _writeIndex;
        private RuntimeConfigVar? _enabledVar;
        private RuntimeConfigVar? _lengthVar;

        internal static PhysicsHistoryCaptureSystem? Instance { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            RuntimeConfigRegistry.Initialize();
            _enabledVar = HistoryConfigVars.PhysicsHistoryEnabled;
            _lengthVar = HistoryConfigVars.PhysicsHistoryLength;
            AllocateBuffer();
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<SimulationSingleton>();
            RequireForUpdate<TimeState>();
            Instance = this;
        }

        protected override void OnDestroy()
        {
            if (_buffer.IsCreated)
            {
                for (int i = 0; i < _bufferLength; i++)
                {
                    _buffer[i].Dispose();
                }
                _buffer.Dispose();
            }

            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_lengthVar != null)
            {
                var desiredLength = math.clamp(math.max(1, _lengthVar.IntValue), 1, 256);
                if (desiredLength != _bufferLength)
                {
                    AllocateBuffer();
                }
            }

            if (_enabledVar == null || !_enabledVar.BoolValue)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var simulation = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();
            simulation.FinalJobHandle.Complete();

            var physicsSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            var snapshot = _buffer[_writeIndex];
            if (snapshot.Valid)
            {
                snapshot.Dispose();
            }

            snapshot.World = physicsSingleton.PhysicsWorld.Clone();
            snapshot.Tick = timeState.Tick;
            snapshot.Valid = true;
            _buffer[_writeIndex] = snapshot;

            _writeIndex = (_writeIndex + 1) % _bufferLength;
        }

        internal bool TryGetPhysicsWorld(uint tick, out PhysicsWorld world)
        {
            if (!_buffer.IsCreated)
            {
                world = default;
                return false;
            }

            for (int i = 0; i < _bufferLength; i++)
            {
                var snapshot = _buffer[i];
                if (snapshot.Valid && snapshot.Tick == tick)
                {
                    world = snapshot.World;
                    return true;
                }
            }

            world = default;
            return false;
        }

        internal bool TryClonePhysicsWorld(uint tick, out PhysicsWorld world)
        {
            if (TryGetPhysicsWorld(tick, out var existing))
            {
                world = existing.Clone();
                return true;
            }

            world = default;
            return false;
        }

        internal bool TryGetLatest(out PhysicsWorld world, out uint tick)
        {
            if (!_buffer.IsCreated)
            {
                world = default;
                tick = 0;
                return false;
            }

            PhysicsWorldSnapshot latest = default;
            var found = false;
            for (int i = 0; i < _bufferLength; i++)
            {
                var snapshot = _buffer[i];
                if (snapshot.Valid && (!found || snapshot.Tick > latest.Tick))
                {
                    latest = snapshot;
                    found = true;
                }
            }

            if (found)
            {
                world = latest.World;
                tick = latest.Tick;
                return true;
            }

            world = default;
            tick = 0;
            return false;
        }

        internal bool TryCloneLatest(out PhysicsWorld world, out uint tick)
        {
            if (TryGetLatest(out var existingWorld, out tick))
            {
                world = existingWorld.Clone();
                return true;
            }

            world = default;
            tick = 0;
            return false;
        }

        internal void CollectTicks(List<uint> destination)
        {
            destination.Clear();
            if (!_buffer.IsCreated)
            {
                return;
            }

            for (int i = 0; i < _bufferLength; i++)
            {
                var snapshot = _buffer[i];
                if (snapshot.Valid)
                {
                    destination.Add(snapshot.Tick);
                }
            }
            destination.Sort();
        }

        private void AllocateBuffer()
        {
            if (_buffer.IsCreated)
            {
                for (int i = 0; i < _bufferLength; i++)
                {
                    _buffer[i].Dispose();
                }
                _buffer.Dispose();
            }

            var length = math.clamp(math.max(1, _lengthVar != null ? _lengthVar.IntValue : 32), 1, 256);
            _bufferLength = length;
            _buffer = new NativeArray<PhysicsWorldSnapshot>(length, Allocator.Persistent);
            for (int i = 0; i < length; i++)
            {
                _buffer[i] = new PhysicsWorldSnapshot { Tick = 0, Valid = false, World = default };
            }
            _writeIndex = 0;
        }
    }

    public readonly struct PhysicsHistoryHandle
    {
        private readonly PhysicsHistoryCaptureSystem? _system;

        internal PhysicsHistoryHandle(PhysicsHistoryCaptureSystem? system)
        {
            _system = system;
        }

        public bool IsCreated => _system != null;

        public bool TryClonePhysicsWorld(uint tick, out PhysicsWorld world)
        {
            if (_system == null)
            {
                world = default;
                return false;
            }

            return _system.TryClonePhysicsWorld(tick, out world);
        }

        public bool TryGetLatest(out PhysicsWorld world, out uint tick)
        {
            if (_system == null)
            {
                world = default;
                tick = 0;
                return false;
            }

            return _system.TryGetLatest(out world, out tick);
        }

        public bool TryCloneLatest(out PhysicsWorld world, out uint tick)
        {
            if (_system == null)
            {
                world = default;
                tick = 0;
                return false;
            }

            return _system.TryCloneLatest(out world, out tick);
        }
    }

    public static class PhysicsHistory
    {
        public static PhysicsHistoryHandle GetHandle()
        {
            return new PhysicsHistoryHandle(PhysicsHistoryCaptureSystem.Instance);
        }
    }
}


