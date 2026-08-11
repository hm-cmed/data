using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CodemedX.Timing
{
    /// <summary>
    /// 指定した秒数にイベントを発火させるシナリオ用タイマー。
    /// ③ 夜勤シミュレータの「10 秒時点で SpO2 低下、30 秒時点で抜管アラート…」のような
    /// 台本を、C# ではなく Inspector 上のデータとして持たせるための共通部品。
    /// </summary>
    [DisallowMultipleComponent]
    public class ScenarioClock : MonoBehaviour
    {
        [Serializable]
        public sealed class ScheduledTrigger
        {
            [SerializeField, Tooltip("開始からの経過秒数。")]
            private float atSeconds;

            [SerializeField, Tooltip("ログに残す識別子。例: patientA_spo2_drop")]
            private string triggerId = "trigger";

            [SerializeField] private UnityEvent onFire = new UnityEvent();

            public float AtSeconds { get { return atSeconds; } }
            public string TriggerId { get { return triggerId; } }
            public UnityEvent OnFire { get { return onFire; } }
            public bool HasFired { get; set; }
        }

        [SerializeField, Tooltip("制限時間（秒）。0 なら無制限。")]
        private float durationSeconds = 180f;

        [SerializeField, Tooltip("Start() で自動的に開始する。")]
        private bool autoStart;

        [SerializeField, Tooltip("発火時刻の昇順で並べる必要はない。実行時に整列する。")]
        private List<ScheduledTrigger> triggers = new List<ScheduledTrigger>();

        private float _startedAtSeconds;

        /// <summary>スケジュール済みイベントが発火した。</summary>
        public event Action<ScheduledTrigger> TriggerFired;

        /// <summary>制限時間に到達した。</summary>
        public event Action TimeExpired;

        public bool IsRunning { get; private set; }

        public float ElapsedSeconds { get { return IsRunning ? Time.unscaledTime - _startedAtSeconds : 0f; } }

        public float RemainingSeconds
        {
            get
            {
                if (durationSeconds <= 0f)
                {
                    return float.PositiveInfinity;
                }

                return Mathf.Max(0f, durationSeconds - ElapsedSeconds);
            }
        }

        protected virtual void Start()
        {
            if (autoStart)
            {
                Begin();
            }
        }

        public void Begin()
        {
            _startedAtSeconds = Time.unscaledTime;
            IsRunning = true;

            for (int i = 0; i < triggers.Count; i++)
            {
                triggers[i].HasFired = false;
            }

            // 昇順に並べておくと、毎フレームの走査を先頭から打ち切れる。
            triggers.Sort((a, b) => a.AtSeconds.CompareTo(b.AtSeconds));
        }

        public void Stop()
        {
            IsRunning = false;
        }

        protected virtual void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            float elapsed = ElapsedSeconds;

            for (int i = 0; i < triggers.Count; i++)
            {
                ScheduledTrigger trigger = triggers[i];
                if (trigger.HasFired)
                {
                    continue;
                }

                if (elapsed < trigger.AtSeconds)
                {
                    break;
                }

                trigger.HasFired = true;
                trigger.OnFire.Invoke();

                if (TriggerFired != null)
                {
                    TriggerFired(trigger);
                }
            }

            if (durationSeconds > 0f && elapsed >= durationSeconds)
            {
                IsRunning = false;
                if (TimeExpired != null)
                {
                    TimeExpired();
                }
            }
        }
    }
}
