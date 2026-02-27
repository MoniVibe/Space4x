#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Space4x.Scenario;

namespace Space4X.Headless
{
    public class Space4XCapitalRangeHeadlessQuestionTests
    {
        [Test]
        public void CapitalRangeQuestionPack_ResolvesRegisteredQuestions()
        {
            var metrics = new Dictionary<string, float>
            {
                { "space4x.gunnery.capital_range.score", 72f },
                { "space4x.gunnery.capital_range.shots.fired", 120f },
                { "space4x.gunnery.capital_range.shots.hit", 54f },
                { "space4x.gunnery.capital_range.hit_rate", 0.45f },
                { "space4x.gunnery.capital_range.reaction_time_s", 3.1f },
                { "space4x.gunnery.capital_range.ttk_s", 28.5f },
                { "space4x.gunnery.capital_range.lock_churn", 10f },
                { "space4x.gunnery.capital_range.drone_assist", 1f },
                { "space4x.gunnery.capital_range.combatants.destroyed", 3f }
            };

            var signals = new Space4XOperatorSignals(metrics, new List<Space4XOperatorBlackCat>());
            var runtime = new Space4XScenarioRuntime
            {
                StartTick = 100u,
                EndTick = 7300u,
                DurationSeconds = 120f
            };

            var questionPack = new Dictionary<string, bool>
            {
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeScore, true },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeHitRate, true },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeReactionTime, true },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeTtk, false },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeLockChurn, false },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeDroneAssist, false }
            };

            var answers = Space4XHeadlessQuestionRegistry.BuildQuestions(
                signals,
                default,
                runtime,
                questionPack);

            Assert.AreEqual(questionPack.Count, answers.Count);
            for (var i = 0; i < answers.Count; i++)
            {
                Assert.AreNotEqual("unregistered_question", answers[i].UnknownReason);
            }

            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeScore, Space4XQuestionStatus.Pass);
            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeHitRate, Space4XQuestionStatus.Pass);
            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeReactionTime, Space4XQuestionStatus.Pass);
        }

        [Test]
        public void CapitalRangeRequiredQuestions_FailWhenNoShotsRecorded()
        {
            var metrics = new Dictionary<string, float>
            {
                { "space4x.gunnery.capital_range.score", 0f },
                { "space4x.gunnery.capital_range.shots.fired", 0f },
                { "space4x.gunnery.capital_range.shots.hit", 0f },
                { "space4x.gunnery.capital_range.hit_rate", 0f },
                { "space4x.gunnery.capital_range.reaction_time_s", -1f },
                { "space4x.gunnery.capital_range.lock_churn", 0f }
            };

            var signals = new Space4XOperatorSignals(metrics, new List<Space4XOperatorBlackCat>());
            var runtime = new Space4XScenarioRuntime
            {
                StartTick = 0u,
                EndTick = 7200u,
                DurationSeconds = 120f
            };

            var questionPack = new Dictionary<string, bool>
            {
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeScore, true },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeHitRate, true },
                { Space4XHeadlessQuestionIds.GunneryCapitalRangeReactionTime, true }
            };

            var answers = Space4XHeadlessQuestionRegistry.BuildQuestions(
                signals,
                default,
                runtime,
                questionPack);

            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeScore, Space4XQuestionStatus.Fail);
            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeHitRate, Space4XQuestionStatus.Fail);
            AssertQuestionStatus(answers, Space4XHeadlessQuestionIds.GunneryCapitalRangeReactionTime, Space4XQuestionStatus.Fail);
        }

        private static void AssertQuestionStatus(List<Space4XQuestionAnswer> answers, string id, Space4XQuestionStatus expectedStatus)
        {
            for (var i = 0; i < answers.Count; i++)
            {
                if (answers[i].Id != id)
                {
                    continue;
                }

                Assert.AreEqual(expectedStatus, answers[i].Status, id);
                return;
            }

            Assert.Fail($"Question '{id}' not found.");
        }
    }
}
#endif
