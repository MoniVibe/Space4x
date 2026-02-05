using PureDOTS.Runtime.Individual;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures Space4X individuals have default anatomy slots for shared limb systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Runtime.Individual.AnatomyLimbBootstrapSystem))]
    public partial struct Space4XAnatomyBootstrapSystem : ISystem
    {
        private ComponentLookup<Space4X.Registry.SentientAnatomy> _space4xAnatomyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimIndividualTag>();
            _space4xAnatomyLookup = state.GetComponentLookup<Space4X.Registry.SentientAnatomy>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _space4xAnatomyLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<SimIndividualTag>>().WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<AnatomySlot>(entity))
                {
                    var slots = ecb.AddBuffer<AnatomySlot>(entity);
                    slots.Add(new AnatomySlot { SlotId = "head", SlotType = AnatomySlotType.Limb, HealthMultiplier = 0.8f });
                    slots.Add(new AnatomySlot { SlotId = "torso", SlotType = AnatomySlotType.Limb, HealthMultiplier = 1.2f });
                    slots.Add(new AnatomySlot { SlotId = "left_arm", SlotType = AnatomySlotType.Limb, HealthMultiplier = 0.9f });
                    slots.Add(new AnatomySlot { SlotId = "right_arm", SlotType = AnatomySlotType.Limb, HealthMultiplier = 0.9f });
                    slots.Add(new AnatomySlot { SlotId = "left_leg", SlotType = AnatomySlotType.Limb, HealthMultiplier = 1.0f });
                    slots.Add(new AnatomySlot { SlotId = "right_leg", SlotType = AnatomySlotType.Limb, HealthMultiplier = 1.0f });
                }

                if (!SystemAPI.HasComponent<SentientAnatomy>(entity))
                {
                    var speciesId = new FixedString64Bytes("space4x.sentient");
                    if (_space4xAnatomyLookup.HasComponent(entity))
                    {
                        speciesId = _space4xAnatomyLookup[entity].SpeciesId;
                    }

                    ecb.AddComponent(entity, new SentientAnatomy { SpeciesId = speciesId });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
