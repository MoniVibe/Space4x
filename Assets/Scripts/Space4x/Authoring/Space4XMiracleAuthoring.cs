using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Authoring helper for defining Space4X miracles that leverage the shared PureDOTS miracle components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XMiracleAuthoring : MonoBehaviour
    {
        [SerializeField]
        private MiracleSetup[] miracles =
        {
            new MiracleSetup
            {
                Label = "Orbital Strike",
                Type = MiracleType.Fireball,
                CastingMode = MiracleCastingMode.Instant,
                BaseRadius = 12f,
                BaseIntensity = 1f,
                BaseCost = 40f,
                SustainedCostPerSecond = 0f,
                Lifecycle = MiracleLifecycleState.Ready,
                ChargePercent = 1f,
                CurrentRadius = 12f,
                CurrentIntensity = 1f,
                CooldownSecondsRemaining = 0f,
                Position = float3.zero,
                TargetPosition = float3.zero,
                UseAuthoringTransform = true
            }
        };

        public MiracleSetup[] Miracles => miracles;

        [Serializable]
        public struct MiracleSetup
        {
            [Tooltip("Optional label used for editor organization only.")]
            public string Label;
            public MiracleType Type;
            public MiracleCastingMode CastingMode;
            [Min(0f)]
            public float BaseRadius;
            [Min(0f)]
            public float BaseIntensity;
            [Min(0f)]
            public float BaseCost;
            [Min(0f)]
            public float SustainedCostPerSecond;
            public MiracleLifecycleState Lifecycle;
            [Range(0f, 1f)]
            public float ChargePercent;
            [Min(0f)]
            public float CurrentRadius;
            [Min(0f)]
            public float CurrentIntensity;
            [Min(0f)]
            public float CooldownSecondsRemaining;
            public uint LastCastTick;
            public byte AlignmentDelta;
            public bool UseAuthoringTransform;
            public float3 Position;
            public bool AddTarget;
            public float3 TargetPosition;
            public Transform TargetEntity;
            public Transform Caster;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XMiracleAuthoring>
        {
            public override void Bake(Space4XMiracleAuthoring authoring)
            {
                if (authoring.miracles == null || authoring.miracles.Length == 0)
                {
                    return;
                }

                foreach (var miracle in authoring.miracles)
                {
                    CreateMiracleEntity(miracle, authoring);
                }
            }

            private void CreateMiracleEntity(MiracleSetup setup, Space4XMiracleAuthoring authoring)
            {
                var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);

                var position = setup.UseAuthoringTransform
                    ? math.float3(authoring.transform.position)
                    : setup.Position;

                AddComponent(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                AddComponent<SpatialIndexedTag>(entity);
                AddComponent(entity, new SpatialGridResidency
                {
                    CellId = -1,
                    LastPosition = position,
                    Version = 0
                });
                AddComponent(entity, new MiracleDefinition
                {
                    Type = setup.Type,
                    CastingMode = setup.CastingMode,
                    BaseRadius = math.max(0f, setup.BaseRadius),
                    BaseIntensity = math.max(0f, setup.BaseIntensity),
                    BaseCost = math.max(0f, setup.BaseCost),
                    SustainedCostPerSecond = math.max(0f, setup.SustainedCostPerSecond)
                });

                AddComponent(entity, new MiracleRuntimeState
                {
                    Lifecycle = setup.Lifecycle,
                    ChargePercent = math.clamp(setup.ChargePercent, 0f, 1f),
                    CurrentRadius = math.max(0f, setup.CurrentRadius),
                    CurrentIntensity = math.max(0f, setup.CurrentIntensity),
                    CooldownSecondsRemaining = math.max(0f, setup.CooldownSecondsRemaining),
                    LastCastTick = setup.LastCastTick,
                    AlignmentDelta = setup.AlignmentDelta
                });

                if (setup.AddTarget)
                {
                    var targetEntity = setup.TargetEntity != null
                        ? GetEntity(setup.TargetEntity, TransformUsageFlags.Dynamic)
                        : Entity.Null;

                    AddComponent(entity, new MiracleTarget
                    {
                        TargetPosition = setup.TargetPosition,
                        TargetEntity = targetEntity
                    });
                }

                if (setup.Caster != null)
                {
                    var casterEntity = GetEntity(setup.Caster, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new MiracleCaster
                    {
                        CasterEntity = casterEntity
                    });
                }
            }
        }
    }
}
