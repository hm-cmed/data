using System;
using UnityEngine;

namespace CodemedX.Affect
{
    /// <summary>
    /// NPC 1 体分の隠れパラメータ。すべて 0-100 にクランプされる。
    /// 「学習者には見えないが行動を規定する値」であり、デブリーフィングで初めて開示するのが基本運用。
    /// </summary>
    [Serializable]
    public sealed class AffectState
    {
        public const float Min = 0f;
        public const float Max = 100f;

        [SerializeField, Range(Min, Max)] private float trust = 50f;
        [SerializeField, Range(Min, Max)] private float anxiety = 30f;
        [SerializeField, Range(Min, Max)] private float urgency = 0f;
        [SerializeField, Range(Min, Max)] private float comprehension = 50f;
        [SerializeField, Range(Min, Max)] private float pressure = 0f;

        /// <summary>(パラメータ, 変更前, 変更後)。閾値判定はこのイベントを購読して行う。</summary>
        public event Action<AffectParameter, float, float> ParameterChanged;

        public float Trust { get { return trust; } }
        public float Anxiety { get { return anxiety; } }
        public float Urgency { get { return urgency; } }
        public float Comprehension { get { return comprehension; } }
        public float Pressure { get { return pressure; } }

        public float Get(AffectParameter parameter)
        {
            switch (parameter)
            {
                case AffectParameter.Trust: return trust;
                case AffectParameter.Anxiety: return anxiety;
                case AffectParameter.Urgency: return urgency;
                case AffectParameter.Comprehension: return comprehension;
                case AffectParameter.Pressure: return pressure;
                default: return 0f;
            }
        }

        public void Set(AffectParameter parameter, float value)
        {
            float clamped = Mathf.Clamp(value, Min, Max);
            float previous = Get(parameter);

            switch (parameter)
            {
                case AffectParameter.Trust: trust = clamped; break;
                case AffectParameter.Anxiety: anxiety = clamped; break;
                case AffectParameter.Urgency: urgency = clamped; break;
                case AffectParameter.Comprehension: comprehension = clamped; break;
                case AffectParameter.Pressure: pressure = clamped; break;
            }

            // クランプで値が動かなかった場合は通知しない（閾値イベントの多重発火を防ぐ）。
            if (!Mathf.Approximately(previous, clamped) && ParameterChanged != null)
            {
                ParameterChanged(parameter, previous, clamped);
            }
        }

        public void Apply(AffectParameter parameter, float delta)
        {
            Set(parameter, Get(parameter) + delta);
        }

        public bool Satisfies(AffectParameter parameter, AffectComparison comparison, float threshold)
        {
            float value = Get(parameter);
            return comparison == AffectComparison.AtLeast ? value >= threshold : value <= threshold;
        }

        public string ToPayloadFragment()
        {
            return string.Format(
                "trust={0:0.#},anxiety={1:0.#},urgency={2:0.#},comprehension={3:0.#},pressure={4:0.#}",
                trust, anxiety, urgency, comprehension, pressure);
        }
    }

    public enum AffectComparison
    {
        AtLeast = 0,
        AtMost = 1
    }
}
