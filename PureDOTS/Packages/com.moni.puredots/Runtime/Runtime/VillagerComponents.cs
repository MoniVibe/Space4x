using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Core villager identification and type.
    /// </summary>
    public struct VillagerId : IComponentData
    {
        public int Value;
        public int FactionId;
    }

    /// <summary>
    /// Need stats tracking for individual entities.
    /// Tracks hunger, fatigue, sleep, and general health.
    /// Matches Godgame schema requirements exactly.
    /// </summary>
    public struct VillagerNeeds : IComponentData
    {
        /// <summary>
        /// Hunger level (0-100, where 0 = starving, 100 = fully fed).
        /// </summary>
        public byte Food;

        /// <summary>
        /// Fatigue level (0-100, where 0 = exhausted, 100 = fully rested).
        /// </summary>
        public byte Rest;

        /// <summary>
        /// Sleep need (0-100, where 0 = sleep-deprived, 100 = fully rested).
        /// </summary>
        public byte Sleep;

        /// <summary>
        /// Overall health status (0-100, separate from combat HP).
        /// </summary>
        public byte GeneralHealth;

        /// <summary>
        /// Current health value (for combat/registry compatibility).
        /// </summary>
        public float Health;

        /// <summary>
        /// Maximum health value (for combat/registry compatibility).
        /// </summary>
        public float MaxHealth;

        /// <summary>
        /// Hunger level (0-100). Backed by both byte Food (legacy) and float Hunger for precision.
        /// </summary>
        public float Hunger;

        /// <summary>
        /// Energy level (0-100, typically synced with Rest).
        /// </summary>
        public float Energy;

        /// <summary>
        /// Current morale (0-100).
        /// </summary>
        public float Morale;

        /// <summary>
        /// Comfort temperature preference (-100 to 100).
        /// </summary>
        public float Temperature;

        // Convenience float accessors (clamped to 0-100 range).
        public float HungerFloat
        {
            get
            {
                // Prefer float storage; fall back to legacy byte if float is unset.
                var value = math.abs(Hunger) > 1e-5f ? Hunger : Food;
                return math.clamp(value, 0f, 100f);
            }
        }
        public float EnergyFloat => math.clamp(Energy, 0f, 100f);
        public float MoraleFloat => math.clamp(Morale, 0f, 100f);
        public float TemperatureFloat => math.clamp(Temperature, -100f, 100f);

        public void SetHunger(float value)
        {
            var clamped = math.clamp(value, 0f, 100f);
            Hunger = clamped;
            Food = (byte)math.round(clamped);
        }

        public void SetEnergy(float value)
        {
            var clamped = math.clamp(value, 0f, 100f);
            Energy = clamped;
            Rest = (byte)math.round(clamped);
        }

        public void SetMorale(float value)
        {
            var clamped = math.clamp(value, 0f, 100f);
            Morale = clamped;
        }

        public void SetTemperature(float value)
        {
            Temperature = math.clamp(value, -100f, 100f);
        }
    }

    /// <summary>
    /// Simple mana / worship pool for miracle interactions.
    /// </summary>
    public struct VillagerMana : IComponentData
    {
        public float CurrentMana;
        public float MaxMana;
    }

    /// <summary>
    /// Primary and derived attribute values used for combat / skill checks.
    /// </summary>
    public struct VillagerAttributes : IComponentData
    {
        // Primary attributes (0-10 typical range)
        public float Physique;
        public float Finesse;
        public float Willpower;

        // Derived attributes (0-200 range)
        public float Strength;
        public float Agility;
        public float Intelligence;
        public float Wisdom;
    }

    /// <summary>
    /// Belief + faith metadata used for worship flows.
    /// </summary>
    public struct VillagerBelief : IComponentData
    {
        public FixedString64Bytes PrimaryDeityId;
        public float Faith;            // 0-1 belief strength
        public float WorshipProgress;  // Normalized worship progress
    }

    /// <summary>
    /// Reputation / fame counters used by social systems.
    /// </summary>
    public struct VillagerReputation : IComponentData
    {
        public float Fame;
        public float Infamy;
        public float Honor;
        public float Glory;
        public float Renown;
        public float Reputation;
    }

    /// <summary>
    /// Comfort profile for thermal systems.
    /// </summary>
    public struct VillagerTemperatureProfile : IComponentData
    {
        public float PreferredTemperature; // Degrees Celsius
        public float ColdTolerance;        // Lowest comfortable temp
        public float HeatTolerance;        // Highest comfortable temp
    }

    /// <summary>
    /// Contextual sensor ranges for vision/hearing modifiers.
    /// </summary>
    public struct VillagerSensorProfile : IComponentData
    {
        public float VisionLitRange;
        public float VisionDimRange;
        public float VisionObscuredRange;
        public float HearingQuietRange;
        public float HearingNoisyRange;
        public float HearingCrowdedRange;
    }

    /// <summary>
    /// Supported discipline types. Mirrors GodGame.Villagers.Discipline.
    /// </summary>
    public enum VillagerDisciplineType : byte
    {
        Unassigned = 0,
        Forester = 1,
        Breeder = 2,
        Worshipper = 3,
        Miner = 4,
        Warrior = 5,
        Farmer = 6,
        Builder = 7
    }

    /// <summary>
    /// Discipline state used for job assignment & UI.
    /// </summary>
    public struct VillagerDisciplineState : IComponentData
    {
        public VillagerDisciplineType Value;
        public byte Level;
        public float Experience;
    }

    /// <summary>
    /// Mood/morale stat for individual entities.
    /// Affects behavior, productivity, and social interactions.
    /// Matches Godgame schema requirements exactly.
    /// </summary>
    public struct VillagerMood : IComponentData
    {
        /// <summary>
        /// Current mood value (0-100, where 0 = very unhappy, 100 = very happy).
        /// </summary>
        public float Mood;

        /// <summary>
        /// Target mood value that mood lerps toward.
        /// </summary>
        public float TargetMood;

        /// <summary>
        /// Rate multiplier for mood adjustment per second.
        /// </summary>
        public float MoodChangeRate;

        /// <summary>
        /// Current wellbeing score (0-100) derived from needs.
        /// </summary>
        public float Wellbeing;

        /// <summary>
        /// Alignment with the divine/player (0-100, where 0 = opposed, 50 = neutral, 100 = aligned).
        /// Affected by miracles, creature actions, and player interactions.
        /// </summary>
        public float Alignment;

        /// <summary>
        /// Tick when alignment was last influenced by an external source.
        /// </summary>
        public uint LastAlignmentInfluenceTick;
    }

    /// <summary>
    /// Availability flags for scheduling / job systems.
    /// </summary>
    public struct VillagerAvailability : IComponentData
    {
        public byte IsAvailable;    // 1 = available for work
        public byte IsReserved;     // 1 = currently reserved/assigned
        public uint LastChangeTick; // For history/analytics
        public float BusyTime;      // Seconds spent busy in current stint
    }

    /// <summary>
    /// Current AI state and goal.
    /// </summary>
    public struct VillagerAIState : IComponentData
    {
        public enum State : byte
        {
            Idle = 0,
            Working = 1,
            Eating = 2,
            Sleeping = 3,
            Fleeing = 4,
            Fighting = 5,
            Dead = 6,
            Travelling = 7
        }

        public enum Goal : byte
        {
            None = 0,
            SurviveHunger = 1,
            Work = 2,
            Rest = 3,
            Flee = 4,
            Fight = 5,
            Socialize = 6,
            Reproduce = 7
        }

        public State CurrentState;
        public Goal CurrentGoal;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float StateTimer;
        public uint StateStartTick;
    }

    /// <summary>
    /// Current job assignment.
    /// </summary>
    public struct VillagerJob : IComponentData
    {
        public enum JobType : byte
        {
            None = 0,
            Farmer = 1,
            Builder = 2,
            Gatherer = 3,
            Hunter = 4,
            Guard = 5,
            Priest = 6,
            Merchant = 7,
            Crafter = 8
        }

        public enum JobPhase : byte
        {
            Idle = 0,
            Assigned = 1,
            Gathering = 2,
            Delivering = 3,
            Completed = 4,
            Interrupted = 5,
            Building = 6,
            Crafting = 7,
            Fighting = 8
        }

        public JobType Type;
        public JobPhase Phase;
        public uint ActiveTicketId;
        public float Productivity; // 0-1, affected by needs and morale
        public uint LastStateChangeTick;
    }

    /// <summary>
    /// Movement and pathfinding data.
    /// </summary>
    public struct VillagerMovement : IComponentData
    {
        public float3 Velocity;
        public float3 DesiredVelocity;
        public float BaseSpeed;
        public float CurrentSpeed;
        public quaternion DesiredRotation;
        public byte IsMoving;
        public byte IsStuck;
        public uint LastMoveTick;
    }

    /// <summary>
    /// Reference to companion entity containing inventory buffer (SoA optimization).
    /// Hot archetype holds only the reference (4 bytes) instead of the full buffer.
    /// </summary>
    public struct VillagerInventoryRef : IComponentData
    {
        public Entity CompanionEntity;  // Reference to companion entity with inventory buffer
    }

    /// <summary>
    /// Inventory of carried resources (moved to companion entity for SoA optimization).
    /// </summary>
    public struct VillagerInventoryItem : IBufferElementData
    {
        public ushort ResourceTypeIndex;  // Optimized: use index instead of FixedString64Bytes
        public float Amount;
        public float MaxCarryCapacity;
    }

    /// <summary>
    /// Withdrawal requests generated by villager behaviour systems.
    /// </summary>
    public struct VillagerWithdrawRequest : IBufferElementData
    {
        public FixedString64Bytes ResourceTypeId;
        public float Amount;
        public Entity TargetStorehouse;
    }

    /// <summary>
    /// Combat stats for individual entities.
    /// Used for combat calculations and registry queries.
    /// Matches Godgame schema requirements exactly.
    /// </summary>
    public struct VillagerCombatStats : IComponentData
    {
        /// <summary>
        /// Attack damage value (for damage calculations).
        /// </summary>
        public float AttackDamage;

        /// <summary>
        /// Attack speed value (attacks per second or similar).
        /// </summary>
        public float AttackSpeed;

        /// <summary>
        /// Current combat target entity.
        /// </summary>
        public Entity CurrentTarget;
    }

    /// <summary>
    /// Social relationships with other villagers.
    /// </summary>
    public struct VillagerRelationship : IBufferElementData
    {
        public Entity OtherVillager;
        public float RelationshipValue; // -100 to 100
        public byte RelationType; // Friend, Enemy, Family, etc.
    }

    public static class VillagerRelationshipTypes
    {
        public const byte Family = 1;
        public const byte Mentor = 2;
        public const byte Squad = 3;
    }

    /// <summary>
    /// Path waypoints for navigation.
    /// </summary>
    public struct VillagerPathWaypoint : IBufferElementData
    {
        public float3 Position;
        public byte WaypointType; // Normal, Door, Ladder, etc.
    }

    /// <summary>
    /// Sensor data for environment awareness.
    /// </summary>
    public struct VillagerSensors : IComponentData
    {
        public float VisionRange;
        public float HearingRange;
        public Entity NearestThreat;
        public Entity NearestFood;
        public Entity NearestShelter;
        public float3 LastKnownThreatPosition;
        public uint LastSensorUpdateTick;
    }

    /// <summary>
    /// Packed flags component replacing multiple tag components for SoA optimization.
    /// Reduces memory footprint from 5×16 bytes (80 bytes) to 2 bytes.
    /// </summary>
    public struct VillagerFlags : IComponentData
    {
        private byte _flags1;
        private byte _flags2;

        // Flags byte 1 (bits 0-7)
        public bool IsSelected
        {
            get => (_flags1 & 0x01) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x01 : _flags1 & ~0x01);
        }

        public bool IsHighlighted
        {
            get => (_flags1 & 0x02) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x02 : _flags1 & ~0x02);
        }

        public bool IsInCombat
        {
            get => (_flags1 & 0x04) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x04 : _flags1 & ~0x04);
        }

        public bool IsCarrying
        {
            get => (_flags1 & 0x08) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x08 : _flags1 & ~0x08);
        }

        public bool IsDead
        {
            get => (_flags1 & 0x10) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x10 : _flags1 & ~0x10);
        }

        public bool IsIdle
        {
            get => (_flags1 & 0x20) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x20 : _flags1 & ~0x20);
        }

        public bool IsWorking
        {
            get => (_flags1 & 0x40) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x40 : _flags1 & ~0x40);
        }

        public bool IsFleeing
        {
            get => (_flags1 & 0x80) != 0;
            set => _flags1 = (byte)(value ? _flags1 | 0x80 : _flags1 & ~0x80);
        }

        // Flags byte 2 (bits 8-15) - reserved for future use
        public bool IsPlayerPriority
        {
            get => (_flags2 & 0x01) != 0;
            set => _flags2 = (byte)(value ? _flags2 | 0x01 : _flags2 & ~0x01);
        }

        public bool IsScheduled
        {
            get => (_flags2 & 0x02) != 0;
            set => _flags2 = (byte)(value ? _flags2 | 0x02 : _flags2 & ~0x02);
        }

        public bool IsReserved
        {
            get => (_flags2 & 0x04) != 0;
            set => _flags2 = (byte)(value ? _flags2 | 0x04 : _flags2 & ~0x04);
        }

        public byte RawFlags1 => _flags1;
        public byte RawFlags2 => _flags2;
    }

    /// <summary>
    /// Legacy tag components maintained for backward compatibility during migration.
    /// These will be removed once all systems migrate to VillagerFlags.
    /// </summary>
    [Obsolete("Use VillagerFlags.IsSelected instead")]
    public struct VillagerSelectedTag : IComponentData { }
    
    [Obsolete("Use VillagerFlags.IsHighlighted instead")]
    public struct VillagerHighlightedTag : IComponentData { }
    
    [Obsolete("Use VillagerFlags.IsInCombat instead")]
    public struct VillagerInCombatTag : IComponentData { }
    
    [Obsolete("Use VillagerFlags.IsCarrying instead")]
    public struct VillagerCarryingTag : IComponentData { }
    
    [Obsolete("Use VillagerFlags.IsDead instead")]
    public struct VillagerDeadTag : IComponentData { }

    /// <summary>
    /// Command buffer for villager AI decisions.
    /// </summary>
    public struct VillagerCommand : IBufferElementData
    {
        public enum CommandType : byte
        {
            MoveTo = 0,
            Attack = 1,
            Gather = 2,
            Build = 3,
            Deposit = 4,
            Eat = 5,
            Sleep = 6,
            Flee = 7,
            Guard = 8
        }

        public CommandType Type;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float Priority;
        public uint IssuedTick;
    }

    /// <summary>
    /// Villager spawning configuration.
    /// </summary>
    public struct VillagerSpawnConfig : IComponentData
    {
        public Entity VillagerPrefab;
        public float3 SpawnPosition;
        public int InitialPopulation;
        public float SpawnRadius;
        public int MaxPopulation;
        public float ReproductionRate;
    }

    /// <summary>
    /// Reference to companion entity containing stats, animation state, and memory buffer (SoA optimization).
    /// Hot archetype holds only the reference (4 bytes) instead of multiple components.
    /// </summary>
    public struct VillagerCompanionRef : IComponentData
    {
        public Entity CompanionEntity;  // Reference to companion entity with stats, animation, memory
    }

    /// <summary>
    /// Tracks villager animations and visual state (moved to companion entity for SoA optimization).
    /// </summary>
    public struct VillagerAnimationState : IComponentData
    {
        public enum AnimationType : byte
        {
            Idle = 0,
            Walk = 1,
            Run = 2,
            Work = 3,
            Attack = 4,
            Die = 5,
            Eat = 6,
            Sleep = 7,
            Celebrate = 8
        }

        public AnimationType CurrentAnimation;
        public float AnimationSpeed;
        public float AnimationTime;
        public byte ShouldUpdateAnimation;
    }

    /// <summary>
    /// Villager statistics for gameplay tracking (moved to companion entity for SoA optimization).
    /// </summary>
    public struct VillagerStats : IComponentData
    {
        public uint BirthTick;
        public uint DeathTick;
        public float TotalWorkDone;
        public float TotalResourcesGathered;
        public int EnemiesKilled;
        public int BuildingsConstructed;
        public float DistanceTraveled;
    }

    /// <summary>
    /// Memory of recent events for decision making (moved to companion entity buffer for SoA optimization).
    /// </summary>
    public struct VillagerMemoryEvent : IBufferElementData
    {
        public enum EventType : byte
        {
            SawThreat = 0,
            WasAttacked = 1,
            FoundFood = 2,
            CompletedWork = 3,
            MetFriend = 4,
            HeardSound = 5
        }

        public EventType Type;
        public float3 EventPosition;
        public Entity RelatedEntity;
        public uint EventTick;
        public float Importance;
    }

    /// <summary>
    /// Singleton for managing villager population.
    /// </summary>
    public struct VillagerPopulationData : IComponentData
    {
        public int TotalPopulation;
        public int ActiveWorkers;
        public int IdleVillagers;
        public int CombatCapable;
        public float AverageHealth;
        public float AverageHunger;
        public float AverageMorale;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Registry summary for the villager domain.
    /// </summary>
    public struct VillagerRegistry : IComponentData
    {
        public int TotalVillagers;
        public int AvailableVillagers;
        public int IdleVillagers;
        public int ReservedVillagers;
        public int CombatReadyVillagers;
        public float AverageHealthPercent;
        public float AverageMoralePercent;
        public float AverageEnergyPercent;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Registry entry snapshot describing a villager's current state.
    /// </summary>
    public struct VillagerRegistryEntry : IBufferElementData, IComparable<VillagerRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity VillagerEntity;
        public int VillagerId;
        public int FactionId;
        public float3 Position;
        public int CellId;
        public uint SpatialVersion;
        public VillagerJob.JobType JobType;
        public VillagerJob.JobPhase JobPhase;
        public uint ActiveTicketId;
        public ushort CurrentResourceTypeIndex;
        public byte AvailabilityFlags;
        public byte Discipline;
        public byte HealthPercent;
        public byte MoralePercent;
        public byte EnergyPercent;
        public byte AIState;
        public byte AIGoal;
        public Entity CurrentTarget;
        public float Productivity;

        public int CompareTo(VillagerRegistryEntry other)
        {
            return VillagerEntity.Index.CompareTo(other.VillagerEntity.Index);
        }

        public Entity RegistryEntity => VillagerEntity;

        public byte RegistryFlags => AvailabilityFlags;
    }

    /// <summary>
    /// Supplementary per-lesson entry that mirrors villager knowledge inside registry buffers.
    /// </summary>
    public struct VillagerLessonRegistryEntry : IBufferElementData, IComparable<VillagerLessonRegistryEntry>, IRegistryEntry
    {
        public Entity VillagerEntity;
        public FixedString64Bytes LessonId;
        public FixedString64Bytes AxisId;
        public FixedString64Bytes OppositeLessonId;
        public float Progress;
        public byte Difficulty;
        public byte MetadataFlags;

        public int CompareTo(VillagerLessonRegistryEntry other)
        {
            var compare = VillagerEntity.Index.CompareTo(other.VillagerEntity.Index);
            return compare != 0 ? compare : LessonId.CompareTo(other.LessonId);
        }

        public Entity RegistryEntity => VillagerEntity;
    }

    public static class VillagerAvailabilityFlags
    {
        public const byte Available = 1 << 0;
        public const byte Reserved = 1 << 1;

        public static byte FromAvailability(in VillagerAvailability availability)
        {
            byte flags = 0;
            if (availability.IsAvailable != 0)
            {
                flags |= Available;
            }

            if (availability.IsReserved != 0)
            {
                flags |= Reserved;
            }

            return flags;
        }
    }

    /// <summary>
    /// Optional binding that maps shared AI action indices to villager goals.
    /// </summary>
    public struct VillagerAIUtilityBinding : IComponentData
    {
        public FixedList32Bytes<VillagerAIState.Goal> Goals;
    }

    public struct VillagerLessonProgress
    {
        public FixedString64Bytes LessonId;
        public float Progress;
    }

    public struct VillagerKnowledge : IComponentData
    {
        public uint Flags;
        public FixedList32Bytes<VillagerLessonProgress> Lessons;

        public int FindLessonIndex(FixedString64Bytes lessonId)
        {
            if (lessonId.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < Lessons.Length; i++)
            {
                if (Lessons[i].LessonId.Equals(lessonId))
                {
                    return i;
                }
            }

            return -1;
        }

        public float GetProgress(FixedString64Bytes lessonId)
        {
            var index = FindLessonIndex(lessonId);
            return index >= 0 ? Lessons[index].Progress : 0f;
        }

        public bool TryAddLesson(FixedString64Bytes lessonId, float initialProgress = 1f)
        {
            if (lessonId.Length == 0 || Lessons.Length >= Lessons.Capacity || FindLessonIndex(lessonId) >= 0)
            {
                return false;
            }

            Lessons.Add(new VillagerLessonProgress
            {
                LessonId = lessonId,
                Progress = math.saturate(initialProgress)
            });
            return true;
        }

        public bool AddProgress(FixedString64Bytes lessonId, float delta, out float newProgress)
        {
            newProgress = 0f;
            if (lessonId.Length == 0 || math.abs(delta) < 1e-5f)
            {
                return false;
            }

            var index = EnsureLesson(lessonId);
            if (index < 0)
            {
                return false;
            }

            var entry = Lessons[index];
            entry.Progress = math.saturate(entry.Progress + delta);
            Lessons[index] = entry;
            newProgress = entry.Progress;
            return true;
        }

        public bool TrySetProgress(FixedString64Bytes lessonId, float value)
        {
            var index = FindLessonIndex(lessonId);
            if (index < 0)
            {
                return false;
            }

            var entry = Lessons[index];
            entry.Progress = math.saturate(value);
            Lessons[index] = entry;
            return true;
        }

        private int EnsureLesson(FixedString64Bytes lessonId)
        {
            var existing = FindLessonIndex(lessonId);
            if (existing >= 0)
            {
                return existing;
            }

            if (Lessons.Length >= Lessons.Capacity || lessonId.Length == 0)
            {
                return -1;
            }

            Lessons.Add(new VillagerLessonProgress
            {
                LessonId = lessonId,
                Progress = 0f
            });
            return Lessons.Length - 1;
        }
    }

    /// <summary>
    /// Knowledge store for aggregate entities (villages, guilds, companies, cultures, etc.).
    /// Mirrors the villager lesson schema but omits individual flags.
    /// </summary>
    public struct AggregateKnowledge : IComponentData
    {
        public FixedList32Bytes<VillagerLessonProgress> Lessons;

        public int FindLessonIndex(FixedString64Bytes lessonId)
        {
            if (lessonId.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < Lessons.Length; i++)
            {
                if (Lessons[i].LessonId.Equals(lessonId))
                {
                    return i;
                }
            }

            return -1;
        }

        public float GetProgress(FixedString64Bytes lessonId)
        {
            var index = FindLessonIndex(lessonId);
            return index >= 0 ? Lessons[index].Progress : 0f;
        }

        public bool TryAddLesson(FixedString64Bytes lessonId, float initialProgress = 0f)
        {
            if (lessonId.Length == 0 || Lessons.Length >= Lessons.Capacity || FindLessonIndex(lessonId) >= 0)
            {
                return false;
            }

            Lessons.Add(new VillagerLessonProgress
            {
                LessonId = lessonId,
                Progress = math.saturate(initialProgress)
            });
            return true;
        }

        public bool AddProgress(FixedString64Bytes lessonId, float delta, out float newProgress)
        {
            newProgress = 0f;
            if (lessonId.Length == 0 || math.abs(delta) < 1e-5f)
            {
                return false;
            }

            var index = EnsureLesson(lessonId);
            if (index < 0)
            {
                return false;
            }

            var entry = Lessons[index];
            entry.Progress = math.saturate(entry.Progress + delta);
            Lessons[index] = entry;
            newProgress = entry.Progress;
            return true;
        }

        public bool TrySetProgress(FixedString64Bytes lessonId, float value)
        {
            var index = FindLessonIndex(lessonId);
            if (index < 0)
            {
                return false;
            }

            var entry = Lessons[index];
            entry.Progress = math.saturate(value);
            Lessons[index] = entry;
            return true;
        }

        public void ApplyDecay(float decayDelta)
        {
            if (decayDelta <= 0f)
            {
                return;
            }

            for (int i = 0; i < Lessons.Length; i++)
            {
                var entry = Lessons[i];
                entry.Progress = math.max(0f, entry.Progress - decayDelta);
                Lessons[i] = entry;
            }
        }

        private int EnsureLesson(FixedString64Bytes lessonId)
        {
            var existing = FindLessonIndex(lessonId);
            if (existing >= 0)
            {
                return existing;
            }

            if (Lessons.Length >= Lessons.Capacity || lessonId.Length == 0)
            {
                return -1;
            }

            Lessons.Add(new VillagerLessonProgress
            {
                LessonId = lessonId,
                Progress = 0f
            });
            return Lessons.Length - 1;
        }
    }

    public static class VillagerKnowledgeFlags
    {
        public const uint HarvestLegendary = 1u << 0;
        public const uint HarvestRelic = 1u << 1;
    }

    public enum VillagerLessonShareSource : byte
    {
        Family = 0,
        Mentor = 1,
        Squad = 2,
        Aggregate = 3
    }

    public struct VillagerLessonShare : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public float Progress;
        public VillagerLessonShareSource Source;
    }

    /// <summary>
    /// Tracks which share producers have already contributed to a villager and when the last pulses occurred.
    /// </summary>
    public struct VillagerLessonShareState : IComponentData
    {
        public const byte FlagFamilyApplied = 1 << 0;

        public byte Flags;
        public uint LastFamilyShareTick;
        public uint LastMentorShareTick;
        public uint LastSquadShareTick;

        public readonly bool HasFamilyApplied => (Flags & FlagFamilyApplied) != 0;
    }

    /// <summary>
    /// Remembers which aggregate provided a lesson so we can decay or reinforce it when memberships change.
    /// </summary>
    public struct VillagerAggregateLessonTracker : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public Entity Aggregate;
        public float Support;
    }

}
