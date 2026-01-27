using PureDOTS.Runtime.Alignment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Profile
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ProfileMutationBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureEventStream(ref state);
            EnsureMutationConfig(ref state);
            EnsureCatalog(ref state);
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var catalogRef in SystemAPI.Query<RefRW<ProfileActionCatalogSingleton>>())
            {
                if (catalogRef.ValueRO.Catalog.IsCreated)
                {
                    catalogRef.ValueRO.Catalog.Dispose();
                    catalogRef.ValueRW.Catalog = default;
                }
            }
        }

        private static void EnsureEventStream(ref SystemState state)
        {
            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProfileActionEventStream>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(
                typeof(ProfileActionEventStream),
                typeof(ProfileActionEventStreamConfig));
            state.EntityManager.AddBuffer<ProfileActionEvent>(entity);
            state.EntityManager.SetComponentData(entity, ProfileActionEventStreamConfig.CreateDefault());
        }

        private static void EnsureMutationConfig(ref SystemState state)
        {
            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProfileMutationConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(ProfileMutationConfig));
            state.EntityManager.SetComponentData(entity, ProfileMutationConfig.CreateDefault());
        }

        private static void EnsureCatalog(ref SystemState state)
        {
            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProfileActionCatalogSingleton>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(ProfileActionCatalogSingleton));
            state.EntityManager.SetComponentData(entity, new ProfileActionCatalogSingleton
            {
                Catalog = BuildDefaultCatalog()
            });
        }

        private static BlobAssetReference<ProfileActionCatalogBlob> BuildDefaultCatalog()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ProfileActionCatalogBlob>();
            var actions = builder.Allocate(ref root.Actions, 7);

            actions[0] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.ObeyOrder,
                AlignmentDelta = new float3(0.02f, 0.15f, 0.05f),
                OutlookDelta = new float4(0.2f, -0.05f, 0.05f, -0.1f),
                DispositionDeltaA = new float3(0.15f, 0.05f, 0.1f),
                DispositionDeltaB = new float3(-0.05f, -0.02f, 0.08f),
                Weight = 1f
            };
            actions[1] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.DisobeyOrder,
                AlignmentDelta = new float3(-0.04f, -0.18f, -0.1f),
                OutlookDelta = new float4(-0.2f, 0.12f, 0.06f, 0.2f),
                DispositionDeltaA = new float3(-0.2f, -0.06f, -0.16f),
                DispositionDeltaB = new float3(0.1f, 0.05f, -0.12f),
                Weight = 1f
            };
            actions[2] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.AttackCivilian,
                AlignmentDelta = new float3(-0.35f, -0.25f, -0.3f),
                OutlookDelta = new float4(-0.15f, 0.1f, 0.08f, 0.15f),
                DispositionDeltaA = new float3(-0.1f, -0.1f, -0.05f),
                DispositionDeltaB = new float3(0.15f, 0.12f, -0.08f),
                Weight = 1.2f
            };
            actions[3] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.AttackHostile,
                AlignmentDelta = new float3(-0.06f, 0.08f, -0.04f),
                OutlookDelta = new float4(0.05f, 0.02f, 0.12f, -0.03f),
                DispositionDeltaA = new float3(0.02f, -0.05f, 0.03f),
                DispositionDeltaB = new float3(0.08f, 0.12f, -0.02f),
                Weight = 0.9f
            };
            actions[4] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.Rescue,
                AlignmentDelta = new float3(0.35f, 0.12f, 0.22f),
                OutlookDelta = new float4(0.1f, -0.05f, 0.02f, -0.08f),
                DispositionDeltaA = new float3(0.1f, 0.12f, 0.07f),
                DispositionDeltaB = new float3(-0.08f, -0.05f, 0.12f),
                Weight = 1.1f
            };
            actions[5] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.MineResource,
                AlignmentDelta = new float3(0.01f, 0.04f, 0.03f),
                OutlookDelta = new float4(0.02f, 0.02f, 0.01f, -0.02f),
                DispositionDeltaA = new float3(0.06f, 0.08f, 0.05f),
                DispositionDeltaB = new float3(-0.04f, -0.02f, 0.12f),
                Weight = 0.6f
            };
            actions[6] = new ProfileActionDefinition
            {
                Token = ProfileActionToken.OrderIssued,
                AlignmentDelta = float3.zero,
                OutlookDelta = float4.zero,
                DispositionDeltaA = float3.zero,
                DispositionDeltaB = float3.zero,
                Weight = 0f
            };

            return builder.CreateBlobAssetReference<ProfileActionCatalogBlob>(Allocator.Persistent);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct ProfileMutationSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<OutlookEntry> _outlookLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private EntityStorageInfoLookup _entityInfo;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProfileActionEventStream>();
            state.RequireForUpdate<ProfileActionEventStreamConfig>();
            state.RequireForUpdate<ProfileMutationConfig>();
            state.RequireForUpdate<ProfileActionCatalogSingleton>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TimeState>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(false);
            _outlookLookup = state.GetBufferLookup<OutlookEntry>(false);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(false);
            _entityInfo = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<ProfileMutationConfig>();
            var catalog = SystemAPI.GetSingleton<ProfileActionCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return;
            }

            var streamEntity = SystemAPI.GetSingletonEntity<ProfileActionEventStream>();
            var stream = SystemAPI.GetComponentRW<ProfileActionEventStream>(streamEntity);
            var events = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);
            var pendingAccumulatorAdds = new NativeParallelHashMap<Entity, ProfileActionAccumulator>(
                math.max(16, events.Length),
                Allocator.Temp);
            var pendingTagAdds = new NativeList<Entity>(Allocator.Temp);
            var hasPendingAccumulatorAdds = false;

            if (events.Length > 0)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    ApplyEvent(ref state, catalog.Catalog, config, events[i], ref pendingAccumulatorAdds, ref pendingTagAdds, ref hasPendingAccumulatorAdds);
                }

                events.Clear();
                stream.ValueRW.EventCount = 0;
            }

            if (hasPendingAccumulatorAdds || pendingTagAdds.Length > 0)
            {
                var entityManager = state.EntityManager;
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                _entityInfo.Update(ref state);

                foreach (var pending in pendingAccumulatorAdds)
                {
                    var entity = pending.Key;
                    if (!_entityInfo.Exists(entity))
                    {
                        continue;
                    }
                    if (_entityInfo[entity].Chunk.Capacity <= 1)
                    {
                        continue;
                    }
                    if (!entityManager.HasComponent<ProfileActionAccumulator>(entity))
                    {
                        ecb.AddComponent(entity, pending.Value);
                    }
                }

                for (int i = 0; i < pendingTagAdds.Length; i++)
                {
                    var entity = pendingTagAdds[i];
                    if (!_entityInfo.Exists(entity))
                    {
                        continue;
                    }
                    if (_entityInfo[entity].Chunk.Capacity <= 1)
                    {
                        continue;
                    }
                    if (!entityManager.HasComponent<ProfileMutationPending>(entity))
                    {
                        ecb.AddComponent<ProfileMutationPending>(entity);
                    }
                }

                ecb.Playback(entityManager);
                ecb.Dispose();
            }

            pendingAccumulatorAdds.Dispose();
            pendingTagAdds.Dispose();

            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
            ApplyAccumulatedDrift(ref state, config, timeState.Tick);
        }

        private void ApplyEvent(
            ref SystemState state,
            BlobAssetReference<ProfileActionCatalogBlob> catalog,
            ProfileMutationConfig config,
            in ProfileActionEvent actionEvent,
            ref NativeParallelHashMap<Entity, ProfileActionAccumulator> pendingAccumulatorAdds,
            ref NativeList<Entity> pendingTagAdds,
            ref bool hasPendingAccumulatorAdds)
        {
            if (actionEvent.Actor == Entity.Null || actionEvent.Token == ProfileActionToken.None)
            {
                return;
            }

            if (!TryResolveDefinition(catalog, actionEvent.Token, out var definition))
            {
                return;
            }

            if (definition.Weight <= 0f)
            {
                return;
            }

            float magnitude = actionEvent.Magnitude <= 0 ? 1f : actionEvent.Magnitude / 100f;
            float multiplier = ResolveMultiplier(config, actionEvent.IntentFlags, actionEvent.JustificationFlags);
            float scale = magnitude * definition.Weight * multiplier;

            var alignmentDelta = definition.AlignmentDelta * (config.AlignmentScale * scale);
            var outlookDelta = definition.OutlookDelta * (config.OutlookScale * scale);
            var dispositionDeltaA = definition.DispositionDeltaA * (config.DispositionScale * scale);
            var dispositionDeltaB = definition.DispositionDeltaB * (config.DispositionScale * scale);

            var entityManager = state.EntityManager;
            ProfileActionAccumulator accumulator;
            var hasAccumulator = entityManager.HasComponent<ProfileActionAccumulator>(actionEvent.Actor);
            if (!hasAccumulator)
            {
                if (!pendingAccumulatorAdds.TryGetValue(actionEvent.Actor, out accumulator))
                {
                    accumulator = default;
                    accumulator.LastAppliedTick = actionEvent.Tick;
                }
            }
            else
            {
                accumulator = entityManager.GetComponentData<ProfileActionAccumulator>(actionEvent.Actor);
            }

            accumulator.Alignment += alignmentDelta;
            accumulator.Outlook += outlookDelta;
            accumulator.DispositionA += dispositionDeltaA;
            accumulator.DispositionB += dispositionDeltaB;
            accumulator.PendingMagnitude += math.csum(math.abs(alignmentDelta)) +
                                            math.csum(math.abs(outlookDelta)) +
                                            math.csum(math.abs(dispositionDeltaA)) +
                                            math.csum(math.abs(dispositionDeltaB));

            if (hasAccumulator)
            {
                entityManager.SetComponentData(actionEvent.Actor, accumulator);
            }
            else
            {
                pendingAccumulatorAdds[actionEvent.Actor] = accumulator;
                hasPendingAccumulatorAdds = true;
            }
            pendingTagAdds.Add(actionEvent.Actor);
        }

        private static bool TryResolveDefinition(BlobAssetReference<ProfileActionCatalogBlob> catalog, ProfileActionToken token, out ProfileActionDefinition definition)
        {
            ref var actions = ref catalog.Value.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                var candidate = actions[i];
                if (candidate.Token == token)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static float ResolveMultiplier(ProfileMutationConfig config, ProfileActionIntentFlags intent, ProfileActionJustificationFlags justification)
        {
            float multiplier = 1f;

            if ((intent & ProfileActionIntentFlags.Coerced) != 0)
            {
                multiplier *= config.CoercedMultiplier;
            }

            if ((intent & ProfileActionIntentFlags.Malice) != 0)
            {
                multiplier *= config.MaliceMultiplier;
            }

            if ((intent & ProfileActionIntentFlags.Benevolence) != 0)
            {
                multiplier *= config.BenevolenceMultiplier;
            }

            if ((justification & (ProfileActionJustificationFlags.SelfDefense |
                                  ProfileActionJustificationFlags.Sanctioned |
                                  ProfileActionJustificationFlags.Retaliation |
                                  ProfileActionJustificationFlags.Necessity)) != 0)
            {
                multiplier *= config.JustifiedMultiplier;
            }

            return multiplier;
        }

        private void ApplyAccumulatedDrift(ref SystemState state, ProfileMutationConfig config, uint currentTick)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (accumulator, entity) in SystemAPI.Query<RefRW<ProfileActionAccumulator>>()
                         .WithAll<ProfileMutationPending>()
                         .WithEntityAccess())
            {
                if (currentTick - accumulator.ValueRO.LastAppliedTick < config.ApplyIntervalTicks)
                {
                    continue;
                }

                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    var current = alignment.AsFloat3();
                    var delta = ClampVector(accumulator.ValueRO.Alignment, config.AlignmentMaxDelta);
                    var next = math.clamp(current + delta, new float3(-1f), new float3(1f));
                    entityManager.SetComponentData(entity, AlignmentTriplet.FromFloats(next.x, next.y, next.z));
                }

                if (_outlookLookup.HasBuffer(entity))
                {
                    var outlookBuffer = _outlookLookup[entity];
                    ApplyOutlookDelta(ref outlookBuffer, accumulator.ValueRO.Outlook, config.OutlookMaxDelta);
                }

                if (_behaviorDispositionLookup.HasComponent(entity))
                {
                    var disposition = _behaviorDispositionLookup[entity];
                    var deltaA = ClampVector(accumulator.ValueRO.DispositionA, config.DispositionMaxDelta);
                    var deltaB = ClampVector(accumulator.ValueRO.DispositionB, config.DispositionMaxDelta);

                    disposition.Compliance = math.clamp(disposition.Compliance + deltaA.x, 0f, 1f);
                    disposition.Caution = math.clamp(disposition.Caution + deltaA.y, 0f, 1f);
                    disposition.FormationAdherence = math.clamp(disposition.FormationAdherence + deltaA.z, 0f, 1f);
                    disposition.RiskTolerance = math.clamp(disposition.RiskTolerance + deltaB.x, 0f, 1f);
                    disposition.Aggression = math.clamp(disposition.Aggression + deltaB.y, 0f, 1f);
                    disposition.Patience = math.clamp(disposition.Patience + deltaB.z, 0f, 1f);

                    _behaviorDispositionLookup[entity] = disposition;
                }

                var updated = accumulator.ValueRW;
                updated.LastAppliedTick = currentTick;
                updated.Alignment *= config.AccumulatorDecay;
                updated.Outlook *= config.AccumulatorDecay;
                updated.DispositionA *= config.AccumulatorDecay;
                updated.DispositionB *= config.AccumulatorDecay;
                updated.PendingMagnitude *= config.AccumulatorDecay;

                accumulator.ValueRW = updated;

                if (updated.PendingMagnitude <= 0.0005f)
                {
                    ecb.RemoveComponent<ProfileMutationPending>(entity);
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static float3 ClampVector(float3 value, float maxDelta)
        {
            return math.clamp(value, new float3(-maxDelta), new float3(maxDelta));
        }

        private static void ApplyOutlookDelta(ref DynamicBuffer<OutlookEntry> buffer, float4 delta, float maxDelta)
        {
            ApplyOutlookDelta(ref buffer, Outlook.Loyalist, delta.x, maxDelta);
            ApplyOutlookDelta(ref buffer, Outlook.Opportunist, delta.y, maxDelta);
            ApplyOutlookDelta(ref buffer, Outlook.Fanatic, delta.z, maxDelta);
            ApplyOutlookDelta(ref buffer, Outlook.Mutinous, delta.w, maxDelta);
        }

        private static void ApplyOutlookDelta(ref DynamicBuffer<OutlookEntry> buffer, Outlook outlookId, float delta, float maxDelta)
        {
            if (math.abs(delta) <= math.EPSILON)
            {
                return;
            }

            delta = math.clamp(delta, -maxDelta, maxDelta);

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].OutlookId != outlookId)
                {
                    continue;
                }

                var entry = buffer[i];
                entry.Weight = (half)math.clamp((float)entry.Weight + delta, -1f, 1f);
                buffer[i] = entry;
                return;
            }

            buffer.Add(new OutlookEntry
            {
                OutlookId = outlookId,
                Weight = (half)math.clamp(delta, -1f, 1f)
            });
        }
    }
}
