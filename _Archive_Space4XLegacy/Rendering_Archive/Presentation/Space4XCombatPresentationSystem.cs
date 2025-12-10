using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates combat presentation for carriers and crafts.
    /// Reads combat state and writes visual state and material properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierPresentationSystem))]
    public partial struct Space4XCombatPresentationSystem : ISystem
    {
        // Pre-defined color constants for Burst compatibility
        private static readonly float4 CombatColorMod = new float4(1.3f, 0.6f, 0.6f, 1f);
        private static readonly float4 ShieldColorMod = new float4(0.6f, 0.8f, 1.2f, 1f);
        private static readonly float4 DestroyedColorMod = new float4(0.3f, 0.3f, 0.3f, 0.5f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            // Presentation can be driven by wall-clock simulation time
            double currentTime = SystemAPI.Time.ElapsedTime;

            // Update carrier combat visuals
            new UpdateCarrierCombatJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                CombatColorMod = CombatColorMod,
                ShieldColorMod = ShieldColorMod,
                DestroyedColorMod = DestroyedColorMod
            }.ScheduleParallel();

            // Update craft combat visuals
            new UpdateCraftCombatJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                CombatColorMod = CombatColorMod
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CarrierPresentationTag))]
        private partial struct UpdateCarrierCombatJob : IJobEntity
        {
            public float DeltaTime;
            public double CurrentTime;
            public float4 CombatColorMod;
            public float4 ShieldColorMod;
            public float4 DestroyedColorMod;

            public void Execute(
                ref CarrierVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in CombatState combatState,
                in PresentationLOD lod)
            {
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update visual state based on combat state
                if (combatState.HealthRatio <= 0f)
                {
                    visualState.State = CarrierVisualStateType.Retreating; // Use Retreating as destroyed state
                }
                else if (combatState.IsInCombat)
                {
                    // Check if recently damaged (using time-based window for presentation)
                    // Assume ~60 ticks per second, so 10 ticks ≈ 0.17 seconds
                    // For presentation, use a fixed time window instead of tick comparison
                    const double recentDamageWindowSeconds = 0.2; // Within last 0.2 seconds
                    // Since we can't convert LastDamageTick to time directly, use health ratio as proxy
                    // If health is below 100%, assume recent damage for visual feedback
                    bool recentlyDamaged = combatState.HealthRatio < 1f;
                    if (recentlyDamaged)
                    {
                        visualState.State = CarrierVisualStateType.Combat;
                    }
                    else
                    {
                        visualState.State = CarrierVisualStateType.Combat;
                    }
                }

                // Apply combat color modifiers
                float4 baseColor = materialProps.BaseColor;
                if (combatState.IsInCombat)
                {
                    baseColor *= CombatColorMod;
                }

                // Add shield glow if shields are active
                if (combatState.ShieldRatio > 0.1f)
                {
                    float shieldPulse = 0.8f + 0.2f * math.sin(visualState.StateTimer * 4f);
                    materialProps.EmissiveColor = ShieldColorMod * combatState.ShieldRatio * shieldPulse * 0.5f;
                }
                else
                {
                    materialProps.EmissiveColor = float4.zero;
                }

                // Apply destroyed state
                if (combatState.HealthRatio <= 0f)
                {
                    baseColor *= DestroyedColorMod;
                    materialProps.Alpha = 0.5f;
                }

                materialProps.BaseColor = baseColor;
            }
        }

        [BurstCompile]
        [WithAll(typeof(CraftPresentationTag))]
        private partial struct UpdateCraftCombatJob : IJobEntity
        {
            public float DeltaTime;
            public double CurrentTime;
            public float4 CombatColorMod;

            public void Execute(
                ref CraftVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in CombatState combatState,
                in PresentationLOD lod)
            {
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update visual state based on combat state
                if (combatState.HealthRatio <= 0f)
                {
                    visualState.State = CraftVisualStateType.Idle; // Use Idle as destroyed placeholder
                }
                else if (combatState.IsInCombat)
                {
                    visualState.State = CraftVisualStateType.Moving; // Use Moving as engaging placeholder
                }

                // Apply combat color modifiers
                if (combatState.IsInCombat)
                {
                    materialProps.BaseColor *= CombatColorMod;
                }
            }
        }
    }

    /// <summary>
    /// System that renders projectiles (lasers, missiles, beams).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCombatPresentationSystem))]
    public partial struct Space4XProjectilePresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectilePresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new UpdateProjectileJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ProjectilePresentationTag))]
        private partial struct UpdateProjectileJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref ProjectileVisualState visualState,
                ref MaterialPropertyOverride materialProps,
                in LocalTransform transform,
                in PresentationLOD lod)
            {
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Update lifetime
                visualState.Lifetime += DeltaTime;

                // Determine color based on projectile type
                float4 projectileColor = visualState.Type switch
                {
                    ProjectileVisualStateType.Laser => new float4(1f, 0.2f, 0.2f, 1f),      // Red
                    ProjectileVisualStateType.Kinetic => new float4(0.8f, 0.8f, 0.2f, 1f),   // Yellow
                    ProjectileVisualStateType.Missile => new float4(1f, 0.6f, 0f, 1f),       // Orange
                    ProjectileVisualStateType.Beam => new float4(0.2f, 0.8f, 1f, 1f),       // Cyan
                    _ => new float4(1f, 1f, 1f, 1f)
                };

                // Fade out as projectile ages
                float ageRatio = visualState.Lifetime / math.max(0.0001f, visualState.MaxLifetime);
                float alpha = 1f - math.saturate(ageRatio);

                materialProps.BaseColor = projectileColor;
                materialProps.EmissiveColor = projectileColor * 0.8f;
                materialProps.Alpha = alpha;
            }
        }
    }

    /// <summary>
    /// System that handles damage feedback (flashes, highlights).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCombatPresentationSystem))]
    public partial struct Space4XDamageFeedbackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            // Presentation can be driven by wall-clock simulation time
            double currentTime = SystemAPI.Time.ElapsedTime;

            new UpdateDamageFeedbackJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UpdateDamageFeedbackJob : IJobEntity
        {
            public float DeltaTime;
            public double CurrentTime;

            public void Execute(
                ref DamageFlash damageFlash,
                ref MaterialPropertyOverride materialProps,
                in CombatState combatState)
            {
                // Check if damage occurred recently (using health ratio as proxy since we can't convert ticks to time)
                // For presentation, assume damage if health is below 100%
                bool damageOccurred = combatState.HealthRatio < 1f;

                if (damageOccurred && damageFlash.FlashTimer <= 0f)
                {
                    // Start flash effect
                    damageFlash.FlashTimer = damageFlash.FlashDuration;
                    damageFlash.FlashIntensity = 1f;
                }

                // Update flash timer
                if (damageFlash.FlashTimer > 0f)
                {
                    damageFlash.FlashTimer -= DeltaTime;
                    damageFlash.FlashIntensity = math.saturate(damageFlash.FlashTimer / damageFlash.FlashDuration);

                    // Apply flash to material
                    float4 flashColor = damageFlash.FlashColor * damageFlash.FlashIntensity;
                    materialProps.BaseColor = math.lerp(materialProps.BaseColor, flashColor, damageFlash.FlashIntensity * 0.5f);
                    materialProps.EmissiveColor = flashColor * damageFlash.FlashIntensity * 0.3f;
                }
                else
                {
                    damageFlash.FlashIntensity = 0f;
                }
            }
        }
    }
}

