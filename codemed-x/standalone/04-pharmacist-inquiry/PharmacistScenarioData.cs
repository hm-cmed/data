using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Pharmacist
{
    /// <summary>服薬指導での 1 つの応答。</summary>
    [Serializable]
    public class CounselingOption
    {
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;
        [SerializeField, TextArea(2, 4)] private string reply = string.Empty;

        [SerializeField, Tooltip("有害事象を示唆する訴えを拾えたか。")]
        private bool capturesSymptom;

        [SerializeField] private string objectiveId = "PHM-CNS-01";
        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Text { get { return text; } }
        public string Reply { get { return reply; } }
        public bool CapturesSymptom { get { return capturesSymptom; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string DebriefNote { get { return debriefNote; } }

        public CounselingOption() { }

        public CounselingOption(
            string text, string reply, bool capturesSymptom, string objectiveId, string debriefNote)
        {
            this.text = text;
            this.reply = reply;
            this.capturesSymptom = capturesSymptom;
            this.objectiveId = objectiveId;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>検査値 1 項目。異常値と正常値・無関係な項目を混ぜて出す。</summary>
    [Serializable]
    public class LabValue
    {
        [SerializeField] private string id = "lab";
        [SerializeField] private string label = string.Empty;
        [SerializeField] private string value = string.Empty;
        [SerializeField] private string reference = string.Empty;

        [SerializeField, Tooltip("疑義の根拠として妥当な項目か。")]
        private bool isRelevant;

        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Id { get { return id; } }
        public string Label { get { return label; } }
        public string Value { get { return value; } }
        public string Reference { get { return reference; } }
        public bool IsRelevant { get { return isRelevant; } }
        public string DebriefNote { get { return debriefNote; } }

        public LabValue() { }

        public LabValue(string id, string label, string value, string reference,
            bool isRelevant, string debriefNote)
        {
            this.id = id;
            this.label = label;
            this.value = value;
            this.reference = reference;
            this.isRelevant = isRelevant;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>医師との電話でのやり取り 1 つ。</summary>
    [Serializable]
    public class InquiryOption
    {
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;
        [SerializeField, TextArea(2, 4)] private string reply = string.Empty;

        [SerializeField, Tooltip("医師の Pressure への増減。")]
        private int pressureDelta;

        [SerializeField, Tooltip("検査値の添付が必要な選択肢か。未添付だと根拠を示せていないことになる。")]
        private bool requiresEvidence;

        [SerializeField, Tooltip("学習者が主張を取り下げる選択肢か。")]
        private bool yieldsToAuthority;

        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Text { get { return text; } }
        public string Reply { get { return reply; } }
        public int PressureDelta { get { return pressureDelta; } }
        public bool RequiresEvidence { get { return requiresEvidence; } }
        public bool YieldsToAuthority { get { return yieldsToAuthority; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string DebriefNote { get { return debriefNote; } }

        public InquiryOption() { }

        public InquiryOption(
            string text, string reply, int pressureDelta, bool requiresEvidence,
            bool yieldsToAuthority, string objectiveId, string debriefNote)
        {
            this.text = text;
            this.reply = reply;
            this.pressureDelta = pressureDelta;
            this.requiresEvidence = requiresEvidence;
            this.yieldsToAuthority = yieldsToAuthority;
            this.objectiveId = objectiveId;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>医師との電話の 1 場面。</summary>
    [Serializable]
    public class InquiryTurn
    {
        [SerializeField, TextArea(2, 4)] private string doctorLine = string.Empty;
        [SerializeField] private List<InquiryOption> options = new List<InquiryOption>();

        public string DoctorLine { get { return doctorLine; } }
        public IReadOnlyList<InquiryOption> Options { get { return options; } }

        public InquiryTurn() { }

        public InquiryTurn(string doctorLine, List<InquiryOption> options)
        {
            this.doctorLine = doctorLine;
            this.options = options;
        }
    }

    /// <summary>④ の中身。文言・検査値・配点はここに集約してある。</summary>
    [Serializable]
    public class PharmacistScenarioData
    {
        [SerializeField, TextArea(4, 12)] private string prescription = string.Empty;
        [SerializeField, TextArea(2, 4)] private string patientOpening = string.Empty;
        [SerializeField] private List<CounselingOption> counseling = new List<CounselingOption>();
        [SerializeField] private List<LabValue> labValues = new List<LabValue>();
        [SerializeField] private List<InquiryTurn> inquiry = new List<InquiryTurn>();

        [SerializeField, Tooltip("電話での応答制限時間（秒）。超えると Pressure が上がる。")]
        private float responseSeconds = 10f;

        public string Prescription { get { return prescription; } }
        public string PatientOpening { get { return patientOpening; } }
        public IReadOnlyList<CounselingOption> Counseling { get { return counseling; } }
        public IReadOnlyList<LabValue> LabValues { get { return labValues; } }
        public IReadOnlyList<InquiryTurn> Inquiry { get { return inquiry; } }
        public float ResponseSeconds { get { return responseSeconds; } }

        public bool IsEmpty { get { return inquiry.Count == 0; } }

        /// <summary>監修前の仮版。実際の研修に使う前に薬剤師のレビューを受けること。</summary>
        public static PharmacistScenarioData CreateDefault()
        {
            PharmacistScenarioData data = new PharmacistScenarioData();

            data.prescription =
                "【処方箋】\n" +
                "患者: C さん（76歳・男性）\n" +
                "処方元: 内科 D 医師\n\n" +
                "1) ロキソプロフェン錠 60mg　1回1錠　1日3回　毎食後　14日分\n" +
                "2) レバミピド錠 100mg　　 1回1錠　1日3回　毎食後　14日分\n" +
                "3) （継続）アムロジピン錠 5mg　1回1錠　1日1回　朝食後\n" +
                "4) （継続）エナラプリル錠 5mg　1回1錠　1日1回　朝食後\n\n" +
                "備考: 腰痛のため 3 か月前から 1) を継続。今回も同一処方。";

            data.patientOpening =
                "（患者）先生、いつもの腰の薬です。……最近ね、なんだか足がむくむんですよ。" +
                "あと、おしっこの出も前より悪い気がして。年のせいですかね。";

            data.counseling.Add(new CounselingOption(
                "むくみはいつ頃からですか。尿の量も減っている感じがありますか。",
                "（患者）そういえば 2 週間くらい前からかな。量は……確かに減ってる気がします。",
                true, "PHM-CNS-01",
                "患者の何気ない訴えから、有害事象の可能性を具体化できている。" +
                "浮腫と尿量減少は腎機能低下を示唆する所見であり、NSAIDs 長期投与と結びつく。"));

            data.counseling.Add(new CounselingOption(
                "お年を召すとよくあることですよ。お薬はきちんと飲めていますか。",
                "（患者）ええ、ちゃんと飲んでます。",
                false, "PHM-CNS-01",
                "訴えを年齢のせいとして流している。服薬状況の確認自体は必要だが、" +
                "有害事象を示唆する訴えを拾い損ねると、この先の疑義に気づけない。"));

            data.counseling.Add(new CounselingOption(
                "腰の痛みはいかがですか。お薬は効いていますか。",
                "（患者）痛みはまあまあです。",
                false, "PHM-CNS-01",
                "効果の確認は重要だが、患者が自分から出した「むくみ」「尿の出」という" +
                "手がかりを拾えていない。患者は重要な情報を、雑談の形で差し出してくることがある。"));

            data.labValues.Add(new LabValue(
                "scr", "血清クレアチニン (SCr)", "1.9 mg/dL", "基準 0.6-1.1（3か月前 1.0）",
                true,
                "3 か月で明らかに上昇している。NSAIDs と ACE 阻害薬（エナラプリル）の併用は" +
                "腎血流を低下させ、腎機能障害のリスクを高める。疑義照会の中核となる根拠。"));

            data.labValues.Add(new LabValue(
                "egfr", "eGFR", "26 mL/min/1.73m²", "3か月前 55",
                true,
                "腎機能が大きく低下している。NSAIDs の継続投与は避けるべき水準にあり、" +
                "用量調節ではなく中止・変更の検討が要る。"));

            data.labValues.Add(new LabValue(
                "k", "血清カリウム (K)", "5.6 mEq/L", "基準 3.5-5.0",
                true,
                "高カリウム血症。ACE 阻害薬と腎機能低下の組み合わせで生じやすく、" +
                "不整脈のリスクにつながる。緊急性を裏づける所見。"));

            data.labValues.Add(new LabValue(
                "hb", "ヘモグロビン (Hb)", "13.2 g/dL", "基準 13.0-17.0",
                false,
                "基準内。この処方の疑義とは直接関係しない。" +
                "関係のない値を根拠として挙げると、照会の説得力がむしろ落ちる。"));

            data.labValues.Add(new LabValue(
                "ast", "AST", "24 U/L", "基準 10-40",
                false,
                "基準内。肝機能は問題になっていない。"));

            data.labValues.Add(new LabValue(
                "glu", "血糖", "102 mg/dL", "基準 70-109",
                false,
                "基準内。今回の疑義とは無関係。"));

            data.inquiry.Add(new InquiryTurn(
                "（医師）はい D です。……ああ、C さんね。いつも通りの処方だけど、何か問題ある？　今から外来なんだけど。",
                new List<InquiryOption>
                {
                    new InquiryOption(
                        "C さんの腎機能について確認させてください。SCr が 3 か月で 1.0 から 1.9 に上昇し、" +
                        "eGFR が 26 まで低下しています。",
                        "（医師）……ん？　そんなに落ちてたか。",
                        -20, true, false, "PHM-SBR-01",
                        "具体的な数値と推移を最初に出している。多忙な相手には、" +
                        "結論に必要な事実を先に短く伝えるのが有効。"),
                    new InquiryOption(
                        "念のため、ロキソプロフェンの継続について確認したいのですが……",
                        "（医師）念のためって何。痛みがあるから出してるんだけど。",
                        20, false, false, "PHM-SBR-01",
                        "「念のため」は根拠が無いことの表明として受け取られる。" +
                        "多忙な相手ほど、曖昧な照会は取り合われない。"),
                    new InquiryOption(
                        "いえ、大丈夫です。このまま調剤します。",
                        "（医師）ああ、よろしく。（電話が切れる）",
                        0, false, true, "PHM-AUT-01",
                        "疑義を確認しないまま調剤に進んでいる。薬剤師法第24条は、" +
                        "疑わしい点を確かめた後でなければ調剤してはならないと定めている。")
                }));

            data.inquiry.Add(new InquiryTurn(
                "（医師）でも痛みは取れてるんでしょ。効いてる薬をやめる理由になる？",
                new List<InquiryOption>
                {
                    new InquiryOption(
                        "エナラプリルとの併用で腎血流がさらに低下します。K も 5.6 と上昇しており、" +
                        "このまま 14 日間の継続は避けたいと考えます。",
                        "（医師）……K も上がってるのか。それは確かにまずいな。",
                        -25, true, false, "PHM-ADR-01",
                        "相互作用の機序と、それを裏づける別の検査値を重ねている。" +
                        "「やめるべき」ではなく「なぜ危険か」を示せている。"),
                    new InquiryOption(
                        "そうですね……でも一応、気になったもので。",
                        "（医師）気になっただけ？　こっちも忙しいんだけど。",
                        25, false, false, "PHM-AUT-01",
                        "相手の反論に押されて主張が弱まっている。" +
                        "根拠を持っているのに引くのは、権威勾配下で最も起きやすい失敗。"),
                    new InquiryOption(
                        "分かりました。先生の判断に従います。",
                        "（医師）じゃあそのままで。（電話が切れる）",
                        0, false, true, "PHM-AUT-01",
                        "根拠を示せる状況で引き下がっている。" +
                        "チーム STEPPS の 2 チャレンジルールでは、懸念は最低 2 回主張することが求められる。")
                }));

            data.inquiry.Add(new InquiryTurn(
                "（医師）じゃあ、どうしたらいいと思う？",
                new List<InquiryOption>
                {
                    new InquiryOption(
                        "ロキソプロフェンを中止し、アセトアミノフェンへの変更をご検討いただけますか。" +
                        "腎機能の再検査もあわせてお願いできればと思います。",
                        "（医師）分かった。そうしよう。処方を出し直すよ。",
                        -30, false, false, "PHM-SBR-01",
                        "問題の指摘だけでなく、代替案を具体的に提示できている。" +
                        "相手が判断しやすい形にすることが、照会を通す実務上の鍵になる。"),
                    new InquiryOption(
                        "先生のご判断にお任せします。",
                        "（医師）……じゃあ、このままでいいか。",
                        20, false, false, "PHM-AUT-01",
                        "ここまで根拠を示しておきながら、最後に判断を委ねてしまっている。" +
                        "代替案が無いと、相手は現状維持を選びやすい。")
                }));

            return data;
        }
    }
}
