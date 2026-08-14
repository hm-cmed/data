using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Pharmacist
{
    /// <summary>
    /// ④ 服薬指導から疑義照会までの多者間連鎖シミュレータ。
    ///
    /// 使い方: 空の GameObject にこのスクリプトを 1 つ付けて Play を押すだけ。
    ///
    /// 学習の主眼:
    ///   患者の訴え → 検査値 → 疑義の同定 → 権威勾配下での照会
    ///   という連鎖を最後まで切らさずに繋げること。
    ///   どこか 1 つでも欠けると、患者に薬が渡る。
    ///
    /// 疑義照会義務は薬剤師法第24条（第21条は調剤応需義務、第28条は調剤録）。
    /// </summary>
    public class PharmacistInquirySim : MonoBehaviour
    {
        private enum Phase
        {
            Counseling,
            LabReview,
            DoctorCall,
            Debrief
        }

        private const string ScenarioId = "pharmacist_inquiry_v1";
        private const int InitialPressure = 40;

        [Header("シナリオの内容")]
        [SerializeField, Tooltip("空のままだと、既定の題材（監修前の仮版）が使われる。")]
        private PharmacistScenarioData scenario = new PharmacistScenarioData();

        [Header("学習者")]
        [SerializeField] private string learnerId = string.Empty;

        [Header("表示")]
        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントを割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        [Header("開発")]
        [SerializeField] private bool echoToConsole = true;

        private PharmacistTrainingLog _log;
        private Phase _phase = Phase.Counseling;
        private float _startedAt;

        private readonly HashSet<string> _attachedEvidence = new HashSet<string>();
        private readonly List<string> _debriefNotes = new List<string>();
        private readonly List<string> _pressureTrace = new List<string>();

        private string _patientReply = string.Empty;
        private bool _symptomCaptured;
        private bool _counselingDone;

        private int _doctorPressure = InitialPressure;
        private int _turnIndex;
        private string _doctorReply = string.Empty;
        private float _turnEnteredAt;
        private bool _timedOutThisTurn;

        private bool _failed;
        private string _failureReason = string.Empty;
        private bool _prescriptionChanged;
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
                scenario = PharmacistScenarioData.CreateDefault();
            }

            _log = new PharmacistTrainingLog(ScenarioId, learnerId);
            _log.EchoToConsole = echoToConsole;
        }

        private void Start()
        {
            _startedAt = Time.unscaledTime;
            _log.Log(EventKind.ScenarioStarted, string.Empty, 0f, ErrorKind.None,
                Payload.New().Add("phase", _phase.ToString()).Build());
        }

        private float ElapsedSeconds { get { return Time.unscaledTime - _startedAt; } }

        private float RemainingResponseSeconds
        {
            get { return Mathf.Max(0f, scenario.ResponseSeconds - (Time.unscaledTime - _turnEnteredAt)); }
        }

        private void Update()
        {
            // 権威勾配の圧力は「即答を迫られること」で生じる。時間の要素は外せない。
            if (_phase != Phase.DoctorCall || _failed || _timedOutThisTurn)
            {
                return;
            }

            if (RemainingResponseSeconds > 0f)
            {
                return;
            }

            _timedOutThisTurn = true;
            ApplyPressure(15, "言い淀み（応答なし）");
            _score -= 3f;
            _doctorReply = "（医師）……もしもし？　聞いてる？";
            _debriefNotes.Add(
                "電話で応答が止まった。沈黙は「根拠が無い」という合図として受け取られ、" +
                "相手の圧力を強める。準備した根拠を先に手元に置いておく必要がある。");

            AdvanceTurnOrFinish();
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

            GUILayout.Label("服薬指導・疑義照会シミュレーション");
            GUILayout.Label(string.Format("局面: {0}　／　経過 {1:0} 秒", PhaseLabel(_phase), ElapsedSeconds));
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_phase)
            {
                case Phase.Counseling: DrawCounseling(); break;
                case Phase.LabReview: DrawLabReview(); break;
                case Phase.DoctorCall: DrawDoctorCall(); break;
                default: DrawDebrief(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string PhaseLabel(Phase phase)
        {
            switch (phase)
            {
                case Phase.Counseling: return "1. 服薬指導";
                case Phase.LabReview: return "2. 検査値の確認";
                case Phase.DoctorCall: return "3. 医師への疑義照会";
                default: return "4. 振り返り";
            }
        }

        // ------------------------------------------------------------------ 1. 服薬指導

        private void DrawCounseling()
        {
            GUILayout.Label(scenario.Prescription);
            GUILayout.Space(10f);
            GUILayout.Label(scenario.PatientOpening);

            if (!string.IsNullOrEmpty(_patientReply))
            {
                GUILayout.Space(6f);
                GUILayout.Label(_patientReply);
            }

            GUILayout.Space(10f);

            if (!_counselingDone)
            {
                GUILayout.Label("どう応じるか:");
                IReadOnlyList<CounselingOption> options = scenario.Counseling;
                for (int i = 0; i < options.Count; i++)
                {
                    if (GUILayout.Button(options[i].Text))
                    {
                        SelectCounseling(options[i]);
                        return;
                    }
                }
            }
            else
            {
                if (GUILayout.Button("電子カルテで検査値を確認する"))
                {
                    GoTo(Phase.LabReview);
                }

                if (GUILayout.Button("問題ないので調剤する"))
                {
                    Fail("InquirySkipped", ErrorKind.InquirySkipped,
                        "検査値を確認せずに調剤へ進んだ。患者が浮腫と尿量減少を訴えている時点で、" +
                        "腎機能を確認する理由があった。");
                }
            }
        }

        private void SelectCounseling(CounselingOption option)
        {
            _patientReply = option.Reply;
            _counselingDone = true;
            _symptomCaptured = option.CapturesSymptom;

            float gained = option.CapturesSymptom ? 6f : -3f;
            _score += gained;
            _debriefNotes.Add("服薬指導での応答: " + option.Text + "\n　" + option.DebriefNote);

            _log.Log(EventKind.DialogueSelected, option.ObjectiveId, gained, ErrorKind.None,
                Payload.New().Add("symptom_captured", option.CapturesSymptom).Build());
        }

        // ------------------------------------------------------------------ 2. 検査値

        private void DrawLabReview()
        {
            GUILayout.Label("直近の血液検査。疑義の根拠になるものを「証拠として添付」する。");
            GUILayout.Space(8f);

            IReadOnlyList<LabValue> values = scenario.LabValues;
            for (int i = 0; i < values.Count; i++)
            {
                LabValue lab = values[i];
                bool attached = _attachedEvidence.Contains(lab.Id);

                GUILayout.Label(string.Format(
                    "{0}{1}　{2}　（{3}）",
                    attached ? "✓ " : "　", lab.Label, lab.Value, lab.Reference));

                if (!attached && GUILayout.Button("　証拠として添付: " + lab.Label))
                {
                    AttachEvidence(lab);
                }

                GUILayout.Space(4f);
            }

            GUILayout.Space(10f);
            GUILayout.Label(string.Format("添付済みの証拠: {0} 件", _attachedEvidence.Count));
            GUILayout.Space(6f);

            if (GUILayout.Button("医師に電話する"))
            {
                GoTo(Phase.DoctorCall);
                _turnEnteredAt = Time.unscaledTime;
                _timedOutThisTurn = false;
            }

            if (GUILayout.Button("問題ないので調剤する"))
            {
                Fail("InquirySkipped", ErrorKind.InquirySkipped,
                    "検査値を見たうえで調剤へ進んだ。薬剤師法第24条は、疑わしい点を" +
                    "確かめた後でなければ調剤してはならないと定めている。");
            }
        }

        private void AttachEvidence(LabValue lab)
        {
            _attachedEvidence.Add(lab.Id);

            float gained = lab.IsRelevant ? 5f : -4f;
            _score += gained;

            if (!lab.IsRelevant)
            {
                _debriefNotes.Add(
                    "根拠にならない検査値を添付した: " + lab.Label + "\n　" + lab.DebriefNote);
            }

            _log.Log(EventKind.LabConfirmed, "PHM-LAB-01", gained,
                lab.IsRelevant ? ErrorKind.None : ErrorKind.EvidenceNotPresented,
                Payload.New()
                    .Add("marker", lab.Id)
                    .Add("value", lab.Value)
                    .Add("relevant", lab.IsRelevant)
                    .Add("attached_as_evidence", true)
                    .Build());

            if (lab.IsRelevant)
            {
                _log.Log(EventKind.EvidenceAttached, "PHM-ADR-01", 0f, ErrorKind.None,
                    Payload.New().Add("marker", lab.Id).Build());
            }
        }

        private bool HasRelevantEvidence
        {
            get
            {
                IReadOnlyList<LabValue> values = scenario.LabValues;
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i].IsRelevant && _attachedEvidence.Contains(values[i].Id))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // ------------------------------------------------------------------ 3. 疑義照会

        private void DrawDoctorCall()
        {
            if (_turnIndex >= scenario.Inquiry.Count)
            {
                Finish(true);
                return;
            }

            InquiryTurn turn = scenario.Inquiry[_turnIndex];

            GUILayout.Label(turn.DoctorLine);

            if (!string.IsNullOrEmpty(_doctorReply))
            {
                GUILayout.Space(6f);
                GUILayout.Label(_doctorReply);
            }

            GUILayout.Space(8f);

            // 残り秒数は出すが、Pressure の値は出さない。
            GUILayout.Label(string.Format("（相手は急いでいる。残り {0:0.0} 秒）", RemainingResponseSeconds));
            GUILayout.Space(6f);

            IReadOnlyList<InquiryOption> options = turn.Options;
            for (int i = 0; i < options.Count; i++)
            {
                if (GUILayout.Button(options[i].Text))
                {
                    SelectInquiry(options[i]);
                    return;
                }
            }
        }

        private void SelectInquiry(InquiryOption option)
        {
            _doctorReply = option.Reply;

            if (option.YieldsToAuthority)
            {
                _score -= 12f;
                _debriefNotes.Add("引き下がった選択: " + option.Text + "\n　" + option.DebriefNote);

                _log.Log(EventKind.DialogueSelected, option.ObjectiveId, -12f, ErrorKind.YieldedToAuthority,
                    Payload.New()
                        .Add("doctor_pressure", _doctorPressure)
                        .Add("had_evidence", HasRelevantEvidence)
                        .Build());

                Fail("YieldedToAuthority", ErrorKind.YieldedToAuthority,
                    "根拠を示せる状況で主張を取り下げた。" +
                    "チーム STEPPS の 2 チャレンジルールでは、懸念は最低 2 回主張することが求められる。");
                return;
            }

            // 根拠を要する主張を、証拠を添付せずに行った場合。
            bool unsupported = option.RequiresEvidence && !HasRelevantEvidence;
            string errorKind = unsupported ? ErrorKind.EvidenceNotPresented : ErrorKind.None;

            int delta = unsupported ? 15 : option.PressureDelta;
            ApplyPressure(delta, option.Text);

            float gained = unsupported ? -5f : (option.PressureDelta < 0 ? 6f : -4f);
            _score += gained;

            if (unsupported)
            {
                _debriefNotes.Add(
                    "検査値を添付せずに主張した。数値を手元に置かないまま話すと、" +
                    "相手には印象論として届く。照会の前に根拠を揃えておく必要がある。");
            }
            else if (!string.IsNullOrEmpty(option.DebriefNote))
            {
                _debriefNotes.Add("電話での応答: " + option.Text + "\n　" + option.DebriefNote);
            }

            _log.Log(EventKind.DialogueSelected, option.ObjectiveId, gained, errorKind,
                Payload.New()
                    .Add("turn", _turnIndex)
                    .Add("pressure_delta", delta)
                    .Add("doctor_pressure", _doctorPressure)
                    .Add("evidence_count", _attachedEvidence.Count)
                    .Build());

            AdvanceTurnOrFinish();
        }

        private void ApplyPressure(int delta, string cause)
        {
            _doctorPressure = Mathf.Clamp(_doctorPressure + delta, 0, 100);
            _pressureTrace.Add(string.Format("{0:+#;-#;0} → {1}　（{2}）",
                delta, _doctorPressure, cause.Length > 24 ? cause.Substring(0, 24) + "…" : cause));
        }

        private void AdvanceTurnOrFinish()
        {
            if (_doctorPressure >= 80)
            {
                _doctorReply = "（医師）忙しいから、そのまま出しておいて。（電話が切れる）";
                Fail("FailedInquiry", ErrorKind.EvidenceNotPresented,
                    "医師の圧力が限界に達し、照会が打ち切られた。" +
                    "曖昧な言い方や沈黙が重なると、根拠があっても取り合われなくなる。");
                return;
            }

            _turnIndex++;
            _turnEnteredAt = Time.unscaledTime;
            _timedOutThisTurn = false;

            if (_turnIndex >= scenario.Inquiry.Count)
            {
                _prescriptionChanged = _doctorPressure <= 30;
                Finish(_prescriptionChanged);
            }
        }

        // ------------------------------------------------------------------ 終了

        private void Fail(string reason, string errorKind, string note)
        {
            _failed = true;
            _failureReason = reason;
            _debriefNotes.Add(note);

            _log.Log(EventKind.ScenarioFailed, "PHM-INQ-01", _score, errorKind,
                Payload.New()
                    .Add("reason", reason)
                    .Add("authority_pressure", _doctorPressure)
                    .Add("evidence_count", _attachedEvidence.Count)
                    .Add("symptom_captured", _symptomCaptured)
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            _phase = Phase.Debrief;
            _scroll = Vector2.zero;
        }

        private void Finish(bool success)
        {
            if (_phase == Phase.Debrief)
            {
                return;
            }

            _prescriptionChanged = success;

            _log.Log(EventKind.EscalationPerformed, "PHM-SBR-01", success ? 10f : 0f, ErrorKind.None,
                Payload.New()
                    .Add("authority_pressure", _doctorPressure)
                    .Add("prescription_changed", success)
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());

            if (success)
            {
                _score += 10f;
            }

            _log.Log(success ? EventKind.ScenarioCompleted : EventKind.ScenarioFailed,
                string.Empty, _score, ErrorKind.None,
                Payload.New()
                    .Add("prescription_changed", success)
                    .Add("authority_pressure", _doctorPressure)
                    .Add("evidence_count", _attachedEvidence.Count)
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            _phase = Phase.Debrief;
            _scroll = Vector2.zero;
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

        // ------------------------------------------------------------------ 振り返り

        private void DrawDebrief()
        {
            if (_prescriptionChanged)
            {
                GUILayout.Label("処方変更の合意が得られた。ロキソプロフェンは中止となった。");
            }
            else if (_failed)
            {
                GUILayout.Label("疑義照会は成立しなかった。処方はそのまま患者に渡ることになる。");
                GUILayout.Label("　終了理由: " + _failureReason);
            }
            else
            {
                GUILayout.Label("会話は終わったが、処方変更の合意には至らなかった。");
            }

            GUILayout.Space(8f);
            GUILayout.Label(string.Format("暫定スコア: {0:0.#} 点　／　所要 {1:0} 秒", _score, ElapsedSeconds));

            GUILayout.Space(10f);
            GUILayout.Label(string.Format(
                "医師の権威勾配プレッシャー（会話中は非表示）: {0} → {1}", InitialPressure, _doctorPressure));

            if (_pressureTrace.Count > 0)
            {
                for (int i = 0; i < _pressureTrace.Count; i++)
                {
                    GUILayout.Label("　" + _pressureTrace[i]);
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("証拠の扱い:");
            IReadOnlyList<LabValue> values = scenario.LabValues;
            for (int i = 0; i < values.Count; i++)
            {
                LabValue lab = values[i];
                bool attached = _attachedEvidence.Contains(lab.Id);
                if (lab.IsRelevant && !attached)
                {
                    GUILayout.Label("　使えたはずで使わなかった: " + lab.Label + " " + lab.Value);
                    GUILayout.Label("　　" + lab.DebriefNote);
                }
                else if (lab.IsRelevant && attached)
                {
                    GUILayout.Label("　使えた: " + lab.Label + " " + lab.Value);
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
                "薬剤師によるレビューを必ず受けてください。");
        }
    }
}
