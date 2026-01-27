using NUnit.Framework;
using PureDOTS.Runtime.AI;
using Unity.Mathematics;

namespace PureDOTS.Tests.EditMode.AI
{
    /// <summary>
    /// Unit tests for game-agnostic AI components and helpers.
    /// </summary>
    public class AIComponentsTests
    {
        [Test]
        public void SteeringCalculations_Seek_ReturnsCorrectDirection()
        {
            var position = new float3(0, 0, 0);
            var target = new float3(10, 0, 0);
            var currentVelocity = float3.zero;
            var maxSpeed = 5f;

            var steering = SteeringCalculations.Seek(position, target, currentVelocity, maxSpeed);

            // Should steer toward target
            Assert.Greater(steering.x, 0f, "Seek should steer toward target (positive X)");
            Assert.AreEqual(0f, steering.y, 0.001f, "Seek should not have Y component");
            Assert.AreEqual(0f, steering.z, 0.001f, "Seek should not have Z component");
        }

        [Test]
        public void SteeringCalculations_Flee_ReturnsOppositeDirection()
        {
            var position = new float3(0, 0, 0);
            var target = new float3(10, 0, 0);
            var currentVelocity = float3.zero;
            var maxSpeed = 5f;

            var steering = SteeringCalculations.Flee(position, target, currentVelocity, maxSpeed);

            // Should steer away from target
            Assert.Less(steering.x, 0f, "Flee should steer away from target (negative X)");
        }

        [Test]
        public void SteeringCalculations_Arrive_SlowsNearTarget()
        {
            var position = new float3(0, 0, 0);
            var target = new float3(2, 0, 0); // Within slowing radius
            var currentVelocity = float3.zero;
            var maxSpeed = 10f;
            var slowingRadius = 5f;
            var stopRadius = 0.5f;

            var steering = SteeringCalculations.Arrive(
                position, target, currentVelocity, maxSpeed, slowingRadius, stopRadius);

            // Speed should be reduced since we're within slowing radius
            var speed = math.length(steering);
            Assert.Less(speed, maxSpeed, "Arrive should reduce speed within slowing radius");
        }

        [Test]
        public void SteeringCalculations_Arrive_StopsAtTarget()
        {
            var position = new float3(0, 0, 0);
            var target = new float3(0.1f, 0, 0); // Within stop radius
            var currentVelocity = new float3(1, 0, 0);
            var maxSpeed = 10f;
            var slowingRadius = 5f;
            var stopRadius = 0.5f;

            var steering = SteeringCalculations.Arrive(
                position, target, currentVelocity, maxSpeed, slowingRadius, stopRadius);

            // Should return negative of current velocity to stop
            Assert.Less(steering.x, 0f, "Arrive should brake when within stop radius");
        }

        [Test]
        public void UtilityCurveEvaluator_Linear_ReturnsCorrectValue()
        {
            var curve = new UtilityCurveDefinition
            {
                Type = CurveType.Linear,
                Slope = 2f,
                YShift = 1f,
                MinValue = 0f,
                MaxValue = 100f
            };

            var result = UtilityCurveEvaluator.Evaluate(curve, 3f);

            // y = 2 * 3 + 1 = 7
            Assert.AreEqual(7f, result, 0.001f, "Linear curve should evaluate correctly");
        }

        [Test]
        public void UtilityCurveEvaluator_Step_ReturnsCorrectValue()
        {
            var curve = new UtilityCurveDefinition
            {
                Type = CurveType.Step,
                XShift = 5f,
                MinValue = 0f,
                MaxValue = 1f
            };

            var belowThreshold = UtilityCurveEvaluator.Evaluate(curve, 3f);
            var aboveThreshold = UtilityCurveEvaluator.Evaluate(curve, 7f);

            Assert.AreEqual(0f, belowThreshold, "Step curve should return min below threshold");
            Assert.AreEqual(1f, aboveThreshold, "Step curve should return max above threshold");
        }

        [Test]
        public void UtilityCurveEvaluator_Clamps_ToMinMax()
        {
            var curve = new UtilityCurveDefinition
            {
                Type = CurveType.Linear,
                Slope = 100f,
                YShift = 0f,
                MinValue = 0f,
                MaxValue = 10f
            };

            var result = UtilityCurveEvaluator.Evaluate(curve, 1f);

            // y = 100 * 1 = 100, but clamped to max 10
            Assert.AreEqual(10f, result, 0.001f, "Curve should clamp to MaxValue");
        }

        [Test]
        public void SensorConfig_Default_HasReasonableValues()
        {
            var config = SensorConfig.Default;

            Assert.Greater(config.Range, 0f, "Default sensor should have positive range");
            Assert.Greater(config.FieldOfView, 0f, "Default sensor should have positive FOV");
            Assert.LessOrEqual(config.FieldOfView, 360f, "Default sensor FOV should not exceed 360");
            Assert.Greater(config.MaxTrackedTargets, 0, "Default sensor should track at least 1 target");
        }

        [Test]
        public void SensorConfig_Omnidirectional_Has360FOV()
        {
            var config = SensorConfig.Omnidirectional(100f);

            Assert.AreEqual(360f, config.FieldOfView, "Omnidirectional sensor should have 360 FOV");
            Assert.AreEqual(100f, config.Range, "Omnidirectional sensor should have specified range");
        }

        [Test]
        public void SteeringConfig_Default_HasReasonableValues()
        {
            var config = SteeringConfig.Default;

            Assert.Greater(config.MaxSpeed, 0f, "Default steering should have positive max speed");
            Assert.Greater(config.MaxAcceleration, 0f, "Default steering should have positive acceleration");
            Assert.Greater(config.ArriveSlowingRadius, config.ArriveStopRadius, 
                "Slowing radius should be greater than stop radius");
        }

        [Test]
        public void DetectionMask_AllFlag_IncludesAllTypes()
        {
            var all = DetectionMask.All;

            Assert.IsTrue((all & DetectionMask.Sight) != 0, "All should include Sight");
            Assert.IsTrue((all & DetectionMask.Sound) != 0, "All should include Sound");
            Assert.IsTrue((all & DetectionMask.Smell) != 0, "All should include Smell");
            Assert.IsTrue((all & DetectionMask.Proximity) != 0, "All should include Proximity");
        }
    }
}

