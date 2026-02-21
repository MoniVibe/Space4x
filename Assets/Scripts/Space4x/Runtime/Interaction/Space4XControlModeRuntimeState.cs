using Space4X.UI;
using Unity.Entities;

namespace Space4X.Runtime.Interaction
{
    /// <summary>
    /// ECS-readable snapshot of presentation control mode state.
    /// Lets Burst hand/order systems react to mode changes without managed dependencies.
    /// </summary>
    public struct Space4XControlModeRuntimeState : IComponentData
    {
        public byte Mode;
        public byte UsesRtsRig;
        public byte IsRtsOrdersEnabled;
        public byte IsDivineHandEnabled;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XControlModeRuntimeSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XControlModeRuntimeState>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XControlModeRuntimeState));
            state.EntityManager.SetComponentData(entity, BuildState(Space4XControlModeState.CurrentMode));
        }

        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.SetSingleton(BuildState(Space4XControlModeState.CurrentMode));
        }

        private static Space4XControlModeRuntimeState BuildState(Space4XControlMode mode)
        {
            bool usesRtsRig = mode == Space4XControlMode.Rts || mode == Space4XControlMode.DivineHand;
            return new Space4XControlModeRuntimeState
            {
                Mode = (byte)mode,
                UsesRtsRig = (byte)(usesRtsRig ? 1 : 0),
                IsRtsOrdersEnabled = (byte)(mode == Space4XControlMode.Rts ? 1 : 0),
                IsDivineHandEnabled = (byte)(mode == Space4XControlMode.DivineHand ? 1 : 0)
            };
        }
    }
}
