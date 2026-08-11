namespace CodemedX.Core
{
    /// <summary>
    /// error_type のカタログ。教育的に意味のある「やってはいけない行動」を型として持たせ、
    /// デブリーフィング時に学習者へ提示できるようにする。
    /// </summary>
    public static class ErrorTypes
    {
        /// <summary>エラーなし。error_type を空文字にしてはならない。</summary>
        public const string None = "none";

        // --- ① 相談援助面接（児童相談所・生活保護等） ---
        /// <summary>高圧的・戦闘的な発言で相手を追い詰めた。</summary>
        public const string CombativeSpeech = "CombativeSpeech";
        /// <summary>重要なリスクサインを見落としたまま次の段階へ進んだ。</summary>
        public const string MissedRiskSign = "MissedRiskSign";
        /// <summary>観察した事実と自身の解釈を混同して記録・報告した。</summary>
        public const string FactInterpretationConflated = "FactInterpretationConflated";
        /// <summary>法的要件を満たさない（または満たすのに行わない）保護判断。</summary>
        public const string UnjustifiedCustodyDecision = "UnjustifiedCustodyDecision";

        // --- ② 困難な対話（ACP / SPIKES） ---
        /// <summary>患者の沈黙を遮って説明を続けた。</summary>
        public const string InterruptedSilence = "InterruptedSilence";
        /// <summary>SPIKES の必要ステップを飛ばした。</summary>
        public const string ProtocolStepSkipped = "ProtocolStepSkipped";
        /// <summary>専門用語をかみ砕かずに悪い知らせを伝えた。</summary>
        public const string JargonOverload = "JargonOverload";
        /// <summary>患者・家族の一方のみを支持して対立を悪化させた。</summary>
        public const string StakeholderNeglected = "StakeholderNeglected";

        // --- ③ 夜勤・複数患者対応 ---
        /// <summary>緊急度の低いタスクを優先し、高緊急タスクを後回しにした。</summary>
        public const string PriorityInversion = "PriorityInversion";
        /// <summary>高優先タスクを許容時間を超えて放置した。</summary>
        public const string CriticalTaskNeglected = "CriticalTaskNeglected";
        /// <summary>応援要請・報告が遅れた、または行われなかった。</summary>
        public const string DelayedEscalation = "DelayedEscalation";
        /// <summary>患者確認・ダブルチェック等の基本的予防策を省略した。</summary>
        public const string SafetyCheckOmitted = "SafetyCheckOmitted";

        // --- ④ 薬剤師：疑義照会 ---
        /// <summary>疑義を確認しないまま調剤へ進んだ。</summary>
        public const string InquirySkipped = "InquirySkipped";
        /// <summary>検査値等の根拠を示さず曖昧に照会した。</summary>
        public const string EvidenceNotPresented = "EvidenceNotPresented";
        /// <summary>権威勾配に屈して主張を取り下げた。</summary>
        public const string YieldedToAuthority = "YieldedToAuthority";

        // --- ⑤ ゲートキーパー ---
        /// <summary>「頑張って」等の安易な励ましで相手の開示を止めた。</summary>
        public const string InappropriateEncouragement = "InappropriateEncouragement";
        /// <summary>信頼が形成される前に踏み込んだ質問をした。</summary>
        public const string PrematureProbing = "PrematureProbing";
        /// <summary>希死念慮の直接確認を最後まで行わなかった。</summary>
        public const string RiskAssessmentIncomplete = "RiskAssessmentIncomplete";
        /// <summary>専門機関へつながないまま対話を終えた。</summary>
        public const string ReferralOmitted = "ReferralOmitted";

        // --- システム ---
        /// <summary>時間切れ。</summary>
        public const string Timeout = "Timeout";
    }
}
