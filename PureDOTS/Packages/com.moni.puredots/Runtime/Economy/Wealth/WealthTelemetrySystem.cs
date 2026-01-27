using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Provides telemetry queries for wealth system:
    /// - Top N richest dynasties
    /// - Inequality metrics (Gini coefficient)
    /// - Bankruptcy tracking
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WealthTelemetrySystem : ISystem
    {
        private BufferLookup<TopDynastyEntity> _topDynastyEntityLookup;
        private BufferLookup<TopDynastyBalance> _topDynastyBalanceLookup;
        private BufferLookup<BankruptEntity> _bankruptEntityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _topDynastyEntityLookup = state.GetBufferLookup<TopDynastyEntity>(false);
            _topDynastyBalanceLookup = state.GetBufferLookup<TopDynastyBalance>(false);
            _bankruptEntityLookup = state.GetBufferLookup<BankruptEntity>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _topDynastyEntityLookup.Update(ref state);
            _topDynastyBalanceLookup.Update(ref state);
            _bankruptEntityLookup.Update(ref state);

            // Update telemetry components if they exist
            foreach (var (telemetry, entity) in SystemAPI.Query<RefRW<WealthTelemetry>>().WithEntityAccess())
            {
                UpdateTelemetry(ref state, entity, ref telemetry.ValueRW);
            }
        }

        [BurstCompile]
        private void UpdateTelemetry(ref SystemState state, Entity telemetryEntity, ref WealthTelemetry telemetry)
        {
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            telemetry.LastUpdateTick = tickTimeState.Tick;

            // Calculate top N dynasties
            var dynasties = new NativeList<(Entity entity, float balance)>(64, Allocator.Temp);
            
            foreach (var (wealth, entity) in SystemAPI.Query<RefRO<DynastyWealth>>().WithEntityAccess())
            {
                dynasties.Add((entity, wealth.ValueRO.Balance));
            }

            // Sort by balance descending
            dynasties.Sort(new DynastyBalanceComparer());

            // Update telemetry buffers
            if (!_topDynastyEntityLookup.HasBuffer(telemetryEntity))
            {
                state.EntityManager.AddBuffer<TopDynastyEntity>(telemetryEntity);
            }
            if (!_topDynastyBalanceLookup.HasBuffer(telemetryEntity))
            {
                state.EntityManager.AddBuffer<TopDynastyBalance>(telemetryEntity);
            }

            var topDynastyEntities = _topDynastyEntityLookup[telemetryEntity];
            var topDynastyBalances = _topDynastyBalanceLookup[telemetryEntity];
            
            topDynastyEntities.Clear();
            topDynastyBalances.Clear();

            var topN = math.min(telemetry.TopNDynastiesCount, dynasties.Length);
            for (int i = 0; i < topN; i++)
            {
                topDynastyEntities.Add(new TopDynastyEntity { Entity = dynasties[i].entity });
                topDynastyBalances.Add(new TopDynastyBalance { Balance = dynasties[i].balance });
            }

            dynasties.Dispose();

            // Calculate Gini coefficient for inequality
            var allWealth = new NativeList<float>(1024, Allocator.Temp);
            
            foreach (var (wealth, entity) in SystemAPI.Query<RefRO<VillagerWealth>>().WithEntityAccess())
            {
                allWealth.Add(wealth.ValueRO.Balance);
            }

            if (allWealth.Length > 0)
            {
                telemetry.GiniCoefficient = CalculateGini(ref allWealth);
            }

            allWealth.Dispose();

            // Track bankruptcies (negative balance crossings)
            if (!_bankruptEntityLookup.HasBuffer(telemetryEntity))
            {
                state.EntityManager.AddBuffer<BankruptEntity>(telemetryEntity);
            }

            var bankruptEntities = _bankruptEntityLookup[telemetryEntity];
            var bankruptSet = new NativeHashSet<Entity>(bankruptEntities.Length, Allocator.Temp);
            
            foreach (var bankrupt in bankruptEntities)
            {
                bankruptSet.Add(bankrupt.Entity);
            }

            foreach (var (wealth, entity) in SystemAPI.Query<RefRO<VillagerWealth>>().WithEntityAccess())
            {
                var balance = wealth.ValueRO.Balance;
                if (balance < 0f && !bankruptSet.Contains(entity))
                {
                    bankruptEntities.Add(new BankruptEntity { Entity = entity });
                    bankruptSet.Add(entity);
                }
                else if (balance >= 0f && bankruptSet.Contains(entity))
                {
                    for (int i = bankruptEntities.Length - 1; i >= 0; i--)
                    {
                        if (bankruptEntities[i].Entity == entity)
                        {
                            bankruptEntities.RemoveAt(i);
                            break;
                        }
                    }
                    bankruptSet.Remove(entity);
                }
            }

            bankruptSet.Dispose();
        }

        [BurstCompile]
        private static float CalculateGini(ref NativeList<float> values)
        {
            if (values.Length == 0)
            {
                return 0f;
            }

            // Sort values
            values.Sort();

            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
                for (int j = 0; j < values.Length; j++)
                {
                    sum += math.abs(values[i] - values[j]);
                }
            }

            if (total == 0f || values.Length == 0)
            {
                return 0f;
            }

            return sum / (2f * values.Length * total);
        }
    }

    /// <summary>
    /// Comparer for sorting dynasties by balance.
    /// </summary>
    struct DynastyBalanceComparer : System.Collections.Generic.IComparer<(Entity entity, float balance)>
    {
        public int Compare((Entity entity, float balance) x, (Entity entity, float balance) y)
        {
            return y.balance.CompareTo(x.balance); // Descending
        }
    }

    /// <summary>
    /// Telemetry component for wealth system metrics.
    /// </summary>
    public struct WealthTelemetry : IComponentData
    {
        public float GiniCoefficient;
        public int TopNDynastiesCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Buffer of top N dynasty entities.
    /// </summary>
    public struct TopDynastyEntity : IBufferElementData
    {
        public Entity Entity;
    }

    /// <summary>
    /// Buffer of top N dynasty balances.
    /// </summary>
    public struct TopDynastyBalance : IBufferElementData
    {
        public float Balance;
    }

    /// <summary>
    /// Buffer of bankrupt entities.
    /// </summary>
    public struct BankruptEntity : IBufferElementData
    {
        public Entity Entity;
    }
}

