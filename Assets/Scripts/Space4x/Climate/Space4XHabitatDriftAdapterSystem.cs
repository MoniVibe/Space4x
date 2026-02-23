using PureDOTS.Runtime.Space;
using PureDOTS.Systems;
using Space4X.Climate;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Climate.Systems
{
    /// <summary>
    /// Adapts Space4X biodeck modules into shared habitat drift signals/mitigation.
    /// Keeps module-specific behavior in game code while sharing generic drift math in PureDOTS.
    /// </summary>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(BioDeckClimateControlSystem))]
    public partial struct Space4XHabitatDriftAdapterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BioDeckModule>();
            state.RequireForUpdate<HabitatDriftRuntimeSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out HabitatDriftRuntimeSettings settings) || settings.Enabled == 0)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (module, cells, moduleEntity) in SystemAPI.Query<RefRO<BioDeckModule>, DynamicBuffer<BioDeckCell>>().WithEntityAccess())
            {
                var target = module.ValueRO.ShipOrStation != Entity.Null
                    ? module.ValueRO.ShipOrStation
                    : moduleEntity;
                if (!em.Exists(target))
                {
                    continue;
                }

                var cellCount = math.max(1, module.ValueRO.GridResolution.x * module.ValueRO.GridResolution.y);
                var climate = AverageClimate(in cells);
                var deckScale = math.saturate(math.sqrt(cellCount) * 0.125f);

                var exposure = new HabitatExposureSignature
                {
                    Conditions = new HabitatPreferenceVector
                    {
                        Temperature = climate.Temperature,
                        Moisture = climate.Moisture,
                        GravityRatio = 1f,
                        Artificiality = 0.9f
                    },
                    DriftPressure = math.lerp(0.55f, 0.35f, deckScale)
                };

                var mitigation = new HabitatDriftMitigation
                {
                    AdaptationRateMultiplier = math.lerp(1f, 0.8f, deckScale),
                    VoidborneRateMultiplier = math.lerp(0.9f, 0.55f, deckScale)
                };

                if (em.HasComponent<HabitatExposureSignature>(target))
                {
                    em.SetComponentData(target, exposure);
                }
                else
                {
                    ecb.AddComponent(target, exposure);
                }

                if (em.HasComponent<HabitatDriftMitigation>(target))
                {
                    em.SetComponentData(target, mitigation);
                }
                else
                {
                    ecb.AddComponent(target, mitigation);
                }
            }

            ecb.Playback(em);
        }

        private static PureDOTS.Environment.ClimateVector AverageClimate(in DynamicBuffer<BioDeckCell> cells)
        {
            if (cells.Length == 0)
            {
                return new PureDOTS.Environment.ClimateVector
                {
                    Temperature = 0f,
                    Moisture = 0.5f,
                    Fertility = 0.6f,
                    WaterLevel = 0.2f,
                    Ruggedness = 0.2f
                };
            }

            var sum = new PureDOTS.Environment.ClimateVector();
            for (int i = 0; i < cells.Length; i++)
            {
                var climate = cells[i].Climate;
                sum.Temperature += climate.Temperature;
                sum.Moisture += climate.Moisture;
                sum.Fertility += climate.Fertility;
                sum.WaterLevel += climate.WaterLevel;
                sum.Ruggedness += climate.Ruggedness;
            }

            var inv = math.rcp(cells.Length);
            return new PureDOTS.Environment.ClimateVector
            {
                Temperature = sum.Temperature * inv,
                Moisture = sum.Moisture * inv,
                Fertility = sum.Fertility * inv,
                WaterLevel = sum.WaterLevel * inv,
                Ruggedness = sum.Ruggedness * inv
            };
        }
    }
}
