using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Processes signature unlock requests and validates milestone requirements.
    /// Adds SignatureSelection entries when valid.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ExtendedMasterySystem))]
    public partial struct SignatureUnlockSystem : ISystem
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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get signature catalog
            if (!SystemAPI.TryGetSingleton<SpellSignatureCatalogRef>(out var signatureCatalogRef) ||
                !signatureCatalogRef.Blob.IsCreated)
            {
                return;
            }

            ref var signatureCatalog = ref signatureCatalogRef.Blob.Value;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessSignatureUnlocksJob
            {
                SignatureCatalog = signatureCatalog,
                CurrentTick = currentTick,
                Ecb = ecb
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessSignatureUnlocksJob : IJobEntity
        {
            [ReadOnly]
            public SpellSignatureBlob SignatureCatalog;

            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<SignatureUnlockRequest> requests,
                ref DynamicBuffer<ExtendedSpellMastery> mastery,
                ref DynamicBuffer<SignatureSelection> selections,
                ref DynamicBuffer<SignatureUnlockedEvent> events)
            {
                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    // Find mastery entry
                    int masteryIndex = -1;
                    for (int j = 0; j < mastery.Length; j++)
                    {
                        if (mastery[j].SpellId.Equals(request.SpellId))
                        {
                            masteryIndex = j;
                            break;
                        }
                    }

                    if (masteryIndex < 0)
                    {
                        requests.RemoveAt(i);
                        continue; // Spell not learned
                    }

                    var masteryEntry = mastery[masteryIndex];

                    // Validate milestone requirement
                    bool canUnlock = false;
                    switch (request.MilestoneIndex)
                    {
                        case 0: // 200%
                            canUnlock = (masteryEntry.Signatures & SpellSignatureFlags.Signature1Unlocked) != 0;
                            break;
                        case 1: // 300%
                            canUnlock = (masteryEntry.Signatures & SpellSignatureFlags.Signature2Unlocked) != 0;
                            break;
                        case 2: // 400%
                            canUnlock = (masteryEntry.Signatures & SpellSignatureFlags.Signature3Unlocked) != 0;
                            break;
                    }

                    if (!canUnlock)
                    {
                        requests.RemoveAt(i);
                        continue; // Milestone not reached
                    }

                    // Check if signature already selected for this milestone
                    bool alreadySelected = false;
                    for (int j = 0; j < selections.Length; j++)
                    {
                        if (selections[j].SpellId.Equals(request.SpellId) &&
                            selections[j].MilestoneIndex == request.MilestoneIndex)
                        {
                            alreadySelected = true;
                            break;
                        }
                    }

                    if (alreadySelected)
                    {
                        requests.RemoveAt(i);
                        continue; // Already selected
                    }

                    // Validate signature exists
                    bool signatureExists = false;
                    for (int j = 0; j < SignatureCatalog.Signatures.Length; j++)
                    {
                        if (SignatureCatalog.Signatures[j].SignatureId.Equals(request.SignatureId))
                        {
                            signatureExists = true;
                            break;
                        }
                    }

                    if (!signatureExists)
                    {
                        requests.RemoveAt(i);
                        continue; // Invalid signature
                    }

                    // Add signature selection
                    selections.Add(new SignatureSelection
                    {
                        SpellId = request.SpellId,
                        SignatureId = request.SignatureId,
                        MilestoneIndex = request.MilestoneIndex,
                        SelectedTick = CurrentTick
                    });

                    // Emit event
                    events.Add(new SignatureUnlockedEvent
                    {
                        SpellId = request.SpellId,
                        SignatureId = request.SignatureId,
                        Entity = entity,
                        MilestoneIndex = request.MilestoneIndex,
                        UnlockedTick = CurrentTick
                    });

                    requests.RemoveAt(i);
                }
            }
        }
    }
}

