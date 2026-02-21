using PureDOTS.Runtime;
using PureDOTS.Runtime.Genetics;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Mirrors genetic and culture axes into TraitAxisValue buffers for filtering/querying.
    /// Data-only bridge; uses IDs from Space4XGeneticsCatalog defaults.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XGeneticsSeedSystem))]
    public partial struct Space4XGeneticsTraitAxisBridgeSystem : ISystem
    {
        private static readonly FixedString32Bytes AxisViolenceDiplomacy = new FixedString32Bytes("gene.violence_diplomacy");
        private static readonly FixedString32Bytes AxisMightMagic = new FixedString32Bytes("gene.might_magic");
        private static readonly FixedString32Bytes AxisSpiritualMaterial = new FixedString32Bytes("culture.spiritual_material");
        private static readonly FixedString32Bytes AxisCorruptPure = new FixedString32Bytes("culture.corrupt_pure");
        private static readonly FixedString32Bytes AxisLawfulChaotic = new FixedString32Bytes("culture.lawful_chaotic");
        private static readonly FixedString32Bytes AxisXenophileXenophobe = new FixedString32Bytes("culture.xenophile_xenophobe");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (genetic, entity) in SystemAPI.Query<RefRO<GeneticInclination>>()
                         .WithChangeFilter<GeneticInclination>()
                         .WithEntityAccess())
            {
                if (em.HasBuffer<TraitAxisValue>(entity))
                {
                    var buffer = em.GetBuffer<TraitAxisValue>(entity);
                    TraitAxisLookup.SetValue(AxisViolenceDiplomacy, genetic.ValueRO.ViolenceDiplomacyAxis, ref buffer);
                    TraitAxisLookup.SetValue(AxisMightMagic, genetic.ValueRO.MightMagicAxis, ref buffer);
                }
                else
                {
                    var buffer = ecb.AddBuffer<TraitAxisValue>(entity);
                    buffer.Add(new TraitAxisValue { AxisId = AxisViolenceDiplomacy, Value = genetic.ValueRO.ViolenceDiplomacyAxis });
                    buffer.Add(new TraitAxisValue { AxisId = AxisMightMagic, Value = genetic.ValueRO.MightMagicAxis });
                }
            }

            foreach (var (culture, entity) in SystemAPI.Query<RefRO<CultureProfile>>()
                         .WithChangeFilter<CultureProfile>()
                         .WithEntityAccess())
            {
                if (em.HasBuffer<TraitAxisValue>(entity))
                {
                    var buffer = em.GetBuffer<TraitAxisValue>(entity);
                    TraitAxisLookup.SetValue(AxisSpiritualMaterial, culture.ValueRO.SpiritualMaterialAxis, ref buffer);
                    TraitAxisLookup.SetValue(AxisCorruptPure, culture.ValueRO.CorruptPureAxis, ref buffer);
                    TraitAxisLookup.SetValue(AxisLawfulChaotic, culture.ValueRO.LawfulChaoticAxis, ref buffer);
                    TraitAxisLookup.SetValue(AxisXenophileXenophobe, culture.ValueRO.XenophileXenophobeAxis, ref buffer);
                }
                else
                {
                    var buffer = ecb.AddBuffer<TraitAxisValue>(entity);
                    buffer.Add(new TraitAxisValue { AxisId = AxisSpiritualMaterial, Value = culture.ValueRO.SpiritualMaterialAxis });
                    buffer.Add(new TraitAxisValue { AxisId = AxisCorruptPure, Value = culture.ValueRO.CorruptPureAxis });
                    buffer.Add(new TraitAxisValue { AxisId = AxisLawfulChaotic, Value = culture.ValueRO.LawfulChaoticAxis });
                    buffer.Add(new TraitAxisValue { AxisId = AxisXenophileXenophobe, Value = culture.ValueRO.XenophileXenophobeAxis });
                }
            }

            ecb.Playback(em);
        }
    }
}
