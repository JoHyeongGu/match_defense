using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DG.Tweening;
using MatchDefense.UI;
using MatchDefense.Tutorial;

namespace MatchDefense.Lobby
{
    [Serializable]
    public class LobbySubStageVisual
    {
        public Vector3 stageObjectPosition;
        public Vector3 stageObjectEulerAngles;
        public string loopAnimationStateName;
        public string enterAnimationTrigger;
        public float enterAnimationDuration = 3.0f;
    }

    [Serializable]
    public class LobbyMainStageVisual
    {
        public GameObject stageObject;
        public Color backgroundColor = new Color(0.95f, 0.86f, 0.75f, 1f);
        public List<LobbySubStageVisual> subStages = new List<LobbySubStageVisual>();
    }

    public class LobbyManager : MonoBehaviour
    {
        [Header("<color=yellow>UI</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private VisualTreeAsset storePageUxml;
        [SerializeField] private VisualTreeAsset inventoryPageUxml;
        [SerializeField] private VisualTreeAsset questPageUxml;
        [SerializeField] private VisualTreeAsset settingsPopupUxml;

        [Header("<color=yellow>Lobby Stages</color>")]
        [SerializeField] private Camera lobbyCamera;
        [SerializeField] private List<LobbyMainStageVisual> lobbyStages = new List<LobbyMainStageVisual>();

        [Header("<color=yellow>Transition</color>")]
        [SerializeField] private float loopCrossFadeDuration = 0.35f;
        [SerializeField] private float worldSlideDistance = 22f;
        [SerializeField] private float worldSlideDuration = 0.75f;

        [Header("<color=yellow>Tutorial Gacha</color>")]
        [SerializeField] private string tutorialGuaranteedUnitId = "baguette";
        [SerializeField] private bool freeGachaDuringTutorial = true;

        private int uiVersion = -1;
        private VisualElement _rootElement;
        private LocalizedLabel _stageTitleLabel;
        private VisualElement _coinPill;
        private Label _coinLabel;
        private Button _playButton;
        private Button _skipButton;
        private Button _settingButton;
        private PageScroll m_PageScroll;

        private int _selectedMainStageIndex = 0;
        private int _selectedSubStageIndex = 0;
        private int _lastPreviewSubStageIndex = -1;
        private bool _isEnteringStage = false;
        private bool _isTransitioningWorld = false;
        private bool _isStageAreaDragging = false;
        private int _stageAreaPointerId = -1;
        private Coroutine _playCoroutine;
        private string _currentLoopStateName = string.Empty;

        private Transform _activeStageTransform;
        private Animator _activeStageAnimator;
        private readonly Dictionary<int, GameObject> _spawnedStageObjects = new Dictionary<int, GameObject>();

        private SettingsPopupController _settingsPopup;
        private LobbyStoreController _storeController;
        private LobbyInventoryController _inventoryController;
        private LobbyQuestController _questController;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (lobbyCamera == null) lobbyCamera = Camera.main;

            _settingsPopup = new SettingsPopupController(isPlaySceneMode: false);
            _storeController = new LobbyStoreController(this, tutorialGuaranteedUnitId, freeGachaDuringTutorial);
            _inventoryController = new LobbyInventoryController();
            _questController = new LobbyQuestController();
        }

        private void OnEnable()
        {
            InitializeUI();
            if (ProgramManager.Instance != null)
            {
                ProgramManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                ProgramManager.Instance.OnStageProgressChanged += RefreshStageSelectItems;
            }
        }

        private void OnDisable()
        {
            DestroyUI();
            if (ProgramManager.Instance != null)
            {
                ProgramManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                ProgramManager.Instance.OnStageProgressChanged -= RefreshStageSelectItems;
            }
        }

        private void Start()
        {
            StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            Time.timeScale = 1f;

            if (_rootElement != null)
                _rootElement.style.opacity = 1f;

            if (ProgramManager.Instance != null)
            {
                bool hasWorldTransition = ProgramManager.Instance.ConsumePendingWorldTransition(out int fromMain, out int toMain);
                if (hasWorldTransition)
                {
                    _selectedMainStageIndex = fromMain;
                    _selectedSubStageIndex = 0;
                    ActivateMainStageInstant(fromMain);
                }
                else
                {
                    _selectedMainStageIndex = ProgramManager.Instance.CurrentMainStageIndex;
                    _selectedSubStageIndex = ProgramManager.Instance.CurrentSubStageIndex;
                    ActivateMainStageInstant(_selectedMainStageIndex);
                }

                RefreshStageSelectItems();
                ApplyStageTransformForProgress(_selectedSubStageIndex);
                UpdatePlayButtonImmediate(_selectedSubStageIndex);

                ProgramManager.Instance.FadeScreen(off: true, duration: 0f);
                yield return new WaitForSecondsRealtime(0.25f);
                ProgramManager.Instance.FadeScreen(off: false, duration: 0.6f);

                if (hasWorldTransition)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    yield return TransitionMainStageWorldRoutine(fromMain, toMain, targetSubStage: 0);
                }
                else
                {
                    int pendingSub = ProgramManager.Instance.ConsumePendingLobbySubStageIndex();
                    if (pendingSub >= 0 && m_PageScroll != null)
                    {
                        yield return new WaitForSecondsRealtime(0.2f);
                        m_PageScroll.GoToPage(pendingSub, animate: true);
                    }
                }

                bool runLobbyTutorial = ProgramManager.Instance.ConsumePendingLobbyTutorial();
                if (runLobbyTutorial && TutorialManager.Instance != null)
                {
                    yield return new WaitForSecondsRealtime(0.45f);
                    TutorialManager.Instance.TryStartTutorial("lobby_gacha_equip");
                }
            }
            else
            {
                ActivateMainStageInstant(_selectedMainStageIndex);
                ApplyStageTransformForProgress(_selectedSubStageIndex);
            }
        }

