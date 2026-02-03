using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Normalizes stance/race/culture buffers ahead of compliance so downstream systems can rely on aggregates.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XAffiliationComplianceSystem))]
    public partial struct Space4XCrewAggregationSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<AlignmentTriplet>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Phase A: Ensure buffers exist via ECB (no structural changes during iteration)
            foreach (var (_, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>>().WithEntityAccess())
            {
                // Ensure TopStance buffer exists
                if (!em.HasBuffer<TopStance>(entity))
                {
                    ecb.AddBuffer<TopStance>(entity);
                }

                // Ensure RacePresence buffer exists if entity has RaceId
                if (SystemAPI.HasComponent<RaceId>(entity) && !em.HasBuffer<RacePresence>(entity))
                {
                    ecb.AddBuffer<RacePresence>(entity);
                }

                // Ensure CulturePresence buffer exists if entity has CultureId
                if (SystemAPI.HasComponent<CultureId>(entity) && !em.HasBuffer<CulturePresence>(entity))
                {
                    ecb.AddBuffer<CulturePresence>(entity);
                }
            }

            // Playback ECB to apply structural changes
            ecb.Playback(em);
            ecb.Dispose();

            // Phase B: Pure aggregation, NO structural changes here
            foreach (var (_, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>>().WithEntityAccess())
            {
                AggregateStances(ref state, entity);
                AggregateRace(ref state, entity);
                AggregateCulture(ref state, entity);
            }
        }

        private void AggregateStances(ref SystemState state, Entity entity)
        {
            // Buffer should already exist from Phase A
            if (!SystemAPI.HasBuffer<TopStance>(entity))
            {
                return; // Should not happen after Phase A, but guard anyway
            }

            var topBuffer = SystemAPI.GetBuffer<TopStance>(entity);

            // Buffer should already exist from Phase A
            if (!SystemAPI.HasBuffer<StanceEntry>(entity))
            {
                // No source data, just clear the output buffer
                topBuffer.Clear();
                return;
            }

            var entries = SystemAPI.GetBuffer<StanceEntry>(entity);

            topBuffer.Clear();

            var accumulator = new StanceAccumulator();
            accumulator.Consider(entries);
            accumulator.WriteTo(topBuffer);
        }

        private void AggregateRace(ref SystemState state, Entity entity)
        {
            if (!SystemAPI.HasComponent<RaceId>(entity))
            {
                return;
            }

            // Buffer should already exist from Phase A
            if (!SystemAPI.HasBuffer<RacePresence>(entity))
            {
                return; // Should not happen after Phase A, but guard anyway
            }

            var race = SystemAPI.GetComponentRO<RaceId>(entity).ValueRO;
            var buffer = SystemAPI.GetBuffer<RacePresence>(entity);

            buffer.Clear();
            buffer.Add(new RacePresence
            {
                RaceId = race.Value,
                Count = 1
            });
        }

        private void AggregateCulture(ref SystemState state, Entity entity)
        {
            if (!SystemAPI.HasComponent<CultureId>(entity))
            {
                return;
            }

            // Buffer should already exist from Phase A
            if (!SystemAPI.HasBuffer<CulturePresence>(entity))
            {
                return; // Should not happen after Phase A, but guard anyway
            }

            var culture = SystemAPI.GetComponentRO<CultureId>(entity).ValueRO;
            var buffer = SystemAPI.GetBuffer<CulturePresence>(entity);

            buffer.Clear();
            buffer.Add(new CulturePresence
            {
                CultureId = culture.Value,
                Count = 1
            });
        }

        private struct StanceSample
        {
            public StanceId Id;
            public float Weight;
            public float Magnitude;
        }

        private struct StanceAccumulator
        {
            public StanceSample First;
            public StanceSample Second;
            public StanceSample Third;
            public int Count;

            public void Consider(DynamicBuffer<StanceEntry> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    Consider(buffer[i].StanceId, (float)buffer[i].Weight);
                }
            }

            private void Consider(StanceId StanceId, float weight)
            {
                var sample = new StanceSample
                {
                    Id = StanceId,
                    Weight = weight,
                    Magnitude = math.abs(weight)
                };

                if (TryReplace(ref sample))
                {
                    return;
                }

                Insert(ref sample);
            }

            public void WriteTo(DynamicBuffer<TopStance> buffer)
            {
                if (Count > 0)
                {
                    buffer.Add(new TopStance { StanceId = First.Id, Weight = (half)First.Weight });
                }

                if (Count > 1)
                {
                    buffer.Add(new TopStance { StanceId = Second.Id, Weight = (half)Second.Weight });
                }

                if (Count > 2)
                {
                    buffer.Add(new TopStance { StanceId = Third.Id, Weight = (half)Third.Weight });
                }
            }

            private bool TryReplace(ref StanceSample sample)
            {
                if (Count > 0 && First.Id == sample.Id)
                {
                    if (sample.Magnitude > First.Magnitude)
                    {
                        First = sample;
                    }
                    return true;
                }

                if (Count > 1 && Second.Id == sample.Id)
                {
                    if (sample.Magnitude > Second.Magnitude)
                    {
                        Second = sample;
                    }
                    return true;
                }

                if (Count > 2 && Third.Id == sample.Id)
                {
                    if (sample.Magnitude > Third.Magnitude)
                    {
                        Third = sample;
                    }
                    return true;
                }

                return false;
            }

            private void Insert(ref StanceSample sample)
            {
                if (Count == 0)
                {
                    First = sample;
                    Count = 1;
                    return;
                }

                if (sample.Magnitude > First.Magnitude)
                {
                    Third = Second;
                    Second = First;
                    First = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (Count < 2)
                {
                    Second = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (sample.Magnitude > Second.Magnitude)
                {
                    Third = Second;
                    Second = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (Count < 3)
                {
                    Third = sample;
                    Count = 3;
                    return;
                }

                if (sample.Magnitude > Third.Magnitude)
                {
                    Third = sample;
                }
            }
        }
    }
}

