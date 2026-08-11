using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Scenarios
{
    /// <summary>
    /// シナリオの局面遷移を管理する汎用ステートマシン。
    /// 遷移表を明示することで「面談を飛ばして判定へ進む」ような不正な進行をコード側で塞ぐ。
    /// MonoBehaviour ではないので EditMode テストからそのまま検証できる。
    /// </summary>
    public class ScenarioStateMachine<TState> where TState : struct, Enum
    {
        /// <summary>1 回の滞在記録。デブリーフィングで「どこで時間を使ったか」を示すのに使う。</summary>
        public struct StateVisit
        {
            public TState State;
            public float EnteredAtSeconds;
            public float DurationSeconds;
        }

        private readonly Dictionary<TState, HashSet<TState>> _allowed =
            new Dictionary<TState, HashSet<TState>>();

        private readonly HashSet<TState> _allowedFromAny = new HashSet<TState>();
        private readonly List<StateVisit> _history = new List<StateVisit>();
        private readonly Func<float> _timeProvider;

        private float _enteredAtSeconds;

        public ScenarioStateMachine(TState initialState, Func<float> timeProvider = null)
        {
            // 既定は unscaledTime。ポーズ中（timeScale = 0）でも滞在時間の計測を止めないため。
            _timeProvider = timeProvider ?? (() => Time.unscaledTime);
            Current = initialState;
            _enteredAtSeconds = _timeProvider();
        }

        /// <summary>(直前の状態, 新しい状態)</summary>
        public event Action<TState, TState> StateChanged;

        public TState Current { get; private set; }

        public float TimeInCurrentStateSeconds { get { return _timeProvider() - _enteredAtSeconds; } }

        public IReadOnlyList<StateVisit> History { get { return _history; } }

        /// <summary><paramref name="from"/> から <paramref name="destinations"/> への遷移を許可する。</summary>
        public ScenarioStateMachine<TState> Allow(TState from, params TState[] destinations)
        {
            HashSet<TState> set;
            if (!_allowed.TryGetValue(from, out set))
            {
                set = new HashSet<TState>();
                _allowed[from] = set;
            }

            for (int i = 0; i < destinations.Length; i++)
            {
                set.Add(destinations[i]);
            }

            return this;
        }

        /// <summary>どの状態からでも入れる状態（中断・タイムアウト等）を登録する。</summary>
        public ScenarioStateMachine<TState> AllowFromAny(params TState[] destinations)
        {
            for (int i = 0; i < destinations.Length; i++)
            {
                _allowedFromAny.Add(destinations[i]);
            }

            return this;
        }

        public bool CanTransitionTo(TState destination)
        {
            if (_allowedFromAny.Contains(destination))
            {
                return true;
            }

            HashSet<TState> set;
            return _allowed.TryGetValue(Current, out set) && set.Contains(destination);
        }

        /// <summary>遷移できたら true。できなければ状態を変えずに false を返す。</summary>
        public bool TryTransitionTo(TState destination)
        {
            if (!CanTransitionTo(destination))
            {
                return false;
            }

            ForceTransitionTo(destination);
            return true;
        }

        /// <summary>遷移表を無視して遷移する。デバッグ・シナリオ強制終了専用。</summary>
        public void ForceTransitionTo(TState destination)
        {
            float now = _timeProvider();
            TState previous = Current;

            _history.Add(new StateVisit
            {
                State = previous,
                EnteredAtSeconds = _enteredAtSeconds,
                DurationSeconds = now - _enteredAtSeconds
            });

            Current = destination;
            _enteredAtSeconds = now;

            if (StateChanged != null)
            {
                StateChanged(previous, destination);
            }
        }

        public bool IsIn(TState state)
        {
            return EqualityComparer<TState>.Default.Equals(Current, state);
        }
    }
}
