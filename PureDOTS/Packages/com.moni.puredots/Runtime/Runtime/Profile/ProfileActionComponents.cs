using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Profile
{
    [Flags]
    public enum ProfileActionIntentFlags : byte
    {
        None = 0,
        Malice = 1 << 0,
        Benevolence = 1 << 1,
        Negligence = 1 << 2,
        Coerced = 1 << 3
    }

    [Flags]
    public enum ProfileActionJustificationFlags : byte
    {
        None = 0,
        SelfDefense = 1 << 0,
        Retaliation = 1 << 1,
        Sanctioned = 1 << 2,
        Necessity = 1 << 3
    }

    [Flags]
    public enum ProfileActionOutcomeFlags : byte
    {
        None = 0,
        Harm = 1 << 0,
        Help = 1 << 1,
        Collateral = 1 << 2,
        Disrupted = 1 << 3
    }

    /// <summary>
    /// Canonical profile action tokens for mutation tracking.
    /// "Civilian" is shorthand for non-combatant/innocent targets.
    /// </summary>
    public enum ProfileActionToken : byte
    {
        None = 0,
        ObeyOrder = 1,
        DisobeyOrder = 2,
        AttackCivilian = 3,
        AttackHostile = 4,
        Rescue = 5,
        MineResource = 6,
        OrderIssued = 7
    }

    [InternalBufferCapacity(128)]
    public struct ProfileActionEvent : IBufferElementData
    {
        public ProfileActionToken Token;
        public ProfileActionIntentFlags IntentFlags;
        public ProfileActionJustificationFlags JustificationFlags;
        public ProfileActionOutcomeFlags OutcomeFlags;
        public byte Magnitude;
        public Entity Actor;
        public Entity Target;
        public Entity IssuingSeat;
        public Entity IssuingOccupant;
        public Entity ActingSeat;
        public Entity ActingOccupant;
        public uint Tick;
    }

    public struct ProfileActionEventStream : IComponentData
    {
        public uint Version;
        public int EventCount;
        public int DroppedEvents;
        public uint LastWriteTick;
    }

    public struct ProfileActionEventStreamConfig : IComponentData
    {
        public int MaxEvents;

        public static ProfileActionEventStreamConfig CreateDefault(int maxEvents = 256)
        {
            return new ProfileActionEventStreamConfig
            {
                MaxEvents = maxEvents <= 0 ? 256 : maxEvents
            };
        }
    }

    public struct ProfileActionCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ProfileActionCatalogBlob> Catalog;
    }

    public struct ProfileActionDefinition
    {
        /// <summary>Alignment delta ordered as Moral, Order, Purity.</summary>
        public float3 AlignmentDelta;
        /// <summary>Outlook delta ordered as Loyalist, Opportunist, Fanatic, Mutinous.</summary>
        public float4 OutlookDelta;
        /// <summary>Disposition delta ordered as Compliance, Caution, FormationAdherence.</summary>
        public float3 DispositionDeltaA;
        /// <summary>Disposition delta ordered as RiskTolerance, Aggression, Patience.</summary>
        public float3 DispositionDeltaB;
        public ProfileActionToken Token;
        public float Weight;
    }

    public struct ProfileActionCatalogBlob
    {
        public BlobArray<ProfileActionDefinition> Actions;
    }

    public struct ProfileMutationConfig : IComponentData
    {
        public float AlignmentScale;
        public float OutlookScale;
        public float DispositionScale;
        public float AlignmentMaxDelta;
        public float OutlookMaxDelta;
        public float DispositionMaxDelta;
        public float AccumulatorDecay;
        public uint ApplyIntervalTicks;
        public float CoercedMultiplier;
        public float JustifiedMultiplier;
        public float MaliceMultiplier;
        public float BenevolenceMultiplier;

        public static ProfileMutationConfig CreateDefault()
        {
            return new ProfileMutationConfig
            {
                AlignmentScale = 0.08f,
                OutlookScale = 0.1f,
                DispositionScale = 0.07f,
                AlignmentMaxDelta = 0.12f,
                OutlookMaxDelta = 0.15f,
                DispositionMaxDelta = 0.1f,
                AccumulatorDecay = 0.85f,
                ApplyIntervalTicks = 30u,
                CoercedMultiplier = 0.35f,
                JustifiedMultiplier = 0.5f,
                MaliceMultiplier = 1.1f,
                BenevolenceMultiplier = 0.9f
            };
        }
    }

    public struct ProfileActionAccumulator : IComponentData
    {
        public float3 Alignment;
        public float4 Outlook;
        public float3 DispositionA;
        public float3 DispositionB;
        public float PendingMagnitude;
        public uint LastAppliedTick;
    }

    public struct ProfileMutationPending : IComponentData
    {
    }
}
