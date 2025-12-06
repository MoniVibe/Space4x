using System;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Demo
{
    public struct DemoHotkeyConfig : IComponentData
    {
        public byte EnableEcsHotkeys;
    }

    /// <summary>
    /// Handles demo hotkeys: P (pause/play), J (jump/flank), B (bindings), V (veteran), R (rewind).
    /// Runs in SimulationSystemGroup to process input.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDemoHotkeySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DemoBootstrapState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DemoHotkeyConfig>(out var config) || config.EnableEcsHotkeys == 0)
                return;

            if (!SystemAPI.TryGetSingletonRW<DemoBootstrapState>(out var demoState))
                return;
            uint seedBase;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                seedBase = timeState.Tick == 0 ? 0x9E3779B9u : timeState.Tick;
            }
            else
            {
                // Fallback to frame-based seed; non-deterministic but stable enough for dev hotkeys
                seedBase = (uint)(UnityEngine.Time.frameCount + 1);
            }
            var em = state.EntityManager;

            // P - Pause/Play
            if (KeyPressed(Key.P))
            {
                demoState.ValueRW.Paused = (byte)(demoState.ValueRO.Paused == 1 ? 0 : 1);
                Debug.Log($"[Demo] Pause toggled: {demoState.ValueRO.Paused == 1}");
            }

            // J - Toggle Jump/Flank planner (set component flag on entities)
            if (KeyPressed(Key.J))
            {
                // TODO: Implement jump/flank planner toggle
                Debug.Log("[Demo] Jump/Flank planner toggle (not yet implemented)");
            }

            // B - Swap Minimal/Fancy bindings
            if (KeyPressed(Key.B))
            {
                if (SystemAPI.TryGetSingletonRW<DemoOptions>(out var options))
                {
                    options.ValueRW.BindingsSet = (byte)(options.ValueRO.BindingsSet == 1 ? 0 : 1);
                    string bindingName = options.ValueRO.BindingsSet == 1 ? "Fancy" : "Minimal";
                    Debug.Log($"[Demo] Bindings swapped to: {bindingName}");
                    
                    // Trigger binding reload by marking presentation entities as dirty
                    // The presentation system will handle the swap
                }
            }

            // V - Toggle veteran proficiency
            if (KeyPressed(Key.V))
            {
                if (SystemAPI.TryGetSingletonRW<DemoOptions>(out var options))
                {
                    options.ValueRW.Veteran = (byte)(options.ValueRO.Veteran == 1 ? 0 : 1);
                    Debug.Log($"[Demo] Veteran proficiency: {options.ValueRO.Veteran == 1}");
                }
            }

            // R - Trigger rewind sequence
            if (KeyPressed(Key.R))
            {
                demoState.ValueRW.RewindEnabled = (byte)(demoState.ValueRO.RewindEnabled == 1 ? 0 : 1);
                Debug.Log($"[Demo] Rewind toggled: {demoState.ValueRO.RewindEnabled == 1}");
            }

            // H - Spawn miner near origin
            if (KeyPressed(Key.H))
            {
                EnqueueSpawn(em, Space4XSpawnKind.Miner, 1, new float3(0f, 0f, 0f), 15f, seedBase);
            }

            // N - Spawn asteroid near origin
            if (KeyPressed(Key.N))
            {
                EnqueueSpawn(em, Space4XSpawnKind.Asteroid, 1, new float3(0f, 0f, 0f), 18f, seedBase + 1u);
            }

            // C - Spawn carrier near origin
            if (KeyPressed(Key.C))
            {
                EnqueueSpawn(em, Space4XSpawnKind.Carrier, 1, new float3(0f, 0f, 0f), 22f, seedBase + 2u);
            }
        }

        private static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Enum.IsDefined(typeof(Key), key))
                return false;

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static void EnqueueSpawn(EntityManager em, Space4XSpawnKind kind, int count, float3 center, float radius, uint seedBase)
        {
            Entity queue;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XSpawnRequest>()))
            {
                queue = q.IsEmptyIgnoreFilter ? em.CreateEntity() : q.GetSingletonEntity();
            }

            if (!em.HasBuffer<Space4XSpawnRequest>(queue))
            {
                em.AddBuffer<Space4XSpawnRequest>(queue);
            }

            var buffer = em.GetBuffer<Space4XSpawnRequest>(queue);
            var requestSeed = math.hash(new uint2(seedBase == 0 ? 0x9E3779B9u : seedBase, (uint)(buffer.Length + 1))) | 1u;
            buffer.Add(new Space4XSpawnRequest
            {
                Kind = kind,
                Count = count,
                Center = center,
                Radius = radius,
                Seed = requestSeed
            });
        }
    }
}
