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

        public float GetResource(ResourceType type)
        {
            return type switch
            {
                ResourceType.Minerals => Minerals,
                ResourceType.RareMetals => RareMetals,
                ResourceType.EnergyCrystals => EnergyCrystals,
                ResourceType.OrganicMatter => OrganicMatter,
                ResourceType.Ore => Ore,
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

