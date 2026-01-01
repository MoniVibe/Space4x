using PureDOTS.Runtime.Modules;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Modules
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct Space4XCaptainPolicySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ManeuverMode>();
            state.RequireForUpdate<OfficerProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (profile, mode) in SystemAPI.Query<RefRO<OfficerProfile>, RefRW<ManeuverMode>>())
            {
                var horizon = math.max(0f, profile.ValueRO.ExpectedManeuverHorizonSeconds);
                var risk = math.saturate(profile.ValueRO.RiskTolerance);

                var hotThreshold = math.lerp(3f, 1f, risk);
                var warmThreshold = math.lerp(8f, 3f, risk);

                var desiredMode = horizon <= hotThreshold
                    ? ShipManeuverMode.Maneuver
                    : horizon <= warmThreshold
                        ? ShipManeuverMode.Transit
                        : ShipManeuverMode.Anchor;

                mode.ValueRW.Mode = desiredMode;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateBefore(typeof(Space4XModulePostureCommandSystem))]
    public partial struct Space4XModuleAttachmentSyncSystem : ISystem
    {
        private BufferLookup<ModuleAttachment> _attachmentLookup;
        private ComponentLookup<ModuleOwner> _ownerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
            _attachmentLookup = state.GetBufferLookup<ModuleAttachment>(false);
            _ownerLookup = state.GetComponentLookup<ModuleOwner>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _attachmentLookup.Update(ref state);
            _ownerLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (slots, ownerEntity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                if (!_attachmentLookup.HasBuffer(ownerEntity))
                {
                    ecb.AddBuffer<ModuleAttachment>(ownerEntity);
                    continue;
                }

                var attachments = _attachmentLookup[ownerEntity];
                attachments.Clear();

                for (var i = 0; i < slots.Length; i++)
                {
                    var module = slots[i].CurrentModule;
                    if (module == Entity.Null)
                    {
                        continue;
                    }

                    attachments.Add(new ModuleAttachment { Module = module });

                    if (_ownerLookup.HasComponent(module))
                    {
                        var owner = _ownerLookup[module];
                        if (owner.Owner != ownerEntity)
                        {
                            owner.Owner = ownerEntity;
                            _ownerLookup[module] = owner;
                        }
                    }
                    else
                    {
                        ecb.AddComponent(module, new ModuleOwner { Owner = ownerEntity });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainPolicySystem))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XModulePostureCommandSystem : ISystem
    {
        private ComponentLookup<ModuleRuntimeState> _runtimeLookup;
        private BufferLookup<ModuleCommand> _commandLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ManeuverMode>();
            state.RequireForUpdate<ModuleAttachment>();

            _runtimeLookup = state.GetComponentLookup<ModuleRuntimeState>(true);
            _commandLookup = state.GetBufferLookup<ModuleCommand>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _runtimeLookup.Update(ref state);
            _commandLookup.Update(ref state);

            foreach (var (mode, modules) in SystemAPI.Query<RefRO<ManeuverMode>, DynamicBuffer<ModuleAttachment>>())
            {
                var desiredPosture = mode.ValueRO.Mode switch
                {
                    ShipManeuverMode.Anchor => ModulePosture.Off,
                    ShipManeuverMode.Transit => ModulePosture.Standby,
                    ShipManeuverMode.Maneuver => ModulePosture.Online,
                    _ => ModulePosture.Standby
                };

                var desiredTarget = desiredPosture switch
                {
                    ModulePosture.Online => 1f,
                    ModulePosture.Standby => 0.35f,
                    _ => 0f
                };

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_commandLookup.HasBuffer(module))
                    {
                        continue;
                    }

                    if (_runtimeLookup.HasComponent(module))
                    {
                        var runtime = _runtimeLookup[module];
                        if (runtime.Posture == desiredPosture &&
                            math.abs(runtime.TargetOutput - desiredTarget) <= 0.01f)
                        {
                            continue;
                        }
                    }

                    var buffer = _commandLookup[module];
                    buffer.Add(new ModuleCommand
                    {
                        Posture = desiredPosture,
                        TargetOutput = desiredTarget,
                        Flags = ModuleCommandFlags.Posture | ModuleCommandFlags.TargetOutput
                    });
                }
            }
        }
    }
}
