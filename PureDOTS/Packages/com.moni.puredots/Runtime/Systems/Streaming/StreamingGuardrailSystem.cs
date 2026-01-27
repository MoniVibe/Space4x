using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Enforces runtime guardrails for streaming commands to keep the loader deterministic.
    /// Drops conflicting commands, honours cooldowns, and exposes debug controls for designers.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(StreamingScannerSystem))]
    [UpdateBefore(typeof(StreamingLoaderSystem))]
    public partial struct StreamingGuardrailSystem : ISystem
    {
        private ComponentLookup<StreamingSectionDescriptor> _descriptorLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingCoordinator>();
            _descriptorLookup = state.GetComponentLookup<StreamingSectionDescriptor>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var coordinatorEntity = SystemAPI.GetSingletonEntity<StreamingCoordinator>();
            var coordinator = SystemAPI.GetComponent<StreamingCoordinator>(coordinatorEntity);

            var worldSequence = (uint)state.WorldUnmanaged.SequenceNumber;
            if (coordinator.WorldSequenceNumber != worldSequence)
            {
                UnityEngine.Debug.LogError("[PureDOTS] StreamingGuardrailSystem detected coordinator belonging to a different world. Skipping update.");
                return;
            }

            uint currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            HandleDebugControls(ref state, coordinatorEntity, currentTick);

            var entityManager = state.EntityManager;
            var commands = entityManager.GetBuffer<StreamingSectionCommand>(coordinatorEntity);
            if (commands.Length == 0)
            {
                return;
            }

            _descriptorLookup.Update(ref state);

            var validCommands = new NativeList<StreamingSectionCommand>(commands.Length, state.WorldUpdateAllocator);
            var seenSections = new NativeHashMap<Entity, StreamingSectionAction>(commands.Length, state.WorldUpdateAllocator);

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];

                if (!entityManager.Exists(command.SectionEntity))
                {
                    continue;
                }

                if (!entityManager.HasComponent<StreamingSectionState>(command.SectionEntity))
                {
                    continue;
                }

                if (seenSections.TryGetValue(command.SectionEntity, out var existingAction))
                {
                    if (existingAction != command.Action)
                    {
                        LogConflict(command.SectionEntity, command.Action, existingAction);
                    }
                    continue;
                }

                var sectionState = entityManager.GetComponentData<StreamingSectionState>(command.SectionEntity);
                bool dropCommand = false;

                dropCommand = command.Action switch
                {
                    StreamingSectionAction.Load => HandleLoadGuard(command.SectionEntity, ref sectionState, currentTick),
                    StreamingSectionAction.Unload => HandleUnloadGuard(command.SectionEntity, ref sectionState),
                    _ => false
                };

                if (dropCommand)
                {
                    entityManager.SetComponentData(command.SectionEntity, sectionState);
                    continue;
                }

                seenSections.TryAdd(command.SectionEntity, command.Action);
                validCommands.Add(command);
            }

            if (validCommands.Length != commands.Length)
            {
                commands.Clear();
                for (int i = 0; i < validCommands.Length; i++)
                {
                    commands.Add(validCommands[i]);
                }
            }
        }

        private void HandleDebugControls(ref SystemState state, Entity coordinatorEntity, uint currentTick)
        {
            if (!SystemAPI.HasComponent<StreamingDebugControl>(coordinatorEntity))
            {
                return;
            }

            var control = SystemAPI.GetComponentRW<StreamingDebugControl>(coordinatorEntity);
            if (!control.ValueRO.ClearCooldowns)
            {
                return;
            }

            int clearedCount = 0;
            foreach (var (section, _) in SystemAPI.Query<RefRW<StreamingSectionState>>().WithEntityAccess())
            {
                ref var value = ref section.ValueRW;
                if (value.CooldownUntilTick > currentTick)
                {
                    value.CooldownUntilTick = currentTick;
                    if (value.Status == StreamingSectionStatus.Error)
                    {
                        value.Status = StreamingSectionStatus.Unloaded;
                    }
                    clearedCount++;
                }
            }

            control.ValueRW.ClearCooldowns = false;
            control.ValueRW.LastClearRequestTick = currentTick;

            if (clearedCount > 0)
            {
                UnityEngine.Debug.Log($"[PureDOTS] Cleared {clearedCount} streaming cooldown(s) on request.");
            }
        }

        private bool HandleLoadGuard(Entity section, ref StreamingSectionState sectionState, uint currentTick)
        {
            if (sectionState.CooldownUntilTick > currentTick)
            {
                sectionState.Status = StreamingSectionStatus.Unloaded;
                LogCooldownDrop(section, sectionState.CooldownUntilTick);
                return true;
            }

            return false;
        }

        private bool HandleUnloadGuard(Entity section, ref StreamingSectionState sectionState)
        {
            if (sectionState.PinCount > 0)
            {
                sectionState.Status = StreamingSectionStatus.Loaded;
                LogPinnedDrop(section, sectionState.PinCount);
                return true;
            }

            if (sectionState.Status == StreamingSectionStatus.Unloaded)
            {
                // Nothing to unload; keep state consistent for downstream systems.
                return true;
            }

            return false;
        }

        private void LogConflict(Entity section, StreamingSectionAction incoming, StreamingSectionAction existing)
        {
            var label = GetSectionLabel(section);
            UnityEngine.Debug.LogWarning($"[PureDOTS] Dropping streaming command {incoming} for '{label}' because {existing} is already pending.");
        }

        private void LogCooldownDrop(Entity section, uint cooldownUntilTick)
        {
            var label = GetSectionLabel(section);
            UnityEngine.Debug.LogWarning($"[PureDOTS] Dropping load for '{label}' while cooldown active until tick {cooldownUntilTick}.");
        }

        private void LogPinnedDrop(Entity section, short pinCount)
        {
            var label = GetSectionLabel(section);
            UnityEngine.Debug.LogWarning($"[PureDOTS] Dropping unload for pinned section '{label}' (PinCount={pinCount}).");
        }

        private string GetSectionLabel(Entity section)
        {
            if (_descriptorLookup.HasComponent(section))
            {
                var descriptor = _descriptorLookup[section];
                if (descriptor.Identifier.Length > 0)
                {
                    return descriptor.Identifier.ToString();
                }
            }

            return $"Entity {section.Index}";
        }
    }
}
