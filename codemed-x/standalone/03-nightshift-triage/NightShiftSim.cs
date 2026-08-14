using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.NightShift
{
    /// <summary>
    /// ③ 夜勤・複数患者の優先順位判断シミュレータ。
    ///
    /// 使い方: 空の GameObject にこのスクリプトを 1 つ付けて Play を押すだけ。
    ///
    /// 5 テーマのうち、このシナリオだけが実時間で進む。
    /// 学習の主眼は「何を先にやるか」と「いつ人を呼ぶか」の 2 点で、
    /// 個々の手技の巧拙は測っていない。
    /// </summary>
    public class NightShiftSim : MonoBehaviour
    {
        private enum Phase
        {
            Briefing,
            Shift,
            Debrief
        }

        /// <summary>タスク 1 件の実行時状態。定義（<see cref="ShiftTask"/>）とは分けて持つ。</summary>
        private class TaskState
        {
            public ShiftTask Definition;
            public bool IsActive;
            public bool IsResolved;
            public bool HasEscalated;
            public float ActivatedAt;
            public float ResolvedAt;
            public float AttendedAt = -1f;

            public float UnattendedSeconds(float now)
            {
                return IsActive ? now - ActivatedAt : 0f;
            }
        }

        private const string ScenarioId = "nightshift_multi_v1";

        [Header("シナリオの内容")]
        [SerializeField, Tooltip("空のままだと、既定の題材（監修前の仮版）が使われる。")]
        private NightShiftScenarioData scenario = new NightShiftScenarioData();

        [Header("学習者")]
        [SerializeField] private string learnerId = string.Empty;

        [Header("表示")]
        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントを割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        [Header("開発")]
        [SerializeField] private bool echoToConsole = true;

        private NightShiftTrainingLog _log;
        private Phase _phase = Phase.Briefing;
        private float _startedAt;

        private readonly List<TaskState> _tasks = new List<TaskState>();
        private readonly List<string> _debriefNotes = new List<string>();
        private readonly List<string> _timeline = new List<string>();

        private TaskState _current;
        private float _currentFinishesAt;

        private bool _showSbar;
        private bool[] _sbarChecks;
        private bool _escalated;
        private float _firstDeteriorationAt = -1f;
        private float _escalatedAt = -1f;

        private int _inversionCount;
        private int _neglectCount;
        private float _score;
        private string _savedLogPath = string.Empty;
        private Vector2 _scroll;

        private void Awake()
        {
            if (scenario == null || scenario.IsEmpty)
            {
                scenario = NightShiftScenarioData.CreateDefault();
            }

            for (int i = 0; i < scenario.Tasks.Count; i++)
            {
                _tasks.Add(new TaskState { Definition = scenario.Tasks[i] });
            }

            _sbarChecks = new bool[scenario.SbarElements.Count];

            _log = new NightShiftTrainingLog(ScenarioId, learnerId);
            _log.EchoToConsole = echoToConsole;
        }

        private float ElapsedSeconds { get { return Time.unscaledTime - _startedAt; } }

        private float RemainingSeconds
        {
            get { return Mathf.Max(0f, scenario.TimeLimitSeconds - ElapsedSeconds); }
        }

        private void Update()
        {
            if (_phase != Phase.Shift)
            {
                return;
            }

            float now = ElapsedSeconds;

            ActivateDueTasks(now);
            FinishCurrentTask(now);
            CheckNeglect(now);

            if (now >= scenario.TimeLimitSeconds)
            {
                Finish();
            }
        }

        private void ActivateDueTasks(float now)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskState task = _tasks[i];
                if (task.IsActive || task.IsResolved || now < task.Definition.StartsAtSeconds)
                {
                    continue;
                }

                task.IsActive = true;
                task.ActivatedAt = now;
                _timeline.Add(string.Format("{0:0}秒　発生: {1}", now, task.Definition.Label));

                if (task.Definition.IsDeterioration && _firstDeteriorationAt < 0f)
                {
                    _firstDeteriorationAt = now;
                }
            }
        }

        private void FinishCurrentTask(float now)
        {
            if (_current == null || now < _currentFinishesAt)
            {
                return;
            }

            TaskState finished = _current;
            _current = null;

            finished.IsActive = false;
            finished.IsResolved = true;
            finished.ResolvedAt = now;

            float responseTime = finished.AttendedAt - finished.ActivatedAt;
            _score += 6f;
            _timeline.Add(string.Format("{0:0}秒　完了: {1}", now, finished.Definition.Label));

            _log.Log(EventKind.TriageAction, finished.Definition.ObjectiveId, 6f, ErrorKind.None,
                Payload.New()
                    .Add("resolved", finished.Definition.Id)
                    .Add("time_to_action_sec", responseTime)
                    .Build());
        }

        /// <summary>放置による重大化。1 タスクにつき一度だけ発火させる。</summary>
        private void CheckNeglect(float now)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskState task = _tasks[i];
                if (!task.IsActive || task.HasEscalated || task == _current)
                {
                    continue;
                }

                if (task.Definition.NeglectSeconds <= 0f)
                {
                    continue;
                }

                float unattended = task.UnattendedSeconds(now);
                if (unattended < task.Definition.NeglectSeconds)
                {
                    continue;
                }

                task.HasEscalated = true;
                _neglectCount++;
                _score -= 8f;

                _timeline.Add(string.Format("{0:0}秒　重大化: {1}", now, task.Definition.Label));
                _debriefNotes.Add(
                    "放置による重大化: " + task.Definition.Label + "\n　" +
                    task.Definition.NeglectOutcome + "\n　" + task.Definition.DebriefNote);

                _log.Log(EventKind.TriageAction, task.Definition.ObjectiveId, -8f,
                    ErrorKind.CriticalTaskNeglected,
                    Payload.New()
                        .Add("escalated", task.Definition.Id)
                        .Add("priority", task.Definition.Priority)
                        .Add("unattended_sec", unattended)
                        .Build());
            }
        }

        private void OnGUI()
        {
            if (uiFont != null)
            {
                GUI.skin.font = uiFont;
            }

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.toggle.fontSize = fontSize;
            GUI.skin.label.wordWrap = true;
            GUI.skin.button.wordWrap = true;
            GUI.skin.toggle.wordWrap = true;

            float width = Mathf.Min(Screen.width - 40f, 900f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 20f, width, Screen.height - 40f));

            GUILayout.Label("夜勤シミュレーション（複数患者・優先順位判断）");

            if (_phase == Phase.Shift)
            {
                GUILayout.Label(string.Format("残り {0:0} 秒", RemainingSeconds));
            }

            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_phase == Phase.Briefing)
            {
                DrawBriefing();
            }
            else if (_phase == Phase.Shift)
            {
                DrawShift();
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
            if (GUILayout.Button("勤務を始める"))
            {
                _startedAt = Time.unscaledTime;
                _phase = Phase.Shift;
                _log.Log(EventKind.ScenarioStarted, string.Empty, 0f, ErrorKind.None,
                    Payload.New().Add("time_limit_sec", scenario.TimeLimitSeconds).Build());
            }
        }

        private void DrawShift()
        {
            float now = ElapsedSeconds;

            if (_current != null)
            {
                GUILayout.Label(string.Format(
                    "対応中: {0}（残り {1:0.0} 秒）", _current.Definition.Label, _currentFinishesAt - now));
                GUILayout.Label("　" + _current.Definition.Detail);
                GUILayout.Space(8f);
            }

            GUILayout.Label("発生中のタスク:");

            bool anyActive = false;
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskState task = _tasks[i];
                if (!task.IsActive)
                {
                    continue;
                }

                anyActive = true;
                float unattended = task.UnattendedSeconds(now);
                string status = task.HasEscalated ? "【重大化】" : string.Format("経過 {0:0} 秒", unattended);

                GUILayout.Label(string.Format("　{0}　{1}", task.Definition.Label, status));
                GUILayout.Label("　　" + task.Definition.Detail);

                if (_current == null && GUILayout.Button("　対応する: " + task.Definition.Label))
                {
                    AttendTo(task, now);
                }

                GUILayout.Space(4f);
            }

            if (!anyActive && _current == null)
            {
                GUILayout.Label("　（今は落ち着いている）");
            }

            GUILayout.Space(10f);

            if (!_escalated)
            {
                if (!_showSbar)
                {
                    if (GUILayout.Button("医師に電話する"))
                    {
                        _showSbar = true;
                    }
                }
                else
                {
                    GUILayout.Label("何を伝えるか（複数選択）:");
                    for (int i = 0; i < scenario.SbarElements.Count; i++)
                    {
                        _sbarChecks[i] = GUILayout.Toggle(_sbarChecks[i], " " + scenario.SbarElements[i]);
                    }

                    if (GUILayout.Button("報告する"))
                    {
                        Escalate(now);
                    }

                    if (GUILayout.Button("やめる"))
                    {
                        _showSbar = false;
                    }
                }
            }
            else
            {
                GUILayout.Label("医師へ報告済み。まもなく到着する。");
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("勤務を終える（デバッグ用）"))
            {
                Finish();
            }
        }

        /// <summary>
        /// タスクに着手する。より緊急なものを差し置いていた場合は、その場で優先順位逆転として記録する。
        /// </summary>
        private void AttendTo(TaskState task, float now)
        {
            TaskState mostUrgent = GetMostUrgentActive();
            bool inversion = mostUrgent != null && mostUrgent != task
                && mostUrgent.Definition.Priority - task.Definition.Priority >= 1;

            task.AttendedAt = now;
            _current = task;
            _currentFinishesAt = now + task.Definition.WorkSeconds;

            _timeline.Add(string.Format("{0:0}秒　着手: {1}", now, task.Definition.Label));

            if (inversion)
            {
                _inversionCount++;
                _score -= 4f;
                _debriefNotes.Add(
                    "優先順位の逆転: 「" + task.Definition.Label + "」より「" +
                    mostUrgent.Definition.Label + "」のほうが緊急だった。\n　" +
                    mostUrgent.Definition.DebriefNote);
            }

            _log.Log(EventKind.TriageAction, task.Definition.ObjectiveId,
                inversion ? -4f : 0f,
                inversion ? ErrorKind.PriorityInversion : ErrorKind.None,
                Payload.New()
                    .Add("chosen", task.Definition.Id)
                    .Add("chosen_priority", task.Definition.Priority)
                    .Add("deferred", inversion ? mostUrgent.Definition.Id : string.Empty)
                    .Add("deferred_priority", inversion ? mostUrgent.Definition.Priority : 0)
                    .Add("deferred_unattended_sec", inversion ? mostUrgent.UnattendedSeconds(now) : 0f)
                    .Build());
        }

        private TaskState GetMostUrgentActive()
        {
            TaskState mostUrgent = null;
            for (int i = 0; i < _tasks.Count; i++)
            {
                TaskState task = _tasks[i];
                if (!task.IsActive)
                {
                    continue;
                }

                if (mostUrgent == null || task.Definition.Priority > mostUrgent.Definition.Priority)
                {
                    mostUrgent = task;
                }
            }

            return mostUrgent;
        }

        private void Escalate(float now)
        {
            _escalated = true;
            _escalatedAt = now;
            _showSbar = false;

            int requiredIncluded = 0;
            int noiseIncluded = 0;
            for (int i = 0; i < _sbarChecks.Length; i++)
            {
                if (!_sbarChecks[i])
                {
                    continue;
                }

                if (IsRequiredSbar(i))
                {
                    requiredIncluded++;
                }
                else
                {
                    noiseIncluded++;
                }
            }

            int requiredTotal = scenario.RequiredSbarIndices.Count;

            // このシナリオでもっとも重要な指標。急変の発生から報告までの秒数。
            float timeToDecision = _firstDeteriorationAt >= 0f ? now - _firstDeteriorationAt : -1f;

            float gained = requiredIncluded * 3f - noiseIncluded * 2f;
            if (timeToDecision >= 0f && timeToDecision <= 30f)
            {
                gained += 8f;
            }
            else if (timeToDecision > 60f)
            {
                gained -= 6f;
                _debriefNotes.Add(string.Format(
                    "急変の発生から報告まで {0:0} 秒かかった。\n　" +
                    "新人看護師の教育で問題になるのは知識ではなく「呼ぶのが遅れる」こと。" +
                    "抱え込まずに早く呼ぶこと自体が、測られている技能にあたる。", timeToDecision));
            }

            _score += gained;
            _timeline.Add(string.Format("{0:0}秒　医師へ報告", now));

            if (requiredIncluded < requiredTotal)
            {
                _debriefNotes.Add(
                    "報告に欠けている要素がある。SBAR は " +
                    "Situation（今起きていること）／ Background（背景）／ " +
                    "Assessment（自分の評価）／ Recommendation（依頼）の 4 つが揃って初めて、" +
                    "受け手が動ける情報になる。");
            }

            if (noiseIncluded > 0)
            {
                _debriefNotes.Add(
                    "報告に、患者の状態と関係のない内容が混ざっている。" +
                    "自分の困りごとは、患者の緊急度とは別に扱う必要がある。");
            }

            _log.Log(EventKind.EscalationPerformed, "NGT-ESC-01", gained,
                requiredIncluded < requiredTotal ? ErrorKind.DelayedEscalation : ErrorKind.None,
                Payload.New()
                    .Add("time_to_decision_sec", timeToDecision)
                    .Add("sbar_included", requiredIncluded)
                    .Add("sbar_total", requiredTotal)
                    .Add("sbar_complete", requiredIncluded >= requiredTotal)
                    .Build());
        }

        private bool IsRequiredSbar(int index)
        {
            IReadOnlyList<int> required = scenario.RequiredSbarIndices;
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        private void Finish()
        {
            if (_phase == Phase.Debrief)
            {
                return;
            }

            if (!_escalated)
            {
                _score -= 10f;
                _debriefNotes.Add(
                    "最後まで医師に連絡しなかった。夜勤の単独対応で問われるのは、" +
                    "一人でやり切ることではなく、抱え込まずに応援を呼べることにある。");

                _log.Log(EventKind.EscalationPerformed, "NGT-ESC-01", -10f, ErrorKind.DelayedEscalation,
                    Payload.New().Add("escalated", false).Build());
            }

            int unresolved = 0;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].IsActive && !_tasks[i].IsResolved)
                {
                    unresolved++;
                }
            }

            bool success = _neglectCount == 0 && _escalated;

            _log.Log(success ? EventKind.ScenarioCompleted : EventKind.ScenarioFailed,
                string.Empty, _score, success ? ErrorKind.None : ErrorKind.Timeout,
                Payload.New()
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Add("priority_inversions", _inversionCount)
                    .Add("neglected_tasks", _neglectCount)
                    .Add("unresolved_tasks", unresolved)
                    .Add("escalated", _escalated)
                    .Build());

            _savedLogPath = _log.SaveToFile();
            _phase = Phase.Debrief;
            _scroll = Vector2.zero;
        }

        private void DrawDebrief()
        {
            GUILayout.Label(string.Format("暫定スコア: {0:0.#} 点", _score));
            GUILayout.Space(6f);

            GUILayout.Label(string.Format("優先順位の逆転: {0} 回", _inversionCount));
            GUILayout.Label(string.Format("放置による重大化: {0} 件", _neglectCount));

            if (_escalated && _firstDeteriorationAt >= 0f)
            {
                GUILayout.Label(string.Format(
                    "急変の発生から医師への報告まで: {0:0} 秒", _escalatedAt - _firstDeteriorationAt));
                GUILayout.Label(
                    "　この数値がこのシナリオでもっとも重要な指標。" +
                    "対応の巧拙よりも、呼ぶまでの時間が患者の転帰を左右する。");
            }
            else if (!_escalated)
            {
                GUILayout.Label("医師への報告: なし");
            }

            GUILayout.Space(10f);
            GUILayout.Label("時系列:");
            for (int i = 0; i < _timeline.Count; i++)
            {
                GUILayout.Label("　" + _timeline[i]);
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
                "看護実務者によるレビューを必ず受けてください。");
        }
    }
}
