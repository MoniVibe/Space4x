using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Deception;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Social;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Deception
{
    /// <summary>
    /// Converts detected lies (from CommsInboxEntry) into suspicion/exposure state + interrupts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InterruptSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Interrupts.InterruptHandlerSystem))]
    public partial struct DeceptionFromCommsSystem : ISystem
    {
        private ComponentLookup<DisguiseIdentity> _disguiseLookup;
        private ComponentLookup<PersonalityAxes> _personalityLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<AIBehaviorProfile> _profileLookup;
        private ComponentLookup<DeceptionResponsePolicy> _policyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DeceptionObserverConfig>();

            _disguiseLookup = state.GetComponentLookup<DisguiseIdentity>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _profileLookup = state.GetComponentLookup<AIBehaviorProfile>(true);
            _policyLookup = state.GetComponentLookup<DeceptionResponsePolicy>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _disguiseLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _policyLookup.Update(ref state);

            foreach (var (observerConfig, inbox, discovery, interrupts, observer) in SystemAPI.Query<
                         RefRO<DeceptionObserverConfig>,
                         DynamicBuffer<CommsInboxEntry>,
                         DynamicBuffer<DisguiseDiscovery>,
                         DynamicBuffer<Interrupt>>().WithEntityAccess())
            {
                if (observerConfig.ValueRO.Enabled == 0)
                {
                    continue;
                }

                var inboxBuffer = inbox;
                var discoveryBuffer = discovery;
                var interruptBuffer = interrupts;

                DecaySuspicion(ref discoveryBuffer, observerConfig.ValueRO, time.Tick);

                for (int i = 0; i < inboxBuffer.Length; i++)
                {
                    var entry = inboxBuffer[i];
                    if (entry.WasProcessed != 0 || entry.WasDeceptionDetected == 0)
                    {
                        continue;
                    }

                    var target = entry.Sender;
                    if (target == Entity.Null)
                    {
                        entry.WasProcessed = 1;
                        inboxBuffer[i] = entry;
                        continue;
                    }

                    var suspicionDelta = math.saturate(observerConfig.ValueRO.SuspicionGainOnDetectedLie) *
                                         math.saturate(1f - entry.Integrity01);

                    var index = FindOrAdd(ref discoveryBuffer, observerConfig.ValueRO.MaxTracked, target, time.Tick);
                    if (index < 0)
                    {
                        entry.WasProcessed = 1;
                        inboxBuffer[i] = entry;
                        continue;
                    }

                    var d = discoveryBuffer[index];
                    d.Suspicion01 = math.saturate(d.Suspicion01 + suspicionDelta);
                    d.LastUpdateTick = time.Tick;

                    var wasExposed = d.IsExposed != 0;
                    var shouldExpose = false;

                    if (_disguiseLookup.HasComponent(target) && _disguiseLookup[target].IsActive != 0)
                    {
                        var disguise = _disguiseLookup[target];
                        var exposeThreshold = math.saturate(observerConfig.ValueRO.ExposeThresholdBase01 + disguise.DisguiseQuality01 * 0.25f);
                        shouldExpose = d.Suspicion01 >= exposeThreshold;
                    }

                    if (!wasExposed && shouldExpose)
                    {
                        d.IsExposed = 1;
                    }

                    discoveryBuffer[index] = d;

                    var policy = _policyLookup.HasComponent(observer)
                        ? _policyLookup[observer]
                        : DeceptionResponsePolicy.Default;

                    var hint = ChooseHint(observer, d.Suspicion01, d.IsExposed != 0, policy);

                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.LieDetected,
                        InterruptPriority.Normal,
                        target,
                        time.Tick,
                        targetEntity: target,
                        payloadValue: d.Suspicion01,
                        payloadId: DeceptionPayloads.HintToPayload(hint));

                    if (!wasExposed && d.IsExposed != 0)
                    {
                        InterruptUtils.Emit(
                            ref interruptBuffer,
                            InterruptType.IdentityExposed,
                            InterruptPriority.High,
                            target,
                            time.Tick,
                            targetEntity: target,
                            payloadValue: d.Suspicion01,
                            payloadId: (FixedString32Bytes)"identity.exposed");
                    }

                    entry.WasProcessed = 1;
                    inboxBuffer[i] = entry;
                }
            }
        }

        private static void DecaySuspicion(ref DynamicBuffer<DisguiseDiscovery> discovery, in DeceptionObserverConfig config, uint tick)
        {
            var decay = math.max(0f, config.SuspicionDecayPerTick);
            if (decay <= 0f)
            {
                return;
            }

            for (int i = discovery.Length - 1; i >= 0; i--)
            {
                var d = discovery[i];
                if (d.IsExposed != 0)
                {
                    continue;
                }

                var age = tick > d.LastUpdateTick ? tick - d.LastUpdateTick : 0u;
                if (age == 0)
                {
                    continue;
                }

                d.Suspicion01 = math.max(0f, d.Suspicion01 - decay * age);
                discovery[i] = d;
            }
        }

        private static int FindOrAdd(ref DynamicBuffer<DisguiseDiscovery> discovery, byte maxTracked, Entity target, uint tick)
        {
            for (int i = 0; i < discovery.Length; i++)
            {
                if (discovery[i].TargetEntity == target)
                {
                    return i;
                }
            }

            if (maxTracked == 0)
            {
                return -1;
            }

            if (discovery.Length < maxTracked)
            {
                discovery.Add(new DisguiseDiscovery
                {
                    TargetEntity = target,
                    Suspicion01 = 0f,
                    IsExposed = 0,
                    LastUpdateTick = tick
                });
                return discovery.Length - 1;
            }

            // Evict least suspicious (bounded, deterministic).
            var bestIndex = 0;
            var bestSuspicion = discovery[0].Suspicion01;
            for (int i = 1; i < discovery.Length; i++)
            {
                if (discovery[i].Suspicion01 < bestSuspicion)
                {
                    bestIndex = i;
                    bestSuspicion = discovery[i].Suspicion01;
                }
            }

            discovery[bestIndex] = new DisguiseDiscovery
            {
                TargetEntity = target,
                Suspicion01 = 0f,
                IsExposed = 0,
                LastUpdateTick = tick
            };
            return bestIndex;
        }

        private LieOutcomeHint ChooseHint(Entity observer, float suspicion01, bool exposed, in DeceptionResponsePolicy policy)
        {
            if (exposed)
            {
                if (suspicion01 >= policy.PublicCalloutThreshold01)
                {
                    return LieOutcomeHint.PublicCallout;
                }
                if (suspicion01 >= policy.PrivateCalloutThreshold01)
                {
                    return LieOutcomeHint.PrivateCallout;
                }
                return policy.OnIdentityExposed;
            }

            if (suspicion01 >= policy.PrivateCalloutThreshold01)
            {
                return LieOutcomeHint.PrivateCallout;
            }

            // Heuristic fallback if no explicit policy: stubborn & bold tends to call out.
            if (_personalityLookup.HasComponent(observer))
            {
                var p = _personalityLookup[observer];
                var bold01 = math.saturate((p.Boldness + 1f) * 0.5f);
                var convict01 = math.saturate((p.Conviction + 1f) * 0.5f);
                if (bold01 > 0.75f && convict01 > 0.75f && suspicion01 > 0.6f)
                {
                    return LieOutcomeHint.PublicCallout;
                }
                if (convict01 > 0.75f && suspicion01 > 0.55f)
                {
                    return LieOutcomeHint.PrivateCallout;
                }
                if (bold01 < 0.25f && suspicion01 > 0.5f)
                {
                    return LieOutcomeHint.PlayAlong;
                }
            }

            return policy.OnLieDetected;
        }
    }
}





