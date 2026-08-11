using UnityEngine;

namespace CodemedX.Core
{
    /// <summary>
    /// 学習者の匿名IDを保持する唯一の場所。
    /// 氏名・学籍番号などの生の識別子はここから先へ渡さない（ログにも payload にも載せない）。
    ///
    /// 運用フロー:
    ///   LMS ログイン / QR / MDM 配布設定 → <see cref="SetFromRawIdentifier"/> でハッシュ化
    ///   → 以降アプリ内では <see cref="AnonymousUserId"/> しか存在しない。
    /// </summary>
    public static class LearnerIdentity
    {
        private static string _anonymousUserId;
        private static bool _isDevelopmentId;

        /// <summary>匿名化済みの学習者ID。未設定なら開発用IDを生成して返す。</summary>
        public static string AnonymousUserId
        {
            get
            {
                if (string.IsNullOrEmpty(_anonymousUserId))
                {
                    UseDevelopmentId();
                }

                return _anonymousUserId;
            }
        }

        /// <summary>本番の学習者IDではなく、端末から生成した開発用IDを使っているか。</summary>
        public static bool IsDevelopmentId { get { return _isDevelopmentId; } }

        /// <summary>生の識別子をソルト付きでハッシュ化して設定する。ソルトは実行時に安全な経路で受け取る。</summary>
        public static void SetFromRawIdentifier(string rawIdentifier, string salt)
        {
            _anonymousUserId = SessionContext.CreateAnonymousUserId(rawIdentifier, salt);
            _isDevelopmentId = false;
        }

        /// <summary>LMS 側で既に匿名化済みのIDを受け取った場合に使う。</summary>
        public static void SetAnonymized(string anonymousUserId)
        {
            if (string.IsNullOrEmpty(anonymousUserId))
            {
                return;
            }

            _anonymousUserId = anonymousUserId;
            _isDevelopmentId = false;
        }

        public static void Clear()
        {
            _anonymousUserId = null;
            _isDevelopmentId = false;
        }

        private static void UseDevelopmentId()
        {
            // 端末固有IDを固定ソルトでハッシュ化しただけの値。学習者の同定には使えないが、
            // 開発中に「同じ端末の一連の試行」をまとめるには十分。
            _anonymousUserId = SessionContext.CreateAnonymousUserId(
                SystemInfo.deviceUniqueIdentifier, "codemedx-development-salt");
            _isDevelopmentId = true;

            Debug.LogWarning(
                "[Codemed-x] 学習者IDが未設定のため開発用IDを使用します。" +
                "本番では LearnerIdentity.SetFromRawIdentifier() を必ず呼んでください。");
        }
    }
}
