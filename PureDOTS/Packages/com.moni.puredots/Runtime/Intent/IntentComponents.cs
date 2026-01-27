using PureDOTS.Runtime.Interrupts;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Intent
{
    using EntityIntentComponent = PureDOTS.Runtime.Interrupts.EntityIntent;
    using IntentModeComponent = PureDOTS.Runtime.Interrupts.IntentMode;
    using InterruptBufferElement = PureDOTS.Runtime.Interrupts.Interrupt;
    using InterruptPriorityComponent = PureDOTS.Runtime.Interrupts.InterruptPriority;
    using InterruptTypeComponent = PureDOTS.Runtime.Interrupts.InterruptType;

    /// <summary>
    /// Helper utilities that expose the canonical intent/interrupt component types through the
    /// <c>PureDOTS.Runtime.Intent</c> namespace, so gameplay code can simply
    /// <c>using PureDOTS.Runtime.Intent;</c> when working with intent data.
    /// </summary>
    public static class IntentComponentAliases
    {
        public static ComponentType EntityIntentReadOnly => ComponentType.ReadOnly<EntityIntentComponent>();
        public static ComponentType EntityIntentReadWrite => ComponentType.ReadWrite<EntityIntentComponent>();
        public static ComponentType InterruptBufferReadOnly => ComponentType.ReadOnly<InterruptBufferElement>();
        public static ComponentType InterruptBufferReadWrite => ComponentType.ReadWrite<InterruptBufferElement>();

        public static System.Type EntityIntentType => typeof(EntityIntentComponent);
        public static System.Type IntentModeType => typeof(IntentModeComponent);
        public static System.Type InterruptBufferType => typeof(InterruptBufferElement);
        public static System.Type InterruptTypeType => typeof(InterruptTypeComponent);
        public static System.Type InterruptPriorityType => typeof(InterruptPriorityComponent);
    }

    /// <summary>
    /// Lightweight queue element for deferred intents.
    /// Systems/gameplay code can enqueue future intents and let <see cref="IntentProcessingSystem"/>
    /// promote them when the active intent completes or when a higher-priority queued intent arrives.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct QueuedIntent : IBufferElementData
    {
        public IntentModeComponent Mode;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public InterruptPriorityComponent Priority;
        public InterruptTypeComponent TriggeringInterrupt;
        public uint RequestedTick;
    }
}

