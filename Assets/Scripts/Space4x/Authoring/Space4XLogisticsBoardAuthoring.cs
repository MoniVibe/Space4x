using PureDOTS.Runtime.Transport;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for a Space4X logistics board entity (demand ledger + reservations).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XLogisticsBoardAuthoring : MonoBehaviour
    {
        [SerializeField] private string boardId = "logistics.board.station";
        [SerializeField] private float minBatchUnits = 5f;
        [SerializeField] private float maxBatchUnits = 50f;
        [SerializeField] private uint reservationExpiryTicks = 120;
        [SerializeField] private uint broadcastIntervalTicks = 60;
        [SerializeField] private byte maxClaimsPerTick = 4;

        private sealed class Baker : Unity.Entities.Baker<Space4XLogisticsBoardAuthoring>
        {
            public override void Bake(Space4XLogisticsBoardAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new LogisticsBoard
                {
                    BoardId = new Unity.Collections.FixedString64Bytes(authoring.boardId),
                    AuthorityEntity = Entity.Null,
                    DomainEntity = Entity.Null,
                    LastUpdateTick = 0
                });

                var minBatch = math.max(0f, authoring.minBatchUnits);
                var maxBatch = math.max(minBatch, authoring.maxBatchUnits);

                AddComponent(entity, new LogisticsBoardConfig
                {
                    MinBatchUnits = minBatch,
                    MaxBatchUnits = maxBatch,
                    ReservationExpiryTicks = authoring.reservationExpiryTicks,
                    BroadcastIntervalTicks = authoring.broadcastIntervalTicks,
                    MaxClaimsPerTick = authoring.maxClaimsPerTick
                });

                AddBuffer<LogisticsDemandEntry>(entity);
                AddBuffer<LogisticsReservationEntry>(entity);
                AddBuffer<LogisticsClaimRequest>(entity);
            }
        }
    }
}
