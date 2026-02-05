using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Scenario
{
    public enum Space4XScenarioActionKind : byte
    {
        MoveFleet = 0,
        TriggerIntercept = 1,
        EconomyEnable = 2,
        ProdCreateBusiness = 3,
        ProdAddItem = 4,
        ProdRequest = 5
    }

    public struct Space4XScenarioAction : IBufferElementData
    {
        public uint ExecuteTick;
        public Space4XScenarioActionKind Kind;
        public FixedString64Bytes FleetId;
        public FixedString64Bytes TargetFleetId;
        public float3 TargetPosition;
        public FixedString64Bytes BusinessId;
        public FixedString64Bytes ItemId;
        public FixedString64Bytes RecipeId;
        public float Quantity;
        public float Capacity;
        public byte BusinessType;
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

    public struct Space4XScenarioBusinessId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public struct Space4XScenarioBusinessWorker : IComponentData
    {
        public Entity Worker;
    }
}
