using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodemedX.NightShift
{
    /// <summary>
    /// 病室のベッド 1 台。<see cref="NightShiftSim"/> のタスク（<see cref="TaskId"/> で対応づけ）が
    /// 発生すると光って知らせ、プレイヤーが近づいて操作キーを押すと着手する。
    ///
    /// IMGUI 版のタスク一覧・ボタンはそのまま残っているので、3D を使わずキーボードだけで
    /// 進める、3D で歩き回りながら進める、のどちらでも同じ判定・同じログになる。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BedStation : MonoBehaviour
    {
        [SerializeField, Tooltip("codemed-x/standalone/03-nightshift-triage/NightShiftScenarioData.cs の ShiftTask.Id と一致させる。")]
        private string taskId = "task";

        [SerializeField, Tooltip("未設定なら自動で探す（シーン内の NightShiftSim を 1 つ想定）。")]
        private NightShiftSim sim;

        [Header("見た目（無くても動く）")]
        [SerializeField, Tooltip("発生中に点滅させるオブジェクト。モニタやランプを想定。")]
        private Renderer alarmRenderer;

        [SerializeField] private Color idleColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.15f, 0.1f, 1f);
        [SerializeField, Range(1f, 10f)] private float blinkSpeed = 4f;

        [SerializeField, Tooltip("発生中に鳴らす音。未設定なら無音。")]
        private AudioSource alarmAudio;

        private bool _playerInRange;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            if (sim == null)
            {
                sim = FindAnyObjectByType<NightShiftSim>();
            }

            if (alarmRenderer != null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void Update()
        {
            if (sim == null)
            {
                return;
            }

            NightShiftSim.TaskSnapshot? snapshot = FindSnapshot();
            UpdateVisual(snapshot);

            // 対応中でなければ何もしない。近くにいて操作キーを押したときだけ着手を試みる。
            if (_playerInRange && snapshot.HasValue && !snapshot.Value.IsBeingAttended
                && WasInteractPressed())
            {
                sim.TryAttendTo(taskId);
            }
        }

        private NightShiftSim.TaskSnapshot? FindSnapshot()
        {
            System.Collections.Generic.List<NightShiftSim.TaskSnapshot> snapshots =
                sim.GetActiveTaskSnapshots();

            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].Id == taskId)
                {
                    return snapshots[i];
                }
            }

            return null;
        }

        private void UpdateVisual(NightShiftSim.TaskSnapshot? snapshot)
        {
            if (alarmAudio != null)
            {
                bool shouldPlay = snapshot.HasValue && !snapshot.Value.IsBeingAttended;
                if (shouldPlay && !alarmAudio.isPlaying)
                {
                    alarmAudio.Play();
                }
                else if (!shouldPlay && alarmAudio.isPlaying)
                {
                    alarmAudio.Stop();
                }
            }

            if (alarmRenderer == null)
            {
                return;
            }

            Color target = idleColor;
            if (snapshot.HasValue)
            {
                bool nearNeglect = snapshot.Value.NeglectSeconds > 0f
                    && snapshot.Value.UnattendedSeconds >= snapshot.Value.NeglectSeconds * 0.6f;

                target = snapshot.Value.HasEscalated || nearNeglect ? criticalColor : activeColor;

                if (!snapshot.Value.IsBeingAttended)
                {
                    // 点滅させて「未対応」を視覚的に目立たせる。
                    float blink = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
                    target = Color.Lerp(idleColor, target, 0.4f + blink * 0.6f);
                }
            }

            alarmRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", target);
            _propertyBlock.SetColor("_Color", target); // Built-in RP のシェーダーでも効くように両方セットする
            _propertyBlock.SetColor("_EmissionColor", target * (snapshot.HasValue ? 1.5f : 0f));
            alarmRenderer.SetPropertyBlock(_propertyBlock);
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
            if (!_playerInRange || sim == null)
            {
                return;
            }

            NightShiftSim.TaskSnapshot? snapshot = FindSnapshot();
            if (!snapshot.HasValue || snapshot.Value.IsBeingAttended)
            {
                return;
            }

            const float width = 260f;
            const float height = 28f;
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - 70f, width, height),
                "[E] 対応する： " + snapshot.Value.Label);
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
