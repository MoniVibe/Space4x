using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems.Debug
{
    /// <summary>
    /// Debug system that logs movement tag information for entities.
    /// Runs once per second to help verify that entities are classified correctly.
    /// Only active in editor/debug builds.
    /// </summary>
#if UNITY_EDITOR
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct MovementPolicyDebugSystem : ISystem
    {
        private const string EnableEnvVar = "PUREDOTS_MOVEMENT_POLICY_DEBUG";
        private uint _lastLogTick;
        private const uint LogIntervalTicks = 60; // Log once per second at 60 TPS

        [BurstDiscard]
        public void OnCreate(ref SystemState state)
        {
            if (Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            if (!IsLoggingEnabled())
            {
                state.Enabled = false;
                return;
            }

            _lastLogTick = 0;
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<TimeState>())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Log once per second
            if (currentTick - _lastLogTick < LogIntervalTicks)
            {
                return;
            }

            _lastLogTick = currentTick;

            // Count entities by movement tag
            int groundCount = 0;
            int flyingCount = 0;
            int spaceCount = 0;
            int untaggedCount = 0;

            var groundQuery = SystemAPI.QueryBuilder()
                .WithAll<GroundMovementTag, LocalTransform>()
                .Build();
            groundCount = groundQuery.CalculateEntityCount();

            var flyingQuery = SystemAPI.QueryBuilder()
                .WithAll<FlyingMovementTag, LocalTransform>()
                .Build();
            flyingCount = flyingQuery.CalculateEntityCount();

            var spaceQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceMovementTag, LocalTransform>()
                .Build();
            spaceCount = spaceQuery.CalculateEntityCount();

            // Count entities with LocalTransform but no movement tag
            var allTransformQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform>()
                .WithNone<GroundMovementTag, FlyingMovementTag, SpaceMovementTag>()
                .Build();
            untaggedCount = allTransformQuery.CalculateEntityCount();

            // Log summary
            if (groundCount > 0 || flyingCount > 0 || spaceCount > 0 || untaggedCount > 0)
            {
                UnityEngine.Debug.Log($"[MovementPolicyDebug] Movement tags - Ground: {groundCount} (green), Flying: {flyingCount} (yellow), Space: {spaceCount} (blue), Untagged: {untaggedCount} (gray)");
            }
        }

        [BurstDiscard]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool IsLoggingEnabled()
        {
            var value = global::System.Environment.GetEnvironmentVariable(EnableEnvVar);
            return IsTruthy(value);
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", System.StringComparison.OrdinalIgnoreCase);
        }
    }
#else
    // Disabled in non-editor builds
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MovementPolicyDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }
    }
#endif

    /// <summary>
    /// Authoring component to enable movement policy debug visualization.
    /// Add this to a GameObject in the scene to enable debug logging.
    /// </summary>
#if UNITY_EDITOR
    [DisallowMultipleComponent]
    public sealed class MovementPolicyDebugAuthoring : MonoBehaviour
    {
        [Tooltip("Enable debug logging for movement tags")]
        public bool enableDebugLogging = true;

        [Tooltip("Log interval in seconds")]
        [Range(0.1f, 10f)]
        public float logIntervalSeconds = 1f;
    }
#endif
}
