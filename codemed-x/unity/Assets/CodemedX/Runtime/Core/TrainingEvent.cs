using System;
using UnityEngine;

namespace CodemedX.Core
{
    /// <summary>
    /// Codemed-x 全シナリオ共通の学習イベント。
    /// フィールド名は schema/training-event.schema.json と 1:1 で対応するため、
    /// スキーマを変更せずにリネームしてはならない（JsonUtility はフィールド名をそのまま JSON キーにする）。
    /// </summary>
    [Serializable]
    public class TrainingEvent
    {
        /// <summary>スキーマの互換性を壊す変更を入れたときだけ上げる。</summary>
        public const string CurrentSchemaVersion = "1.0";

        public string schema_version = CurrentSchemaVersion;

        /// <summary>匿名化された学習者ID。生の氏名・学籍番号を入れてはならない。</summary>
        public string user_id;

        /// <summary>セッションごとの一意な GUID。</summary>
        public string session_id;

        /// <summary>例: welfare_abuse_v1, nightshift_multi_v1</summary>
        public string scenario_id;

        /// <summary>例: DialogueSelected, TriageAction, LabConfirmed（<see cref="EventTypes"/> のカタログを使う）。</summary>
        public string event_type;

        /// <summary>評価項目ID。schema/objectives.csv 経由でコアカリの id に紐づく。該当なしは空文字。</summary>
        public string objective_id = string.Empty;

        /// <summary>UTC のミリ秒 Unix タイムスタンプ。</summary>
        public long event_timestamp_unix_ms;

        /// <summary>
        /// セッション内の単調増加連番。ミリ秒タイムスタンプは同時刻で衝突しうるため、
        /// 順序の確定と再送時の重複排除（冪等キー）にはこちらを使う。
        /// </summary>
        public long sequence;

        /// <summary>行動に対するスコア（加減点、または進捗）。</summary>
        public float score;

        /// <summary>エラー種別。エラーなしは "none"（<see cref="ErrorTypes"/> のカタログを使う）。</summary>
        public string error_type = ErrorTypes.None;

        /// <summary>SystemInfo.deviceModel (Meta Quest 3, PC, etc.)</summary>
        public string device_model;

        /// <summary>Application.version</summary>
        public string build_version;

        /// <summary>移動方式 (teleport / continuous / seated / desktop)</summary>
        public string locomotion_mode;

        /// <summary>
        /// シナリオ固有の詳細を格納する JSON 文字列。
        /// 入れ子オブジェクトを避けるため、あえて文字列としてエスケープして持つ
        /// （JsonUtility が入れ子の可変構造を扱えないため、かつ LMS 側の列定義を固定するため）。
        /// 自由記述の生データや個人情報を入れてはならない。
        /// </summary>
        public string payload_json = string.Empty;

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static TrainingEvent FromJson(string json)
        {
            return JsonUtility.FromJson<TrainingEvent>(json);
        }

        public override string ToString()
        {
            return string.Format(
                "[{0}] {1} scenario={2} objective={3} score={4} error={5}",
                sequence, event_type, scenario_id, objective_id, score, error_type);
        }
    }
}