        #region Lobby Stage Visuals & World Transition
        private LobbyMainStageVisual GetMainVisual(int mainIndex)
        {
            if (lobbyStages == null || lobbyStages.Count == 0) return null;
            return lobbyStages[Mathf.Clamp(mainIndex, 0, lobbyStages.Count - 1)];
        }

        private LobbySubStageVisual GetSubVisual(int mainIndex, int subIndex)
        {
            LobbyMainStageVisual main = GetMainVisual(mainIndex);
            if (main == null || main.subStages == null || main.subStages.Count == 0) return null;
            return main.subStages[Mathf.Clamp(subIndex, 0, main.subStages.Count - 1)];
        }

        private GameObject ResolveOrSpawnMainStageObject(int mainIndex)
        {
            if (_spawnedStageObjects.TryGetValue(mainIndex, out GameObject cached) && cached != null)
                return cached;

            LobbyMainStageVisual mainVisual = GetMainVisual(mainIndex);
            GameObject sourceObj = mainVisual?.stageObject;
            if (sourceObj == null) return null;

            GameObject instance = sourceObj.scene.IsValid()
                ? sourceObj
                : Instantiate(sourceObj);

            _spawnedStageObjects[mainIndex] = instance;
            return instance;
        }

        private void BindActiveStageObject(GameObject stageObj)
        {
            _activeStageTransform = stageObj != null ? stageObj.transform : null;
            _activeStageAnimator = stageObj != null ? stageObj.GetComponentInChildren<Animator>(true) : null;
        }

        private void ActivateMainStageInstant(int mainIndex)
        {
            if (lobbyStages != null)
            {
                for (int i = 0; i < lobbyStages.Count; i++)
                {
                    GameObject obj = lobbyStages[i]?.stageObject;
                    if (obj != null && obj.scene.IsValid())
                        obj.SetActive(i == mainIndex);
                }
            }

            foreach (var kv in _spawnedStageObjects)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(kv.Key == mainIndex);
            }

            GameObject activeObj = ResolveOrSpawnMainStageObject(mainIndex);
            if (activeObj != null) activeObj.SetActive(true);
            BindActiveStageObject(activeObj);

