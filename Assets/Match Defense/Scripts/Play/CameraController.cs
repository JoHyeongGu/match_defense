using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using DG.Tweening;
#if !UNITY_EDITOR
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace MatchDefense
{
    public class CameraController : MonoBehaviour
    {
        [Header("<color=yellow>Movement</color>")]
        [SerializeField] private float moveSpeed = 0.03f;
        [SerializeField] private Vector2 horizontalLimit = new(-10f, 10f);
        [SerializeField] private Vector2 verticalLimit = new(-10f, 10f);

        [Header("<color=yellow>Rotation</color>")]
        [SerializeField] private Vector2 rotSpeed = new(0.2f, 0.2f);
        [SerializeField] private float maxRot = 45f;
        [SerializeField] private float minRot = -80f;

        [Header("<color=yellow>Zoom</color>")]
        [SerializeField] private float wheelZoomStep = 0.2f;
        [SerializeField] private float minScale = 0.25f;
        [SerializeField] private float maxScale = 1.0f;

        [Header("<color=yellow>Smoothing</color>")]
        [SerializeField] private float smoothTime = 0.1f;

        // 원본 상태 기억
        private Vector3 _originPos;
        private float _originYaw;
        private float _originPitch;

        // 목표 상태
        private Vector3 _targetPos;
        private float _targetYaw;
        private float _targetPitch;
        private float _targetScale;

        // 보간용 캐시
        private Vector3 _posVel;
        private float _yawVel;
        private float _pitchVel;
        private float _scaleVel;

        private void Awake()
        {
            _originPos = transform.position;
            Vector3 euler = transform.eulerAngles;
            _originYaw = euler.y;
            _originPitch = euler.x > 180f ? euler.x - 360f : euler.x;

            _targetPos = _originPos;
            _targetYaw = _originYaw;
            _targetPitch = _originPitch;
            _targetScale = maxScale;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void LateUpdate()
        {
#if UNITY_EDITOR
            HandleEditorInput();
#else
            HandleMobileInput();
#endif
            CheckResetCondition();
            ApplyTransform();
        }

        #region Public API
        // 줌, 회전, XY 좌표를 최대로 축소(초기화)하면서 Z 위치만 지정된 곳으로 부드럽게 이동
        public void ResetAndMoveZ(float targetZ, float duration)
        {
            // 줌(Scale) 최대 축소 상태(maxScale)로 복구
            DOTween.To(() => _targetScale, x => _targetScale = x, maxScale, duration).SetEase(Ease.OutQuad);

            // 회전값 원래대로 복구
            DOTween.To(() => _targetYaw, x => _targetYaw = x, _originYaw, duration).SetEase(Ease.OutQuad);
            DOTween.To(() => _targetPitch, x => _targetPitch = x, _originPitch, duration).SetEase(Ease.OutQuad);

            // CheckResetCondition에서 엉뚱한 곳으로 튀지 않게 원본 Z값도 목표 Z값으로 동기화
            DOTween.To(() => _originPos.z, x => _originPos.z = x, targetZ, duration).SetEase(Ease.OutQuad);

            // 카메라 위치 X, Y 원상 복구 및 Z축 이동
            DOTween.To(() => _targetPos.x, x => _targetPos.x = x, _originPos.x, duration).SetEase(Ease.OutQuad);
            DOTween.To(() => _targetPos.y, x => _targetPos.y = x, _originPos.y, duration).SetEase(Ease.OutQuad);
            DOTween.To(() => _targetPos.z, x => _targetPos.z = x, targetZ, duration).SetEase(Ease.OutQuad);
        }
        #endregion

        #region Editor Input
#if UNITY_EDITOR
        private void HandleEditorInput()
        {
            if (Defense.UnitSpawnManager.IsDraggingUnit) return;
            if (Mouse.current == null) return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                float factor = scroll > 0f ? (1f - wheelZoomStep) : (1f + wheelZoomStep);
                _targetScale = Mathf.Clamp(_targetScale * factor, minScale, maxScale);
            }

            bool isZoomedIn = _targetScale < maxScale - 0.02f;

            if (isZoomedIn && Mouse.current.leftButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                MovePivot(delta);
            }

            if (isZoomedIn && Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                RotatePivot(delta);
            }
        }
#endif
        #endregion

        #region Mobile Input
#if !UNITY_EDITOR
        private void HandleMobileInput()
        {
            if (Defense.UnitSpawnManager.IsDraggingUnit) return;

            int touchCount = Touch.activeTouches.Count;
            if (touchCount == 0) return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[0].touchId))
                return;

            bool isZoomedIn = _targetScale < maxScale - 0.02f;

            // 1손가락 이동 (Pan)
            if (touchCount == 1 && isZoomedIn)
            {
                Touch t = Touch.activeTouches[0];
                if (t.phase == TouchPhase.Moved)
                {
                    Vector2 normalizedDelta = (t.delta / Screen.height) * 1000f;
                    MovePivot(normalizedDelta);
                }
            }
            // 2손가락 줌(Pinch) & 회전(Twist) & 상하 틸트
            else if (touchCount == 2)
            {
                Touch t0 = Touch.activeTouches[0];
                Touch t1 = Touch.activeTouches[1];

                Vector2 t0Prev = t0.screenPosition - t0.delta;
                Vector2 t1Prev = t1.screenPosition - t1.delta;

                // --- 1. 줌 (Pinch) ---
                float prevDist = Vector2.Distance(t0Prev, t1Prev);
                float currDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);
                
                float deltaDist = (currDist - prevDist) / Screen.height;

                if (Mathf.Abs(deltaDist) > 0.005f)
                {
                    _targetScale = Mathf.Clamp(_targetScale - (deltaDist * 2.0f), minScale, maxScale);
                }

                // --- 2. 회전 및 상하 틸트 ---
                if (isZoomedIn)
                {
                    Vector2 prevDir = t1Prev - t0Prev;
                    Vector2 currDir = t1.screenPosition - t0.screenPosition;
                    float twistAngle = Vector2.SignedAngle(prevDir, currDir);

                    float avgDeltaY = (t0.delta.y + t1.delta.y) * 0.5f;
                    float tiltDelta = (avgDeltaY / Screen.height) * 1000f;

                    Vector2 rotDelta = new Vector2(twistAngle * 3f, tiltDelta);
                    RotatePivot(rotDelta);
                }
            }
        }
