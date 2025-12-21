using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Social;
using Unity.Entities;
using UnityDebug = UnityEngine.Debug;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Headless-only proof that custody state is being created (mutiny outcomes, spy detention, etc.).
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XSpyDetentionSystem))]
    [UpdateAfter(typeof(Space4XMutinyCustodySystem))]
    public partial struct Space4XCustodyProofSystem : ISystem
    {
        private byte _printed;
        private EntityQuery _custodyQuery;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            _custodyQuery = state.GetEntityQuery(ComponentType.ReadOnly<CustodyState>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_printed != 0)
            {
                state.Enabled = false;
                return;
            }

            if (_custodyQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var count = _custodyQuery.CalculateEntityCount();

            foreach (var (custody, entity) in SystemAPI.Query<RefRO<CustodyState>>().WithEntityAccess())
            {
                UnityDebug.Log(
                    $"[Space4XCustodyProof] PASS tick={tick} count={count} entity={entity.Index}:{entity.Version} kind={custody.ValueRO.Kind} status={custody.ValueRO.Status} captor={custody.ValueRO.CaptorScope.Index}:{custody.ValueRO.CaptorScope.Version}");
                _printed = 1;
                state.Enabled = false;
                return;
            }
        }
    }
}

