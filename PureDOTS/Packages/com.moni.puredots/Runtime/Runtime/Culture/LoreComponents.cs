using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Culture
{
    /// <summary>
    /// Buffer of stories an entity knows.
    /// </summary>
    public struct KnownStory : IBufferElementData
    {
        /// <summary>
        /// Story identifier from catalog.
        /// </summary>
        public FixedString64Bytes StoryId;

        /// <summary>
        /// How well the story is remembered (0-1).
        /// Decays over time without reinforcement.
        /// </summary>
        public float Retention;

        /// <summary>
        /// Accuracy of the remembered version (0-1).
        /// Stories can drift/mutate during transmission.
        /// </summary>
        public float Accuracy;

        /// <summary>
        /// Tick when story was learned.
        /// </summary>
        public uint LearnedTick;

        /// <summary>
        /// Entity that taught this story.
        /// </summary>
        public Entity LearnedFromEntity;

        /// <summary>
        /// Number of times this entity has shared the story.
        /// </summary>
        public uint TimesShared;
    }

    /// <summary>
    /// State for an entity actively telling a story.
    /// </summary>
    public struct StoryTellerState : IComponentData
    {
        /// <summary>
        /// Story being told.
        /// </summary>
        public FixedString64Bytes CurrentStoryId;

        /// <summary>
        /// Entity listening (single listener mode).
        /// </summary>
        public Entity ListenerEntity;

        /// <summary>
        /// Progress through the telling (0-1).
        /// </summary>
        public float TellingProgress;

        /// <summary>
        /// How engaged the listener is (0-1).
        /// Affects retention and accuracy.
        /// </summary>
        public float ListenerEngagement;

        /// <summary>
        /// Tick when telling started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Storytelling mode.
        /// </summary>
        public StoryTellingMode Mode;
    }

    /// <summary>
    /// Mode of storytelling.
    /// </summary>
    public enum StoryTellingMode : byte
    {
        /// <summary>
        /// One-on-one conversation.
        /// </summary>
        Private = 0,

        /// <summary>
        /// Public performance to group.
        /// </summary>
        Public = 1,

        /// <summary>
        /// Formal education setting.
        /// </summary>
        Teaching = 2,

        /// <summary>
        /// Ritual/ceremonial telling.
        /// </summary>
        Ritual = 3,

        /// <summary>
        /// Written transmission (books, scrolls).
        /// </summary>
        Written = 4,

        /// <summary>
        /// Broadcast (Space4X communication networks).
        /// </summary>
        Broadcast = 5
    }

    /// <summary>
    /// Cultural memory and identity tracking.
    /// </summary>
    public struct CulturalMemory : IComponentData
    {
        /// <summary>
        /// Primary cultural identity.
        /// </summary>
        public FixedString64Bytes PrimaryCultureId;

        /// <summary>
        /// Secondary cultural identity (if applicable).
        /// </summary>
        public FixedString64Bytes SecondaryCultureId;

        /// <summary>
        /// Strength of cultural attachment (0-1).
        /// </summary>
        public float CulturalIdentity;

        /// <summary>
        /// Resistance to cultural change (0-1).
        /// </summary>
        public float CulturalResilience;

        /// <summary>
        /// Total stories known from primary culture.
        /// </summary>
        public uint PrimaryCultureStoriesKnown;

        /// <summary>
        /// Total stories shared to others.
        /// </summary>
        public uint TotalStoriesShared;

        /// <summary>
        /// Cultural drift from origin (0-1).
        /// High drift = local variant of culture.
        /// </summary>
        public float CulturalDrift;
    }

    /// <summary>
    /// Request to share a story with another entity.
    /// </summary>
    public struct ShareStoryRequest : IComponentData
    {
        public FixedString64Bytes StoryId;
        public Entity TargetEntity;
        public StoryTellingMode Mode;
        public uint RequestTick;
    }

    /// <summary>
    /// Event raised when story transmission completes.
    /// </summary>
    public struct StorySharedEvent : IBufferElementData
    {
        public FixedString64Bytes StoryId;
        public Entity TellerEntity;
        public Entity ListenerEntity;
        public float FinalRetention;
        public float FinalAccuracy;
        public uint CompletedTick;
        public StoryShareResult Result;
    }

    /// <summary>
    /// Result of story sharing attempt.
    /// </summary>
    public enum StoryShareResult : byte
    {
        Success = 0,
        PartialSuccess = 1,  // Low retention/accuracy
        Interrupted = 2,
        Rejected = 3,        // Listener refused
        AlreadyKnown = 4,
        MissingPrerequisite = 5
    }

    /// <summary>
    /// Marker for entities that can tell stories effectively.
    /// </summary>
    public struct StoryTeller : IComponentData
    {
        /// <summary>
        /// Storytelling skill (affects transmission quality).
        /// </summary>
        public float Skill;

        /// <summary>
        /// Charisma/engagement modifier.
        /// </summary>
        public float Charisma;

        /// <summary>
        /// Number of listeners that can be engaged simultaneously.
        /// </summary>
        public byte MaxAudienceSize;

        /// <summary>
        /// Preferred storytelling mode.
        /// </summary>
        public StoryTellingMode PreferredMode;
    }
}

