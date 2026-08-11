using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CodemedX.Core
{
    /// <summary>
    /// 1 セッション分の不変メタデータと連番カウンタ。
    /// <see cref="TrainingEvent"/> の生成は必ずここを通し、共通フィールドの詰め忘れを起こさない。
    /// </summary>
    public sealed class SessionContext
    {
        private long _sequence = -1;

        public string UserId { get; private set; }
        public string SessionId { get; private set; }
        public string ScenarioId { get; private set; }
        public string DeviceModel { get; private set; }
        public string BuildVersion { get; private set; }

        /// <summary>移動方式は途中で切り替わりうるので唯一の可変項目。</summary>
        public string LocomotionMode { get; private set; }

        public SessionContext(
            string anonymousUserId,
            string scenarioId,
            string locomotionMode = LocomotionModes.Unknown,
            string sessionId = null,
            string deviceModel = null,
            string buildVersion = null)
        {
            if (string.IsNullOrEmpty(anonymousUserId))
            {
                throw new ArgumentException("anonymousUserId は必須です。", "anonymousUserId");
            }

            if (string.IsNullOrEmpty(scenarioId))
            {
                throw new ArgumentException("scenarioId は必須です。", "scenarioId");
            }

            UserId = anonymousUserId;
            ScenarioId = scenarioId;
            SessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString() : sessionId;
            DeviceModel = string.IsNullOrEmpty(deviceModel) ? SystemInfo.deviceModel : deviceModel;
            BuildVersion = string.IsNullOrEmpty(buildVersion) ? Application.version : buildVersion;
            LocomotionMode = LocomotionModes.IsValid(locomotionMode) ? locomotionMode : LocomotionModes.Unknown;
        }

        public void SetLocomotionMode(string locomotionMode)
        {
            LocomotionMode = LocomotionModes.IsValid(locomotionMode) ? locomotionMode : LocomotionModes.Unknown;
        }

        /// <summary>共通フィールドを埋めた <see cref="TrainingEvent"/> を発行する。</summary>
        public TrainingEvent CreateEvent(
            string eventType,
            string objectiveId = "",
            float score = 0f,
            string errorType = ErrorTypes.None,
            string payloadJson = "")
        {
            return new TrainingEvent
            {
                schema_version = TrainingEvent.CurrentSchemaVersion,
                user_id = UserId,
                session_id = SessionId,
                scenario_id = ScenarioId,
                event_type = eventType,
                objective_id = objectiveId ?? string.Empty,
                event_timestamp_unix_ms = UtcNowUnixMilliseconds(),
                sequence = Interlocked.Increment(ref _sequence),
                score = score,
                error_type = string.IsNullOrEmpty(errorType) ? ErrorTypes.None : errorType,
                device_model = DeviceModel,
                build_version = BuildVersion,
                locomotion_mode = LocomotionMode,
                payload_json = payloadJson ?? string.Empty
            };
        }

        public static long UtcNowUnixMilliseconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        /// <summary>
        /// 学籍番号等の生の識別子をソルト付き SHA-256 で匿名化する。
        /// ソルトはビルドに埋め込まず、実行時に安全な経路（MDM の設定、初回起動時の入力等）から渡すこと。
        /// </summary>
        public static string CreateAnonymousUserId(string rawIdentifier, string salt, int lengthInHexChars = 16)
        {
            if (string.IsNullOrEmpty(rawIdentifier))
            {
                throw new ArgumentException("rawIdentifier は必須です。", "rawIdentifier");
            }

            if (string.IsNullOrEmpty(salt))
            {
                throw new ArgumentException(
                    "salt は必須です。ソルト無しのハッシュは総当たりで元の識別子に戻せるため許可しません。", "salt");
            }

            if (lengthInHexChars < 8 || lengthInHexChars > 64)
            {
                throw new ArgumentOutOfRangeException("lengthInHexChars", "8〜64 の範囲で指定してください。");
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + rawIdentifier));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString(0, lengthInHexChars);
            }
        }
    }
}
