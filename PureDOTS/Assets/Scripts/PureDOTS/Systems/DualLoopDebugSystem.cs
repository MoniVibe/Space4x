using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial struct DualLoopDebugSystem : ISystem
    {
        private double _startTime;
        private bool _logged;

        public void OnCreate(ref SystemState state)
        {
            _startTime = double.NaN;
            _logged = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_logged)
            {
                state.Enabled = false;
                return;
            }

            if (double.IsNaN(_startTime))
            {
                _startTime = SystemAPI.Time.ElapsedTime;
                return;
            }

            if (SystemAPI.Time.ElapsedTime - _startTime < 1.0)
            {
                return;
            }

            var resourceQuery = SystemAPI.QueryBuilder().WithAll<ResourceSourceConfig>().Build();
            var villagerQuery = SystemAPI.QueryBuilder().WithAll<VillagerNeeds>().Build();
            var storehouseQuery = SystemAPI.QueryBuilder().WithAll<StorehouseConfig>().Build();

            int resourceCount = resourceQuery.IsEmpty ? 0 : resourceQuery.CalculateEntityCount();
            int villagerCount = villagerQuery.IsEmpty ? 0 : villagerQuery.CalculateEntityCount();
            int storehouseCount = storehouseQuery.IsEmpty ? 0 : storehouseQuery.CalculateEntityCount();

            Debug.Log($"[DualLoopDebugSystem] Resources={resourceCount}, Storehouses={storehouseCount}, Villagers={villagerCount}");

            _logged = true;
            state.Enabled = false;
        }
    }
}

