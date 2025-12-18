using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4x.Narrative
{
    /// <summary>
    /// Bridge system that reads PureDOTS narrative signals and maps them to Space4x-specific UI/events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4xNarrativeBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.EnableSpace4x ||
                !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            // Get or create event feed singleton
            Entity eventFeedEntity;
            if (!SystemAPI.TryGetSingletonEntity<EventFeedBuffer>(out eventFeedEntity))
            {
                eventFeedEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddBuffer<EventFeedBuffer>(eventFeedEntity);
            }

            var eventFeed = state.EntityManager.GetBuffer<EventFeedBuffer>(eventFeedEntity);

            // Read narrative signals
            if (!SystemAPI.TryGetSingletonEntity<NarrativeSignalBufferElement>(out var signalEntity))
            {
                return;
            }

            var signalBuffer = state.EntityManager.GetBuffer<NarrativeSignalBufferElement>(signalEntity);

            // Process signals and append to event feed
            for (int i = signalBuffer.Length - 1; i >= 0; i--)
            {
                var signal = signalBuffer[i];

                // Append to event feed
                eventFeed.Add(new EventFeedBuffer
                {
                    SignalType = signal.SignalType,
                    Id = signal.Id.Value,
                    TargetIndex = signal.Target.Index,
                    PayloadA = signal.PayloadA,
                    PayloadB = signal.PayloadB
                });

                // Remove processed signal
                signalBuffer.RemoveAt(i);
            }

            // Read reward signals
            if (!SystemAPI.TryGetSingletonEntity<NarrativeRewardSignal>(out var rewardEntity))
            {
                return;
            }

            var rewardBuffer = state.EntityManager.GetBuffer<NarrativeRewardSignal>(rewardEntity);

            for (int i = rewardBuffer.Length - 1; i >= 0; i--)
            {
                var reward = rewardBuffer[i];
                
                // Append reward to event feed
                eventFeed.Add(new EventFeedBuffer
                {
                    SignalType = 3, // RewardGranted
                    Id = reward.SourceId.Value,
                    TargetIndex = reward.Target.Index,
                    PayloadA = reward.RewardType,
                    PayloadB = reward.Amount
                });
                
                // Remove processed reward
                rewardBuffer.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Simple buffer for storing narrative events in Space4x event feed.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct EventFeedBuffer : IBufferElementData
    {
        public int SignalType;
        public int Id;
        public int TargetIndex;
        public int PayloadA;
        public int PayloadB;
    }
}
