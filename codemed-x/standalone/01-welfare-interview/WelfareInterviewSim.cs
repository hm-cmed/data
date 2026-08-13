using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Welfare
{
    /// <summary>
    /// ① 相談援助面接シミュレータ（児童相談所の家庭訪問）。
    ///
    /// 使い方: 空の GameObject にこのスクリプトを 1 つ付けて Play を押すだけ。
    /// Canvas も Prefab も XR パッケージも要らない（画面は IMGUI で描いている）。
    /// まず「動くものを触って中身を詰める」ための土台であり、
    /// 見た目を整えるのは内容が固まってからで間に合う。
    ///
    /// 学習の主眼:
    ///   1. 観察した「事実」と自分の「解釈」を分けて集める
    ///   2. 拒否的な保護者との関係を切らずに情報を得る
    ///   3. 集めた事実を法的要件に照らして一時保護の要否を判断する
    ///   4. 上司へ、事実・判断・依頼を分けて報告する
    /// </summary>
    public class WelfareInterviewSim : MonoBehaviour
    {
        private enum Phase
        {
            Preparation,
            Observation,
            Interview,
            Assessment,
            Escalation,
            Debrief
        }

        private const string ScenarioId = "welfare_abuse_v1";
        private const int InitialTrust = 40;

        [Header("シナリオの内容")]
        [SerializeField, Tooltip("空のままだと、既定の題材（監修前の仮版）が使われる。")]
        private WelfareScenarioData scenario = new WelfareScenarioData();

        [Header("学習者")]
        [SerializeField, Tooltip("匿名化済みの学習者ID。空なら anonymous として記録する。")]
        private string learnerId = string.Empty;

        [Header("表示")]
        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントをここに割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        [Header("開発")]
        [SerializeField, Tooltip("記録したイベントを Console にも出す。")]
        private bool echoToConsole = true;

        private WelfareTrainingLog _log;
        private Phase _phase = Phase.Preparation;
        private float _startedAt;

        private readonly HashSet<int> _openedFiles = new HashSet<int>();
        private readonly HashSet<string> _observedIds = new HashSet<string>();
        private readonly List<string> _debriefNotes = new List<string>();

        private string _selectedObservableId = string.Empty;
        private int _turnIndex;
        private string _guardianReply = string.Empty;
        private int _guardianTrust = InitialTrust;
        private bool _interviewEndedEarly;

        private bool[] _groundChecks;
        private bool[] _reportChecks;
        private bool _custodyDecision = true;

        private float _score;
        private string _savedLogPath = string.Empty;
        private Vector2 _scroll;

        private void Awake()
        {
            if (scenario == null || scenario.IsEmpty)
            {
                scenario = WelfareScenarioData.CreateDefault();
            }

            _groundChecks = new bool[scenario.CustodyGrounds.Count];
            _reportChecks = new bool[scenario.ReportElements.Count];

            _log = new WelfareTrainingLog(ScenarioId, learnerId);
            _log.EchoToConsole = echoToConsole;
        }

        private void Start()
        {
            _startedAt = Time.unscaledTime;
            _log.Log(EventKind.ScenarioStarted, string.Empty, 0f, ErrorKind.None,
                Payload.New().Add("phase", _phase.ToString()).Build());
        }

        private float ElapsedSeconds
        {
            get { return Time.unscaledTime - _startedAt; }
        }

        private void OnGUI()
        {
            if (uiFont != null)
            {
                GUI.skin.font = uiFont;
            }

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.button.wordWrap = true;
            GUI.skin.label.wordWrap = true;
            GUI.skin.toggle.fontSize = fontSize;
            GUI.skin.toggle.wordWrap = true;

            float width = Mathf.Min(Screen.width - 40f, 900f);
            float height = Screen.height - 40f;
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 20f, width, height));

            DrawHeader();
            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_phase)
            {
                case Phase.Preparation: DrawPreparation(); break;
                case Phase.Observation: DrawObservation(); break;
                case Phase.Interview: DrawInterview(); break;
                case Phase.Assessment: DrawAssessment(); break;
                case Phase.Escalation: DrawEscalation(); break;
                case Phase.Debrief: DrawDebrief(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.Label("児童相談所 家庭訪問シミュレーション");
            GUILayout.Label(string.Format(
                "局面: {0}　　経過: {1:0} 秒", PhaseLabel(_phase), ElapsedSeconds));

            // 保護者の信頼度は面談中には出さない。数値を見て操作する練習になってしまうため、
            // 振り返りで初めて開示する。
            GUILayout.Space(6f);
        }

        private static string PhaseLabel(Phase phase)
        {
            switch (phase)
            {
                case Phase.Preparation: return "1. 事前確認";
                case Phase.Observation: return "2. 住環境の観察";
                case Phase.Interview: return "3. 保護者との面談";
                case Phase.Assessment: return "4. 一時保護の判断";
                case Phase.Escalation: return "5. 上司への報告";
                default: return "6. 振り返り";
            }
        }

        // ------------------------------------------------------------------ 1. 事前確認

        private void DrawPreparation()
        {
            GUILayout.Label(scenario.CaseSummary);
            GUILayout.Space(10f);
            GUILayout.Label("確認できる資料（読んだものは訪問時の判断材料になる）");

            for (int i = 0; i < scenario.CaseFiles.Count; i++)
            {
                bool opened = _openedFiles.Contains(i);
                if (GUILayout.Button((opened ? "✓ " : "　") + scenario.CaseFiles[i]))
                {
                    if (_openedFiles.Add(i))
                    {
                        _log.Log(EventKind.CaseFileOpened, "WLF-OBS-02", 1f, ErrorKind.None,
                            Payload.New().Add("file_index", i).Build());
                    }
                }
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("訪問する"))
            {
                // 資料を読まずに訪問した場合は、それ自体を逸脱として記録する。
                bool unprepared = _openedFiles.Count < scenario.CaseFiles.Count;
                if (unprepared)
                {
                    _score -= 3f;
                    _debriefNotes.Add(
                        "事前資料を読み切らずに訪問した。過去の通告歴や出欠の推移は、" +
                        "現地で見たものの意味を左右する。読まずに行くと『初めて見る家』としてしか判断できない。");
                }

                _log.Log(EventKind.StateChanged, "WLF-OBS-02", unprepared ? -3f : 3f,
                    unprepared ? ErrorKind.UnpreparedVisit : ErrorKind.None,
                    Payload.New()
                        .Add("files_opened", _openedFiles.Count)
                        .Add("files_total", scenario.CaseFiles.Count)
                        .Build());

                if (!unprepared)
                {
                    _score += 3f;
                }

                GoTo(Phase.Observation);
            }
        }

        // ------------------------------------------------------------------ 2. 観察

        private void DrawObservation()
        {
            GUILayout.Label(
                "玄関を上がらせてもらった。気になったところを見ていく。\n" +
                "見たものは「観察した事実」として記録される。解釈は後の段階で行う。");
            GUILayout.Space(8f);

            IReadOnlyList<Observable> items = scenario.Observables;
            for (int i = 0; i < items.Count; i++)
            {
                Observable item = items[i];
                bool seen = _observedIds.Contains(item.Id);

                if (GUILayout.Button((seen ? "✓ " : "　") + item.Label))
                {
                    _selectedObservableId = item.Id;
                    if (_observedIds.Add(item.Id))
                    {
                        float gained = item.IsRiskSign ? (item.IsCritical ? 4f : 2f) : 0f;
                        _score += gained;
                        _log.Log(EventKind.RiskSignObserved, "WLF-OBS-01", gained, ErrorKind.None,
                            Payload.New()
                                .Add("target", item.Id)
                                .Add("risk_sign", item.IsRiskSign)
                                .Add("critical", item.IsCritical)
                                .Build());
                    }
                }

                if (_selectedObservableId == item.Id)
                {
                    GUILayout.Label("　→ " + item.Detail);
                    GUILayout.Space(4f);
                }
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("保護者と話す"))
            {
                ReportMissedObservations();
                GoTo(Phase.Interview);
            }
        }

        /// <summary>
        /// 見落としは「イベントが無いこと」ではなく、明示的に記録する。
        /// 通信欠損と区別できないログでは、教育上いちばん大事なデータが取れない。
        /// </summary>
        private void ReportMissedObservations()
        {
            IReadOnlyList<Observable> items = scenario.Observables;
            for (int i = 0; i < items.Count; i++)
            {
                Observable item = items[i];
                if (!item.IsRiskSign || _observedIds.Contains(item.Id))
                {
                    continue;
                }

                float penalty = item.IsCritical ? -5f : -2f;
                _score += penalty;
                _debriefNotes.Add("見落とし: " + item.Label + "\n　" + item.DebriefNote);

                _log.Log(EventKind.ObservationMissed, "WLF-OBS-01", penalty,
                    item.IsCritical ? ErrorKind.MissedRiskSign : ErrorKind.None,
                    Payload.New().Add("target", item.Id).Add("critical", item.IsCritical).Build());
            }
        }

        // ------------------------------------------------------------------ 3. 面談

        private void DrawInterview()
        {
            if (_interviewEndedEarly)
            {
                GUILayout.Label(
                    "玄関の戸が閉まった。これ以上の聴き取りはできない。\n" +
                    "得られた情報が限られたまま、判断しなければならない。");
                GUILayout.Space(10f);
                if (GUILayout.Button("判断に進む"))
                {
                    GoTo(Phase.Assessment);
                }

                return;
            }

            if (_turnIndex >= scenario.Interview.Count)
            {
                GUILayout.Label("面談を終えた。");
                GUILayout.Space(10f);
                if (GUILayout.Button("判断に進む"))
                {
                    GoTo(Phase.Assessment);
                }

                return;
            }

            InterviewTurn turn = scenario.Interview[_turnIndex];

            GUILayout.Label("保護者:");
            GUILayout.Label("　" + turn.GuardianLine);

            if (!string.IsNullOrEmpty(_guardianReply))
            {
                GUILayout.Space(6f);
                GUILayout.Label("（直前の返答）" + _guardianReply);
            }

            GUILayout.Space(10f);
            GUILayout.Label("どう応じるか:");

            IReadOnlyList<Utterance> options = turn.Options;
            for (int i = 0; i < options.Count; i++)
            {
                if (GUILayout.Button(options[i].Text))
                {
                    SelectUtterance(options[i]);
                    return;
                }
            }
        }

        private void SelectUtterance(Utterance choice)
        {
            _guardianTrust = Mathf.Clamp(_guardianTrust + choice.TrustDelta, 0, 100);
            _guardianReply = choice.Reply;

            float gained = choice.TrustDelta > 0 ? 3f : (choice.TrustDelta < 0 ? -4f : 0f);
            _score += gained;

            if (!string.IsNullOrEmpty(choice.DebriefNote))
            {
                _debriefNotes.Add("面談での選択: " + choice.Text + "\n　" + choice.DebriefNote);
            }

            _log.Log(EventKind.DialogueSelected, choice.ObjectiveId, gained, choice.ErrorKindName,
                Payload.New()
                    .Add("turn", _turnIndex)
                    .Add("trust_delta", choice.TrustDelta)
                    .Add("guardian_trust", _guardianTrust)
                    .Build());

            _turnIndex++;

            // 信頼が失われると、相手は語るのをやめる。情報が欠けたまま判断させることに意味がある。
            if (_guardianTrust <= 15)
            {
                _interviewEndedEarly = true;
                _debriefNotes.Add(
                    "保護者との関係が切れ、面談が途中で終わった。判断の材料が不足したまま" +
                    "決めざるを得なくなる。関係を保つことは、それ自体が安全確認の手段になる。");
            }
        }

        // ------------------------------------------------------------------ 4. 判断

        private void DrawAssessment()
        {
            GUILayout.Label(
                "ここまでに得た事実をもとに、一時保護（児童福祉法第33条）の要否を判断する。");
            GUILayout.Space(8f);

            GUILayout.Label("判断:");
            if (GUILayout.Toggle(_custodyDecision, " 一時保護が必要である"))
            {
                _custodyDecision = true;
            }

            if (GUILayout.Toggle(!_custodyDecision, " 一時保護は要しない（在宅で支援する）"))
            {
                _custodyDecision = false;
            }

            GUILayout.Space(10f);
            GUILayout.Label("その根拠として挙げるもの（複数選択）:");
            for (int i = 0; i < scenario.CustodyGrounds.Count; i++)
            {
                _groundChecks[i] = GUILayout.Toggle(_groundChecks[i], " " + scenario.CustodyGrounds[i]);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("判断を提出する"))
            {
                SubmitAssessment();
            }
        }

        private void SubmitAssessment()
        {
            bool decisionCorrect = _custodyDecision == scenario.CustodyIsRequired;

            int validPicked = 0;
            int invalidPicked = 0;
            for (int i = 0; i < _groundChecks.Length; i++)
            {
                if (!_groundChecks[i])
                {
                    continue;
                }

                if (IsValidGround(i))
                {
                    validPicked++;
                }
                else
                {
                    invalidPicked++;
                    _debriefNotes.Add(
                        "根拠にできないものを挙げている: " + scenario.CustodyGrounds[i] +
                        "\n　態度や生活水準への印象は、法的要件の判断材料にはならない。" +
                        "観察された事実と、児童の安全に対する具体的な危険で説明する必要がある。");
                }
            }

            float gained = (decisionCorrect ? 10f : -10f) + validPicked * 3f - invalidPicked * 3f;
            _score += gained;

            if (!decisionCorrect)
            {
                _debriefNotes.Add(
                    "一時保護の要否の判断が、集まった事実と整合していない。" +
                    "身体的暴力への言及と、説明の付かない多発性のあざが揃った時点で、" +
                    "児童の安全を家庭内で確保できるとは言いにくい。");
            }

            if (decisionCorrect && validPicked == 0)
            {
                _debriefNotes.Add(
                    "結論は妥当だが、根拠が示されていない。結論だけの報告は上司も判断できず、" +
                    "記録としても後から検証できない。");
            }

            _log.Log(EventKind.AssessmentSubmitted, "WLF-LAW-01", gained,
                decisionCorrect ? ErrorKind.None : ErrorKind.UnjustifiedCustodyDecision,
                Payload.New()
                    .Add("decision", _custodyDecision ? "custody" : "home_support")
                    .Add("correct", decisionCorrect)
                    .Add("valid_grounds", validPicked)
                    .Add("invalid_grounds", invalidPicked)
                    .Build());

            GoTo(Phase.Escalation);
        }

        private bool IsValidGround(int index)
        {
            IReadOnlyList<int> valid = scenario.ValidGroundIndices;
            for (int i = 0; i < valid.Count; i++)
            {
                if (valid[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ 5. 報告

        private void DrawEscalation()
        {
            GUILayout.Label("上司に電話で報告する。伝える内容を選ぶ（複数選択）:");
            GUILayout.Space(8f);

            for (int i = 0; i < scenario.ReportElements.Count; i++)
            {
                _reportChecks[i] = GUILayout.Toggle(_reportChecks[i], " " + scenario.ReportElements[i]);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("報告する"))
            {
                SubmitEscalation();
            }
        }

        private void SubmitEscalation()
        {
            int requiredIncluded = 0;
            int noiseIncluded = 0;

            for (int i = 0; i < _reportChecks.Length; i++)
            {
                if (!_reportChecks[i])
                {
                    continue;
                }

                if (IsRequiredReportElement(i))
                {
                    requiredIncluded++;
                }
                else
                {
                    noiseIncluded++;
                }
            }

            int requiredTotal = scenario.RequiredReportIndices.Count;
            float gained = requiredIncluded * 3f - noiseIncluded * 2f;
            _score += gained;

            if (requiredIncluded < requiredTotal)
            {
                _debriefNotes.Add(
                    "報告に欠けている要素がある。上司が判断できる報告には、" +
                    "「観察した事実」「保護者の発言」「危険と判断した理由」「求める指示」が要る。");
            }

            if (noiseIncluded > 0)
            {
                _debriefNotes.Add(
                    "報告に主観的な印象が混ざっている。印象は判断の根拠にならず、" +
                    "記録に残ると以降の関係者の見方を歪める。");
            }

            _log.Log(EventKind.EscalationPerformed, "WLF-ESC-01", gained,
                requiredIncluded < requiredTotal ? ErrorKind.IncompleteEscalation : ErrorKind.None,
                Payload.New()
                    .Add("required_included", requiredIncluded)
                    .Add("required_total", requiredTotal)
                    .Add("subjective_included", noiseIncluded)
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());

            FinishScenario();
        }

        private bool IsRequiredReportElement(int index)
        {
            IReadOnlyList<int> required = scenario.RequiredReportIndices;
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ 6. 振り返り

        private void FinishScenario()
        {
            int riskSignsFound = 0;
            int riskSignsTotal = 0;
            IReadOnlyList<Observable> items = scenario.Observables;
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].IsRiskSign)
                {
                    continue;
                }

                riskSignsTotal++;
                if (_observedIds.Contains(items[i].Id))
                {
                    riskSignsFound++;
                }
            }

            _log.Log(EventKind.ScenarioCompleted, string.Empty, _score, ErrorKind.None,
                Payload.New()
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Add("risk_signs_found", riskSignsFound)
                    .Add("risk_signs_total", riskSignsTotal)
                    .Add("guardian_trust_final", _guardianTrust)
                    .Add("interview_ended_early", _interviewEndedEarly)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            GoTo(Phase.Debrief);
        }

        private void DrawDebrief()
        {
            GUILayout.Label(string.Format("暫定スコア: {0:0.#} 点", _score));
            GUILayout.Label(string.Format(
                "所要時間: {0:0} 秒　／　記録したイベント: {1} 件", ElapsedSeconds, _log.Events.Count));
            GUILayout.Space(6f);

            GUILayout.Label(string.Format(
                "保護者の信頼度（面談中は非表示）: {0} → {1}", InitialTrust, _guardianTrust));
            GUILayout.Label(
                "　面談中に数値が見えていると、相手の反応ではなく数値を見て操作する練習になる。" +
                "実際の面接では手がかりは相手の表情と言葉しかないため、ここで初めて開示している。");

            GUILayout.Space(10f);
            if (_debriefNotes.Count == 0)
            {
                GUILayout.Label("大きな逸脱はありませんでした。");
            }
            else
            {
                GUILayout.Label("振り返り:");
                for (int i = 0; i < _debriefNotes.Count; i++)
                {
                    GUILayout.Label("・" + _debriefNotes[i]);
                    GUILayout.Space(4f);
                }
            }

            GUILayout.Space(10f);
            if (!string.IsNullOrEmpty(_savedLogPath))
            {
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
                "児童福祉の実務者によるレビューを必ず受けてください。");
        }

        private void GoTo(Phase next)
        {
            Phase previous = _phase;
            _phase = next;
            _scroll = Vector2.zero;

            _log.Log(EventKind.StateChanged, string.Empty, 0f, ErrorKind.None,
                Payload.New()
                    .Add("from", previous.ToString())
                    .Add("to", next.ToString())
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());
        }
    }
}
