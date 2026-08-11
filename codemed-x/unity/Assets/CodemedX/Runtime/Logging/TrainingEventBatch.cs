using System;
using CodemedX.Core;
using UnityEngine;

namespace CodemedX.Logging
{
    /// <summary>
    /// API Gateway への POST ボディ。HMD は電波断・スリープが日常的に起きるため、
    /// イベントは 1 件ずつではなく必ずバッチで送る。
    /// フィールド名は schema/training-event-batch.schema.json と 1:1 で対応する。
    /// </summary>
    [Serializable]
    public class TrainingEventBatch
    {
        public string schema_version = TrainingEvent.CurrentSchemaVersion;

        /// <summary>バッチ単位の冪等キー。再送時も同じ値を使うのでサーバ側で重複排除できる。</summary>
        public string batch_id;

        public long sent_at_unix_ms;

        /// <summary>何回目の送信試行か。0 = 初回。</summary>
        public int retry_count;

        public TrainingEvent[] events;

        public static TrainingEventBatch Create(TrainingEvent[] events)
        {
            return new TrainingEventBatch
            {
                schema_version = TrainingEvent.CurrentSchemaVersion,
                batch_id = Guid.NewGuid().ToString(),
                sent_at_unix_ms = SessionContext.UtcNowUnixMilliseconds(),
                retry_count = 0,
                events = events
            };
        }

        public string ToJson()
        {
            // 再送のたびに送信時刻と試行回数を更新する（batch_id は据え置き＝冪等）。
            sent_at_unix_ms = SessionContext.UtcNowUnixMilliseconds();
            return JsonUtility.ToJson(this);
        }
    }
}
