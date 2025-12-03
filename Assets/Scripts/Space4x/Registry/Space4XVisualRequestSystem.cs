using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Visuals;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// System that requests visual representation for Space4X mining vessels.
    /// Writes to MiningVisualRequest buffer on the MiningVisualManifest singleton.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MiningVesselSystem))]
    public partial struct Space4XVesselVisualRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Don't use RequireForUpdate for MiningVisualManifest - it calls HasSingleton which throws on duplicates
            // Instead, check manually in OnUpdate using TryGetSingletonEntity
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Use direct query to avoid HasSingleton exception on duplicates
            Entity manifestEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<MiningVisualManifest>().WithEntityAccess())
            {
                manifestEntity = entity;
                break; // Use first one found
            }

            if (manifestEntity == Entity.Null)
            {
                return;
            }

            // Allow visuals even if rewind isn't initialized or is in different mode
            if (SystemAPI.HasSingleton<RewindState>())
            {
                var rewindState = SystemAPI.GetSingleton<RewindState>();
                if (rewindState.Mode != RewindMode.Record)
                {
                    return;
                }
            }

            var requests = SystemAPI.GetBuffer<MiningVisualRequest>(manifestEntity);

            int requestCount = 0;
            foreach (var (vessel, transform, entity) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                requests.Add(new MiningVisualRequest
                {
                    VisualType = MiningVisualType.Vessel,
                    SourceEntity = entity,
                    Position = transform.ValueRO.Position,
                    BaseScale = 1.0f // Base scale, can be adjusted based on cargo level
                });
                requestCount++;
            }

#if UNITY_EDITOR && SPACE4X_DEBUG_VISUAL
            if (requestCount > 0)
            {
                UnityEngine.Debug.Log($"[Space4XVesselVisualRequestSystem] Added {requestCount} vessel visual requests");
            }
#endif
        }
    }

    /// <summary>
    /// System that requests visual representation for Space4X carrier ships.
    /// Writes to MiningVisualRequest buffer on the MiningVisualManifest singleton.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierPatrolSystem))]
    public partial struct Space4XCarrierVisualRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Don't use RequireForUpdate for MiningVisualManifest - it calls HasSingleton which throws on duplicates
            // Instead, check manually in OnUpdate using TryGetSingletonEntity
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Use direct query to avoid HasSingleton exception on duplicates
            Entity manifestEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<MiningVisualManifest>().WithEntityAccess())
            {
                manifestEntity = entity;
                break; // Use first one found
            }

            if (manifestEntity == Entity.Null)
            {
                return;
            }

            // Allow visuals even if rewind isn't initialized or is in different mode
            if (SystemAPI.HasSingleton<RewindState>())
            {
                var rewindState = SystemAPI.GetSingleton<RewindState>();
                if (rewindState.Mode != RewindMode.Record)
                {
                    return;
                }
            }

            var requests = SystemAPI.GetBuffer<MiningVisualRequest>(manifestEntity);

            int requestCount = 0;
            foreach (var (carrier, transform, entity) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                requests.Add(new MiningVisualRequest
                {
                    VisualType = MiningVisualType.Vessel,
                    SourceEntity = entity,
                    Position = transform.ValueRO.Position,
                    BaseScale = 1.5f // Carriers are larger than vessels
                });
                requestCount++;
            }

#if UNITY_EDITOR && SPACE4X_DEBUG_VISUAL
            if (requestCount > 0)
            {
                UnityEngine.Debug.Log($"[Space4XCarrierVisualRequestSystem] Added {requestCount} carrier visual requests");
            }
#endif
        }
    }
}

