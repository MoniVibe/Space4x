using NUnit.Framework;
using PureDOTS.Runtime.Ships;
using Unity.Entities;

namespace PureDOTS.Tests.Ships
{
    public class ModuleMaintenanceTests
    {
        [Test]
        public void ApplyDegradation_MarksFailedState()
        {
            var health = new ModuleHealth
            {
                MaxHealth = 100f,
                Health = 30f,
                DegradationPerTick = 5f,
                FailureThreshold = 25f,
                LastProcessedTick = 0
            };

            var op = new ModuleOperationalState
            {
                InCombat = 0,
                IsOnline = 1,
                LoadFactor = 0.5f
            };

            ModuleMaintenanceUtility.ApplyDegradation(ref health, op, 5);

            Assert.AreEqual(ModuleHealthState.Failed, health.State);
            Assert.Less(health.Health, 25f);
            Assert.AreEqual(5u, health.LastProcessedTick);
        }

        [Test]
        public void SelectTicketIndex_PrefersPriorityThenSeverityThenTick()
        {
            using var world = new World("repair-queue-test");
            var entity = world.EntityManager.CreateEntity();
            var buffer = world.EntityManager.AddBuffer<ModuleRepairTicket>(entity);

            buffer.Add(new ModuleRepairTicket { Module = entity, Priority = 1, Severity = 0.3f, RequestedTick = 20 });
            buffer.Add(new ModuleRepairTicket { Module = entity, Priority = 1, Severity = 0.8f, RequestedTick = 40 });
            buffer.Add(new ModuleRepairTicket { Module = entity, Priority = 2, Severity = 0.1f, RequestedTick = 10 });

            var index = ModuleMaintenanceUtility.SelectTicketIndex(buffer);
            Assert.AreEqual(2, index, "Higher priority ticket should win even with lower severity.");

            buffer[2] = new ModuleRepairTicket { Module = entity, Priority = 1, Severity = 0.8f, RequestedTick = 10 };
            index = ModuleMaintenanceUtility.SelectTicketIndex(buffer);
            Assert.AreEqual(2, index, "Tie on priority should pick higher severity and earlier tick.");
        }

        [Test]
        public void CalculateRepairRate_AppliesSkillScalar()
        {
            var settings = new ModuleRepairSettings
            {
                MaxConcurrent = 1,
                FieldRepairRate = 2f,
                StationRepairRate = 6f,
                AllowFieldRepairs = 1
            };

            var noviceRate = ModuleMaintenanceUtility.CalculateRepairRate(settings, ModuleRepairKind.Field, 0);
            var veteranRate = ModuleMaintenanceUtility.CalculateRepairRate(settings, ModuleRepairKind.Field, 30);

            Assert.Greater(veteranRate, noviceRate);
            Assert.AreEqual(settings.FieldRepairRate, noviceRate);
            Assert.Greater(ModuleMaintenanceUtility.CalculateRepairRate(settings, ModuleRepairKind.Station, 10),
                ModuleMaintenanceUtility.CalculateRepairRate(settings, ModuleRepairKind.Field, 10));
        }

        [Test]
        public void IsRefitAllowed_RespectsFacilityAndFieldPermissions()
        {
            var settings = CarrierRefitSettings.CreateDefaults();
            var state = new CarrierRefitState { InRefitFacility = 0, SpeedMultiplier = 1f };

            Assert.IsFalse(ModuleMaintenanceUtility.IsRefitAllowed(ModuleRepairKind.Station, settings, state), "Station refit should be blocked without facility.");
            Assert.IsFalse(ModuleMaintenanceUtility.IsRefitAllowed(ModuleRepairKind.Field, settings, state), "Field refit blocked when AllowFieldRefit = 0.");

            settings.AllowFieldRefit = 1;
            Assert.IsTrue(ModuleMaintenanceUtility.IsRefitAllowed(ModuleRepairKind.Field, settings, state));

            state.InRefitFacility = 1;
            Assert.IsTrue(ModuleMaintenanceUtility.IsRefitAllowed(ModuleRepairKind.Station, settings, state));
        }

        [Test]
        public void CalculateRefitDuration_ScalesWithMassSkillAndSpeed()
        {
            var module = new ShipModule { Mass = 10f };
            var settings = CarrierRefitSettings.CreateDefaults();

            var baseDuration = ModuleMaintenanceUtility.CalculateRefitDuration(module, settings, 0, 1f);
            var skilledDuration = ModuleMaintenanceUtility.CalculateRefitDuration(module, settings, 20, 1f);
            var boostedDuration = ModuleMaintenanceUtility.CalculateRefitDuration(module, settings, 0, 2f);

            Assert.Less(skilledDuration, baseDuration, "Higher skill should reduce duration.");
            Assert.Less(boostedDuration, baseDuration, "Speed multiplier should reduce duration.");
        }
    }
}
