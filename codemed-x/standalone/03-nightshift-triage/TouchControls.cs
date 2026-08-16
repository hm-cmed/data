using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TouchPhase = UnityEngine.TouchPhase;

namespace CodemedX.NightShift
{
    /// <summary>
    /// タブレット（指のドラッグ）で移動と視点操作をするための仮想パッド。
    ///
    /// 画面の左半分に触れると、触れた位置を中心に移動パッドが現れる。
    /// 画面の右半分をドラッグすると視点が回る。どちらも「触れるまで絵が出ない」可変式。
    ///
    /// 実際の移動・視点への適用はしない。<see cref="MoveInput"/> / <see cref="LookDelta"/> を
    /// 公開するだけで、<see cref="SimpleWalker"/> がこれを読みにいく。
    /// PC（タッチ非対応の端末）では何も描画・入力せず、WASD + マウスがそのまま使える。
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        private struct ActiveTouch
        {
            public int Id;
            public Vector2 Position;
            public TouchPhase State;
        }

        [SerializeField, Tooltip(
            "メニュー（SBAR の選択など）が開いている間だけ操作を止めるためのシナリオ。" +
            "未設定ならシーンから自動で探す。")]
        private NightShiftSim sim;

        [SerializeField, Range(60f, 200f), Tooltip("移動パッドの半径（ピクセル）。")]
        private float padRadius = 90f;

        [SerializeField, Range(0.05f, 2f), Tooltip("視点の感度。")]
        private float lookSensitivity = 0.3f;

        [SerializeField, Tooltip("タッチに対応していない端末（PC 等）でも常に表示する。動作確認用。")]
        private bool forceShow;

        private int? _moveTouchId;
        private Vector2 _moveAnchor;
        private Vector2 _moveKnob;

        private int? _lookTouchId;
        private Vector2 _lookLastPosition;

        /// <summary>-1〜1 に収まる移動方向。触れていなければ (0,0)。</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>直近フレームの視点の動き。触れていなければ (0,0)。</summary>
        public Vector2 LookDelta { get; private set; }

        /// <summary>この端末でタッチ操作を使うか。</summary>
        public bool IsActive { get { return forceShow || Input.touchSupported; } }

        private void Awake()
        {
            if (sim == null)
            {
                sim = FindAnyObjectByType<NightShiftSim>();
            }
        }

        private void Update()
        {
            LookDelta = Vector2.zero;

            if (!IsActive || (sim != null && sim.IsUiCapturingInput))
            {
                ReleaseMove();
                ReleaseLook();
                return;
            }

            List<ActiveTouch> touches = ReadTouches();
            bool moveSeen = false;
            bool lookSeen = false;

            for (int i = 0; i < touches.Count; i++)
            {
                ActiveTouch touch = touches[i];

                if (touch.State == TouchPhase.Began)
                {
                    bool isLeftHalf = touch.Position.x < Screen.width * 0.5f;
                    if (isLeftHalf && _moveTouchId == null)
                    {
                        _moveTouchId = touch.Id;
                        _moveAnchor = touch.Position;
                        _moveKnob = touch.Position;
                    }
                    else if (!isLeftHalf && _lookTouchId == null)
                    {
                        _lookTouchId = touch.Id;
                        _lookLastPosition = touch.Position;
                    }
                }

                if (_moveTouchId.HasValue && touch.Id == _moveTouchId.Value)
                {
                    moveSeen = true;
                    if (touch.State == TouchPhase.Ended || touch.State == TouchPhase.Canceled)
                    {
                        ReleaseMove();
                    }
                    else
                    {
                        Vector2 offset = touch.Position - _moveAnchor;
                        _moveKnob = _moveAnchor + Vector2.ClampMagnitude(offset, padRadius);
                    }
                }

                if (_lookTouchId.HasValue && touch.Id == _lookTouchId.Value)
                {
                    lookSeen = true;
                    if (touch.State == TouchPhase.Ended || touch.State == TouchPhase.Canceled)
                    {
                        ReleaseLook();
                    }
                    else
                    {
                        LookDelta += (touch.Position - _lookLastPosition) * lookSensitivity;
                        _lookLastPosition = touch.Position;
                    }
                }
            }

            // 端末が Ended を送り損ねることがあるため、対象の指が今フレームに無ければ手放す。
            if (_moveTouchId.HasValue && !moveSeen)
            {
                ReleaseMove();
            }

            if (_lookTouchId.HasValue && !lookSeen)
            {
                ReleaseLook();
            }

            MoveInput = _moveTouchId.HasValue
                ? Vector2.ClampMagnitude((_moveKnob - _moveAnchor) / padRadius, 1f)
                : Vector2.zero;
        }

        private void ReleaseMove()
        {
            _moveTouchId = null;
            MoveInput = Vector2.zero;
        }

        private void ReleaseLook()
        {
            _lookTouchId = null;
        }

        private void OnGUI()
        {
            if (!IsActive || (sim != null && sim.IsUiCapturingInput) || !_moveTouchId.HasValue)
            {
                return;
            }

            // 数値が大きいほど奥。SimBackdrop（100）より手前、シナリオ本体の UI（既定 0）より奥。
            GUI.depth = 20;
            DrawPad(_moveAnchor, _moveKnob);
        }

        private static void DrawPad(Vector2 anchor, Vector2 knob)
        {
            const float baseSize = 140f;
            const float knobSize = 64f;

            // Touch の座標は左下原点、GUI の Rect は左上原点なので Y を反転する。
            float baseX = anchor.x - baseSize * 0.5f;
            float baseY = Screen.height - anchor.y - baseSize * 0.5f;
            GUI.Box(new Rect(baseX, baseY, baseSize, baseSize), string.Empty);

            float knobX = knob.x - knobSize * 0.5f;
            float knobY = Screen.height - knob.y - knobSize * 0.5f;
            GUI.Box(new Rect(knobX, knobY, knobSize, knobSize), string.Empty);
        }

        private static List<ActiveTouch> ReadTouches()
        {
            List<ActiveTouch> result = new List<ActiveTouch>();

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                foreach (TouchControl touch in Touchscreen.current.touches)
                {
                    UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.None)
                    {
                        continue;
                    }

                    result.Add(new ActiveTouch
                    {
                        Id = touch.touchId.ReadValue(),
                        Position = touch.position.ReadValue(),
                        State = ConvertPhase(phase)
                    });
                }

                return result;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                result.Add(new ActiveTouch { Id = touch.fingerId, Position = touch.position, State = touch.phase });
            }
#endif
            return result;
        }

#if ENABLE_INPUT_SYSTEM
        private static TouchPhase ConvertPhase(UnityEngine.InputSystem.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: return TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved: return TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Stationary: return TouchPhase.Stationary;
                case UnityEngine.InputSystem.TouchPhase.Ended: return TouchPhase.Ended;
                default: return TouchPhase.Canceled;
            }
        }
#endif
    }
}
