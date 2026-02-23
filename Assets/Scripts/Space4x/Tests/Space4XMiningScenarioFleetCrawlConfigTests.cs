#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Space4x.Scenario;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XMiningScenarioFleetCrawlConfigTests
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";

        private World _world;
        private EntityManager _entityManager;
        private InitializationSystemGroup _initGroup;
        private Space4XMiningScenarioSystem _scenarioSystem;
        private string _previousScenarioPath;
        private List<string> _tempScenarioFiles;

        [SetUp]
        public void SetUp()
        {
            _previousScenarioPath = Environment.GetEnvironmentVariable(ScenarioPathEnv);
            _tempScenarioFiles = new List<string>();

            _world = new World("Space4XMiningScenarioFleetCrawlConfigTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _initGroup = _world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            _scenarioSystem = _world.GetOrCreateSystemManaged<Space4XMiningScenarioSystem>();
            _initGroup.AddSystemToUpdateList(_scenarioSystem);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(ScenarioPathEnv, _previousScenarioPath);

            if (_tempScenarioFiles != null)
            {
                for (var i = 0; i < _tempScenarioFiles.Count; i++)
                {
                    try
                    {
                        if (File.Exists(_tempScenarioFiles[i]))
                        {
                            File.Delete(_tempScenarioFiles[i]);
                        }
                    }
                    catch
                    {
                        // Best effort cleanup for temp files.
                    }
                }
            }

            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void FleetcrawlConfig_NormalizesAndDefaultsInvalidEntries()
        {
            LoadScenarioJson(
                @"{
  ""seed"": 11,
  ""duration_s"": 1,
  ""scenarioConfig"": {
    ""fleetCrawl"": {
      ""contractId"": "" space4x.fleetcrawl.room_contract.v1 "",
      ""runDifficulty"": "" impossible "",
      ""depthStart"": 0,
      ""roomPlan"": [
        {
          ""archetype"": ""resource_room"",
          ""roomClass"": ""ELITE"",
          ""systemSize"": ""LARGE"",
          ""threatLevel"": 0,
          ""wildcards"": [""Hazard"", ""hazard"", ""DISTRESS""]
        },
        {
          ""archetype"": ""swarm_room"",
          ""roomClass"": ""???"",
          ""systemSize"": ""??"",
          ""threatLevel"": 3,
          ""wildcards"": [""market"", ""market"", ""anomaly""]
        }
      ]
    }
  },
  ""spawn"": []
}");

            var configQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetcrawlScenarioContractConfig));
            Assert.AreEqual(1, configQuery.CalculateEntityCount(), "FleetCrawl config singleton should exist.");

            var configEntity = configQuery.GetSingletonEntity();
            var config = _entityManager.GetComponentData<Space4XFleetcrawlScenarioContractConfig>(configEntity);
            var roomPlan = _entityManager.GetBuffer<Space4XFleetcrawlRoomPlanOverride>(configEntity);

            Assert.AreEqual("space4x.fleetcrawl.room_contract.v1", config.ContractId.ToString());
            Assert.AreEqual("normal", config.RunDifficulty.ToString(), "Invalid run difficulty should fall back to normal.");
            Assert.AreEqual(1, config.DepthStart, "Depth start should be clamped to minimum 1.");
            Assert.AreEqual(1, config.HasRoomPlan);
            Assert.AreEqual(2, roomPlan.Length);

            Assert.AreEqual("elite", roomPlan[0].RoomClass.ToString());
            Assert.AreEqual("large", roomPlan[0].SystemSize.ToString());
            Assert.AreEqual(1, roomPlan[0].ThreatLevel);
            Assert.AreEqual("hazard|distress_signal", roomPlan[0].WildcardsCsv.ToString());

            Assert.AreEqual("normal", roomPlan[1].RoomClass.ToString(), "Unknown roomClass should default to normal.");
            Assert.AreEqual("medium", roomPlan[1].SystemSize.ToString(), "Unknown systemSize should default to medium.");
            Assert.AreEqual(3, roomPlan[1].ThreatLevel);
            Assert.AreEqual("roaming_market|anomaly", roomPlan[1].WildcardsCsv.ToString());
        }

        [Test]
        public void FleetcrawlConfig_IsClearedWhenScenarioOmitsFleetCrawlConfig()
        {
            LoadScenarioJson(
                @"{
  ""seed"": 12,
  ""duration_s"": 1,
  ""scenarioConfig"": {
    ""fleetCrawl"": {
      ""contractId"": ""space4x.fleetcrawl.room_contract.v1"",
      ""runDifficulty"": ""hard"",
      ""depthStart"": 2
    }
  },
  ""spawn"": []
}");

            var query = _entityManager.CreateEntityQuery(typeof(Space4XFleetcrawlScenarioContractConfig));
            Assert.AreEqual(1, query.CalculateEntityCount(), "FleetCrawl config should exist after configured scenario load.");

            _scenarioSystem.RequestReloadForModeSwitch();

            LoadScenarioJson(
                @"{
  ""seed"": 13,
  ""duration_s"": 1,
  ""scenarioConfig"": {},
  ""spawn"": []
}");

            Assert.AreEqual(0, query.CalculateEntityCount(), "FleetCrawl config singleton should be removed when fleetCrawl config is omitted.");
        }

        private void LoadScenarioJson(string json)
        {
            var scenarioPath = Path.Combine(Path.GetTempPath(), $"space4x_fleetcrawl_cfg_{Guid.NewGuid():N}.json");
            File.WriteAllText(scenarioPath, json);
            _tempScenarioFiles.Add(scenarioPath);

            Environment.SetEnvironmentVariable(ScenarioPathEnv, scenarioPath);
            _initGroup.Update();
        }
    }
}
#endif
