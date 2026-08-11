using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace CodemedX.Logging
{
    /// <summary>
    /// API Gateway へ JSON を POST するシンク。
    /// HMD アプリはシンクライアントであり、判定ロジックをサーバへ持たせないため、
    /// ここで行うのは「バッチをそのまま届ける」ことだけ。
    /// </summary>
    public sealed class HttpEventSink : ITrainingEventSink
    {
        private const string BatchIdHeader = "X-Codemedx-Batch-Id";
        private const string SchemaVersionHeader = "X-Codemedx-Schema-Version";

        /// <summary>認証情報の載せ方。</summary>
        public enum AuthMode
        {
            /// <summary>Authorization: Bearer &lt;token&gt;。API Gateway 等の通常の送信先はこちら。</summary>
            BearerHeader = 0,

            /// <summary>
            /// ?token=&lt;token&gt;。Google Apps Script のウェブアプリは Authorization ヘッダを
            /// 受け取れないため、その場合だけこちらを使う。
            /// </summary>
            QueryParameter = 1
        }

        private readonly string _endpointUrl;
        private readonly int _timeoutSeconds;
        private readonly AuthMode _authMode;
        private readonly string _authQueryParameterName;
        private string _authToken;

        public HttpEventSink(
            string endpointUrl,
            int timeoutSeconds = 15,
            AuthMode authMode = AuthMode.BearerHeader,
            string authQueryParameterName = "token")
        {
            _endpointUrl = endpointUrl;
            _timeoutSeconds = Mathf.Max(1, timeoutSeconds);
            _authMode = authMode;
            _authQueryParameterName = authQueryParameterName;
        }

        public string Name { get { return "http"; } }

        /// <summary>認証トークンを実行時に注入する。C# ソースにも ScriptableObject にも埋め込まないこと。</summary>
        public void SetAuthToken(string authToken)
        {
            _authToken = authToken;
        }

        public IEnumerator Send(TrainingEventBatch batch, Action<SinkResult> onComplete)
        {
            if (string.IsNullOrEmpty(_endpointUrl))
            {
                onComplete(SinkResult.PermanentFailure);
                yield break;
            }

            string json = batch.ToJson();

            using (UnityWebRequest request = UnityWebRequest.Post(BuildUrl(), json, "application/json"))
            {
                request.timeout = _timeoutSeconds;
                request.SetRequestHeader(BatchIdHeader, batch.batch_id);
                request.SetRequestHeader(SchemaVersionHeader, batch.schema_version);

                if (!string.IsNullOrEmpty(_authToken) && _authMode == AuthMode.BearerHeader)
                {
                    request.SetRequestHeader("Authorization", "Bearer " + _authToken);
                }

                yield return request.SendWebRequest();

                onComplete(Classify(request));
            }
        }

        /// <summary>
        /// トークンは URL 側に焼き込まず、送信の直前に組み立てる。
        /// EventLoggerSettings（=ビルドに同梱されるアセット）へ書かないための措置。
        /// </summary>
        private string BuildUrl()
        {
            if (_authMode != AuthMode.QueryParameter || string.IsNullOrEmpty(_authToken))
            {
                return _endpointUrl;
            }

            string separator = _endpointUrl.Contains("?") ? "&" : "?";
            return _endpointUrl + separator + _authQueryParameterName + "="
                + UnityWebRequest.EscapeURL(_authToken);
        }

        private static SinkResult Classify(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success
                && request.responseCode >= 200 && request.responseCode < 300)
            {
                return SinkResult.Success;
            }

            // 通信断・タイムアウトは端末側の事情なので必ず再送する。
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return SinkResult.RetryableFailure;
            }

            long code = request.responseCode;

            // 408 Request Timeout / 429 Too Many Requests / 5xx はサーバ側の一時的な問題。
            if (code == 0 || code == 408 || code == 429 || code >= 500)
            {
                return SinkResult.RetryableFailure;
            }

            // 400 系（スキーマ違反・認証失敗）は同じ内容を送り直しても通らない。
            return SinkResult.PermanentFailure;
        }
    }
}
