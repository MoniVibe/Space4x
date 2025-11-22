using System;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Authoring helper to declare carrier module slots and refit/repair capabilities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCarrierModuleSlotsAuthoring : MonoBehaviour
    {
        [SerializeField] private SlotDefinition[] slots = Array.Empty<SlotDefinition>();

        [Header("Refit")]
        [Min(0f)] public float RefitRatePerSecond = 1f;
        public bool SupportsFieldRefit = true;

        [Header("Field Repair")]
        [Min(0f)] public float RepairRatePerSecond = 0.5f;
        [Min(0f)] public float CriticalRepairRate = 0.1f;
        public bool CanRepairCritical = false;

        [Serializable]
        public struct SlotDefinition
        {
            public int SlotIndex;
            public ModuleSlotSize SlotSize;
            public GameObject Module;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XCarrierModuleSlotsAuthoring>
        {
            public override void Bake(Space4XCarrierModuleSlotsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<CarrierModuleSlot>(entity);

                var slots = authoring.slots ?? Array.Empty<SlotDefinition>();
                SortSlots(slots);

                foreach (var slot in slots)
                {
                    var moduleEntity = slot.Module != null ? GetEntity(slot.Module, TransformUsageFlags.Dynamic) : Entity.Null;
                    buffer.Add(new CarrierModuleSlot
                    {
                        SlotIndex = slot.SlotIndex,
                        SlotSize = slot.SlotSize,
                        CurrentModule = moduleEntity,
                        TargetModule = moduleEntity,
                        RefitProgress = 0f,
                        State = moduleEntity == Entity.Null ? ModuleSlotState.Empty : ModuleSlotState.Active
                    });
                }

                if (authoring.RefitRatePerSecond > 0f)
                {
                    AddComponent(entity, new ModuleRefitFacility
                    {
                        RefitRatePerSecond = authoring.RefitRatePerSecond,
                        SupportsFieldRefit = (byte)(authoring.SupportsFieldRefit ? 1 : 0)
                    });
                }

                if (authoring.RepairRatePerSecond > 0f || authoring.CriticalRepairRate > 0f)
                {
                    AddComponent(entity, new FieldRepairCapability
                    {
                        RepairRatePerSecond = authoring.RepairRatePerSecond,
                        CriticalRepairRate = authoring.CriticalRepairRate,
                        CanRepairCritical = (byte)(authoring.CanRepairCritical ? 1 : 0)
                    });
                }

                AddComponent(entity, new ModuleStatAggregate
                {
                    SpeedMultiplier = 1f,
                    CargoMultiplier = 1f,
                    EnergyMultiplier = 1f,
                    RefitRateMultiplier = 1f,
                    RepairRateMultiplier = 1f,
                    ActiveModuleCount = 0
                });
            }

            private static void SortSlots(SlotDefinition[] entries)
            {
                for (var i = 0; i < entries.Length - 1; i++)
                {
                    var min = i;
                    for (var j = i + 1; j < entries.Length; j++)
                    {
                        if (entries[j].SlotIndex < entries[min].SlotIndex)
                        {
                            min = j;
                        }
                    }

                    if (min != i)
                    {
                        (entries[i], entries[min]) = (entries[min], entries[i]);
                    }
                }
            }
        }
    }
}
