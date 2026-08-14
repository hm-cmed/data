using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CodemedX.Gatekeeper
{
    /// <summary>
    /// 学習イベント 1 件。
    /// フィールド名は codemed-x/schema/training-event.schema.json と 1:1 で対応させてある。
    /// このシナリオ単体で完結させるため、共通基盤には依存せず同じ形を持たせている
    /// （他のシナリオを作るときも同じ形にすれば、LMS 側は 1 種類の受け口で全部を受けられる）。
    /// </summary>
    [Serializable]
    public class TrainingEvent
    {
        public string schema_version = "1.0";
        public string user_id;
        public string session_id;
        public string scenario_id;
        public string event_type;
        public string objective_id = string.Empty;
        public long event_timestamp_unix_ms;
        public long sequence;
        public float score;
        public string error_type = "none";
        public string device_model;
        public string build_version;
        public string locomotion_mode;
        public string payload_json = string.Empty;

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    /// <summary>event_type の値。文字列を直接書かず必ずここを経由する。</summary>
    public static class EventKind
    {
        public const string ScenarioStarted = "ScenarioStarted";
        public const string StateChanged = "StateChanged";
        public const string DialogueSelected = "DialogueSelected";
        public const string AssessmentItemChecked = "AssessmentItemChecked";
        public const string AffectThresholdCrossed = "AffectThresholdCrossed";
        public const string SafetyPlanAgreed = "SafetyPlanAgreed";
        public const string ReferralAgreed = "ReferralAgreed";
        public const string ScenarioCompleted = "ScenarioCompleted";
        public const string ScenarioFailed = "ScenarioFailed";
    }

    /// <summary>error_type の値。教育的に意味のある逸脱に名前を付けておく。</summary>
    public static class ErrorKind
    {
        public const string None = "none";

        /// <summary>安易な励ましで相手の開示を止めた。</summary>
        public const string InappropriateEncouragement = "InappropriateEncouragement";

        /// <summary>信頼が形成される前に踏み込んだ質問をした。</summary>
        public const string PrematureProbing = "PrematureProbing";

        /// <summary>希死念慮の直接確認を最後まで行わなかった。</summary>
        public const string RiskAssessmentIncomplete = "RiskAssessmentIncomplete";

        /// <summary>専門機関へつながないまま対話を終えた。</summary>
        public const string ReferralOmitted = "ReferralOmitted";

    }

    /// <summary>
    /// payload_json に入れるフラットな JSON を組み立てる。
    /// 文字列連結だとエスケープ漏れでログ全体が壊れるため、必ずこれを使う。
    /// </summary>
    public sealed class Payload
    {
        private readonly StringBuilder _builder = new StringBuilder("{");
        private bool _hasEntry;

        public static Payload New()
        {
            return new Payload();
        }

        public Payload Add(string key, string value)
        {
            return Raw(key, "\"" + Escape(value ?? string.Empty) + "\"");
        }

        public Payload Add(string key, bool value)
        {
            return Raw(key, value ? "true" : "false");
        }

        public Payload Add(string key, int value)
        {
            return Raw(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public Payload Add(string key, float value)
        {
            // ロケール依存の小数点（"2,1"）で JSON を壊さないよう InvariantCulture を強制する。
            return Raw(key, value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public string Build()
        {
            return _builder.ToString() + "}";
        }

        private Payload Raw(string key, string rawValue)
        {
            if (_hasEntry)
            {
                _builder.Append(',');
            }

            _builder.Append('"').Append(Escape(key)).Append("\":").Append(rawValue);
            _hasEntry = true;
            return this;
        }

        private static string Escape(string value)
        {
            StringBuilder escaped = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"')
                {
                    escaped.Append("\\\"");
                }
                else if (c == '\\')
                {
                    escaped.Append("\\\\");
                }
                else if (c == '\n')
                {
                    escaped.Append("\\n");
                }
                else if (c == '\r')
                {
                    escaped.Append("\\r");
                }
                else if (c < 0x20)
                {
                    escaped.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    escaped.Append(c);
                }
            }

            return escaped.ToString();
        }
    }

    /// <summary>
    /// 学習履歴の記録先。まずは Console と端末内ファイルに書くだけにしてある。
    /// サーバへ送るのは、シナリオが動くようになってからで間に合う。
    /// </summary>
    public sealed class GatekeeperTrainingLog
    {
        private readonly List<TrainingEvent> _events = new List<TrainingEvent>();
        private readonly string _scenarioId;
        private readonly string _sessionId;
        private readonly string _userId;
        private long _sequence = -1;

        public GatekeeperTrainingLog(string scenarioId, string userId)
        {
            _scenarioId = scenarioId;
            _sessionId = Guid.NewGuid().ToString();
            _userId = string.IsNullOrEmpty(userId) ? "anonymous" : userId;
        }

        public IReadOnlyList<TrainingEvent> Events { get { return _events; } }

        public string SessionId { get { return _sessionId; } }

        /// <summary>Console にも出すか。開発中は true が便利。</summary>
        public bool EchoToConsole { get; set; }

        public TrainingEvent Log(
            string eventType,
            string objectiveId = "",
            float score = 0f,
            string errorType = ErrorKind.None,
            string payloadJson = "")
        {
            TrainingEvent trainingEvent = new TrainingEvent
            {
                user_id = _userId,
                session_id = _sessionId,
                scenario_id = _scenarioId,
                event_type = eventType,
                objective_id = objectiveId ?? string.Empty,
                event_timestamp_unix_ms = UtcNowUnixMilliseconds(),
                sequence = ++_sequence,
                score = score,
                error_type = string.IsNullOrEmpty(errorType) ? ErrorKind.None : errorType,
                device_model = SystemInfo.deviceModel,
                build_version = Application.version,
                locomotion_mode = "desktop",
                payload_json = payloadJson ?? string.Empty
            };

            _events.Add(trainingEvent);

            if (EchoToConsole)
            {
                Debug.Log("[Gatekeeper] " + trainingEvent.ToJson());
            }

            return trainingEvent;
        }

        /// <summary>JSON Lines で保存する。戻り値は保存先のフルパス。</summary>
        public string SaveToFile()
        {
            string path = Path.Combine(
                Application.persistentDataPath, _scenarioId + "_" + _sessionId + ".jsonl");

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < _events.Count; i++)
            {
                builder.Append(_events[i].ToJson()).Append('\n');
            }

            try
            {
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                return path;
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[Gatekeeper] ログの保存に失敗しました: " + exception.Message);
                return string.Empty;
            }
        }

        public static long UtcNowUnixMilliseconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
