using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Transformation
{
    /// <summary>
    /// Transformation potential for an entity.
    /// </summary>
    public struct TransformationPotential : IComponentData
    {
        public FixedString32Bytes EligibleType; // "FallenStarDemon", "AscendedAngel"
        public float TransformChance;           // 0-1 probability
        public byte RequiredPhysique;           // Stat requirements
        public byte RequiredWill;
        public byte RequiredAlignment;          // Chaotic, Lawful, etc.
        public byte MeetsRequirements;
    }

    /// <summary>
    /// Trigger for transformation.
    /// </summary>
    public struct TransformationTrigger : IComponentData
    {
        public FixedString32Bytes TriggerType;  // "divine_throw", "corruption", "devotion"
        public Entity TriggeringEntity;         // God hand, corruption source
        public float TriggerMagnitude;          // Distance thrown, corruption level
        public uint TriggerTick;
        public byte RollSucceeded;
    }

    /// <summary>
    /// Transformation in progress.
    /// </summary>
    public struct TransformationInProgress : IComponentData
    {
        public FixedString32Bytes TargetForm;   // What they're becoming
        public float Progress;                   // 0-1
        public uint StartTick;
        public uint CompletionTick;              // When transformation finishes
        public byte IsDelayed;                   // Some transform after delay
    }

    /// <summary>
    /// Retained identity after transformation.
    /// </summary>
    public struct RetainedIdentity : IComponentData
    {
        public FixedString64Bytes OriginalName;
        public uint OriginalEntityId;
        public Entity OriginalVillage;
        public Entity OriginalFamily;
        public byte OriginalAlignment;
        public float RelationToTransformer;     // Positive = grateful, Negative = vengeful
    }

    /// <summary>
    /// Transformed entity marker.
    /// </summary>
    public struct TransformedEntity : IComponentData
    {
        public FixedString32Bytes OriginalType; // "Villager", "Crew"
        public FixedString32Bytes CurrentType;  // "Demon", "Angel"
        public uint TransformationTick;
        public Entity TransformationCause;      // Who/what caused this
        public byte RetainsMemories;
        public byte RetainsRelationships;
    }

    /// <summary>
    /// Retained memory from before transformation.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RetainedMemory : IBufferElementData
    {
        public FixedString64Bytes MemoryType;   // "family_bond", "grudge", "loyalty"
        public Entity RelatedEntity;
        public float Intensity;                  // How strong the memory
        public byte IsPositive;
    }

    /// <summary>
    /// Transformation configuration.
    /// </summary>
    public struct TransformationConfig : IComponentData
    {
        public float BaseTransformChance;
        public float PhysiqueWeighting;          // How much physique affects chance
        public float WillWeighting;
        public uint MinDelayTicks;               // For delayed transformations
        public uint MaxDelayTicks;
        public byte AllowIdentityRetention;
        public float MemoryDecayRate;            // 0 = permanent memories
    }

    /// <summary>
    /// Transformation type definition.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TransformationTypeRequirement : IBufferElementData
    {
        public FixedString32Bytes TransformType;
        public byte MinPhysique;
        public byte MinWill;
        public byte RequiredAlignment;
        public FixedString32Bytes RequiredTrigger;
    }
}

