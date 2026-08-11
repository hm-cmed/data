using System.Collections.Generic;
using System.IO;
using System.Text;
using CodemedX.Assessment;
using UnityEditor;
using UnityEngine;

namespace CodemedX.EditorTools
{
    /// <summary>
    /// codemed-x/schema/objectives.csv から <see cref="ObjectiveCatalog"/> を再生成する。
    ///
    /// 評価項目の正本は Unity のアセットではなく CSV 側に置く。理由:
    /// - CSV は本リポジトリの医学教育モデル・コア・カリキュラム(令和4年度改訂版)と同じ場所で
    ///   バージョン管理され、tools/validate_codemedx.py で id の実在を機械的に検証できる。
    /// - シナリオ監修者（教員）が Unity を開かずに評価項目をレビュー・修正できる。
    /// </summary>
    public static class ObjectiveCatalogImporter
    {
        private const string DefaultCsvRelativePath = "../../schema/objectives.csv";
        private const string MenuPath = "Tools/Codemed-x/評価項目カタログを CSV から再生成";

        [MenuItem(MenuPath)]
        public static void ImportSelectedCatalog()
        {
            ObjectiveCatalog catalog = Selection.activeObject as ObjectiveCatalog;
            if (catalog == null)
            {
                catalog = FindFirstCatalog();
            }

            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "Codemed-x",
                    "ObjectiveCatalog アセットが見つかりません。\n" +
                    "Assets 右クリック > Create > Codemed-x > Objective Catalog で作成してください。",
                    "OK");
                return;
            }

            string csvPath = ResolveCsvPath(catalog);
            if (!File.Exists(csvPath))
            {
                csvPath = EditorUtility.OpenFilePanel("objectives.csv を選択", Application.dataPath, "csv");
                if (string.IsNullOrEmpty(csvPath))
                {
                    return;
                }
            }

            List<ObjectiveEntry> entries = Parse(csvPath);
            Undo.RecordObject(catalog, "Import Objective Catalog");
            catalog.ReplaceEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log(string.Format(
                "[Codemed-x] {0} から評価項目 {1} 件を取り込みました。", csvPath, entries.Count), catalog);
        }

        private static ObjectiveCatalog FindFirstCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(ObjectiveCatalog).Name);
            if (guids.Length == 0)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<ObjectiveCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string ResolveCsvPath(ObjectiveCatalog catalog)
        {
            // Application.dataPath は <repo>/codemed-x/unity/Assets を指すので、2 つ上が codemed-x。
            string codemedxRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string repositoryRoot = Path.GetFullPath(Path.Combine(codemedxRoot, ".."));

            if (!string.IsNullOrEmpty(catalog.SourceCsvPath))
            {
                // SourceCsvPath は "codemed-x/schema/objectives.csv" のようなリポジトリ相対指定。
                string fromRepositoryRoot = Path.GetFullPath(
                    Path.Combine(repositoryRoot, catalog.SourceCsvPath));
                if (File.Exists(fromRepositoryRoot))
                {
                    return fromRepositoryRoot;
                }
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, DefaultCsvRelativePath));
        }

        private static List<ObjectiveEntry> Parse(string csvPath)
        {
            List<ObjectiveEntry> entries = new List<ObjectiveEntry>();
            string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length < 2)
            {
                return entries;
            }

            List<string> header = SplitCsvLine(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i].Trim()))
                {
                    continue;
                }

                List<string> cells = SplitCsvLine(lines[i]);
                entries.Add(new ObjectiveEntry(
                    GetCell(header, cells, "objective_id"),
                    GetCell(header, cells, "scenario_id"),
                    GetCell(header, cells, "label"),
                    GetCell(header, cells, "category"),
                    GetCell(header, cells, "corecurriculum_index").Split(';'),
                    GetCell(header, cells, "corecurriculum_ids").Split(';'),
                    GetCell(header, cells, "external_standard")));
            }

            return entries;
        }

        private static string GetCell(List<string> header, List<string> cells, string columnName)
        {
            int index = header.IndexOf(columnName);
            return index >= 0 && index < cells.Count ? cells[index] : string.Empty;
        }

        /// <summary>ダブルクォート囲みとエスケープに対応した最小限の CSV 行パーサ。</summary>
        private static List<string> SplitCsvLine(string line)
        {
            List<string> cells = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    cells.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }

            cells.Add(current.ToString());

            // csv は Excel 互換のため UTF-8 BOM 付き。先頭セルの BOM を落とす。
            if (cells.Count > 0)
            {
                cells[0] = cells[0].TrimStart('\uFEFF');
            }

            return cells;
        }
    }
}
