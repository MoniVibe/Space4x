using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space4x.Scenario
{
    [Serializable]
    public class RefitScenarioJson
    {
        public int seed;
        public float duration_s;
        public List<SpawnDefinition> spawn;
        public List<ScenarioAction> actions;
        public TelemetryExpectations telemetryExpectations;
    }

    [Serializable]
    public class SpawnDefinition
    {
        public string kind;
        public string hullId;
        public float[] position;
        public List<LoadoutEntry> loadout;
        public string entityId;
        public ComponentData components;
    }

    [Serializable]
    public class LoadoutEntry
    {
        public int slotIndex;
        public string moduleId;
    }

    [Serializable]
    public class ComponentData
    {
        public bool RefitFacilityTag;
        public FacilityZoneData FacilityZone;
    }

    [Serializable]
    public class FacilityZoneData
    {
        public float radiusMeters;
    }

    [Serializable]
    public class ScenarioAction
    {
        public float time_s;
        public string type;
        public string target;
        public float to;
        public string mode;
        public string[] targets;
        public string targetEntity;
        public RefitSwap swap;
    }

    [Serializable]
    public class RefitSwap
    {
        public int slotIndex;
        public string newModuleId;
    }

    [Serializable]
    public class TelemetryExpectations
    {
        public bool expectNonNegativePowerBalance;
        public int expectRefitCount;
        public int expectFieldRepairCount;
        public List<string> expectModulesRestoredTo;
        public List<TelemetryAssertion> assertions;
        public TelemetryExport export;
    }

    [Serializable]
    public class TelemetryAssertion
    {
        public string name;
        public string op;
        public string lhs;
        public string rhs;
    }

    [Serializable]
    public class TelemetryExport
    {
        public string csv;
        public string json;
    }
}

