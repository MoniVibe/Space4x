using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Scenario
{
    public enum Space4XScenarioActionKind : byte
    {
        MoveFleet = 0,
        TriggerIntercept = 1
    }

    public struct Space4XScenarioAction : IBufferElementData
    {
        public uint ExecuteTick;
        public Space4XScenarioActionKind Kind;
        public FixedString64Bytes FleetId;
        public FixedString64Bytes TargetFleetId;
        public float3 TargetPosition;
        public byte Executed;
    }

    /// <summary>
    /// Marker for scenario-created move targets so fleets can move without teleporting.
    /// </summary>
    public struct Space4XScenarioMoveTarget : IComponentData
    {
        public FixedString64Bytes FleetId;
        public uint CreatedTick;
    }
}
