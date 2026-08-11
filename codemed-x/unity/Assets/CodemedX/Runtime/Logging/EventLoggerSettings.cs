using UnityEngine;

namespace CodemedX.Logging
{
    /// <summary>
    /// ログ送信の設定。Resources/CodemedXEventLoggerSettings.asset として配置すると
    /// <see cref="EventLogger"/> が自動で読み込む。
    ///
    /// 認証トークンとユーザーID用ソルトはここに置かない。ビルドに同梱される ScriptableObject は
    /// apk を展開すれば読めるため、実行時に <see cref="EventLogger.SetAuthToken"/> で注入する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CodemedXEventLoggerSettings",
        menuName = "Codemed-x/Event Logger Settings",
        order = 0)]
    public class EventLoggerSettings : ScriptableObject
    {
        public const string DefaultResourcePath = "CodemedXEventLoggerSettings";

        [Header("送信先")]
        [SerializeField, Tooltip("API Gateway の URL。空にするとオフライン（スプールのみ）で動作する。")]
        private string endpointUrl = string.Empty;

        [Header("バッチ")]
        [SerializeField, Range(1, 200), Tooltip("1 バッチに詰める最大イベント数。")]
        private int maxBatchSize = 25;

        [SerializeField, Range(1f, 120f), Tooltip("定期フラッシュの間隔（秒）。")]
        private float flushIntervalSeconds = 10f;

        [SerializeField, Range(1, 60), Tooltip("HTTP リクエストのタイムアウト（秒）。")]
        private int requestTimeoutSeconds = 15;

        [Header("認証")]
        [SerializeField, Tooltip(
            "認証情報の載せ方。Google Apps Script を受け皿にする場合のみ QueryParameter を選ぶ" +
            "（GAS は Authorization ヘッダを受け取れないため）。")]
        private HttpEventSink.AuthMode authMode = HttpEventSink.AuthMode.BearerHeader;

        [SerializeField, Tooltip("QueryParameter 方式のときのパラメータ名。")]
        private string authQueryParameterName = "token";

        [Header("再送")]
        [SerializeField, Range(0, 10), Tooltip("1 バッチあたりの最大再送回数。使い切るとスプールへ退避する。")]
        private int maxRetryCount = 4;

        [SerializeField, Range(0.5f, 10f)] private float initialRetryDelaySeconds = 2f;
        [SerializeField, Range(1f, 4f)] private float retryBackoffMultiplier = 2f;
        [SerializeField, Range(5f, 300f)] private float maxRetryDelaySeconds = 60f;

        [Header("オフライン")]
        [SerializeField, Tooltip("送れなかったイベントを端末内に JSON Lines で退避し、次回起動時に再送する。")]
        private bool enableOfflineSpool = true;

        [SerializeField] private string spoolFileName = "codemedx_events.jsonl";

        [SerializeField, Range(1, 100), Tooltip("スプールファイルの上限（MB）。超えたら古い行から捨てる。")]
        private int spoolMaxSizeMegabytes = 8;

        [Header("開発")]
        [SerializeField, Tooltip("送信したイベントを Console にも出す。")]
        private bool logToConsole;

        public string EndpointUrl { get { return endpointUrl; } }
        public int MaxBatchSize { get { return maxBatchSize; } }
        public float FlushIntervalSeconds { get { return flushIntervalSeconds; } }
        public int RequestTimeoutSeconds { get { return requestTimeoutSeconds; } }
        public HttpEventSink.AuthMode AuthMode { get { return authMode; } }
        public string AuthQueryParameterName { get { return authQueryParameterName; } }
        public int MaxRetryCount { get { return maxRetryCount; } }
        public bool EnableOfflineSpool { get { return enableOfflineSpool; } }
        public string SpoolFileName { get { return spoolFileName; } }
        public long SpoolMaxSizeBytes { get { return (long)spoolMaxSizeMegabytes * 1024L * 1024L; } }
        public bool LogToConsole { get { return logToConsole; } }

        /// <summary>
        /// 指定回目の再送までの待ち時間（秒）。指数バックオフに ±20% のジッタを掛け、
        /// 演習で 30 台の HMD が一斉に復帰したときにサーバへ同時再送が集中しないようにする。
        /// </summary>
        public float GetRetryDelaySeconds(int retryAttempt)
        {
            if (retryAttempt < 0)
            {
                retryAttempt = 0;
            }

            float delay = initialRetryDelaySeconds * Mathf.Pow(retryBackoffMultiplier, retryAttempt);
            delay = Mathf.Min(delay, maxRetryDelaySeconds);
            return delay * Random.Range(0.8f, 1.2f);
        }

        /// <summary>テスト・エディタ拡張から設定を組み立てるためのファクトリ。</summary>
        public static EventLoggerSettings CreateDefault(string endpointUrl = "")
        {
            EventLoggerSettings settings = CreateInstance<EventLoggerSettings>();
            settings.endpointUrl = endpointUrl;
            return settings;
        }
    }
}
