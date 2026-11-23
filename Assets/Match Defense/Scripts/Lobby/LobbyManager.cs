using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MatchDefense.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [Header("<color=yellow>UI</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private VisualTreeAsset storePageUxml;
        [SerializeField] private VisualTreeAsset inventoryPageUxml;
        [SerializeField] private VisualTreeAsset questPageUxml;
        private int uiVersion = -1;
        private VisualElement _rootElement;
        private Button _playButton;
        private Button _skipButton;
        private Button _settingButton;

        [Header("<color=yellow>Animation</color>")]
        [SerializeField] private Animator stageAnimator;

        [Header("<color=yellow>Stage Rotate</color>")]
        [SerializeField] private Transform objectToRotate;
        [SerializeField] private Transform[] pageTransforms;
        private PageScroll m_PageScroll;

        private void OnEnable()
        {
            InitializeUI();
        }

        private void OnDisable() => DestroyUI();

        private void Start()
        {
            StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            ProgramManager.Instance.FadeScreen(off: true);
            yield return new WaitForSeconds(1f);
            ProgramManager.Instance.FadeScreen(off: false, duration: 1f);
        }


        #region UI

        private void InitializeUI()
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void DestroyUI()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnbindEvents();
        }

        private void OnUIReload(PanelRenderer panel, VisualElement rootElement, int version)
        {
            if (version == uiVersion) return;
            uiVersion = version;

            _rootElement = rootElement;
            _rootElement.style.opacity = 1f;

            _playButton = rootElement.Q<Button>("play-btn");
            _skipButton = rootElement.Q<Button>("skip-btn");
            _settingButton = rootElement.Q<Button>("setting-btn");
            m_PageScroll = rootElement.Q<PageScroll>("sub-stage-scroll");

            UnbindEvents();
            BindEvents();
            InitPages(rootElement);
        }

        private void BindEvents()
        {
            if (_playButton != null)
                _playButton.clicked += OnPlayClicked;
            if (_skipButton != null)
                _skipButton.clicked += OnSkipClicked;
            if (_settingButton != null)
                _settingButton.clicked += () => UIManager.Instance.SwitchLanguage();
            if (m_PageScroll != null)
            {
                m_PageScroll.OnScrollProgressChanged += HandleScrollProgress;
                m_PageScroll.OnPageSettled += HandlePageSettled;
            }
        }

        private void UnbindEvents()
        {
            if (_playButton != null)
                _playButton.clicked -= OnPlayClicked;
            if (_skipButton != null)
                _skipButton.clicked -= OnSkipClicked;
            if (_settingButton != null)
                _settingButton.clicked -= () => UIManager.Instance.SwitchLanguage();
            if (m_PageScroll != null)
            {
                m_PageScroll.OnScrollProgressChanged -= HandleScrollProgress;
                m_PageScroll.OnPageSettled -= HandlePageSettled;
            }
        }

        private IEnumerator FadePanelRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_rootElement != null)
                    _rootElement.style.opacity = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            if (_rootElement != null)
                _rootElement.style.opacity = to;
        }

        #endregion


        #region Play

        private void OnPlayClicked()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            StartCoroutine(PlayRoutine());
        }

        private void OnSkipClicked()
        {
            if (_skipButton != null) _skipButton.clicked -= OnSkipClicked;
            StartCoroutine(SkipAnimation());
        }

        private IEnumerator PlayRoutine()
        {
            stageAnimator.SetTrigger("1-1");
            _playButton.AddToClassList("hide");

            yield return FadePanelRoutine(1f, 0f, 0.5f);
            _skipButton.SetEnabled(true);

            yield return new WaitForSeconds(19f);
            ProgramManager.Instance.FadeScreen(off: true, duration: 1f);
            yield return new WaitForSeconds(1f);
            StopAllCoroutines();
            StartCoroutine(LoadPlaySceneRoutine());
        }

        private IEnumerator SkipAnimation()
        {
            ProgramManager.Instance.FadeScreen(off: true, duration: 1f);
            yield return new WaitForSeconds(1f);
            StopAllCoroutines();
            StartCoroutine(LoadPlaySceneRoutine());
        }

        private IEnumerator LoadPlaySceneRoutine()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("Play");
            while (!sceneLoad.isDone)
                yield return null;
            yield return new WaitForSeconds(1f);
            this.gameObject.SetActive(false);
        }

        #endregion


        #region Stage

        private void HandleScrollProgress(float progress)
        {
            if (objectToRotate == null || pageTransforms == null || pageTransforms.Length == 0) return;

            int startIndex = Mathf.FloorToInt(progress);
            int endIndex = startIndex + 1;

            float t = progress - startIndex;
            startIndex = Mathf.Clamp(startIndex, 0, pageTransforms.Length - 1);
            endIndex = Mathf.Clamp(endIndex, 0, pageTransforms.Length - 1);

            Transform startTrans = pageTransforms[startIndex];
            Transform endTrans = pageTransforms[endIndex];

            objectToRotate.position = Vector3.Lerp(startTrans.position, endTrans.position, t);
            objectToRotate.rotation = Quaternion.Slerp(startTrans.rotation, endTrans.rotation, t);
        }

        private void HandlePageSettled(int pageIndex)
        {
            // Debug.Log($"Stage Page Settled: {pageIndex}");
        }

        #endregion


        #region STORE & INVENTORY & QUEST

        private int _currentPageIndex = -1;
        private VisualElement pagesContainer;
        private VisualElement[] _pages;
        private Button _stageButton;
        private Button _storeButton;
        private Button _inventoryButton;
        private Button _questButton;

        public void InitPages(VisualElement rootElement)
        {
            _currentPageIndex = -1;
            VisualTreeAsset[] pageTemplates = { storePageUxml, inventoryPageUxml, questPageUxml };
            _pages = new VisualElement[pageTemplates.Length];
            pagesContainer = rootElement.Q<VisualElement>("pages");

            for (int i = 0; i < pageTemplates.Length; i++)
            {
                if (pageTemplates[i] == null) continue;

                VisualElement pageInstance = pageTemplates[i].Instantiate();
                pageInstance.AddToClassList("page");
                pageInstance.style.display = DisplayStyle.None;
                pageInstance.SetEnabled(false);
                pageInstance.RegisterCallback<TransitionEndEvent>(evt =>
                {
                    if (!pageInstance.ClassListContains("page--active"))
                    {
                        pageInstance.style.display = DisplayStyle.None;
                        pageInstance.SetEnabled(false);
                    }
                });

                pagesContainer.Add(pageInstance);
                _pages[i] = pageInstance;
            }

            _stageButton = rootElement.Q<Button>("stage-btn");
            _storeButton = rootElement.Q<Button>("store-btn");
            _inventoryButton = rootElement.Q<Button>("inven-btn");
            _questButton = rootElement.Q<Button>("quest-btn");

            if (_stageButton != null) _stageButton.clicked += () => SwitchPage(-1);
            if (_storeButton != null) _storeButton.clicked += () => SwitchPage(0);
            if (_inventoryButton != null) _inventoryButton.clicked += () => SwitchPage(1);
            if (_questButton != null) _questButton.clicked += () => SwitchPage(2);
        }

        public void CleanupPages()
        {
            if (_stageButton != null) _stageButton.clicked -= () => SwitchPage(-1);
            if (_storeButton != null) _storeButton.clicked -= () => SwitchPage(0);
            if (_inventoryButton != null) _inventoryButton.clicked -= () => SwitchPage(1);
            if (_questButton != null) _questButton.clicked -= () => SwitchPage(2);
        }

        public void SwitchPage(int targetIndex)
        {
            if (targetIndex == _currentPageIndex) return;

            if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Length && _pages[_currentPageIndex] != null)
            {
                var prevPage = _pages[_currentPageIndex];
                prevPage.RemoveFromClassList("page--active");
                prevPage.pickingMode = PickingMode.Ignore;
            }

            if (targetIndex >= 0 && targetIndex < _pages.Length && _pages[targetIndex] != null)
            {
                var nextPage = _pages[targetIndex];
                nextPage.style.display = DisplayStyle.Flex;
                nextPage.SetEnabled(true);
                nextPage.pickingMode = PickingMode.Position;
                nextPage.schedule.Execute(() =>
                {
                    nextPage.AddToClassList("page--active");
                });
            }
            _currentPageIndex = targetIndex;
        }

        #endregion
    }
}