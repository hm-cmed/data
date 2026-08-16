using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodemedX.Launcher
{
    /// <summary>
    /// 5 つのシナリオへの入り口となる、シーン選択メニュー。
    ///
    /// このスクリプトはどのシナリオにも依存しない。空のシーンにこれだけを置き、
    /// Build Settings に「このシーン + 5 つのシナリオのシーン」を登録すれば、
    /// 1 つの WebGL ビルド・1 つの URL で 5 本すべてに入れるようになる。
    ///
    /// 使い方は codemed-x/docs/webgl-deploy.md を参照。
    /// </summary>
    public class ScenarioLauncher : MonoBehaviour
    {
        /// <summary>メニュー 1 行分（表示名 + 読み込むシーン名）。</summary>
        [Serializable]
        public class MenuItem
        {
            [SerializeField] private string label = string.Empty;

            [SerializeField, Tooltip("Build Settings に登録したシーン名（拡張子なし）と 1 文字も違えず一致させる。")]
            private string sceneName = string.Empty;

            public MenuItem()
            {
            }

            public MenuItem(string label, string sceneName)
            {
                this.label = label;
                this.sceneName = sceneName;
            }

            public string Label { get { return label; } }
            public string SceneName { get { return sceneName; } }
        }

        [Header("表示")]
        [SerializeField] private string title = "Codemed-x 学習用シミュレーション";

        [SerializeField, Tooltip("日本語が □ になる場合は、日本語を含むフォントを割り当てる。")]
        private Font uiFont;

        [SerializeField, Range(12, 28)] private int fontSize = 18;

        [Header("メニュー項目")]
        [SerializeField, Tooltip(
            "空のまま Play すると、既定の 5 本（シーン名は README の推奨どおり）で埋まる。\n" +
            "シーン名は Build Settings に登録した名前と一致させること。一致しないと押しても反応しない。")]
        private List<MenuItem> items = new List<MenuItem>();

        private string _lastError = string.Empty;

        private void Awake()
        {
            if (items.Count == 0)
            {
                items.AddRange(CreateDefaultItems());
            }
        }

        private static IEnumerable<MenuItem> CreateDefaultItems()
        {
            yield return new MenuItem("① 相談援助面接", "01-WelfareInterview");
            yield return new MenuItem("② 困難な対話（ACP）", "02-AcpDialogue");
            yield return new MenuItem("③ 夜勤・複数患者の優先順位判断", "03-NightShiftTriage");
            yield return new MenuItem("④ 疑義照会（薬剤師）", "04-PharmacistInquiry");
            yield return new MenuItem("⑤ ゲートキーパー", "05-Gatekeeper");
        }

        private void OnGUI()
        {
            if (uiFont != null)
            {
                GUI.skin.font = uiFont;
            }

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.label.wordWrap = true;

            float width = Mathf.Min(Screen.width - 80f, 560f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 40f, width, Screen.height - 80f));

            GUILayout.Label(title);
            GUILayout.Space(20f);

            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                // 高さを大きめにしてあるのは、タブレットでも指で確実にタップできるようにするため。
                if (GUILayout.Button(item.Label, GUILayout.Height(64f)))
                {
                    Load(item.SceneName);
                }

                GUILayout.Space(12f);
            }

            if (!string.IsNullOrEmpty(_lastError))
            {
                GUILayout.Space(10f);
                GUILayout.Label(_lastError);
            }

            GUILayout.EndArea();
        }

        private void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                _lastError = "このボタンにはシーン名が設定されていません。Inspector で Scene Name を入力してください。";
                return;
            }

            // Build Settings に登録の無いシーン名を渡すと、Unity は画面を変えずに
            // Console へエラーを出すだけになる。ここでも同じ理由を出しておく。
            _lastError = "「" + sceneName + "」に移動できない場合は、" +
                "File > Build Settings にこの名前でシーンが登録されているか確認してください。";

            SceneManager.LoadScene(sceneName);
        }
    }
}
