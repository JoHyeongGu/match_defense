using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

namespace MatchDefense
{
    [RequireComponent(typeof(PanelRenderer))]
    public class ProgramManager : MonoBehaviour
    {
        public static ProgramManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                _panelRenderer = GetComponent<PanelRenderer>();
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            DestroyFadeScreen();
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            InitFadeScreen(root);
        }


        #region Fade Screen

        public bool IsLoading => _fadeScreen == null ? _currentAlpha <= 0.05f 
                                : _fadeScreen.resolvedStyle.opacity <= 0.05f;
        private Tween _fadeTween;
        private VisualElement _fadeScreen;
        private PanelRenderer _panelRenderer;
        private Color _currentColor = Color.black;
        private float _currentAlpha = 1f;

        private void InitFadeScreen(VisualElement root)
        {
            _fadeScreen = new VisualElement { pickingMode = PickingMode.Ignore };
            _fadeScreen.style.position = Position.Absolute;
            _fadeScreen.style.top = 0;
            _fadeScreen.style.bottom = 0;
            _fadeScreen.style.left = 0;
            _fadeScreen.style.right = 0;
            UpdateFadeVisual(_currentAlpha, _currentColor);
            root.Add(_fadeScreen);
        }

        private void DestroyFadeScreen()
        {
            _fadeTween?.Kill();
        }

        public void FadeScreen(bool off, Color? color = null, float duration = 0.5f)
        {
            _currentColor = color ?? Color.black;
            float targetAlpha = off ? 1f : 0f;
            _fadeTween?.Kill();
            if (_fadeScreen == null)
            {
                _currentAlpha = targetAlpha;
                return;
            }
            _fadeScreen.pickingMode = off ? PickingMode.Position : PickingMode.Ignore;
            _fadeTween = DOTween.To(
                () => _fadeScreen.resolvedStyle.opacity,
                alpha =>
                {
                    _currentAlpha = alpha;
                    UpdateFadeVisual(alpha, _currentColor);
                },
                targetAlpha,
                duration
            ).SetEase(Ease.InOutSine);
        }

        private void UpdateFadeVisual(float alpha, Color color)
        {
            if (_fadeScreen == null) return;

            _fadeScreen.style.backgroundColor = color;
            _fadeScreen.style.opacity = alpha;
            _fadeScreen.pickingMode = alpha > 0.05f ? PickingMode.Position : PickingMode.Ignore;
            _fadeScreen.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion
        
    }
}