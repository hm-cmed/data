namespace CodemedX.Core
{
    /// <summary>
    /// event_type のカタログ。文字列リテラルを直接書かず必ずここを経由する
    /// （LMS 側の集計クエリがタイポで壊れるのを防ぐため）。
    /// シナリオ固有のイベントを足すときは、まずこのファイルに定数を追加する。
    /// </summary>
    public static class EventTypes
    {
        // --- 全シナリオ共通のライフサイクル ---
        public const string SessionStarted = "SessionStarted";
        public const string ScenarioStarted = "ScenarioStarted";
        public const string StateChanged = "StateChanged";
        public const string ScenarioCompleted = "ScenarioCompleted";
        public const string ScenarioFailed = "ScenarioFailed";
        public const string ScenarioAborted = "ScenarioAborted";

        // --- 対話 ---
        public const string DialogueNodeEntered = "DialogueNodeEntered";
        public const string DialogueSelected = "DialogueSelected";
        public const string SilenceRespected = "SilenceRespected";
        public const string AffectThresholdCrossed = "AffectThresholdCrossed";
        public const string ProtocolStepCompleted = "ProtocolStepCompleted";

        // --- 空間観察 ---
        public const string RiskSignObserved = "RiskSignObserved";
        public const string ObservationMissed = "ObservationMissed";

        // --- 判断・行動 ---
        public const string TriageAction = "TriageAction";
        public const string LabConfirmed = "LabConfirmed";
        public const string EvidenceAttached = "EvidenceAttached";
        public const string AssessmentItemChecked = "AssessmentItemChecked";
        public const string AssessmentSubmitted = "AssessmentSubmitted";
        public const string EscalationPerformed = "EscalationPerformed";
        public const string ReferralAgreed = "ReferralAgreed";
        public const string SafetyPlanAgreed = "SafetyPlanAgreed";

        // --- 環境・システム ---
        public const string LocomotionModeChanged = "LocomotionModeChanged";
        public const string ComfortBreakTaken = "ComfortBreakTaken";
    }
}
