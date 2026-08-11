using System;
using System.Collections.Generic;
using CodemedX.Affect;
using CodemedX.Core;
using UnityEngine;

namespace CodemedX.Dialogue
{
    /// <summary>
    /// 学習者が選ぶ 1 つの発話。
    /// 「なぜその選択が良い／悪いのか」を objectiveId と errorType としてデータに持たせることで、
    /// 判定ロジックを C# に書かずに済ませる（シナリオ監修者が Inspector で調整できる）。
    /// </summary>
    [Serializable]
    public sealed class DialogueChoice
    {
        [SerializeField, TextArea(1, 4)] private string text = string.Empty;

        [SerializeField, Tooltip("遷移先ノードID。空ならこの対話を終了する。")]
        private string nextNodeId = string.Empty;

        [SerializeField] private float score;

        [SerializeField, Tooltip("schema/objectives.csv の objective_id。")]
        private string objectiveId = string.Empty;

        [SerializeField, Tooltip("ErrorTypes のカタログ値。良い選択なら none。")]
        private string errorType = ErrorTypes.None;

        [SerializeField, Tooltip("選択した時点でこの評価項目を『到達』とみなす。")]
        private bool marksObjectiveAchieved;

        [SerializeField] private List<AffectEffect> effects = new List<AffectEffect>();

        [SerializeField, Tooltip("すべて満たされていないと『時期尚早な選択』として扱う条件。")]
        private List<AffectCondition> requirements = new List<AffectCondition>();

        [SerializeField, Tooltip("条件を満たさない状態で選んだ場合の error_type。")]
        private string prematureErrorType = ErrorTypes.PrematureProbing;

        [SerializeField, Tooltip("条件を満たさない場合の遷移先。空なら nextNodeId をそのまま使う。")]
        private string prematureNextNodeId = string.Empty;

        [SerializeField, Tooltip("条件を満たさない選択肢を UI に出さない（=学習者に選ばせない）。")]
        private bool hideWhenUnavailable;

        public string Text { get { return text; } }
        public string NextNodeId { get { return nextNodeId; } }
        public float Score { get { return score; } }
        public string ObjectiveId { get { return objectiveId; } }
        public string ErrorTypeName { get { return errorType; } }
        public bool MarksObjectiveAchieved { get { return marksObjectiveAchieved; } }
        public IReadOnlyList<AffectEffect> Effects { get { return effects; } }
        public IReadOnlyList<AffectCondition> Requirements { get { return requirements; } }
        public string PrematureErrorTypeName { get { return prematureErrorType; } }
        public string PrematureNextNodeId { get { return prematureNextNodeId; } }
        public bool HideWhenUnavailable { get { return hideWhenUnavailable; } }
    }

    /// <summary>NPC の 1 発話と、それに対する学習者の選択肢。</summary>
    [Serializable]
    public sealed class DialogueNode
    {
        [SerializeField] private string id = string.Empty;

        [SerializeField, Tooltip("発話する AffectActor の ActorId。")]
        private string speakerActorId = "npc";

        [SerializeField, TextArea(2, 6)] private string line = string.Empty;

        [SerializeField, Tooltip("このノードに紐づく評価項目ID（SPIKES のステップ等）。")]
        private string objectiveId = string.Empty;

        [SerializeField, Tooltip("0 より大きいと『沈黙』として扱う。この秒数内に選択すると遮ったと判定する。")]
        private float silenceSeconds;

        [SerializeField, Tooltip("沈黙を守れた場合に加点する値。")]
        private float silenceRespectedScore = 2f;

        [SerializeField, Tooltip("沈黙を遮った場合に減点する値（負の値を入れる）。")]
        private float silenceInterruptedScore = -3f;

        [SerializeField] private List<DialogueChoice> choices = new List<DialogueChoice>();

        [SerializeField, Tooltip("選択肢が無い終端ノード。ここに到達すると対話が終わる。")]
        private bool isTerminal;

        public string Id { get { return id; } }
        public string SpeakerActorId { get { return speakerActorId; } }
        public string Line { get { return line; } }
        public string ObjectiveId { get { return objectiveId; } }
        public float SilenceSeconds { get { return silenceSeconds; } }
        public float SilenceRespectedScore { get { return silenceRespectedScore; } }
        public float SilenceInterruptedScore { get { return silenceInterruptedScore; } }
        public IReadOnlyList<DialogueChoice> Choices { get { return choices; } }
        public bool IsTerminal { get { return isTerminal || choices.Count == 0; } }
    }

    /// <summary>
    /// 対話全体のデータ。C# を書き換えずにシナリオ監修者が分岐を編集できるよう ScriptableObject にする。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueGraph", menuName = "Codemed-x/Dialogue Graph", order = 3)]
    public class DialogueGraph : ScriptableObject
    {
        [SerializeField] private string entryNodeId = string.Empty;
        [SerializeField] private List<DialogueNode> nodes = new List<DialogueNode>();

        private Dictionary<string, DialogueNode> _byId;

        public string EntryNodeId { get { return entryNodeId; } }
        public IReadOnlyList<DialogueNode> Nodes { get { return nodes; } }

        public bool TryGetNode(string nodeId, out DialogueNode node)
        {
            BuildIndexIfNeeded();
            return _byId.TryGetValue(nodeId ?? string.Empty, out node);
        }

        public DialogueNode GetEntryNode()
        {
            DialogueNode node;
            if (TryGetNode(entryNodeId, out node))
            {
                return node;
            }

            return nodes.Count > 0 ? nodes[0] : null;
        }

        /// <summary>参照切れのノードIDを検出する。シナリオ実装中の typo を実行前に潰すために使う。</summary>
        public List<string> FindBrokenReferences()
        {
            BuildIndexIfNeeded();
            List<string> broken = new List<string>();

            if (!string.IsNullOrEmpty(entryNodeId) && !_byId.ContainsKey(entryNodeId))
            {
                broken.Add("entryNodeId: " + entryNodeId);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode node = nodes[i];
                IReadOnlyList<DialogueChoice> choices = node.Choices;
                for (int j = 0; j < choices.Count; j++)
                {
                    DialogueChoice choice = choices[j];
                    AddIfBroken(broken, node.Id, j, choice.NextNodeId);
                    AddIfBroken(broken, node.Id, j, choice.PrematureNextNodeId);
                }
            }

            return broken;
        }

        private void AddIfBroken(List<string> broken, string nodeId, int choiceIndex, string reference)
        {
            if (!string.IsNullOrEmpty(reference) && !_byId.ContainsKey(reference))
            {
                broken.Add(string.Format("{0}.choices[{1}] -> {2}", nodeId, choiceIndex, reference));
            }
        }

        private void BuildIndexIfNeeded()
        {
            if (_byId != null)
            {
                return;
            }

            _byId = new Dictionary<string, DialogueNode>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                DialogueNode node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.Id))
                {
                    _byId[node.Id] = node;
                }
            }
        }

        private void OnEnable()
        {
            _byId = null;
        }

        private void OnValidate()
        {
            _byId = null;
        }
    }
}
