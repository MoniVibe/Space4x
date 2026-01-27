#if UNITY_EDITOR
using PureDOTS.Runtime.Space;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class CarrierDockingAuthoring : MonoBehaviour
    {
        [Header("Hangar Capacity")]
        [Min(0)] public int maxDockedCraft = 12;
        [Min(0)] public int maxHangarSlots = 18;

        [Header("Throughput (craft per minute)")]
        [Min(0f)] public float dockingThroughput = 6f;
        [Min(0f)] public float undockingThroughput = 8f;

        [Header("Default Radii (km)")]
        [Min(0f)] public float defaultPatrolRadiusKm = 25f;
        [Min(0f)] public float defaultHarvestRadiusKm = 2f;
    }

    public sealed class CarrierDockingBaker : Baker<CarrierDockingAuthoring>
    {
        public override void Bake(CarrierDockingAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new DockingBayConfig
            {
                MaxDockedCraft = Mathf.Max(0, authoring.maxDockedCraft),
                MaxHangarSlots = Mathf.Max(0, authoring.maxHangarSlots),
                DockingThroughputPerMinute = Mathf.Max(0f, authoring.dockingThroughput),
                UndockingThroughputPerMinute = Mathf.Max(0f, authoring.undockingThroughput),
                DefaultPatrolRadiusKm = Mathf.Max(0f, authoring.defaultPatrolRadiusKm),
                DefaultHarvestRadiusKm = Mathf.Max(0f, authoring.defaultHarvestRadiusKm)
            });

            AddComponent(entity, new DockingBayState
            {
                CurrentDocked = 0,
                PendingLaunchCount = 0,
                LastDockTick = 0,
                LastLaunchTick = 0
            });
        }
    }
}
#endif
