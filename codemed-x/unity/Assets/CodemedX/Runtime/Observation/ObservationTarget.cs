using System;
using CodemedX.Core;
using CodemedX.Logging;
using UnityEngine;

namespace CodemedX.Observation
{
    /// <summary>
    /// 空間内の「観察されるべきもの」。
    /// ① の虐待リスクサイン（散乱した酒瓶、不自然な生活痕）、③ の生体モニタ、④ の検査値パネルなど、
    /// 5 シナリオすべての「気づけたか / 見落としたか」をこの 1 コンポーネントで表す。
    ///
    /// 注視（Gaze）で発見扱いにするには <see cref="GazeDwellTracker"/> を、
    /// クリック／グラブで発見扱いにするには <see cref="Discover"/> を UI 側から呼ぶ。
    /// </summary>
    [DisallowMultipleComponent]
    public class ObservationTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("ログに残す識別子。例: kitchen_liquor_bottles")]
        private string targetId = "observation_target";

        [SerializeField, Tooltip("schema/objectives.csv の objective_id。")]
        private string objectiveId = string.Empty;

        [SerializeField, Tooltip("発見扱いにするまでの注視時間（秒）。")]
        private float requiredDwellSeconds = 2f;

        [SerializeField, Tooltip("発見時の加点。")]
        private float score = 1f;

        [SerializeField, Tooltip("見落とすと重大な影響がある項目か。完了時の未発見チェックに使う。")]
        private bool isCritical;

        [SerializeField, TextArea(1, 3), Tooltip("デブリーフィングで提示する解説。")]
        private string debriefNote = string.Empty;

        /// <summary>(対象, 注視秒数)</summary>
        public event Action<ObservationTarget, float> Discovered;

        public string TargetId { get { return targetId; } }
        public string ObjectiveId { get { return objectiveId; } }
        public float RequiredDwellSeconds { get { return requiredDwellSeconds; } }
        public float Score { get { return score; } }
        public bool IsCritical { get { return isCritical; } }
        public string DebriefNote { get { return debriefNote; } }
        public bool IsDiscovered { get; private set; }

        /// <summary>この対象を「発見した」ものとして記録する。二重発見は無視する。</summary>
        public void Discover(float dwellSeconds)
        {
            if (IsDiscovered)
            {
                return;
            }

            IsDiscovered = true;

            EventLogger.Instance.Log(
                EventTypes.RiskSignObserved,
                objectiveId,
                score,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("target", targetId)
                    .Add("dwell_sec", dwellSeconds)
                    .Add("critical", isCritical)
                    .Build());

            if (Discovered != null)
            {
                Discovered(this, dwellSeconds);
            }
        }

        /// <summary>
        /// 未発見のまま局面が終わったことを記録する。
        /// 「見落とし」は学習上もっとも重要なデータなので、必ず明示的にログへ残す。
        /// </summary>
        public void ReportMissed()
        {
            if (IsDiscovered)
            {
                return;
            }

            EventLogger.Instance.Log(
                EventTypes.ObservationMissed,
                objectiveId,
                0f,
                isCritical ? ErrorTypes.MissedRiskSign : ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("target", targetId)
                    .Add("critical", isCritical)
                    .Build());
        }
    }
}
