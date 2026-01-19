using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessForceExitSystem : ISystem
    {
        private const string ForceExitSecondsEnv = "SPACE4X_HEADLESS_FORCE_EXIT_SECONDS";
        private float _forceExitSeconds;
        private float _exitRequestedAt;
        private int _exitCode;
        private byte _armed;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            _forceExitSeconds = ResolveForceExitSeconds();
            if (_forceExitSeconds <= 0f)
            {
                state.Enabled = false;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_forceExitSeconds <= 0f)
            {
                return;
            }

            if (_armed == 0)
            {
                if (!SystemAPI.TryGetSingleton(out HeadlessExitRequest request))
                {
                    return;
                }

                _exitRequestedAt = Time.realtimeSinceStartup;
                _exitCode = request.ExitCode;
                _armed = 1;
                UnityDebug.Log($"[Space4XHeadlessForceExit] Exit requested; forcing after {_forceExitSeconds:0.##}s if still running.");
                return;
            }

            if (Time.realtimeSinceStartup - _exitRequestedAt < _forceExitSeconds)
            {
                return;
            }

            UnityDebug.Log($"[Space4XHeadlessForceExit] Forcing process exit (code={_exitCode}) after {_forceExitSeconds:0.##}s grace.");
            Environment.Exit(_exitCode);
        }

        private static float ResolveForceExitSeconds()
        {
            var value = Environment.GetEnvironmentVariable(ForceExitSecondsEnv);
            return float.TryParse(value, out var seconds) && seconds > 0f ? seconds : 0f;
        }
    }
}
