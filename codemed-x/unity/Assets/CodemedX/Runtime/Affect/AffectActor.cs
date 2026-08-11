using System;
using System.Collections.Generic;
using CodemedX.Core;
using CodemedX.Logging;
using UnityEngine;

namespace CodemedX.Affect
{
    /// <summary>
    /// 閾値を跨いだときに発火する状態遷移条件。
    /// 例: 家族の Anxiety が 80 以上で「感情的激高状態」へ。
    /// </summary>
    [Serializable]
    public sealed class AffectThreshold
    {
        [SerializeField] private AffectParameter parameter = AffectParameter.Trust;
        [SerializeField] private AffectComparison comparison = AffectComparison.AtMost;
        [SerializeField, Range(AffectState.Min, AffectState.Max)] private float threshold = 30f;

        [SerializeField, Tooltip("遷移先の状態名。NPC の演技・音声の切り替えキーに使う。")]
        private string stateName = "Withdrawn";

        [SerializeField, Tooltip("この遷移に紐づく評価項目ID（schema/objectives.csv）。")]
        private string objectiveId = string.Empty;

        [SerializeField, Tooltip("一度だけ発火させる。")]
        private bool fireOnce = true;

        public AffectParameter Parameter { get { return parameter; } }
        public AffectComparison Comparison { get { return comparison; } }
        public float Threshold { get { return threshold; } }
        public string StateName { get { return stateName; } }
        public string ObjectiveId { get { return objectiveId; } }
        public bool FireOnce { get { return fireOnce; } }

        public bool IsSatisfiedBy(float value)
        {
            return comparison == AffectComparison.AtLeast ? value >= threshold : value <= threshold;
        }
    }

    /// <summary>
    /// 隠れパラメータを持つ NPC（保護者・患者・家族・医師・住民）に付けるコンポーネント。
    /// パラメータが閾値を跨いだ瞬間に <see cref="ThresholdCrossed"/> を発火し、
    /// 同時に AffectThresholdCrossed イベントをログへ送る。
    /// </summary>
    [DisallowMultipleComponent]
    public class AffectActor : MonoBehaviour
    {
        [SerializeField, Tooltip("対話グラフから参照される識別子。例: guardian, patient, family, doctor")]
        private string actorId = "npc";

        [SerializeField] private AffectState state = new AffectState();

        [SerializeField, Tooltip("上から順に評価する。最初に満たされたものが現在の状態名になる。")]
        private List<AffectThreshold> thresholds = new List<AffectThreshold>();

        private readonly HashSet<AffectThreshold> _firedThresholds = new HashSet<AffectThreshold>();

        /// <summary>(アクター, 跨いだ閾値)</summary>
        public event Action<AffectActor, AffectThreshold> ThresholdCrossed;

        public string ActorId { get { return actorId; } }
        public AffectState State { get { return state; } }

        /// <summary>直近に成立した閾値の状態名。アニメーション・音声の分岐に使う。</summary>
        public string CurrentStateName { get; private set; }

        protected virtual void OnEnable()
        {
            state.ParameterChanged += HandleParameterChanged;
        }

        protected virtual void OnDisable()
        {
            state.ParameterChanged -= HandleParameterChanged;
        }

        public void Apply(AffectParameter parameter, float delta)
        {
            state.Apply(parameter, delta);
        }

        public bool Satisfies(AffectParameter parameter, AffectComparison comparison, float threshold)
        {
            return state.Satisfies(parameter, comparison, threshold);
        }

        private void HandleParameterChanged(AffectParameter parameter, float previous, float current)
        {
            for (int i = 0; i < thresholds.Count; i++)
            {
                AffectThreshold threshold = thresholds[i];
                if (threshold.Parameter != parameter)
                {
                    continue;
                }

                // 「跨いだ瞬間」だけを拾う。値が閾値の内側で動き続けても再発火させない。
                bool satisfiedBefore = threshold.IsSatisfiedBy(previous);
                bool satisfiedNow = threshold.IsSatisfiedBy(current);
                if (satisfiedBefore || !satisfiedNow)
                {
                    continue;
                }

                if (threshold.FireOnce && !_firedThresholds.Add(threshold))
                {
                    continue;
                }

                CurrentStateName = threshold.StateName;
                Fire(threshold, current);
            }
        }

        private void Fire(AffectThreshold threshold, float value)
        {
            string payload = PayloadBuilder.Create()
                .Add("actor", actorId)
                .Add("parameter", threshold.Parameter.ToString())
                .Add("value", value)
                .Add("state", threshold.StateName)
                .Build();

            EventLogger.Instance.Log(
                EventTypes.AffectThresholdCrossed,
                threshold.ObjectiveId,
                0f,
                ErrorTypes.None,
                payload);

            if (ThresholdCrossed != null)
            {
                ThresholdCrossed(this, threshold);
            }
        }
    }
}
