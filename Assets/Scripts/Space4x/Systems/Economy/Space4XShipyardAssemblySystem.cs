using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Converts ship hull items into spawned vessels at shipyard facilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Runtime.Economy.Production.ProductionJobCompletionSystem))]
    public partial struct Space4XShipyardAssemblySystem : ISystem
    {
        private const int MaxSpawnPerTick = 1;

        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<TechLevel> _techLookup;

        private FixedString64Bytes _hullItemId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _inventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemsLookup = state.GetBufferLookup<InventoryItem>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _techLookup = state.GetComponentLookup<TechLevel>(true);

            _hullItemId = new FixedString64Bytes("lcv-sparrow");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _inventoryLookup.Update(ref state);
            _itemsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _techLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var spawnCount = 0;

            foreach (var (role, link, facility) in SystemAPI
                         .Query<RefRO<FacilityBusinessClassComponent>, RefRO<ColonyFacilityLink>>()
                         .WithEntityAccess())
            {
                if (role.ValueRO.Value != FacilityBusinessClass.Shipyard)
                {
                    continue;
                }

                if (spawnCount >= MaxSpawnPerTick)
                {
                    break;
                }

                if (!_inventoryLookup.HasComponent(facility))
                {
                    continue;
                }

                var inventoryEntity = _inventoryLookup[facility].InventoryEntity;
                if (inventoryEntity == Entity.Null || !_itemsLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                var items = _itemsLookup[inventoryEntity];
                if (!TryConsumeHull(ref items, _hullItemId))
                {
                    continue;
                }

                var facilityPos = _transformLookup.HasComponent(facility)
                    ? _transformLookup[facility].Position
                    : float3.zero;

                var colony = link.ValueRO.Colony;
                var tech = _techLookup.HasComponent(colony) ? _techLookup[colony] : default;
                var warpPrecision = WarpPrecision.FromTier(ResolveWarpTier(tech));

                var ship = ecb.CreateEntity();
                var spawnOffset = new float3(0f, 0f, 8f + spawnCount * 2f);
                ecb.AddComponent(ship, LocalTransform.FromPositionRotationScale(facilityPos + spawnOffset, quaternion.identity, 1f));
                ecb.AddComponent<SpatialIndexedTag>(ship);
                ecb.AddComponent(ship, MediumContext.Vacuum);

                var carrierId = BuildCarrierId(facility.Index, tickTime.Tick);
                ecb.AddComponent(ship, new Carrier
                {
                    CarrierId = carrierId,
                    AffiliationEntity = Entity.Null,
                    Speed = 5f,
                    Acceleration = 0.5f,
                    Deceleration = 0.6f,
                    TurnSpeed = 0.35f,
                    SlowdownDistance = 20f,
                    ArrivalDistance = 3f,
                    PatrolCenter = facilityPos,
                    PatrolRadius = 45f
                });

                ecb.AddComponent(ship, new MovementCommand
                {
                    TargetPosition = facilityPos,
                    ArrivalThreshold = 2f
                });

                ecb.AddComponent(ship, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = 5f,
                    CurrentSpeed = 0f,
                    Acceleration = 0.5f,
                    Deceleration = 0.6f,
                    TurnSpeed = 0.35f,
                    SlowdownDistance = 20f,
                    ArrivalDistance = 3f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0,
                    MoveStartTick = 0
                });

                ecb.AddComponent(ship, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.None,
                    TargetEntity = Entity.Null,
                    TargetPosition = facilityPos,
                    StateTimer = 0f,
                    StateStartTick = tickTime.Tick
                });

                ecb.AddComponent(ship, new EntityIntent
                {
                    Mode = IntentMode.Idle,
                    TargetEntity = Entity.Null,
                    TargetPosition = facilityPos,
                    TriggeringInterrupt = InterruptType.None,
                    IntentSetTick = tickTime.Tick,
                    Priority = InterruptPriority.Low,
                    IsValid = 0
                });

                ecb.AddBuffer<Interrupt>(ship);

                ecb.AddComponent(ship, new VesselPhysicalProperties
                {
                    Radius = 2.4f,
                    BaseMass = 100f,
                    HullDensity = 1.1f,
                    CargoMassPerUnit = 0.02f,
                    Restitution = 0.08f,
                    TangentialDamping = 0.25f
                });

                ecb.AddComponent(ship, DockingCapacity.LightCarrier);
                ecb.AddBuffer<DockedEntity>(ship);
                ecb.AddBuffer<ResourceStorage>(ship);

                ecb.AddComponent(ship, new CarrierHullId { HullId = _hullItemId });
                ecb.AddComponent(ship, HullIntegrity.LightCarrier);

                ecb.AddComponent(ship, new CaptainOrder
                {
                    Type = CaptainOrderType.None,
                    Status = CaptainOrderStatus.None,
                    Priority = 255,
                    TargetEntity = Entity.Null,
                    TargetPosition = facilityPos,
                    IssuedTick = tickTime.Tick,
                    TimeoutTick = 0u,
                    IssuingAuthority = Entity.Null
                });

                ecb.AddComponent(ship, tech);
                ecb.AddComponent(ship, warpPrecision);
                ecb.AddComponent(ship, ReinforcementTactics.Coordinated);
                ecb.AddComponent(ship, new ReinforcementArrival
                {
                    ArrivalPosition = facilityPos,
                    ArrivalRotation = quaternion.identity,
                    ArrivalTick = tickTime.Tick,
                    UsedTactic = ReinforcementTactic.Standard,
                    FormationSlot = 0,
                    HasArrived = 1
                });

                ecb.AddComponent(ship, new ColonyTechLink { Colony = colony });

                var affiliations = ecb.AddBuffer<AffiliationTag>(ship);
                affiliations.Add(new AffiliationTag
                {
                    Type = AffiliationType.Colony,
                    Target = colony,
                    Loyalty = (half)1f
                });

                spawnCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool TryConsumeHull(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                if (item.Quantity < 1f)
                {
                    continue;
                }

                item.Quantity -= 1f;
                if (item.Quantity <= 0f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }

                return true;
            }

            return false;
        }

        private static WarpTechTier ResolveWarpTier(in TechLevel tech)
        {
            var tier = math.max(tech.MiningTech,
                math.max(tech.CombatTech, math.max(tech.HaulingTech, tech.ProcessingTech)));

            return tier switch
            {
                >= 4 => WarpTechTier.Experimental,
                3 => WarpTechTier.Advanced,
                2 => WarpTechTier.Standard,
                1 => WarpTechTier.Basic,
                _ => WarpTechTier.Primitive
            };
        }

        private static FixedString64Bytes BuildCarrierId(int facilityIndex, uint tick)
        {
            FixedString64Bytes id = default;
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('-');
            AppendDigits(ref id, facilityIndex);
            id.Append('-');
            AppendDigits(ref id, (int)tick);
            return id;
        }

        private static void AppendDigits(ref FixedString64Bytes id, int value)
        {
            if (value == 0)
            {
                id.Append('0');
                return;
            }

            var digits = new FixedList32Bytes<char>();
            var remaining = math.abs(value);
            while (remaining > 0 && digits.Length < digits.Capacity)
            {
                var digit = (char)('0' + (remaining % 10));
                digits.Add(digit);
                remaining /= 10;
            }

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                id.Append(digits[i]);
            }
        }
    }
}
