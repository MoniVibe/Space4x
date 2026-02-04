using PureDOTS.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Keeps ship tech/warp precision aligned with its origin colony.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XShipTechSyncSystem : ISystem
    {
        private ComponentLookup<TechLevel> _techLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            _techLookup = state.GetComponentLookup<TechLevel>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            _techLookup.Update(ref state);

            foreach (var (link, tech, warp) in SystemAPI
                         .Query<RefRO<ColonyTechLink>, RefRW<TechLevel>, RefRW<WarpPrecision>>())
            {
                var colony = link.ValueRO.Colony;
                if (colony == Entity.Null || !_techLookup.HasComponent(colony))
                {
                    continue;
                }

                var colonyTech = _techLookup[colony];
                if (!TechEquals(tech.ValueRO, colonyTech))
                {
                    tech.ValueRW = colonyTech;
                }

                var targetTier = ResolveWarpTier(colonyTech);
                if (warp.ValueRO.TechTier != targetTier)
                {
                    warp.ValueRW = WarpPrecision.FromTier(targetTier);
                }
            }
        }

        private static bool TechEquals(in TechLevel a, in TechLevel b)
        {
            return a.MiningTech == b.MiningTech &&
                   a.CombatTech == b.CombatTech &&
                   a.HaulingTech == b.HaulingTech &&
                   a.ProcessingTech == b.ProcessingTech;
        }

        private static WarpTechTier ResolveWarpTier(in TechLevel tech)
        {
            var tier = math.max((int)tech.MiningTech,
                math.max((int)tech.CombatTech, math.max((int)tech.HaulingTech, (int)tech.ProcessingTech)));

            return tier switch
            {
                >= 4 => WarpTechTier.Experimental,
                3 => WarpTechTier.Advanced,
                2 => WarpTechTier.Standard,
                1 => WarpTechTier.Basic,
                _ => WarpTechTier.Primitive
            };
        }
    }
}
