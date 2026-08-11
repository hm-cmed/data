using System.Globalization;
using System.Text;

namespace CodemedX.Core
{
    /// <summary>
    /// payload_json に入れるフラットな JSON 文字列を組み立てる。
    /// JsonUtility は Dictionary を扱えず、文字列連結はエスケープ漏れでログ全体を壊すため、
    /// payload はすべてこのビルダー経由で作る。
    ///
    /// 使い方:
    /// <code>
    /// string payload = PayloadBuilder.Create()
    ///     .Add("target", "kitchen_liquor_bottles")
    ///     .Add("dwell_sec", 2.1f)
    ///     .Build();
    /// </code>
    /// </summary>
    public sealed class PayloadBuilder
    {
        /// <summary>スキーマ上の payload_json の上限。超過分は切り捨てる。</summary>
        public const int MaxLength = 4096;

        private readonly StringBuilder _builder = new StringBuilder("{");
        private bool _hasEntry;

        public static PayloadBuilder Create()
        {
            return new PayloadBuilder();
        }

        public PayloadBuilder Add(string key, string value)
        {
            return AppendRaw(key, "\"" + Escape(value ?? string.Empty) + "\"");
        }

        public PayloadBuilder Add(string key, bool value)
        {
            return AppendRaw(key, value ? "true" : "false");
        }

        public PayloadBuilder Add(string key, int value)
        {
            return AppendRaw(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public PayloadBuilder Add(string key, long value)
        {
            return AppendRaw(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public PayloadBuilder Add(string key, float value)
        {
            // ロケール依存の小数点（"2,1"）で JSON を壊さないよう InvariantCulture を強制する。
            return AppendRaw(key, value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public string Build()
        {
            string json = _builder.ToString() + "}";
            return json.Length <= MaxLength ? json : json.Substring(0, MaxLength);
        }

        private PayloadBuilder AppendRaw(string key, string rawValue)
        {
            if (_hasEntry)
            {
                _builder.Append(',');
            }

            _builder.Append('"').Append(Escape(key)).Append("\":").Append(rawValue);
            _hasEntry = true;
            return this;
        }

        private static string Escape(string value)
        {
            StringBuilder escaped = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            escaped.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            escaped.Append(c);
                        }

                        break;
                }
            }

            return escaped.ToString();
        }
    }
}
