using System.Collections.Generic;
using PureDOTS.Runtime.Math;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlRoomDirectorSystem))]
    public partial struct Space4XFleetcrawlSpecialAbilitySystem : ISystem
    {
        private const float SpecialRadius = 62f;
        private const float SpecialDamage = 72f;
        private const int MaxTargets = 8;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<PlayerFlagshipTag>();
            state.RequireForUpdate<Space4XRunEnemyTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var em = state.EntityManager;

            foreach (var (directiveRef, flagshipTransform, flagshipEntity) in SystemAPI
                         .Query<RefRW<Space4XFleetcrawlPlayerDirective>, RefRO<LocalTransform>>()
                         .WithAll<PlayerFlagshipTag, Space4XRunPlayerTag>()
                         .WithEntityAccess())
            {
                var directive = directiveRef.ValueRO;
                if (directive.SpecialRequested == 0)
                {
                    continue;
                }

                // Requests can be injected externally, so keep a final cooldown gate in ECS.
                if (tick < directive.SpecialCooldownUntilTick)
                {
                    directive.SpecialRequested = 0;
                    directiveRef.ValueRW = directive;
                    continue;
                }

                if (!em.HasComponent<ShipSpecialEnergyState>(flagshipEntity))
                {
                    directive.SpecialRequested = 0;
                    directiveRef.ValueRW = directive;
                    continue;
                }

                var specialEnergy = em.GetComponentData<ShipSpecialEnergyState>(flagshipEntity);
                var specialCurrent = specialEnergy.Current;
                if (!ResourcePoolMath.TrySpend(ref specialCurrent, Space4XFleetcrawlSpecialEnergyRules.SpecialAbilityCost))
                {
                    specialEnergy.FailedSpendAttempts =
                        (ushort)math.min((int)ushort.MaxValue, specialEnergy.FailedSpendAttempts + 1);
                    specialEnergy.Current = specialCurrent;
                    em.SetComponentData(flagshipEntity, specialEnergy);
                    directive.SpecialRequested = 0;
                    directiveRef.ValueRW = directive;
                    Debug.Log(
                        $"[FleetcrawlAbility] SPECIAL denied tick={tick} reason=insufficient_special_energy current={specialEnergy.Current:0.##} cost={Space4XFleetcrawlSpecialEnergyRules.SpecialAbilityCost:0.##}");
                    continue;
                }

                specialEnergy.Current = specialCurrent;
                specialEnergy.LastSpent = Space4XFleetcrawlSpecialEnergyRules.SpecialAbilityCost;
                specialEnergy.LastSpendTick = tick;
                em.SetComponentData(flagshipEntity, specialEnergy);

                var hits = FireSpecialPulse(
                    ref state,
                    flagshipEntity,
                    flagshipTransform.ValueRO.Position,
                    tick);

                directive.SpecialRequested = 0;
                directiveRef.ValueRW = directive;

                Debug.Log(
                    $"[FleetcrawlAbility] SPECIAL fired tick={tick} hits={hits} radius={SpecialRadius:0.#} damage={SpecialDamage:0.#} energy_cost={Space4XFleetcrawlSpecialEnergyRules.SpecialAbilityCost:0.##}");
            }
        }

        private int FireSpecialPulse(
            ref SystemState state,
            Entity source,
            float3 origin,
            uint tick)
        {
            var em = state.EntityManager;
            var radiusSq = SpecialRadius * SpecialRadius;
            var candidates = new NativeList<SpecialTargetCandidate>(Allocator.Temp);

            foreach (var (enemyTransform, hull, side, enemyEntity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<HullIntegrity>, RefRO<ScenarioSide>>()
                         .WithAll<Space4XRunEnemyTag>()
                         .WithEntityAccess())
            {
                if (side.ValueRO.Side != 1 || hull.ValueRO.Current <= 0f)
                {
                    continue;
                }

                var distSq = math.distancesq(origin, enemyTransform.ValueRO.Position);
                if (distSq > radiusSq)
                {
                    continue;
                }

                candidates.Add(new SpecialTargetCandidate
                {
                    Target = enemyEntity,
                    DistanceSq = distSq
                });
            }

            if (candidates.Length == 0)
            {
                candidates.Dispose();
                return 0;
            }

            var candidateArray = candidates.AsArray();
            candidateArray.Sort(new SpecialTargetComparer());

            var hits = math.min(MaxTargets, candidates.Length);
            for (var i = 0; i < hits; i++)
            {
                var target = candidates[i].Target;
                if (!em.Exists(target))
                {
                    continue;
                }

                if (!em.HasBuffer<DamageEvent>(target))
                {
                    em.AddBuffer<DamageEvent>(target);
                }

                var damageEvents = em.GetBuffer<DamageEvent>(target);
                damageEvents.Add(new DamageEvent
                {
                    Source = source,
                    WeaponType = WeaponType.Ion,
                    RawDamage = SpecialDamage,
                    ShieldDamage = SpecialDamage * 0.35f,
                    ArmorDamage = SpecialDamage * 0.10f,
                    HullDamage = SpecialDamage * 0.55f,
                    Tick = tick,
                    IsCritical = 0
                });
            }

            candidates.Dispose();
            return hits;
        }

        private struct SpecialTargetCandidate
        {
            public Entity Target;
            public float DistanceSq;
        }

        private struct SpecialTargetComparer : IComparer<SpecialTargetCandidate>
        {
            public int Compare(SpecialTargetCandidate x, SpecialTargetCandidate y)
            {
                var distanceCompare = x.DistanceSq.CompareTo(y.DistanceSq);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                var indexCompare = x.Target.Index.CompareTo(y.Target.Index);
                if (indexCompare != 0)
                {
                    return indexCompare;
                }

                return x.Target.Version.CompareTo(y.Target.Version);
            }
        }
    }
}
