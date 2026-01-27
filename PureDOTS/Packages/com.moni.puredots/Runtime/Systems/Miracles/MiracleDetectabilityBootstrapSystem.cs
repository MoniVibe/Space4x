using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using RuntimeMiracleType = PureDOTS.Runtime.Miracles.MiracleType;
using ComponentsMiracleType = PureDOTS.Runtime.Components.MiracleType;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Validates that miracle effect entities have spatial residency and sensor signatures/emitters.
    /// Debug-only validation system to enforce "Miracles are Detectable" contract.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    public partial struct MiracleDetectabilityBootstrapSystem : ISystem
    {
        [BurstDiscard]
        private static void LogMissingDetectability(int entityIndex, MiracleId miracleId)
        {
            UnityEngine.Debug.LogWarning($"[MiracleDetectability] Miracle effect entity {entityIndex} (MiracleId={miracleId}) missing both SensorSignature and SensorySignalEmitter. Add at least one for detectability. " +
                "Use SensorSignature for visible/contact miracles (e.g., Fireball), SensorySignalEmitter for ambient/area miracles (e.g., Food smell).");
        }

        [BurstDiscard]
        private static void LogMissingLocalTransform(int entityIndex, MiracleId miracleId)
        {
            UnityEngine.Debug.LogWarning($"[MiracleDetectability] Miracle effect entity {entityIndex} (MiracleId={miracleId}) missing LocalTransform component. Add LocalTransform for spatial residency.");
        }

        [BurstDiscard]
        private static void LogMissingLocalTransformLegacy(int entityIndex, ComponentsMiracleType tokenType)
        {
            UnityEngine.Debug.LogWarning($"[MiracleDetectability] Miracle token entity {entityIndex} (Type={tokenType}) missing LocalTransform component.");
        }

        [BurstDiscard]
        private static void LogMissingDetectabilityLegacy(int entityIndex, ComponentsMiracleType tokenType)
        {
            UnityEngine.Debug.LogWarning($"[MiracleDetectability] Miracle token entity {entityIndex} (Type={tokenType}) missing both SensorSignature and SensorySignalEmitter.");
        }
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Only validate in debug builds
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.Tick > 10)
            {
                // Only validate for first few ticks to avoid spam
                return;
            }

            foreach (var (effect, entity) in SystemAPI.Query<RefRO<MiracleEffectNew>>().WithEntityAccess())
            {
                // Check for LocalTransform (spatial residency)
                if (!SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    LogMissingLocalTransform(entity.Index, effect.ValueRO.Id);
                }

                // Check for SensorSignature OR SensorySignalEmitter (at least one required)
                bool hasSignature = SystemAPI.HasComponent<SensorSignature>(entity);
                bool hasEmitter = SystemAPI.HasComponent<SensorySignalEmitter>(entity);

                if (!hasSignature && !hasEmitter)
                {
                    LogMissingDetectability(entity.Index, effect.ValueRO.Id);
                }
            }

            // Also check legacy MiracleTokenLegacy entities
            foreach (var (token, entity) in SystemAPI.Query<RefRO<MiracleTokenLegacy>>().WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    LogMissingLocalTransformLegacy(entity.Index, token.ValueRO.Type);
                }

                bool hasSignature = SystemAPI.HasComponent<SensorSignature>(entity);
                bool hasEmitter = SystemAPI.HasComponent<SensorySignalEmitter>(entity);

                if (!hasSignature && !hasEmitter)
                {
                    LogMissingDetectabilityLegacy(entity.Index, token.ValueRO.Type);
                }
            }
#endif
        }
    }
}



