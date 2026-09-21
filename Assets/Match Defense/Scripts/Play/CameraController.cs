using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
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
                    // 기기 해상도에 상관없이 일정한 이동 속도를 내도록 화면 높이로 정규화
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
                
                // 픽셀 거리를 화면 높이로 나누어 정규화된 줌 비율 산출
                float deltaDist = (currDist - prevDist) / Screen.height;

                // 데드존(Deadzone) 설정: 미세한 손떨림에는 줌이 반응하지 않도록 차단
                if (Mathf.Abs(deltaDist) > 0.005f)
                {
                    _targetScale = Mathf.Clamp(_targetScale - (deltaDist * 2.0f), minScale, maxScale);
                }

                // --- 2. 회전 및 상하 틸트 ---
                if (isZoomedIn)
                {
                    // 비틀기(Twist) 각도 계산: 이전 프레임의 두 손가락 각도와 현재 프레임의 각도 차이 도출
                    Vector2 prevDir = t1Prev - t0Prev;
                    Vector2 currDir = t1.screenPosition - t0.screenPosition;
                    float twistAngle = Vector2.SignedAngle(prevDir, currDir);

                    // 상하 틸트(Pitch): 두 손가락이 동시에 위아래로 움직인 평균값 (마찬가지로 해상도 정규화)
                    float avgDeltaY = (t0.delta.y + t1.delta.y) * 0.5f;
                    float tiltDelta = (avgDeltaY / Screen.height) * 1000f;

                    // RotatePivot 함수가 예상하는 delta 값의 스케일에 맞게 보정하여 전달
                    // X축은 좌우 회전(Yaw), Y축은 상하 틸트(Pitch)
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

            // [핵심 변경] 월드 좌표계 기준으로 무조건 고정된 한계선(Limit) 안에서만 놀도록 강제 클램프
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