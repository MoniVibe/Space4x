using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    /// <summary>
    /// Processes timed mining/haul/combat timeline actions for the mining scenario runner.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XMiningScenarioActionProcessor : ISystem
    {
        private ComponentLookup<VesselAIState> _aiLookup;
        private ComponentLookup<MiningVessel> _vesselLookup;
        private BufferLookup<ResourceStorage> _storageLookup;
        private ComponentLookup<Carrier> _carrierLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ScenarioActionScheduler>();

            _aiLookup = state.GetComponentLookup<VesselAIState>(false);
            _vesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var currentSeconds = time.Tick * time.FixedDeltaTime;

            _aiLookup.Update(ref state);
            _vesselLookup.Update(ref state);
            _storageLookup.Update(ref state);
            _carrierLookup.Update(ref state);

            foreach (var (scheduler, actions, entity) in SystemAPI.Query<RefRW<ScenarioActionScheduler>, DynamicBuffer<ScenarioActionEntry>>().WithEntityAccess())
            {
                var lastProcessed = scheduler.ValueRO.LastProcessedTime;

                for (int i = 0; i < actions.Length; i++)
                {
                    var action = actions[i];
                    if (action.TimeSeconds > lastProcessed && action.TimeSeconds <= currentSeconds)
                    {
                        ProcessAction(ref state, action, time.Tick);
                    }
                }

                scheduler.ValueRW.LastProcessedTime = currentSeconds;
            }
        }

        private void ProcessAction(ref SystemState state, ScenarioActionEntry action, uint currentTick)
        {
            var spawnPickup = new FixedString64Bytes("spawnPickup");
            var forceReturn = new FixedString64Bytes("forceReturn");
            var drainStorage = new FixedString64Bytes("drainCarrierStorage");

            if (action.ActionType == spawnPickup)
            {
                SpawnPickup(ref state, action, currentTick);
            }
            else if (action.ActionType == forceReturn)
            {
                ForceReturn(ref state, action, currentTick);
            }
            else if (action.ActionType == drainStorage)
            {
                DrainCarrierStorage(ref state, action);
            }
        }

        private void SpawnPickup(ref SystemState state, ScenarioActionEntry action, uint currentTick)
        {
            var resourceType = ParseResourceType(action.Target);
            var pickup = state.EntityManager.CreateEntity(typeof(SpawnResource), typeof(LocalTransform));
            state.EntityManager.SetComponentData(pickup, new SpawnResource
            {
                Type = resourceType,
                Amount = math.max(0f, action.FloatValue),
                SourceEntity = Entity.Null,
                SpawnTick = currentTick
            });

            var position = action.TargetPosition;
            state.EntityManager.SetComponentData(pickup, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
        }

        private void ForceReturn(ref SystemState state, ScenarioActionEntry action, uint currentTick)
        {
            if (action.TargetEntityId.IsEmpty)
            {
                return;
            }

            foreach (var (aiState, vessel) in SystemAPI.Query<RefRW<VesselAIState>, RefRO<MiningVessel>>())
            {
                if (vessel.ValueRO.VesselId != action.TargetEntityId)
                {
                    continue;
                }

                aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Returning;
                aiState.ValueRW.CurrentState = VesselAIState.State.Returning;
                aiState.ValueRW.TargetEntity = vessel.ValueRO.CarrierEntity;
                aiState.ValueRW.StateStartTick = currentTick;
                aiState.ValueRW.StateTimer = 0f;
                break;
            }
        }

        private void DrainCarrierStorage(ref SystemState state, ScenarioActionEntry action)
        {
            if (action.TargetEntityId.IsEmpty)
            {
                return;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<Carrier>().WithEntityAccess())
            {
                if (carrier.CarrierId != action.TargetEntityId)
                {
                    continue;
                }

                if (!_storageLookup.HasBuffer(entity))
                {
                    continue;
                }

                var buffer = _storageLookup[entity];
                var amountToDrain = math.max(0f, action.FloatValue);
                for (int i = 0; i < buffer.Length && amountToDrain > 0f; i++)
                {
                    var entry = buffer[i];
                    var drain = math.min(entry.Amount, amountToDrain);
                    entry.Amount -= drain;
                    amountToDrain -= drain;
                    buffer[i] = entry;
                }
                break;
            }
        }

        private static ResourceType ParseResourceType(in FixedString128Bytes id)
        {
            FixedString128Bytes minerals = "space4x.resource.minerals";
            FixedString128Bytes rareMetals = "space4x.resource.rare_metals";
            FixedString128Bytes energy = "space4x.resource.energy_crystals";
            FixedString128Bytes organic = "space4x.resource.organic_matter";

            if (id == rareMetals)
            {
                return ResourceType.RareMetals;
            }

            if (id == energy)
            {
                return ResourceType.EnergyCrystals;
            }

            if (id == organic)
            {
                return ResourceType.OrganicMatter;
            }

            // Default/fallback
            return id == minerals ? ResourceType.Minerals : ResourceType.Minerals;
        }
    }
}

