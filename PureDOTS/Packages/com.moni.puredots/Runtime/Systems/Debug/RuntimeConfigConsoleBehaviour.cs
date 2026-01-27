using System;
using System.Collections.Generic;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.Debugging;
using PureDOTS.Runtime.Visuals;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PureDOTS.Runtime.Debugging
{
    [DisallowMultipleComponent]
    internal sealed class RuntimeConfigConsoleBehaviour : MonoBehaviour
    {
        private const int MaxLogEntries = 128;

        [SerializeField]
        private Key _toggleKey = Key.Backquote;

        private readonly List<string> _log = new(MaxLogEntries);
        private Rect _windowRect = new Rect(20f, 20f, 520f, 420f);
        private Vector2 _scrollPosition;
        private string _commandLine = string.Empty;
        private bool _visible;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var control = keyboard[_toggleKey];
            if (control != null && control.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            GUI.depth = -1000;
            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "PureDOTS Console");
        }

        public void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (_log.Count >= MaxLogEntries)
            {
                _log.RemoveAt(0);
            }

            _log.Add(message);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Commands: list, get <var>, set <var> <value>, reset <var>, save, help");

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(260f));
            for (var i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);

            GUI.SetNextControlName("ConsoleInput");
            _commandLine = GUILayout.TextField(_commandLine);
            GUI.FocusControl("ConsoleInput");

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter))
            {
                ExecuteCommand(_commandLine);
                _commandLine = string.Empty;
                currentEvent.Use();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Execute", GUILayout.Width(80f)))
            {
                ExecuteCommand(_commandLine);
                _commandLine = string.Empty;
            }
            if (GUILayout.Button(_visible ? "Hide" : "Show", GUILayout.Width(80f)))
            {
                _visible = !_visible;
            }
            if (GUILayout.Button("Save", GUILayout.Width(80f)))
            {
                RuntimeConfigRegistry.Save();
                AppendLog("Saved configuration to disk.");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return;

            AppendLog($"> {commandLine}");

            var tokens = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return;

            var command = tokens[0].ToLowerInvariant();

            switch (command)
            {
                case "help":
                    AppendLog("Commands: list, get <var>, set <var> <value>, reset <var>, save, overlay <show|hide|toggle>, visuals <show|hide|toggle|hud>, history <dump|latest>");
                    break;

                case "list":
                    foreach (var configVar in RuntimeConfigRegistry.GetVariables())
                    {
                        AppendLog($"{configVar.Name} = {configVar.Value} {(string.IsNullOrEmpty(configVar.Description) ? string.Empty : "- " + configVar.Description)}");
                    }
                    break;

                case "get":
                    if (tokens.Length < 2)
                    {
                        AppendLog("Usage: get <var>");
                        break;
                    }
                    if (RuntimeConfigRegistry.TryGetVar(tokens[1], out var getVar))
                    {
                        AppendLog($"{getVar.Name} = {getVar.Value}");
                    }
                    else
                    {
                        AppendLog($"Unknown config var '{tokens[1]}'.");
                    }
                    break;

                case "set":
                    if (tokens.Length < 3)
                    {
                        AppendLog("Usage: set <var> <value>");
                        break;
                    }
                    var setName = tokens[1];
                    if (tokens.Length > 2)
                    {
                        var builder = new StringBuilder();
                        for (int i = 2; i < tokens.Length; i++)
                        {
                            if (i > 2)
                            {
                                builder.Append(' ');
                            }

                            builder.Append(tokens[i]);
                        }

                        var setValue = builder.ToString();
                        if (RuntimeConfigRegistry.SetValue(setName, setValue, out var setMessage))
                        {
                            AppendLog(setMessage);
                        }
                        else
                        {
                            AppendLog(setMessage);
                        }
                    }
                    else
                    {
                        AppendLog("Usage: set <var> <value>");
                    }
                    break;

                case "reset":
                    if (tokens.Length < 2)
                    {
                        AppendLog("Usage: reset <var>");
                        break;
                    }
                    if (RuntimeConfigRegistry.ResetValue(tokens[1], out var resetMessage))
                    {
                        AppendLog(resetMessage);
                    }
                    else
                    {
                        AppendLog(resetMessage);
                    }
                    break;

                case "save":
                    RuntimeConfigRegistry.Save();
                    AppendLog("Saved configuration to disk.");
                    break;

                case "overlay":
                    HandleOverlayCommand(tokens);
                    break;

                case "visuals":
                    HandleVisualsCommand(tokens);
                    break;

                default:
                    AppendLog($"Unknown command '{command}'.");
                    break;
            }
        }

        private void HandleOverlayCommand(string[] tokens)
        {
            if (DebugConfigVars.DiagnosticsOverlayEnabled == null)
            {
                AppendLog("Overlay configuration unavailable.");
                return;
            }

            if (tokens.Length < 2)
            {
                AppendLog("Usage: overlay <show|hide|toggle>");
                return;
            }

            var action = tokens[1].ToLowerInvariant();
            switch (action)
            {
                case "show":
                    DebugConfigVars.DiagnosticsOverlayEnabled.BoolValue = true;
                    AppendLog("Runtime diagnostics overlay enabled.");
                    break;
                case "hide":
                    DebugConfigVars.DiagnosticsOverlayEnabled.BoolValue = false;
                    AppendLog("Runtime diagnostics overlay disabled.");
                    break;
                case "toggle":
                    DebugConfigVars.DiagnosticsOverlayEnabled.BoolValue = !DebugConfigVars.DiagnosticsOverlayEnabled.BoolValue;
                    AppendLog($"Runtime diagnostics overlay {(DebugConfigVars.DiagnosticsOverlayEnabled.BoolValue ? "enabled" : "disabled")}." );
                    break;
                default:
                    AppendLog("Usage: overlay <show|hide|toggle>");
                    break;
            }
        }

        private void HandleVisualsCommand(string[] tokens)
        {
            if (MiningVisualConfigVars.VisualsEnabled == null)
            {
                AppendLog("Mining visuals configuration unavailable.");
                return;
            }

            if (tokens.Length < 2)
            {
                AppendLog("Usage: visuals <show|hide|toggle|stats|hud> [on|off]");
                return;
            }

            var action = tokens[1].ToLowerInvariant();
            switch (action)
            {
                case "show":
                    MiningVisualConfigVars.VisualsEnabled.BoolValue = true;
                    AppendLog("Mining visuals enabled.");
                    break;
                case "hide":
                    MiningVisualConfigVars.VisualsEnabled.BoolValue = false;
                    AppendLog("Mining visuals disabled.");
                    break;
                case "toggle":
                    MiningVisualConfigVars.VisualsEnabled.BoolValue = !MiningVisualConfigVars.VisualsEnabled.BoolValue;
                    AppendLog($"Mining visuals {(MiningVisualConfigVars.VisualsEnabled.BoolValue ? "enabled" : "disabled")}." );
                    break;
                case "stats":
                    DumpMiningVisualStats();
                    break;
                case "debug":
                    DumpMiningVisualDebug();
                    break;
                case "hud":
                    if (MiningVisualConfigVars.HudEnabled == null)
                    {
                        AppendLog("Mining visuals HUD configuration unavailable.");
                        break;
                    }

                    bool desired;
                    if (tokens.Length >= 3)
                    {
                        var modifier = tokens[2].ToLowerInvariant();
                        if (modifier is "on" or "show" or "true")
                        {
                            desired = true;
                        }
                        else if (modifier is "off" or "hide" or "false")
                        {
                            desired = false;
                        }
                        else
                        {
                            AppendLog("Usage: visuals hud <on|off>");
                            break;
                        }
                    }
                    else
                    {
                        desired = !MiningVisualConfigVars.HudEnabled.BoolValue;
                    }

                    MiningVisualConfigVars.HudEnabled.BoolValue = desired;
                    AppendLog($"Mining visuals HUD {(desired ? "enabled" : "disabled")}.");
                    break;
                default:
                    AppendLog("Usage: visuals <show|hide|toggle|stats|hud> [on|off]");
                    break;
            }
        }

        private void DumpMiningVisualStats()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                AppendLog("World unavailable.");
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiningVisualManifest>());
            if (query.IsEmptyIgnoreFilter)
            {
                AppendLog("Mining visual manifest unavailable.");
                return;
            }

            var manifestEntity = query.GetSingletonEntity();
            var manifest = entityManager.GetComponentData<MiningVisualManifest>(manifestEntity);

            AppendLog($"Villagers: {manifest.VillagerNodeCount} active, {manifest.VillagerThroughput:F2}/min | Vessels: {manifest.VesselCount} active, {manifest.VesselThroughput:F2}/min");
        }

        private void DumpMiningVisualDebug()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                AppendLog("World unavailable.");
                return;
            }

            var entityManager = world.EntityManager;

            var timeInfo = "n/a";
            using (var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()))
            {
                if (!timeQuery.IsEmptyIgnoreFilter)
                {
                    var timeState = timeQuery.GetSingleton<TimeState>();
                    timeInfo = $"tick={timeState.Tick} paused={timeState.IsPaused} dt={timeState.FixedDeltaTime:F3}";
                }
            }

            var rewindInfo = "n/a";
            using (var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()))
            {
                if (!rewindQuery.IsEmptyIgnoreFilter)
                {
                    var rewindState = rewindQuery.GetSingleton<RewindState>();
                    var startTick = 0u;
                    using var legacyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindLegacyState>());
                    if (!legacyQuery.IsEmptyIgnoreFilter)
                    {
                        startTick = legacyQuery.GetSingleton<RewindLegacyState>().StartTick;
                    }
                    rewindInfo = $"mode={rewindState.Mode} start={startTick} target={rewindState.TargetTick}";
                }
            }

            var phaseCounts = new int[Enum.GetValues(typeof(VillagerJob.JobPhase)).Length];
            var gathererTotal = 0;
            var gathererActive = 0;
            var missingProgress = 0;
            var missingTicket = 0;
            var zeroReserved = 0;

            using (var villagerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerJob>()))
            using (var villagerEntities = villagerQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in villagerEntities)
                {
                    var job = entityManager.GetComponentData<VillagerJob>(entity);
                    var phaseIndex = (int)job.Phase;
                    if ((uint)phaseIndex < (uint)phaseCounts.Length)
                    {
                        phaseCounts[phaseIndex]++;
                    }

                    if (job.Type != VillagerJob.JobType.Gatherer)
                    {
                        continue;
                    }

                    gathererTotal++;
                    if (job.Phase is VillagerJob.JobPhase.Idle or VillagerJob.JobPhase.Completed or VillagerJob.JobPhase.Interrupted)
                    {
                        continue;
                    }

                    gathererActive++;

                    if (!entityManager.HasComponent<VillagerJobProgress>(entity))
                    {
                        missingProgress++;
                    }

                    if (!entityManager.HasComponent<VillagerJobTicket>(entity))
                    {
                        missingTicket++;
                        continue;
                    }

                    var ticket = entityManager.GetComponentData<VillagerJobTicket>(entity);
                    if (ticket.ReservedUnits <= 0f)
                    {
                        zeroReserved++;
                    }
                }

                var builder = new StringBuilder();
                builder.AppendLine($"Time: {timeInfo} | Rewind: {rewindInfo}");
                builder.AppendLine($"VillagerJob entities: {villagerEntities.Length}");
                builder.AppendLine($"Gatherers total={gathererTotal}, active(non-idle)={gathererActive}, missingProgress={missingProgress}, missingTicket={missingTicket}, zeroReserved={zeroReserved}");

                var phaseNames = Enum.GetNames(typeof(VillagerJob.JobPhase));
                for (var i = 0; i < phaseCounts.Length; i++)
                {
                    builder.Append(phaseNames[i]);
                    builder.Append('=');
                    builder.Append(phaseCounts[i]);
                    if (i < phaseCounts.Length - 1)
                    {
                        builder.Append(", ");
                    }
                }

                AppendLog(builder.ToString());
            }
        }
    }
}


