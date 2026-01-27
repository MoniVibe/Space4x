using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    [DisallowMultipleComponent]
    public sealed class EntityResourceBatteriesAuthoring : MonoBehaviour
    {
        [Header("Energy")]
        [SerializeField] private bool enableEnergy;
        [SerializeField] private float energyMax = 100f;
        [SerializeField] private float energyStart = 100f;
        [SerializeField] private float energyRegen = 5f;

        [Header("Heat")]
        [SerializeField] private bool enableHeat;
        [SerializeField] private float temperature = 0f;
        [SerializeField] private float safeMin = -10f;
        [SerializeField] private float safeMax = 80f;
        [SerializeField] private float coolRate = 5f;

        [Header("Integrity")]
        [SerializeField] private bool enableIntegrity;
        [SerializeField] private float integrityMax = 100f;
        [SerializeField] private float integrityStart = 100f;
        [SerializeField] private float integrityRegen = 0f;

        private sealed class Baker : Baker<EntityResourceBatteriesAuthoring>
        {
            public override void Bake(EntityResourceBatteriesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (authoring.enableEnergy)
                {
                    AddComponent(entity, new EnergyPool
                    {
                        Max = Mathf.Max(0f, authoring.energyMax),
                        Current = Mathf.Clamp(authoring.energyStart, 0f, Mathf.Max(0.01f, authoring.energyMax)),
                        RegenPerSecond = authoring.energyRegen
                    });
                }

                if (authoring.enableHeat)
                {
                    AddComponent(entity, new HeatState
                    {
                        Temperature = authoring.temperature,
                        SafeMin = authoring.safeMin,
                        SafeMax = authoring.safeMax,
                        CoolRatePerSecond = authoring.coolRate
                    });
                }

                if (authoring.enableIntegrity)
                {
                    AddComponent(entity, new IntegrityState
                    {
                        Max = Mathf.Max(0f, authoring.integrityMax),
                        Current = Mathf.Clamp(authoring.integrityStart, 0f, Mathf.Max(0.01f, authoring.integrityMax)),
                        RegenPerSecond = authoring.integrityRegen
                    });
                }
            }
        }
    }
}



