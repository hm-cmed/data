using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Acp
{
    /// <summary>
    /// ② 困難な対話シミュレータ（終末期 ACP / SPIKES）。
    ///
    /// 使い方: 空の GameObject にこのスクリプトを 1 つ付けて Play を押すだけ。
    ///
    /// 学習の主眼:
    ///   1. SPIKES の順序を踏む（環境設定・認識確認・同意・情報提供・感情・方針）
    ///   2. 患者と家族という利害の異なる 2 者を、どちらも切り捨てずに扱う
    ///   3. 沈黙を埋めずに待つ
    /// </summary>
    public class AcpDialogueSim : MonoBehaviour
    {
        private enum Phase
        {
            Briefing,
            Dialogue,
            Debrief
        }

        private const string ScenarioId = "acp_dialogue_v1";

        [Header("シナリオの内容")]
        [SerializeField, Tooltip("空のままだと、既定の題材（監修前の仮版）が使われる。")]
        private AcpScenarioData scenario = new AcpScenarioData();

        [Header("学習者")]
        [SerializeField] private string learnerId = string.Empty;

        [Header("表示")]
        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントを割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        [Header("開発")]
        [SerializeField] private bool echoToConsole = true;

        private AcpTrainingLog _log;
        private Phase _phase = Phase.Briefing;

        /// <summary>
        /// 現在の局面の名前。背景や BGM を局面ごとに切り替えたいときに、
        /// 外部（SimBackdrop 等）から参照される。
        /// このシナリオ自身は表示に関与しないので、依存は一方向のまま保たれる。
        /// </summary>
        public string CurrentPhaseName { get { return _phase.ToString(); } }
        private float _startedAt;

        // 隠れパラメータ。対話中は一切表示しない。
        private int _patientTrust = 50;
        private int _patientAnxiety = 40;
        private int _patientComprehension = 50;
        private int _familyTrust = 50;
        private int _familyAnxiety = 45;

        private readonly List<string> _debriefNotes = new List<string>();
        private readonly List<string> _trace = new List<string>();
        private readonly HashSet<string> _crossedThresholds = new HashSet<string>();

        private int _stepIndex;
        private string _npcReply = string.Empty;
        private float _stepEnteredAt;
        private bool _silenceResolved = true;
        private int _skippedSteps;
        private int _interruptedSilences;
        private bool _breakdown;
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
                scenario = AcpScenarioData.CreateDefault();
            }

            _log = new AcpTrainingLog(ScenarioId, learnerId);
            _log.EchoToConsole = echoToConsole;
        }

        private void Start()
        {
            _startedAt = Time.unscaledTime;
            _log.Log(EventKind.ScenarioStarted, string.Empty, 0f, ErrorKind.None,
                Payload.New().Add("steps", scenario.Steps.Count).Build());
        }

        private float ElapsedSeconds { get { return Time.unscaledTime - _startedAt; } }

        private void Update()
        {
            // 沈黙を最後まで待てた場合は、その時点で記録する。
            if (_silenceResolved || _phase != Phase.Dialogue)
            {
                return;
            }

            if (Time.unscaledTime - _stepEnteredAt < scenario.SilenceSeconds)
            {
                return;
            }

            _silenceResolved = true;
            _score += 4f;
            _log.Log(EventKind.SilenceRespected, "ACP-EMP-01", 4f, ErrorKind.None,
                Payload.New()
                    .Add("step", _stepIndex)
                    .Add("waited_sec", scenario.SilenceSeconds)
                    .Build());
        }

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

            GUILayout.Label("終末期の話し合い（人生会議）シミュレーション");
            GUILayout.Label(string.Format("経過: {0:0} 秒", ElapsedSeconds));
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_phase == Phase.Briefing)
            {
                DrawBriefing();
            }
            else if (_phase == Phase.Dialogue)
            {
                DrawDialogue();
            }
            else
            {
                DrawDebrief();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawBriefing()
        {
            GUILayout.Label(scenario.Briefing);
            GUILayout.Space(10f);
            if (GUILayout.Button("面談を始める"))
            {
                _phase = Phase.Dialogue;
                EnterStep(0);
            }
        }

        private void DrawDialogue()
        {
            if (_breakdown || _stepIndex >= scenario.Steps.Count)
            {
                Finish();
                return;
            }

            AcpStep step = scenario.Steps[_stepIndex];

            GUILayout.Label(string.Format("[{0}/{1}] {2}",
                _stepIndex + 1, scenario.Steps.Count, step.StepLabel));
            GUILayout.Space(4f);
            GUILayout.Label(step.Situation);
            GUILayout.Space(6f);
            GUILayout.Label(step.NpcLine);

            if (!string.IsNullOrEmpty(_npcReply))
            {
                GUILayout.Space(6f);
                GUILayout.Label("（直前の反応）" + _npcReply);
            }

            if (step.IsSilence)
            {
                GUILayout.Space(6f);
                GUILayout.Label("——（沈黙）——");
            }

            GUILayout.Space(10f);
            GUILayout.Label("どう応じるか:");

            IReadOnlyList<AcpOption> options = step.Options;
            for (int i = 0; i < options.Count; i++)
            {
                if (GUILayout.Button(options[i].Text))
                {
                    Select(step, options[i]);
                    return;
                }
            }

            GUILayout.Space(10f);
            // ステップを飛ばせるようにしておく。止めてしまうと、何が悪いのかを体験できない。
            if (GUILayout.Button("（このステップを飛ばして先に進む）"))
            {
                SkipStep(step);
            }
        }

        private void Select(AcpStep step, AcpOption option)
        {
            // 沈黙の場面で待ち切らずに選んだ場合は、選択の当否とは別に記録する。
            if (step.IsSilence && !_silenceResolved)
            {
                _silenceResolved = true;
                _interruptedSilences++;
                _score -= 5f;
                _debriefNotes.Add(
                    "沈黙を待たずに言葉を挟んだ。沈黙は空白ではなく、相手が受け止めるための時間。" +
                    "埋めたくなるのは話す側の不安であって、相手の必要ではない。");

                _log.Log(EventKind.DialogueSelected, "ACP-EMP-01", -5f, ErrorKind.InterruptedSilence,
                    Payload.New()
                        .Add("step", _stepIndex)
                        .Add("waited_sec", Time.unscaledTime - _stepEnteredAt)
                        .Add("required_sec", scenario.SilenceSeconds)
                        .Build());
            }

            ApplyDeltas(option);
            _npcReply = option.Reply;

            float gained = option.ErrorKindName == ErrorKind.None ? 4f : -4f;
            _score += gained;

            if (!string.IsNullOrEmpty(option.DebriefNote))
            {
                _debriefNotes.Add(step.StepLabel + "\n　選択: " + option.Text + "\n　" + option.DebriefNote);
            }

            _trace.Add(string.Format("{0}: 患者T{1} A{2} C{3} ／ 家族T{4} A{5}",
                step.StepLabel, _patientTrust, _patientAnxiety, _patientComprehension,
                _familyTrust, _familyAnxiety));

            // 判定に効いた値は両者ぶん残す。片方だけだと後から何が起きたか再現できない。
            _log.Log(EventKind.DialogueSelected, option.ObjectiveId, gained, option.ErrorKindName,
                BuildStatePayload().Add("step", _stepIndex).Build());

            _log.Log(EventKind.ProtocolStepCompleted, step.ObjectiveId, 0f, ErrorKind.None,
                Payload.New().Add("step_label", step.StepLabel).Build());

            CheckThresholds();

            if (!_breakdown)
            {
                EnterStep(_stepIndex + 1);
            }
        }

        private void SkipStep(AcpStep step)
        {
            _skippedSteps++;
            _score -= 6f;
            _debriefNotes.Add(
                step.StepLabel + " を飛ばした。\n　" +
                "SPIKES の各ステップは、次のステップが成立するための前提を作っている。" +
                "飛ばしても話は進むが、相手の準備が整わないまま情報だけが渡される。");

            _log.Log(EventKind.ProtocolStepCompleted, step.ObjectiveId, -6f, ErrorKind.ProtocolStepSkipped,
                Payload.New().Add("step_label", step.StepLabel).Add("skipped", true).Build());

            EnterStep(_stepIndex + 1);
        }

        private void ApplyDeltas(AcpOption option)
        {
            _patientTrust = Mathf.Clamp(_patientTrust + option.PatientTrust, 0, 100);
            _patientAnxiety = Mathf.Clamp(_patientAnxiety + option.PatientAnxiety, 0, 100);
            _patientComprehension = Mathf.Clamp(_patientComprehension + option.PatientComprehension, 0, 100);
            _familyTrust = Mathf.Clamp(_familyTrust + option.FamilyTrust, 0, 100);
            _familyAnxiety = Mathf.Clamp(_familyAnxiety + option.FamilyAnxiety, 0, 100);
        }

        /// <summary>閾値を跨いだ瞬間だけ記録する。内側で動き続けても再発火させない。</summary>
        private void CheckThresholds()
        {
            TryCross("family_agitated", _familyAnxiety >= 80, "family", "Anxiety", _familyAnxiety, "Agitated",
                "家族が感情的に激高した状態に入った。ここから先は、家族への対応を挟まないと" +
                "本人との合意も成立しなくなる。");

            TryCross("patient_withdrawn", _patientTrust <= 20, "patient", "Trust", _patientTrust, "Withdrawn",
                "患者が語らなくなった。以降に得られる「同意」は、本人の意思を反映していない。");

            TryCross("patient_confused", _patientComprehension <= 30, "patient", "Comprehension",
                _patientComprehension, "Confused",
                "患者の理解が伴っていない。この状態で合意を取っても、" +
                "それは意思決定の支援ではなく手続きの消化になる。");

            if (_familyAnxiety >= 95 || _patientTrust <= 10)
            {
                _breakdown = true;
            }
        }

        private void TryCross(
            string key, bool crossed, string actor, string parameter, int value, string state, string note)
        {
            if (!crossed || !_crossedThresholds.Add(key))
            {
                return;
            }

            _debriefNotes.Add(note);
            _log.Log(EventKind.AffectThresholdCrossed, string.Empty, 0f, ErrorKind.None,
                Payload.New()
                    .Add("actor", actor)
                    .Add("parameter", parameter)
                    .Add("value", value)
                    .Add("state", state)
                    .Build());
        }

        private Payload BuildStatePayload()
        {
            return Payload.New()
                .Add("patient_trust", _patientTrust)
                .Add("patient_anxiety", _patientAnxiety)
                .Add("patient_comprehension", _patientComprehension)
                .Add("family_trust", _familyTrust)
                .Add("family_anxiety", _familyAnxiety);
        }

        private void EnterStep(int index)
        {
            _stepIndex = index;
            _stepEnteredAt = Time.unscaledTime;
            _scroll = Vector2.zero;

            bool isSilence = index >= 0 && index < scenario.Steps.Count && scenario.Steps[index].IsSilence;
            _silenceResolved = !isSilence;

            if (index < scenario.Steps.Count)
            {
                _log.Log(EventKind.StateChanged, string.Empty, 0f, ErrorKind.None,
                    Payload.New().Add("step", index).Add("elapsed_sec", ElapsedSeconds).Build());
            }
        }

        private void Finish()
        {
            bool success = !_breakdown && _patientTrust >= 50 && _familyTrust >= 50;

            _log.Log(success ? EventKind.ScenarioCompleted : EventKind.ScenarioFailed,
                string.Empty, _score, ErrorKind.None,
                BuildStatePayload()
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Add("skipped_steps", _skippedSteps)
                    .Add("interrupted_silences", _interruptedSilences)
                    .Add("breakdown", _breakdown)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            _phase = Phase.Debrief;
            _scroll = Vector2.zero;
        }

        private void DrawDebrief()
        {
            if (_breakdown)
            {
                GUILayout.Label("対話が決裂した。方針の合意には至らなかった。");
            }
            else if (_patientTrust >= 50 && _familyTrust >= 50)
            {
                GUILayout.Label("本人と家族の双方との関係を保ったまま、方針の話し合いを終えた。");
            }
            else
            {
                GUILayout.Label("話し合いは終わったが、どちらかとの関係が損なわれたままになっている。");
            }

            GUILayout.Space(8f);
            GUILayout.Label(string.Format("暫定スコア: {0:0.#} 点　／　所要 {1:0} 秒", _score, ElapsedSeconds));
            GUILayout.Space(10f);

            GUILayout.Label("隠れパラメータの最終値（対話中は非表示）:");
            GUILayout.Label(string.Format(
                "　患者　 信頼 {0} ／ 不安 {1} ／ 理解 {2}",
                _patientTrust, _patientAnxiety, _patientComprehension));
            GUILayout.Label(string.Format(
                "　家族　 信頼 {0} ／ 不安 {1}", _familyTrust, _familyAnxiety));
            GUILayout.Label(
                "　対話中に数値が見えていると、相手の反応ではなく数値を見て操作する練習になる。" +
                "実際の面談では手がかりは表情と言葉しかないため、ここで初めて開示している。");

            GUILayout.Space(10f);
            GUILayout.Label(string.Format(
                "飛ばしたステップ: {0} 回　／　沈黙を遮った回数: {1} 回",
                _skippedSteps, _interruptedSilences));

            if (_trace.Count > 0)
            {
                GUILayout.Space(10f);
                GUILayout.Label("パラメータの推移:");
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
                "緩和ケア領域の実務者によるレビューを必ず受けてください。");
        }
    }
}
