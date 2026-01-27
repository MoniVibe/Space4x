using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Bridge system to sync IndividualWealth with economy Chunk 1 (VillagerWealth).
    /// Reads VillagerWealth and writes to IndividualWealth for entities that have both.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WealthBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            var job = new BridgeJob
            {
                CurrentTick = timeState.Tick
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct BridgeJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                ref IndividualWealth individualWealth,
                in PureDOTS.Runtime.Economy.Wealth.VillagerWealth villagerWealth)
            {
                // Sync liquid funds from VillagerWealth.Balance
                individualWealth.LiquidFunds = villagerWealth.Balance;
                
                // Influence and Reputation are SimIndividual-specific, not synced from Chunk 1
                // They can be updated by other systems (social, progression, etc.)
                
                individualWealth.LastUpdateTick = CurrentTick;
            }
        }
    }
}

