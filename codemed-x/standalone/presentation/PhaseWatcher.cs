using System.Reflection;
using UnityEngine;

namespace CodemedX.Presentation
{
    /// <summary>
    /// シナリオ側の「いまどの局面か」を、依存を作らずに読み取るための小さな仕掛け。
    ///
    /// リフレクションを使っているのは、5 つのシナリオがそれぞれ独立したフォルダで完結しており、
    /// 共通のインターフェースを持たせると依存が生まれてしまうため。
    /// 「public string CurrentPhaseName を持っている」という緩い約束だけで繋いでいる。
    ///
    /// 演出側の部品（SimBackdrop / PanoramicBackdrop）から共有して使う。
    /// この判定を 1 か所に置いておかないと、部品を増やすたびに同じ探索が増えていく。
    /// </summary>
    public class PhaseWatcher
    {
        private const string PropertyName = "CurrentPhaseName";

        private MonoBehaviour _source;
        private PropertyInfo _property;

        /// <summary>実際に繋がったスクリプト。Inspector に見せ返すために公開している。</summary>
        public MonoBehaviour Source { get { return _source; } }

        /// <summary>最後に読み取った局面名。まだ読めていなければ空文字列。</summary>
        public string CurrentPhase { get; private set; }

        public bool IsBound { get { return _source != null && _property != null; } }

        public PhaseWatcher()
        {
            CurrentPhase = string.Empty;
        }

        /// <summary>
        /// 局面を持つスクリプトを探して繋ぐ。
        /// preferred が指定されていればそれだけを見る（1 シーンに複数のシナリオを置いた場合）。
        /// 未指定なら owner と同じ GameObject → シーン全体の順に探す。
        /// </summary>
        public void Bind(MonoBehaviour owner, MonoBehaviour preferred)
        {
            if (preferred != null)
            {
                _property = FindProperty(preferred);
                _source = _property != null ? preferred : null;

                if (_property == null)
                {
                    Debug.LogWarning(
                        "[Codemed-x] " + preferred.GetType().Name +
                        " に public string " + PropertyName +
                        " がないため、局面ごとの切り替えは行いません。",
                        owner);
                }

                return;
            }

            // 同じ GameObject を先に見る（1 シーン 1 シナリオの構成ではこれで当たる）。
            if (owner != null)
            {
                MonoBehaviour[] onSelf = owner.GetComponents<MonoBehaviour>();
                for (int i = 0; i < onSelf.Length; i++)
                {
                    if (TryUse(owner, onSelf[i]))
                    {
                        return;
                    }
                }
            }

            // 見つからなければシーン全体から探す。Awake で 1 回だけなので負荷は問題にならない。
            MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (TryUse(owner, all[i]))
                {
                    return;
                }
            }
        }

        /// <summary>局面が変わっていれば true。Update から毎フレーム呼ぶ。</summary>
        public bool Poll()
        {
            string phase = Read();
            if (phase == CurrentPhase)
            {
                return false;
            }

            CurrentPhase = phase;
            return true;
        }

        private bool TryUse(MonoBehaviour owner, MonoBehaviour candidate)
        {
            if (candidate == null || candidate == owner)
            {
                return false;
            }

            PropertyInfo property = FindProperty(candidate);
            if (property == null)
            {
                return false;
            }

            _source = candidate;
            _property = property;
            return true;
        }

        private string Read()
        {
            if (!IsBound)
            {
                return string.Empty;
            }

            object value = _property.GetValue(_source, null);
            return value as string ?? string.Empty;
        }

        private static PropertyInfo FindProperty(MonoBehaviour target)
        {
            PropertyInfo property = target.GetType().GetProperty(
                PropertyName, BindingFlags.Public | BindingFlags.Instance);

            return property != null && property.PropertyType == typeof(string) ? property : null;
        }
    }
}
