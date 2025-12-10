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
        }

        private static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Enum.IsDefined(typeof(Key), key))
                return false;

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