            LobbyMainStageVisual mainVisual = GetMainVisual(mainIndex);
            if (lobbyCamera != null && mainVisual != null)
                lobbyCamera.backgroundColor = mainVisual.backgroundColor;

            MainStageConfig mainCfg = ProgramManager.Instance?.GetMainStageConfig(mainIndex);
            if (_stageTitleLabel != null && mainCfg != null && !string.IsNullOrEmpty(mainCfg.titleLocalizationKey))
                _stageTitleLabel.LocalizationKey = mainCfg.titleLocalizationKey;

            _currentLoopStateName = string.Empty;
        }

        public void SwitchMainStageWorld(int targetMainIndex, int targetSubStage = 0)
        {
            if (_isTransitioningWorld || _isEnteringStage) return;
            if (ProgramManager.Instance != null && !ProgramManager.Instance.IsMainStageUnlocked(targetMainIndex)) return;
            if (targetMainIndex == _selectedMainStageIndex) return;

            StartCoroutine(TransitionMainStageWorldRoutine(_selectedMainStageIndex, targetMainIndex, targetSubStage));
        }

        private IEnumerator TransitionMainStageWorldRoutine(int fromMain, int toMain, int targetSubStage)
        {
            _isTransitioningWorld = true;
            UpdatePlayButtonImmediate(targetSubStage);

            float dir = toMain > fromMain ? 1f : -1f;
            GameObject fromObj = ResolveOrSpawnMainStageObject(fromMain);
            GameObject toObj = ResolveOrSpawnMainStageObject(toMain);
            LobbyMainStageVisual nextVisual = GetMainVisual(toMain);
            LobbySubStageVisual nextSubVisual = GetSubVisual(toMain, targetSubStage);

            Vector3 targetPos = nextSubVisual != null ? nextSubVisual.stageObjectPosition : Vector3.zero;
            Quaternion targetRot = nextSubVisual != null ? Quaternion.Euler(nextSubVisual.stageObjectEulerAngles) : Quaternion.identity;

            Sequence seq = DOTween.Sequence().SetUpdate(true);

            if (fromObj != null)
            {
                Vector3 fromPos = fromObj.transform.position;
                seq.Join(fromObj.transform.DOMoveX(fromPos.x - dir * worldSlideDistance, worldSlideDuration).SetEase(Ease.InOutCubic));
            }

            if (toObj != null && toObj != fromObj)
            {
                toObj.transform.position = new Vector3(targetPos.x + dir * worldSlideDistance, targetPos.y, targetPos.z);
                toObj.transform.rotation = targetRot;
                toObj.SetActive(true);
                seq.Join(toObj.transform.DOMoveX(targetPos.x, worldSlideDuration).SetEase(Ease.OutCubic));
            }

            if (lobbyCamera != null && nextVisual != null)
                seq.Join(lobbyCamera.DOColor(nextVisual.backgroundColor, worldSlideDuration).SetEase(Ease.InOutSine));

            yield return seq.WaitForCompletion();

            if (fromObj != null && fromObj != toObj)
                fromObj.SetActive(false);

            _selectedMainStageIndex = toMain;
            _selectedSubStageIndex = targetSubStage;
            _currentLoopStateName = string.Empty;
            BindActiveStageObject(toObj);
            ProgramManager.Instance?.SelectStage(_selectedMainStageIndex, _selectedSubStageIndex);

            RefreshStageSelectItems();
            if (m_PageScroll != null)
                m_PageScroll.GoToPage(targetSubStage, animate: false);

            ApplyStageTransformForProgress(targetSubStage);
            _isTransitioningWorld = false;
            HandlePageSettled(targetSubStage);
        }
        #endregion

        #region UI
        private void InitializeUI()
        {
            if (panelRenderer != null)
                panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void DestroyUI()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            UnbindEvents();
            CleanupPages();
            _settingsPopup?.Unbind();
            _storeController?.Unbind();
            _inventoryController?.Unbind();
            _questController?.Unbind();
        }

