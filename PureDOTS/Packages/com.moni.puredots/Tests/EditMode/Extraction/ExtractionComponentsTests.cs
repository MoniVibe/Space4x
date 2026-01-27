using NUnit.Framework;
using PureDOTS.Runtime.Extraction;
using Unity.Mathematics;

namespace PureDOTS.Tests.EditMode.Extraction
{
    /// <summary>
    /// Unit tests for game-agnostic extraction components.
    /// </summary>
    public class ExtractionComponentsTests
    {
        [Test]
        public void HarvestSlot_Create_SetsDefaultValues()
        {
            var offset = new float3(1, 0, 2);
            var slot = HarvestSlot.Create(offset, 0.8f);

            Assert.AreEqual(offset, slot.LocalOffset, "Offset should match input");
            Assert.AreEqual(0.8f, slot.EfficiencyMultiplier, 0.001f, "Efficiency should match input");
            Assert.AreEqual(Unity.Entities.Entity.Null, slot.AssignedAgent, "Agent should be null");
            Assert.AreEqual(0u, slot.LastHarvestTick, "LastHarvestTick should be 0");
            Assert.AreEqual(0, slot.Flags, "Flags should be 0");
        }

        [Test]
        public void HarvestSlotFlags_Blocked_IsSetCorrectly()
        {
            byte flags = HarvestSlotFlags.Blocked | HarvestSlotFlags.RequiresSpecialist;

            Assert.IsTrue((flags & HarvestSlotFlags.Blocked) != 0, "Blocked flag should be set");
            Assert.IsTrue((flags & HarvestSlotFlags.RequiresSpecialist) != 0, 
                "RequiresSpecialist flag should be set");
            Assert.IsFalse((flags & HarvestSlotFlags.PendingArrival) != 0, 
                "PendingArrival flag should not be set");
        }

        [Test]
        public void ExtractionRequestStatus_HasExpectedOrder()
        {
            Assert.AreEqual(0, (byte)ExtractionRequestStatus.Pending);
            Assert.AreEqual(1, (byte)ExtractionRequestStatus.Assigned);
            Assert.AreEqual(2, (byte)ExtractionRequestStatus.InProgress);
            Assert.AreEqual(3, (byte)ExtractionRequestStatus.Completed);
            Assert.AreEqual(4, (byte)ExtractionRequestStatus.Cancelled);
        }

        [Test]
        public void ExtractionConfig_Default_HasReasonableValues()
        {
            var config = ExtractionConfig.Default;

            Assert.Greater(config.MaxRequestsPerTick, 0, "Should process at least 1 request per tick");
            Assert.Greater(config.RequestTimeoutSeconds, 0f, "Should have positive timeout");
        }
    }
}

