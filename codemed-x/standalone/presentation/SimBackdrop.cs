using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Presentation
{
    /// <summary>
    /// シナリオの背後に背景画像と登場人物を描く。
    ///
    /// **シナリオ側のスクリプトを一切変更せずに使える。**
    /// 同じ GameObject（または同じシーンの別の GameObject）に付けるだけ。
    /// IMGUI の描画順は GUI.depth で決まり、数値が大きいほど奥に描かれる。
    /// このコンポーネントは depth を大きくしてあるので、シナリオの UI の後ろに回る。
    ///
    /// 使い方:
    ///   1. シーンに空の GameObject を作り、このスクリプトを付ける
    ///   2. Background に背景画像を割り当てる
    ///   3. Characters に立ち絵を追加し、AnchorX（0=左端 / 0.5=中央 / 1=右端）で位置を決める
    ///
    /// 画像は Inspector で差し替えるだけなので、素材が揃う前に灰色の板で
    /// レイアウトだけ先に決めておくこともできる。
    /// </summary>
    public class SimBackdrop : MonoBehaviour
    {
        /// <summary>画面に立たせる登場人物 1 人分。</summary>
        [Serializable]
        public class Character
        {
            [SerializeField, Tooltip("立ち絵。背景を透過した PNG を推奨。")]
            private Texture2D texture;

            [SerializeField, Range(0f, 1f), Tooltip("横位置。0 = 左端、0.5 = 中央、1 = 右端。")]
            private float anchorX = 0.5f;

            [SerializeField, Range(0.1f, 1.5f), Tooltip("画面の高さに対する大きさ。")]
            private float heightRatio = 0.9f;

            [SerializeField, Range(-0.5f, 0.5f), Tooltip("下端からのずらし量（画面高さ比）。")]
            private float offsetY;

            [SerializeField, Tooltip("左右を反転する。1 枚の立ち絵を左右で使い回すときに使う。")]
            private bool flipHorizontally;

            [SerializeField, Tooltip("発言していないときの暗さ。1 で常に明るいまま。")]
            [Range(0.3f, 1f)]
            private float dimmedBrightness = 0.55f;

            [SerializeField, Tooltip("この人物を最初から明るく表示する。")]
            private bool speakingByDefault = true;

            public Texture2D Texture { get { return texture; } }
            public float AnchorX { get { return anchorX; } }
            public float HeightRatio { get { return heightRatio; } }
            public float OffsetY { get { return offsetY; } }
            public bool FlipHorizontally { get { return flipHorizontally; } }
            public float DimmedBrightness { get { return dimmedBrightness; } }
            public bool SpeakingByDefault { get { return speakingByDefault; } }
        }

        [Header("背景")]
        [SerializeField, Tooltip("背景画像。画面いっぱいに、縦横比を保って表示される。")]
        private Texture2D background;

        [SerializeField, Tooltip("背景が無いときに使う色。")]
        private Color fallbackColor = new Color(0.16f, 0.17f, 0.19f, 1f);

        [Header("登場人物")]
        [SerializeField] private List<Character> characters = new List<Character>();

        [Header("文字の読みやすさ")]
        [SerializeField, Range(0f, 0.9f), Tooltip("背景の上に敷く暗幕の濃さ。文字が読めなければ上げる。")]
        private float dimAmount = 0.45f;

        [SerializeField, Tooltip("暗幕の色。")]
        private Color dimColor = new Color(0f, 0f, 0f, 1f);

        [Header("描画順")]
        [SerializeField, Tooltip("大きいほど奥。シナリオ UI（既定 0）より必ず大きくする。")]
        private int guiDepth = 100;

        private readonly HashSet<int> _speakingIndices = new HashSet<int>();
        private Texture2D _solidTexture;
        private bool _speakingInitialized;

        /// <summary>発言中の人物を切り替える。演出を細かくしたくなったら台本側から呼ぶ。</summary>
        public void SetSpeaking(int characterIndex)
        {
            _speakingIndices.Clear();
            _speakingIndices.Add(characterIndex);
            _speakingInitialized = true;
        }

        /// <summary>全員を明るくする（同席者が同時に反応する場面など）。</summary>
        public void SetAllSpeaking()
        {
            _speakingIndices.Clear();
            for (int i = 0; i < characters.Count; i++)
            {
                _speakingIndices.Add(i);
            }

            _speakingInitialized = true;
        }

        /// <summary>背景を差し替える。局面ごとに変えたい場合に使う。</summary>
        public void SetBackground(Texture2D texture)
        {
            background = texture;
        }

        private void Awake()
        {
            _solidTexture = new Texture2D(1, 1);
            _solidTexture.SetPixel(0, 0, Color.white);
            _solidTexture.Apply();
            _solidTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDestroy()
        {
            if (_solidTexture != null)
            {
                Destroy(_solidTexture);
            }
        }

        private void OnGUI()
        {
            // 数値が大きいほど奥に描かれる。シナリオ側の UI は既定の 0 なので手前に来る。
            GUI.depth = guiDepth;

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

            DrawBackground(screen);
            DrawDim(screen);
            DrawCharacters(screen);
        }

        private void DrawBackground(Rect screen)
        {
            if (background == null)
            {
                DrawSolid(screen, fallbackColor);
                return;
            }

            // 画面を覆いつつ縦横比を保つ（はみ出した分は切り落とす）。
            float screenAspect = screen.width / screen.height;
            float imageAspect = (float)background.width / background.height;

            Rect target;
            if (imageAspect > screenAspect)
            {
                float width = screen.height * imageAspect;
                target = new Rect((screen.width - width) * 0.5f, 0f, width, screen.height);
            }
            else
            {
                float height = screen.width / imageAspect;
                target = new Rect(0f, (screen.height - height) * 0.5f, screen.width, height);
            }

            GUI.DrawTexture(target, background, ScaleMode.StretchToFill);
        }

        private void DrawDim(Rect screen)
        {
            if (dimAmount <= 0f)
            {
                return;
            }

            Color color = dimColor;
            color.a = dimAmount;
            DrawSolid(screen, color);
        }

        private void DrawCharacters(Rect screen)
        {
            if (!_speakingInitialized)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    if (characters[i].SpeakingByDefault)
                    {
                        _speakingIndices.Add(i);
                    }
                }

                _speakingInitialized = true;
            }

            Color previousColor = GUI.color;

            for (int i = 0; i < characters.Count; i++)
            {
                Character character = characters[i];
                if (character.Texture == null)
                {
                    continue;
                }

                float height = screen.height * character.HeightRatio;
                float width = height * character.Texture.width / character.Texture.height;
                float x = (screen.width - width) * character.AnchorX;
                float y = screen.height - height - screen.height * character.OffsetY;

                Rect target = new Rect(x, y, width, height);

                // 発言していない人物を少し暗くすると、誰が話しているかが分かりやすくなる。
                float brightness = _speakingIndices.Contains(i) ? 1f : character.DimmedBrightness;
                GUI.color = new Color(brightness, brightness, brightness, 1f);

                if (character.FlipHorizontally)
                {
                    Matrix4x4 previousMatrix = GUI.matrix;
                    GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), target.center);
                    GUI.DrawTexture(target, character.Texture, ScaleMode.ScaleToFit);
                    GUI.matrix = previousMatrix;
                }
                else
                {
                    GUI.DrawTexture(target, character.Texture, ScaleMode.ScaleToFit);
                }
            }

            GUI.color = previousColor;
        }

        private void DrawSolid(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _solidTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }
    }
}
