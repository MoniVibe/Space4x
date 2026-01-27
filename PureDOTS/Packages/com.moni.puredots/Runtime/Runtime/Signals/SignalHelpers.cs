using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Signals
{
    /// <summary>
    /// Static helpers for signal propagation and decay calculations.
    /// </summary>
    [BurstCompile]
    public static class SignalHelpers
    {
        /// <summary>
        /// Broadcasts a signal from a position.
        /// </summary>
        public static Signal BroadcastSignal(
            SignalType type,
            SignalPriority priority,
            float3 origin,
            float range,
            float strength,
            float duration,
            Entity source,
            uint currentTick)
        {
            return new Signal
            {
                Type = type,
                Priority = priority,
                Propagation = PropagationMode.Radial,
                Origin = origin,
                Direction = float3.zero,
                Range = range,
                CurrentRange = 0,
                Strength = strength,
                DecayRate = strength / math.max(1f, duration),
                ExpansionRate = range / math.max(1f, duration),
                EmittedTick = currentTick,
                ExpirationTick = currentTick + (uint)duration,
                SourceEntity = source,
                IsActive = 1
            };
        }

        /// <summary>
        /// Propagates signal (expands range, decays strength).
        /// </summary>
        public static void PropagateSignal(
            ref Signal signal,
            float deltaTime,
            uint currentTick)
        {
            if (signal.IsActive == 0)
                return;

            // Expand range
            signal.CurrentRange = math.min(
                signal.Range,
                signal.CurrentRange + signal.ExpansionRate * deltaTime);

            // Decay strength
            signal.Strength = math.max(0, signal.Strength - signal.DecayRate * deltaTime);

            // Check expiration
            if (currentTick >= signal.ExpirationTick || signal.Strength <= 0)
            {
                signal.IsActive = 0;
            }
        }

        /// <summary>
        /// Decays signal strength over time.
        /// </summary>
        public static float DecaySignal(
            float currentStrength,
            float decayRate,
            float deltaTime)
        {
            return math.max(0, currentStrength - decayRate * deltaTime);
        }

        /// <summary>
        /// Checks if entity is in signal range.
        /// </summary>
        public static bool IsInSignalRange(
            float3 receiverPosition,
            in Signal signal,
            out float receivedStrength)
        {
            receivedStrength = 0;

            if (signal.IsActive == 0)
                return false;

            float distance = math.length(receiverPosition - signal.Origin);

            // Check if within current expansion
            if (distance > signal.CurrentRange)
                return false;

            // Check propagation mode
            if (signal.Propagation == PropagationMode.Directional)
            {
                float3 toReceiver = math.normalizesafe(receiverPosition - signal.Origin);
                float dot = math.dot(toReceiver, signal.Direction);
                if (dot < 0.5f) // ~60 degree cone
                    return false;
            }

            // Calculate received strength (inverse square falloff)
            float falloff = 1f - (distance / math.max(1f, signal.Range));
            receivedStrength = signal.Strength * falloff * falloff;

            return receivedStrength > 0.01f;
        }

        /// <summary>
        /// Receives a signal if in range.
        /// </summary>
        public static bool TryReceiveSignal(
            float3 receiverPosition,
            in Signal signal,
            in SignalReceiver receiver,
            uint currentTick,
            out ReceivedSignal received)
        {
            received = default;

            if (receiver.IsJammed != 0)
                return false;

            // Check type permissions
            if (!CanReceiveType(signal.Type, receiver))
                return false;

            if (!IsInSignalRange(receiverPosition, signal, out float strength))
                return false;

            // Apply receiver sensitivity
            strength *= receiver.Sensitivity;

            received = new ReceivedSignal
            {
                SignalEntity = Entity.Null, // Set by caller
                Type = signal.Type,
                Priority = signal.Priority,
                Origin = signal.Origin,
                Strength = strength,
                Distance = math.length(receiverPosition - signal.Origin),
                ReceivedTick = currentTick,
                PayloadId = signal.PayloadId,
                WasProcessed = 0
            };

            return true;
        }

        /// <summary>
        /// Checks if receiver can receive signal type.
        /// </summary>
        private static bool CanReceiveType(SignalType type, in SignalReceiver receiver)
        {
            return type switch
            {
                SignalType.Distress => receiver.CanReceiveDistress != 0,
                SignalType.Rally => receiver.CanReceiveRally != 0,
                SignalType.Alert => receiver.CanReceiveAlert != 0,
                SignalType.Message => receiver.CanReceiveMessage != 0,
                _ => true // Allow other types by default
            };
        }

        /// <summary>
        /// Calculates signal jamming effect.
        /// </summary>
        public static float CalculateJammingEffect(
            float3 signalPosition,
            float3 jammerPosition,
            in SignalJammer jammer,
            SignalType signalType)
        {
            if (jammer.IsActive == 0)
                return 0;

            if (jammer.JamAllTypes == 0 && jammer.TargetType != signalType)
                return 0;

            float distance = math.length(signalPosition - jammerPosition);
            if (distance > jammer.JamRadius)
                return 0;

            float falloff = 1f - (distance / jammer.JamRadius);
            return jammer.JamStrength * falloff;
        }

        /// <summary>
        /// Relays signal through beacon.
        /// </summary>
        public static Signal RelaySignal(
            in Signal original,
            in Beacon beacon,
            float3 beaconPosition,
            uint currentTick)
        {
            var relayed = original;
            relayed.Origin = beaconPosition;
            relayed.CurrentRange = 0;
            relayed.EmittedTick = currentTick;
            relayed.Range = beacon.RelayRange;

            // Apply amplification
            if (beacon.CanAmplify != 0)
            {
                relayed.Strength = math.min(1f, relayed.Strength * beacon.SignalBoost);
            }

            // Extend expiration
            float remainingDuration = original.ExpirationTick - currentTick;
            relayed.ExpirationTick = currentTick + (uint)remainingDuration;

            return relayed;
        }

        /// <summary>
        /// Updates alert state based on received signals.
        /// </summary>
        public static void UpdateAlertState(
            ref AlertState alert,
            in DynamicBuffer<ReceivedSignal> signals,
            float3 entityPosition,
            uint currentTick)
        {
            // Find highest priority threat
            byte maxAlert = 0;
            float closestThreat = float.MaxValue;
            float3 threatDir = float3.zero;

            for (int i = 0; i < signals.Length; i++)
            {
                var sig = signals[i];
                if (sig.WasProcessed != 0)
                    continue;

                byte alertLevel = GetAlertLevel(sig.Type, sig.Priority);
                if (alertLevel > maxAlert)
                {
                    maxAlert = alertLevel;
                }

                if (sig.Type == SignalType.Alert || sig.Type == SignalType.Combat ||
                    sig.Type == SignalType.Distress)
                {
                    if (sig.Distance < closestThreat)
                    {
                        closestThreat = sig.Distance;
                        threatDir = math.normalizesafe(sig.Origin - entityPosition);
                    }
                }
            }

            if (maxAlert > alert.AlertLevel)
            {
                alert.AlertLevel = maxAlert;
                alert.AlertStartTick = currentTick;
                alert.ThreatDirection = threatDir;
                alert.ThreatDistance = closestThreat;
            }

            // Decay alert over time
            uint ticksSinceAlert = currentTick - alert.AlertStartTick;
            if (ticksSinceAlert > 1000 && alert.AlertLevel > 0)
            {
                alert.AlertLevel = (byte)math.max(0, alert.AlertLevel - 1);
                alert.AlertDecayTick = currentTick;
            }
        }

        /// <summary>
        /// Gets alert level for signal type.
        /// </summary>
        private static byte GetAlertLevel(SignalType type, SignalPriority priority)
        {
            byte baseLevel = type switch
            {
                SignalType.Combat => 200,
                SignalType.Distress => 150,
                SignalType.Alert => 100,
                SignalType.Retreat => 80,
                SignalType.Rally => 50,
                _ => 25
            };

            byte priorityBonus = (byte)((int)priority * 10);
            return (byte)math.min(255, baseLevel + priorityBonus);
        }

        /// <summary>
        /// Processes queued messages for delivery.
        /// </summary>
        public static void ProcessMessageQueue(
            ref DynamicBuffer<QueuedMessage> queue,
            uint currentTick)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                var msg = queue[i];
                if (msg.IsDelivered != 0)
                    continue;

                if (currentTick >= msg.DeliveryTick)
                {
                    msg.IsDelivered = 1;
                    queue[i] = msg;
                }
            }

            // Remove old delivered messages
            for (int i = queue.Length - 1; i >= 0; i--)
            {
                if (queue[i].IsDelivered != 0)
                {
                    uint age = currentTick - queue[i].DeliveryTick;
                    if (age > 1000) // Keep for 1000 ticks
                    {
                        queue.RemoveAtSwapBack(i);
                    }
                }
            }
        }

        /// <summary>
        /// Creates default signal receiver.
        /// </summary>
        public static SignalReceiver CreateDefaultReceiver()
        {
            return new SignalReceiver
            {
                Sensitivity = 1f,
                MaxRange = 100f,
                CanReceiveDistress = 1,
                CanReceiveRally = 1,
                CanReceiveAlert = 1,
                CanReceiveMessage = 1,
                IsJammed = 0
            };
        }

        /// <summary>
        /// Creates default signal emitter.
        /// </summary>
        public static SignalEmitter CreateDefaultEmitter()
        {
            return new SignalEmitter
            {
                MaxRange = 50f,
                BaseStrength = 1f,
                Cooldown = 100f,
                LastEmissionTick = 0,
                CanBroadcastDistress = 1,
                CanBroadcastRally = 0,
                CanBroadcastAlert = 1,
                IsEnabled = 1
            };
        }

        /// <summary>
        /// Gets signal type name.
        /// </summary>
        public static FixedString32Bytes GetSignalTypeName(SignalType type)
        {
            return type switch
            {
                SignalType.Distress => "Distress",
                SignalType.Rally => "Rally",
                SignalType.Alert => "Alert",
                SignalType.Message => "Message",
                SignalType.Beacon => "Beacon",
                SignalType.Trade => "Trade",
                SignalType.Discovery => "Discovery",
                SignalType.Combat => "Combat",
                SignalType.Retreat => "Retreat",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Calculates network path through beacons.
        /// </summary>
        public static bool HasNetworkPath(
            in DynamicBuffer<BeaconConnection> connections,
            Entity targetBeacon)
        {
            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i].ConnectedBeacon == targetBeacon &&
                    connections[i].IsActive != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

