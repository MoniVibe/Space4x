#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Stats;
using PureDOTS.Runtime.Villagers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Space4XIndividualStats = PureDOTS.Runtime.Stats.IndividualStats;

namespace PureDOTS.Tests.Identity
{
    /// <summary>
    /// Unit tests for EntityProfilingService methods.
    /// Tests service API correctness, component creation, and parameter handling.
    /// </summary>
    public class EntityProfilingServiceTests
    {
        [Test]
        public void CreateVillager_SetsArchetypeAndTick()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            var profile = new VillagerProfileData 
            { 
                BasePhysique = 50f, 
                BaseFinesse = 50f, 
                BaseWill = 50f 
            };
            
            // Act
            EntityProfilingService.CreateVillager(
                ref entityManager, 
                entity, 
                profile, 
                new FixedString64Bytes("Default"), 
                100u);
            
            // Assert
            Assert.IsTrue(entityManager.HasComponent<EntityProfile>(entity), "EntityProfile should be created");
            var ep = entityManager.GetComponentData<EntityProfile>(entity);
            Assert.AreEqual("Default", ep.ArchetypeName.ToString(), "Archetype name should be set");
            Assert.AreEqual(100u, ep.CreatedTick, "CreatedTick should be set");
            Assert.IsTrue(entityManager.HasComponent<VillagerProfileData>(entity), "VillagerProfileData should be stored");
            Assert.IsTrue(entityManager.HasComponent<VillagerArchetypeAssignment>(entity), "VillagerArchetypeAssignment should be created");
            
            world.Dispose();
        }

        [Test]
        public void CreateVillager_UsesDefaultArchetype_WhenNoArchetypeProvided()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            var profile = new VillagerProfileData 
            { 
                BasePhysique = 50f, 
                BaseFinesse = 50f, 
                BaseWill = 50f 
            };
            
            // Act
            EntityProfilingService.CreateVillager(
                ref entityManager, 
                entity, 
                profile);
            
            // Assert
            var ep = entityManager.GetComponentData<EntityProfile>(entity);
            Assert.AreEqual("Default", ep.ArchetypeName.ToString(), "Should use Default archetype when profile has stats");
            
            world.Dispose();
        }

        [Test]
        public void CreateIndividual_WithOfficerStats_PopulatesProfile()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            var profile = new IndividualProfileData 
            { 
                BasePhysique = 50f, 
                BaseFinesse = 50f, 
                BaseWill = 50f 
            };
            
            var officerStats = new Space4XIndividualStats
            {
                Command = (half)75f,
                Tactics = (half)80f,
                Logistics = (half)70f,
                Diplomacy = (half)65f,
                Engineering = (half)85f,
                Resolve = (half)90f
            };
            
            // Act
            EntityProfilingService.CreateIndividualWithOfficerStats(
                ref entityManager, 
                entity, 
                profile,
                default,
                200u,
                officerStats);
            
            // Assert
            Assert.IsTrue(entityManager.HasComponent<IndividualProfileData>(entity), "IndividualProfileData should be stored");
            var storedProfile = entityManager.GetComponentData<IndividualProfileData>(entity);
            Assert.AreEqual(75f, (float)storedProfile.Command, 0.01f, "Command should be populated");
            Assert.AreEqual(80f, (float)storedProfile.Tactics, 0.01f, "Tactics should be populated");
            Assert.AreEqual(70f, (float)storedProfile.Logistics, 0.01f, "Logistics should be populated");
            
            var ep = entityManager.GetComponentData<EntityProfile>(entity);
            Assert.AreEqual(200u, ep.CreatedTick, "CreatedTick should be set");
            
            world.Dispose();
        }

        [Test]
        public void ApplyArchetype_UpdatesExistingAssignment()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new VillagerArchetypeAssignment
            {
                ArchetypeName = new FixedString64Bytes("OldArchetype"),
                CachedIndex = 5
            });
            
            // Act
            EntityProfilingService.ApplyArchetype(ref entityManager, entity, new FixedString64Bytes("NewArchetype"));
            
            // Assert
            var assignment = entityManager.GetComponentData<VillagerArchetypeAssignment>(entity);
            Assert.AreEqual("NewArchetype", assignment.ArchetypeName.ToString(), "Archetype name should be updated");
            Assert.AreEqual(-1, assignment.CachedIndex, "CachedIndex should be reset");
            
            world.Dispose();
        }

        [Test]
        public void ApplyDerivedStats_SetsNeedsRecalculation()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new DerivedAttributes
            {
                Strength = 50f,
                Agility = 50f,
                Intelligence = 50f,
                WisdomDerived = 50f,
                LastRecalculatedTick = 100u,
                NeedsRecalculation = 0
            });
            
            // Act
            EntityProfilingService.ApplyDerivedStats(ref entityManager, entity);
            
            // Assert
            var derived = entityManager.GetComponentData<DerivedAttributes>(entity);
            Assert.AreEqual(1, derived.NeedsRecalculation, "NeedsRecalculation should be set to 1");
            
            world.Dispose();
        }

        [Test]
        public void ApplyDerivedStats_CreatesComponent_IfMissing()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            // Act
            EntityProfilingService.ApplyDerivedStats(ref entityManager, entity);
            
            // Assert
            Assert.IsTrue(entityManager.HasComponent<DerivedAttributes>(entity), "DerivedAttributes should be created");
            var derived = entityManager.GetComponentData<DerivedAttributes>(entity);
            Assert.AreEqual(1, derived.NeedsRecalculation, "NeedsRecalculation should be set to 1");
            
            world.Dispose();
        }

        [Test]
        public void IsProfileComplete_ReturnsFalse_WhenPhaseNotComplete()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new ProfileApplicationState
            {
                Phase = ProfileApplicationPhase.StatsApplied,
                LastUpdatedTick = 100u,
                NeedsRecalculation = 0
            });
            
            // Act
            bool isComplete = EntityProfilingService.IsProfileComplete(ref entityManager, entity);
            
            // Assert
            Assert.IsFalse(isComplete, "Profile should not be complete when phase is not Complete");
            
            world.Dispose();
        }

        [Test]
        public void IsProfileComplete_ReturnsTrue_WhenPhaseComplete()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new ProfileApplicationState
            {
                Phase = ProfileApplicationPhase.Complete,
                LastUpdatedTick = 100u,
                NeedsRecalculation = 0
            });
            
            // Act
            bool isComplete = EntityProfilingService.IsProfileComplete(ref entityManager, entity);
            
            // Assert
            Assert.IsTrue(isComplete, "Profile should be complete when phase is Complete");
            
            world.Dispose();
        }

        [Test]
        public void SkipEntityProfiling_ComponentExists()
        {
            // Arrange
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            // Act
            entityManager.AddComponent<SkipEntityProfiling>(entity);
            
            // Assert
            Assert.IsTrue(entityManager.HasComponent<SkipEntityProfiling>(entity), "SkipEntityProfiling component should exist");
            
            world.Dispose();
        }
    }
}
#endif

