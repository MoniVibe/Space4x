using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// ComponentSystemGroup variant that logs the specific system name whenever an exception is thrown.
    /// This makes it possible to identify which unmanaged system triggered a Burst exception without
    /// modifying Unity packages or disabling Burst globally.
    /// </summary>
    public abstract unsafe partial class InstrumentedComponentSystemGroup : ComponentSystemGroup
    {
        private static string ResolveSystemName(World world, SystemHandle handle)
        {
            if (handle.Equals(SystemHandle.Null))
            {
                return "<null system>";
            }

            try
            {
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
                var debugName = state.DebugName;
                if (!debugName.IsEmpty)
                {
                    return debugName.ToString();
                }
            }
            catch
            {
                // If DebugName cannot be materialized, fall back to the hash representation.
            }

            return $"SystemHandle(0x{handle.GetHashCode():X})";
        }

        private void UpdateAllWithDiagnostics()
        {
            SortSystems();

            using var systems = GetAllSystems(Allocator.Temp);
            var world = World.Unmanaged;
            for (int i = 0; i < systems.Length; ++i)
            {
                var handle = systems[i];
                try
                {
                    handle.Update(world);
                }
                catch (Exception ex)
                {
                    var systemName = ResolveSystemName(World, handle);
                    UnityEngine.Debug.LogError($"[{GetType().Name}] Exception while updating {systemName}. See stack trace below.");
                    UnityEngine.Debug.LogException(ex);
                }

                if (World.QuitUpdate)
                {
                    break;
                }
            }
        }

        protected override void OnUpdate()
        {
            if (RateManager == null)
            {
                UpdateAllWithDiagnostics();
            }
            else
            {
                while (RateManager.ShouldGroupUpdate(this))
                {
                    UpdateAllWithDiagnostics();
                }
            }
        }
    }
}
