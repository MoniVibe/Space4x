using System;
using System.Diagnostics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;
using UnityTime = UnityEngine.Time;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessForceExitSystem : ISystem
    {
        private const string ForceExitSecondsEnv = "SPACE4X_HEADLESS_FORCE_EXIT_SECONDS";
        private const string ForceExitHardEnv = "SPACE4X_HEADLESS_FORCE_EXIT_HARD";
        private float _forceExitSeconds;
        private float _exitRequestedAt;
        private int _exitCode;
        private byte _armed;
        private byte _forceKill;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            _forceExitSeconds = ResolveForceExitSeconds();
            _forceKill = ResolveForceExitHard() ? (byte)1 : (byte)0;
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

                _exitRequestedAt = UnityTime.realtimeSinceStartup;
                _exitCode = request.ExitCode;
                _armed = 1;
                UnityDebug.Log($"[Space4XHeadlessForceExit] Exit requested; forcing after {_forceExitSeconds:0.##}s if still running.");
                return;
            }

            if (UnityTime.realtimeSinceStartup - _exitRequestedAt < _forceExitSeconds)
            {
                return;
            }

            UnityDebug.Log($"[Space4XHeadlessForceExit] Forcing process exit (code={_exitCode}) after {_forceExitSeconds:0.##}s grace.");
            if (_forceKill != 0)
            {
                Process.GetCurrentProcess().Kill();
                return;
            }

            SystemEnv.Exit(_exitCode);
        }

        private static float ResolveForceExitSeconds()
        {
            var value = SystemEnv.GetEnvironmentVariable(ForceExitSecondsEnv);
            return float.TryParse(value, out var seconds) && seconds > 0f ? seconds : 0f;
        }

        private static bool ResolveForceExitHard()
        {
            var value = SystemEnv.GetEnvironmentVariable(ForceExitHardEnv);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
