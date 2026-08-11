using System.Collections.Generic;
using CodemedX.Core;
using CodemedX.Logging;
using NUnit.Framework;
using UnityEngine;

namespace CodemedX.Tests
{
    /// <summary>
    /// 共通ログの形が崩れると 5 シナリオすべての学習履歴が同時に壊れるため、
    /// JSON のキー名とシリアライズの往復をテストで固定する。
    /// </summary>
    public class TrainingEventTests
    {
        /// <summary>schema/training-event.schema.json が要求するキー。</summary>
        private static readonly string[] RequiredKeys =
        {
            "schema_version", "user_id", "session_id", "scenario_id", "event_type",
            "objective_id", "event_timestamp_unix_ms", "sequence", "score", "error_type",
            "device_model", "build_version", "locomotion_mode", "payload_json"
        };

        private static SessionContext CreateSession()
        {
            return new SessionContext(
                "a3f1c9e0b47d2856",
                "welfare_abuse_v1",
                LocomotionModes.Teleport,
                "5f2a1c88-2b3d-4a1e-9c77-0d51ab9e4c10",
                "Meta Quest 3",
                "0.1.0");
        }

        [Test]
        public void Json_contains_every_schema_key()
        {
            string json = CreateSession().CreateEvent(EventTypes.ScenarioStarted).ToJson();

            foreach (string key in RequiredKeys)
            {
                StringAssert.Contains("\"" + key + "\"", json);
            }
        }

        [Test]
        public void Json_round_trip_preserves_values()
        {
            TrainingEvent original = CreateSession().CreateEvent(
                EventTypes.DialogueSelected, "WLF-INT-02", -2f, ErrorTypes.CombativeSpeech, "{\"choice\":2}");

            TrainingEvent restored = TrainingEvent.FromJson(original.ToJson());

            Assert.AreEqual(original.user_id, restored.user_id);
            Assert.AreEqual(original.session_id, restored.session_id);
            Assert.AreEqual(original.scenario_id, restored.scenario_id);
            Assert.AreEqual(original.event_type, restored.event_type);
            Assert.AreEqual(original.objective_id, restored.objective_id);
            Assert.AreEqual(original.sequence, restored.sequence);
            Assert.AreEqual(original.error_type, restored.error_type);
            Assert.AreEqual(original.payload_json, restored.payload_json);
            Assert.AreEqual(original.score, restored.score, 0.0001f);
        }

        [Test]
        public void Sequence_starts_at_zero_and_increases()
        {
            SessionContext session = CreateSession();

            Assert.AreEqual(0, session.CreateEvent(EventTypes.ScenarioStarted).sequence);
            Assert.AreEqual(1, session.CreateEvent(EventTypes.DialogueSelected).sequence);
            Assert.AreEqual(2, session.CreateEvent(EventTypes.ScenarioCompleted).sequence);
        }

        [Test]
        public void Empty_error_type_becomes_none()
        {
            TrainingEvent trainingEvent = CreateSession().CreateEvent(EventTypes.TriageAction, "", 0f, "");

            Assert.AreEqual(ErrorTypes.None, trainingEvent.error_type);
        }

        [Test]
        public void Invalid_locomotion_mode_falls_back_to_unknown()
        {
            SessionContext session = new SessionContext("user", "welfare_abuse_v1", "flying");

            Assert.AreEqual(LocomotionModes.Unknown, session.LocomotionMode);
        }

        [Test]
        public void Anonymous_user_id_is_deterministic_and_salt_dependent()
        {
            string a = SessionContext.CreateAnonymousUserId("student-0001", "salt-A");
            string b = SessionContext.CreateAnonymousUserId("student-0001", "salt-A");
            string c = SessionContext.CreateAnonymousUserId("student-0001", "salt-B");

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(16, a.Length);
            StringAssert.DoesNotContain("student", a);
        }

        [Test]
        public void Anonymous_user_id_requires_a_salt()
        {
            Assert.Throws<System.ArgumentException>(
                () => SessionContext.CreateAnonymousUserId("student-0001", ""));
        }

        [Test]
        public void Batch_serializes_all_events()
        {
            SessionContext session = CreateSession();
            TrainingEventBatch batch = TrainingEventBatch.Create(new[]
            {
                session.CreateEvent(EventTypes.ScenarioStarted),
                session.CreateEvent(EventTypes.ScenarioCompleted)
            });

            string json = batch.ToJson();

            StringAssert.Contains("\"batch_id\"", json);
            StringAssert.Contains(EventTypes.ScenarioStarted, json);
            StringAssert.Contains(EventTypes.ScenarioCompleted, json);
        }

        [Test]
        public void Payload_builder_escapes_and_uses_invariant_numbers()
        {
            string payload = PayloadBuilder.Create()
                .Add("note", "改行\nと\"引用符\"")
                .Add("dwell_sec", 2.5f)
                .Add("critical", true)
                .Build();

            StringAssert.Contains("\\n", payload);
            StringAssert.Contains("\\\"", payload);
            StringAssert.Contains("\"dwell_sec\":2.5", payload);
            StringAssert.Contains("\"critical\":true", payload);
            Assert.IsTrue(payload.StartsWith("{") && payload.EndsWith("}"));
        }

        [Test]
        public void Payload_builder_result_is_parseable_by_json_utility()
        {
            string payload = PayloadBuilder.Create().Add("target", "kitchen_liquor_bottles").Build();

            PayloadProbe probe = JsonUtility.FromJson<PayloadProbe>(payload);

            Assert.AreEqual("kitchen_liquor_bottles", probe.target);
        }

        [Test]
        public void Retry_delay_grows_and_is_capped()
        {
            EventLoggerSettings settings = EventLoggerSettings.CreateDefault();
            List<float> delays = new List<float>();

            for (int attempt = 0; attempt < 8; attempt++)
            {
                delays.Add(settings.GetRetryDelaySeconds(attempt));
            }

            // ジッタがあるので厳密な単調増加ではなく、下限・上限の範囲だけを保証する。
            Assert.Less(delays[0], delays[4]);
            foreach (float delay in delays)
            {
                Assert.Greater(delay, 0f);
                Assert.LessOrEqual(delay, 60f * 1.2f + 0.001f);
            }
        }

        [System.Serializable]
        private class PayloadProbe
        {
            public string target;
        }
    }
}
