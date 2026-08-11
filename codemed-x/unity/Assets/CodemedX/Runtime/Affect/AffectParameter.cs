namespace CodemedX.Affect
{
    /// <summary>
    /// NPC が内部に持つ「隠れパラメータ」。5 シナリオすべてがこの 4 軸の組み合わせで表現できる。
    /// - ① 相談援助面接: Trust（保護者の感情値）
    /// - ② ACP: Trust / Anxiety / Comprehension（患者と家族で非対称に動く）
    /// - ③ 夜勤: 患者の Anxiety（不穏）
    /// - ④ 疑義照会: Pressure（医師の権威勾配）
    /// - ⑤ ゲートキーパー: Trust / Urgency（切迫度）
    /// </summary>
    public enum AffectParameter
    {
        /// <summary>信頼度。低いと開示が止まり、面談が打ち切られる。</summary>
        Trust = 0,

        /// <summary>不安・動揺。高いと感情的激高や不穏行動へ遷移する。</summary>
        Anxiety = 1,

        /// <summary>切迫度。⑤ の希死念慮など、表面上は見えないリスクの高さ。</summary>
        Urgency = 2,

        /// <summary>理解度。説明が伝わっているか。低いまま同意を取ると不適切な意思決定になる。</summary>
        Comprehension = 3,

        /// <summary>相手から掛かる圧力。④ の権威勾配（AuthorityPressure）に使う。</summary>
        Pressure = 4
    }
}
