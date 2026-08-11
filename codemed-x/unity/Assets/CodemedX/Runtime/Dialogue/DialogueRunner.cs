using System;
using System.Collections.Generic;
using CodemedX.Affect;
using CodemedX.Core;
using CodemedX.Logging;
using UnityEngine;

namespace CodemedX.Dialogue
{
    /// <summary>
    /// 対話グラフを実行し、選択に応じて NPC の隠れパラメータを動かしてログを送る。
    /// UI（3D パネル / XR Ray での選択 / 音声認識）は本クラスのイベントを購読して実装する。
    ///
    /// ① 保護者面談、② ACP、④ 疑義照会、⑤ ゲートキーパーはすべてこのランナーを共有し、
    /// 差分は DialogueGraph アセットとしてデータで表現する。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueGraph graph;

        [SerializeField, Tooltip("この対話に登場する NPC。ActorId で対話グラフから参照される。")]
        private List<AffectActor> actors = new List<AffectActor>();

        [SerializeField, Tooltip("開始時に対話グラフの参照切れを検査して Console に警告を出す。")]
        private bool validateOnStart = true;

        private readonly Dictionary<string, AffectActor> _actorsById = new Dictionary<string, AffectActor>();
        private float _nodeEnteredAtSeconds;
        private bool _silenceResolved;

        /// <summary>新しいノードへ入った。UI は台詞と選択肢を描画する。</summary>
        public event Action<DialogueNode> NodeEntered;

        /// <summary>(選択されたノード, 選択肢, 条件を満たしていたか)</summary>
        public event Action<DialogueNode, DialogueChoice, bool> ChoiceSelected;

        /// <summary>対話が終端に達した。</summary>
        public event Action<DialogueNode> DialogueEnded;

        public DialogueNode CurrentNode { get; private set; }

        public bool IsRunning { get { return CurrentNode != null; } }

        /// <summary>沈黙ノードで、まだ待つべき時間が残っているか。</summary>
        public bool IsInSilenceWindow
        {
            get
            {
                return CurrentNode != null
                    && CurrentNode.SilenceSeconds > 0f
                    && Time.unscaledTime - _nodeEnteredAtSeconds < CurrentNode.SilenceSeconds;
            }
        }

        protected virtual void Awake()
        {
            RebuildActorIndex();
        }

        protected virtual void Start()
        {
            if (validateOnStart && graph != null)
            {
                List<string> broken = graph.FindBrokenReferences();
                if (broken.Count > 0)
                {
                    Debug.LogError(
                        "[Codemed-x] 対話グラフに参照切れがあります:\n" + string.Join("\n", broken.ToArray()), graph);
                }
            }
        }

        protected virtual void Update()
        {
            // 沈黙を最後まで待てたら、その時点で加点して記録する。
            if (_silenceResolved || CurrentNode == null || CurrentNode.SilenceSeconds <= 0f || IsInSilenceWindow)
            {
                return;
            }

            _silenceResolved = true;
            EventLogger.Instance.Log(
                EventTypes.SilenceRespected,
                CurrentNode.ObjectiveId,
                CurrentNode.SilenceRespectedScore,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("node", CurrentNode.Id)
                    .Add("waited_sec", CurrentNode.SilenceSeconds)
                    .Build());
        }

        public void SetGraph(DialogueGraph dialogueGraph)
        {
            graph = dialogueGraph;
        }

        public void RegisterActor(AffectActor actor)
        {
            if (actor != null && !string.IsNullOrEmpty(actor.ActorId))
            {
                _actorsById[actor.ActorId] = actor;
            }
        }

        public AffectActor GetActor(string actorId)
        {
            AffectActor actor;
            return _actorsById.TryGetValue(actorId ?? string.Empty, out actor) ? actor : null;
        }

        public void Begin()
        {
            if (graph == null)
            {
                Debug.LogError("[Codemed-x] DialogueGraph が未設定です。", this);
                return;
            }

            EnterNode(graph.GetEntryNode());
        }

