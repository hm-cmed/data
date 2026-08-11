using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Assessment
{
    /// <summary>
    /// 1 件の評価項目。schema/objectives.csv の 1 行に対応する。
    /// </summary>
    [Serializable]
    public sealed class ObjectiveEntry
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private string scenarioId;
        [SerializeField] private string label;
        [SerializeField] private string category;

        [SerializeField, Tooltip("医学教育モデル・コア・カリキュラム(令和4年度改訂版)のインデックス。例: CM-01-02-02")]
        private string[] coreCurriculumIndices = new string[0];

        [SerializeField, Tooltip("同カリキュラムの id。改訂で番号が動いても指す項目が変わらない。")]
        private string[] coreCurriculumIds = new string[0];

        [SerializeField, Tooltip("SPIKES、児童福祉法第33条など、準拠する外部基準。")]
        private string externalStandard;

        [SerializeField, Tooltip("総合スコアに掛ける重み。")]
        private float weight = 1f;

        [SerializeField, Tooltip("未達成ならシナリオ全体を不合格にする必須項目か。")]
        private bool required;

        public string ObjectiveId { get { return objectiveId; } }
        public string ScenarioId { get { return scenarioId; } }
        public string Label { get { return label; } }
        public string Category { get { return category; } }
        public IReadOnlyList<string> CoreCurriculumIndices { get { return coreCurriculumIndices; } }
        public IReadOnlyList<string> CoreCurriculumIds { get { return coreCurriculumIds; } }
        public string ExternalStandard { get { return externalStandard; } }
        public float Weight { get { return weight; } }
        public bool Required { get { return required; } }

        public ObjectiveEntry() { }

        public ObjectiveEntry(
            string objectiveId,
            string scenarioId,
            string label,
            string category,
            string[] coreCurriculumIndices,
            string[] coreCurriculumIds,
            string externalStandard)
        {
            this.objectiveId = objectiveId;
            this.scenarioId = scenarioId;
            this.label = label;
            this.category = category;
            this.coreCurriculumIndices = coreCurriculumIndices ?? new string[0];
            this.coreCurriculumIds = coreCurriculumIds ?? new string[0];
            this.externalStandard = externalStandard;
        }
    }

    /// <summary>
    /// シナリオが参照する評価項目の一覧。
    /// 実体は本リポジトリの schema/objectives.csv であり、Unity 側のアセットは
    /// Tools &gt; Codemed-x &gt; 評価項目カタログを CSV から再生成 で同期する。
    /// これにより objective_id は必ず医学教育モデル・コア・カリキュラムの id に辿れる。
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectiveCatalog", menuName = "Codemed-x/Objective Catalog", order = 1)]
    public class ObjectiveCatalog : ScriptableObject
    {
        [SerializeField] private string sourceCsvPath = "codemed-x/schema/objectives.csv";
        [SerializeField] private List<ObjectiveEntry> entries = new List<ObjectiveEntry>();

        private Dictionary<string, ObjectiveEntry> _byId;

        public string SourceCsvPath { get { return sourceCsvPath; } }
        public IReadOnlyList<ObjectiveEntry> Entries { get { return entries; } }

        public bool TryGet(string objectiveId, out ObjectiveEntry entry)
        {
            BuildIndexIfNeeded();
            return _byId.TryGetValue(objectiveId ?? string.Empty, out entry);
        }

        public bool Contains(string objectiveId)
        {
            ObjectiveEntry ignored;
            return TryGet(objectiveId, out ignored);
        }

        public List<ObjectiveEntry> GetForScenario(string scenarioId)
        {
            List<ObjectiveEntry> result = new List<ObjectiveEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ScenarioId == scenarioId)
                {
                    result.Add(entries[i]);
                }
            }

            return result;
        }

        /// <summary>エディタ拡張から CSV の内容で丸ごと差し替える。</summary>
        public void ReplaceEntries(List<ObjectiveEntry> newEntries)
        {
            entries = newEntries ?? new List<ObjectiveEntry>();
            _byId = null;
        }

        private void BuildIndexIfNeeded()
        {
            if (_byId != null)
            {
                return;
            }

            _byId = new Dictionary<string, ObjectiveEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ObjectiveEntry entry = entries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.ObjectiveId))
                {
                    _byId[entry.ObjectiveId] = entry;
                }
            }
        }

        private void OnEnable()
        {
            _byId = null;
        }
    }
}
