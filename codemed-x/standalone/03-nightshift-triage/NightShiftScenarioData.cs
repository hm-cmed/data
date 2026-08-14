using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.NightShift
{
    /// <summary>
    /// 夜勤中に発生するタスク 1 件。
    /// 緊急度と放置の許容時間を持たせることで、優先順位の当否を機械的に判定できる。
    /// </summary>
    [Serializable]
    public class ShiftTask
    {
        [SerializeField] private string id = "task";
        [SerializeField, TextArea(1, 3)] private string label = string.Empty;
        [SerializeField, TextArea(2, 4)] private string detail = string.Empty;

        [SerializeField, Tooltip("発生する時刻（開始からの秒数）。")]
        private float startsAtSeconds;

        [SerializeField, Tooltip("数値が大きいほど緊急。生命に直結するものを高くする。")]
        private int priority = 1;

        [SerializeField, Tooltip("対応に必要な秒数。")]
        private float workSeconds = 8f;

        [SerializeField, Tooltip("発生から何秒放置すると重大化するか。0 なら重大化しない。")]
        private float neglectSeconds = 15f;

        [SerializeField, TextArea(2, 4), Tooltip("放置して重大化したときに起きること。")]
        private string neglectOutcome = string.Empty;

        [SerializeField, Tooltip("急変にあたるか。エスカレーションまでの時間を測る基準になる。")]
        private bool isDeterioration;

        [SerializeField] private string objectiveId = string.Empty;

        [SerializeField, TextArea(2, 4)] private string debriefNote = string.Empty;

        public string Id { get { return id; } }
        public string Label { get { return label; } }
        public string Detail { get { return detail; } }
        public float StartsAtSeconds { get { return startsAtSeconds; } }
        public int Priority { get { return priority; } }
        public float WorkSeconds { get { return workSeconds; } }
        public float NeglectSeconds { get { return neglectSeconds; } }
        public string NeglectOutcome { get { return neglectOutcome; } }
        public bool IsDeterioration { get { return isDeterioration; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string DebriefNote { get { return debriefNote; } }

        public ShiftTask() { }

        public ShiftTask(
            string id, string label, string detail, float startsAtSeconds, int priority,
            float workSeconds, float neglectSeconds, string neglectOutcome,
            bool isDeterioration, string objectiveId, string debriefNote)
        {
            this.id = id;
            this.label = label;
            this.detail = detail;
            this.startsAtSeconds = startsAtSeconds;
            this.priority = priority;
            this.workSeconds = workSeconds;
            this.neglectSeconds = neglectSeconds;
            this.neglectOutcome = neglectOutcome;
            this.isDeterioration = isDeterioration;
            this.objectiveId = objectiveId;
            this.debriefNote = debriefNote;
        }
    }

    /// <summary>③ の中身。時間台本と配点はここに集約してある。</summary>
    [Serializable]
    public class NightShiftScenarioData
    {
        [SerializeField, TextArea(4, 10)] private string briefing = string.Empty;

        [SerializeField, Tooltip("制限時間（秒）。")]
        private float timeLimitSeconds = 180f;

        [SerializeField] private List<ShiftTask> tasks = new List<ShiftTask>();

        [SerializeField, Tooltip("SBAR の各要素。報告時に選ばせる。")]
        private List<string> sbarElements = new List<string>();

        [SerializeField, Tooltip("報告に必ず含めるべき要素（sbarElements の添字）。")]
        private List<int> requiredSbarIndices = new List<int>();

        public string Briefing { get { return briefing; } }
        public float TimeLimitSeconds { get { return timeLimitSeconds; } }
        public IReadOnlyList<ShiftTask> Tasks { get { return tasks; } }
        public IReadOnlyList<string> SbarElements { get { return sbarElements; } }
        public IReadOnlyList<int> RequiredSbarIndices { get { return requiredSbarIndices; } }

        public bool IsEmpty { get { return tasks.Count == 0; } }

        /// <summary>監修前の仮版。実際の研修に使う前に看護実務者のレビューを受けること。</summary>
        public static NightShiftScenarioData CreateDefault()
        {
            NightShiftScenarioData data = new NightShiftScenarioData();

            data.briefing =
                "【場面】\n" +
                "一般病棟の夜勤。深夜 2 時。あなたは受け持ち 10 名を一人で見ている。\n" +
                "他のスタッフは別フロアの対応に出ており、当直医は仮眠中。\n\n" +
                "これから 3 分間に、複数のことが同時に起きる。\n" +
                "すべてに完璧に対応することはできない。\n" +
                "何を先にやるか、そしていつ人を呼ぶかが問われる。\n\n" +
                "・タスクは発生した順に一覧へ追加される\n" +
                "・「対応する」を押すと、その処置に時間を使う（その間も他は進行する）\n" +
                "・「医師に電話する」はいつでも選べる";

            data.tasks.Add(new ShiftTask(
                "patientA_spo2", "患者A: SpO2 低下のアラーム",
                "モニタが SpO2 86% を示して鳴っている。呼吸数 28 回/分、努力呼吸あり。" +
                "肺炎で入院中の 82 歳男性。",
                10f, 10, 10f, 15f,
                "低酸素が遷延し、意識レベルが低下した。",
                true, "NGT-MON-01",
                "生命に直結する所見であり、この場面で最も緊急度が高い。" +
                "一次対応（体位・酸素）と並行して、応援を呼ぶ判断が要る。"));

            data.tasks.Add(new ShiftTask(
                "patientB_fall", "患者B: ベッドサイドで立ち上がろうとしている",
                "認知症のある 78 歳女性。点滴ルートを引っ張りながら柵を越えようとしている。" +
                "転倒歴あり。",
                30f, 8, 8f, 12f,
                "自己抜管と転倒が発生した。前腕から出血している。",
                false, "NGT-SAF-01",
                "予防的に介入すれば防げる事象。起きてから対応するのと、" +
                "起きる前に止めるのとでは、患者の被害も業務量もまったく違う。"));

            data.tasks.Add(new ShiftTask(
                "phone_admin", "ナースステーションの電話",
                "外線。明日の入院予定に関する事務連絡。",
                60f, 2, 6f, 0f,
                string.Empty,
                false, "NGT-TRI-01",
                "緊急度は最も低い。後回しにしても患者に不利益は生じない。" +
                "鳴っているものに反射的に反応してしまうのが、時間圧下で起きやすい失敗。"));

            data.tasks.Add(new ShiftTask(
                "nurse_calls", "複数病室のナースコールが同時点灯",
                "3 号室と 5 号室。内容は不明。",
                90f, 5, 10f, 30f,
                "対応が遅れ、患者から強い苦情が出た。",
                false, "NGT-TRI-01",
                "内容が分からない以上、緊急度は判断できない。" +
                "まず用件だけ確認して振り分けるのが現実的な対処になる。"));

            data.sbarElements.Add("S: 患者A の SpO2 が 86% まで低下し、努力呼吸がある");
            data.sbarElements.Add("B: 肺炎で入院中の 82 歳男性、既往に COPD");
            data.sbarElements.Add("A: 呼吸不全が進行していると考えられる");
            data.sbarElements.Add("R: 至急、診察をお願いしたい");
            data.sbarElements.Add("夜勤で人手が足りず困っている");
            data.requiredSbarIndices.Add(0);
            data.requiredSbarIndices.Add(1);
            data.requiredSbarIndices.Add(2);
            data.requiredSbarIndices.Add(3);

            return data;
        }
    }
}
