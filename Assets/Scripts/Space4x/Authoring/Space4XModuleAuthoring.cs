using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Authoring helper that bakes a module definition into an entity with stats and health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XModuleAuthoring : MonoBehaviour
    {
        [SerializeField] private string moduleId = "space4x.module.generic";
        public ModuleSlotSize SlotSize = ModuleSlotSize.Medium;

        [Min(0f)] public float MaxHealth = 1f;
        [Range(0f, 1f)] public float MaxFieldRepairFraction = 0.8f;
        [Min(0f)] public float DegradationPerSecond = 0f;
        [Range(0, 255)] public byte RepairPriority = 128;

        public float SpeedMultiplier = 1f;
        public float CargoMultiplier = 1f;
        public float EnergyMultiplier = 1f;
        public float RefitRateMultiplier = 1f;
        public float RepairRateMultiplier = 1f;

        private sealed class Baker : Unity.Entities.Baker<Space4XModuleAuthoring>
        {
            public override void Bake(Space4XModuleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var id = new ModuleTypeId
                {
                    Value = new FixedString64Bytes(authoring.moduleId ?? string.Empty)
                };

                AddComponent(entity, id);
                AddComponent(entity, new ModuleSlotRequirement
                {
                    SlotSize = authoring.SlotSize
                });

                AddComponent(entity, new ModuleStatModifier
                {
                    SpeedMultiplier = math.max(0f, authoring.SpeedMultiplier),
                    CargoMultiplier = math.max(0f, authoring.CargoMultiplier),
                    EnergyMultiplier = math.max(0f, authoring.EnergyMultiplier),
                    RefitRateMultiplier = math.max(0f, authoring.RefitRateMultiplier),
                    RepairRateMultiplier = math.max(0f, authoring.RepairRateMultiplier)
                });

                var maxHealth = math.max(0.01f, authoring.MaxHealth);
                var maxFieldRepair = math.clamp(authoring.MaxFieldRepairFraction, 0f, 1f) * maxHealth;
                AddComponent(entity, new ModuleHealth
                {
                    MaxHealth = maxHealth,
                    CurrentHealth = maxHealth,
                    MaxFieldRepairHealth = math.clamp(maxFieldRepair, 0f, maxHealth),
                    DegradationPerSecond = math.max(0f, authoring.DegradationPerSecond),
                    RepairPriority = authoring.RepairPriority,
                    Failed = 0
                });
            }
        }
    }
}