        /// <summary>選択肢が現在の隠れパラメータ条件を満たしているか。UI のグレーアウト判定に使う。</summary>
        public bool IsChoiceAvailable(DialogueChoice choice)
        {
            IReadOnlyList<AffectCondition> requirements = choice.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                AffectCondition condition = requirements[i];
                if (!condition.IsSatisfiedBy(GetActor(condition.TargetActorId)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>UI に出すべき選択肢（hideWhenUnavailable が立っていて条件未達のものを除く）。</summary>
        public List<DialogueChoice> GetVisibleChoices()
        {
            List<DialogueChoice> visible = new List<DialogueChoice>();
            if (CurrentNode == null)
            {
                return visible;
            }

            IReadOnlyList<DialogueChoice> choices = CurrentNode.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                if (!choices[i].HideWhenUnavailable || IsChoiceAvailable(choices[i]))
                {
                    visible.Add(choices[i]);
                }
            }

            return visible;
        }

        /// <summary>現在ノードの選択肢を index で選ぶ。</summary>
        public bool Select(int choiceIndex)
        {
            if (CurrentNode == null || choiceIndex < 0 || choiceIndex >= CurrentNode.Choices.Count)
            {
                return false;
            }

            return Select(CurrentNode.Choices[choiceIndex]);
        }

        public bool Select(DialogueChoice choice)
        {
            if (CurrentNode == null || choice == null)
            {
                return false;
            }

            DialogueNode node = CurrentNode;
            bool available = IsChoiceAvailable(choice);

            // 沈黙を遮ったかどうかは、選択肢の良し悪しとは独立に評価する。
            if (IsInSilenceWindow && !_silenceResolved)
            {
                _silenceResolved = true;
                EventLogger.Instance.Log(
                    EventTypes.DialogueSelected,
                    node.ObjectiveId,
                    node.SilenceInterruptedScore,
                    ErrorTypes.InterruptedSilence,
                    PayloadBuilder.Create()
                        .Add("node", node.Id)
                        .Add("waited_sec", Time.unscaledTime - _nodeEnteredAtSeconds)
                        .Add("required_sec", node.SilenceSeconds)
                        .Build());
            }

            ApplyEffects(choice);

            string errorType = available ? choice.ErrorTypeName : choice.PrematureErrorTypeName;
            EventLogger.Instance.Log(
                EventTypes.DialogueSelected,
                choice.ObjectiveId,
                available ? choice.Score : 0f,
                errorType,
                BuildSelectionPayload(node, choice, available));

            if (ChoiceSelected != null)
            {
                ChoiceSelected(node, choice, available);
            }

            string nextNodeId = !available && !string.IsNullOrEmpty(choice.PrematureNextNodeId)
                ? choice.PrematureNextNodeId
                : choice.NextNodeId;

            DialogueNode next;
            if (string.IsNullOrEmpty(nextNodeId) || graph == null || !graph.TryGetNode(nextNodeId, out next))
            {
                End(node);
                return true;
            }

            EnterNode(next);
            return true;
        }

        /// <summary>対話を途中で打ち切る（信頼度が下がりきって面談が強制終了する場合など）。</summary>
        public void Abort(string reason)
        {
            DialogueNode node = CurrentNode;
            if (node == null)
            {
                return;
            }

            EventLogger.Instance.Log(
                EventTypes.DialogueNodeEntered,
                node.ObjectiveId,
                0f,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("node", node.Id)
                    .Add("aborted", true)
                    .Add("reason", reason)
                    .Build());

            End(node);
        }

        private void EnterNode(DialogueNode node)
        {
            CurrentNode = node;
            _nodeEnteredAtSeconds = Time.unscaledTime;
            _silenceResolved = node == null || node.SilenceSeconds <= 0f;

            if (node == null)
            {
                return;
            }

            EventLogger.Instance.Log(
                EventTypes.DialogueNodeEntered,
                node.ObjectiveId,
                0f,
                ErrorTypes.None,
                PayloadBuilder.Create()
                    .Add("node", node.Id)
                    .Add("speaker", node.SpeakerActorId)
                    .Add("silence_sec", node.SilenceSeconds)
                    .Build());

            if (NodeEntered != null)
            {
                NodeEntered(node);
            }

            if (node.IsTerminal)
            {
                End(node);
            }
        }

        private void End(DialogueNode lastNode)
        {
            CurrentNode = null;
            if (DialogueEnded != null)
            {
                DialogueEnded(lastNode);
            }
        }

        private void ApplyEffects(DialogueChoice choice)
        {
            IReadOnlyList<AffectEffect> effects = choice.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                AffectEffect effect = effects[i];
                AffectActor actor = GetActor(effect.TargetActorId);
                if (actor == null)
                {
                    Debug.LogWarning(
                        "[Codemed-x] ActorId \"" + effect.TargetActorId + "\" の AffectActor が見つかりません。", this);
                    continue;
                }

                actor.Apply(effect.Parameter, effect.Delta);
            }
        }

        private string BuildSelectionPayload(DialogueNode node, DialogueChoice choice, bool available)
        {
            PayloadBuilder payload = PayloadBuilder.Create()
                .Add("node", node.Id)
                .Add("choice_index", IndexOfChoice(node, choice))
                .Add("condition_met", available);

            // 判定に効いた隠れパラメータを一緒に残さないと、後からログだけで再現できない。
            for (int i = 0; i < actors.Count; i++)
            {
                AffectActor actor = actors[i];
                if (actor != null)
                {
                    payload.Add(actor.ActorId + "_trust", actor.State.Trust);
                    payload.Add(actor.ActorId + "_anxiety", actor.State.Anxiety);
                }
            }

            return payload.Build();
        }

        // IReadOnlyList には IndexOf が無いので自前で引く。
        private static int IndexOfChoice(DialogueNode node, DialogueChoice choice)
        {
            IReadOnlyList<DialogueChoice> choices = node.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                if (ReferenceEquals(choices[i], choice))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RebuildActorIndex()
        {
            _actorsById.Clear();
            for (int i = 0; i < actors.Count; i++)
            {
                RegisterActor(actors[i]);
            }
        }
    }
}
