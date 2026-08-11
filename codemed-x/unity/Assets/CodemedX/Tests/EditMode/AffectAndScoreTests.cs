using System.Collections.Generic;
using CodemedX.Affect;
using CodemedX.Assessment;
using CodemedX.Core;
using NUnit.Framework;

namespace CodemedX.Tests
{
    public class AffectStateTests
    {
        [Test]
        public void Values_are_clamped_to_zero_and_one_hundred()
        {
            AffectState state = new AffectState();

            state.Set(AffectParameter.Trust, 250f);
            Assert.AreEqual(100f, state.Trust, 0.001f);

            state.Set(AffectParameter.Trust, -40f);
            Assert.AreEqual(0f, state.Trust, 0.001f);
        }

        [Test]
        public void Apply_adds_a_delta()
        {
            AffectState state = new AffectState();
            state.Set(AffectParameter.Anxiety, 50f);

            state.Apply(AffectParameter.Anxiety, 15f);

            Assert.AreEqual(65f, state.Anxiety, 0.001f);
        }

        [Test]
        public void Change_event_does_not_fire_when_already_clamped()
        {
            AffectState state = new AffectState();
            state.Set(AffectParameter.Trust, 100f);

            int changeCount = 0;
            state.ParameterChanged += (parameter, previous, current) => changeCount++;

            state.Apply(AffectParameter.Trust, 20f);

            Assert.AreEqual(0, changeCount, "上限に張り付いた状態での加算で閾値イベントが多重発火してはいけない。");
        }

        [Test]
        public void Change_event_reports_previous_and_current()
        {
            AffectState state = new AffectState();
            state.Set(AffectParameter.Urgency, 40f);

            float reportedPrevious = -1f;
            float reportedCurrent = -1f;
            state.ParameterChanged += (parameter, previous, current) =>
            {
                reportedPrevious = previous;
                reportedCurrent = current;
            };

            state.Apply(AffectParameter.Urgency, 20f);

            Assert.AreEqual(40f, reportedPrevious, 0.001f);
            Assert.AreEqual(60f, reportedCurrent, 0.001f);
        }

        [Test]
        public void Threshold_is_only_satisfied_on_the_correct_side()
        {
            AffectThresholdProbe probe = new AffectThresholdProbe();

            Assert.IsTrue(probe.AtLeast50.IsSatisfiedBy(50f));
            Assert.IsTrue(probe.AtLeast50.IsSatisfiedBy(80f));
            Assert.IsFalse(probe.AtLeast50.IsSatisfiedBy(49f));
        }

        /// <summary>AffectThreshold のフィールドは Inspector 用に private なので、テスト用に JSON で組み立てる。</summary>
        private class AffectThresholdProbe
        {
            public readonly AffectThreshold AtLeast50 =
                UnityEngine.JsonUtility.FromJson<AffectThreshold>(
                    "{\"parameter\":0,\"comparison\":0,\"threshold\":50.0}");
        }
    }

    public class ScoreCardTests
    {
        [Test]
        public void Records_scores_per_objective()
        {
            ScoreCard card = new ScoreCard("welfare_abuse_v1");

            card.Record("WLF-OBS-01", 1f);
            card.Record("WLF-OBS-01", 1f);
            card.Record("WLF-INT-01", 3f);

            Assert.AreEqual(2f, card.Get("WLF-OBS-01").Score, 0.001f);
            Assert.AreEqual(2, card.Get("WLF-OBS-01").AttemptCount);
            Assert.AreEqual(5f, card.TotalScore, 0.001f);
        }

        [Test]
        public void Counts_errors_even_without_an_objective()
        {
            ScoreCard card = new ScoreCard("welfare_abuse_v1");

            card.Record(string.Empty, 0f, ErrorTypes.CombativeSpeech);
            card.Record("WLF-INT-02", -2f, ErrorTypes.CombativeSpeech);
            card.Record("WLF-INT-01", 3f);

            Assert.AreEqual(2, card.ErrorCount);
            Assert.AreEqual(1, card.Get("WLF-INT-02").ErrorCount);
        }

        [Test]
        public void Achievement_is_sticky()
        {
            ScoreCard card = new ScoreCard("welfare_abuse_v1");

            card.MarkAchieved("WLF-LAW-01");
            card.Record("WLF-LAW-01", -1f, ErrorTypes.None);

            Assert.IsTrue(card.IsAchieved("WLF-LAW-01"));
        }

        [Test]
        public void Summary_payload_is_valid_json_with_the_expected_fields()
        {
            ScoreCard card = new ScoreCard("welfare_abuse_v1");
            card.Record("WLF-OBS-01", 1f, ErrorTypes.None, true);
            card.Record("WLF-INT-02", -2f, ErrorTypes.CombativeSpeech);

            string payload = card.ToPayloadJson(390f);

            StringAssert.Contains("\"elapsed_sec\":390", payload);
            StringAssert.Contains("\"objectives_achieved\":1", payload);
            StringAssert.Contains("\"error_count\":1", payload);
        }

        [Test]
        public void Unmet_required_objectives_are_empty_without_a_catalog()
        {
            ScoreCard card = new ScoreCard("welfare_abuse_v1");

            List<string> unmet = card.GetUnmetRequiredObjectives();

            Assert.IsNotNull(unmet);
            Assert.AreEqual(0, unmet.Count);
        }
    }
}
