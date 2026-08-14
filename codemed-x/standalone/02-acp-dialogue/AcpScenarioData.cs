using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Acp
{
    /// <summary>学習者が選ぶ 1 つの発話。患者と家族に非対称に効く。</summary>
    [Serializable]
    public class AcpOption
    {
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;

        [SerializeField, Tooltip("患者の Trust への増減。")] private int patientTrust;
        [SerializeField, Tooltip("患者の Anxiety への増減。")] private int patientAnxiety;
        [SerializeField, Tooltip("患者の Comprehension への増減。")] private int patientComprehension;
        [SerializeField, Tooltip("家族の Trust への増減。")] private int familyTrust;
        [SerializeField, Tooltip("家族の Anxiety への増減。")] private int familyAnxiety;

        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private string errorKind = ErrorKind.None;

        [SerializeField, TextArea(2, 4)] private string reply = string.Empty;
        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Text { get { return text; } }
        public int PatientTrust { get { return patientTrust; } }
        public int PatientAnxiety { get { return patientAnxiety; } }
        public int PatientComprehension { get { return patientComprehension; } }
        public int FamilyTrust { get { return familyTrust; } }
        public int FamilyAnxiety { get { return familyAnxiety; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string ErrorKindName { get { return errorKind; } }
        public string Reply { get { return reply; } }
        public string DebriefNote { get { return debriefNote; } }

        public AcpOption() { }

        public AcpOption(
            string text,
            int patientTrust, int patientAnxiety, int patientComprehension,
            int familyTrust, int familyAnxiety,
            string objectiveId, string errorKind, string reply, string debriefNote)
        {
            this.text = text;
            this.patientTrust = patientTrust;
            this.patientAnxiety = patientAnxiety;
            this.patientComprehension = patientComprehension;
            this.familyTrust = familyTrust;
            this.familyAnxiety = familyAnxiety;
            this.objectiveId = objectiveId;
            this.errorKind = errorKind;
            this.reply = reply;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>SPIKES の 1 ステップ分の場面。</summary>
    [Serializable]
    public class AcpStep
    {
        [SerializeField, Tooltip("SPIKES のどのステップか（画面表示用）。")]
        private string stepLabel = string.Empty;

        [SerializeField] private string objectiveId = string.Empty;

        [SerializeField, TextArea(2, 5), Tooltip("場面の状況説明。")]
        private string situation = string.Empty;

        [SerializeField, TextArea(2, 4), Tooltip("患者・家族の発言。")]
        private string npcLine = string.Empty;

        [SerializeField, Tooltip("この場面が『沈黙』か。true なら待てたかどうかを測る。")]
        private bool isSilence;

        [SerializeField] private List<AcpOption> options = new List<AcpOption>();

        public string StepLabel { get { return stepLabel; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string Situation { get { return situation; } }
        public string NpcLine { get { return npcLine; } }
        public bool IsSilence { get { return isSilence; } }
        public IReadOnlyList<AcpOption> Options { get { return options; } }

        public AcpStep() { }

        public AcpStep(
            string stepLabel, string objectiveId, string situation,
            string npcLine, bool isSilence, List<AcpOption> options)
        {
            this.stepLabel = stepLabel;
            this.objectiveId = objectiveId;
            this.situation = situation;
            this.npcLine = npcLine;
            this.isSilence = isSilence;
            this.options = options;
        }
    }

    /// <summary>
    /// ② の中身。文言と増減値はここに集約してあり、進行のコードとは分けてある。
    /// 監修者が直すのはこのファイルだけで済む。
    /// </summary>
    [Serializable]
    public class AcpScenarioData
    {
        [SerializeField, TextArea(4, 10)] private string briefing = string.Empty;

        [SerializeField, Tooltip("沈黙の場面で、待つべき秒数。")]
        private float silenceSeconds = 4f;

        [SerializeField] private List<AcpStep> steps = new List<AcpStep>();

        public string Briefing { get { return briefing; } }
        public float SilenceSeconds { get { return silenceSeconds; } }
        public IReadOnlyList<AcpStep> Steps { get { return steps; } }

        public bool IsEmpty { get { return steps.Count == 0; } }

        /// <summary>監修前の仮版。実際の研修に使う前に緩和ケア領域の実務者のレビューを受けること。</summary>
        public static AcpScenarioData CreateDefault()
        {
            AcpScenarioData data = new AcpScenarioData();

            data.briefing =
                "【場面】\n" +
                "患者: B さん（72歳・男性）膵臓がん。抗がん剤治療を 2 レジメン行ったが病勢が進行。\n" +
                "　　　本人は「もう点滴はやめて、家で過ごしたい」と繰り返し話している。\n" +
                "家族: 長男（45歳）。遠方に住み、月に一度来院。\n" +
                "　　　「まだ治療法があるはずだ。あきらめないでほしい」と希望している。\n" +
                "あなた: 主治医。今日は本人と長男の同席で、今後の方針を話し合う。\n\n" +
                "目標は「正しい説明をすること」ではなく、\n" +
                "本人の意思を、家族との関係を壊さずに支えること。";

            data.steps.Add(new AcpStep(
                "S — Setting（環境設定）", "ACP-SET-01",
                "外来の診察室。長男が同席している。次の患者が待っており、廊下の物音が聞こえる。",
                "（長男）今日はどういうお話でしょうか。",
                false,
                new List<AcpOption>
                {
                    new AcpOption(
                        "面談室へ移りましょう。時間も取ってありますので、落ち着いてお話ししましょう。",
                        10, -5, 0, 10, -10, "ACP-SET-01", ErrorKind.None,
                        "（患者）……ありがとうございます。",
                        "場所と時間を確保することは、内容と同じくらい結果を左右する。" +
                        "遮られる環境で悪い知らせを伝えると、その後の話し合いが成立しない。"),
                    new AcpOption(
                        "ここで大丈夫です。手短にお話ししますね。",
                        -10, 10, 0, -5, 10, "ACP-SET-01", ErrorKind.None,
                        "（長男）……はあ。",
                        "環境設定を省くと、患者は「軽く扱われた」と感じ、" +
                        "以降の情報がどれだけ正確でも受け取られにくくなる。"),
                    new AcpOption(
                        "（何も言わず検査結果の画面を開く）",
                        -15, 15, -10, -10, 15, "ACP-SET-01", ErrorKind.None,
                        "（長男）……先生、どうなんですか。",
                        "説明の前提を作らずにデータから入ると、相手は身構える。" +
                        "SPIKES の S を飛ばした状態。")
                }));

            data.steps.Add(new AcpStep(
                "P — Perception（認識の確認）", "ACP-PER-01",
                "面談室に移った。まず、本人と家族が現状をどう理解しているかを確かめたい。",
                "（患者）……この前の検査、あまり良くなかったんでしょう。",
                false,
                new List<AcpOption>
                {
                    new AcpOption(
                        "B さんご自身は、今のお体の状態をどのように受け止めておられますか。",
                        15, -5, 10, 5, 0, "ACP-PER-01", ErrorKind.None,
                        "（患者）……正直、もう長くないんだろうと思っています。",
                        "説明の前に相手の認識を聞く。ここを飛ばすと、既に分かっていることを" +
                        "延々と説明したり、逆に前提の理解が無いまま結論を伝えたりする。"),
                    new AcpOption(
                        "結論から申し上げると、抗がん剤の効果は認められませんでした。",
                        -5, 15, -10, -10, 20, "ACP-PER-01", ErrorKind.JargonOverload,
                        "（長男）……そんな、いきなり。",
                        "相手の準備状態を確かめずに結論から入っている。内容が正確でも、" +
                        "受け取る側の準備が無ければ情報は届かない。"),
                    new AcpOption(
                        "（長男に向かって）ご家族はどうお考えですか。",
                        -10, 5, 0, 10, -5, "ACP-PER-01", ErrorKind.StakeholderNeglected,
                        "（長男）まだ治療はあるはずです。ねえ、父さん。",
                        "本人を差し置いて家族に先に聞くと、本人は「自分の話なのに蚊帳の外だ」と感じる。" +
                        "本人の意思決定を支える場面では順序が意味を持つ。")
                }));

            data.steps.Add(new AcpStep(
                "I — Invitation（どこまで知りたいか）", "ACP-INV-01",
                "本人の認識は確認できた。次に、どこまで詳しく聞きたいかを確かめる。",
                "（患者）……先生、はっきり言ってもらって構いません。",
                false,
                new List<AcpOption>
                {
                    new AcpOption(
                        "分かりました。数字も含めて詳しくお話ししますが、途中で止めたくなったら言ってください。",
                        15, -5, 10, 5, 0, "ACP-INV-01", ErrorKind.None,
                        "（患者）……はい、お願いします。",
                        "知る権利と同時に「知らないでいる権利」も保障している。" +
                        "許可を得てから伝えることで、聞く側の主導権が保たれる。"),
                    new AcpOption(
                        "（長男に）ご家族には別room で改めてご説明しますね。",
                        5, 0, 0, -15, 20, "ACP-INV-01", ErrorKind.StakeholderNeglected,
                        "（長男）……私は聞いてはいけないんですか。",
                        "同席している家族を外すと、家族は疎外され不安が増す。" +
                        "後の合意形成が著しく難しくなる。"),
                    new AcpOption(
                        "では説明します。（すぐに病状の説明を始める）",
                        0, 5, -5, 0, 5, "ACP-INV-01", ErrorKind.ProtocolStepSkipped,
                        "（患者）……あ、はい。",
                        "本人が「はっきり言って」と言った時点で許可があるとみなすことはできるが、" +
                        "どこまで・どの粒度でを確かめないと、必要以上に踏み込むことがある。")
                }));

            data.steps.Add(new AcpStep(
                "K — Knowledge（情報提供）", "ACP-KNW-01",
                "病状と、今後の見通しを伝える場面。",
                "（患者）……お願いします。",
                false,
                new List<AcpOption>
                {
                    new AcpOption(
                        "残念ながら、今の治療でがんを小さくすることは難しい状況です。\n" +
                        "これからは、つらい症状を和らげることを中心にしていく段階だと考えています。",
                        15, 5, 15, 5, 10, "ACP-KNW-01", ErrorKind.None,
                        "（患者）……そうですか。（長い沈黙）",
                        "専門用語を避け、短く区切って伝えている。" +
                        "「治療をやめる」ではなく「何を目標に切り替えるか」として示せている。"),
                    new AcpOption(
                        "PD の判断で、セカンドラインも奏効せず、PS も低下しています。\n" +
                        "BSC への移行が妥当と考えます。",
                        -5, 15, -25, -10, 20, "ACP-KNW-01", ErrorKind.JargonOverload,
                        "（長男）……すみません、どういう意味ですか。",
                        "専門用語で伝えると、理解を伴わないまま話が進む。" +
                        "この状態で得た同意は、意思決定の支援になっていない。"),
                    new AcpOption(
                        "まだ試せる治験があるかもしれません。探してみましょう。",
                        -10, 10, -10, 20, -20, "ACP-KNW-01", ErrorKind.StakeholderNeglected,
                        "（長男）ぜひお願いします！（患者）……。",
                        "家族の希望に沿う答えだが、本人の「やめたい」という意思から離れている。" +
                        "一時的に家族の不安は下がるが、本人の信頼を失う。")
                }));

            data.steps.Add(new AcpStep(
                "E — Emotion（感情への対応）", "ACP-EMP-01",
                "伝え終えた直後。患者はうつむいたまま黙っている。長男も言葉が出ない。",
                "（患者）……（沈黙）",
                true,
                new List<AcpOption>
                {
                    new AcpOption(
                        "（何も言わず、そばにいる）",
                        20, -10, 0, 5, -5, "ACP-EMP-01", ErrorKind.None,
                        "（患者）……すみません。少し、考えてしまって。",
                        "沈黙は空白ではなく、相手が受け止めるための時間。" +
                        "埋めずに待てるかどうかが、この場面で測られている。"),
                    new AcpOption(
                        "つらいお話でしたよね。今、どんなことを考えておられますか。",
                        15, -10, 5, 5, -5, "ACP-EMP-01", ErrorKind.None,
                        "（患者）……家に帰りたい、と。それだけです。",
                        "感情に名前を付けて返し、そのうえで開かれた問いをしている。" +
                        "ただし沈黙が十分でないうちに言葉を挟むと、遮ったことになる。"),
                    new AcpOption(
                        "それで、今後の治療方針ですが、緩和ケア病棟への紹介を——",
                        -20, 20, -10, -5, 15, "ACP-EMP-01", ErrorKind.InterruptedSilence,
                        "（患者）……はい。（それきり黙る）",
                        "沈黙を情報で埋めている。話す側の不安を解消するための行動であって、" +
                        "相手のためのものではない。以降、本人は本音を語らなくなる。")
                }));

            data.steps.Add(new AcpStep(
                "S — Strategy（方針の合意）", "ACP-STR-01",
                "本人は在宅を希望し、長男は治療の継続を望んでいる。両者の間で方針を作る場面。",
                "（長男）先生、本当にもう何もできないんですか。父を見捨てるんですか。",
                false,
                new List<AcpOption>
                {
                    new AcpOption(
                        "見捨てるということでは決してありません。\n" +
                        "ご心配は当然だと思います。お父様が家で過ごせるように、\n" +
                        "痛みを抑える手当てと訪問診療の体制を一緒に作らせてください。",
                        15, -10, 10, 15, -25, "ACP-STR-01", ErrorKind.None,
                        "（長男）……そういうことが、できるんですか。",
                        "家族の不安（見捨てられ感）に応えつつ、本人の希望を実現する具体策を示している。" +
                        "どちらかを選ぶのではなく、両方を成立させる道を提案できている。"),
                    new AcpOption(
                        "ご本人の意思が最優先です。B さんは家に帰りたいとおっしゃっています。",
                        20, -5, 0, -15, 20, "ACP-STR-01", ErrorKind.StakeholderNeglected,
                        "（長男）……私の言うことは関係ないと。",
                        "本人の意思を守る点では正しいが、家族を置き去りにしている。" +
                        "退院後の生活を支えるのは家族であり、ここで対立が残ると在宅は成り立たない。"),
                    new AcpOption(
                        "ではもう一度、治療を検討してみましょうか。",
                        -25, 20, -10, 15, -20, "ACP-STR-01", ErrorKind.StakeholderNeglected,
                        "（患者）……（長男）お願いします。",
                        "その場の対立は収まるが、本人の意思が覆されている。" +
                        "合意形成ではなく、声の大きい側への迎合になっている。")
                }));

            return data;
        }
    }
}
