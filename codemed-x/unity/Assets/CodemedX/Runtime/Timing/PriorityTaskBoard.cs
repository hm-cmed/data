using System;
using System.Collections.Generic;
using CodemedX.Core;
using CodemedX.Logging;
using UnityEngine;

namespace CodemedX.Timing
{
    /// <summary>
    /// 同時多発するタスクの 1 件。③ 夜勤シミュレータの「SpO2 低下」「抜管リスク」「電話」等。
    /// 緊急度（<see cref="Priority"/>）と放置許容時間を持ち、学習者の選択の当否を機械的に判定できるようにする。
    /// </summary>
    [DisallowMultipleComponent]
    public class PriorityTask : MonoBehaviour
    {
        [SerializeField, Tooltip("ログに残す識別子。例: patientA_spo2_drop")]
        private string taskId = "task";

        [SerializeField, Tooltip("数値が大きいほど緊急。生命に直結するものを高くする。")]
        private int priority = 1;

        [SerializeField, Tooltip("発生から何秒放置すると重大化するか。")]
        private float neglectThresholdSeconds = 15f;

        [SerializeField, Tooltip("schema/objectives.csv の objective_id。")]
        private string objectiveId = string.Empty;

        [SerializeField, Tooltip("放置して重大化したときの error_type。")]
        private string neglectErrorType = ErrorTypes.CriticalTaskNeglected;

        [SerializeField, Tooltip("解決できたときの加点。")]
        private float resolveScore = 5f;

        public string TaskId { get { return taskId; } }
        public int Priority { get { return priority; } }
        public float NeglectThresholdSeconds { get { return neglectThresholdSeconds; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string NeglectErrorType { get { return neglectErrorType; } }
        public float ResolveScore { get { return resolveScore; } }

        public bool IsActive { get; private set; }
        public bool IsResolved { get; private set; }
        public bool HasEscalated { get; private set; }
        public float ActivatedAtSeconds { get; private set; }

        /// <summary>タスクを発生させる（ScenarioClock のトリガーから呼ぶ）。</summary>
        public void Activate()
        {
            if (IsActive || IsResolved)
            {
                return;
            }

            IsActive = true;
            HasEscalated = false;
            ActivatedAtSeconds = Time.unscaledTime;
        }

        public void MarkResolved()
        {
            IsActive = false;
            IsResolved = true;
        }

        public void MarkEscalated()
        {
            HasEscalated = true;
        }

        public float GetUnattendedSeconds()
        {
            return IsActive ? Time.unscaledTime - ActivatedAtSeconds : 0f;
        }
    }

    /// <summary>
    /// 発生中のタスクを一覧で保持し、「いま学習者が着手しているタスク」との緊急度差を評価する。
    ///
    /// 判定するのは 2 つだけ:
    /// - 優先順位の逆転（より緊急なタスクがあるのに低緊急のタスクに着手した）
    /// - 放置による重大化（許容時間を超えて未対応）
    /// いずれも「時間圧下の臨床判断」を測る指標そのものなので、必ずログに残す。
    /// </summary>
    [DisallowMultipleComponent]
    public class PriorityTaskBoard : MonoBehaviour
    {
        [SerializeField] private List<PriorityTask> tasks = new List<PriorityTask>();

        [SerializeField, Tooltip("優先順位逆転とみなす緊急度の差。1 なら 1 段でも下を選んだら逆転扱い。")]
        private int inversionPriorityGap = 1;

        [SerializeField, Tooltip("優先順位逆転時の減点（負の値）。")]
        private float inversionScore = -4f;

        [SerializeField, Tooltip("放置による重大化時の減点（負の値）。")]
        private float neglectScore = -8f;

        private PriorityTask _attendedTask;

        /// <summary>(放置され重大化したタスク, 放置秒数)</summary>
        public event Action<PriorityTask, float> TaskEscalated;

        public IReadOnlyList<PriorityTask> Tasks { get { return tasks; } }

        public PriorityTask AttendedTask { get { return _attendedTask; } }

        public void Register(PriorityTask task)
        {
            if (task != null && !tasks.Contains(task))
            {
                tasks.Add(task);
            }
        }

        /// <summary>発生中のうち最も緊急なタスク。無ければ null。</summary>
        public PriorityTask GetMostUrgentActiveTask()
        {
            PriorityTask mostUrgent = null;
            for (int i = 0; i < tasks.Count; i++)
            {
                PriorityTask task = tasks[i];
                if (task == null || !task.IsActive)
                {
                    continue;
                }

                if (mostUrgent == null || task.Priority > mostUrgent.Priority)
                {
                    mostUrgent = task;
                }
            }

            return mostUrgent;
        }

        /// <summary>
        /// 学習者が着手したタスクを宣言する（トリガー領域への侵入、対象のグラブ等から呼ぶ）。
        /// より緊急なタスクを差し置いていた場合はここで優先順位逆転として記録する。
        /// </summary>
        public void AttendTo(PriorityTask task)
        {
            _attendedTask = task;
            if (task == null)
            {
                return;
            }

            PriorityTask mostUrgent = GetMostUrgentActiveTask();
            bool isInversion = mostUrgent != null
                && mostUrgent != task
                && mostUrgent.Priority - task.Priority >= inversionPriorityGap;

            EventLogger.Instance.Log(
                EventTypes.TriageAction,
                task.ObjectiveId,
                isInversion ? inversionScore : 0f,
                isInversion ? ErrorTypes.PriorityInversion : ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("chosen", task.TaskId)
                    .Add("chosen_priority", task.Priority)
                    .Add("deferred", isInversion ? mostUrgent.TaskId : string.Empty)
                    .Add("deferred_priority", isInversion ? mostUrgent.Priority : 0)
                    .Add("deferred_unattended_sec", isInversion ? mostUrgent.GetUnattendedSeconds() : 0f)
                    .Build());
        }

        /// <summary>タスクを解決済みにして加点する。</summary>
        public void Resolve(PriorityTask task)
        {
            if (task == null || task.IsResolved)
            {
                return;
            }

            float unattended = task.GetUnattendedSeconds();
            task.MarkResolved();

            if (_attendedTask == task)
            {
                _attendedTask = null;
            }

            EventLogger.Instance.Log(
                EventTypes.TriageAction,
                task.ObjectiveId,
                task.ResolveScore,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("resolved", task.TaskId)
                    .Add("time_to_action_sec", unattended)
                    .Build());
        }

        protected virtual void Update()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                PriorityTask task = tasks[i];
                if (task == null || !task.IsActive || task.HasEscalated)
                {
                    continue;
                }

                float unattended = task.GetUnattendedSeconds();
                if (unattended < task.NeglectThresholdSeconds || task == _attendedTask)
                {
                    continue;
                }

                task.MarkEscalated();

                EventLogger.Instance.Log(
                    EventTypes.TriageAction,
                    task.ObjectiveId,
                    neglectScore,
                    task.NeglectErrorType,
                    PayloadBuilder.Create()
                        .Add("escalated", task.TaskId)
                        .Add("priority", task.Priority)
                        .Add("unattended_sec", unattended)
                        .Build());

                if (TaskEscalated != null)
                {
                    TaskEscalated(task, unattended);
                }
            }
        }
    }
}
