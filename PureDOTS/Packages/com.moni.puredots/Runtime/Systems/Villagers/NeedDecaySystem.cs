using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Computes need decay per tick using VillagerNeedCurve and updates VillagerNeedsHot.
    /// Burst-compiled, FixedStep only.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerArchetypeResolutionSystem))]
    public partial struct NeedDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerArchetypeCatalogComponent>();
            state.RequireForUpdate<VillagerNeedCurveCatalogComponent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }
            
            if (!SystemAPI.TryGetSingleton<VillagerArchetypeCatalogComponent>(out var archetypeCatalog) ||
                !SystemAPI.TryGetSingleton<VillagerNeedCurveCatalogComponent>(out var needCurveCatalog))
            {
                return;
            }
            
            var archetypeBlob = archetypeCatalog.Catalog;
            var needCurveBlob = needCurveCatalog.Catalog;
            var deltaTime = timeState.FixedDeltaTime;
            
            var job = new DecayNeedsJob
            {
                ArchetypeCatalog = archetypeBlob,
                NeedCurveCatalog = needCurveBlob,
                DeltaTime = deltaTime
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        
        [BurstCompile]
        public partial struct DecayNeedsJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<VillagerArchetypeCatalogBlob> ArchetypeCatalog;
            [ReadOnly] public BlobAssetReference<VillagerNeedCurveCatalogBlob> NeedCurveCatalog;
            public float DeltaTime;
            
            public void Execute(
                ref VillagerNeeds needs,
                ref VillagerNeedsHot needsHot,
                in VillagerArchetypeResolved archetype)
            {
                if (!ArchetypeCatalog.IsCreated || !NeedCurveCatalog.IsCreated)
                {
                    return;
                }
                
                var archetypeData = archetype.Data;
                
                // Decay needs based on archetype decay rates
                var hungerDecay = archetypeData.HungerDecayRate * DeltaTime;
                var energyDecay = archetypeData.EnergyDecayRate * DeltaTime;
                var moraleDecay = archetypeData.MoraleDecayRate * DeltaTime;
                
                // Update needs (increase hunger, decrease energy/morale)
                var currentHunger = needs.HungerFloat;
                var currentEnergy = needs.EnergyFloat;
                var currentMorale = needs.MoraleFloat;
                
                needs.SetHunger(math.min(100f, currentHunger + hungerDecay * 100f));
                needs.SetEnergy(math.max(0f, currentEnergy - energyDecay * 100f));
                needs.SetMorale(math.max(0f, currentMorale - moraleDecay * 100f));
                
                // Update hot component with normalized values
                needsHot.Hunger = math.clamp(needs.HungerFloat / 100f, 0f, 1f);
                needsHot.Energy = math.clamp(needs.EnergyFloat / 100f, 0f, 1f);
                needsHot.Morale = math.clamp(needs.MoraleFloat / 100f, 0f, 1f);
                
                // Evaluate utility curves for work/rest
                needsHot.UtilityWork = EvaluateUtility(NeedCurveCatalog, 0, needsHot.HungerUrgency, needsHot.EnergyUrgency); // Work utility
                needsHot.UtilityRest = EvaluateUtility(NeedCurveCatalog, 1, needsHot.EnergyUrgency, needsHot.MoraleUrgency); // Rest utility
            }
            
            private static float EvaluateUtility(
                BlobAssetReference<VillagerNeedCurveCatalogBlob> catalog,
                byte needType,
                float urgency1,
                float urgency2)
            {
                if (!catalog.IsCreated)
                {
                    return 0.5f; // Default utility
                }
                
                // Find curve for this need type
                for (int i = 0; i < catalog.Value.Curves.Length; i++)
                {
                    ref var curve = ref catalog.Value.Curves[i];
                    if (curve.NeedType == needType)
                    {
                        // Evaluate curve with urgency (higher urgency = higher utility)
                        // Urgency is already normalized 0-1, pass directly to curve
                        return curve.Evaluate(urgency1);
                    }
                }
                
                // Fallback: linear utility based on urgency
                return urgency1;
            }
        }
    }
}

