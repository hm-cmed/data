using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Gatekeeper
{
    /// <summary>
    /// ⑤ ゲートキーパー（希死念慮のアセスメントと危機介入）シミュレータ。
    ///
    /// 使い方: 空の GameObject にこのスクリプトを 1 つ付けて Play を押すだけ。
    ///
    /// 学習の主眼:
    ///   気づき・傾聴・つなぎ・見守り の 4 要素。
    ///   とくに「安易に励まさずに聴く」「直接尋ねる」「確実につなぐ」の 3 点。
    ///
    /// 取り扱いについて:
    ///   相談窓口の案内を全局面で常設し、いつでも中断できるようにしている。
    ///   学習者自身が当事者である可能性を前提とした作りにすること。
    /// </summary>
    public class GatekeeperSim : MonoBehaviour
    {
        private enum Phase
        {
            Briefing,
            Dialogue,
            Closing,
            Debrief
        }

        private const string ScenarioId = "gatekeeper_crisis_v1";
        private const int InitialTrust = 40;
        private const int InitialUrgency = 50;

        [Header("シナリオの内容")]
        [SerializeField, Tooltip("空のままだと、既定の題材（監修前の仮版）が使われる。")]
        private GatekeeperScenarioData scenario = new GatekeeperScenarioData();

        [Header("学習者")]
        [SerializeField] private string learnerId = string.Empty;

        [Header("表示")]
        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントを割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        [Header("開発")]
        [SerializeField] private bool echoToConsole = true;

        private GatekeeperTrainingLog _log;
        private Phase _phase = Phase.Briefing;
        private float _startedAt;

        // 学習者には最後まで見せない。
        private int _trust = InitialTrust;
        private int _urgency = InitialUrgency;

        private readonly HashSet<AssessmentItem> _checkedItems = new HashSet<AssessmentItem>();
        private readonly HashSet<int> _usedProbes = new HashSet<int>();
        private readonly List<string> _debriefNotes = new List<string>();
        private readonly List<string> _trace = new List<string>();

        private int _turnIndex;
        private string _residentReply = string.Empty;
        private bool _openHeart;

        /// <summary>直接確認を「適切なタイミングで」行えたか。時期尚早な試行はここに含めない。</summary>
        private bool _directQuestionAsked;

        /// <summary>信頼が足りない状態で踏み込んでしまった回数。</summary>
        private int _prematureAsks;

        private int _encouragementCount;
        private bool _aborted;
        private float _score;
        private string _savedLogPath = string.Empty;
        private Vector2 _scroll;

        // 1 シーンで複数のシナリオを同時に動かすと、それぞれが全画面 UI を描いて重なり、
        // どれも操作できなくなる。先に起動した 1 本だけを動かし、残りは待機させる。
        // 目印は自分の GameObject の子として作る。GameObject.Find は有効なオブジェクトしか
        // 見つけないため、Inspector でチェックを外せば次回 Play で別のシナリオが動く。
        private const string ActiveSimToken = "[CodemedX] ActiveSim";

        private GameObject _tokenObject;

        private void OnDestroy()
        {
            if (_tokenObject != null)
            {
                Destroy(_tokenObject);
            }
        }

        private void Awake()
        {
            if (GameObject.Find(ActiveSimToken) != null)
            {
                Debug.LogWarning(
                    "[Codemed-x] 別のシナリオが実行中のため " + GetType().Name + " は待機します。\n" +
                    "1 シーンで動かせるシナリオは 1 つだけです。切り替えるには、動かしたい" +
                    "GameObject 以外を Inspector のチェックボックスで無効にして Play し直してください。",
                    this);
                enabled = false;
                return;
            }

            _tokenObject = new GameObject(ActiveSimToken);
            _tokenObject.transform.SetParent(transform, false);

            if (scenario == null || scenario.IsEmpty)
            {
                scenario = GatekeeperScenarioData.CreateDefault();
            }

            _log = new GatekeeperTrainingLog(ScenarioId, learnerId);
            _log.EchoToConsole = echoToConsole;
        }

        private void Start()
        {
            _startedAt = Time.unscaledTime;
            _log.Log(EventKind.ScenarioStarted, string.Empty, 0f, ErrorKind.None,
                Payload.New().Add("turns", scenario.Turns.Count).Build());
        }

        private float ElapsedSeconds { get { return Time.unscaledTime - _startedAt; } }

        private void OnGUI()
        {
            if (uiFont != null)
            {
                GUI.skin.font = uiFont;
            }

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.label.wordWrap = true;
            GUI.skin.button.wordWrap = true;

            float width = Mathf.Min(Screen.width - 40f, 900f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 20f, width, Screen.height - 40f));

            // 相談窓口の案内は、シナリオの成否に関わらず全局面で常に表示する。
            GUILayout.Box(scenario.HelplineNotice);
            GUILayout.Space(4f);

            GUILayout.Label("ゲートキーパー研修シミュレーション");
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_phase)
            {
                case Phase.Briefing: DrawBriefing(); break;
                case Phase.Dialogue: DrawDialogue(); break;
                case Phase.Closing: DrawClosing(); break;
                default: DrawDebrief(); break;
            }

            GUILayout.EndScrollView();

            if (_phase != Phase.Debrief)
            {
                GUILayout.Space(4f);
                if (GUILayout.Button("演習を中断する"))
                {
                    _aborted = true;
                    Finish();
                }
            }

            GUILayout.EndArea();
        }

        private void DrawBriefing()
        {
            GUILayout.Label(scenario.Briefing);
            GUILayout.Space(10f);
            if (GUILayout.Button("話を聞く"))
            {
                _phase = Phase.Dialogue;
                _scroll = Vector2.zero;
            }
        }

        private void DrawDialogue()
        {
            if (_turnIndex < scenario.Turns.Count)
            {
                GatekeeperTurn turn = scenario.Turns[_turnIndex];
                GUILayout.Label(turn.ResidentLine);

                if (!string.IsNullOrEmpty(_residentReply))
                {
                    GUILayout.Space(6f);
                    GUILayout.Label("（直前の反応）" + _residentReply);
                }

                GUILayout.Space(10f);
                GUILayout.Label("どう応じるか:");

                IReadOnlyList<GatekeeperOption> options = turn.Options;
                for (int i = 0; i < options.Count; i++)
                {
                    if (GUILayout.Button(options[i].Text))
                    {
                        SelectOption(options[i], true);
                        return;
                    }
                }
            }
            else
            {
                GUILayout.Label(_residentReply);
                GUILayout.Space(10f);
            }

            // 直接的な問いかけは、条件を満たしていなくても常に画面に出す。
            // グレーアウトも非表示もしない。今聞くべきかを学習者自身が判断する構造にしないと訓練にならない。
            if (!_directQuestionAsked)
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("［直接尋ねる］" + scenario.DirectQuestion.Text))
                {
                    AskDirectQuestion();
                    return;
                }
            }

            if (_openHeart)
            {
                GUILayout.Space(10f);
                GUILayout.Label("確認する:");
                IReadOnlyList<GatekeeperOption> probes = scenario.Probes;
                for (int i = 0; i < probes.Count; i++)
                {
                    if (_usedProbes.Contains(i))
                    {
                        continue;
                    }

                    if (GUILayout.Button(probes[i].Text))
                    {
                        _usedProbes.Add(i);
                        SelectOption(probes[i], false);
                        return;
                    }
                }
            }

            if (_turnIndex >= scenario.Turns.Count)
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("話を締めくくる"))
                {
                    _phase = Phase.Closing;
                    _scroll = Vector2.zero;
                }
            }
        }

        private void DrawClosing()
        {
            GUILayout.Label("対話の終わりに、何を決めておくか。");
            GUILayout.Space(8f);

            IReadOnlyList<GatekeeperOption> options = scenario.ClosingOptions;
            for (int i = 0; i < options.Count; i++)
            {
                GatekeeperOption option = options[i];
                bool done = option.ChecksItem != AssessmentItem.None && _checkedItems.Contains(option.ChecksItem);

                if (done)
                {
                    GUILayout.Label("✓ " + option.Text);
                    continue;
                }

                if (GUILayout.Button(option.Text))
                {
                    SelectOption(option, false);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_residentReply))
            {
                GUILayout.Space(8f);
                GUILayout.Label("（直前の反応）" + _residentReply);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("見送る"))
            {
                Finish();
            }
        }

        private void AskDirectQuestion()
        {
            GatekeeperOption question = scenario.DirectQuestion;
            bool timely = _trust >= scenario.DirectQuestionTrustThreshold;

            if (timely)
            {
                _directQuestionAsked = true;
                ApplyDeltas(question.TrustDelta, question.UrgencyDelta, "直接尋ねた");
                _openHeart = true;
                _residentReply = question.Reply;
                _score += 12f;
                _checkedItems.Add(AssessmentItem.SuicidalIdeation);
                _debriefNotes.Add("直接尋ねた（適切なタイミング）\n　" + question.DebriefNote);

                _log.Log(EventKind.AssessmentItemChecked, "GTK-ASK-01", 12f, ErrorKind.None,
                    Payload.New()
                        .Add("item", "suicidal_ideation")
                        .Add("timely", true)
                        .Add("trust", _trust)
                        .Build());
            }
            else
            {
                // 時期尚早でも試行として閉じない。傾聴を重ねてから尋ね直せる余地を残す。
                _prematureAsks++;
                ApplyDeltas(-15, 5, "時期尚早な直接質問");
                _residentReply = "（E さん）……え。いえ、そんな。……なんでそんなこと聞くんですか。";
                _score -= 6f;
                _debriefNotes.Add(
                    "信頼が十分に築かれる前に踏み込んだ。尋ねること自体は必要だが、" +
                    "相手が「この人には話せる」と感じる前だと、問いが詰問として届く。" +
                    "傾聴を重ねてからもう一度、が原則。");

                _log.Log(EventKind.DialogueSelected, "GTK-ASK-01", -6f, ErrorKind.PrematureProbing,
                    Payload.New().Add("trust", _trust).Add("timely", false).Build());
            }
        }

        private void SelectOption(GatekeeperOption option, bool advanceTurn)
        {
            ApplyDeltas(option.TrustDelta, option.UrgencyDelta, option.Text);
            _residentReply = option.Reply;

            if (option.ErrorKindName == ErrorKind.InappropriateEncouragement)
            {
                _encouragementCount++;
            }

            float gained = option.ErrorKindName == ErrorKind.None ? 4f : -6f;
            _score += gained;

            if (option.ChecksItem != AssessmentItem.None && _checkedItems.Add(option.ChecksItem))
            {
                _score += 4f;
                _log.Log(EventKind.AssessmentItemChecked, option.ObjectiveId, 4f, ErrorKind.None,
                    Payload.New().Add("item", option.ChecksItem.ToString()).Build());

                if (option.ChecksItem == AssessmentItem.SafetyPlan)
                {
                    _log.Log(EventKind.SafetyPlanAgreed, "GTK-WCH-01", 0f, ErrorKind.None,
                        Payload.New().Add("agreed", true).Build());
                }
                else if (option.ChecksItem == AssessmentItem.Referral)
                {
                    _log.Log(EventKind.ReferralAgreed, "GTK-CON-01", 0f, ErrorKind.None,
                        Payload.New().Add("agreed", true).Build());
                }
            }

            if (!string.IsNullOrEmpty(option.DebriefNote))
            {
                _debriefNotes.Add("選択: " + option.Text + "\n　" + option.DebriefNote);
            }

            _log.Log(EventKind.DialogueSelected, option.ObjectiveId, gained, option.ErrorKindName,
                Payload.New()
                    .Add("turn", _turnIndex)
                    .Add("trust", _trust)
                    .Add("urgency", _urgency)
                    .Build());

            CheckThresholds();

            if (advanceTurn)
            {
                _turnIndex++;
            }
        }

        private void ApplyDeltas(int trustDelta, int urgencyDelta, string cause)
        {
            _trust = Mathf.Clamp(_trust + trustDelta, 0, 100);
            _urgency = Mathf.Clamp(_urgency + urgencyDelta, 0, 100);

            _trace.Add(string.Format(
                "信頼 {0,3} ／ 切迫 {1,3}　（{2}）",
                _trust, _urgency, cause.Length > 22 ? cause.Substring(0, 22) + "…" : cause));
        }

        private void CheckThresholds()
        {
            if (_trust <= 25)
            {
                _log.Log(EventKind.AffectThresholdCrossed, string.Empty, 0f, ErrorKind.None,
                    Payload.New()
                        .Add("parameter", "Trust")
                        .Add("value", _trust)
                        .Add("state", "Withdrawn")
                        .Build());
            }

            // 本音の開示は、適切なタイミングでの直接確認を経てのみ起きる。
        }

        private void Finish()
        {
            if (_phase == Phase.Debrief)
            {
                return;
            }

            bool hasSafetyPlan = _checkedItems.Contains(AssessmentItem.SafetyPlan);
            bool hasReferral = _checkedItems.Contains(AssessmentItem.Referral);
            bool success = hasSafetyPlan && hasReferral && !_aborted;

            if (!_directQuestionAsked)
            {
                _score -= 10f;
                _debriefNotes.Add(
                    "最後まで直接的な確認を行わなかった。尋ねないことは「安全な選択」ではなく、" +
                    "不作為の誤りにあたる。尋ねなければ危険の程度を評価できず、" +
                    "必要な支援につなぐ判断もできない。");

                _log.Log(EventKind.AssessmentItemChecked, "GTK-ASK-01", -10f,
                    ErrorKind.RiskAssessmentIncomplete,
                    Payload.New().Add("item", "suicidal_ideation").Add("checked", false).Build());
            }

            if (!hasReferral && !_aborted)
            {
                _debriefNotes.Add(
                    "専門機関へつながないまま対話を終えた。「つなぎ」は窓口を案内することではなく、" +
                    "確実に到達させること。同行や日程調整まで踏み込めるかで結果が変わる。");
            }

            _log.Log(success ? EventKind.ScenarioCompleted : EventKind.ScenarioFailed,
                string.Empty, _score,
                _directQuestionAsked ? ErrorKind.None : ErrorKind.RiskAssessmentIncomplete,
                Payload.New()
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Add("trust_final", _trust)
                    .Add("urgency_final", _urgency)
                    .Add("asked_directly", _directQuestionAsked)
                    .Add("premature_asks", _prematureAsks)
                    .Add("has_plan", _checkedItems.Contains(AssessmentItem.Plan))
                    .Add("has_means_access", _checkedItems.Contains(AssessmentItem.MeansAccess))
                    .Add("has_previous_attempt", _checkedItems.Contains(AssessmentItem.PreviousAttempt))
                    .Add("has_support", _checkedItems.Contains(AssessmentItem.Support))
                    .Add("has_safety_plan", hasSafetyPlan)
                    .Add("has_referral", hasReferral)
                    .Add("inappropriate_encouragement", _encouragementCount)
                    .Add("aborted", _aborted)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            _phase = Phase.Debrief;
            _scroll = Vector2.zero;
        }

        private void DrawDebrief()
        {
            if (_aborted)
            {
                GUILayout.Label("演習を中断しました。");
            }
            else if (_checkedItems.Contains(AssessmentItem.SafetyPlan)
                && _checkedItems.Contains(AssessmentItem.Referral))
            {
                GUILayout.Label("安全の約束と、専門機関への接続の両方が成立しました。");
            }
            else
            {
                GUILayout.Label("対話は終わりましたが、次につながる約束は成立していません。");
            }

            GUILayout.Space(8f);
            GUILayout.Label(string.Format("暫定スコア: {0:0.#} 点　／　所要 {1:0} 秒", _score, ElapsedSeconds));

            GUILayout.Space(10f);
            GUILayout.Label("確認できた項目:");
            DrawItem("希死念慮の直接確認", AssessmentItem.SuicidalIdeation);
            DrawItem("具体的な計画の有無", AssessmentItem.Plan);
            DrawItem("手段へのアクセス", AssessmentItem.MeansAccess);
            DrawItem("これまでの未遂歴", AssessmentItem.PreviousAttempt);
            DrawItem("周囲のサポート", AssessmentItem.Support);
            DrawItem("安全の約束", AssessmentItem.SafetyPlan);
            DrawItem("専門機関への接続", AssessmentItem.Referral);

            GUILayout.Space(10f);
            GUILayout.Label(string.Format(
                "相手の内面（対話中は非表示）: 信頼 {0} → {1}　／　切迫度 {2} → {3}",
                InitialTrust, _trust, InitialUrgency, _urgency));

            if (_prematureAsks > 0)
            {
                GUILayout.Label(string.Format(
                    "　信頼が足りない段階で踏み込んだ回数: {0} 回", _prematureAsks));
            }

            if (_encouragementCount > 0)
            {
                GUILayout.Label(string.Format(
                    "　安易な励ましを {0} 回選んでいます。", _encouragementCount));
                GUILayout.Label(
                    "　注目してほしいのは、励ましたあと相手が「そうですね」と落ち着いて見えるのに、" +
                    "切迫度が上がっている点です。表面上の反応と内側の状態は一致しません。" +
                    "だからこそ、切迫度は対話中に表示していません。");
            }

            if (_trace.Count > 0)
            {
                GUILayout.Space(10f);
                GUILayout.Label("推移:");
                for (int i = 0; i < _trace.Count; i++)
                {
                    GUILayout.Label("　" + _trace[i]);
                }
            }

            if (_debriefNotes.Count > 0)
            {
                GUILayout.Space(10f);
                GUILayout.Label("振り返り:");
                for (int i = 0; i < _debriefNotes.Count; i++)
                {
                    GUILayout.Label("・" + _debriefNotes[i]);
                    GUILayout.Space(4f);
                }
            }

            if (!string.IsNullOrEmpty(_savedLogPath))
            {
                GUILayout.Space(10f);
                GUILayout.Label("学習履歴の保存先:");
                GUILayout.Label(_savedLogPath);
                if (GUILayout.Button("保存先のパスをクリップボードにコピー"))
                {
                    GUIUtility.systemCopyBuffer = _savedLogPath;
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label(
                "注意: この題材は監修前の仮版です。実際の研修に使う前に、" +
                "精神保健の専門家によるレビューを必ず受けてください。");
        }

        private void DrawItem(string label, AssessmentItem item)
        {
            GUILayout.Label((_checkedItems.Contains(item) ? "　✓ " : "　－ ") + label);
        }
    }
}
