using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Presentation
{
    /// <summary>
    /// 360度画像（パノラマ）を局面ごとに切り替える。
    /// <see cref="SimBackdrop"/> の 360度版で、役割は同じ。
    ///
    /// SimBackdrop が画像を「板として画面に描く」のに対し、こちらは Skybox を差し替える。
    /// 360度画像（equirectangular・横縦比 2:1）を SimBackdrop に割り当てると、
    /// 球に貼るはずのものを平面に引き伸ばすことになり、端が歪んで見える。
    /// **360度画像はこちらを使う。**
    ///
    /// 事前に用意するもの（画像 1 枚につき Material 1 つ）:
    ///   1. 画像を選び、Inspector で Texture Shape = 2D、Wrap Mode = Clamp
    ///   2. Assets 右クリック &gt; Create &gt; Material
    ///   3. Shader を Skybox/Panoramic にし、Spherical (HDR) に画像を割り当てる
    ///
    /// 使い方:
    ///   1. シーンに空の GameObject を作り、このスクリプトを付ける
    ///   2. Default Skybox に既定のマテリアル、Phase Skyboxes に局面ごとのマテリアルを並べる
    ///
    /// **カメラが要る。** シーンに Camera を置き、Clear Flags を Skybox にしておく
    /// （Skybox はカメラが描くもので、IMGUI と違ってカメラ無しでは映らない）。
    /// </summary>
    public class PanoramicBackdrop : MonoBehaviour
    {
        /// <summary>局面（Phase）と 360度マテリアルの対応 1 件。</summary>
        [Serializable]
        public class PhaseSkybox
        {
            [SerializeField, Tooltip(
                "シナリオ側の局面名。各 Sim の Phase の名前をそのまま書く。\n" +
                "① Preparation / Observation / Interview / Assessment / Escalation / Debrief\n" +
                "② Briefing / Dialogue / Debrief\n" +
                "③ Briefing / Shift / Debrief\n" +
                "④ Counseling / LabReview / DoctorCall / Debrief\n" +
                "⑤ Briefing / Dialogue / Closing / Debrief")]
            private string phaseName = string.Empty;

            [SerializeField, Tooltip("Shader が Skybox/Panoramic のマテリアル。")]
            private Material skybox;

            public string PhaseName { get { return phaseName; } }
            public Material Skybox { get { return skybox; } }
        }

        [SerializeField, Tooltip("局面ごとの指定が無いときに使うマテリアル。")]
        private Material defaultSkybox;

        [SerializeField, Tooltip(
            "局面ごとに 360度画像を切り替える。上から順に探し、最初に名前が一致したものを使う。")]
        private List<PhaseSkybox> phaseSkyboxes = new List<PhaseSkybox>();

        [SerializeField, Tooltip(
            "局面を読み取るシナリオのスクリプト。未設定なら同じ GameObject → シーン全体の順に自動で探す。")]
        private MonoBehaviour phaseSource;

        [SerializeField, Tooltip(
            "終了時にシーン本来の Skybox へ戻す。Play を止めたあとに設定が書き換わったままになるのを防ぐ。")]
        private bool restoreOnDestroy = true;

        private readonly PhaseWatcher _phase = new PhaseWatcher();
        private Material _originalSkybox;
        private Material _applied;

        /// <summary>局面の対応表を使わず手動で切り替えたい場合に使う。</summary>
        public void SetSkybox(Material skybox)
        {
            Apply(skybox);
        }

        private void Awake()
        {
            _originalSkybox = RenderSettings.skybox;

            _phase.Bind(this, phaseSource);
            phaseSource = _phase.Source;

            Apply(ResolveSkyboxFor(_phase.CurrentPhase));
        }

        private void Update()
        {
            if (_phase.Poll())
            {
                Apply(ResolveSkyboxFor(_phase.CurrentPhase));
            }
        }

        private void OnDestroy()
        {
            // Apply() は null を弾くので、戻すときは直接代入する
            // （もともと Skybox が設定されていないシーンもあるため）。
            if (restoreOnDestroy && _applied != null)
            {
                RenderSettings.skybox = _originalSkybox;
                DynamicGI.UpdateEnvironment();
            }
        }

        private Material ResolveSkyboxFor(string phase)
        {
            if (!string.IsNullOrEmpty(phase))
            {
                for (int i = 0; i < phaseSkyboxes.Count; i++)
                {
                    PhaseSkybox entry = phaseSkyboxes[i];
                    if (entry != null && entry.PhaseName == phase && entry.Skybox != null)
                    {
                        return entry.Skybox;
                    }
                }
            }

            return defaultSkybox;
        }

        private void Apply(Material skybox)
        {
            if (skybox == null || skybox == _applied)
            {
                return;
            }

            _applied = skybox;
            RenderSettings.skybox = skybox;

            // 環境光を新しい空に合わせ直す。切り替えたときだけ呼ぶので負荷は問題にならない。
            DynamicGI.UpdateEnvironment();
        }
    }
}
