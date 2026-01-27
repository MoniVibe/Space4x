#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Devtools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Instantiates prefabs from validated spawn candidates.
    /// Uses EndFixedStepSimulationEntityCommandBufferSystem for structural changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ValidateSpawnCandidatesSystem))]
    public partial struct InstantiateSpawnSystem : ISystem
    {
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString64Bytes _statNameHealth;
        private FixedString64Bytes _statNameSpeed;
        private FixedString64Bytes _statNameMass;
        private FixedString64Bytes _statNameDamage;
        private FixedString64Bytes _statNameRange;

        private EndFixedStepSimulationEntityCommandBufferSystem.Singleton _ecbSingleton;

        public void OnCreate(ref SystemState state)
        {
            _ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpawnRequest>();
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _statNameHealth = new FixedString64Bytes("health");
            _statNameSpeed = new FixedString64Bytes("speed");
            _statNameMass = new FixedString64Bytes("mass");
            _statNameDamage = new FixedString64Bytes("damage");
            _statNameRange = new FixedString64Bytes("range");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PrototypeRegistryBlob>())
            {
                return;
            }

            var registry = SystemAPI.GetSingleton<PrototypeRegistryBlob>();
            var ecb = _ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, candidates, validationResults, statOverrides, entity) in SystemAPI.Query<RefRO<SpawnRequest>, DynamicBuffer<SpawnCandidate>, DynamicBuffer<SpawnValidationResult>, DynamicBuffer<StatOverride>>().WithEntityAccess())
            {
                var req = request.ValueRO;

                // If ValidateOnly flag is set, skip instantiation
                if ((req.Flags & SpawnFlags.ValidateOnly) != 0)
                {
                    continue;
                }

                // Get prefab from registry
                if (!PrototypeLookup.TryGetPrefab(registry.Entries, req.PrototypeId, out var prefab))
                {
                    continue;
                }

                // Instantiate valid candidates
                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    var validation = validationResults[i];

                    if (candidate.IsValid == 0 || validation.FailureReason != ValidationFailureReason.None)
                    {
                        continue;
                    }

                    // Instantiate prefab
                    var instance = ecb.Instantiate(prefab);
                    ecb.SetComponent(instance, LocalTransform.FromPositionRotation(candidate.Position, candidate.Rotation));

                    // Apply stat overrides if any
                    ApplyStatOverrides(ecb, instance, statOverrides, registry.Entries, req.PrototypeId);

                    // Apply default alignment/outlook
                    if (PrototypeLookup.TryGetAlignmentDefault(registry.Entries, req.PrototypeId, out var alignment))
                    {
                        ecb.AddComponent(instance, alignment);
                    }
                    if (PrototypeLookup.TryGetOutlookDefault(registry.Entries, req.PrototypeId, out var outlook))
                    {
                        ecb.AddComponent(instance, outlook);
                    }
                }

                // Cleanup request entity
                ecb.DestroyEntity(entity);
            }
        }

        private void ApplyStatOverrides(EntityCommandBuffer ecb, Entity instance, DynamicBuffer<StatOverride> overrides, BlobAssetReference<BlobArray<PrototypeRegistry.PrototypeEntry>> registry, int prototypeId)
        {
            if (overrides.Length == 0)
            {
                return;
            }

            // Get default stats
            if (PrototypeLookup.TryGetStatsDefault(registry, prototypeId, out var defaultStats))
            {
                var stats = defaultStats;
                for (int i = 0; i < overrides.Length; i++)
                {
                    TryApplyStatOverride(ref stats, overrides[i]);
                }

                ecb.AddComponent(instance, stats);
            }
        }

        private bool TryApplyStatOverride(ref PrototypeStatsDefault stats, in StatOverride statOverride)
        {
            if (EqualsStatName(statOverride.Name, _statNameHealth))
            {
                stats.Health = statOverride.Value;
                return true;
            }

            if (EqualsStatName(statOverride.Name, _statNameSpeed))
            {
                stats.Speed = statOverride.Value;
                return true;
            }

            if (EqualsStatName(statOverride.Name, _statNameMass))
            {
                stats.Mass = statOverride.Value;
                return true;
            }

            if (EqualsStatName(statOverride.Name, _statNameDamage))
            {
                stats.Damage = statOverride.Value;
                return true;
            }

            if (EqualsStatName(statOverride.Name, _statNameRange))
            {
                stats.Range = statOverride.Value;
                return true;
            }

            return false;
        }

        private static bool EqualsStatName(in FixedString64Bytes candidate, in FixedString64Bytes targetLower)
        {
            unsafe
            {
                if (candidate.Length != targetLower.Length)
                {
                    return false;
                }

                var candidatePtr = candidate.GetUnsafePtr();
                var targetPtr = targetLower.GetUnsafePtr();

                for (int i = 0; i < targetLower.Length; i++)
                {
                    var normalized = ToLowerAscii(candidatePtr[i]);
                    if (normalized != targetPtr[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static byte ToLowerAscii(byte value)
        {
            const byte upperA = (byte)'A';
            const byte upperZ = (byte)'Z';
            return value >= upperA && value <= upperZ ? (byte)(value + 32) : value;
        }
    }
}
#endif
