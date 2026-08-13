using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Welfare
{
    /// <summary>
    /// 家庭訪問で目に入るもの。リスクサインもあれば、そうでないものもある。
    /// 「気づけたか」だけでなく「関係ないものに時間を使ったか」も見たいので、
    /// 中立な項目をあえて混ぜてある。
    /// </summary>
    [Serializable]
    public class Observable
    {
        [SerializeField] private string id = "item";
        [SerializeField, TextArea(1, 3)] private string label = string.Empty;
        [SerializeField, TextArea(2, 4)] private string detail = string.Empty;

        [SerializeField, Tooltip("虐待・ネグレクトのリスクを示す所見か。")]
        private bool isRiskSign;

        [SerializeField, Tooltip("見落とすと重大な影響がある項目か。")]
        private bool isCritical;

        [SerializeField, TextArea(2, 4), Tooltip("振り返りで提示する解説。")]
        private string debriefNote = string.Empty;

        public string Id { get { return id; } }
        public string Label { get { return label; } }
        public string Detail { get { return detail; } }
        public bool IsRiskSign { get { return isRiskSign; } }
        public bool IsCritical { get { return isCritical; } }
        public string DebriefNote { get { return debriefNote; } }

        public Observable() { }

        public Observable(string id, string label, string detail, bool isRiskSign, bool isCritical, string debriefNote)
        {
            this.id = id;
            this.label = label;
            this.detail = detail;
            this.isRiskSign = isRiskSign;
            this.isCritical = isCritical;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>保護者に対する 1 つの発言。</summary>
    [Serializable]
    public class Utterance
    {
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;

        [SerializeField, Tooltip("保護者の信頼度に与える増減。")]
        private int trustDelta;

        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private string errorKind = ErrorKind.None;

        [SerializeField, TextArea(2, 4), Tooltip("保護者の返答。")]
        private string reply = string.Empty;

        [SerializeField, TextArea(2, 4), Tooltip("振り返りで提示する解説。")]
        private string debriefNote = string.Empty;

        public string Text { get { return text; } }
        public int TrustDelta { get { return trustDelta; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string ErrorKindName { get { return errorKind; } }
        public string Reply { get { return reply; } }
        public string DebriefNote { get { return debriefNote; } }

        public Utterance() { }

        public Utterance(
            string text, int trustDelta, string objectiveId, string errorKind, string reply, string debriefNote)
        {
            this.text = text;
            this.trustDelta = trustDelta;
            this.objectiveId = objectiveId;
            this.errorKind = errorKind;
            this.reply = reply;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>保護者との対話の 1 場面。</summary>
    [Serializable]
    public class InterviewTurn
    {
        [SerializeField, TextArea(2, 5), Tooltip("保護者の発言。")]
        private string guardianLine = string.Empty;

        [SerializeField] private List<Utterance> options = new List<Utterance>();

        public string GuardianLine { get { return guardianLine; } }
        public IReadOnlyList<Utterance> Options { get { return options; } }

        public InterviewTurn() { }

        public InterviewTurn(string guardianLine, List<Utterance> options)
        {
            this.guardianLine = guardianLine;
            this.options = options;
        }
    }

    /// <summary>
    /// シナリオの中身（文言・配点）。コードから切り離してあるので、
    /// 監修者は Inspector 上でここだけを直せる。
    /// 未設定のまま実行した場合は <see cref="CreateDefault"/> の内容が使われる。
    /// </summary>
    [Serializable]
    public class WelfareScenarioData
    {
        [SerializeField, TextArea(4, 10)] private string caseSummary = string.Empty;
        [SerializeField] private List<string> caseFiles = new List<string>();
        [SerializeField] private List<Observable> observables = new List<Observable>();
        [SerializeField] private List<InterviewTurn> interview = new List<InterviewTurn>();
        [SerializeField] private List<string> custodyGrounds = new List<string>();

        [SerializeField, Tooltip("一時保護の要件を満たす根拠（custodyGrounds の添字）。")]
        private List<int> validGroundIndices = new List<int>();

        [SerializeField, Tooltip("このケースで一時保護が必要か（正答）。")]
        private bool custodyIsRequired = true;

        [SerializeField] private List<string> reportElements = new List<string>();

        [SerializeField, Tooltip("報告に必ず含めるべき要素（reportElements の添字）。")]
        private List<int> requiredReportIndices = new List<int>();

        public string CaseSummary { get { return caseSummary; } }
        public IReadOnlyList<string> CaseFiles { get { return caseFiles; } }
        public IReadOnlyList<Observable> Observables { get { return observables; } }
        public IReadOnlyList<InterviewTurn> Interview { get { return interview; } }
        public IReadOnlyList<string> CustodyGrounds { get { return custodyGrounds; } }
        public IReadOnlyList<int> ValidGroundIndices { get { return validGroundIndices; } }
        public bool CustodyIsRequired { get { return custodyIsRequired; } }
        public IReadOnlyList<string> ReportElements { get { return reportElements; } }
        public IReadOnlyList<int> RequiredReportIndices { get { return requiredReportIndices; } }

        public bool IsEmpty
        {
            get { return observables.Count == 0 || interview.Count == 0; }
        }

        /// <summary>
        /// 監修前の仮の内容。実運用では必ず児童福祉の実務者のレビューを受けること。
        /// ここでは「観察事実と解釈を分けて集め、法的要件に照らして判断する」という
        /// 構造を体験できる最小限の題材にしてある。
        /// </summary>
        public static WelfareScenarioData CreateDefault()
        {
            WelfareScenarioData data = new WelfareScenarioData();

            data.caseSummary =
                "【通告受理票】\n" +
                "対象児童: A児（5歳・女児）\n" +
                "世帯: 母（28歳）、内縁の男性（31歳）との3人暮らし\n" +
                "通告者: 保育所長\n" +
                "通告内容: 2週間の連続欠席。登所していた時期も、同じ衣服が続き、\n" +
                "　　　　　入浴した様子がないことがあった。前腕に複数のあざを確認。\n" +
                "　　　　　本児は「ころんだ」と説明。\n" +
                "本日の目的: 児童の安全確認（48時間ルール）と家庭状況のアセスメント";

            data.caseFiles.Add("過去の通告履歴（2年前に1件。当時は継続指導で終結）");
            data.caseFiles.Add("保育所からの出欠記録（直近3か月で欠席が増加傾向）");
            data.caseFiles.Add("世帯の支援状況（生活保護は受給していない。就労状況は不明）");

            data.observables.Add(new Observable(
                "child_condition", "A児の様子と全身の状態を確認する",
                "痩せ型。表情が乏しく、来訪者と目を合わせない。前腕に新旧混在するあざが複数あり、" +
                "本人は小声で「ころんだ」と答える。",
                true, true,
                "新旧が混在する多発性のあざは、単回の転倒では説明が難しい。" +
                "部位・形状・新旧を『観察事実』として記録し、原因の断定は避ける。児童の安全確認は本日の最優先事項。"));

            data.observables.Add(new Observable(
                "kitchen", "台所と食料の状況を見る",
                "流し台に食器が積まれている。冷蔵庫にはほとんど食材がなく、床に酒瓶が複数並んでいる。",
                true, true,
                "食料の欠如は養育環境上の重大な所見。酒瓶は『飲酒の可能性を示す所見』であって、" +
                "アルコール依存や虐待の証拠ではない。事実として記録し、解釈は分けて扱う。"));

            data.observables.Add(new Observable(
                "sleeping_area", "A児の寝る場所を確認する",
                "居間の隅に薄い毛布が1枚。子ども用の寝具や着替えは見当たらない。",
                true, false,
                "本児専用の生活スペースや衣類の有無は、養育の実態を示す具体的な所見になる。"));

            data.observables.Add(new Observable(
                "medication", "居間のテーブルの上を見る",
                "処方薬の袋が置かれている。母親宛の精神科の処方が含まれている。",
                true, false,
                "保護者の治療状況は支援の手がかりであり、それ自体は虐待の根拠ではない。" +
                "『支援が必要な状況』として扱い、決めつけない姿勢が求められる。"));

            data.observables.Add(new Observable(
                "tv_game", "テレビとゲーム機を見る",
                "比較的新しい機種が置かれている。",
                false, false,
                "経済状況の推測材料にはなるが、養育の適否とは直接結びつかない。" +
                "生活水準への価値判断は、アセスメントの根拠にしてはならない。"));

            data.observables.Add(new Observable(
                "entrance", "玄関まわりを見る",
                "郵便物が溜まっている。督促状らしき封書が混ざっている。",
                true, false,
                "生活の破綻の兆候であり、経済的支援や生活保護の相談につなぐ手がかりになる。"));

            data.interview.Add(new InterviewTurn(
                "（ドアを細く開けて）……何ですか。話すことはありません。帰ってください。",
                new List<Utterance>
                {
                    new Utterance(
                        "突然お伺いして驚かれましたよね。5分だけお時間をいただけませんか。",
                        10, "WLF-INT-01", ErrorKind.None,
                        "……5分だけですよ。",
                        "相手の反応を否定せずに受け止めたうえで、こちらの用件を具体的に伝えている。" +
                        "拒否的な相手に対しては、まず接触を維持することが最優先。"),
                    new Utterance(
                        "通告が入っています。お子さんに会わせてください。",
                        -15, "WLF-INT-02", ErrorKind.CombativeSpeech,
                        "……人のことを疑ってるんですか。もう帰ってください。",
                        "通告の事実をいきなり突きつけると、保護者は身構えて情報が閉ざされる。" +
                        "安全確認という目的は変えずに、伝え方を工夫する必要がある。"),
                    new Utterance(
                        "保育所を長くお休みされていると伺いました。何かお困りのことがありますか。",
                        15, "WLF-INT-01", ErrorKind.None,
                        "……別に。ちょっと色々あって、それだけです。",
                        "事実（欠席）から入り、相手を困っている人として扱っている。" +
                        "非難ではなく支援の入口として提示できている。")
                }));

            data.interview.Add(new InterviewTurn(
                "私だって毎日必死なんです。仕事も探してるし、寝る時間だってない。",
                new List<Utterance>
                {
                    new Utterance(
                        "毎日休まる時間がないのですね。その中でお子さんを見てこられたのは大変だったと思います。",
                        15, "WLF-INT-01", ErrorKind.None,
                        "……誰もそんなこと言ってくれなかった。",
                        "保護者の労苦を認めることは、養育上の問題を容認することとは別。" +
                        "ここで関係が作れると、以降の情報開示が大きく変わる。"),
                    new Utterance(
                        "大変なのは分かりますが、お子さんが一番の被害者ですよね。",
                        -20, "WLF-INT-02", ErrorKind.CombativeSpeech,
                        "……もういいです。帰ってください。",
                        "正論だが、保護者を加害者の位置に置いた瞬間に対話は終わる。" +
                        "結果として児童の安全確認ができなくなり、子どもの不利益になる。"),
                    new Utterance(
                        "お一人で抱えてこられたのですね。使える制度のご案内もできます。",
                        10, "WLF-SOC-01", ErrorKind.None,
                        "……そういうの、よく分からなくて。",
                        "経済的困窮が見えている場合、生活保護等の社会資源への接続は" +
                        "介入と支援の両輪になる。")
                }));

            data.interview.Add(new InterviewTurn(
                "……あの人（内縁の男性）は、しつけには厳しくて。私が止めても聞かなくて。",
                new List<Utterance>
                {
                    new Utterance(
                        "止めようとされていたのですね。どんなことがあったか、差し支えない範囲で教えていただけますか。",
                        15, "WLF-INT-01", ErrorKind.None,
                        "……手が出ることがあります。私にも、あの子にも。",
                        "重要な開示。保護者を責めずに具体化を促せている。" +
                        "同時に、保護者自身が被害を受けている可能性（DV）も視野に入る。"),
                    new Utterance(
                        "それは虐待です。あなたにも責任があります。",
                        -25, "WLF-INT-02", ErrorKind.CombativeSpeech,
                        "……もう何も話しません。",
                        "開示しかけた相手を評価・断罪すると、そこで情報が止まる。" +
                        "判断は必要だが、それを face-to-face の対話でぶつける場面ではない。"),
                    new Utterance(
                        "そうですか。ところで、お仕事の状況はいかがですか。",
                        -10, "WLF-INT-01", ErrorKind.None,
                        "……はあ。",
                        "最も重要な開示を素通りしている。話題を変えることは、" +
                        "相手にとっては「聞いてもらえなかった」という体験になる。")
                }));

            data.custodyGrounds.Add("新旧混在する多発性のあざがあり、説明と所見が一致しない");
            data.custodyGrounds.Add("同居者による身体的暴力について、保護者から直接の言及があった");
            data.custodyGrounds.Add("食料の確保や本児の生活スペースなど、養育環境に重大な不足がある");
            data.custodyGrounds.Add("保護者の態度が非協力的で、印象が良くない");
            data.custodyGrounds.Add("家に新しいゲーム機があり、金銭の使い方に問題がある");
            data.validGroundIndices.Add(0);
            data.validGroundIndices.Add(1);
            data.validGroundIndices.Add(2);
            data.custodyIsRequired = true;

            data.reportElements.Add("観察した事実（あざの部位・新旧、食料と寝具の状況）");
            data.reportElements.Add("保護者からの発言内容（同居者の暴力への言及）");
            data.reportElements.Add("児童の安全が確保できないと判断した理由");
            data.reportElements.Add("一時保護の要否についての自分の意見と、求める指示");
            data.reportElements.Add("保護者の人柄についての印象");
            data.requiredReportIndices.Add(0);
            data.requiredReportIndices.Add(1);
            data.requiredReportIndices.Add(2);
            data.requiredReportIndices.Add(3);

            return data;
        }
    }
}
