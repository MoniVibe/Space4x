using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class PureDotsConfigAuthoring : MonoBehaviour
    {
        public PureDotsRuntimeConfig config;
    }

    public sealed class PureDotsConfigBaker : Baker<PureDotsConfigAuthoring>
    {
        public override void Bake(PureDotsConfigAuthoring authoring)
        {
            if (authoring.config == null)
            {
                Debug.LogWarning("PureDotsConfigAuthoring has no config asset assigned.", authoring);
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            var time = authoring.config.Time.ToComponent();
            var history = authoring.config.History.ToComponent();

            AddComponent(entity, time);
            AddComponent(entity, history);
        }
    }
}
