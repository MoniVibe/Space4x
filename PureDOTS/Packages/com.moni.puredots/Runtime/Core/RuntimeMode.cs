using System;
using Unity.Burst;
using UnityEngine;

namespace PureDOTS.Runtime.Core
{
    /// <summary>
    /// Shared runtime helpers for checking execution environment (editor/headless/server).
    /// </summary>
    public static class RuntimeMode
    {
        private const string HeadlessEnvVar = "PUREDOTS_HEADLESS";
        private const string NoGraphicsEnvVar = "PUREDOTS_NOGRAPHICS";
        private const string ForceRenderEnvVar = "PUREDOTS_FORCE_RENDER";
        private const string LegacyRenderingEnvVar = "PUREDOTS_RENDERING";
        private const string HeadlessPresentationEnvVar = "PUREDOTS_HEADLESS_PRESENTATION";

        public struct Flags
        {
            public byte HeadlessRequested;
            public byte ForceRender;
            public byte RenderingEnabled;
            public byte BatchMode;
        }

        private struct RuntimeModeKey
        {
        }

        private static readonly SharedStatic<Flags> s_flags =
            SharedStatic<Flags>.GetOrCreate<RuntimeModeKey>();
        private static readonly SharedStatic<byte> s_editorOverrideEnabled =
            SharedStatic<byte>.GetOrCreate<EditorOverrideContext, EditorOverrideKey>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void InitializeRuntimeFlags()
        {
            RefreshFromEnvironment();
            LogState("Init");
        }

        /// <summary>
        /// True when running in batch mode.
        /// </summary>
        public static bool IsBatchMode => s_flags.Data.BatchMode != 0;

        /// <summary>
        /// True when headless execution is explicitly requested.
        /// </summary>
        public static bool HeadlessRequested => s_flags.Data.HeadlessRequested != 0;

        /// <summary>
        /// True when running in headless/server contexts (legacy compatibility).
        /// </summary>
        public static bool IsHeadless => HeadlessRequested;

        /// <summary>
        /// True when presentation/rendering systems should be active.
        /// </summary>
        public static bool IsRenderingEnabled
        {
            get
            {
                if (s_flags.Data.RenderingEnabled != 0)
                {
                    return true;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (s_editorOverrideEnabled.Data != 0 && !IsBatchMode)
                {
                    return true;
                }
#endif

                return false;
            }
        }

        public static void ForceRenderingEnabled(bool enabled, string reason = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsBatchMode)
            {
                LogState("EditorSmokeOverrideIgnored");
                return;
            }

            s_editorOverrideEnabled.Data = (byte)(enabled ? 1 : 0);
            LogState(string.IsNullOrWhiteSpace(reason) ? "EditorSmokeOverride" : reason);
#else
            _ = enabled;
            _ = reason;
#endif
        }

        // Managed-only initializer (call from a NON-burst system)
        public static void RefreshFromEnvironment()
        {
            bool isBatchMode = Application.isBatchMode;
            bool headless = isBatchMode || EnvIsSet(HeadlessEnvVar);
            bool noGraphics = EnvIsSet(NoGraphicsEnvVar);
            bool forceRender = EnvIsSet(ForceRenderEnvVar) || EnvIsSet(LegacyRenderingEnvVar);
            bool rendering = forceRender || (!headless && !noGraphics);

            s_flags.Data = new Flags
            {
                HeadlessRequested = (byte)(headless ? 1 : 0),
                ForceRender = (byte)(forceRender ? 1 : 0),
                RenderingEnabled = (byte)(rendering ? 1 : 0),
                BatchMode = (byte)(isBatchMode ? 1 : 0)
            };
        }

        private static bool EnvIsSet(string key)
        {
            var v = global::System.Environment.GetEnvironmentVariable(key);
            return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void LogState(string reason = null)
        {
            int isBatchMode = IsBatchMode ? 1 : 0;
            int headlessRequested = HeadlessRequested ? 1 : 0;
            int renderingEnabled = IsRenderingEnabled ? 1 : 0;
            int forceRender = s_flags.Data.ForceRender != 0 ? 1 : 0;
            int editorOverride = s_editorOverrideEnabled.Data != 0 ? 1 : 0;
            int headlessPresentation = EnvIsSet(HeadlessPresentationEnvVar) ? 1 : 0;
            string suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";
            Debug.Log($"[RuntimeMode] IsBatchMode={isBatchMode} HeadlessRequested={headlessRequested} RenderingEnabled={renderingEnabled} ForceRender={forceRender} EditorOverride={editorOverride} HeadlessPresentation={headlessPresentation}{suffix}");
        }

        private struct EditorOverrideKey
        {
        }

        private struct EditorOverrideContext
        {
        }
    }
}
