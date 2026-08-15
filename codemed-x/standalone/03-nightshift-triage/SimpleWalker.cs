using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodemedX.NightShift
{
    /// <summary>
    /// 病棟を歩き回るための一人称移動。WASD で移動、マウスで視点。
    ///
    /// XR パッケージには依存しない。まず PC で空間の作りを確かめるためのもので、
    /// HMD へ進む段になったら XR Origin に置き換える。
    ///
    /// Input System / 旧 Input Manager のどちらでも動く（両方無効なら移動しない）。
    ///
    /// 同じフォルダの <see cref="NightShiftSim"/> を見て、メニューが開いている間は操作を止める。
    /// 他のシナリオで使い回す場合は、その参照を外して <see cref="ControlSuspended"/> を
    /// 外から切り替えればよい。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleWalker : MonoBehaviour
    {
        [SerializeField, Tooltip("視点を回すカメラ。未設定なら子の Camera を探す。")]
        private Transform cameraTransform;

        [SerializeField, Range(0.5f, 5f)] private float moveSpeed = 1.6f;

        [SerializeField, Range(0.05f, 1f)] private float mouseSensitivity = 0.15f;

        [SerializeField, Range(30f, 89f)] private float pitchLimit = 80f;

        [SerializeField, Tooltip("開始時にマウスカーソルを画面へ固定する。Esc で解除。")]
        private bool lockCursorOnStart = true;

        [SerializeField, Tooltip(
            "メニュー（SBAR の選択、説明、振り返り）が開いている間だけ操作を止めるためのシナリオ。" +
            "未設定ならシーンから自動で探す。")]
        private NightShiftSim sim;

        private CharacterController _controller;
        private float _pitch;
        private bool _cursorLocked;
        private bool _suspended;

        /// <summary>true の間、移動と視点操作を止めてカーソルを解放する。</summary>
        public bool ControlSuspended { get; set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (sim == null)
            {
                sim = FindAnyObjectByType<NightShiftSim>();
            }

            if (cameraTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null)
                {
                    cameraTransform = childCamera.transform;
                }
            }
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            // メニューが開いている間は、カーソルを解放して視点も移動も止める。
            // 固定したままだと、SBAR の選択肢を押そうとしただけで視点が回ってしまう。
            bool suspended = ControlSuspended || (sim != null && sim.IsUiCapturingInput);
            if (suspended != _suspended)
            {
                _suspended = suspended;
                if (suspended)
                {
                    SetCursorLocked(false);
                }
                else if (lockCursorOnStart)
                {
                    SetCursorLocked(true);
                }
            }

            if (suspended)
            {
                return;
            }

            // Esc でカーソルを解放し、画面をクリックすると再び固定する。
            // UI のボタンを押したいときに視点操作が邪魔にならないようにするため。
            if (WasCancelPressed())
            {
                SetCursorLocked(false);
            }
            else if (!_cursorLocked && WasClickPressed())
            {
                SetCursorLocked(true);
            }

            if (_cursorLocked)
            {
                ApplyLook(ReadLookDelta());
            }

            ApplyMove(ReadMoveInput());
        }

        private void ApplyLook(Vector2 delta)
        {
            transform.Rotate(Vector3.up, delta.x * mouseSensitivity, Space.World);

            if (cameraTransform == null)
            {
                return;
            }

            _pitch = Mathf.Clamp(_pitch - delta.y * mouseSensitivity, -pitchLimit, pitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void ApplyMove(Vector2 input)
        {
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // 段差の無い平屋を想定しているので、重力は接地させるだけの弱いもので足りる。
            Vector3 velocity = direction * moveSpeed + Vector3.down * 4f;
            _controller.Move(velocity * Time.deltaTime);
        }

        private void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        // ------------------------------------------------------------------ 入力

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                return new Vector2(x, y);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }

        private static Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.delta.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            // 旧 Input Manager の Mouse X/Y は感度が大きく異なるので倍率を合わせる。
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.escapeKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private static bool WasClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }
}
