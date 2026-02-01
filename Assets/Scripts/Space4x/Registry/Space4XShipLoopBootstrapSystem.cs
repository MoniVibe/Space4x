using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Bootstraps ship-scale components so AI, morale, crew, and department loops can run without full authoring.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XAuthoritySeatBootstrapSystem))]
    [UpdateBefore(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XShipLoopBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainOrder>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, shipEntity) in SystemAPI.Query<RefRO<CaptainOrder>>().WithNone<Prefab>().WithEntityAccess())
            {
                EnsureCaptainState(em, shipEntity, ref ecb);
                EnsureMorale(em, shipEntity, ref ecb);
                EnsureStance(em, shipEntity, ref ecb);
                EnsurePreFlight(em, shipEntity, ref ecb);
                EnsureThreatAssessment(em, shipEntity, ref ecb);
                EnsureAIQueue(em, shipEntity, ref ecb);
                EnsureCrewRoster(em, shipEntity, ref ecb);
                EnsureCarrierDepartments(em, shipEntity, ref ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void EnsureCaptainState(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<CaptainState>(shipEntity))
            {
                ecb.AddComponent(shipEntity, CaptainState.Default);
            }

            if (!em.HasComponent<CaptainReadiness>(shipEntity))
            {
                ecb.AddComponent(shipEntity, CaptainReadiness.Standard);
            }

            if (!em.HasBuffer<EscalationRequest>(shipEntity))
            {
                ecb.AddBuffer<EscalationRequest>(shipEntity);
            }
        }

        private static void EnsureMorale(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<MoraleState>(shipEntity))
            {
                var baseline = 0f;
                if (em.HasComponent<AlignmentTriplet>(shipEntity))
                {
                    baseline = MoraleUtility.ComputeBaseline(em.GetComponentData<AlignmentTriplet>(shipEntity));
                }
                ecb.AddComponent(shipEntity, MoraleState.FromBaseline(baseline));
            }

            if (!em.HasBuffer<MoraleModifier>(shipEntity))
            {
                ecb.AddBuffer<MoraleModifier>(shipEntity);
            }
        }

        private static void EnsureStance(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (em.HasComponent<VesselStanceComponent>(shipEntity))
            {
                return;
            }

            ecb.AddComponent(shipEntity, new VesselStanceComponent
            {
                CurrentStance = VesselStanceMode.Balanced,
                DesiredStance = VesselStanceMode.Balanced,
                StanceChangeTick = 0u
            });
        }

        private static void EnsurePreFlight(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (em.HasComponent<PreFlightCheck>(shipEntity))
            {
                return;
            }

            var provisions = 1f;
            if (em.HasComponent<SupplyStatus>(shipEntity))
            {
                provisions = em.GetComponentData<SupplyStatus>(shipEntity).ProvisionsRatio;
            }

            var crewMorale = 0.5f;
            if (em.HasComponent<MoraleState>(shipEntity))
            {
                var morale = (float)em.GetComponentData<MoraleState>(shipEntity).Current;
                crewMorale = math.saturate(0.5f + 0.5f * morale);
            }

            var hullRatio = 1f;
            if (em.HasComponent<HullIntegrity>(shipEntity))
            {
                var hull = em.GetComponentData<HullIntegrity>(shipEntity);
                hullRatio = hull.Max > 0f ? hull.Current / hull.Max : 1f;
            }

            ecb.AddComponent(shipEntity, new PreFlightCheck
            {
                ProvisionsLevel = (half)math.saturate(provisions),
                CrewMorale = (half)math.saturate(crewMorale),
                HullIntegrity = (half)math.saturate(hullRatio),
                CheckPassed = 1,
                CheckTick = 0u
            });
        }

        private static void EnsureThreatAssessment(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (em.HasComponent<ThreatAssessment>(shipEntity))
            {
                return;
            }

            ecb.AddComponent(shipEntity, new ThreatAssessment
            {
                LocalThreatLevel = (half)0f,
                RouteThreatLevel = (half)0f,
                DefensiveCapability = (half)1f,
                CanProceed = 1,
                AssessmentTick = 0u
            });
        }

        private static void EnsureAIQueue(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<AICommandQueue>(shipEntity))
            {
                ecb.AddComponent(shipEntity, new AICommandQueue { LastProcessedTick = 0u });
            }

            if (!em.HasBuffer<AIOrder>(shipEntity))
            {
                ecb.AddBuffer<AIOrder>(shipEntity);
            }
        }

        private static void EnsureCrewRoster(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            DynamicBuffer<PlatformCrewMember> crewBuffer;
            if (em.HasBuffer<PlatformCrewMember>(shipEntity))
            {
                crewBuffer = em.GetBuffer<PlatformCrewMember>(shipEntity);
            }
            else
            {
                crewBuffer = ecb.AddBuffer<PlatformCrewMember>(shipEntity);
            }

            if (crewBuffer.Length == 0)
            {
                TryAddPilot(em, shipEntity, ref crewBuffer);
            }

            var isCarrier = em.HasComponent<Carrier>(shipEntity);
            var defaultCrew = isCarrier ? 8 : 2;
            if (!em.HasComponent<CrewCapacity>(shipEntity))
            {
                var capacity = isCarrier ? CrewCapacity.LightCarrier : CrewCapacity.Create(20);
                capacity.CurrentCrew = math.max(defaultCrew, crewBuffer.Length);
                if (capacity.MaxCrew < capacity.CurrentCrew)
                {
                    capacity.MaxCrew = capacity.CurrentCrew;
                    capacity.CriticalMax = math.max(capacity.CriticalMax, capacity.CurrentCrew);
                }
                ecb.AddComponent(shipEntity, capacity);
            }
            else
            {
                var capacity = em.GetComponentData<CrewCapacity>(shipEntity);
                if (capacity.CurrentCrew == 0 && crewBuffer.Length > 0)
                {
                    capacity.CurrentCrew = math.max(defaultCrew, crewBuffer.Length);
                    if (capacity.MaxCrew < capacity.CurrentCrew)
                    {
                        capacity.MaxCrew = capacity.CurrentCrew;
                        capacity.CriticalMax = math.max(capacity.CriticalMax, capacity.CurrentCrew);
                    }
                    ecb.SetComponent(shipEntity, capacity);
                }
                else if (capacity.CurrentCrew == 0 && crewBuffer.Length == 0)
                {
                    capacity.CurrentCrew = defaultCrew;
                    if (capacity.MaxCrew < capacity.CurrentCrew)
                    {
                        capacity.MaxCrew = capacity.CurrentCrew;
                        capacity.CriticalMax = math.max(capacity.CriticalMax, capacity.CurrentCrew);
                    }
                    ecb.SetComponent(shipEntity, capacity);
                }
                else if (capacity.CurrentCrew < crewBuffer.Length)
                {
                    capacity.CurrentCrew = crewBuffer.Length;
                    if (capacity.MaxCrew < capacity.CurrentCrew)
                    {
                        capacity.MaxCrew = capacity.CurrentCrew;
                        capacity.CriticalMax = math.max(capacity.CriticalMax, capacity.CurrentCrew);
                    }
                    ecb.SetComponent(shipEntity, capacity);
                }
            }

            if (isCarrier && !em.HasComponent<CommandLoad>(shipEntity))
            {
                ecb.AddComponent(shipEntity, CommandLoad.LightCarrier);
            }

            if (isCarrier && !em.HasComponent<DockingCapacity>(shipEntity))
            {
                ecb.AddComponent(shipEntity, DockingCapacity.LightCarrier);
            }
        }

        private static void TryAddPilot(EntityManager em, Entity shipEntity, ref DynamicBuffer<PlatformCrewMember> crewBuffer)
        {
            if (em.HasComponent<VesselPilotLink>(shipEntity))
            {
                var pilot = em.GetComponentData<VesselPilotLink>(shipEntity).Pilot;
                if (pilot != Entity.Null)
                {
                    crewBuffer.Add(new PlatformCrewMember { CrewEntity = pilot, RoleId = 0 });
                    return;
                }
            }

            if (em.HasComponent<StrikeCraftPilotLink>(shipEntity))
            {
                var pilot = em.GetComponentData<StrikeCraftPilotLink>(shipEntity).Pilot;
                if (pilot != Entity.Null)
                {
                    crewBuffer.Add(new PlatformCrewMember { CrewEntity = pilot, RoleId = 0 });
                }
            }
        }

        private static void EnsureCarrierDepartments(EntityManager em, Entity shipEntity, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<Carrier>(shipEntity))
            {
                return;
            }

            DynamicBuffer<DepartmentStatsBuffer> statsBuffer;
            if (em.HasBuffer<DepartmentStatsBuffer>(shipEntity))
            {
                statsBuffer = em.GetBuffer<DepartmentStatsBuffer>(shipEntity);
            }
            else
            {
                statsBuffer = ecb.AddBuffer<DepartmentStatsBuffer>(shipEntity);
            }

            if (statsBuffer.Length == 0)
            {
                for (int i = 0; i < (int)DepartmentType.Count; i++)
                {
                    statsBuffer.Add(new DepartmentStatsBuffer
                    {
                        Stats = DepartmentStats.Default((DepartmentType)i)
                    });
                }
            }

            DynamicBuffer<DepartmentStaffingBuffer> staffingBuffer;
            if (em.HasBuffer<DepartmentStaffingBuffer>(shipEntity))
            {
                staffingBuffer = em.GetBuffer<DepartmentStaffingBuffer>(shipEntity);
            }
            else
            {
                staffingBuffer = ecb.AddBuffer<DepartmentStaffingBuffer>(shipEntity);
            }

            if (staffingBuffer.Length == 0)
            {
                var crewCount = 0;
                if (em.HasComponent<CrewCapacity>(shipEntity))
                {
                    crewCount = em.GetComponentData<CrewCapacity>(shipEntity).CurrentCrew;
                }
                else if (em.HasBuffer<PlatformCrewMember>(shipEntity))
                {
                    crewCount = em.GetBuffer<PlatformCrewMember>(shipEntity).Length;
                }

                var remaining = crewCount;
                for (int i = 0; i < (int)DepartmentType.Count; i++)
                {
                    var current = remaining > 0 ? 1 : 0;
                    if (remaining > 0)
                    {
                        remaining--;
                    }

                    staffingBuffer.Add(new DepartmentStaffingBuffer
                    {
                        Staffing = DepartmentStaffing.Create((DepartmentType)i, 1, current)
                    });
                }
            }

            if (!em.HasComponent<CarrierDepartmentState>(shipEntity))
            {
                ecb.AddComponent(shipEntity, CarrierDepartmentState.Default);
            }
        }
    }
}
