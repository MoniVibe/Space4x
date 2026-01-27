using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Time.Branching
{
    /// <summary>
    /// Represents a timeline branch for what-if simulation.
    /// </summary>
    public struct TimelineBranch : IComponentData
    {
        public FixedString64Bytes BranchId;        // Unique identifier
        public FixedString64Bytes ParentBranchId;  // Branch this split from (empty = main)
        public uint BranchPointTick;               // When the branch was created
        public uint CurrentTick;                   // Current tick in this branch
        public uint MaxTick;                       // Furthest simulated tick
        
        public bool IsMainTimeline;                // Is this the canonical timeline
        public bool IsActive;                      // Currently being simulated
        public bool IsFrozen;                      // No more simulation allowed
        public bool IsMarkedForMerge;              // Will be merged back to parent
        
        public float DivergenceScore;              // How different from parent (0-1)
        public byte Priority;                      // Simulation priority (0=low, 2=high)
    }

    /// <summary>
    /// Request to create a what-if scenario.
    /// </summary>
    public struct WhatIfRequest : IComponentData
    {
        public FixedString64Bytes ScenarioName;
        public uint StartTick;                     // Where to branch from
        public uint SimulationDuration;            // How many ticks to simulate
        public bool AutoMergeIfBetter;             // Merge if outcome is better
        public bool PreserveAfterComplete;         // Keep branch after simulation
        public uint RequestTick;
    }

    /// <summary>
    /// Modification to apply in what-if scenario.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct WhatIfModification : IBufferElementData
    {
        public FixedString32Bytes ModificationType; // "set_resource", "spawn_entity", "remove_entity"
        public Entity TargetEntity;
        public FixedString32Bytes ParameterName;
        public float ParameterValue;
        public float3 Position;                    // For spawn operations
    }

    /// <summary>
    /// Result of a what-if simulation.
    /// </summary>
    public struct WhatIfResult : IComponentData
    {
        public FixedString64Bytes ScenarioName;
        public FixedString64Bytes BranchId;
        
        // Outcome metrics
        public float FinalPopulation;
        public float FinalResources;
        public float FinalHappiness;
        public float FinalMilitary;
        
        // Comparison to baseline
        public float PopulationDelta;
        public float ResourcesDelta;
        public float HappinessDelta;
        public float MilitaryDelta;
        
        public float OverallScore;                 // Weighted composite
        public bool IsBetterThanBaseline;
        
        public uint SimulatedTicks;
        public uint CompletedTick;
        public bool IsComplete;
    }

    /// <summary>
    /// Configuration for time spine branching.
    /// </summary>
    public struct TimeSpineConfig : IComponentData
    {
        public byte MaxConcurrentBranches;         // Limit active branches
        public uint MaxBranchDuration;             // Max ticks a branch can run
        public uint BranchGCInterval;              // How often to clean up old branches
        public float DivergenceThreshold;          // Auto-freeze if too divergent
        public bool AllowNestedBranches;           // Branches from branches
        public bool AutoPruneLowPriority;          // Remove low-priority branches when at limit
    }

    /// <summary>
    /// Snapshot of world state at a branch point.
    /// </summary>
    public struct BranchSnapshot : IComponentData
    {
        public FixedString64Bytes BranchId;
        public uint SnapshotTick;
        public int EntityCount;
        public float TotalResources;
        public float TotalPopulation;
        public uint SnapshotSize;                  // Bytes
        public bool IsValid;
    }

    /// <summary>
    /// Entity state delta for branch comparison.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct BranchEntityDelta : IBufferElementData
    {
        public Entity EntityRef;
        public FixedString32Bytes ComponentType;
        public FixedString32Bytes FieldName;
        public float BaselineValue;
        public float BranchValue;
        public float Delta;
    }

    /// <summary>
    /// Request to merge a branch back to parent.
    /// </summary>
    public struct BranchMergeRequest : IComponentData
    {
        public FixedString64Bytes SourceBranchId;
        public FixedString64Bytes TargetBranchId;  // Usually parent or main
        public bool FullMerge;                     // Replace vs selective merge
        public uint RequestTick;
    }

    /// <summary>
    /// Selective merge configuration.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct MergeSelection : IBufferElementData
    {
        public Entity EntityToMerge;
        public FixedString32Bytes ComponentToMerge; // Empty = all components
        public bool MergeChildren;
    }

    /// <summary>
    /// Branch comparison result.
    /// </summary>
    public struct BranchComparison : IComponentData
    {
        public FixedString64Bytes BranchAId;
        public FixedString64Bytes BranchBId;
        
        public float ResourceDifference;
        public float PopulationDifference;
        public float MilitaryDifference;
        public float HappinessDifference;
        
        public int EntitiesOnlyInA;
        public int EntitiesOnlyInB;
        public int EntitiesInBoth;
        public int EntitiesDiverged;
        
        public uint ComparedAtTick;
    }

    /// <summary>
    /// Tracks which branch an entity belongs to.
    /// </summary>
    public struct BranchMembership : IComponentData
    {
        public FixedString64Bytes BranchId;
        public uint CreatedInBranchTick;
        public bool ExistsInParent;                // Did this entity exist before branch
    }

    /// <summary>
    /// Request to compare two branches.
    /// </summary>
    public struct ComparisonRequest : IComponentData
    {
        public FixedString64Bytes BranchAId;
        public FixedString64Bytes BranchBId;
        public bool DetailedComparison;            // Include entity-level deltas
        public uint RequestTick;
    }
}

