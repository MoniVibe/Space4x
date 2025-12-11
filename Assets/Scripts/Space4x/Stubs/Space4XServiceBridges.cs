// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Diplomacy;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Narrative;
using PureDOTS.Runtime.Persistence;
using PureDOTS.Runtime.Sensors;
using PureDOTS.Runtime.TimeControl;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Stubs
{
    public static class Space4XNavigationBridgeStub
    {
        public static Entity RequestCarrierPath(ref SystemState state, in Entity carrier, in float3 start, in float3 destination, byte flags = 0)
        {
            return NavigationServiceStub.RequestPath(ref state, in carrier, in start, in destination, flags);
        }
    }

    public static class Space4XCombatBridgeStub
    {
        public static void ScheduleCarrierEngagement(in Entity attacker, in Entity defender)
        {
            CombatServiceStub.ScheduleEngagement(in attacker, in defender);
        }
    }

    public static class Space4XEconomyBridgeStub
    {
        public static void SubmitStationJob(in Entity facility, int recipeId)
        {
            EconomyServiceStub.EnqueueProduction(in facility, recipeId);
        }
    }

    public static class Space4XDiplomacyBridgeStub
    {
        public static void ApplyFactionDelta(in Entity a, in Entity b, float delta)
        {
            DiplomacyServiceStub.ApplyRelationDelta(in a, in b, delta);
        }
    }

    public static class Space4XTelemetryBridgeStub
    {
        public static void LogCarrierMetric(FixedString64Bytes name, float value)
        {
            TelemetryBridgeStub.RecordMetric(name, value);
        }
    }

    public static class Space4XSensorBridgeStub
    {
        public static void RegisterRig(in Entity entity, byte channelsMask)
        {
            SensorServiceStub.RegisterRig(in entity, channelsMask);
        }

        public static void SubmitInterrupt(in Entity entity, byte category)
        {
            SensorServiceStub.SubmitInterrupt(in entity, category);
        }
    }

    public static class Space4XTimeBridgeStub
    {
        public static void RequestPause() => TimeControlServiceStub.RequestPause();
        public static void RequestResume() => TimeControlServiceStub.RequestResume();
        public static void RequestScrub(uint tick) => TimeControlServiceStub.RequestScrub(tick);
    }

    public static class Space4XNarrativeBridgeStub
    {
        public static void RaiseEvent(int situationId, int eventId)
        {
            NarrativeServiceStub.RaiseEvent(situationId, eventId);
        }
    }

    public static class Space4XSaveLoadBridgeStub
    {
        public static SnapshotHandle RequestSave(ref SystemState state)
        {
            return SaveLoadServiceStub.RequestSave(ref state);
        }

        public static void RequestLoad(SnapshotHandle handle)
        {
            SaveLoadServiceStub.RequestLoad(handle);
        }
    }
}
