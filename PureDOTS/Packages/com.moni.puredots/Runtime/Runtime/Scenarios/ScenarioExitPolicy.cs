using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using SystemEnvironment = System.Environment;

namespace PureDOTS.Runtime.Scenarios
{
    public enum ExitPolicy : byte
    {
        NeverNonZero = 0,
        InvariantsOnly = 1,
        InvariantsAndDeterminism = 2,
        Strict = 3
    }

    public enum ScenarioSeverity : byte
    {
        Info = 0,
        Warn = 1,
        Error = 2,
        Fatal = 3
    }

    public enum ScenarioIssueKind : byte
    {
        Info = 0,
        Invariant = 1,
        Determinism = 2,
        ScenarioContract = 3,
        Performance = 4,
        Quality = 5,
        Telemetry = 6,
        Other = 7
    }

    public struct ScenarioRunIssue
    {
        public ScenarioIssueKind Kind;
        public ScenarioSeverity Severity;
        public FixedString64Bytes Code;
        public FixedString128Bytes Message;
    }

    internal static class ScenarioRunIssueReporter
    {
        private static readonly List<ScenarioRunIssue> s_issues = new();

        public static void BeginRun()
        {
            s_issues.Clear();
        }

        public static void Report(ScenarioIssueKind kind, ScenarioSeverity severity, string code, string message)
        {
            ScenarioRunIssue issue = default;
            issue.Kind = kind;
            issue.Severity = severity;
            issue.Code = new FixedString64Bytes(code ?? string.Empty);
            issue.Message = new FixedString128Bytes(message ?? string.Empty);
            s_issues.Add(issue);

            var prefix = $"[ScenarioRunner] {kind}/{severity}: ";
            switch (severity)
            {
                case ScenarioSeverity.Fatal:
                case ScenarioSeverity.Error:
                    Debug.LogError(prefix + message);
                    break;
                case ScenarioSeverity.Warn:
                    Debug.LogWarning(prefix + message);
                    break;
                default:
                    Debug.Log(prefix + message);
                    break;
            }
        }

        public static void FlushToResult(ref ScenarioRunResult result)
        {
            if (s_issues.Count == 0)
            {
                result.Issues = null;
                result.HighestSeverity = ScenarioSeverity.Info;
                return;
            }

            result.Issues = new List<ScenarioRunIssue>(s_issues);
            result.HighestSeverity = ScenarioExitUtility.CalculateHighestSeverity(result.ExitPolicy, result.Issues);
        }
    }

    public static class ScenarioExitUtility
    {
        private const string ExitPolicyEnvVar = "PUREDOTS_EXIT_POLICY";

        private static int GetIntEnv(string key, int defaultValue)
        {
            var s = SystemEnvironment.GetEnvironmentVariable(key);
            return int.TryParse(s, out var v) ? v : defaultValue;
        }

        private static string GetEnv(string key, string defaultValue = "")
            => SystemEnvironment.GetEnvironmentVariable(key) ?? defaultValue;

        public static ExitPolicy ResolveExitPolicy()
        {
            var env = GetEnv(ExitPolicyEnvVar);
            if (string.IsNullOrWhiteSpace(env))
            {
                return ExitPolicy.InvariantsAndDeterminism;
            }

            switch (env.Trim().ToLowerInvariant())
            {
                case "never":
                case "nevernonzero":
                    return ExitPolicy.NeverNonZero;
                case "invariants":
                case "invariantsonly":
                    return ExitPolicy.InvariantsOnly;
                case "strict":
                    return ExitPolicy.Strict;
                case "determinism":
                case "invariantsanddeterminism":
                    return ExitPolicy.InvariantsAndDeterminism;
                default:
                    return ExitPolicy.InvariantsAndDeterminism;
            }
        }

        public static ScenarioSeverity CalculateHighestSeverity(ExitPolicy policy, IReadOnlyList<ScenarioRunIssue> issues)
        {
            var highest = ScenarioSeverity.Info;
            if (policy == ExitPolicy.NeverNonZero)
            {
                return ScenarioSeverity.Warn;
            }

            if (issues == null)
            {
                return highest;
            }

            foreach (var issue in issues)
            {
                var severity = AdjustSeverity(policy, issue);
                if (severity > highest)
                {
                    highest = severity;
                }
            }

            return highest;
        }

        private static ScenarioSeverity AdjustSeverity(ExitPolicy policy, in ScenarioRunIssue issue)
        {
            switch (issue.Kind)
            {
                case ScenarioIssueKind.Invariant:
                case ScenarioIssueKind.ScenarioContract:
                    return issue.Severity;
                case ScenarioIssueKind.Determinism:
                    return policy >= ExitPolicy.InvariantsAndDeterminism ? issue.Severity : ScenarioSeverity.Warn;
                case ScenarioIssueKind.Performance:
                case ScenarioIssueKind.Quality:
                    return policy == ExitPolicy.Strict ? issue.Severity : ScenarioSeverity.Warn;
                default:
                    return issue.Severity;
            }
        }

        public static bool ShouldExitNonZero(in ScenarioRunResult result, out ScenarioSeverity severity)
        {
            severity = result.HighestSeverity;
            if (result.ExitPolicy == ExitPolicy.NeverNonZero)
            {
                return false;
            }

            return severity switch
            {
                ScenarioSeverity.Fatal => true,
                ScenarioSeverity.Error => result.ExitPolicy == ExitPolicy.Strict,
                _ => false
            };
        }

        public static void ReportInvariant(string code, string message)
        {
            ScenarioRunIssueReporter.Report(ScenarioIssueKind.Invariant, ScenarioSeverity.Fatal, code, message);
        }

        public static void ReportScenarioContract(string code, string message)
        {
            ScenarioRunIssueReporter.Report(ScenarioIssueKind.ScenarioContract, ScenarioSeverity.Fatal, code, message);
        }

        public static void ReportDeterminism(string code, string message)
        {
            ScenarioRunIssueReporter.Report(ScenarioIssueKind.Determinism, ScenarioSeverity.Fatal, code, message);
        }

        public static void ReportPerformance(string code, string message, bool fatal = false)
        {
            var severity = fatal ? ScenarioSeverity.Fatal : ScenarioSeverity.Error;
            ScenarioRunIssueReporter.Report(ScenarioIssueKind.Performance, severity, code, message);
        }

        public static void ReportQuality(string code, ScenarioSeverity severity, string message)
        {
            ScenarioRunIssueReporter.Report(ScenarioIssueKind.Quality, severity, code, message);
        }
    }
}
