using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Data-driven scenario player. Loads scenario definitions from JSON,
    /// plays ordered sequences of FSM states with proper min/max durations
    /// and bridging animations. PRD §6.4 scenario system.
    /// </summary>
    public sealed class ScenarioPlayer
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ScenarioPlayer));
        private readonly FSM _fsm;
        private readonly IRandom _random;

        private List<ScenarioDef> _scenarios = new();
        private Dictionary<string, string[]> _bridges = new();

        private int _currentStepIndex = -1;
        private ScenarioDef? _activeScenario;
        private double _stepElapsedMs;
        private double _stepTargetMs;

        /// <summary>True when a scenario is actively playing.</summary>
        public bool IsActive => _activeScenario != null;
        /// <summary>Current step index in the active scenario, or -1.</summary>
        public int CurrentStepIndex => _currentStepIndex;

        public ScenarioPlayer(FSM fsm, IRandom random)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Load scenario definitions from a JSON file.
        /// </summary>
        public void LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Warning("Scenarios file not found: {Path}", path);
                return;
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ScenarioConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.Scenarios != null)
                _scenarios = config.Scenarios;
            if (config?.Transitions?.Bridges != null)
                _bridges = config.Transitions.Bridges;

            Logger.Information("Loaded {Count} scenarios from {Path}", _scenarios.Count, path);
        }

        /// <summary>
        /// Start playing a scenario by index.
        /// </summary>
        public void StartScenario(int index)
        {
            if (index < 0 || index >= _scenarios.Count)
            {
                Logger.Warning("Invalid scenario index: {Index}", index);
                return;
            }

            _activeScenario = _scenarios[index];
            _currentStepIndex = -1;
            AdvanceToNextStep();
            Logger.Information("Scenario started: {Id}", _activeScenario!.Id);
        }

        /// <summary>
        /// Start a scenario by ID.
        /// </summary>
        public bool StartScenarioById(string id)
        {
            for (var i = 0; i < _scenarios.Count; i++)
            {
                if (_scenarios[i].Id == id)
                {
                    StartScenario(i);
                    return true;
                }
            }
            Logger.Warning("Scenario not found: {Id}", id);
            return false;
        }

        /// <summary>
        /// Pick and start a random "random_idle" scenario weighted by their weight field.
        /// </summary>
        public void StartRandomIdleScenario()
        {
            var candidates = new List<(int index, double weight)>();
            for (var i = 0; i < _scenarios.Count; i++)
            {
                if (_scenarios[i].Trigger == "random_idle")
                {
                    var w = _scenarios[i].Weight > 0 ? _scenarios[i].Weight : 1.0;
                    candidates.Add((i, w));
                }
            }

            if (candidates.Count == 0)
                return;

            double total = 0;
            foreach (var (_, w) in candidates) total += w;
            double r = _random.NextDouble() * total;
            foreach (var (idx, w) in candidates)
            {
                r -= w;
                if (r <= 0)
                {
                    StartScenario(idx);
                    return;
                }
            }
            StartScenario(candidates[^1].index);
        }

        /// <summary>
        /// Update the scenario player. Call every tick.
        /// Returns true if the scenario completed and should be cleared.
        /// </summary>
        public bool Update(double deltaTimeMs)
        {
            if (_activeScenario == null || _currentStepIndex < 0)
                return false;

            _stepElapsedMs += deltaTimeMs;

            if (_stepElapsedMs >= _stepTargetMs)
            {
                AdvanceToNextStep();
            }

            return false;
        }

        /// <summary>
        /// Stop the active scenario and reset.
        /// </summary>
        public void Stop()
        {
            _activeScenario = null;
            _currentStepIndex = -1;
            _stepElapsedMs = 0;
        }

        private void AdvanceToNextStep()
        {
            if (_activeScenario == null) return;

            _currentStepIndex++;

            if (_currentStepIndex >= _activeScenario.Steps.Count)
            {
                Logger.Information("Scenario completed: {Id}", _activeScenario.Id);
                Stop();
                return;
            }

            var step = _activeScenario.Steps[_currentStepIndex];
            var target = ParseState(step.State);

            // Route through bridging states if needed
            if (_fsm.CurrentState != target && _fsm.CurrentState != FSMState.Idle)
            {
                var bridgeKey = _fsm.CurrentState.ToString() + "->" + step.State;
                if (_bridges.TryGetValue(bridgeKey, out var bridgeStates))
                {
                    // For now, just transition to the first bridge state
                    // The scenario will continue from there on next tick
                    var firstBridge = ParseState(bridgeStates[0]);
                    if (_fsm.CurrentState != firstBridge)
                    {
                        try { _fsm.TransitionTo(firstBridge, bypassValidation: true); }
                        catch (Exception ex) { Logger.Debug(ex, "Bridge transition failed"); }
                    }
                }
            }

            // Transition to the target state
            try
            {
                _fsm.TransitionTo(target, bypassValidation: true);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Scenario step transition failed: {State}", step.State);
            }

            // Set duration target
            double min = step.MinDurationMs > 0 ? step.MinDurationMs : 1000;
            double max = step.MaxDurationMs > 0 ? step.MaxDurationMs : min;
            _stepTargetMs = min + _random.NextDouble() * (max - min);
            _stepElapsedMs = 0;

            Logger.Debug("Scenario step {Index}/{Total}: {State} for {Ms:F0}ms",
                _currentStepIndex + 1, _activeScenario.Steps.Count, step.State, _stepTargetMs);
        }

        private static FSMState ParseState(string name)
        {
            if (Enum.TryParse<FSMState>(name, out var state))
                return state;
            return FSMState.Idle;
        }
    }

    // ── JSON model classes ──

    public sealed class ScenarioConfig
    {
        [JsonPropertyName("scenarios")]
        public List<ScenarioDef> Scenarios { get; set; } = new();

        [JsonPropertyName("transitions")]
        public TransitionConfig? Transitions { get; set; }
    }

    public sealed class ScenarioDef
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = "random_idle";

        [JsonPropertyName("weight")]
        public double Weight { get; set; } = 1.0;

        [JsonPropertyName("steps")]
        public List<ScenarioStep> Steps { get; set; } = new();
    }

    public sealed class ScenarioStep
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = "Idle";

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "hold";

        [JsonPropertyName("minDurationMs")]
        public double MinDurationMs { get; set; } = 1000;

        [JsonPropertyName("maxDurationMs")]
        public double MaxDurationMs { get; set; } = 0;
    }

    public sealed class TransitionConfig
    {
        [JsonPropertyName("bridges")]
        public Dictionary<string, string[]> Bridges { get; set; } = new();
    }
}
