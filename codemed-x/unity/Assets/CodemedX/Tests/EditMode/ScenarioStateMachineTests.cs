using CodemedX.Scenarios;
using NUnit.Framework;

namespace CodemedX.Tests
{
    /// <summary>
    /// 遷移表に無い進行（面談を飛ばして判定へ行く等）が通ってしまうと、
    /// 学習履歴上は「手順を踏んだ」ことになってしまうため、拒否を明示的にテストする。
    /// </summary>
    public class ScenarioStateMachineTests
    {
        private enum WelfareState
        {
            Preparation,
            Observation,
            Interview,
            Assessment,
            Escalation,
            Completed,
            Aborted
        }

        private float _now;

        private ScenarioStateMachine<WelfareState> CreateMachine()
        {
            _now = 0f;
            ScenarioStateMachine<WelfareState> machine =
                new ScenarioStateMachine<WelfareState>(WelfareState.Preparation, () => _now);

            machine.Allow(WelfareState.Preparation, WelfareState.Observation)
                   .Allow(WelfareState.Observation, WelfareState.Interview)
                   .Allow(WelfareState.Interview, WelfareState.Assessment)
                   .Allow(WelfareState.Assessment, WelfareState.Escalation)
                   .Allow(WelfareState.Escalation, WelfareState.Completed)
                   .AllowFromAny(WelfareState.Aborted);

            return machine;
        }

        [Test]
        public void Allowed_transition_succeeds()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();

            Assert.IsTrue(machine.TryTransitionTo(WelfareState.Observation));
            Assert.IsTrue(machine.IsIn(WelfareState.Observation));
        }

        [Test]
        public void Skipping_a_phase_is_rejected_and_state_is_unchanged()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();

            Assert.IsFalse(machine.TryTransitionTo(WelfareState.Assessment));
            Assert.IsTrue(machine.IsIn(WelfareState.Preparation));
        }

        [Test]
        public void Abort_is_reachable_from_any_state()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();
            machine.TryTransitionTo(WelfareState.Observation);
            machine.TryTransitionTo(WelfareState.Interview);

            Assert.IsTrue(machine.TryTransitionTo(WelfareState.Aborted));
        }

        [Test]
        public void History_records_duration_of_each_phase()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();

            _now = 30f;
            machine.TryTransitionTo(WelfareState.Observation);
            _now = 95f;
            machine.TryTransitionTo(WelfareState.Interview);

            Assert.AreEqual(2, machine.History.Count);
            Assert.AreEqual(WelfareState.Preparation, machine.History[0].State);
            Assert.AreEqual(30f, machine.History[0].DurationSeconds, 0.001f);
            Assert.AreEqual(WelfareState.Observation, machine.History[1].State);
            Assert.AreEqual(65f, machine.History[1].DurationSeconds, 0.001f);
        }

        [Test]
        public void Time_in_current_state_uses_injected_clock()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();

            _now = 12f;

            Assert.AreEqual(12f, machine.TimeInCurrentStateSeconds, 0.001f);
        }

        [Test]
        public void State_changed_event_reports_previous_and_next()
        {
            ScenarioStateMachine<WelfareState> machine = CreateMachine();
            WelfareState from = WelfareState.Completed;
            WelfareState to = WelfareState.Completed;

            machine.StateChanged += (previous, current) =>
            {
                from = previous;
                to = current;
            };

            machine.TryTransitionTo(WelfareState.Observation);

            Assert.AreEqual(WelfareState.Preparation, from);
            Assert.AreEqual(WelfareState.Observation, to);
        }
    }
}
