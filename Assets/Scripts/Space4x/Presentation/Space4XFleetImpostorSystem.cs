using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that manages fleet impostor rendering for distant fleets.
    /// Creates and updates fleet icons when fleets are at Impostor LOD level.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XFleetImpostorSystem : ISystem
    {
        // Pre-defined colors for fleet strength indicators
        private static readonly float4 WeakFleetColor = new float4(0.5f, 0.5f, 0.5f, 1f);
        private static readonly float4 MediumFleetColor = new float4(0.8f, 0.8f, 0.4f, 1f);
        private static readonly float4 StrongFleetColor = new float4(1f, 0.6f, 0.2f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleet>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update fleet aggregate data and impostor visuals
            new UpdateFleetImpostorJob
            {
                DeltaTime = deltaTime,
                WeakFleetColor = WeakFleetColor,
                MediumFleetColor = MediumFleetColor,
                StrongFleetColor = StrongFleetColor
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UpdateFleetImpostorJob : IJobEntity
        {
            public float DeltaTime;
            public float4 WeakFleetColor;
            public float4 MediumFleetColor;
            public float4 StrongFleetColor;

            public void Execute(
                ref FleetAggregateData aggregateData,
                ref FleetIconMesh iconMesh,
                ref FleetStrengthIndicator strengthIndicator,
                ref MaterialPropertyOverride materialProps,
                in Space4XFleet fleet,
                in LocalTransform transform,
                in PresentationLOD lod,
                in FactionColor factionColor)
            {
                // Only process fleets at Impostor LOD or higher
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update aggregate data
                aggregateData.Centroid = transform.Position;
                aggregateData.ShipCount = fleet.ShipCount;

                // Calculate normalized strength (0-1)
                float normalizedStrength = math.saturate(fleet.ShipCount / 100f); // Assume 100 ships is "full strength"
                aggregateData.Strength = normalizedStrength;
                strengthIndicator.NormalizedStrength = normalizedStrength;

                // Calculate indicator level (1-5 bars)
                strengthIndicator.IndicatorLevel = (int)math.ceil(normalizedStrength * 5f);

                // Scale icon based on fleet size
                iconMesh.Size = 1f + normalizedStrength * 2f;

                // Determine fleet color based on strength
                float4 strengthColor;
                if (normalizedStrength < 0.33f)
                {
                    strengthColor = WeakFleetColor;
                }
                else if (normalizedStrength < 0.66f)
                {
                    strengthColor = MediumFleetColor;
                }
                else
                {
                    strengthColor = StrongFleetColor;
                }

                // Blend faction color with strength color
                materialProps.BaseColor = math.lerp(factionColor.Value, strengthColor, 0.3f);

                // Add pulse effect for engaging fleets
                if (fleet.Posture == Space4XFleetPosture.Engaging)
                {
                    float pulse = 0.7f + 0.3f * math.sin(materialProps.PulsePhase * 6f);
                    materialProps.BaseColor *= pulse;
                    materialProps.EmissiveColor = new float4(1f, 0.3f, 0.3f, 1f) * 0.5f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }

                materialProps.PulsePhase += DeltaTime;
                materialProps.Alpha = 1f;
            }
        }
    }

    /// <summary>
    /// Authoring component for fleet impostor entities.
    /// </summary>
    public class FleetImpostorAuthoring : UnityEngine.MonoBehaviour
    {
        [UnityEngine.Header("Fleet Settings")]
        [UnityEngine.Tooltip("Initial ship count")]
        public int ShipCount = 10;

        [UnityEngine.Tooltip("Fleet posture")]
        public Space4XFleetPosture Posture = Space4XFleetPosture.Idle;

        [UnityEngine.Header("Visual Settings")]
        [UnityEngine.Tooltip("Faction color")]
        public UnityEngine.Color FactionColorValue = UnityEngine.Color.blue;

        [UnityEngine.Tooltip("Icon mesh index")]
        public int IconMeshIndex = 0;
    }

    /// <summary>
    /// Baker for FleetImpostorAuthoring.
    /// </summary>
    public class FleetImpostorBaker : Baker<FleetImpostorAuthoring>
    {
        public override void Bake(FleetImpostorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add fleet impostor tag
            AddComponent(entity, new FleetImpostorTag());

            // Add fleet component
            AddComponent(entity, new Space4XFleet
            {
                FleetId = new Unity.Collections.FixedString64Bytes("Fleet_" + entity.Index),
                ShipCount = authoring.ShipCount,
                Posture = authoring.Posture,
                TaskForce = 0
            });

            // Add aggregate data
            AddComponent(entity, new FleetAggregateData
            {
                ShipCount = authoring.ShipCount,
                Strength = authoring.ShipCount / 100f
            });

            // Add icon mesh
            AddComponent(entity, new FleetIconMesh
            {
                MeshIndex = authoring.IconMeshIndex,
                Size = 1f
            });

            // Add strength indicator
            AddComponent(entity, new FleetStrengthIndicator
            {
                NormalizedStrength = authoring.ShipCount / 100f,
                IndicatorLevel = (int)math.ceil(authoring.ShipCount / 20f)
            });

            // Add faction color
            AddComponent(entity, new FactionColor
            {
                Value = new float4(
                    authoring.FactionColorValue.r,
                    authoring.FactionColorValue.g,
                    authoring.FactionColorValue.b,
                    authoring.FactionColorValue.a)
            });

            // Add LOD component
            AddComponent(entity, new PresentationLOD
            {
                Level = PresentationLODLevel.Impostor,
                DistanceToCamera = 1000f
            });

            // Add material property override
            AddComponent(entity, MaterialPropertyOverride.Default);
        }
    }
}

