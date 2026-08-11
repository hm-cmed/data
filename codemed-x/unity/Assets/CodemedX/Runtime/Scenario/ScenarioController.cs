using System;
using CodemedX.Assessment;
using CodemedX.Core;
using CodemedX.Logging;
using UnityEngine;

namespace CodemedX.Scenarios
{
    /// <summary>
    /// 全シナリオの共通基底。①〜⑤ のシナリオ Manager はこれを継承し、
    /// 状態 enum と遷移表、局面ごとの処理だけを書けばよい。
    ///
    /// 基底側が引き受けること:
    /// - セッション開始と ScenarioStarted / StateChanged / ScenarioCompleted の送信
    /// - 制限時間の監視とタイムアウト処理
    /// - スコアカードへの集約
    /// </summary>
    public abstract class ScenarioController<TState> : MonoBehaviour where TState : struct, Enum
    {
        [SerializeField] private ScenarioDefinition definition;

        [SerializeField, Tooltip("Start() で自動的にセッションを開始する。ロビー UI から開始する場合は外す。")]
        private bool beginOnStart = true;

        private float _startedAtSeconds;
        private bool _isFinished;

        public ScenarioDefinition Definition { get { return definition; } }

        public bool IsRunning { get; private set; }

        public float ElapsedSeconds
        {
            get { return IsRunning || _isFinished ? Time.unscaledTime - _startedAtSeconds : 0f; }
        }

        public float RemainingSeconds
        {
            get
            {
                if (definition == null || !definition.HasTimeLimit)
                {
                    return float.PositiveInfinity;
                }

                return Mathf.Max(0f, definition.TimeLimitSeconds - ElapsedSeconds);
            }
        }

        protected ScenarioStateMachine<TState> Machine { get; private set; }

        protected ScoreCard Score { get; private set; }

        /// <summary>シナリオ開始時の状態。</summary>
        protected abstract TState InitialState { get; }

        /// <summary>許可する状態遷移を宣言する。ここに書かれていない遷移は実行時に拒否される。</summary>
        protected abstract void ConfigureTransitions(ScenarioStateMachine<TState> machine);

        protected virtual void OnScenarioStarted() { }

        protected virtual void OnStateEntered(TState previous, TState current) { }

        protected virtual void OnScenarioFinished(bool success) { }

        protected virtual void Awake()
        {
            if (definition == null)
            {
                Debug.LogError("[Codemed-x] ScenarioDefinition が未設定です。", this);
                enabled = false;
                return;
            }

            Machine = new ScenarioStateMachine<TState>(InitialState);
            ConfigureTransitions(Machine);
            Machine.StateChanged += HandleStateChanged;

            Score = new ScoreCard(definition.ScenarioId, definition.ObjectiveCatalog);
        }

        protected virtual void Start()
        {
            if (beginOnStart)
            {
                BeginScenario();
            }
        }

        protected virtual void OnDestroy()
        {
            if (Machine != null)
            {
                Machine.StateChanged -= HandleStateChanged;
            }
        }

        protected virtual void Update()
        {
            if (!IsRunning || definition == null || !definition.HasTimeLimit)
            {
                return;
            }

            if (ElapsedSeconds >= definition.TimeLimitSeconds)
            {
                LogEvent(EventTypes.ScenarioFailed, string.Empty, 0f, ErrorTypes.Timeout);
                FinishScenario(false);
            }
        }

        public void BeginScenario()
        {
            if (IsRunning || definition == null)
            {
                return;
            }

            SessionContext session = new SessionContext(
                LearnerIdentity.AnonymousUserId,
                definition.ScenarioId,
                definition.DefaultLocomotionMode);

            EventLogger.Instance.BeginSession(session);

            _startedAtSeconds = Time.unscaledTime;
            IsRunning = true;
            _isFinished = false;

            LogEvent(
                EventTypes.ScenarioStarted,
                string.Empty,
                0f,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("state", Machine.Current.ToString())
                    .Add("time_limit_sec", definition.TimeLimitSeconds)
                    .Build());

            OnScenarioStarted();
        }

        /// <summary>遷移表に従って状態を進める。拒否されたら false（実装バグの早期発見用に警告も出す）。</summary>
        public bool TryTransitionTo(TState destination)
        {
            if (Machine == null)
            {
                return false;
            }

            if (!Machine.TryTransitionTo(destination))
            {
                Debug.LogWarning(string.Format(
                    "[Codemed-x] 許可されていない状態遷移です: {0} -> {1}", Machine.Current, destination), this);
                return false;
            }

            return true;
        }

        /// <summary>イベントを送信し、同時にスコアカードへ集約する。シナリオ側はこれだけを使う。</summary>
        protected TrainingEvent LogEvent(
            string eventType,
            string objectiveId = "",
            float score = 0f,
            string errorType = ErrorTypes.None,
            string payloadJson = "",
            bool objectiveAchieved = false)
        {
            if (Score != null)
            {
                Score.Record(objectiveId, score, errorType, objectiveAchieved);
            }

            return EventLogger.Instance.Log(eventType, objectiveId, score, errorType, payloadJson);
        }

        /// <summary>シナリオを終了し、集計を送信する。二重呼び出しは無視する。</summary>
        public void FinishScenario(bool success)
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;
            IsRunning = false;

            EventLogger.Instance.Log(
                success ? EventTypes.ScenarioCompleted : EventTypes.ScenarioFailed,
                string.Empty,
                Score != null ? Score.TotalScore : 0f,
                ErrorTypes.None,
                Score != null ? Score.ToPayloadJson(ElapsedSeconds) : string.Empty);

            OnScenarioFinished(success);
        }

        private void HandleStateChanged(TState previous, TState current)
        {
            LogEvent(
                EventTypes.StateChanged,
                string.Empty,
                0f,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("from", previous.ToString())
                    .Add("to", current.ToString())
                    .Add("elapsed_sec", ElapsedSeconds)
                    .Build());

            OnStateEntered(previous, current);
        }
    }
}
