using NUnit.Framework;
using PureDOTS.Runtime.Groups;
using Unity.Entities;

namespace PureDOTS.Tests.EditMode.Groups
{
    /// <summary>
    /// Unit tests for game-agnostic group components.
    /// </summary>
    public class GroupComponentsTests
    {
        [Test]
        public void GroupConfig_Default_HasReasonableValues()
        {
            var config = GroupConfig.Default;

            Assert.Greater(config.MaxMembers, 0, "Default group should allow at least 1 member");
            Assert.GreaterOrEqual(config.MaxMembers, config.MinMembers, 
                "MaxMembers should be >= MinMembers");
            Assert.Greater(config.AggregationInterval, 0, 
                "Default should have positive aggregation interval");
        }

        [Test]
        public void GroupMemberFlags_Active_IsSetCorrectly()
        {
            var flags = GroupMemberFlags.Active | GroupMemberFlags.CanVote;

            Assert.IsTrue((flags & GroupMemberFlags.Active) != 0, "Active flag should be set");
            Assert.IsTrue((flags & GroupMemberFlags.CanVote) != 0, "CanVote flag should be set");
            Assert.IsFalse((flags & GroupMemberFlags.Away) != 0, "Away flag should not be set");
        }

        [Test]
        public void GroupRole_Leader_HasHigherValue()
        {
            Assert.Greater((byte)GroupRole.Leader, (byte)GroupRole.Member,
                "Leader role should have higher value than Member");
        }

        [Test]
        public void GroupStatus_Active_IsOperational()
        {
            var status = GroupStatus.Active;
            Assert.AreEqual(GroupStatus.Active, status);
        }

        [Test]
        public void GroupType_HasExpectedValues()
        {
            Assert.AreEqual(0, (byte)GroupType.Generic);
            Assert.AreEqual(1, (byte)GroupType.Military);
            Assert.AreEqual(2, (byte)GroupType.Guild);
        }

        [Test]
        public void GroupConfigFlags_CanCombine()
        {
            var flags = GroupConfigFlags.PersistWithoutLeader | GroupConfigFlags.CanMerge;

            Assert.IsTrue((flags & GroupConfigFlags.PersistWithoutLeader) != 0);
            Assert.IsTrue((flags & GroupConfigFlags.CanMerge) != 0);
            Assert.IsFalse((flags & GroupConfigFlags.CanSplit) != 0);
        }

        [Test]
        public void RemovalReason_HasExpectedValues()
        {
            Assert.AreEqual(0, (byte)RemovalReason.Left);
            Assert.AreEqual(1, (byte)RemovalReason.Expelled);
            Assert.AreEqual(2, (byte)RemovalReason.Died);
        }
    }
}