        private void OnUIReload(PanelRenderer panel, VisualElement rootElement, int version)
        {
            if (version == uiVersion && _rootElement != null) return;
            uiVersion = version;

            UnbindEvents();
            CleanupPages();

            _isEnteringStage = false;
            _rootElement = rootElement;
            _rootElement.style.opacity = 1f;

            _stageTitleLabel = rootElement.Q<LocalizedLabel>("stage-title");
            _coinPill = rootElement.Q<VisualElement>("coin-status");
            _coinLabel = rootElement.Q<Label>("coin-label");
            CoinFlyEffect.ApplyCoinSprite(rootElement.Q<VisualElement>("coin-icon"), rootElement.Q<VisualElement>("coin-icon")?.Q<Label>());
            _playButton = rootElement.Q<Button>("play-btn");
            _skipButton = rootElement.Q<Button>("skip-btn");
            _settingButton = rootElement.Q<Button>("setting-btn");
            m_PageScroll = rootElement.Q<PageScroll>("sub-stage-scroll");

            _playButton?.RemoveFromClassList("hide");
            if (_skipButton != null)
            {
                _skipButton.SetEnabled(false);
                _skipButton.pickingMode = PickingMode.Ignore;
            }

            BindEvents();
            InitPages(rootElement);
            _settingsPopup?.Mount(rootElement, settingsPopupUxml);

            RefreshCoinUI();
            RefreshStageSelectItems();
            HandlePageSettled(_selectedSubStageIndex);
        }

        private void BindEvents()
        {
            if (_playButton != null)
                _playButton.clicked += OnPlayClicked;
            if (_skipButton != null)
                _skipButton.clicked += OnSkipClicked;
            if (_settingButton != null)
                _settingButton.clicked += OnSettingClicked;
            if (m_PageScroll != null)
            {
                m_PageScroll.OnScrollProgressChanged += HandleScrollProgress;
                m_PageScroll.OnPageSettled += HandlePageSettled;
            }
            if (_rootElement != null)
            {
                _rootElement.RegisterCallback<PointerDownEvent>(OnStageAreaPointerDown);
                _rootElement.RegisterCallback<PointerMoveEvent>(OnStageAreaPointerMove);
                _rootElement.RegisterCallback<PointerUpEvent>(OnStageAreaPointerUp);
                _rootElement.RegisterCallback<PointerCancelEvent>(OnStageAreaPointerCancel);
            }
        }

        private void UnbindEvents()
        {
            if (_playButton != null)
                _playButton.clicked -= OnPlayClicked;
            if (_skipButton != null)
                _skipButton.clicked -= OnSkipClicked;
            if (_settingButton != null)
                _settingButton.clicked -= OnSettingClicked;
            if (m_PageScroll != null)
            {
                m_PageScroll.OnScrollProgressChanged -= HandleScrollProgress;
                m_PageScroll.OnPageSettled -= HandlePageSettled;
            }
            if (_rootElement != null)
            {
                _rootElement.UnregisterCallback<PointerDownEvent>(OnStageAreaPointerDown);
                _rootElement.UnregisterCallback<PointerMoveEvent>(OnStageAreaPointerMove);
                _rootElement.UnregisterCallback<PointerUpEvent>(OnStageAreaPointerUp);
                _rootElement.UnregisterCallback<PointerCancelEvent>(OnStageAreaPointerCancel);
            }
            _isStageAreaDragging = false;
            _stageAreaPointerId = -1;
        }

