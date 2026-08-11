using System;
using System.Collections;
using UnityEngine;

namespace CodemedX.Logging
{
    /// <summary>
    /// バッチを Console に出すだけのシンク。サーバ未接続の段階でシナリオ実装を進めるために使う。
    /// </summary>
    public sealed class DebugLogSink : ITrainingEventSink
    {
        public string Name { get { return "debug"; } }

        public IEnumerator Send(TrainingEventBatch batch, Action<SinkResult> onComplete)
        {
            Debug.Log(string.Format(
                "[Codemed-x] batch {0} ({1} events)\n{2}",
                batch.batch_id,
                batch.events != null ? batch.events.Length : 0,
                batch.ToJson()));

            onComplete(SinkResult.Success);
            yield break;
        }
    }
}
