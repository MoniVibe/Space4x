using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Global player resource tracking singleton.
    /// Updated when carriers receive cargo from mining vessels.
    /// </summary>
    public struct PlayerResources : IComponentData
    {
        public float Minerals;
        public float RareMetals;
        public float EnergyCrystals;
        public float OrganicMatter;
        public float Ore;
        public float Volatiles;
        public float TransplutonicOre;
        public float ExoticGases;
        public float VolatileMotes;
        public float IndustrialCrystals;
        public float Isotopes;
        public float HeavyWater;
        public float LiquidOzone;
        public float StrontiumClathrates;
        public float SalvageComponents;
        public float BoosterGas;
        public float RelicData;
        public float Food;
        public float Water;
        public float Supplies;
        public float Fuel;

        public float GetResource(ResourceType type)
        {
            return type switch
            {
                ResourceType.Minerals => Minerals,
                ResourceType.RareMetals => RareMetals,
                ResourceType.EnergyCrystals => EnergyCrystals,
                ResourceType.OrganicMatter => OrganicMatter,
                ResourceType.Ore => Ore,
                ResourceType.Volatiles => Volatiles,
                ResourceType.TransplutonicOre => TransplutonicOre,
                ResourceType.ExoticGases => ExoticGases,
                ResourceType.VolatileMotes => VolatileMotes,
                ResourceType.IndustrialCrystals => IndustrialCrystals,
                ResourceType.Isotopes => Isotopes,
                ResourceType.HeavyWater => HeavyWater,
                ResourceType.LiquidOzone => LiquidOzone,
                ResourceType.StrontiumClathrates => StrontiumClathrates,
                ResourceType.SalvageComponents => SalvageComponents,
                ResourceType.BoosterGas => BoosterGas,
                ResourceType.RelicData => RelicData,
                ResourceType.Food => Food,
                ResourceType.Water => Water,
                ResourceType.Supplies => Supplies,
                ResourceType.Fuel => Fuel,
                _ => 0f
            };
        }

        public void AddResource(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Minerals:
                    Minerals += amount;
                    break;
                case ResourceType.RareMetals:
                    RareMetals += amount;
                    break;
                case ResourceType.EnergyCrystals:
                    EnergyCrystals += amount;
                    break;
                case ResourceType.OrganicMatter:
                    OrganicMatter += amount;
                    break;
                case ResourceType.Ore:
                    Ore += amount;
                    break;
                case ResourceType.Volatiles:
                    Volatiles += amount;
                    break;
                case ResourceType.TransplutonicOre:
                    TransplutonicOre += amount;
                    break;
                case ResourceType.ExoticGases:
                    ExoticGases += amount;
                    break;
                case ResourceType.VolatileMotes:
                    VolatileMotes += amount;
                    break;
                case ResourceType.IndustrialCrystals:
                    IndustrialCrystals += amount;
                    break;
                case ResourceType.Isotopes:
                    Isotopes += amount;
                    break;
                case ResourceType.HeavyWater:
                    HeavyWater += amount;
                    break;
                case ResourceType.LiquidOzone:
                    LiquidOzone += amount;
                    break;
                case ResourceType.StrontiumClathrates:
                    StrontiumClathrates += amount;
                    break;
                case ResourceType.SalvageComponents:
                    SalvageComponents += amount;
                    break;
                case ResourceType.BoosterGas:
                    BoosterGas += amount;
                    break;
                case ResourceType.RelicData:
                    RelicData += amount;
                    break;
                case ResourceType.Food:
                    Food += amount;
                    break;
                case ResourceType.Water:
                    Water += amount;
                    break;
                case ResourceType.Supplies:
                    Supplies += amount;
                    break;
                case ResourceType.Fuel:
                    Fuel += amount;
                    break;
            }
        }
    }

    /// <summary>
    /// Ensures PlayerResources singleton exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XPlayerResourcesBootstrapSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PlayerResources>())
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<PlayerResources>(entity);
            state.Enabled = false;
        }
    }
}
