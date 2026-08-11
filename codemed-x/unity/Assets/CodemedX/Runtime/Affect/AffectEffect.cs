using System;
using UnityEngine;

namespace CodemedX.Affect
{
    /// <summary>
    /// 選択肢が NPC の隠れパラメータに与える影響。
    /// 「患者の意思のみを肯定すると、患者の Trust は +20、家族の Trust は -10 / Anxiety は +15」のような
    /// 非対称な変動を、対話グラフのデータとして表現するための最小単位。
    /// </summary>
    [Serializable]
    public sealed class AffectEffect
    {
        [SerializeField, Tooltip("影響を受ける AffectActor の ActorId。")]
        private string targetActorId = "npc";

        [SerializeField] private AffectParameter parameter = AffectParameter.Trust;

        [SerializeField, Range(-100f, 100f)] private float delta;

        public string TargetActorId { get { return targetActorId; } }
        public AffectParameter Parameter { get { return parameter; } }
        public float Delta { get { return delta; } }

        public AffectEffect() { }

        public AffectEffect(string targetActorId, AffectParameter parameter, float delta)
        {
            this.targetActorId = targetActorId;
            this.parameter = parameter;
            this.delta = delta;
        }
    }

    /// <summary>
    /// 選択肢を選べる条件。
    /// 例: ⑤ の「死にたいと考えていますか？」は Trust が 50 以上でなければ時期尚早となる。
    /// </summary>
    [Serializable]
    public sealed class AffectCondition
    {
        [SerializeField] private string targetActorId = "npc";
        [SerializeField] private AffectParameter parameter = AffectParameter.Trust;
        [SerializeField] private AffectComparison comparison = AffectComparison.AtLeast;
        [SerializeField, Range(AffectState.Min, AffectState.Max)] private float value = 50f;

        public string TargetActorId { get { return targetActorId; } }
        public AffectParameter Parameter { get { return parameter; } }
        public AffectComparison Comparison { get { return comparison; } }
        public float Value { get { return value; } }

        public AffectCondition() { }

        public AffectCondition(
            string targetActorId, AffectParameter parameter, AffectComparison comparison, float value)
        {
            this.targetActorId = targetActorId;
            this.parameter = parameter;
            this.comparison = comparison;
            this.value = value;
        }

        public bool IsSatisfiedBy(AffectActor actor)
        {
            return actor != null && actor.Satisfies(parameter, comparison, value);
        }
    }
}
