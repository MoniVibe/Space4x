#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;
using Space4X.Runtime;
using PureDOTS.Runtime;
using Unity.Rendering;
using UnityEngine;
using Shared.Demo;

namespace Space4X.Demo
{
    /// <summary>
    /// Fallback demo spawner for editor/dev builds: if no gameplay entities appear after a short wait,
    /// spawn a minimal scene so the demo always shows something.
    /// </summary>
    public struct DemoSpawnedTag : IComponentData {}

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DemoWorldPreflightSystem : ISystem
    {
        uint warmup;
        bool done;

        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            warmup = 8; // let bootstrap/scenario run

            // Ensure a demo scenario marker exists so systems can gate on it.
            if (!SystemAPI.TryGetSingleton<DemoScenarioState>(out _))
            {
                var e = s.EntityManager.CreateEntity(typeof(DemoScenarioState));
                s.EntityManager.SetComponentData(e, new DemoScenarioState
                {
                    IsActive = true,
                    StartWorldSeconds = 0f,
                    EnableSpace4x = true,
                    EnableGodgame = false,
                    EnableEconomy = false,
                    Current = DemoScenario.Space4XPhysicsOnly,
                    IsInitialized = true,
                    BootPhase = DemoBootPhase.Done
                });
            }
        }

        public void OnUpdate(ref SystemState s)
        {
            // TEMP: disable this demo spawner to keep Space4X debug orbit cubes clear
            s.Enabled = false;
            return;

            if (done) return;
            if (warmup-- > 0) return;

            if (SystemAPI.QueryBuilder().WithAll<DemoSpawnedTag>().Build().CalculateEntityCount() > 0)
            { done = true; return; }

            if (SystemAPI.QueryBuilder().WithAll<VesselAIState>().Build().IsEmptyIgnoreFilter)
                SpawnSpace4xFallback(ref s);
            done = true;
        }

        static void SpawnSpace4xFallback(ref SystemState s)
        {
            var em = s.EntityManager;

            // Simple visible test entity (red cube at origin, elevated)
            var testEntity = em.CreateEntity();
            em.AddComponentData(testEntity, LocalTransform.FromPosition(new float3(0f, 5f, 0f)));
            DemoRenderUtil.MakeRenderable(em, testEntity, new float4(1f, 0f, 0f, 1f)); // Red cube
            UnityEngine.Debug.Log("[DemoWorldPreflightSystem] Spawned visible test entity at (0, 5, 0).");

            // Station placeholder
            var station = em.CreateEntity();
            em.AddComponentData(station, new LocalTransform { Position = float3.zero, Rotation = quaternion.identity, Scale = 1f });
            em.AddComponentData(station, new FacilityZone { RadiusMeters = 40f });
            em.AddComponentData(station, new RefitFacilityTag());
            em.AddComponentData(station, HullIntegrity.LightCarrier);
            DemoRenderUtil.MakeRenderable(em, station, new float4(0.2f, 0.9f, 0.4f, 1f));

            // Two simple vessels
            for (int i = 0; i < 2; i++)
            {
                var ship = em.CreateEntity();
                em.AddComponentData(ship, new LocalTransform
                {
                    Position = new float3(30f, 0f, i == 0 ? 12f : -12f),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponentData(ship, HullIntegrity.LightCraft);
                em.AddComponentData(ship, new VesselAIState { CurrentState = VesselAIState.State.Idle, CurrentGoal = VesselAIState.Goal.Idle });
                em.AddComponentData(ship, new VesselMovement { BaseSpeed = 10f, CurrentSpeed = 0f, IsMoving = 0 });
                DemoRenderUtil.MakeRenderable(em, ship, new float4(0.95f, 0.75f, 0.15f, 1f));
            }

            // mark so we don't respawn
            var tag = em.CreateEntity(); em.AddComponentData(tag, new DemoSpawnedTag());

            UnityEngine.Debug.Log("[DemoWorldPreflightSystem] Spawned fallback primitives.");
        }
    }
}
#endif
