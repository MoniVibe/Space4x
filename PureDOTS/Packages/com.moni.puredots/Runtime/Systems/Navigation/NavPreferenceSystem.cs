using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Assigns navigation preference profiles to entities based on their type and role.
    /// Civilian caravans, military couriers, raiders, etc. get different preference profiles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateBefore(typeof(PathRequestSystem))]
    public partial struct NavPreferenceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Assign preferences to bands (raiders get raider profile, others get civilian)
            foreach (var (bandStats, entity) in SystemAPI.Query<RefRO<BandStats>>()
                .WithEntityAccess()
                .WithNone<NavPreference>())
            {
                // Check if band is raiding/routing (raider profile)
                var flags = bandStats.ValueRO.Flags;
                if ((flags & BandStatusFlags.Routing) != 0)
                {
                    ecb.AddComponent(entity, NavPreference.CreateRaider());
                }
                else
                {
                    ecb.AddComponent(entity, NavPreference.CreateCivilianCaravan());
                }
            }

            // Assign preferences to armies (military courier profile)
            foreach (var (armyStats, entity) in SystemAPI.Query<RefRO<ArmyStats>>()
                .WithEntityAccess()
                .WithNone<NavPreference>())
            {
                ecb.AddComponent(entity, NavPreference.CreateMilitaryCourier());
            }

            // Assign default preferences to entities requesting paths without preferences
            foreach (var (pathRequest, entity) in SystemAPI.Query<RefRO<PathRequest>>()
                .WithEntityAccess()
                .WithNone<NavPreference>())
            {
                ecb.AddComponent(entity, NavPreference.CreateDefault());
            }

            ecb.Playback(state.EntityManager);
        }
    }
}






















