using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
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
    /// Seeds a starter ship for each business using hull catalog IDs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBusinessAssetOwnershipSystem))]
    public partial struct Space4XBusinessFleetSeedSystem : ISystem
    {
        private static readonly FixedString64Bytes HullIdLight = "lcv-sparrow";
        private static readonly FixedString64Bytes HullIdCarrier = "cv-mule";

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<TechLevel> _techLookup;
        private ComponentLookup<Space4XBusinessAssetOwner> _assetOwnerLookup;
        private BufferLookup<Space4XBusinessAssetLink> _assetLinkLookup;
        private ComponentLookup<Space4XBusinessFleetSeedConfig> _fleetConfigLookup;
        private BufferLookup<Space4XBusinessFleetSeedOverride> _fleetOverrideLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _techLookup = state.GetComponentLookup<TechLevel>(true);
            _assetOwnerLookup = state.GetComponentLookup<Space4XBusinessAssetOwner>(false);
            _assetLinkLookup = state.GetBufferLookup<Space4XBusinessAssetLink>(false);
            _fleetConfigLookup = state.GetComponentLookup<Space4XBusinessFleetSeedConfig>(true);
            _fleetOverrideLookup = state.GetBufferLookup<Space4XBusinessFleetSeedOverride>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
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

            _transformLookup.Update(ref state);
            _techLookup.Update(ref state);
            _assetOwnerLookup.Update(ref state);
            _assetLinkLookup.Update(ref state);
            _fleetConfigLookup.Update(ref state);
            _fleetOverrideLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (businessState, businessEntity) in SystemAPI.Query<RefRO<Space4XBusinessState>>()
                         .WithNone<Space4XBusinessFleetSeeded>()
                         .WithEntityAccess())
            {
                if (HasShipAsset(businessEntity))
                {
                    ecb.AddComponent<Space4XBusinessFleetSeeded>(businessEntity);
                    continue;
                }

                var hullId = ResolveStarterHull(ref state, businessState.ValueRO.Kind);
                if (hullId.IsEmpty)
                {
                    ecb.AddComponent<Space4XBusinessFleetSeeded>(businessEntity);
                    continue;
                }

                var colony = businessState.ValueRO.Colony;
                if (colony == Entity.Null)
                {
                    ecb.AddComponent<Space4XBusinessFleetSeeded>(businessEntity);
                    continue;
                }

                var colonyPos = _transformLookup.HasComponent(colony)
                    ? _transformLookup[colony].Position
                    : float3.zero;

                var tech = _techLookup.HasComponent(colony) ? _techLookup[colony] : default;
                var ship = SpawnShip(ref state, ref ecb, colony, colonyPos, hullId, tech, tickTime.Tick);
                if (ship != Entity.Null)
                {
                    ecb.AddComponent(ship, new Space4XBusinessAssetOwner
                    {
                        Business = businessEntity,
                        AssetType = Space4XBusinessAssetType.Ship,
                        AssignedTick = tickTime.Tick,
                        CatalogId = hullId
                    });

                    EnsureLink(ref state, ref ecb, businessEntity, ship, Space4XBusinessAssetType.Ship, tickTime.Tick, hullId);
                }

                ecb.AddComponent<Space4XBusinessFleetSeeded>(businessEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool HasShipAsset(Entity businessEntity)
        {
            if (!_assetLinkLookup.HasBuffer(businessEntity))
            {
                return false;
            }

            var links = _assetLinkLookup[businessEntity];
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].AssetType == Space4XBusinessAssetType.Ship)
                {
                    return true;
                }
            }

            return false;
        }

        private FixedString64Bytes ResolveStarterHull(ref SystemState state, Space4XBusinessKind kind)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XBusinessFleetSeedConfig>(out var configEntity) &&
                _fleetConfigLookup.HasComponent(configEntity))
            {
                var config = _fleetConfigLookup[configEntity];
                if (_fleetOverrideLookup.HasBuffer(configEntity))
                {
                    var overrides = _fleetOverrideLookup[configEntity];
                    for (int i = 0; i < overrides.Length; i++)
                    {
                        if (overrides[i].BusinessKind == kind && !overrides[i].HullId.IsEmpty)
                        {
                            return overrides[i].HullId;
                        }
                    }
                }

                if (!config.DefaultHullId.IsEmpty)
                {
                    return config.DefaultHullId;
                }
            }

            switch (kind)
            {
                case Space4XBusinessKind.Shipwright:
                case Space4XBusinessKind.MarketHub:
                case Space4XBusinessKind.DeepCoreSyndicate:
                    return HullIdCarrier;
                default:
                    return HullIdLight;
            }
        }

        private Entity SpawnShip(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity colony,
            float3 colonyPos,
            FixedString64Bytes hullId,
            in TechLevel tech,
            uint tick)
        {
            if (hullId.IsEmpty)
            {
                return Entity.Null;
            }

            var isCarrierHull = hullId.Equals(HullIdCarrier);
            var ship = ecb.CreateEntity();
            var spawnOffset = new float3(0f, 0f, isCarrierHull ? 14f : 10f);
            var position = colonyPos + spawnOffset;

            ecb.AddComponent(ship, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            ecb.AddComponent<SpatialIndexedTag>(ship);
            ecb.AddComponent(ship, MediumContext.Vacuum);

            var baseSpeed = isCarrierHull ? 4.2f : 5f;
            var baseAcceleration = isCarrierHull ? 0.35f : 0.5f;
            var baseDeceleration = isCarrierHull ? 0.45f : 0.6f;
            var baseTurn = isCarrierHull ? 0.25f : 0.35f;
            var baseSlowdown = isCarrierHull ? 28f : 20f;
            var baseArrival = isCarrierHull ? 4f : 3f;

            ecb.AddComponent(ship, new Carrier
            {
                CarrierId = BuildCarrierId(colony.Index, tick),
                AffiliationEntity = Entity.Null,
                Speed = baseSpeed,
                Acceleration = baseAcceleration,
                Deceleration = baseDeceleration,
                TurnSpeed = baseTurn,
                SlowdownDistance = baseSlowdown,
                ArrivalDistance = baseArrival,
                PatrolCenter = position,
                PatrolRadius = isCarrierHull ? 60f : 45f
            });

            ecb.AddComponent(ship, new MovementCommand
            {
                TargetPosition = position,
                ArrivalThreshold = 2f
            });

            ecb.AddComponent(ship, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = baseSpeed,
                CurrentSpeed = 0f,
                Acceleration = baseAcceleration,
                Deceleration = baseDeceleration,
                TurnSpeed = baseTurn,
                SlowdownDistance = baseSlowdown,
                ArrivalDistance = baseArrival,
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
                TargetPosition = position,
                StateTimer = 0f,
                StateStartTick = tick
            });

            ecb.AddComponent(ship, new EntityIntent
            {
                Mode = IntentMode.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = position,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = tick,
                Priority = InterruptPriority.Low,
                IsValid = 0
            });

            ecb.AddBuffer<Interrupt>(ship);

            ecb.AddComponent(ship, new VesselPhysicalProperties
            {
                Radius = isCarrierHull ? 3.6f : 2.4f,
                BaseMass = isCarrierHull ? 260f : 100f,
                HullDensity = isCarrierHull ? 1.2f : 1.1f,
                CargoMassPerUnit = 0.02f,
                Restitution = 0.08f,
                TangentialDamping = 0.25f
            });

            ecb.AddComponent(ship, isCarrierHull ? DockingCapacity.HeavyCarrier : DockingCapacity.LightCarrier);
            ecb.AddBuffer<DockedEntity>(ship);
            ecb.AddBuffer<ResourceStorage>(ship);

            ecb.AddComponent(ship, new CarrierHullId { HullId = hullId });
            var hullIntegrity = isCarrierHull ? HullIntegrity.HeavyCarrier : HullIntegrity.LightCarrier;
            hullIntegrity.Max = hullIntegrity.BaseMax;
            hullIntegrity.Current = hullIntegrity.BaseMax;
            ecb.AddComponent(ship, hullIntegrity);
            ecb.AddComponent(ship, isCarrierHull ? CrewCapacity.HeavyCarrier : CrewCapacity.LightCarrier);

            ecb.AddComponent(ship, new CaptainOrder
            {
                Type = CaptainOrderType.None,
                Status = CaptainOrderStatus.None,
                Priority = 255,
                TargetEntity = Entity.Null,
                TargetPosition = position,
                IssuedTick = tick,
                TimeoutTick = 0u,
                IssuingAuthority = Entity.Null
            });

            ecb.AddComponent(ship, tech);
            ecb.AddComponent(ship, WarpPrecision.FromTier(ResolveWarpTier(tech)));
            ecb.AddComponent(ship, ReinforcementTactics.Coordinated);
            ecb.AddComponent(ship, new ReinforcementArrival
            {
                ArrivalPosition = position,
                ArrivalRotation = quaternion.identity,
                ArrivalTick = tick,
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

            return ship;
        }

        private static WarpTechTier ResolveWarpTier(in TechLevel tech)
        {
            var tier = math.max((int)tech.MiningTech,
                math.max((int)tech.CombatTech, math.max((int)tech.HaulingTech, (int)tech.ProcessingTech)));

            return tier switch
            {
                >= 4 => WarpTechTier.Experimental,
                3 => WarpTechTier.Advanced,
                2 => WarpTechTier.Standard,
                1 => WarpTechTier.Basic,
                _ => WarpTechTier.Primitive
            };
        }

        private static FixedString64Bytes BuildCarrierId(int colonyIndex, uint tick)
        {
            FixedString64Bytes id = default;
            id.Append('b');
            id.Append('i');
            id.Append('z');
            id.Append('-');
            AppendDigits(ref id, colonyIndex);
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

        private void EnsureLink(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity business,
            Entity asset,
            Space4XBusinessAssetType assetType,
            uint tick,
            FixedString64Bytes catalogId)
        {
            if (business == Entity.Null || asset == Entity.Null)
            {
                return;
            }

            if (_assetLinkLookup.HasBuffer(business))
            {
                var links = _assetLinkLookup[business];
                for (int i = 0; i < links.Length; i++)
                {
                    if (links[i].Asset == asset)
                    {
                        if (links[i].CatalogId.IsEmpty && !catalogId.IsEmpty)
                        {
                            var existing = links[i];
                            existing.CatalogId = catalogId;
                            links[i] = existing;
                        }
                        return;
                    }
                }

                links.Add(new Space4XBusinessAssetLink
                {
                    Asset = asset,
                    AssetType = assetType,
                    AssignedTick = tick,
                    CatalogId = catalogId
                });
            }
            else
            {
                var links = ecb.AddBuffer<Space4XBusinessAssetLink>(business);
                links.Add(new Space4XBusinessAssetLink
                {
                    Asset = asset,
                    AssetType = assetType,
                    AssignedTick = tick,
                    CatalogId = catalogId
                });
            }
        }
    }
}
