using CodemedX.Assessment;
using CodemedX.Core;
using UnityEngine;

namespace CodemedX.Scenarios
{
    /// <summary>
    /// 1 シナリオのメタ情報。scenario_id をコードに散らさず、この 1 アセットに集約する。
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDefinition", menuName = "Codemed-x/Scenario Definition", order = 2)]
    public class ScenarioDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("スキーマの規約に従い snake_case + _v<番号>。例: welfare_abuse_v1")]
        private string scenarioId = "unnamed_v1";

        [SerializeField] private string displayName = "無題のシナリオ";

        [SerializeField, TextArea(2, 5)] private string description = string.Empty;

        [SerializeField, Tooltip("制限時間（秒）。0 なら無制限。")]
        private float timeLimitSeconds;

        [SerializeField, Tooltip("デブリーフィング画面で「到達」と表示する暫定の閾値。確定判定はサーバ側で行う。")]
        private float provisionalPassingScore = 60f;

        [SerializeField] private ObjectiveCatalog objectiveCatalog;

        [SerializeField, Tooltip("既定の移動方式。実行時に切り替えたら SessionContext.SetLocomotionMode() を呼ぶ。")]
        private LocomotionModeOption defaultLocomotionMode = LocomotionModeOption.Teleport;

        public enum LocomotionModeOption
        {
            Teleport = 0,
            Continuous = 1,
            Seated = 2,
            Desktop = 3
        }

        public string ScenarioId { get { return scenarioId; } }
        public string DisplayName { get { return displayName; } }
        public string Description { get { return description; } }
        public float TimeLimitSeconds { get { return timeLimitSeconds; } }
        public bool HasTimeLimit { get { return timeLimitSeconds > 0f; } }
        public float ProvisionalPassingScore { get { return provisionalPassingScore; } }
        public ObjectiveCatalog ObjectiveCatalog { get { return objectiveCatalog; } }

        public string DefaultLocomotionMode
        {
            get
            {
                switch (defaultLocomotionMode)
                {
                    case LocomotionModeOption.Teleport: return LocomotionModes.Teleport;
                    case LocomotionModeOption.Continuous: return LocomotionModes.Continuous;
                    case LocomotionModeOption.Seated: return LocomotionModes.Seated;
                    case LocomotionModeOption.Desktop: return LocomotionModes.Desktop;
                    default: return LocomotionModes.Unknown;
                }
            }
        }

        private void OnValidate()
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(scenarioId, "^[a-z0-9_]+_v[0-9]+$"))
            {
                Debug.LogWarning(
                    "[Codemed-x] scenario_id \"" + scenarioId + "\" はスキーマの書式に合いません " +
                    "(^[a-z0-9_]+_v[0-9]+$)。LMS 側の集計から漏れます。", this);
            }
        }
    }
}
