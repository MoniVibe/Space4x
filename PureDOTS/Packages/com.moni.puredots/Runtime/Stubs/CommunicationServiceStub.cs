// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Communication
{
    public static class CommunicationServiceStub
    {
        public static void RegisterChannel(EntityManager manager, Entity entity, int channelId, float latencySeconds)
        {
            if (!manager.HasComponent<CommChannel>(entity))
            {
                manager.AddComponentData(entity, new CommChannel
                {
                    ChannelId = channelId,
                    Reliability = 1f,
                    LatencySeconds = latencySeconds
                });
            }
        }

        public static void ReportDisruption(EntityManager manager, Entity entity, float severity, float recoveryRate)
        {
            severity = math.saturate(severity);
            recoveryRate = math.max(0.01f, recoveryRate);
            if (!manager.HasComponent<CommDisruption>(entity))
            {
                manager.AddComponentData(entity, new CommDisruption
                {
                    Severity = severity,
                    RecoveryRate = recoveryRate
                });
            }
            else
            {
                manager.SetComponentData(entity, new CommDisruption
                {
                    Severity = severity,
                    RecoveryRate = recoveryRate
                });
            }
        }
    }
}
