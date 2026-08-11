using System.Collections.Generic;
using System.IO;
using System.Text;
using CodemedX.Core;
using UnityEngine;

namespace CodemedX.Logging
{
    /// <summary>
    /// 送信できなかったイベントを端末内に JSON Lines で退避する。
    /// 演習会場の Wi-Fi は落ちる前提で、学習履歴を落とさないための最後の砦。
    /// 次回起動時に <see cref="EventLogger"/> が読み戻して再送する。
    /// </summary>
    public sealed class EventSpool
    {
        private readonly string _filePath;
        private readonly long _maxSizeBytes;

        public EventSpool(string fileName, long maxSizeBytes)
        {
            _filePath = Path.Combine(Application.persistentDataPath, fileName);
            _maxSizeBytes = maxSizeBytes;
        }

        public string FilePath { get { return _filePath; } }

        public bool HasPendingEvents
        {
            get { return File.Exists(_filePath) && new FileInfo(_filePath).Length > 0; }
        }

        public void Append(IEnumerable<TrainingEvent> events)
        {
            StringBuilder builder = new StringBuilder();
            foreach (TrainingEvent trainingEvent in events)
            {
                builder.Append(trainingEvent.ToJson()).Append('\n');
            }

            if (builder.Length == 0)
            {
                return;
            }

            try
            {
                File.AppendAllText(_filePath, builder.ToString(), Encoding.UTF8);
                TrimIfOversized();
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[Codemed-x] スプールへの書き込みに失敗しました: " + exception.Message);
            }
        }

        /// <summary>スプールを読み出して即座に空にする（読み出し中の追記と二重送信を避けるため）。</summary>
        public List<TrainingEvent> DrainAll()
        {
            List<TrainingEvent> events = new List<TrainingEvent>();
            if (!File.Exists(_filePath))
            {
                return events;
            }

            try
            {
                string[] lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                File.Delete(_filePath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    TrainingEvent parsed = TrainingEvent.FromJson(line);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.event_type))
                    {
                        events.Add(parsed);
                    }
                }
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[Codemed-x] スプールの読み出しに失敗しました: " + exception.Message);
            }

            return events;
        }

        private void TrimIfOversized()
        {
            FileInfo info = new FileInfo(_filePath);
            if (!info.Exists || info.Length <= _maxSizeBytes)
            {
                return;
            }

            // 上限を超えたら古い行から捨てる。端末のストレージを埋めるより履歴の欠落を選ぶ。
            string[] lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            int keepFrom = lines.Length / 2;
            StringBuilder builder = new StringBuilder();
            for (int i = keepFrom; i < lines.Length; i++)
            {
                builder.Append(lines[i]).Append('\n');
            }

            File.WriteAllText(_filePath, builder.ToString(), Encoding.UTF8);
            Debug.LogWarning(string.Format(
                "[Codemed-x] スプールが上限を超えたため古い {0} 件を破棄しました。", keepFrom));
        }
    }
}
