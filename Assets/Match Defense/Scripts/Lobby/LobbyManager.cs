using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

namespace MatchDefense.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        [Header("<color=yellow> References </color>")]
        [SerializeField] private PanelRenderer panelRenderer;

        private void OnEnable()
        {
            InitializeUI();
            InitializeStage();
        }

        private void OnDisable()
        {
            DestroyUI();
        }

        private async void Start()
        {
            ProgramManager.Instance.FadeScreen(off: true);
            await Task.Delay(1000);
            ProgramManager.Instance.FadeScreen(off: false, duration: 1f);
        }

        #region UI

        private Button _playButton;

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

        private async void OnPlayClicked()
        {
            _playButton.AddToClassList("hide");
            ProgramManager.Instance.FadeScreen(off: true, duration: 1f);
            await Task.Delay(3000);
            ProgramManager.Instance.FadeScreen(off: false, duration: 1f);
            await Task.Delay(1000);
            await GoToNextStage();
            _playButton.RemoveFromClassList("hide");
        }

        #endregion


        #region UI Page

        [Header("<color=yellow> UI : Pages </color>")]
        [SerializeField] private VisualTreeAsset storePageUxml;
        [SerializeField] private VisualTreeAsset inventoryPageUxml;
        [SerializeField] private VisualTreeAsset questPageUxml;

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

            // Store: 0, Inventory: 1, Quest: 2
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
        [Header("<color=yellow> Stage Background </color>")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform stagePointParent;

        private int _currentStage = 0;
        private Transform[] _stagePoints;

        private void InitializeStage()
        {
            _stagePoints = new Transform[stagePointParent.childCount];
            foreach (Transform child in stagePointParent)
                _stagePoints[child.GetSiblingIndex()] = child;
        }

        private async Task GoToNextStage()
        {
            _currentStage++;
            if (_currentStage >= _stagePoints.Length)
                _currentStage = 0;

            await player.DOMove(_stagePoints[_currentStage].position, 1f).SetEase(Ease.InOutSine).AsyncWaitForCompletion();
        }

        #endregion
    }
}