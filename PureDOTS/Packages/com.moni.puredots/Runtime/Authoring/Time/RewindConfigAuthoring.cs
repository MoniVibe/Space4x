using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Authoring component for configuring baseline rewind state per scene/world.
    /// Emits only config; RewindState is created by bootstrap.
    /// </summary>
    public class RewindConfigAuthoring : MonoBehaviour
    {
        [Header("Tick")]
        [Tooltip("Seconds per simulation tick (e.g., 1/60).")]
        public float TickDuration = 1f / 60f;

        [Tooltip("Maximum history buffer length in ticks.")]
        public int MaxHistoryTicks = 10_000;

        [Header("Initial State")]
        public RewindMode InitialMode = RewindMode.Play;
    }

    public class RewindConfigBaker : Baker<RewindConfigAuthoring>
    {
        public override void Bake(RewindConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new RewindConfig
            {
                TickDuration = authoring.TickDuration,
                MaxHistoryTicks = authoring.MaxHistoryTicks,
                InitialMode = authoring.InitialMode
            });
        }
    }
}



