using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodemedX.NightShift
{
    /// <summary>
    /// ナースステーションの電話。近づいて操作キーを押すと SBAR 選択画面（既存の IMGUI パネル）を開く。
    /// 選択・送信そのものはロジックを持たず <see cref="NightShiftSim.TryOpenEscalationMenu"/> を呼ぶだけ。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NurseStationPhone : MonoBehaviour
    {
        [SerializeField, Tooltip("未設定なら自動で探す（シーン内の NightShiftSim を 1 つ想定）。")]
        private NightShiftSim sim;

        private bool _playerInRange;

        private void Awake()
        {
            if (sim == null)
            {
                sim = FindAnyObjectByType<NightShiftSim>();
            }
        }

        private void Update()
        {
            if (_playerInRange && sim != null && WasInteractPressed())
            {
                sim.TryOpenEscalationMenu();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<SimpleWalker>() != null)
            {
                _playerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<SimpleWalker>() != null)
            {
                _playerInRange = false;
            }
        }

        private void OnGUI()
        {
            if (!_playerInRange || sim == null || !sim.IsShiftActive)
            {
                return;
            }

            // ボタンにしてあるのは、キーボードの無いタブレットでもタップで開けるようにするため。
            // PC では引き続き [E] キーでも開ける（下の Update を参照）。
            const float width = 260f;
            const float height = 36f;
            if (GUI.Button(new Rect((Screen.width - width) * 0.5f, Screen.height - 70f, width, height),
                "[E] 医師に電話する"))
            {
                sim.TryOpenEscalationMenu();
            }
        }

        private static bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.eKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }
    }
}
