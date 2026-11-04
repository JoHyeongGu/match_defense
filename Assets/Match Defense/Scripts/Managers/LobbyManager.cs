using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MatchDefense.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [Header("<color=yellow> References </color>")]
        [SerializeField] private PanelRenderer panelRenderer;

        [Header("<color=yellow> UI : Pages </color>")]
        [SerializeField] private VisualTreeAsset storePageUxml;
        [SerializeField] private VisualTreeAsset inventoryPageUxml;
        [SerializeField] private VisualTreeAsset questPageUxml;

        [Header("<color=yellow> Stage Background </color>")]
        [SerializeField] private Animator stageAnimator;

        private Button _playButton;
        private VisualElement _rootElement;

        private void OnEnable() => InitializeUI();
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

        private void OnUIReload(PanelRenderer panel, VisualElement rootElement)
        {
            _rootElement = rootElement;
            _rootElement.style.opacity = 1f;
            _playButton = rootElement.Q<Button>("play-btn");
            UnbindEvents();
            BindEvents();
            InitPages(rootElement);
        }

        private void BindEvents()
        {
            if (_playButton != null)
                _playButton.clicked += OnPlayClicked;
        }

        private void UnbindEvents()
        {
            if (_playButton != null)
                _playButton.clicked -= OnPlayClicked;
        }

        private void OnPlayClicked()
        {
            UnbindEvents();
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            _playButton.AddToClassList("hide");

            yield return FadePanelRoutine(1f, 0f, 1f);

            stageAnimator.SetTrigger("1-1");

            yield return new WaitForSeconds(12.5f);

            ProgramManager.Instance.FadeScreen(off: true, duration: 1f);
            yield return new WaitForSeconds(1f);

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("Play");
            while (!sceneLoad.isDone)
                yield return null;

            ProgramManager.Instance.FadeScreen(off: false, duration: 1f);
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

        #region UI Page
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
            _inventoryButton = rootElement.Q<Button>("inventory-btn");
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

        #region Stage
        private int _currentStage = 0;

        private IEnumerator GoToNextStageRoutine()
        {
            _currentStage++;
            yield return new WaitForSeconds(1f);
        }
        #endregion
    }
}