        private void OnSettingClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            _settingsPopup?.Open();
        }

        private void HandleCoinsChanged(int coins) => RefreshCoinUI();

        private void RefreshCoinUI()
        {
            if (_coinLabel == null || ProgramManager.Instance == null) return;
            _coinLabel.text = $"{ProgramManager.Instance.Coins:N0}";
        }

        private void RefreshStageSelectItems()
        {
            if (m_PageScroll == null || ProgramManager.Instance == null) return;

            MainStageConfig mainCfg = ProgramManager.Instance.GetMainStageConfig(_selectedMainStageIndex);
            if (_stageTitleLabel != null && mainCfg != null && !string.IsNullOrEmpty(mainCfg.titleLocalizationKey))
                _stageTitleLabel.LocalizationKey = mainCfg.titleLocalizationKey;

            List<StageSelect> stageItems = m_PageScroll.Query<StageSelect>().ToList();
            for (int i = 0; i < stageItems.Count; i++)
            {
                stageItems[i].FilledCount = ProgramManager.Instance.GetSubStageStars(_selectedMainStageIndex, i);
                bool unlocked = ProgramManager.Instance.IsSubStageUnlocked(_selectedMainStageIndex, i);
                stageItems[i].style.opacity = unlocked ? 1f : 0.45f;

                SubStageConfig subCfg = ProgramManager.Instance.GetSubStageConfig(_selectedMainStageIndex, i);
                if (subCfg != null && !string.IsNullOrEmpty(subCfg.subTitleLocalizationKey))
                {
                    LocalizedLabel subLabel = stageItems[i].Q<LocalizedLabel>();
                    if (subLabel != null)
                        subLabel.LocalizationKey = subCfg.subTitleLocalizationKey;
                }
            }

            UpdatePlayButtonImmediate(_selectedSubStageIndex);
        }

        private void UpdatePlayButtonImmediate(int subStageIndex)
        {
            if (_playButton == null) return;
            bool unlocked = ProgramManager.Instance == null
                || ProgramManager.Instance.IsSubStageUnlocked(_selectedMainStageIndex, subStageIndex);
            bool canPlay = unlocked && !_isEnteringStage && !_isTransitioningWorld;

            _playButton.SetEnabled(canPlay);
            _playButton.EnableInClassList("play-btn--locked", !unlocked);
        }

        private IEnumerator FadePanelRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_rootElement != null)
                    _rootElement.style.opacity = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            if (_rootElement != null)
                _rootElement.style.opacity = to;
        }
        #endregion

        #region Play & Stage Enter Animation
        private void OnPlayClicked()
        {
            if (_isEnteringStage || _isTransitioningWorld) return;
            if (ProgramManager.Instance != null && !ProgramManager.Instance.IsSubStageUnlocked(_selectedMainStageIndex, _selectedSubStageIndex))
                return;

            _isEnteringStage = true;
            UpdatePlayButtonImmediate(_selectedSubStageIndex);

            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            _playCoroutine = StartCoroutine(PlayRoutine(_selectedMainStageIndex, _selectedSubStageIndex));
        }

        private void OnSkipClicked()
        {
            if (!_isEnteringStage) return;
            if (_skipButton != null)
            {
                _skipButton.SetEnabled(false);
                _skipButton.pickingMode = PickingMode.Ignore;
            }

            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            StartCoroutine(SkipAnimationRoutine(_selectedMainStageIndex, _selectedSubStageIndex));
        }

        private IEnumerator PlayRoutine(int mainIndex, int subIndex)
        {
            SubStageConfig subCfg = ProgramManager.Instance?.GetSubStageConfig(mainIndex, subIndex);
            LobbySubStageVisual subVisual = GetSubVisual(mainIndex, subIndex);
            ProgramManager.Instance?.SelectStage(mainIndex, subIndex);

            if (_activeStageAnimator != null && subVisual != null && !string.IsNullOrEmpty(subVisual.enterAnimationTrigger))
                _activeStageAnimator.SetTrigger(subVisual.enterAnimationTrigger);

            _playButton?.AddToClassList("hide");

            yield return FadePanelRoutine(1f, 0f, 0.5f);
            if (_skipButton != null)
            {
                _skipButton.SetEnabled(true);
                _skipButton.pickingMode = PickingMode.Position;
            }

            float waitDuration = subVisual != null ? Mathf.Max(0.2f, subVisual.enterAnimationDuration) : 3.0f;
            yield return new WaitForSecondsRealtime(waitDuration);

            yield return LoadTargetStageSceneRoutine(subCfg);
        }

        private IEnumerator SkipAnimationRoutine(int mainIndex, int subIndex)
        {
            SubStageConfig subCfg = ProgramManager.Instance?.GetSubStageConfig(mainIndex, subIndex);
            yield return LoadTargetStageSceneRoutine(subCfg);
        }

        private IEnumerator LoadTargetStageSceneRoutine(SubStageConfig subCfg)
        {
            ProgramManager.Instance?.FadeScreen(off: true, duration: 0.6f);
            yield return new WaitForSecondsRealtime(0.65f);

            string targetScene = (subCfg != null && !string.IsNullOrEmpty(subCfg.sceneName)) ? subCfg.sceneName : "Play";

            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogWarning($"[LobbyManager] Scene '{targetScene}' is not in Build Settings. Falling back to 'Play'.");
                targetScene = "Play";
            }

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(targetScene);
            while (sceneLoad != null && !sceneLoad.isDone)
                yield return null;
        }
        #endregion

        #region Stage Drag, Rotate & Loop Animation
        private void OnStageAreaPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _isEnteringStage || _isTransitioningWorld || _currentPageIndex >= 0) return;
            if (m_PageScroll == null || evt.target != _rootElement) return;

            _isStageAreaDragging = true;
            _stageAreaPointerId = evt.pointerId;
            _rootElement.CapturePointer(evt.pointerId);
            m_PageScroll.BeginExternalDrag(evt.position.x);
        }

        private void OnStageAreaPointerMove(PointerMoveEvent evt)
        {
            if (!_isStageAreaDragging || evt.pointerId != _stageAreaPointerId) return;
            m_PageScroll?.UpdateExternalDrag(evt.position.x);
        }

        private void OnStageAreaPointerUp(PointerUpEvent evt)
        {
            if (!_isStageAreaDragging || evt.pointerId != _stageAreaPointerId) return;
            _isStageAreaDragging = false;
            if (_rootElement != null && _rootElement.HasPointerCapture(evt.pointerId))
                _rootElement.ReleasePointer(evt.pointerId);
            _stageAreaPointerId = -1;
            m_PageScroll?.EndExternalDrag(evt.position.x);
        }

        private void OnStageAreaPointerCancel(PointerCancelEvent evt)
        {
            if (!_isStageAreaDragging || evt.pointerId != _stageAreaPointerId) return;
            _isStageAreaDragging = false;
            if (_rootElement != null && _rootElement.HasPointerCapture(evt.pointerId))
                _rootElement.ReleasePointer(evt.pointerId);
            _stageAreaPointerId = -1;
            m_PageScroll?.EndExternalDrag(evt.position.x);
        }

        private void HandleScrollProgress(float progress)
        {
            int nearestSub = Mathf.Max(0, Mathf.RoundToInt(progress));
            if (nearestSub != _lastPreviewSubStageIndex)
            {
                _lastPreviewSubStageIndex = nearestSub;
                UpdatePlayButtonImmediate(nearestSub);
            }

            if (_isTransitioningWorld) return;
            ApplyStageTransformForProgress(progress);
        }

        private void ApplyStageTransformForProgress(float progress)
        {
            if (_activeStageTransform == null) return;

            LobbyMainStageVisual mainVisual = GetMainVisual(_selectedMainStageIndex);
            if (mainVisual == null || mainVisual.subStages == null || mainVisual.subStages.Count == 0) return;

            int count = mainVisual.subStages.Count;
            int startIndex = Mathf.Clamp(Mathf.FloorToInt(progress), 0, count - 1);
            int endIndex = Mathf.Clamp(startIndex + 1, 0, count - 1);
            float t = Mathf.Clamp01(progress - startIndex);

            LobbySubStageVisual startVisual = mainVisual.subStages[startIndex];
            LobbySubStageVisual endVisual = mainVisual.subStages[endIndex];
            if (startVisual == null || endVisual == null) return;

            _activeStageTransform.position = Vector3.Lerp(startVisual.stageObjectPosition, endVisual.stageObjectPosition, t);
            _activeStageTransform.rotation = Quaternion.Slerp(
                Quaternion.Euler(startVisual.stageObjectEulerAngles),
                Quaternion.Euler(endVisual.stageObjectEulerAngles),
                t
            );
        }

        private void HandlePageSettled(int pageIndex)
        {
            _selectedSubStageIndex = Mathf.Max(0, pageIndex);
            _lastPreviewSubStageIndex = _selectedSubStageIndex;
            ProgramManager.Instance?.SelectStage(_selectedMainStageIndex, _selectedSubStageIndex);

            LobbySubStageVisual subVisual = GetSubVisual(_selectedMainStageIndex, _selectedSubStageIndex);
            if (subVisual != null)
                PlayStageLoopAnimation(subVisual.loopAnimationStateName);

            UpdatePlayButtonImmediate(_selectedSubStageIndex);
        }

        private void PlayStageLoopAnimation(string stateName)
        {
            if (_activeStageAnimator == null || string.IsNullOrEmpty(stateName)) return;
            if (_currentLoopStateName == stateName) return;

            int stateHash = Animator.StringToHash(stateName);
            if (_activeStageAnimator.HasState(0, stateHash))
            {
                _currentLoopStateName = stateName;
                _activeStageAnimator.CrossFadeInFixedTime(stateHash, loopCrossFadeDuration, 0);
            }
        }
        #endregion

        #region Store, Inventory & Quest
        private int _currentPageIndex = -1;
        private VisualElement pagesContainer;
        private VisualElement[] _pages;
        private Button _stageButton;
        private Button _storeButton;
        private Button _inventoryButton;
        private Button _questButton;

        private void OnStageTabClicked() => SwitchPage(-1);
        private void OnStoreTabClicked() => SwitchPage(0);
        private void OnInventoryTabClicked() => SwitchPage(1);
        private void OnQuestTabClicked() => SwitchPage(2);

        public void InitPages(VisualElement rootElement)
        {
            _currentPageIndex = -1;
            VisualTreeAsset[] pageTemplates = { storePageUxml, inventoryPageUxml, questPageUxml };
            _pages = new VisualElement[pageTemplates.Length];
            pagesContainer = rootElement.Q<VisualElement>("pages");
            pagesContainer?.Clear();

            for (int i = 0; i < pageTemplates.Length; i++)
            {
                if (pageTemplates[i] == null || pagesContainer == null) continue;

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

            if (_pages.Length > 0 && _pages[0] != null)
                _storeController?.Bind(_pages[0]);

            if (_pages.Length > 1 && _pages[1] != null)
                _inventoryController?.Bind(_pages[1]);

            if (_pages.Length > 2 && _pages[2] != null)
                _questController?.Bind(_pages[2], _rootElement, _coinPill, _coinLabel);

            _stageButton = rootElement.Q<Button>("stage-btn");
            _storeButton = rootElement.Q<Button>("store-btn");
            _inventoryButton = rootElement.Q<Button>("inven-btn");
            _questButton = rootElement.Q<Button>("quest-btn");

            if (_stageButton != null) _stageButton.clicked += OnStageTabClicked;
            if (_storeButton != null) _storeButton.clicked += OnStoreTabClicked;
            if (_inventoryButton != null) _inventoryButton.clicked += OnInventoryTabClicked;
            if (_questButton != null) _questButton.clicked += OnQuestTabClicked;
        }

        public void CleanupPages()
        {
            if (_stageButton != null) _stageButton.clicked -= OnStageTabClicked;
            if (_storeButton != null) _storeButton.clicked -= OnStoreTabClicked;
            if (_inventoryButton != null) _inventoryButton.clicked -= OnInventoryTabClicked;
            if (_questButton != null) _questButton.clicked -= OnQuestTabClicked;
        }

        public void SwitchPage(int targetIndex)
        {
            if (targetIndex == _currentPageIndex) return;
            SoundManager.Instance?.PlaySFX(SfxType.TabSwitch);

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

                if (targetIndex == 0) _storeController?.RefreshUI();
                else if (targetIndex == 1) _inventoryController?.RefreshAll();
                else if (targetIndex == 2) _questController?.RefreshAll();
            }

            _currentPageIndex = targetIndex;
        }
        #endregion
    }
}
