using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum Space4XResourceBand : byte
    {
        Inner = 0,
        Mid = 1,
        Outer = 2,
        Logistics = 3,
        Nebula = 4,
        AncientCore = 5,
        BlackHole = 6,
        Neutron = 7,
        Hazard = 8,
        SuperResource = 9,
        Ruins = 10,
        Gate = 11
    }

    public struct Space4XResourceDistributionConfig : IComponentData
    {
        public float BiasChance;

        public static Space4XResourceDistributionConfig Default => new Space4XResourceDistributionConfig
        {
            BiasChance = 0.45f
        };
    }

    public struct Space4XResourceDistributionBaselineConfig : IComponentData
    {
        public float BiasChance;
    }

    [InternalBufferCapacity(20)]
    public struct Space4XResourceWeightEntry : IBufferElementData
    {
        public ResourceType Type;
        public float InnerWeight;
        public float MidWeight;
        public float OuterWeight;
        public float LogisticsWeight;
        public float NebulaWeight;
        public float AncientCoreWeight;
        public float BlackHoleWeight;
        public float NeutronWeight;
        public float HazardWeight;
        public float SuperResourceWeight;
        public float RuinsWeight;
        public float GateWeight;
    }

    [InternalBufferCapacity(20)]
    public struct Space4XResourceWeightBaselineEntry : IBufferElementData
    {
        public ResourceType Type;
        public float InnerWeight;
        public float MidWeight;
        public float OuterWeight;
        public float LogisticsWeight;
        public float NebulaWeight;
        public float AncientCoreWeight;
        public float BlackHoleWeight;
        public float NeutronWeight;
        public float HazardWeight;
        public float SuperResourceWeight;
        public float RuinsWeight;
        public float GateWeight;
    }

    public static class Space4XResourceDistributionUtility
    {
        public static ResourceType RollResource(
            Space4XResourceBand band,
            in DynamicBuffer<Space4XResourceWeightEntry> weights,
            ref Random rng)
        {
            var total = ResolveTotalWeight(band, weights);
            if (total <= 0f)
            {
                return ResourceType.Minerals;
            }

            var roll = rng.NextFloat(0f, total);
            for (int i = 0; i < weights.Length; i++)
            {
                var entry = weights[i];
                roll -= ResolveWeight(band, entry);
                if (roll <= 0f)
                {
                    return entry.Type;
                }
            }

            return weights.Length > 0 ? weights[weights.Length - 1].Type : ResourceType.Minerals;
        }

        public static ResourceType RollResource(
            Space4XResourceBand band,
            in DynamicBuffer<Space4XResourceWeightEntry> weights,
            uint hash)
        {
            var total = ResolveTotalWeight(band, weights);
            if (total <= 0f)
            {
                return ResourceType.Minerals;
            }

            var roll = (hash % 10000u) / 10000f * total;
            for (int i = 0; i < weights.Length; i++)
            {
                var entry = weights[i];
                roll -= ResolveWeight(band, entry);
                if (roll <= 0f)
                {
                    return entry.Type;
                }
            }

            return weights.Length > 0 ? weights[weights.Length - 1].Type : ResourceType.Minerals;
        }

        private static float ResolveTotalWeight(Space4XResourceBand band, in DynamicBuffer<Space4XResourceWeightEntry> weights)
        {
            var total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                total += ResolveWeight(band, weights[i]);
            }
            return total;
        }

        private static float ResolveWeight(Space4XResourceBand band, in Space4XResourceWeightEntry entry)
        {
            return band switch
            {
                Space4XResourceBand.Inner => entry.InnerWeight,
                Space4XResourceBand.Mid => entry.MidWeight,
                Space4XResourceBand.Outer => entry.OuterWeight,
                Space4XResourceBand.Logistics => entry.LogisticsWeight,
                Space4XResourceBand.Nebula => entry.NebulaWeight,
                Space4XResourceBand.AncientCore => entry.AncientCoreWeight,
                Space4XResourceBand.BlackHole => entry.BlackHoleWeight,
                Space4XResourceBand.Neutron => entry.NeutronWeight,
                Space4XResourceBand.Hazard => entry.HazardWeight,
                Space4XResourceBand.SuperResource => entry.SuperResourceWeight,
                Space4XResourceBand.Ruins => entry.RuinsWeight,
                Space4XResourceBand.Gate => entry.GateWeight,
                _ => 0f
            };
        }
    }

    public static class Space4XResourceDistributionDefaults
    {
        public static void PopulateDefaults(ref DynamicBuffer<Space4XResourceWeightEntry> buffer)
        {
            buffer.Clear();
            for (var i = 0; i < (int)ResourceType.Count; i++)
            {
                buffer.Add(new Space4XResourceWeightEntry
                {
                    Type = (ResourceType)i
                });
            }

            AddPoolWeights(ref buffer, InnerPool, Space4XResourceBand.Inner);
            AddPoolWeights(ref buffer, MidPool, Space4XResourceBand.Mid);
            AddPoolWeights(ref buffer, OuterPool, Space4XResourceBand.Outer);

            AddPoolWeights(ref buffer, NebulaBiasPool, Space4XResourceBand.Nebula);
            AddPoolWeights(ref buffer, AncientCoreBiasPool, Space4XResourceBand.AncientCore);
            AddPoolWeights(ref buffer, BlackHoleBiasPool, Space4XResourceBand.BlackHole);
            AddPoolWeights(ref buffer, NeutronBiasPool, Space4XResourceBand.Neutron);
            AddPoolWeights(ref buffer, HazardBiasPool, Space4XResourceBand.Hazard);
            AddPoolWeights(ref buffer, SuperResourceBiasPool, Space4XResourceBand.SuperResource);
            AddPoolWeights(ref buffer, RuinsBiasPool, Space4XResourceBand.Ruins);
            AddPoolWeights(ref buffer, GateBiasPool, Space4XResourceBand.Gate);

            SetWeight(ref buffer, ResourceType.Food, Space4XResourceBand.Logistics, 14f);
            SetWeight(ref buffer, ResourceType.Water, Space4XResourceBand.Logistics, 12f);
            SetWeight(ref buffer, ResourceType.Supplies, Space4XResourceBand.Logistics, 10f);
            SetWeight(ref buffer, ResourceType.Fuel, Space4XResourceBand.Logistics, 10f);
            SetWeight(ref buffer, ResourceType.Minerals, Space4XResourceBand.Logistics, 12f);
            SetWeight(ref buffer, ResourceType.OrganicMatter, Space4XResourceBand.Logistics, 10f);
            SetWeight(ref buffer, ResourceType.Volatiles, Space4XResourceBand.Logistics, 10f);
            SetWeight(ref buffer, ResourceType.HeavyWater, Space4XResourceBand.Logistics, 8f);
            SetWeight(ref buffer, ResourceType.Isotopes, Space4XResourceBand.Logistics, 8f);
            SetWeight(ref buffer, ResourceType.EnergyCrystals, Space4XResourceBand.Logistics, 8f);
            SetWeight(ref buffer, ResourceType.Ore, Space4XResourceBand.Logistics, 8f);
            SetWeight(ref buffer, ResourceType.RareMetals, Space4XResourceBand.Logistics, 6f);
            SetWeight(ref buffer, ResourceType.IndustrialCrystals, Space4XResourceBand.Logistics, 4f);
            SetWeight(ref buffer, ResourceType.TransplutonicOre, Space4XResourceBand.Logistics, 4f);
            SetWeight(ref buffer, ResourceType.ExoticGases, Space4XResourceBand.Logistics, 4f);
            SetWeight(ref buffer, ResourceType.LiquidOzone, Space4XResourceBand.Logistics, 4f);
            SetWeight(ref buffer, ResourceType.StrontiumClathrates, Space4XResourceBand.Logistics, 3f);
            SetWeight(ref buffer, ResourceType.SalvageComponents, Space4XResourceBand.Logistics, 3f);
            SetWeight(ref buffer, ResourceType.VolatileMotes, Space4XResourceBand.Logistics, 3f);
            SetWeight(ref buffer, ResourceType.BoosterGas, Space4XResourceBand.Logistics, 3f);
            SetWeight(ref buffer, ResourceType.RelicData, Space4XResourceBand.Logistics, 2f);
        }

        private static void AddPoolWeights(ref DynamicBuffer<Space4XResourceWeightEntry> buffer, ResourceType[] pool, Space4XResourceBand band)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                AddWeight(ref buffer, pool[i], band, 1f);
            }
        }

        private static void AddWeight(ref DynamicBuffer<Space4XResourceWeightEntry> buffer, ResourceType type, Space4XResourceBand band, float weight)
        {
            var index = (int)type;
            if (index < 0 || index >= buffer.Length)
            {
                return;
            }

            var entry = buffer[index];
            switch (band)
            {
                case Space4XResourceBand.Inner:
                    entry.InnerWeight += weight;
                    break;
                case Space4XResourceBand.Mid:
                    entry.MidWeight += weight;
                    break;
                case Space4XResourceBand.Outer:
                    entry.OuterWeight += weight;
                    break;
                case Space4XResourceBand.Logistics:
                    entry.LogisticsWeight += weight;
                    break;
                case Space4XResourceBand.Nebula:
                    entry.NebulaWeight += weight;
                    break;
                case Space4XResourceBand.AncientCore:
                    entry.AncientCoreWeight += weight;
                    break;
                case Space4XResourceBand.BlackHole:
                    entry.BlackHoleWeight += weight;
                    break;
                case Space4XResourceBand.Neutron:
                    entry.NeutronWeight += weight;
                    break;
                case Space4XResourceBand.Hazard:
                    entry.HazardWeight += weight;
                    break;
                case Space4XResourceBand.SuperResource:
                    entry.SuperResourceWeight += weight;
                    break;
                case Space4XResourceBand.Ruins:
                    entry.RuinsWeight += weight;
                    break;
                case Space4XResourceBand.Gate:
                    entry.GateWeight += weight;
                    break;
            }
            buffer[index] = entry;
        }

        private static void SetWeight(ref DynamicBuffer<Space4XResourceWeightEntry> buffer, ResourceType type, Space4XResourceBand band, float weight)
        {
            var index = (int)type;
            if (index < 0 || index >= buffer.Length)
            {
                return;
            }

            var entry = buffer[index];
            switch (band)
            {
                case Space4XResourceBand.Inner:
                    entry.InnerWeight = weight;
                    break;
                case Space4XResourceBand.Mid:
                    entry.MidWeight = weight;
                    break;
                case Space4XResourceBand.Outer:
                    entry.OuterWeight = weight;
                    break;
                case Space4XResourceBand.Logistics:
                    entry.LogisticsWeight = weight;
                    break;
                case Space4XResourceBand.Nebula:
                    entry.NebulaWeight = weight;
                    break;
                case Space4XResourceBand.AncientCore:
                    entry.AncientCoreWeight = weight;
                    break;
                case Space4XResourceBand.BlackHole:
                    entry.BlackHoleWeight = weight;
                    break;
                case Space4XResourceBand.Neutron:
                    entry.NeutronWeight = weight;
                    break;
                case Space4XResourceBand.Hazard:
                    entry.HazardWeight = weight;
                    break;
                case Space4XResourceBand.SuperResource:
                    entry.SuperResourceWeight = weight;
                    break;
                case Space4XResourceBand.Ruins:
                    entry.RuinsWeight = weight;
                    break;
                case Space4XResourceBand.Gate:
                    entry.GateWeight = weight;
                    break;
            }
            buffer[index] = entry;
        }

        private static readonly ResourceType[] InnerPool =
        {
            ResourceType.Minerals, ResourceType.Minerals, ResourceType.Minerals,
            ResourceType.Food,
            ResourceType.Water,
            ResourceType.Supplies,
            ResourceType.Fuel,
            ResourceType.Ore, ResourceType.Ore,
            ResourceType.EnergyCrystals, ResourceType.EnergyCrystals,
            ResourceType.OrganicMatter, ResourceType.OrganicMatter,
            ResourceType.Volatiles, ResourceType.Volatiles,
            ResourceType.HeavyWater,
            ResourceType.Isotopes,
            ResourceType.RareMetals,
            ResourceType.IndustrialCrystals
        };

        private static readonly ResourceType[] MidPool =
        {
            ResourceType.Minerals,
            ResourceType.Food,
            ResourceType.Water,
            ResourceType.Supplies,
            ResourceType.Fuel,
            ResourceType.Ore,
            ResourceType.EnergyCrystals,
            ResourceType.OrganicMatter,
            ResourceType.Volatiles,
            ResourceType.Isotopes,
            ResourceType.HeavyWater,
            ResourceType.RareMetals, ResourceType.RareMetals,
            ResourceType.IndustrialCrystals, ResourceType.IndustrialCrystals,
            ResourceType.ExoticGases,
            ResourceType.LiquidOzone,
            ResourceType.TransplutonicOre,
            ResourceType.SalvageComponents
        };

        private static readonly ResourceType[] OuterPool =
        {
            ResourceType.Minerals,
            ResourceType.Water,
            ResourceType.Fuel,
            ResourceType.RareMetals,
            ResourceType.TransplutonicOre, ResourceType.TransplutonicOre,
            ResourceType.ExoticGases, ResourceType.ExoticGases,
            ResourceType.VolatileMotes, ResourceType.VolatileMotes,
            ResourceType.IndustrialCrystals,
            ResourceType.LiquidOzone,
            ResourceType.StrontiumClathrates,
            ResourceType.SalvageComponents,
            ResourceType.BoosterGas,
            ResourceType.RelicData,
            ResourceType.Isotopes,
            ResourceType.HeavyWater
        };

        private static readonly ResourceType[] NebulaBiasPool =
        {
            ResourceType.ExoticGases, ResourceType.ExoticGases,
            ResourceType.VolatileMotes,
            ResourceType.LiquidOzone,
            ResourceType.EnergyCrystals,
            ResourceType.Volatiles
        };

        private static readonly ResourceType[] AncientCoreBiasPool =
        {
            ResourceType.RelicData, ResourceType.RelicData,
            ResourceType.SalvageComponents,
            ResourceType.IndustrialCrystals,
            ResourceType.TransplutonicOre
        };

        private static readonly ResourceType[] BlackHoleBiasPool =
        {
            ResourceType.TransplutonicOre,
            ResourceType.ExoticGases,
            ResourceType.VolatileMotes,
            ResourceType.HeavyWater,
            ResourceType.Isotopes
        };

        private static readonly ResourceType[] NeutronBiasPool =
        {
            ResourceType.Isotopes,
            ResourceType.HeavyWater,
            ResourceType.ExoticGases,
            ResourceType.IndustrialCrystals
        };

        private static readonly ResourceType[] HazardBiasPool =
        {
            ResourceType.VolatileMotes,
            ResourceType.BoosterGas,
            ResourceType.LiquidOzone,
            ResourceType.ExoticGases
        };

        private static readonly ResourceType[] SuperResourceBiasPool =
        {
            ResourceType.TransplutonicOre,
            ResourceType.RareMetals,
            ResourceType.IndustrialCrystals
        };

        private static readonly ResourceType[] RuinsBiasPool =
        {
            ResourceType.RelicData,
            ResourceType.SalvageComponents,
            ResourceType.IndustrialCrystals
        };

        private static readonly ResourceType[] GateBiasPool =
        {
            ResourceType.RelicData,
            ResourceType.Isotopes,
            ResourceType.ExoticGases
        };
    }

    public static class Space4XResourceDistributionBaselines
    {
        public static void CopyWeightsToBaseline(
            in DynamicBuffer<Space4XResourceWeightEntry> source,
            ref DynamicBuffer<Space4XResourceWeightBaselineEntry> destination)
        {
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                var entry = source[i];
                destination.Add(new Space4XResourceWeightBaselineEntry
                {
                    Type = entry.Type,
                    InnerWeight = entry.InnerWeight,
                    MidWeight = entry.MidWeight,
                    OuterWeight = entry.OuterWeight,
                    LogisticsWeight = entry.LogisticsWeight,
                    NebulaWeight = entry.NebulaWeight,
                    AncientCoreWeight = entry.AncientCoreWeight,
                    BlackHoleWeight = entry.BlackHoleWeight,
                    NeutronWeight = entry.NeutronWeight,
                    HazardWeight = entry.HazardWeight,
                    SuperResourceWeight = entry.SuperResourceWeight,
                    RuinsWeight = entry.RuinsWeight,
                    GateWeight = entry.GateWeight
                });
            }
        }

        public static void CopyBaselineToWeights(
            in DynamicBuffer<Space4XResourceWeightBaselineEntry> source,
            ref DynamicBuffer<Space4XResourceWeightEntry> destination)
        {
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                var entry = source[i];
                destination.Add(new Space4XResourceWeightEntry
                {
                    Type = entry.Type,
                    InnerWeight = entry.InnerWeight,
                    MidWeight = entry.MidWeight,
                    OuterWeight = entry.OuterWeight,
                    LogisticsWeight = entry.LogisticsWeight,
                    NebulaWeight = entry.NebulaWeight,
                    AncientCoreWeight = entry.AncientCoreWeight,
                    BlackHoleWeight = entry.BlackHoleWeight,
                    NeutronWeight = entry.NeutronWeight,
                    HazardWeight = entry.HazardWeight,
                    SuperResourceWeight = entry.SuperResourceWeight,
                    RuinsWeight = entry.RuinsWeight,
                    GateWeight = entry.GateWeight
                });
            }
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XResourceDistributionBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XResourceDistributionConfig>(out var entity))
            {
                EnsureDefaults(ref state, entity);
                state.Enabled = false;
                return;
            }

            entity = state.EntityManager.CreateEntity(typeof(Space4XResourceDistributionConfig));
            state.EntityManager.SetComponentData(entity, Space4XResourceDistributionConfig.Default);
            var weightsBuffer = state.EntityManager.AddBuffer<Space4XResourceWeightEntry>(entity);
            Space4XResourceDistributionDefaults.PopulateDefaults(ref weightsBuffer);
            state.EntityManager.AddComponentData(entity, new Space4XResourceDistributionBaselineConfig
            {
                BiasChance = Space4XResourceDistributionConfig.Default.BiasChance
            });
            state.EntityManager.AddBuffer<Space4XResourceWeightBaselineEntry>(entity);
            var weights = state.EntityManager.GetBuffer<Space4XResourceWeightEntry>(entity);
            var baselineBuffer = state.EntityManager.GetBuffer<Space4XResourceWeightBaselineEntry>(entity);
            Space4XResourceDistributionBaselines.CopyWeightsToBaseline(weights, ref baselineBuffer);
            state.EntityManager.SetName(entity, "Space4XResourceDistributionConfig");
            state.Enabled = false;
        }

        private void EnsureDefaults(ref SystemState state, Entity entity)
        {
            var entityManager = state.EntityManager;
            if (!entityManager.HasBuffer<Space4XResourceWeightEntry>(entity))
            {
                var buffer = entityManager.AddBuffer<Space4XResourceWeightEntry>(entity);
                Space4XResourceDistributionDefaults.PopulateDefaults(ref buffer);
            }

            var weights = entityManager.GetBuffer<Space4XResourceWeightEntry>(entity);
            if (weights.Length != (int)ResourceType.Count)
            {
                Space4XResourceDistributionDefaults.PopulateDefaults(ref weights);
            }

            if (!entityManager.HasComponent<Space4XResourceDistributionBaselineConfig>(entity))
            {
                entityManager.AddComponentData(entity, new Space4XResourceDistributionBaselineConfig
                {
                    BiasChance = entityManager.GetComponentData<Space4XResourceDistributionConfig>(entity).BiasChance
                });
            }

            if (!entityManager.HasBuffer<Space4XResourceWeightBaselineEntry>(entity))
            {
                entityManager.AddBuffer<Space4XResourceWeightBaselineEntry>(entity);
            }

            weights = entityManager.GetBuffer<Space4XResourceWeightEntry>(entity);
            var baselineBuffer = entityManager.GetBuffer<Space4XResourceWeightBaselineEntry>(entity);
            if (baselineBuffer.Length != weights.Length)
            {
                Space4XResourceDistributionBaselines.CopyWeightsToBaseline(weights, ref baselineBuffer);
            }
        }
    }
}
