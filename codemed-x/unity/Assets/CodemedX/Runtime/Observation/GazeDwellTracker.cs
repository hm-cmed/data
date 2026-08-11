using System.Collections.Generic;
using UnityEngine;

namespace CodemedX.Observation
{
    /// <summary>
    /// レイキャストで <see cref="ObservationTarget"/> を一定時間見つめたら「発見」として扱う。
    ///
    /// 既定では HMD の視線方向（カメラの前方）を使う。XRIT 3.x の Ray Interactor を使う場合は
    /// <see cref="SetRayOrigin"/> にそのコントローラの Transform を渡す（Interactor 実装には依存させない）。
    /// </summary>
    [DisallowMultipleComponent]
    public class GazeDwellTracker : MonoBehaviour
    {
        [SerializeField, Tooltip("レイの起点。未設定なら Camera.main を使う。")]
        private Transform rayOrigin;

        [SerializeField, Tooltip("観察対象として扱うレイヤー。")]
        private LayerMask targetLayers = ~0;

        [SerializeField, Tooltip("レイの最大距離（m）。")]
        private float maxDistance = 10f;

        [SerializeField, Tooltip("視線が一瞬外れても注視を継続扱いにする猶予（秒）。")]
        private float dwellGraceSeconds = 0.3f;

        [SerializeField, Tooltip("発見済みの対象を再び注視しても何もしない。")]
        private bool ignoreDiscovered = true;

        private readonly Dictionary<ObservationTarget, float> _dwellSeconds =
            new Dictionary<ObservationTarget, float>();

        private ObservationTarget _currentTarget;
        private float _offTargetSeconds;

        /// <summary>現在注視している対象（無ければ null）。</summary>
        public ObservationTarget CurrentTarget { get { return _currentTarget; } }

        /// <summary>現在の対象を注視し続けている秒数。</summary>
        public float CurrentDwellSeconds
        {
            get
            {
                float dwell;
                return _currentTarget != null && _dwellSeconds.TryGetValue(_currentTarget, out dwell) ? dwell : 0f;
            }
        }

        public void SetRayOrigin(Transform origin)
        {
            rayOrigin = origin;
        }

        protected virtual void Update()
        {
            Transform origin = ResolveOrigin();
            if (origin == null)
            {
                return;
            }

            ObservationTarget hitTarget = Raycast(origin);

            if (hitTarget == null)
            {
                // 猶予時間内に戻ってくれば注視の継続とみなす（HMD の微小な揺れで進捗が消えないように）。
                _offTargetSeconds += Time.unscaledDeltaTime;
                if (_offTargetSeconds > dwellGraceSeconds)
                {
                    _currentTarget = null;
                }

                return;
            }

            if (hitTarget != _currentTarget)
            {
                _currentTarget = hitTarget;
            }

            _offTargetSeconds = 0f;

            if (ignoreDiscovered && hitTarget.IsDiscovered)
            {
                return;
            }

            float dwell;
            _dwellSeconds.TryGetValue(hitTarget, out dwell);
            dwell += Time.unscaledDeltaTime;
            _dwellSeconds[hitTarget] = dwell;

            if (dwell >= hitTarget.RequiredDwellSeconds)
            {
                hitTarget.Discover(dwell);
            }
        }

        private ObservationTarget Raycast(Transform origin)
        {
            RaycastHit hit;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, maxDistance, targetLayers))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<ObservationTarget>();
        }

        private Transform ResolveOrigin()
        {
            if (rayOrigin != null)
            {
                return rayOrigin;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }
    }
}
