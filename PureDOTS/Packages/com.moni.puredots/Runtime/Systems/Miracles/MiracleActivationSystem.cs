using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Consumes miracle activation requests and spawns effect entities.
    /// Checks cooldowns and respects miracle specifications from catalog.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    public partial struct MiracleActivationSystem : ISystem
    {
        private TimeAwareController _controller;
        private BufferLookup<MiracleCooldown> _cooldownLookup;
        private ComponentLookup<MiracleTargetSolution> _targetSolutionLookup;
        private ComponentLookup<MiracleChargeState> _chargeStateLookup;
        private ComponentLookup<MiracleChannelState> _channelStateLookup;
        private ComponentLookup<DivineHandInput> _handInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleConfigState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            // Only Record mode - we don't process activations during CatchUp/Playback
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record,
                TimeAwareExecutionOptions.SkipWhenPaused);
            _cooldownLookup = state.GetBufferLookup<MiracleCooldown>(false);
            _targetSolutionLookup = state.GetComponentLookup<MiracleTargetSolution>(true);
            _chargeStateLookup = state.GetComponentLookup<MiracleChargeState>(true);
            _channelStateLookup = state.GetComponentLookup<MiracleChannelState>(true);
            _handInputLookup = state.GetComponentLookup<DivineHandInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            if (!_controller.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            // Only process in Record mode (not during playback)
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<MiracleConfigState>(out var configState))
            {
                return;
            }

            ref var catalog = ref configState.Catalog.Value;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _cooldownLookup.Update(ref state);
            _targetSolutionLookup.Update(ref state);
            _chargeStateLookup.Update(ref state);
            _channelStateLookup.Update(ref state);
            _handInputLookup.Update(ref state);
            
            // Track which entities need cooldown entries added (for new buffers created this frame)
            var pendingCooldowns = new NativeHashMap<Entity, NativeList<MiracleCooldown>>(16, Allocator.TempJob);

            // Process all activation requests
            foreach (var (requests, entity) in SystemAPI.Query<DynamicBuffer<MiracleActivationRequest>>().WithEntityAccess())
            {
                DynamicBuffer<MiracleCooldown> cooldowns = default;
                bool hasCooldownBuffer = _cooldownLookup.HasBuffer(entity);
                if (hasCooldownBuffer)
                {
                    cooldowns = _cooldownLookup[entity];
                }
                bool hasTargetSolution = _targetSolutionLookup.HasComponent(entity);
                MiracleTargetSolution targetSolution = hasTargetSolution ? _targetSolutionLookup[entity] : default;

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    if (hasTargetSolution)
                    {
                        bool mismatchedSelection = targetSolution.SelectedMiracleId != request.Id;
                        if (targetSolution.IsValid == 0 || mismatchedSelection)
                        {
                            requests.RemoveAt(i);
                            continue;
                        }
                    }
                    
                    // Find spec in catalog
                    bool foundSpec = false;
                    int specIndex = -1;
                    for (int j = 0; j < catalog.Specs.Length; j++)
                    {
                        if (catalog.Specs[j].Id == request.Id)
                        {
                            specIndex = j;
                            foundSpec = true;
                            break;
                        }
                    }

                    if (!foundSpec)
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    ref var spec = ref catalog.Specs[specIndex];

                    // Validate dispense mode compatibility
                    byte requestedMode = request.DispenseMode;
                    byte allowedModes = spec.AllowedDispenseModes;
                    bool modeAllowed = false;
                    if (requestedMode == (byte)DispenseMode.Sustained)
                    {
                        modeAllowed = (allowedModes & (byte)DispenseMode.Sustained) != 0;
                    }
                    else if (requestedMode == (byte)DispenseMode.Throw)
                    {
                        modeAllowed = (allowedModes & (byte)DispenseMode.Throw) != 0;
                    }
                    // If requestedMode is 0 or unknown, modeAllowed remains false

                    if (!modeAllowed)
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    // Check cooldown
                    bool canActivate = false;
                    
                    if (hasCooldownBuffer)
                    {
                        for (int j = 0; j < cooldowns.Length; j++)
                        {
                            if (cooldowns[j].Id == request.Id)
                            {
                                if (cooldowns[j].RemainingSeconds <= 0f && cooldowns[j].ChargesAvailable > 0)
                                {
                                    canActivate = true;
                                    // Reduce charge and set cooldown
                                    var cooldown = cooldowns[j];
                                    if (cooldown.ChargesAvailable > 0)
                                    {
                                        cooldown.ChargesAvailable--;
                                    }
                                    cooldown.RemainingSeconds = spec.BaseCooldownSeconds * configState.GlobalCooldownScale;
                                    cooldowns[j] = cooldown;
                                }
                                break;
                            }
                        }
                    }

                    // If no cooldown entry exists, allow activation and create cooldown
                    if (!canActivate)
                    {
                        bool hasCooldown = false;
                        if (hasCooldownBuffer)
                        {
                            for (int j = 0; j < cooldowns.Length; j++)
                            {
                                if (cooldowns[j].Id == request.Id)
                                {
                                    hasCooldown = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!hasCooldown)
                        {
                            // First time using this miracle - allow activation
                            canActivate = true;
                            if (!hasCooldownBuffer)
                            {
                                // Create cooldown buffer
                                ecb.AddBuffer<MiracleCooldown>(entity);
                                // Track that we need to add cooldown entry after playback
                                if (!pendingCooldowns.ContainsKey(entity))
                                {
                                    pendingCooldowns.Add(entity, new NativeList<MiracleCooldown>(4, Allocator.TempJob));
                                }
                                pendingCooldowns[entity].Add(new MiracleCooldown
                                {
                                    Id = request.Id,
                                    RemainingSeconds = spec.BaseCooldownSeconds * configState.GlobalCooldownScale,
                                    ChargesAvailable = (byte)(spec.MaxCharges - 1)
                                });
                            }
                            else
                            {
                                // Buffer exists but no entry - add it now
                                cooldowns.Add(new MiracleCooldown
                                {
                                    Id = request.Id,
                                    RemainingSeconds = spec.BaseCooldownSeconds * configState.GlobalCooldownScale,
                                    ChargesAvailable = (byte)(spec.MaxCharges - 1)
                                });
                            }
                        }
                    }

                    if (!canActivate)
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    // MVP: Ignore prayer costs (spec has BasePrayerCost but we don't validate it)

                    // Compute charge scaling, if available
                    float normalizedCharge = 0f;
                    bool hasChargeState = _chargeStateLookup.HasComponent(entity);
                    if (hasChargeState && spec.ChargeModel != MiracleChargeModel.None)
                    {
                        var chargeState = _chargeStateLookup[entity];
                        normalizedCharge = math.saturate(chargeState.Charge01);
                    }

                    float radius = ComputeChargedRadius(request.TargetRadius, ref spec, normalizedCharge);
                    float intensity = ComputeChargedIntensity(ref spec, normalizedCharge);
                    float lifetime = spec.BaseDuration > 0f ? spec.BaseDuration : 60f;

                    // Check dispense mode
                    if (request.DispenseMode == (byte)DispenseMode.Sustained)
                    {
                        // Sustained mode: spawn MiracleSustainedEffect entity
                        bool hasChannelState = _channelStateLookup.HasComponent(entity);
                        MiracleChannelState channelState = hasChannelState ? _channelStateLookup[entity] : default;
                        if (hasChannelState && channelState.ActiveEffectEntity != Entity.Null && 
                            SystemAPI.Exists(channelState.ActiveEffectEntity))
                        {
                            // Already channeling, skip (prevent multiple channels)
                            requests.RemoveAt(i);
                            continue;
                        }

                        // Spawn sustained effect entity
                        var sustainedEntity = ecb.CreateEntity();
                        ecb.AddComponent(sustainedEntity, LocalTransform.FromPosition(request.TargetPoint));
                        ecb.AddComponent(sustainedEntity, new MiracleSustainedEffect
                        {
                            Owner = entity,
                            Id = request.Id,
                            TargetPoint = request.TargetPoint,
                            Radius = radius,
                            Intensity = intensity,
                            IsChanneling = 1
                        });

                        // Update channel state on caster
                        if (!hasChannelState)
                        {
                            ecb.AddComponent(entity, new MiracleChannelState
                            {
                                ActiveEffectEntity = sustainedEntity,
                                ChannelingId = request.Id,
                                ChannelStartTime = (float)SystemAPI.Time.ElapsedTime
                            });
                        }
                        else
                        {
                            channelState.ActiveEffectEntity = sustainedEntity;
                            channelState.ChannelingId = request.Id;
                            channelState.ChannelStartTime = (float)SystemAPI.Time.ElapsedTime;
                            ecb.SetComponent(entity, channelState);
                        }
                    }
                    else if (request.DispenseMode == (byte)DispenseMode.Throw)
                    {
                        // Throw mode: spawn MiracleToken entity with physics
                        var tokenEntity = ecb.CreateEntity();

                        // Get hand input for spawn position and aim direction
                        bool hasHandInput = _handInputLookup.HasComponent(entity);
                        DivineHandInput handInput = hasHandInput ? _handInputLookup[entity] : default;
                        float3 spawnPosition;
                        if (hasHandInput)
                        {
                            // Spawn at hand/cursor position (where player is holding/aiming from)
                            spawnPosition = handInput.CursorWorldPosition;
                        }
                        else
                        {
                            // Fallback to target point if no hand input available
                            spawnPosition = request.TargetPoint;
                            // Default to forward direction if no hand input available
                            handInput = new DivineHandInput
                            {
                                AimDirection = new float3(0f, 0f, 1f),
                                CursorWorldPosition = request.TargetPoint
                            };
                        }
                        ecb.AddComponent(tokenEntity, LocalTransform.FromPosition(spawnPosition));

                        // Calculate launch velocity
                        float3 launchVelocity = ComputeThrowVelocity(handInput, ref spec, normalizedCharge);

                        // Add miracle token data
                        ecb.AddComponent(tokenEntity, new PureDOTS.Runtime.Miracles.MiracleToken
                        {
                            Id = request.Id,
                            Owner = entity,
                            Intensity = intensity,
                            Radius = radius,
                            LaunchVelocity = launchVelocity
                        });

                        // Add impact tracking
                        float maxFlightTime = spec.BaseDuration > 0f ? spec.BaseDuration : 10f; // Default 10s timeout
                        ecb.AddComponent(tokenEntity, new MiracleOnImpact
                        {
                            ExplosionRadius = radius,
                            HasImpacted = 0,
                            MaxFlightTime = maxFlightTime,
                            FlightTime = 0f
                        });

                        // Add physics setup (triggers bootstrap)
                        ecb.AddComponent(tokenEntity, new RequiresPhysics
                        {
                            Priority = 0,
                            Flags = PhysicsInteractionFlags.Collidable
                        });

                        ecb.AddComponent(tokenEntity, new PhysicsInteractionConfig
                        {
                            Mass = 1f,
                            CollisionRadius = math.max(0.1f, spec.ThrowCollisionRadius),
                            Restitution = 0f,
                            Friction = 0f,
                            LinearDamping = 0f,
                            AngularDamping = 0f
                        });

                        // Add collision event buffer for impact detection
                        ecb.AddBuffer<PhysicsCollisionEventElement>(tokenEntity);

                        // Physics setup notes:
                        // - PhysicsVelocity will be added by PhysicsBodyBootstrapSystem (runs in InitializationSystemGroup)
                        // - MiracleTokenVelocitySystem (runs in PhysicsSystemGroup before BuildPhysicsWorld) sets initial velocity
                        // - ThrownObjectGravitySystem (runs in PhysicsSystemGroup before BuildPhysicsWorld) applies gravity each frame
                        // - PhysicsGravityFactor is set to 0 by bootstrap, but ThrownObjectGravitySystem applies custom gravity
                        //   regardless (it reads the factor but doesn't mutate it, using a local factor for custom gravity)
                        // - Unity Physics gravity should be disabled (PhysicsStep.Gravity = float3.zero) to prevent double-gravity

                        // Add BeingThrown for flight tracking and gravity application
                        ecb.AddComponent(tokenEntity, new BeingThrown
                        {
                            InitialVelocity = launchVelocity,
                            TimeSinceThrow = 0f,
                            PrevPosition = spawnPosition, // Initialize prevPos for tunneling prevention
                            PrevRotation = quaternion.identity
                        });
                    }
                    else
                    {
                        // Instant mode: spawn MiracleEffectNew entity (existing logic)
                        var effectEntity = ecb.CreateEntity();
                        
                        // Add generic miracle effect component
                        ecb.AddComponent(effectEntity, new MiracleEffectNew
                        {
                            Id = request.Id,
                            RemainingSeconds = lifetime,
                            Intensity = intensity,
                            Origin = request.TargetPoint,
                            Radius = radius
                        });

                        // Add LocalTransform
                        ecb.AddComponent(effectEntity, LocalTransform.FromPosition(request.TargetPoint));

                        // Add miracle-specific components based on ID
                        // This will be extended by game-specific systems (Rain, Temporal Veil, etc.)
                        // For now, we just spawn the generic effect entity
                    }

                    // Reset charge state after consumption
                    if (hasChargeState)
                    {
                        ecb.SetComponent(entity, new MiracleChargeState());
                    }

                    // Remove processed request
                    requests.RemoveAt(i);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            
            // Add cooldown entries for entities that just got buffers created
            _cooldownLookup.Update(ref state);
            foreach (var kvp in pendingCooldowns)
            {
                var entity = kvp.Key;
                var cooldownsToAdd = kvp.Value;
                
                if (_cooldownLookup.HasBuffer(entity))
                {
                    var cooldowns = _cooldownLookup[entity];
                    for (int i = 0; i < cooldownsToAdd.Length; i++)
                    {
                        cooldowns.Add(cooldownsToAdd[i]);
                    }
                }
                
                cooldownsToAdd.Dispose();
            }
            
            pendingCooldowns.Dispose();
        }

        private static float ComputeChargedRadius(float requestedRadius, ref MiracleSpec spec, float normalizedCharge)
        {
            float baseRadius = math.max(0f, spec.BaseRadius);
            float maxRadius = math.max(baseRadius, spec.MaxRadius > 0f ? spec.MaxRadius : baseRadius);
            float clamped = math.clamp(requestedRadius, baseRadius, maxRadius);
            float chargedMultiplier = math.lerp(1f, spec.RadiusChargeMultiplier, normalizedCharge);
            float charged = clamped * chargedMultiplier;
            float absoluteMax = maxRadius * spec.RadiusChargeMultiplier;
            return math.clamp(charged, baseRadius, absoluteMax);
        }

        private static float ComputeChargedIntensity(ref MiracleSpec spec, float normalizedCharge)
        {
            float baseStrength = math.max(0f, spec.BaseStrength);
            float chargedMultiplier = math.lerp(1f, spec.StrengthChargeMultiplier, normalizedCharge);
            return baseStrength * chargedMultiplier;
        }

        private static float3 ComputeThrowVelocity(
            in DivineHandInput handInput,
            ref MiracleSpec spec,
            float normalizedCharge)
        {
            // Base direction from aim
            float3 aimDir = math.normalizesafe(handInput.AimDirection, new float3(0f, 0f, 1f));
            
            // Speed calculation
            float baseSpeed = math.max(0.1f, spec.ThrowSpeedBase);
            float chargeMultiplier = math.lerp(1f, spec.ThrowSpeedChargeMultiplier, normalizedCharge);
            float speed = baseSpeed * chargeMultiplier;
            
            // Base velocity
            float3 velocity = aimDir * speed;
            
            // Add arc boost (upward component)
            float arcBoost = spec.ThrowArcBoost;
            if (arcBoost > 0f)
            {
                // Add upward component, stronger when aiming forward
                float forwardComponent = math.max(0f, aimDir.z); // Assuming Z is forward
                velocity.y += arcBoost * forwardComponent;
            }
            
            return velocity;
        }
    }
}
