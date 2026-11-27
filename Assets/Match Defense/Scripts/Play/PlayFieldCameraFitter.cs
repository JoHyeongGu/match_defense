using UnityEngine;

namespace MatchDefense
{
    [RequireComponent(typeof(Camera))]
    public class PlayFieldCameraFitter : MonoBehaviour
    {
        [Header("<color=yellow>Target Field</color>")]
        [SerializeField] private Renderer targetBoard;
        [SerializeField] private float extraPadding = 0.5f;

        [Header("<color=yellow>UI Safe Margins</color>")]
        [Range(0f, 0.4f)] [SerializeField] private float topUiRatio = 0.085f;
        [Range(0f, 0.8f)] [SerializeField] private float bottomUiRatio = 0.58f;
        [Range(0f, 0.2f)] [SerializeField] private float horizontalMargin = 0.03f;

        private Camera _cam;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void Start()
        {
            FitCamera();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
                FitCamera();
        }

        [ContextMenu("Fit Camera Now")]
        public void FitCamera()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null || targetBoard == null || Screen.width <= 0 || Screen.height <= 0) return;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            Bounds b = targetBoard.bounds;
            float fieldWidth = b.size.x + extraPadding * 2f;
            float fieldDepth = b.size.z + extraPadding * 2f;

            float aspect = (float)Screen.width / Screen.height;
            float visibleHeightRatio = Mathf.Max(0.1f, 1f - topUiRatio - bottomUiRatio);
            float visibleWidthRatio = Mathf.Max(0.1f, 1f - horizontalMargin * 2f);

            float requiredHeightForDepth = fieldDepth / visibleHeightRatio;
            float requiredHeightForWidth = fieldWidth / (visibleWidthRatio * aspect);
            float frustumHeight = Mathf.Max(requiredHeightForDepth, requiredHeightForWidth);

            float windowCenterY = bottomUiRatio + visibleHeightRatio * 0.5f;
            float verticalOffsetRatio = windowCenterY - 0.5f;
            float offsetZ = -verticalOffsetRatio * frustumHeight;

            if (_cam.orthographic)
            {
                _cam.orthographicSize = frustumHeight * 0.5f;
                Vector3 worldPos = new Vector3(b.center.x, transform.position.y, b.center.z + offsetZ);
                transform.position = worldPos;
            }
            else
            {
                float halfFovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = (frustumHeight * 0.5f) / Mathf.Tan(halfFovRad);
                Vector3 worldPos = new Vector3(b.center.x, b.center.y + distance, b.center.z + offsetZ);
                transform.position = worldPos;
            }
        }
    }
}