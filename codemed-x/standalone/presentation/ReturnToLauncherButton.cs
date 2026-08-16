using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodemedX.Presentation
{
    /// <summary>
    /// 画面の隅に「メニューに戻る」ボタンを出す。押すと `ScenarioLauncher` のシーンへ戻る。
    ///
    /// シナリオ側のスクリプトには一切触れない。<see cref="SimBackdrop"/> と同じ緩い置き方で、
    /// 5 つのシナリオシーンのどれに置いても同じように動く。
    /// シーンごとに配る運用（ランチャーを使わない）なら、このコンポーネントごと外せばよい。
    /// </summary>
    public class ReturnToLauncherButton : MonoBehaviour
    {
        [SerializeField, Tooltip("戻り先のシーン名。Build Settings に登録したランチャーのシーン名と一致させる。")]
        private string launcherSceneName = "00-Launcher";

        [SerializeField, Range(10, 24)] private int fontSize = 14;

        private void OnGUI()
        {
            // 数値が小さいほど手前。シナリオ本体（既定 0）にも SimBackdrop（100）にも隠れない。
            GUI.depth = -10;

            GUI.skin.button.fontSize = fontSize;
            const float width = 130f;
            const float height = 36f;
            if (GUI.Button(new Rect(Screen.width - width - 12f, 12f, width, height), "≡ メニュー"))
            {
                SceneManager.LoadScene(launcherSceneName);
            }
        }
    }
}
