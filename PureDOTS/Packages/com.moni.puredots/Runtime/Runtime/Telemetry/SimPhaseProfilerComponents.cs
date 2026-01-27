using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    public enum SimPhase : byte
    {
        ScenarioApply = 0,
        Movement = 1,
        Physics = 2,
        Sensors = 3,
        Comms = 4,
        Knowledge = 5,
        Economy = 6,
        PresentationBridge = 7,
        Count = 8
    }

    public struct SimPhaseWorstTickRecord
    {
        public uint Tick;
        public float DurationMs;
        public byte DominantPhase;
    }

    public struct SimPhaseProfilerState : IComponentData
    {
        public uint Tick;
        public float TickTotalMs;
        public float ScenarioApplyMs;
        public float MovementMs;
        public float PhysicsMs;
        public float SensorsMs;
        public float CommsMs;
        public float KnowledgeMs;
        public float EconomyMs;
        public float PresentationBridgeMs;

        public SimPhaseWorstTickRecord WorstTick0;
        public SimPhaseWorstTickRecord WorstTick1;
        public SimPhaseWorstTickRecord WorstTick2;

        public void ResetForTick(uint tick)
        {
            Tick = tick;
            TickTotalMs = 0f;
            ScenarioApplyMs = 0f;
            MovementMs = 0f;
            PhysicsMs = 0f;
            SensorsMs = 0f;
            CommsMs = 0f;
            KnowledgeMs = 0f;
            EconomyMs = 0f;
            PresentationBridgeMs = 0f;
        }

        public void SetPhaseDuration(SimPhase phase, float durationMs)
        {
            switch (phase)
            {
                case SimPhase.ScenarioApply:
                    ScenarioApplyMs = durationMs;
                    break;
                case SimPhase.Movement:
                    MovementMs = durationMs;
                    break;
                case SimPhase.Physics:
                    PhysicsMs = durationMs;
                    break;
                case SimPhase.Sensors:
                    SensorsMs = durationMs;
                    break;
                case SimPhase.Comms:
                    CommsMs = durationMs;
                    break;
                case SimPhase.Knowledge:
                    KnowledgeMs = durationMs;
                    break;
                case SimPhase.Economy:
                    EconomyMs = durationMs;
                    break;
                case SimPhase.PresentationBridge:
                    PresentationBridgeMs = durationMs;
                    break;
            }
        }

        public float GetPhaseDuration(SimPhase phase)
        {
            return phase switch
            {
                SimPhase.ScenarioApply => ScenarioApplyMs,
                SimPhase.Movement => MovementMs,
                SimPhase.Physics => PhysicsMs,
                SimPhase.Sensors => SensorsMs,
                SimPhase.Comms => CommsMs,
                SimPhase.Knowledge => KnowledgeMs,
                SimPhase.Economy => EconomyMs,
                SimPhase.PresentationBridge => PresentationBridgeMs,
                _ => 0f
            };
        }

        public SimPhase GetDominantPhase()
        {
            var bestPhase = SimPhase.ScenarioApply;
            var bestValue = ScenarioApplyMs;
            for (byte phase = 1; phase < (byte)SimPhase.Count; phase++)
            {
                var value = GetPhaseDuration((SimPhase)phase);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPhase = (SimPhase)phase;
                }
            }
            return bestPhase;
        }

        public void UpdateWorstRecords(uint tick, SimPhase dominantPhase)
        {
            var record = new SimPhaseWorstTickRecord
            {
                Tick = tick,
                DurationMs = TickTotalMs,
                DominantPhase = (byte)dominantPhase
            };

            InsertWorstRecord(ref WorstTick0, ref record);
            InsertWorstRecord(ref WorstTick1, ref record);
            InsertWorstRecord(ref WorstTick2, ref record);
        }

        private void InsertWorstRecord(ref SimPhaseWorstTickRecord slot, ref SimPhaseWorstTickRecord candidate)
        {
            if (candidate.DurationMs <= slot.DurationMs)
            {
                return;
            }

            var overflow = slot;
            slot = candidate;
            candidate = overflow;
        }
    }

    public struct SimPhaseProfilerPhaseStartTimes : IComponentData
    {
        private const double Invalid = double.MinValue;

        public double ScenarioApply;
        public double Movement;
        public double Physics;
        public double Sensors;
        public double Comms;
        public double Knowledge;
        public double Economy;
        public double PresentationBridge;

        public void SetStart(SimPhase phase, double value)
        {
            switch (phase)
            {
                case SimPhase.ScenarioApply:
                    ScenarioApply = value;
                    break;
                case SimPhase.Movement:
                    Movement = value;
                    break;
                case SimPhase.Physics:
                    Physics = value;
                    break;
                case SimPhase.Sensors:
                    Sensors = value;
                    break;
                case SimPhase.Comms:
                    Comms = value;
                    break;
                case SimPhase.Knowledge:
                    Knowledge = value;
                    break;
                case SimPhase.Economy:
                    Economy = value;
                    break;
                case SimPhase.PresentationBridge:
                    PresentationBridge = value;
                    break;
            }
        }

        public double GetStart(SimPhase phase)
        {
            return phase switch
            {
                SimPhase.ScenarioApply => ScenarioApply,
                SimPhase.Movement => Movement,
                SimPhase.Physics => Physics,
                SimPhase.Sensors => Sensors,
                SimPhase.Comms => Comms,
                SimPhase.Knowledge => Knowledge,
                SimPhase.Economy => Economy,
                SimPhase.PresentationBridge => PresentationBridge,
                _ => Invalid
            };
        }

        public void ClearStart(SimPhase phase)
        {
            SetStart(phase, Invalid);
        }

        public void Reset()
        {
            ScenarioApply = Invalid;
            Movement = Invalid;
            Physics = Invalid;
            Sensors = Invalid;
            Comms = Invalid;
            Knowledge = Invalid;
            Economy = Invalid;
            PresentationBridge = Invalid;
        }
    }

    public static class SimPhaseProfilerPhaseStartTimesExtensions
    {
        public static SimPhaseProfilerPhaseStartTimes CreateDefault()
        {
            var times = new SimPhaseProfilerPhaseStartTimes();
            times.Reset();
            return times;
        }
    }
}
