using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Detects when entities have 4+ hybrid spells and enables school founding.
    /// Processes school founding requests and creates FoundedSchool entries.
    /// Grants founder bonuses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(HybridizationSystem))]
    public partial struct SchoolFoundingSystem : ISystem
    {
        // Instance field for Burst-compatible FixedString pattern (initialized in OnCreate)
        private FixedString32Bytes _schoolIdPrefix;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            // Initialize FixedString pattern here (OnCreate is not Burst-compiled)
            _schoolIdPrefix = new FixedString32Bytes("School_");
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

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Update founding progress
            new UpdateFoundingProgressJob
            {
                CurrentTick = currentTick
            }.ScheduleParallel();

            // Process founding requests
            new ProcessFoundingRequestsJob
            {
                CurrentTick = currentTick,
                Ecb = ecb,
                SchoolIdPrefix = _schoolIdPrefix
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct UpdateFoundingProgressJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref SchoolFoundingProgress progress,
                in DynamicBuffer<HybridSpell> hybridSpells)
            {
                // Count hybrid spells
                progress.HybridSpellCount = (byte)math.min(hybridSpells.Length, 255);

                // Determine primary/secondary schools from hybrids
                if (hybridSpells.Length > 0)
                {
                    // Count school occurrences (use byte keys to satisfy NativeHashMap constraints)
                    var schoolCounts = new NativeHashMap<byte, int>(16, Allocator.Temp);
                    for (int i = 0; i < hybridSpells.Length; i++)
                    {
                        var school = (byte)hybridSpells[i].DerivedSchool;
                        if (schoolCounts.ContainsKey(school))
                        {
                            schoolCounts[school] = schoolCounts[school] + 1;
                        }
                        else
                        {
                            schoolCounts[school] = 1;
                        }
                    }

                    // Find most common schools
                    SpellSchool primary = SpellSchool.None;
                    SpellSchool secondary = SpellSchool.None;
                    int primaryCount = 0;
                    int secondaryCount = 0;

                    foreach (var kvp in schoolCounts)
                    {
                        var school = (SpellSchool)kvp.Key;
                        if (kvp.Value > primaryCount)
                        {
                            secondary = primary;
                            secondaryCount = primaryCount;
                            primary = school;
                            primaryCount = kvp.Value;
                        }
                        else if (kvp.Value > secondaryCount)
                        {
                            secondary = school;
                            secondaryCount = kvp.Value;
                        }
                    }

                    progress.PrimarySchool = primary;
                    progress.SecondarySchool = secondary;
                    schoolCounts.Dispose();
                }

                // Check if can found school (4+ hybrids)
                progress.CanFoundSchool = hybridSpells.Length >= 4;
                progress.LastUpdateTick = CurrentTick;
            }
        }

        [BurstCompile]
        public partial struct ProcessFoundingRequestsJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public FixedString32Bytes SchoolIdPrefix;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<SchoolFoundingRequest> requests,
                in SchoolFoundingProgress progress,
                in DynamicBuffer<HybridSpell> hybridSpells,
                ref DynamicBuffer<FoundedSchool> foundedSchools,
                ref DynamicBuffer<SchoolFoundedEvent> events)
            {
                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    // Validate prerequisites
                    if (!progress.CanFoundSchool || hybridSpells.Length < 4)
                    {
                        requests.RemoveAt(i);
                        continue; // Not enough hybrids
                    }

                    // Generate school ID (using Append for Burst compatibility)
                    var schoolId = new FixedString64Bytes();
                    schoolId.Append(SchoolIdPrefix);
                    schoolId.Append(entity.Index);
                    schoolId.Append('_');
                    schoolId.Append(CurrentTick);

                    // Calculate complexity based on hybrid schools
                    byte complexity = CalculateComplexity(progress.PrimarySchool, progress.SecondarySchool);

                    // Create list of required hybrid IDs
                    var requiredHybrids = new NativeList<FixedString64Bytes>(hybridSpells.Length, Allocator.Temp);
                    for (int j = 0; j < hybridSpells.Length; j++)
                    {
                        requiredHybrids.Add(hybridSpells[j].HybridSpellId);
                    }

                    // Note: BlobArray allocation would need to be done via ECB or separate system
                    // For now, we'll store the count and reference the hybrid spells buffer

                    // Create founded school entry
                    // Note: BlobArray for RequiredHybrids would need special handling
                    // Simplified version: store count and reference hybrid spells
                    foundedSchools.Add(new FoundedSchool
                    {
                        SchoolId = schoolId,
                        DisplayName = request.ProposedName,
                        FounderEntity = entity,
                        FoundedTick = CurrentTick,
                        Complexity = complexity,
                        RequiredHybrids = default, // Would need blob allocation
                        Description = request.ProposedDescription
                    });

                    // Grant founder bonuses (add component if it doesn't exist, or set it if it does)
                    Ecb.AddComponent(entityInQueryIndex, entity, new SchoolFounderBonus
                    {
                        SchoolId = schoolId,
                        CastSpeedBonus = 1.2f,      // 20% faster casting
                        EffectBonus = 1.15f,        // 15% stronger effects
                        TeachingBonus = 1.3f,       // 30% better teaching
                        ManaCostReduction = 0.1f    // 10% mana reduction
                    });

                    // Emit event
                    events.Add(new SchoolFoundedEvent
                    {
                        SchoolId = schoolId,
                        DisplayName = request.ProposedName,
                        FounderEntity = entity,
                        Complexity = complexity,
                        FoundedTick = CurrentTick
                    });

                    requiredHybrids.Dispose();
                    requests.RemoveAt(i);
                }
            }

            [BurstCompile]
            private byte CalculateComplexity(SpellSchool primary, SpellSchool secondary)
            {
                // Simple complexity calculation
                // More diverse schools = higher complexity
                if (primary == secondary || secondary == SpellSchool.None)
                {
                    return 5; // Medium complexity
                }
                return 7; // Higher complexity for diverse schools
            }
        }
    }
}

