using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Gatekeeper
{
    /// <summary>確認すべきアセスメント項目の種別。</summary>
    public enum AssessmentItem
    {
        None = 0,
        SuicidalIdeation = 1,
        Plan = 2,
        MeansAccess = 3,
        PreviousAttempt = 4,
        Support = 5,
        SafetyPlan = 6,
        Referral = 7
    }

    /// <summary>学習者が選ぶ 1 つの応答。</summary>
    [Serializable]
    public class GatekeeperOption
    {
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;
        [SerializeField, TextArea(2, 4)] private string reply = string.Empty;

        [SerializeField, Tooltip("相手の Trust への増減。")] private int trustDelta;

        [SerializeField, Tooltip("相手の Urgency への増減。学習者には見せない。")]
        private int urgencyDelta;

        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private string errorKind = ErrorKind.None;

        [SerializeField, Tooltip("この応答で確認できるアセスメント項目。")]
        private AssessmentItem checksItem = AssessmentItem.None;

        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Text { get { return text; } }
        public string Reply { get { return reply; } }
        public int TrustDelta { get { return trustDelta; } }
        public int UrgencyDelta { get { return urgencyDelta; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string ErrorKindName { get { return errorKind; } }
        public AssessmentItem ChecksItem { get { return checksItem; } }
        public string DebriefNote { get { return debriefNote; } }

        public GatekeeperOption() { }

        public GatekeeperOption(
            string text, string reply, int trustDelta, int urgencyDelta,
            string objectiveId, string errorKind, AssessmentItem checksItem, string debriefNote)
        {
            this.text = text;
            this.reply = reply;
            this.trustDelta = trustDelta;
            this.urgencyDelta = urgencyDelta;
            this.objectiveId = objectiveId;
            this.errorKind = errorKind;
            this.checksItem = checksItem;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>対話の 1 場面。</summary>
    [Serializable]
    public class GatekeeperTurn
    {
        [SerializeField, TextArea(2, 5)] private string residentLine = string.Empty;
        [SerializeField] private List<GatekeeperOption> options = new List<GatekeeperOption>();

        public string ResidentLine { get { return residentLine; } }
        public IReadOnlyList<GatekeeperOption> Options { get { return options; } }

        public GatekeeperTurn() { }

        public GatekeeperTurn(string residentLine, List<GatekeeperOption> options)
        {
            this.residentLine = residentLine;
            this.options = options;
        }
    }

    /// <summary>⑤ の中身。台詞はすべてここに集約してある。</summary>
    [Serializable]
    public class GatekeeperScenarioData
    {
        [SerializeField, TextArea(3, 8), Tooltip("全局面で画面に常設する相談窓口の案内。")]
        private string helplineNotice = string.Empty;

        [SerializeField, TextArea(4, 10)] private string briefing = string.Empty;

        [SerializeField] private List<GatekeeperTurn> turns = new List<GatekeeperTurn>();

        [SerializeField, Tooltip("直接的な問いかけ。信頼が十分でないと時期尚早になる。")]
        private GatekeeperOption directQuestion = new GatekeeperOption();

        [SerializeField, Tooltip("直接確認が有効になる Trust の下限。")]
        private int directQuestionTrustThreshold = 50;

        [SerializeField, Tooltip("本音の開示後に確認できる項目。")]
        private List<GatekeeperOption> probes = new List<GatekeeperOption>();

        [SerializeField, Tooltip("対話の締めくくり（安全の約束・専門機関への接続）。")]
        private List<GatekeeperOption> closingOptions = new List<GatekeeperOption>();

        public string HelplineNotice { get { return helplineNotice; } }
        public string Briefing { get { return briefing; } }
        public IReadOnlyList<GatekeeperTurn> Turns { get { return turns; } }
        public GatekeeperOption DirectQuestion { get { return directQuestion; } }
        public int DirectQuestionTrustThreshold { get { return directQuestionTrustThreshold; } }
        public IReadOnlyList<GatekeeperOption> Probes { get { return probes; } }
        public IReadOnlyList<GatekeeperOption> ClosingOptions { get { return closingOptions; } }

        public bool IsEmpty { get { return turns.Count == 0; } }

        /// <summary>
        /// 監修前の仮版。実際の研修に使う前に、精神保健の専門家によるレビューを必ず受けること。
        /// 相手の描写は特定の属性・疾患に紐づけないようにしてある。
        /// </summary>
        public static GatekeeperScenarioData CreateDefault()
        {
            GatekeeperScenarioData data = new GatekeeperScenarioData();

            data.helplineNotice =
                "【相談窓口】つらいと感じたときは、一人で抱えずご相談ください。\n" +
                "　こころの健康相談統一ダイヤル 0570-064-556／ よりそいホットライン 0120-279-338\n" +
                "　この演習はいつでも中断できます。";

            data.briefing =
                "【場面】\n" +
                "あなたは地域の相談窓口の職員（ゲートキーパー研修の受講者）。\n" +
                "顔なじみの E さんが、いつもと様子が違う。\n" +
                "ここ 2 週間ほど地域の集まりに顔を出さず、今日は約束をして来所した。\n\n" +
                "ゲートキーパーの役割は「気づき・傾聴・つなぎ・見守り」の 4 つ。\n" +
                "問題を解決することではなく、相手の話を受け止め、\n" +
                "必要な支援に確実につなぐことが目標になる。\n\n" +
                "※ 相手の内面（信頼・切迫度）は数値としては表示されません。\n" +
                "　 手がかりは相手の言葉だけです。";

            data.turns.Add(new GatekeeperTurn(
                "（E さん）……すみません、わざわざ呼び出してもらって。大したことじゃないんですけど。",
                new List<GatekeeperOption>
                {
                    new GatekeeperOption(
                        "お忙しいところありがとうございます。最近お見かけしなかったので、気になっていました。",
                        "（E さん）……そうですか。まあ、ちょっと色々ありまして。",
                        15, -5, "GTK-AWR-01", ErrorKind.None, AssessmentItem.None,
                        "変化に気づいたことを、責める調子でなく伝えている。" +
                        "「気づき」は観察するだけでなく、気にかけていると伝わって初めて意味を持つ。"),
                    new GatekeeperOption(
                        "大したことじゃないなら良かったです。何かあれば言ってくださいね。",
                        "（E さん）……はい。（沈黙）",
                        -10, 10, "GTK-AWR-01", ErrorKind.None, AssessmentItem.None,
                        "相手の「大したことない」を額面通り受け取っている。" +
                        "その言葉自体が、話していいか迷っているサインであることが多い。"),
                    new GatekeeperOption(
                        "何かあったんですか。悩みがあるなら早めに相談しないと駄目ですよ。",
                        "（E さん）……いえ、本当に大丈夫です。",
                        -20, 10, "GTK-LST-01", ErrorKind.InappropriateEncouragement, AssessmentItem.None,
                        "助言や指導が先に出ている。相手はまだ何も話していない段階であり、" +
                        "この時点での「〜すべき」は相手を閉じさせる。")
                }));

            data.turns.Add(new GatekeeperTurn(
                "（E さん）……最近、夜も眠れなくて。食べる気もしなくて。何のために生きてるのかなって、思うことがあって。",
                new List<GatekeeperOption>
                {
                    new GatekeeperOption(
                        "……そう感じておられるんですね。よかったら、もう少し聞かせてもらえますか。",
                        "（E さん）……仕事も辞めることになって。誰にも言えなくて。",
                        20, -10, "GTK-LST-01", ErrorKind.None, AssessmentItem.None,
                        "相手の言葉をそのまま受け止め、開示を促している。" +
                        "評価も助言もせずに聴く時間が、この場面ではもっとも価値がある。"),
                    new GatekeeperOption(
                        "そんなこと言わないでください。E さんが元気だとみんな安心するんですから。",
                        "（E さん）……そうですよね。すみません、変なこと言って。",
                        -30, 20, "GTK-LST-01", ErrorKind.InappropriateEncouragement, AssessmentItem.None,
                        "善意の励ましだが、「その話はしないで」という合図として届く。" +
                        "相手は謝って話をやめる。表面上は落ち着いて見えても、状況は改善していない。"),
                    new GatekeeperOption(
                        "眠れないのはつらいですね。病院には行かれましたか。",
                        "（E さん）……いえ、まだ。",
                        5, 0, "GTK-AWR-01", ErrorKind.None, AssessmentItem.None,
                        "受診の確認は必要だが、相手が語った「何のために生きてるのか」という" +
                        "部分を素通りしている。手がかりは、相手が差し出した言葉の中にある。"),
                    new GatekeeperOption(
                        "誰にでもそういう時期はありますよ。時間が解決してくれます。",
                        "（E さん）……はい。",
                        -25, 15, "GTK-LST-01", ErrorKind.InappropriateEncouragement, AssessmentItem.None,
                        "一般化して片づけている。相手の体験を「よくあること」にすると、" +
                        "話す意味が失われる。")
                }));

            data.turns.Add(new GatekeeperTurn(
                "（E さん）……迷惑ばかりかけて。いなくなったほうが楽なんじゃないかって。",
                new List<GatekeeperOption>
                {
                    new GatekeeperOption(
                        "……それだけ追い詰められているんですね。話してくださってありがとうございます。",
                        "（E さん）……こんなこと、初めて言いました。",
                        20, -5, "GTK-LST-01", ErrorKind.None, AssessmentItem.None,
                        "重い開示を受け止め、話してくれたこと自体をねぎらっている。" +
                        "ここで動揺や否定を返すと、相手は「言うべきではなかった」と学習してしまう。"),
                    new GatekeeperOption(
                        "そんな風に思っちゃいけません。ご家族が悲しみますよ。",
                        "（E さん）……そうですよね。すみません。",
                        -25, 15, "GTK-LST-01", ErrorKind.InappropriateEncouragement, AssessmentItem.None,
                        "正論だが、相手を責める形になっている。" +
                        "「家族が悲しむ」は、既に自分を負担だと感じている人には追い打ちになる。"),
                    new GatekeeperOption(
                        "何か楽しいことを見つけましょう。気分転換が大事ですよ。",
                        "（E さん）……はい、そうします。",
                        -20, 20, "GTK-LST-01", ErrorKind.InappropriateEncouragement, AssessmentItem.None,
                        "助言で話を終わらせている。相手は「分かってもらえない」と判断し、" +
                        "以降は本音を出さなくなる。切迫度は下がっていない。")
                }));

            data.directQuestion = new GatekeeperOption(
                "……死にたいと考えていますか。",
                "（E さん）……はい。……考えます。毎日。",
                20, 0, "GTK-ASK-01", ErrorKind.None, AssessmentItem.SuicidalIdeation,
                "直接尋ねることは、相手を追い詰めない。むしろ「話していい相手だ」と伝わる。" +
                "ゲートキーパー研修でもっとも実行されにくい行動であり、" +
                "尋ねないままでは危険の程度を評価できない。");

            data.probes.Add(new GatekeeperOption(
                "具体的に、どうしようと考えたことはありますか。",
                "（E さん）……薬を、集めていて。",
                5, 0, "GTK-RSK-01", ErrorKind.None, AssessmentItem.Plan,
                "計画の具体性は、危険度を評価するうえで欠かせない情報。" +
                "尋ねにくいが、尋ねなければ支援の緊急度を判断できない。"));

            data.probes.Add(new GatekeeperOption(
                "その薬は、今も手元にありますか。",
                "（E さん）……はい。家に。",
                5, 0, "GTK-RSK-01", ErrorKind.None, AssessmentItem.MeansAccess,
                "手段へのアクセスの確認。手元にあるかどうかで、当面の安全確保の方法が変わる。"));

            data.probes.Add(new GatekeeperOption(
                "これまでにも、同じように考えたことはありましたか。",
                "（E さん）……何年か前に一度。そのときは、家族が気づいてくれて。",
                5, 0, "GTK-RSK-01", ErrorKind.None, AssessmentItem.PreviousAttempt,
                "過去の未遂歴は、もっとも強い危険因子の一つ。必ず確認する項目にあたる。"));

            data.probes.Add(new GatekeeperOption(
                "今、身近に頼れる方はいらっしゃいますか。",
                "（E さん）……妹が、隣の市に。しばらく連絡していませんが。",
                10, -5, "GTK-WCH-01", ErrorKind.None, AssessmentItem.Support,
                "孤立の程度と、使える支援の把握。" +
                "「誰もいない」と思い込んでいるだけの場合も多く、一緒に探す価値がある。"));

            data.closingOptions.Add(new GatekeeperOption(
                "今日から次にお会いするまで、薬は妹さんに預かってもらえませんか。" +
                "それと、つらくなったらここに連絡すると約束してもらえますか。",
                "（E さん）……はい。約束します。",
                15, -20, "GTK-WCH-01", ErrorKind.None, AssessmentItem.SafetyPlan,
                "手段を遠ざけ、連絡先を具体的に決めている。" +
                "抽象的な「頑張って」ではなく、実行できる約束になっている。"));

            data.closingOptions.Add(new GatekeeperOption(
                "専門の相談機関に一緒に行きませんか。私から連絡して、日程を決めましょう。",
                "（E さん）……一人だと行けないと思うので、お願いできますか。",
                15, -20, "GTK-CON-01", ErrorKind.None, AssessmentItem.Referral,
                "「つなぎ」は、窓口を案内することではなく、確実に到達させること。" +
                "同行や日程調整まで踏み込めるかで結果が変わる。"));

            data.closingOptions.Add(new GatekeeperOption(
                "また何かあったら、いつでも来てくださいね。",
                "（E さん）……はい。ありがとうございました。",
                0, 10, "GTK-CON-01", ErrorKind.ReferralOmitted, AssessmentItem.None,
                "善意の言葉だが、次の行動が何も決まっていない。" +
                "「いつでも来て」は、来られない状態の人には届かない。"));

            return data;
        }
    }
}
