using System;
using System.Collections;
using System.Collections.Generic;
using CodemedX.Core;
using UnityEngine;

namespace CodemedX.Logging
{
    /// <summary>
    /// 全シナリオ共通のログ送信口。シナリオ側は <see cref="Log(string,string,float,string,string)"/> を
    /// 呼ぶだけでよく、バッチ化・再送・オフライン退避はすべてここが引き受ける。
    ///
    /// 設計原則（HMD アプリはシンクライアント）:
    /// - 合否判定やスコアの正規化はサーバ側で行う。端末は観測した事実を送るだけ。
    /// - 認証情報は C# にもアセットにも埋め込まず <see cref="SetAuthToken"/> で実行時に注入する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EventLogger : MonoBehaviour
    {
        private static EventLogger _instance;
        private static bool _isQuitting;

        private readonly Queue<TrainingEvent> _queue = new Queue<TrainingEvent>();
        private readonly List<ITrainingEventSink> _sinks = new List<ITrainingEventSink>();

        private EventLoggerSettings _settings;
        private EventSpool _spool;
        private HttpEventSink _httpSink;
        private SessionContext _session;
        private bool _isSending;
        private bool _missingSessionReported;

        /// <summary>ログが積まれた瞬間に発火する。デブリーフィング用 UI やテストの購読口。</summary>
        public event Action<TrainingEvent> EventLogged;

        public static EventLogger Instance
        {
            get
            {
                if (_instance == null && !_isQuitting)
                {
                    // FindObjectOfType は Unity 6 で非推奨。
                    _instance = FindAnyObjectByType<EventLogger>();

                    if (_instance == null)
                    {
                        GameObject host = new GameObject("[Codemed-x] EventLogger");
                        _instance = host.AddComponent<EventLogger>();
                    }
                }

                return _instance;
            }
        }

        public SessionContext Session { get { return _session; } }

        public int PendingEventCount { get { return _queue.Count; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            _settings = Resources.Load<EventLoggerSettings>(EventLoggerSettings.DefaultResourcePath);

            // UnityEngine.Object の null は ?? 演算子で拾えないため明示的に判定する。
            if (_settings == null)
            {
                _settings = EventLoggerSettings.CreateDefault();
                Debug.LogWarning(
                    "[Codemed-x] Resources/" + EventLoggerSettings.DefaultResourcePath +
                    " が見つかりません。既定値（送信先なし・スプールのみ）で動作します。");
            }

            if (_settings.EnableOfflineSpool)
            {
                _spool = new EventSpool(_settings.SpoolFileName, _settings.SpoolMaxSizeBytes);
            }

            if (!string.IsNullOrEmpty(_settings.EndpointUrl))
            {
                _httpSink = new HttpEventSink(
                    _settings.EndpointUrl,
                    _settings.RequestTimeoutSeconds,
                    _settings.AuthMode,
                    _settings.AuthQueryParameterName);
                _sinks.Add(_httpSink);
            }

            if (_settings.LogToConsole)
            {
                _sinks.Add(new DebugLogSink());
            }

            RestoreSpooledEvents();
            StartCoroutine(FlushLoop());
        }

        /// <summary>シナリオ開始時に一度だけ呼ぶ。以降のイベントはこのセッションに紐づく。</summary>
        public void BeginSession(SessionContext session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            _session = session;
            _missingSessionReported = false;
            Log(EventTypes.SessionStarted);
        }

        /// <summary>認証トークンを実行時に注入する。</summary>
        public void SetAuthToken(string authToken)
        {
            if (_httpSink != null)
            {
                _httpSink.SetAuthToken(authToken);
            }
        }

        public void AddSink(ITrainingEventSink sink)
        {
            if (sink != null && !_sinks.Contains(sink))
            {
                _sinks.Add(sink);
            }
        }

        public TrainingEvent Log(
            string eventType,
            string objectiveId = "",
            float score = 0f,
            string errorType = ErrorTypes.None,
            string payloadJson = "")
        {
            if (_session == null)
            {
                if (!_missingSessionReported)
                {
                    _missingSessionReported = true;
                    Debug.LogError(
                        "[Codemed-x] BeginSession() より前にログが発生したため破棄しました: " + eventType);
                }

                return null;
            }

            TrainingEvent trainingEvent = _session.CreateEvent(eventType, objectiveId, score, errorType, payloadJson);
            Log(trainingEvent);
            return trainingEvent;
        }

        public void Log(TrainingEvent trainingEvent)
        {
            if (trainingEvent == null)
            {
                return;
            }

            _queue.Enqueue(trainingEvent);

            if (EventLogged != null)
            {
                EventLogged(trainingEvent);
            }

            // シナリオの終了イベントは取りこぼしが致命的なので即時フラッシュする。
            if (trainingEvent.event_type == EventTypes.ScenarioCompleted
                || trainingEvent.event_type == EventTypes.ScenarioFailed
                || trainingEvent.event_type == EventTypes.ScenarioAborted)
            {
                RequestFlush();
            }
        }

        /// <summary>次のフレームを待たずに送信を試みる。</summary>
        public void RequestFlush()
        {
            if (!_isSending && isActiveAndEnabled)
            {
                StartCoroutine(FlushOnce());
            }
        }

        private IEnumerator FlushLoop()
        {
            while (true)
            {
                // Time.timeScale = 0 のポーズ中でもログは送りたいので Realtime で待つ。
                yield return new WaitForSecondsRealtime(_settings.FlushIntervalSeconds);
                yield return FlushOnce();
            }
        }

        private IEnumerator FlushOnce()
        {
            if (_isSending || _queue.Count == 0)
            {
                yield break;
            }

            _isSending = true;
            try
            {
                while (_queue.Count > 0)
                {
                    List<TrainingEvent> chunk = DequeueChunk(_settings.MaxBatchSize);
                    yield return SendWithRetry(chunk);
                }
            }
            finally
            {
                _isSending = false;
            }
        }

        private List<TrainingEvent> DequeueChunk(int maxCount)
        {
            List<TrainingEvent> chunk = new List<TrainingEvent>(maxCount);
            while (chunk.Count < maxCount && _queue.Count > 0)
            {
                chunk.Add(_queue.Dequeue());
            }

            return chunk;
        }

        private IEnumerator SendWithRetry(List<TrainingEvent> events)
        {
            if (_sinks.Count == 0)
            {
                // 送信先が一つも無い構成（完全オフライン運用）ではスプールが唯一の保存先になる。
                Spool(events);
                yield break;
            }

            TrainingEventBatch batch = TrainingEventBatch.Create(events.ToArray());

            // 成功済みのシンクへ再送しないよう、未達のシンクだけを持ち回る。
            List<ITrainingEventSink> pending = new List<ITrainingEventSink>(_sinks);

            for (int attempt = 0; attempt <= _settings.MaxRetryCount && pending.Count > 0; attempt++)
            {
                batch.retry_count = attempt;
                bool retryable = false;

                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    ITrainingEventSink sink = pending[i];
                    SinkResult result = SinkResult.RetryableFailure;
                    yield return sink.Send(batch, r => result = r);

                    if (result == SinkResult.Success)
                    {
                        pending.RemoveAt(i);
                    }
                    else if (result == SinkResult.RetryableFailure)
                    {
                        retryable = true;
                    }
                    else
                    {
                        // 恒久的失敗はリトライしても通らないので、このシンクは諦める。
                        Debug.LogError(string.Format(
                            "[Codemed-x] シンク {0} がバッチ {1} を恒久的に拒否しました。",
                            sink.Name, batch.batch_id));
                        pending.RemoveAt(i);
                    }
                }

                if (pending.Count == 0 || !retryable)
                {
                    break;
                }

                if (attempt < _settings.MaxRetryCount)
                {
                    yield return new WaitForSecondsRealtime(_settings.GetRetryDelaySeconds(attempt));
                }
            }

            if (pending.Count > 0)
            {
                Spool(events);
            }
        }

        private void Spool(List<TrainingEvent> events)
        {
            if (_spool != null)
            {
                _spool.Append(events);
                Debug.LogWarning(string.Format(
                    "[Codemed-x] {0} 件のイベントを送信できずスプールへ退避しました。", events.Count));
                return;
            }

            Debug.LogError(string.Format(
                "[Codemed-x] {0} 件のイベントを送信できず、スプールも無効なため失われました。", events.Count));
        }

        private void RestoreSpooledEvents()
        {
            if (_spool == null || !_spool.HasPendingEvents)
            {
                return;
            }

            List<TrainingEvent> restored = _spool.DrainAll();
            foreach (TrainingEvent trainingEvent in restored)
            {
                _queue.Enqueue(trainingEvent);
            }

            Debug.Log(string.Format(
                "[Codemed-x] 前回未送信の {0} 件をスプールから復元しました。", restored.Count));
        }

        private void OnApplicationPause(bool isPaused)
        {
            // HMD を外すとアプリはいつでも止まりうる。コルーチンの完了を待てないので同期的に退避する。
            if (isPaused)
            {
                SpoolQueuedEvents();
            }
            else
            {
                RestoreSpooledEvents();
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            SpoolQueuedEvents();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void SpoolQueuedEvents()
        {
            if (_queue.Count == 0)
            {
                return;
            }

            List<TrainingEvent> remaining = DequeueChunk(_queue.Count);
            Spool(remaining);
        }
    }
}
