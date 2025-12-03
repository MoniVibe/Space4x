using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;

namespace Space4X.Time
{
    /// <summary>
    /// Compatibility layer providing a static Time facade for Space4X.
    /// Routes to TimeHelpers / TickTimeState to provide Unity.Time-like API.
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// The time in seconds it took to complete the last frame (read only).
        /// Routes to TimeHelpers.GetGlobalDelta.
        /// </summary>
        public static float deltaTime
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.deltaTime; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter &&
                    !entityManager.CreateEntityQuery(typeof(TickTimeState)).IsEmptyIgnoreFilter)
                {
                    var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                    var tickTimeState = entityManager.CreateEntityQuery(typeof(TickTimeState)).GetSingleton<TickTimeState>();
                    return TimeHelpers.GetGlobalDelta(tickTimeState, timeState);
                }

                return UnityEngine.Time.deltaTime; // Fallback
            }
        }

        /// <summary>
        /// The time in seconds it took to complete the last frame, ignoring time scale (read only).
        /// Routes to TimeHelpers.GetGlobalDelta with time scale = 1.
        /// </summary>
        public static float unscaledDeltaTime
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.unscaledDeltaTime; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TickTimeState)).IsEmptyIgnoreFilter)
                {
                    var tickTimeState = entityManager.CreateEntityQuery(typeof(TickTimeState)).GetSingleton<TickTimeState>();
                    return tickTimeState.FixedDeltaTime;
                }

                return UnityEngine.Time.unscaledDeltaTime; // Fallback
            }
        }

        /// <summary>
        /// The scale at which time is passing (read only).
        /// Routes to TimeState.CurrentSpeedMultiplier.
        /// </summary>
        public static float timeScale
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.timeScale; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
                {
                    var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                    return timeState.CurrentSpeedMultiplier;
                }

                return UnityEngine.Time.timeScale; // Fallback
            }
        }

        /// <summary>
        /// The time at the beginning of this frame (read only).
        /// Routes to TimeState.Tick converted to seconds.
        /// </summary>
        public static float time
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.time; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
                {
                    var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                    return timeState.Tick * timeState.FixedDeltaTime;
                }

                return UnityEngine.Time.time; // Fallback
            }
        }

        /// <summary>
        /// The time the latest frame started (read only).
        /// Routes to TimeState.Tick converted to seconds.
        /// </summary>
        public static float fixedTime
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.fixedTime; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
                {
                    var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                    return timeState.Tick * timeState.FixedDeltaTime;
                }

                return UnityEngine.Time.fixedTime; // Fallback
            }
        }

        /// <summary>
        /// The interval in seconds at which physics and other fixed frame rate updates are performed (read only).
        /// Routes to TimeState.FixedDeltaTime.
        /// </summary>
        public static float fixedDeltaTime
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return UnityEngine.Time.fixedDeltaTime; // Fallback to Unity time
                }

                var entityManager = world.EntityManager;
                if (!entityManager.CreateEntityQuery(typeof(TimeState)).IsEmptyIgnoreFilter)
                {
                    var timeState = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>();
                    return timeState.FixedDeltaTime;
                }

                return UnityEngine.Time.fixedDeltaTime; // Fallback
            }
        }
    }
}


