using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Behavioral personality traits for villagers.
    /// These axes determine HOW villagers respond emotionally, independent of alignment (moral values).
    /// Based on Villager_Behavioral_Personality.md design.
    /// </summary>
    public struct VillagerBehavior : IComponentData
    {
        // Behavioral axes (-100 to +100)
        // Vengeful: -100 (forgiving) to +100 (vengeful)
        // Bold: -100 (craven) to +100 (bold)
        public sbyte VengefulScore;        // Forgiving(-) to Vengeful(+)
        public sbyte BoldScore;            // Craven(-) to Bold(+)
        
        // Computed modifiers (sync system calculates)
        public float InitiativeModifier;   // Applied to base initiative (from village band)
        
        // State tracking
        public byte ActiveGrudgeCount;     // Unresolved grudges
        public uint LastMajorActionTick;   // Tick of last autonomous decision
        
        // Helper methods
        public bool IsVengeful => VengefulScore < -20;  // More negative = more vengeful
        public bool IsForgiving => VengefulScore > 40;
        public bool IsBold => BoldScore > 40;
        public bool IsCraven => BoldScore < -40;
    }
    
    /// <summary>
    /// Grudge tracking buffer - records wrongs committed against the villager.
    /// Grudges decay over time and boost initiative when active.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct VillagerGrudge : IBufferElementData
    {
        public Entity Target;                      // Who wronged them
        public FixedString64Bytes OffenseType;     // "killed_friend", "stole_property", "insulted"
        public float IntensityScore;               // Current intensity (decays over time)
        public uint OccurredTick;                  // When it happened
        public byte RetaliationAttempts;           // Revenge attempt count
        
        // Grudge decay rate depends on VengefulScore:
        // Vengeful (-70): DecayRate = 0.01 × (100 + VengefulScore) = 0.3 per day
        // Forgiving (+60): DecayRate = 2.0 per day (rapid fade)
    }
    
    /// <summary>
    /// Initiative state tracking for autonomous action timing.
    /// Initiative determines WHEN villagers act autonomously (life-changing decisions).
    /// </summary>
    public struct VillagerInitiativeState : IComponentData
    {
        public float CurrentInitiative;            // Computed value (0.0-1.0)
        public uint NextActionTick;                // When next evaluation occurs
        public FixedString32Bytes PendingAction;   // "seek_courtship", "plot_revenge", "open_business"
        
        // Initiative frequency formula (from docs):
        // High Initiative (0.8+): Rapid life decisions, acts frequently
        // Medium Initiative (0.4-0.6): Measured decisions, waits for right moment
        // Low Initiative (0.0-0.3): Slow to act, rarely initiates change
    }
    
    /// <summary>
    /// Villager alignment component - moral/ideological position.
    /// Uses tri-axis alignment: Good/Evil, Order/Chaos, Purity/Corruption.
    /// Based on Generalized_Alignment_Framework.md.
    /// </summary>
    public struct VillagerAlignment : IComponentData
    {
        // Tri-axis alignment (-100 to +100)
        public sbyte MoralAxis;     // -100 (evil) to +100 (good)
        public sbyte OrderAxis;    // -100 (chaotic) to +100 (lawful)
        public sbyte PurityAxis;   // -100 (corrupt) to +100 (pure)
        
        // Alignment strength (how strongly held, 0-1)
        public float AlignmentStrength;
        
        // Last shift tracking
        public uint LastShiftTick;
        
        // Helper methods for common queries
        public bool IsGood => MoralAxis > 20;
        public bool IsEvil => MoralAxis < -20;
        public bool IsLawful => OrderAxis > 20;
        public bool IsChaotic => OrderAxis < -20;
        public bool IsPure => PurityAxis > 20;
        public bool IsCorrupt => PurityAxis < -20;

        // Normalized helpers (-1..+1) for systems that previously consumed float axes.
        public float MoralNormalized => math.clamp(MoralAxis * 0.01f, -1f, 1f);
        public float OrderNormalized => math.clamp(OrderAxis * 0.01f, -1f, 1f);
        public float PurityNormalized => math.clamp(PurityAxis * 0.01f, -1f, 1f);
        public float MaterialismNormalized => -MoralNormalized;
        public float IntegrityNormalized => PurityNormalized;

        public static sbyte ToAxisValue(float normalized)
        {
            return (sbyte)math.clamp(math.round(normalized * 100f), -100f, 100f);
        }
    }
}
