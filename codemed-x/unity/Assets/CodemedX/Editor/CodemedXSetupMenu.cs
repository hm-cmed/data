using System.IO;
using System.Text;
using CodemedX.Assessment;
using CodemedX.Logging;
using UnityEditor;
using UnityEngine;

namespace CodemedX.EditorTools
{
    /// <summary>
    /// セットアップ用のメニュー。
    ///
    /// ScriptableObject は本来 Assets 右クリック &gt; Create から作るが、
    /// Create メニューは項目が多く目的のものを探しにくいため、
    /// 迷わない入口として Tools メニューにも置いている。
    /// </summary>
    public static class CodemedXSetupMenu
    {
        private const string ResourcesFolder = "Assets/CodemedX/Resources";
        private const string SettingsAssetPath =
            ResourcesFolder + "/" + EventLoggerSettings.DefaultResourcePath + ".asset";

        [MenuItem("Tools/Codemed-x/ログ送信設定アセットを作成 or 選択", false, 10)]
        public static void CreateOrSelectSettings()
        {
            EventLoggerSettings settings = AssetDatabase.LoadAssetAtPath<EventLoggerSettings>(SettingsAssetPath);

            if (settings == null)
            {
                // Resources フォルダ直下・既定の名前でないと EventLogger が自動で読み込めない。
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();

                settings = ScriptableObject.CreateInstance<EventLoggerSettings>();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
                AssetDatabase.SaveAssets();

                Debug.Log("[Codemed-x] 設定アセットを作成しました: " + SettingsAssetPath, settings);
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Codemed-x/セットアップ状態を確認", false, 11)]
        public static void ReportSetupStatus()
        {
            StringBuilder report = new StringBuilder("[Codemed-x] セットアップ状態\n");

            EventLoggerSettings settings = AssetDatabase.LoadAssetAtPath<EventLoggerSettings>(SettingsAssetPath);
            if (settings == null)
            {
                report.Append("  ✗ ログ送信設定アセットがありません（").Append(SettingsAssetPath).Append("）\n");
                report.Append("     → Tools > Codemed-x > ログ送信設定アセットを作成 or 選択 で作れます\n");
            }
            else
            {
                report.Append("  ✓ ログ送信設定アセット: ").Append(SettingsAssetPath).Append('\n');
                report.Append("     送信先: ").Append(
                    string.IsNullOrEmpty(settings.EndpointUrl)
                        ? "未設定（イベントは端末内に退避されます）"
                        : settings.EndpointUrl).Append('\n');
                report.Append("     Console 出力: ").Append(settings.LogToConsole ? "有効" : "無効").Append('\n');
            }

            string[] catalogGuids = AssetDatabase.FindAssets("t:" + typeof(ObjectiveCatalog).Name);
            if (catalogGuids.Length == 0)
            {
                report.Append("  - 評価項目カタログは未作成です（シナリオ実装時に必要になります）\n");
            }
            else
            {
                for (int i = 0; i < catalogGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                    ObjectiveCatalog catalog = AssetDatabase.LoadAssetAtPath<ObjectiveCatalog>(path);
                    report.Append("  ✓ 評価項目カタログ: ").Append(path)
                          .Append("（").Append(catalog != null ? catalog.Entries.Count : 0).Append(" 項目）\n");
                }
            }

            report.Append("  情報: 端末内スプールの保存先 ").Append(Application.persistentDataPath);

            Debug.Log(report.ToString());
        }
    }
}
