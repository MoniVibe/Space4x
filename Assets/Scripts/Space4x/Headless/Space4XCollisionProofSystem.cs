using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof that carriers never penetrate asteroids below summed radii.
    /// Logs exactly one PASS/FAIL line and can request exit when configured.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XCollisionProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_COLLISION_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_COLLISION_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string CollisionScenarioFile = "space4x_collision_micro.json";
        private const uint DefaultTimeoutTicks = 600;
        private const float PenetrationEpsilon = 0.05f;
        private static readonly FixedString64Bytes CollisionTestId = new FixedString64Bytes("S0.SPACE4X_COLLISION_MICRO");

        private byte _enabled;
        private byte _done;
        private byte _bankResolved;
        private byte _bankLogged;
        private FixedString64Bytes _bankTestId;
        private uint _startTick;
        private uint _timeoutTick;
        private byte _profileReady;
        private float _carrierRadius;
        private float _asteroidRadius;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Carrier>();
            state.RequireForUpdate<Asteroid>();
            state.RequireForUpdate<LocalTransform>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _done != 0)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (ResolveBankTestId() == false)
            {
                return;
            }

            if (_timeoutTick == 0)
            {
                _startTick = timeState.Tick;
                _timeoutTick = _startTick + DefaultTimeoutTicks;
            }

            EnsureProfileRadii(ref state);

            var hasCarrier = false;
            var hasAsteroid = false;
            var minAllowed = _carrierRadius + _asteroidRadius - PenetrationEpsilon;
            var minAllowedSq = minAllowed * minAllowed;

            using var asteroidPositions = new NativeList<float3>(Allocator.Temp);
            foreach (var (_, transform) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithNone<Prefab>())
            {
                hasAsteroid = true;
                asteroidPositions.Add(transform.ValueRO.Position);
            }

            foreach (var (_, transform) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithNone<Prefab>())
            {
                hasCarrier = true;
                var carrierPos = transform.ValueRO.Position;
                for (int i = 0; i < asteroidPositions.Length; i++)
                {
                    var delta = carrierPos - asteroidPositions[i];
                    if (math.lengthsq(delta) < minAllowedSq)
                    {
                        Fail(ref state, timeState.Tick, "penetration", carrierPos, asteroidPositions[i]);
                        return;
                    }
                }
            }

            if (timeState.Tick < _timeoutTick)
            {
                return;
            }

            if (_profileReady == 0 || !hasCarrier || !hasAsteroid)
            {
                Fail(ref state, timeState.Tick, "missing", float3.zero, float3.zero);
                return;
            }

            Pass(ref state, timeState.Tick, hasCarrier, hasAsteroid);
        }

        private void EnsureProfileRadii(ref SystemState state)
        {
            if (_profileReady != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PhysicsColliderProfileComponent>(out var profileComponent) ||
                !profileComponent.Profile.IsCreated)
            {
                return;
            }

            ref var entries = ref profileComponent.Profile.Value.Entries;
            if (!PhysicsColliderProfileHelpers.TryGetSpec(ref entries, Space4XRenderKeys.Carrier, out var carrierSpec))
            {
                return;
            }

            if (!PhysicsColliderProfileHelpers.TryGetSpec(ref entries, Space4XRenderKeys.Asteroid, out var asteroidSpec))
            {
                return;
            }

            _carrierRadius = ResolveRadius(carrierSpec);
            _asteroidRadius = ResolveRadius(asteroidSpec);
            if (_carrierRadius <= 0f || _asteroidRadius <= 0f)
            {
                return;
            }

            _profileReady = 1;
        }

        private static float ResolveRadius(in PhysicsColliderSpec spec)
        {
            return spec.Shape switch
            {
                PhysicsColliderShape.Box => math.cmax(spec.Dimensions) * 0.5f,
                PhysicsColliderShape.Capsule => spec.Dimensions.x,
                _ => spec.Dimensions.x
            };
        }

        private void Pass(ref SystemState state, uint tick, bool hasCarrier, bool hasAsteroid)
        {
            _done = 1;
            UnityDebug.Log($"[Space4XCollisionProof] PASS tick={tick} carriers={(hasCarrier ? 1 : 0)} asteroids={(hasAsteroid ? 1 : 0)} eps={PenetrationEpsilon:F2}");
            LogBankResult(ref state, true, "pass", tick);
            ExitIfRequested(ref state, tick, 0);
        }

        private void Fail(ref SystemState state, uint tick, string reason, float3 carrierPos, float3 asteroidPos)
        {
            _done = 1;
            UnityDebug.LogError($"[Space4XCollisionProof] FAIL tick={tick} reason={reason} carrier={carrierPos} asteroid={asteroidPos} eps={PenetrationEpsilon:F2}");
            LogBankResult(ref state, false, reason, tick);
            ExitIfRequested(ref state, tick, 4);
        }

        private static void ExitIfRequested(ref SystemState state, uint tick, int exitCode)
        {
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }

        private bool ResolveBankTestId()
        {
            if (_bankResolved != 0)
            {
                return !_bankTestId.IsEmpty;
            }

            _bankResolved = 1;
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            if (scenarioPath.EndsWith(CollisionScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = CollisionTestId;
                return true;
            }

            _enabled = 0;
            return false;
        }

        private void LogBankResult(ref SystemState state, bool pass, string reason, uint tick)
        {
            if (_bankLogged != 0 || _bankTestId.IsEmpty)
            {
                return;
            }

            ResolveTickInfo(ref state, tick, out var tickTime, out var scenarioTick);
            var delta = (int)tickTime - (int)scenarioTick;
            _bankLogged = 1;

            if (pass)
            {
                UnityDebug.Log($"BANK:{_bankTestId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{_bankTestId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
        }

        private void ResolveTickInfo(ref SystemState state, uint tick, out uint tickTime, out uint scenarioTick)
        {
            tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
        }
    }
}
