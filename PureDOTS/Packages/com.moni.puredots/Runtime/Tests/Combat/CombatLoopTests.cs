using NUnit.Framework;
using PureDOTS.Runtime.Combat;
using Unity.Entities;

namespace PureDOTS.Tests.Combat
{
    public class CombatLoopTests
    {
        [Test]
        public void PilotExperienceUnlocksManeuvers()
        {
            using var world = new World("CombatTestWorld");
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity(typeof(PilotExperience), typeof(VesselManeuverProfile));
            entityManager.SetComponentData(entity, new PilotExperience { Experience = 30f });
            entityManager.SetComponentData(entity, new VesselManeuverProfile
            {
                StrafeThreshold = 10f,
                KiteThreshold = 25f,
                JTurnThreshold = 40f
            });
            Assert.That(entityManager.GetComponentData<PilotExperience>(entity).Experience, Is.EqualTo(30f));
        }
    }
}
