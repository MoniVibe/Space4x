using NUnit.Framework;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using System.IO;
using Unity.Entities;

namespace PureDOTS.Tests.Scenarios
{
    public class ScenarioRunnerTests
    {
        [Test]
        public void TryParse_ValidJson_Succeeds()
        {
            const string json = @"{
  ""scenarioId"": ""scenario.smoke"",
  ""seed"": 123,
  ""runTicks"": 120,
  ""entityCounts"": [
    { ""registryId"": ""registry.villager"", ""count"": 5 },
    { ""registryId"": ""registry.storehouse"", ""count"": 1 }
  ],
  ""inputCommands"": [
    { ""tick"": 10, ""commandId"": ""time.pause"", ""payload"": """" },
    { ""tick"": 30, ""commandId"": ""time.play"", ""payload"": """" }
  ]
}";

            Assert.IsTrue(ScenarioRunner.TryParse(json, out var data, out var error), error.ToString());
            using var scenario = Build(data);

            Assert.AreEqual("scenario.smoke", scenario.ScenarioId.ToString());
            Assert.AreEqual(123u, scenario.Seed);
            Assert.AreEqual(120, scenario.RunTicks);
            Assert.AreEqual(2, scenario.EntityCounts.Length);
            Assert.AreEqual("registry.villager", scenario.EntityCounts[0].RegistryId.ToString());
            Assert.AreEqual(5, scenario.EntityCounts[0].Count);
            Assert.AreEqual(2, scenario.InputCommands.Length);
            Assert.AreEqual(10, scenario.InputCommands[0].Tick);
            Assert.AreEqual("time.pause", scenario.InputCommands[0].CommandId.ToString());
        }

        [Test]
        public void TryParse_InvalidMissingId_Fails()
        {
            const string json = @"{
  ""scenarioId"": """",
  ""seed"": 1,
  ""runTicks"": 10
}";

            Assert.IsFalse(ScenarioRunner.TryParse(json, out _, out _));
        }

        [Test]
        public void TryBuild_FiltersInvalidEntries()
        {
            const string json = @"{
  ""scenarioId"": ""scenario.filter"",
  ""seed"": 9,
  ""runTicks"": 15,
  ""entityCounts"": [
    { ""registryId"": ""registry.valid"", ""count"": 3 },
    { ""registryId"": """", ""count"": 4 },
    { ""registryId"": ""registry.negative"", ""count"": -5 }
  ],
  ""inputCommands"": [
    { ""tick"": 4, ""commandId"": ""cmd.valid"", ""payload"": ""p"" },
    { ""tick"": -1, ""commandId"": ""cmd.invalid"", ""payload"": ""ignored"" },
    { ""tick"": 6, ""commandId"": """", ""payload"": ""ignored2"" }
  ]
}";

            Assert.IsTrue(ScenarioRunner.TryParse(json, out var data, out var error), error.ToString());
            using var scenario = Build(data);

            Assert.AreEqual(1, scenario.EntityCounts.Length);
            Assert.AreEqual("registry.valid", scenario.EntityCounts[0].RegistryId.ToString());
            Assert.AreEqual(3, scenario.EntityCounts[0].Count);
            Assert.AreEqual(1, scenario.InputCommands.Length);
            Assert.AreEqual("cmd.valid", scenario.InputCommands[0].CommandId.ToString());
        }

        [Test]
        public void SampleScenarios_ParseSuccessfully()
        {
            var root = Path.Combine("Packages", "com.moni.puredots", "Runtime", "Runtime", "Scenarios", "Samples");
            var files = new[]
            {
                Path.Combine(root, "godgame_smoke.json"),
                Path.Combine(root, "space4x_smoke.json"),
                Path.Combine(root, "space4x_modules_smoke.json")
            };

            foreach (var file in files)
            {
                Assert.IsTrue(File.Exists(file), $"Missing sample scenario: {file}");
                var json = File.ReadAllText(file);
                Assert.IsTrue(ScenarioRunner.TryParse(json, out var data, out var error), error.ToString());
                using var scenario = Build(data);
                Assert.Greater(scenario.RunTicks, 0, file);
                Assert.IsTrue(scenario.ScenarioId.Length > 0, file);
            }
        }

        [Test]
        public void HeadlessRun_AdvancesTicks()
        {
            var path = Path.Combine("Packages", "com.moni.puredots", "Runtime", "Runtime", "Scenarios", "Samples", "godgame_smoke.json");
            Assume.That(File.Exists(path), "Sample scenario missing");

            var result = ScenarioRunnerExecutor.RunFromFile(path);
            Assert.Greater(result.FinalTick, 0);
            Assert.IsTrue(result.RunTicks >= result.FinalTick);
            Assert.IsInstanceOf<string>(ScenarioRunResultJson.Serialize(result));
            Assert.GreaterOrEqual(result.EntityCountEntries, 1);
            Assert.Greater(result.CommandCapacity, 0);
            Assert.Greater(result.SnapshotCapacity, 0);
            Assert.Greater(result.TotalLogBytes, 0);
        }

        private static ResolvedScenario Build(ScenarioDefinitionData data)
        {
            Assert.IsTrue(ScenarioRunner.TryBuild(data, Allocator.Temp, out var scenario, out var error), error.ToString());
            return scenario;
        }
    }
}
