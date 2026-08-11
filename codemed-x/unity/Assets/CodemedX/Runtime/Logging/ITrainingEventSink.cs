using System;
using System.Collections;

namespace CodemedX.Logging
{
    public enum SinkResult
    {
        /// <summary>送信成功。バッチは破棄してよい。</summary>
        Success,

        /// <summary>一時的な失敗（通信断、タイムアウト、429、5xx）。バックオフして再送する。</summary>
        RetryableFailure,

        /// <summary>恒久的な失敗（400、401、404 等）。再送しても無駄なのでスプールへ退避する。</summary>
        PermanentFailure
    }

    /// <summary>
    /// バッチの送信先。<see cref="EventLogger"/> がキューイング・バッチ化・再送を担い、
    /// シンクは「1 バッチを届ける」ことだけに責任を持つ。
    /// UnityWebRequest をそのまま扱えるようコルーチンとして定義している。
    /// </summary>
    public interface ITrainingEventSink
    {
        string Name { get; }

        IEnumerator Send(TrainingEventBatch batch, Action<SinkResult> onComplete);
    }
}