#endif
        #endregion

        #region Core Transform
        private void MovePivot(Vector2 delta)
        {
            Quaternion yawRot = Quaternion.Euler(0f, _targetYaw, 0f);
            Vector3 right = yawRot * Vector3.right;
            Vector3 forward = yawRot * Vector3.forward;

            Vector3 move = (right * -delta.x + forward * -delta.y) * (moveSpeed * _targetScale);
            _targetPos += move;

            _targetPos.x = Mathf.Clamp(_targetPos.x, horizontalLimit.x, horizontalLimit.y);
            _targetPos.z = Mathf.Clamp(_targetPos.z, verticalLimit.x, verticalLimit.y);
        }

        private void RotatePivot(Vector2 delta)
        {
            _targetYaw += delta.x * rotSpeed.x;
            _targetPitch -= delta.y * rotSpeed.y;
            _targetPitch = Mathf.Clamp(_targetPitch, minRot, maxRot);
        }

        private void CheckResetCondition()
        {
            if (_targetScale >= maxScale - 0.01f)
            {
                _targetPos = _originPos;
                _targetYaw = _originYaw;
                _targetPitch = _originPitch;
            }
        }

        private void ApplyTransform()
        {
            Vector3 curPos = Vector3.SmoothDamp(transform.position, _targetPos, ref _posVel, smoothTime);
            float curYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetYaw, ref _yawVel, smoothTime);
            float curPitch = Mathf.SmoothDampAngle(transform.eulerAngles.x, _targetPitch, ref _pitchVel, smoothTime);
            float curScale = Mathf.SmoothDamp(transform.localScale.x, _targetScale, ref _scaleVel, smoothTime);

            transform.position = curPos;
            transform.rotation = Quaternion.Euler(curPitch, curYaw, 0f);
            transform.localScale = new Vector3(curScale, curScale, curScale);
        }
        #endregion
    }
}