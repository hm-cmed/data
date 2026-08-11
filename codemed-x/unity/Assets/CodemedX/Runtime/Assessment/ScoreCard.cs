using System.Collections.Generic;
using CodemedX.Core;

namespace CodemedX.Assessment
{
    /// <summary>
    /// 評価項目ごとの到達状況を端末側で集約する。
    ///
    /// 注意: ここで出す合計点は「デブリーフィング画面に即時表示するための暫定値」であり、
    /// 成績としての確定はサーバ側で行う（端末はシンクライアント）。
    /// 端末の値を正とすると、改造ビルドで成績を書き換えられる。
    /// </summary>
    public sealed class ScoreCard
    {
        /// <summary>1 評価項目分の集計。</summary>
        public sealed class ObjectiveResult
        {
            public string ObjectiveId;
            public float Score;
            public int AttemptCount;
            public int ErrorCount;
            public bool Achieved;
        }

        private readonly Dictionary<string, ObjectiveResult> _results = new Dictionary<string, ObjectiveResult>();
        private readonly List<string> _errorTypes = new List<string>();
        private readonly ObjectiveCatalog _catalog;
        private readonly string _scenarioId;

        public ScoreCard(string scenarioId, ObjectiveCatalog catalog = null)
        {
            _scenarioId = scenarioId;
            _catalog = catalog;
        }

        public IReadOnlyList<string> ErrorTypes { get { return _errorTypes; } }

        public int ErrorCount { get { return _errorTypes.Count; } }

        public float TotalScore
        {
            get
            {
                float total = 0f;
                foreach (KeyValuePair<string, ObjectiveResult> pair in _results)
                {
                    total += pair.Value.Score * GetWeight(pair.Key);
                }

                return total;
            }
        }

        /// <summary>行動 1 回分を記録する。<paramref name="objectiveId"/> が空でもエラーは集計する。</summary>
        public void Record(string objectiveId, float score, string errorType = ErrorTypes.None, bool achieved = false)
        {
            bool isError = !string.IsNullOrEmpty(errorType) && errorType != ErrorTypes.None;
            if (isError)
            {
                _errorTypes.Add(errorType);
            }

            if (string.IsNullOrEmpty(objectiveId))
            {
                return;
            }

            ObjectiveResult result;
            if (!_results.TryGetValue(objectiveId, out result))
            {
                result = new ObjectiveResult { ObjectiveId = objectiveId };
                _results[objectiveId] = result;
            }

            result.Score += score;
            result.AttemptCount++;
            if (isError)
            {
                result.ErrorCount++;
            }

            if (achieved)
            {
                result.Achieved = true;
            }
        }

        public void MarkAchieved(string objectiveId)
        {
            Record(objectiveId, 0f, ErrorTypes.None, true);
        }

        public bool IsAchieved(string objectiveId)
        {
            ObjectiveResult result;
            return _results.TryGetValue(objectiveId, out result) && result.Achieved;
        }

        public ObjectiveResult Get(string objectiveId)
        {
            ObjectiveResult result;
            return _results.TryGetValue(objectiveId, out result) ? result : null;
        }

        /// <summary>カタログ上の必須項目のうち、未達成のものを返す。</summary>
        public List<string> GetUnmetRequiredObjectives()
        {
            List<string> unmet = new List<string>();
            if (_catalog == null)
            {
                return unmet;
            }

            List<ObjectiveEntry> scenarioObjectives = _catalog.GetForScenario(_scenarioId);
            for (int i = 0; i < scenarioObjectives.Count; i++)
            {
                ObjectiveEntry entry = scenarioObjectives[i];
                if (entry.Required && !IsAchieved(entry.ObjectiveId))
                {
                    unmet.Add(entry.ObjectiveId);
                }
            }

            return unmet;
        }

        /// <summary>完了イベントの payload_json に載せるサマリ。</summary>
        public string ToPayloadJson(float elapsedSeconds)
        {
            int achievedCount = 0;
            foreach (KeyValuePair<string, ObjectiveResult> pair in _results)
            {
                if (pair.Value.Achieved)
                {
                    achievedCount++;
                }
            }

            return PayloadBuilder.Create()
                .Add("elapsed_sec", elapsedSeconds)
                .Add("total_score", TotalScore)
                .Add("objectives_touched", _results.Count)
                .Add("objectives_achieved", achievedCount)
                .Add("error_count", ErrorCount)
                .Add("unmet_required", string.Join("|", GetUnmetRequiredObjectives().ToArray()))
                .Build();
        }

        private float GetWeight(string objectiveId)
        {
            ObjectiveEntry entry;
            if (_catalog != null && _catalog.TryGet(objectiveId, out entry))
            {
                return entry.Weight;
            }

            return 1f;
        }
    }
}
