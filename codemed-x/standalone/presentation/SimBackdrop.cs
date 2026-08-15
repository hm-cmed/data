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
    ///   2. Background に背景画像を割り当てる（局面ごとに変えたい場合は Phase Backgrounds を使う）
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

            [SerializeField, Tooltip(
                "この人物を表示する局面名（カンマ区切り）。空なら全局面で表示する。" +
                "例: Interview,Assessment")]
            private string visibleInPhases = string.Empty;

            public Texture2D Texture { get { return texture; } }
            public float AnchorX { get { return anchorX; } }
            public float HeightRatio { get { return heightRatio; } }
            public float OffsetY { get { return offsetY; } }
            public bool FlipHorizontally { get { return flipHorizontally; } }
            public float DimmedBrightness { get { return dimmedBrightness; } }
            public bool SpeakingByDefault { get { return speakingByDefault; } }
            public string VisibleInPhases { get { return visibleInPhases; } }
        }

        /// <summary>局面（Phase）と背景画像の対応 1 件。</summary>
        [Serializable]
        public class PhaseBackground
        {
            [SerializeField, Tooltip(
                "シナリオ側の局面名。Console に出る局面名か、各 Sim の Phase enum の名前をそのまま書く。\n" +
                "① Preparation / Observation / Interview / Assessment / Escalation / Debrief\n" +
                "② Briefing / Dialogue / Debrief\n" +
                "③ Briefing / Shift / Debrief\n" +
                "④ Counseling / LabReview / DoctorCall / Debrief\n" +
                "⑤ Briefing / Dialogue / Closing / Debrief")]
            private string phaseName = string.Empty;

            [SerializeField] private Texture2D background;

            public string PhaseName { get { return phaseName; } }
            public Texture2D Background { get { return background; } }
        }

        [Header("背景")]
        [SerializeField, Tooltip("既定の背景画像。局面ごとの指定が無いときはこれが使われる。")]
        private Texture2D background;

        [SerializeField, Tooltip(
            "局面ごとに背景を切り替える。上から順に探し、最初に名前が一致したものを使う。")]
        private List<PhaseBackground> phaseBackgrounds = new List<PhaseBackground>();

        [SerializeField, Tooltip(
            "局面を読み取るシナリオのスクリプト。未設定なら同じ GameObject → シーン全体の順に自動で探す。")]
        private MonoBehaviour phaseSource;

        [SerializeField, Range(0f, 2f), Tooltip("背景が切り替わるときのフェード秒数。0 で即時。")]
        private float crossFadeSeconds = 0.4f;

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

        // 局面の追跡とフェード
        private readonly PhaseWatcher _phase = new PhaseWatcher();
        private Texture2D _currentTexture;
        private Texture2D _previousTexture;
        private float _fadeStartedAt = -1f;

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

        /// <summary>背景を差し替える。局面の対応表を使わず手動で切り替えたい場合に使う。</summary>
        public void SetBackground(Texture2D texture)
        {
            BeginFadeTo(texture);
        }

        private void Awake()
        {
            _solidTexture = new Texture2D(1, 1);
            _solidTexture.SetPixel(0, 0, Color.white);
            _solidTexture.Apply();
            _solidTexture.hideFlags = HideFlags.HideAndDontSave;

            _phase.Bind(this, phaseSource);

            // 自動で見つけた場合も Inspector に見えるようにしておく（何に繋がったかを確認できる）。
            phaseSource = _phase.Source;

            _currentTexture = background;
        }

        private void OnDestroy()
        {
            if (_solidTexture != null)
            {
                Destroy(_solidTexture);
            }
        }

        private void Update()
        {
            if (_phase.Poll())
            {
                BeginFadeTo(ResolveBackgroundFor(_phase.CurrentPhase));
            }
        }

        private Texture2D ResolveBackgroundFor(string phase)
        {
            if (!string.IsNullOrEmpty(phase))
            {
                for (int i = 0; i < phaseBackgrounds.Count; i++)
                {
                    PhaseBackground entry = phaseBackgrounds[i];
                    if (entry != null && entry.PhaseName == phase && entry.Background != null)
                    {
                        return entry.Background;
                    }
                }
            }

            return background;
        }

        private void BeginFadeTo(Texture2D texture)
        {
            if (texture == _currentTexture)
            {
                return;
            }

            _previousTexture = _currentTexture;
            _currentTexture = texture;
            _fadeStartedAt = crossFadeSeconds > 0f ? Time.unscaledTime : -1f;
        }

        // ------------------------------------------------------------------ 描画

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
            float fade = 1f;
            if (_fadeStartedAt >= 0f && crossFadeSeconds > 0f)
            {
                fade = Mathf.Clamp01((Time.unscaledTime - _fadeStartedAt) / crossFadeSeconds);
                if (fade >= 1f)
                {
                    _fadeStartedAt = -1f;
                    _previousTexture = null;
                }
            }

            // 切り替え中は、前の背景を下に敷いたまま新しい背景を重ねて透過させる。
            if (fade < 1f && _previousTexture != null)
            {
                DrawCover(screen, _previousTexture, 1f);
            }
            else if (_currentTexture == null)
            {
                DrawSolid(screen, fallbackColor);
            }

            if (_currentTexture != null)
            {
                DrawCover(screen, _currentTexture, fade);
            }
            else if (fade < 1f)
            {
                DrawSolid(screen, new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, fade));
            }
        }

        /// <summary>画面を覆いつつ縦横比を保って描く（はみ出した分は切り落とす）。</summary>
        private void DrawCover(Rect screen, Texture2D texture, float alpha)
        {
            float screenAspect = screen.width / screen.height;
            float imageAspect = (float)texture.width / texture.height;

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

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(target, texture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
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
                if (character.Texture == null || !IsVisibleInCurrentPhase(character))
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

        private bool IsVisibleInCurrentPhase(Character character)
        {
            string phases = character.VisibleInPhases;
            if (string.IsNullOrEmpty(phases) || string.IsNullOrEmpty(_phase.CurrentPhase))
            {
                return true;
            }

            string[] names = phases.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Trim() == _phase.CurrentPhase)
                {
                    return true;
                }
            }

            return false;
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
