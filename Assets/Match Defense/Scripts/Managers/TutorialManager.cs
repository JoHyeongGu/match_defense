using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MatchDefense.Defense;
using MatchDefense.Match;

namespace MatchDefense.Tutorial
{
    public enum TutorialHighlightType
    {
        None,
        UIElement,
        WorldEnemy,
        WorldTransform,
        UnitSlotAndField
    }

    public enum TutorialCompletionTrigger
    {
        TapAnywhere,
        TargetButtonClick,
        MatchCompleted,
        UnitPlaced,
        WaveStartPressed,
        TutorialEnemyKilled,
        GachaDrawn,
        UnitEquipped,
        QuestClaimed
    }

    public enum TutorialDialogPosition
    {
        Top,
        Center,
        Bottom
    }

    [Serializable]
    public class TutorialStep
    {
        public string stepId = "step_1";
        [Tooltip("Localization key from UIManager CSV")]
        public string messageLocalizationKey = "tut_s1_welcome";

        [Header("Highlight (Spotlight)")]
        public TutorialHighlightType highlightType = TutorialHighlightType.None;
        [Tooltip("Target UXML element name when highlightType is UIElement")]
        public string targetElementName;
        [Tooltip("Padding around the highlight frame (px)")]
        public float highlightPadding = 16f;
        [Tooltip("Spotlight rectangle size (px) for WorldEnemy/WorldTransform")]
        public Vector2 worldSpotlightSize = new Vector2(260f, 260f);
        public Transform targetWorldTransform;

        [Header("Completion Condition")]
        public TutorialCompletionTrigger completionTrigger = TutorialCompletionTrigger.TapAnywhere;
        [Tooltip("Required count for MatchCompleted or UnitPlaced trigger")]
        public int requiredCount = 1;

        [Header("Gameplay Hooks")]
        [Tooltip("Ensure at least 1 unit in slot 0 when entering this step")]
        public bool grantTutorialUnitOnEnter = false;
        [Tooltip("Tutorial enemy prefab to spawn on step enter (falls back to EnemySpawner default)")]
        public GameObject spawnTutorialEnemyPrefab;
        [Tooltip("Pause gameplay while this tutorial step is active")]
        public bool pauseGameDuringStep = false;
        [Tooltip("Game speed multiplier during this step (WatchEnemy defaults to 2.5x if <= 1)")]
        [Min(1f)] public float stepTimeScale = 1f;
        [Tooltip("Tutorial dialog position")]
        public TutorialDialogPosition dialogPosition = TutorialDialogPosition.Top;
    }

    [Serializable]
    public class EnemyIntroTrigger
    {
        public string introTutorialId = "stage_2_new_enemy";
        public string targetEnemyId = "enemy_runner";
        public string messageLocalizationKey = "tut_s2_enemy_runner";
        public Vector2 spotlightSize = new Vector2(260f, 260f);
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("<color=yellow>References</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private VisualTreeAsset tutorialOverlayUxml;
        [SerializeField] private Camera worldCamera;

        [Header("<color=yellow>Tutorial Config</color>")]
        [SerializeField] private string tutorialId = "stage_1_intro";
        [SerializeField] private bool autoStartOnSceneLoad = true;
        [SerializeField] private bool canSkip = true;
        [SerializeField] private bool onlyRunIfNotCompleted = true;
        [SerializeField] private float defaultWatchEnemyTimeScale = 2.5f;
        [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

        [Header("<color=yellow>Enemy First-Appear Intro (Stage 2+)</color>")]
        [SerializeField] private List<EnemyIntroTrigger> enemyIntroTriggers = new List<EnemyIntroTrigger>();

        public bool IsTutorialRunning { get; private set; }
        public TutorialStep CurrentStep => (IsTutorialRunning && _currentStepIndex >= 0 && _currentStepIndex < steps.Count)
            ? steps[_currentStepIndex]
            : null;

        public event Action OnTutorialStarted;
        public event Action OnTutorialFinished;

        private int _uiVersion = -1;
        private VisualElement _root;
        private VisualElement _overlayRoot;
        private VisualElement _dimTop;
        private VisualElement _dimBottom;
        private VisualElement _dimLeft;
        private VisualElement _dimRight;
        private VisualElement _spotlightFrame;
        private VisualElement _dialogBox;
        private Label _dialogText;
        private Label _tapHintLabel;
        private Button _skipButton;

        private int _currentStepIndex = -1;
        private int _currentProgressCount = 0;
        private Enemy _trackedEnemy;
        private Button _hookedTargetButton;
        private Toggle _hookedTargetToggle;
        private bool _isEnemyIntroPopupActive;
        private EnemyIntroTrigger _activeEnemyIntro;
        private bool _isRegisteredToPanel;
        private bool _hasAttemptedAutoStart;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            if (worldCamera == null) worldCamera = Camera.main;
            ResolvePanelRenderer();
        }

        private void OnEnable()
        {
            RegisterPanelCallback();

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged += RefreshLocalizedText;

            SubscribeGameplayEvents();
        }

        private void OnDisable()
        {
            UnregisterPanelCallback();

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged -= RefreshLocalizedText;

            UnsubscribeGameplayEvents();
            UnhookCurrentTarget();

            GameManager.Instance?.SetTutorialTimeScale(1f);

            if (MatchManager.Instance != null && MatchManager.Instance.InterceptWaveStart == HandleWaveStartIntercept)
                MatchManager.Instance.InterceptWaveStart = null;
        }

        private void Start()
        {
            ResolvePanelRenderer();
            RegisterPanelCallback();
            SubscribeGameplayEvents();

            if (autoStartOnSceneLoad && !_hasAttemptedAutoStart)
            {
                _hasAttemptedAutoStart = true;
                TryStartTutorial();
            }
        }

        private void ResolvePanelRenderer()
        {
            if (panelRenderer != null && panelRenderer.GetComponent<ProgramManager>() == null)
                return;

            panelRenderer = null;

            PanelRenderer[] allPanels = FindObjectsByType<PanelRenderer>();
            for (int i = 0; i < allPanels.Length; i++)
            {
                PanelRenderer candidate = allPanels[i];
                if (candidate == null) continue;
                if (candidate.GetComponent<ProgramManager>() != null) continue;

                panelRenderer = candidate;
                return;
            }
        }

        private void RegisterPanelCallback()
        {
            if (_isRegisteredToPanel || panelRenderer == null) return;
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
            _isRegisteredToPanel = true;
        }

        private void UnregisterPanelCallback()
        {
            if (!_isRegisteredToPanel || panelRenderer == null) return;
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            _isRegisteredToPanel = false;
        }

        private void Update()
        {
            if (!IsTutorialRunning && !_isEnemyIntroPopupActive) return;
            if (_overlayRoot == null) return;

            if (_isEnemyIntroPopupActive && _trackedEnemy != null)
            {
                Vector2 sz = _activeEnemyIntro.spotlightSize.sqrMagnitude > 1f
                    ? _activeEnemyIntro.spotlightSize
                    : new Vector2(260f, 260f);
                UpdateWorldSpotlight(_trackedEnemy.transform.position, sz);
                return;
            }

            TutorialStep step = CurrentStep;
            if (step == null) return;

            if (ShouldHighlightSlotAndField(step))
            {
                UpdateSlotAndFieldSpotlight(step.targetElementName, step.highlightPadding > 0f ? step.highlightPadding : 16f);
            }
            else if (step.highlightType == TutorialHighlightType.UIElement)
            {
                UpdateUISpotlight(step.targetElementName, step.highlightPadding > 0f ? step.highlightPadding : 16f);
            }
            else if (step.highlightType == TutorialHighlightType.WorldEnemy && _trackedEnemy != null)
            {
                Vector2 sz = step.worldSpotlightSize.sqrMagnitude > 1f ? step.worldSpotlightSize : new Vector2(260f, 260f);
                UpdateWorldSpotlight(_trackedEnemy.transform.position, sz);
            }
            else if (step.highlightType == TutorialHighlightType.WorldTransform && step.targetWorldTransform != null)
            {
                Vector2 sz = step.worldSpotlightSize.sqrMagnitude > 1f ? step.worldSpotlightSize : new Vector2(260f, 260f);
                UpdateWorldSpotlight(step.targetWorldTransform.position, sz);
            }
        }

        private static bool ShouldHighlightSlotAndField(TutorialStep step)
        {
            if (step == null) return false;
            return step.highlightType == TutorialHighlightType.UnitSlotAndField
                || step.completionTrigger == TutorialCompletionTrigger.UnitPlaced;
        }

        #region UI Setup
        private void OnUIReload(PanelRenderer panel, VisualElement rootElement, int version)
        {
            if (rootElement == null) return;
            if (version == _uiVersion && _overlayRoot != null && _overlayRoot.panel != null) return;

            _uiVersion = version;
            _root = rootElement;
            _overlayRoot = null;

            MountOverlay();

            if (IsTutorialRunning)
                ApplyCurrentStepVisuals();
        }

        private void MountOverlay()
        {
            if (_root == null) return;

            _overlayRoot = _root.Q<VisualElement>("tutorial-overlay");
            if (_overlayRoot == null)
            {
                if (tutorialOverlayUxml != null)
                {
                    TemplateContainer container = tutorialOverlayUxml.Instantiate();
                    container.style.position = Position.Absolute;
                    container.style.left = 0;
                    container.style.right = 0;
                    container.style.top = 0;
                    container.style.bottom = 0;
                    container.pickingMode = PickingMode.Ignore;
                    _root.Add(container);
                    _overlayRoot = container.Q<VisualElement>("tutorial-overlay");
                }
                else
                {
                    _overlayRoot = BuildFallbackOverlay();
                    _root.Add(_overlayRoot);
                }
            }

            if (_overlayRoot == null) return;

            _dimTop = _overlayRoot.Q<VisualElement>("tut-dim-top");
            _dimBottom = _overlayRoot.Q<VisualElement>("tut-dim-bottom");
            _dimLeft = _overlayRoot.Q<VisualElement>("tut-dim-left");
            _dimRight = _overlayRoot.Q<VisualElement>("tut-dim-right");
            _spotlightFrame = _overlayRoot.Q<VisualElement>("tut-spotlight-frame");
            _dialogBox = _overlayRoot.Q<VisualElement>("tut-dialog-box");
            _dialogText = _overlayRoot.Q<Label>("tut-dialog-text");
            _tapHintLabel = _overlayRoot.Q<Label>("tut-tap-hint");
            _skipButton = _overlayRoot.Q<Button>("tut-skip-btn");

            if (_skipButton != null)
            {
                _skipButton.clicked -= OnSkipClicked;
                _skipButton.clicked += OnSkipClicked;
                _skipButton.style.display = canSkip ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RegisterDimTap(_dimTop);
            RegisterDimTap(_dimBottom);
            RegisterDimTap(_dimLeft);
            RegisterDimTap(_dimRight);
            RegisterDimTap(_dialogBox);

            SetOverlayVisible(IsTutorialRunning || _isEnemyIntroPopupActive);
            RefreshLocalizedText();
        }

        private VisualElement BuildFallbackOverlay()
        {
            Font koreanFont = Resources.Load<Font>("Hakgyoansim Dunggeunmiso TTF B");

            var overlay = new VisualElement { name = "tutorial-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;

            Color dimColor = new Color(0.05f, 0.02f, 0.01f, 0.72f);
            string[] dimNames = { "tut-dim-top", "tut-dim-bottom", "tut-dim-left", "tut-dim-right" };
            for (int i = 0; i < dimNames.Length; i++)
            {
                var d = new VisualElement { name = dimNames[i] };
                d.style.position = Position.Absolute;
                d.style.backgroundColor = dimColor;
                overlay.Add(d);
            }

            var frame = new VisualElement { name = "tut-spotlight-frame", pickingMode = PickingMode.Ignore };
            frame.style.position = Position.Absolute;
            frame.style.backgroundColor = Color.clear;
            frame.style.borderTopWidth = frame.style.borderBottomWidth = frame.style.borderLeftWidth = frame.style.borderRightWidth = 6f;
            Color frameColor = new Color(1f, 0.84f, 0.28f, 1f);
            frame.style.borderTopColor = frame.style.borderBottomColor = frame.style.borderLeftColor = frame.style.borderRightColor = frameColor;
            frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = 24f;
            overlay.Add(frame);

            var dialog = new VisualElement { name = "tut-dialog-box" };
            dialog.style.position = Position.Absolute;
            dialog.style.left = Length.Percent(5f);
            dialog.style.right = Length.Percent(5f);
            dialog.style.top = Length.Percent(14f);
            dialog.style.height = StyleKeyword.Auto;
            dialog.style.flexShrink = 0;
            dialog.style.paddingTop = dialog.style.paddingBottom = 32f;
            dialog.style.paddingLeft = dialog.style.paddingRight = 30f;
            dialog.style.backgroundColor = new Color(0.44f, 0.24f, 0.11f, 0.96f);
            dialog.style.borderTopWidth = dialog.style.borderBottomWidth = dialog.style.borderLeftWidth = dialog.style.borderRightWidth = 6f;
            dialog.style.borderTopColor = dialog.style.borderBottomColor = dialog.style.borderLeftColor = dialog.style.borderRightColor = frameColor;
            dialog.style.borderTopLeftRadius = dialog.style.borderTopRightRadius = dialog.style.borderBottomLeftRadius = dialog.style.borderBottomRightRadius = 30f;
            dialog.style.alignItems = Align.Center;

            var txt = new Label { name = "tut-dialog-text", pickingMode = PickingMode.Ignore };
            txt.style.width = Length.Percent(100f);
            txt.style.height = StyleKeyword.Auto;
            txt.style.flexShrink = 0;
            txt.style.whiteSpace = WhiteSpace.Normal;
            txt.style.unityTextAlign = TextAnchor.MiddleCenter;
            txt.style.fontSize = 34f;
            txt.style.color = new Color(1f, 0.96f, 0.91f, 1f);
            if (koreanFont != null) txt.style.unityFont = koreanFont;
            dialog.Add(txt);

            var hint = new Label { name = "tut-tap-hint", pickingMode = PickingMode.Ignore };
            hint.style.marginTop = 16f;
            hint.style.fontSize = 26f;
            hint.style.color = new Color(1f, 0.85f, 0.47f, 1f);
            if (koreanFont != null) hint.style.unityFont = koreanFont;
            dialog.Add(hint);

            overlay.Add(dialog);

            var skip = new Button { name = "tut-skip-btn", text = "SKIP" };
            skip.style.position = Position.Absolute;
            skip.style.top = Length.Percent(3.5f);
            skip.style.right = Length.Percent(4f);
            skip.style.width = 190f;
            skip.style.height = 80f;
            skip.style.fontSize = 32f;
            skip.style.color = Color.white;
            skip.style.backgroundColor = new Color(0.33f, 0.17f, 0.07f, 0.92f);
            skip.style.borderTopWidth = skip.style.borderBottomWidth = skip.style.borderLeftWidth = skip.style.borderRightWidth = 4f;
            skip.style.borderTopColor = skip.style.borderBottomColor = skip.style.borderLeftColor = skip.style.borderRightColor = frameColor;
            skip.style.borderTopLeftRadius = skip.style.borderTopRightRadius = skip.style.borderBottomLeftRadius = skip.style.borderBottomRightRadius = 20f;
            if (koreanFont != null) skip.style.unityFont = koreanFont;
            overlay.Add(skip);

            return overlay;
        }

        private void RegisterDimTap(VisualElement el)
        {
            if (el == null) return;
            el.UnregisterCallback<ClickEvent>(OnOverlayTapped);
            el.RegisterCallback<ClickEvent>(OnOverlayTapped);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRoot == null) return;
            _overlayRoot.EnableInClassList("hide", !visible);
            _overlayRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) _overlayRoot.BringToFront();
        }
        #endregion

        #region Public Control API
        public bool TryStartTutorial(string overrideTutorialId = null)
        {
            string targetId = string.IsNullOrEmpty(overrideTutorialId) ? tutorialId : overrideTutorialId;
            if (steps == null || steps.Count == 0) return false;

            if (onlyRunIfNotCompleted && ProgramManager.Instance != null && ProgramManager.Instance.IsTutorialCompleted(targetId))
                return false;

            ResolvePanelRenderer();
            if (panelRenderer == null) return false;

            RegisterPanelCallback();

            tutorialId = targetId;
            IsTutorialRunning = true;
            _currentStepIndex = -1;

            if (MatchManager.Instance != null)
                MatchManager.Instance.InterceptWaveStart = HandleWaveStartIntercept;

            SetOverlayVisible(true);
            OnTutorialStarted?.Invoke();
            AdvanceStep();
            return true;
        }

        public void SkipTutorial()
        {
            if (!IsTutorialRunning && !_isEnemyIntroPopupActive) return;

            if (_isEnemyIntroPopupActive)
            {
                CloseEnemyIntro();
                return;
            }

            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            MatchManager.Instance?.ClearTutorialBoardLock();
            FinishTutorial();
        }

        public void NotifyGachaDrawn()
        {
            if (!IsTutorialRunning) return;
            if (CurrentStep != null && CurrentStep.completionTrigger == TutorialCompletionTrigger.GachaDrawn)
                AdvanceStep();
        }

        public void NotifyUnitEquipped()
        {
            if (!IsTutorialRunning) return;
            if (CurrentStep != null && CurrentStep.completionTrigger == TutorialCompletionTrigger.UnitEquipped)
                AdvanceStep();
        }

        public void NotifyQuestClaimed()
        {
            if (!IsTutorialRunning) return;
            if (CurrentStep != null && CurrentStep.completionTrigger == TutorialCompletionTrigger.QuestClaimed)
                AdvanceStep();
        }

        public void SetOverlayHiddenTemporarily(bool hidden)
        {
            if (_overlayRoot == null) return;
            _overlayRoot.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        }
        #endregion

        #region Step Execution
        private void AdvanceStep()
        {
            UnhookCurrentTarget();

            if (CurrentStep != null && CurrentStep.pauseGameDuringStep)
                GameManager.Instance?.SetTutorialPause(false);

            GameManager.Instance?.SetTutorialTimeScale(1f);

            _currentStepIndex++;
            _currentProgressCount = 0;

            if (_currentStepIndex >= steps.Count)
            {
                FinishTutorial();
                return;
            }

            TutorialStep step = steps[_currentStepIndex];

            if (step.completionTrigger == TutorialCompletionTrigger.MatchCompleted)
                MatchManager.Instance?.SetupTutorialLShapeBoard(targetUnitIndex: 0);

            if (step.grantTutorialUnitOnEnter || step.completionTrigger == TutorialCompletionTrigger.UnitPlaced)
                MatchManager.Instance?.EnsureTutorialUnitAvailable(0);

            if (step.completionTrigger == TutorialCompletionTrigger.TutorialEnemyKilled || step.spawnTutorialEnemyPrefab != null)
            {
                if (EnemySpawner.Instance != null)
                {
                    GameObject enemyPrefab = step.spawnTutorialEnemyPrefab != null
                        ? step.spawnTutorialEnemyPrefab
                        : EnemySpawner.Instance.GetFallbackEnemyPrefab();

                    if (enemyPrefab != null)
                    {
                        _trackedEnemy = EnemySpawner.Instance.SpawnSpecificEnemy(enemyPrefab, 1f, 1f);
                        _trackedEnemy?.SetTutorialHP(0.1f);
                    }
                }
            }

            float targetScale = step.stepTimeScale > 1.01f
                ? step.stepTimeScale
                : (step.completionTrigger == TutorialCompletionTrigger.TutorialEnemyKilled ? defaultWatchEnemyTimeScale : 1f);
            GameManager.Instance?.SetTutorialTimeScale(targetScale);

            if (step.pauseGameDuringStep)
                GameManager.Instance?.SetTutorialPause(true);

            ApplyCurrentStepVisuals();
        }

        private void FinishTutorial()
        {
            UnhookCurrentTarget();
            GameManager.Instance?.SetTutorialPause(false);
            GameManager.Instance?.SetTutorialTimeScale(1f);
            MatchManager.Instance?.ClearTutorialBoardLock();

            if (MatchManager.Instance != null && MatchManager.Instance.InterceptWaveStart == HandleWaveStartIntercept)
                MatchManager.Instance.InterceptWaveStart = null;

            IsTutorialRunning = false;
            _currentStepIndex = -1;
            _trackedEnemy = null;

            ProgramManager.Instance?.CompleteTutorial(tutorialId);
            SetOverlayVisible(false);
            OnTutorialFinished?.Invoke();
        }

        private void ApplyCurrentStepVisuals()
        {
            if (_overlayRoot == null) return;
            TutorialStep step = CurrentStep;
            if (step == null) return;

            SetOverlayVisible(true);
            RefreshLocalizedText();

            TutorialDialogPosition effectivePos = ShouldHighlightSlotAndField(step)
                ? TutorialDialogPosition.Bottom
                : step.dialogPosition;
            ApplyDialogPosition(effectivePos);

            bool isTapToContinue = step.completionTrigger == TutorialCompletionTrigger.TapAnywhere;
            if (_tapHintLabel != null)
                _tapHintLabel.style.display = isTapToContinue ? DisplayStyle.Flex : DisplayStyle.None;

            if (ShouldHighlightSlotAndField(step))
            {
                UpdateSlotAndFieldSpotlight(step.targetElementName, step.highlightPadding > 0f ? step.highlightPadding : 16f);
            }
            else if (step.highlightType == TutorialHighlightType.None)
            {
                SetFullScreenDim();
            }
            else if (step.highlightType == TutorialHighlightType.UIElement)
            {
                UpdateUISpotlight(step.targetElementName, step.highlightPadding > 0f ? step.highlightPadding : 16f);
                HookTargetUIElement(step);
            }
            else if (step.highlightType == TutorialHighlightType.WorldEnemy && _trackedEnemy != null)
            {
                Vector2 sz = step.worldSpotlightSize.sqrMagnitude > 1f ? step.worldSpotlightSize : new Vector2(260f, 260f);
                UpdateWorldSpotlight(_trackedEnemy.transform.position, sz);
            }
            else if (step.highlightType == TutorialHighlightType.WorldTransform && step.targetWorldTransform != null)
            {
                Vector2 sz = step.worldSpotlightSize.sqrMagnitude > 1f ? step.worldSpotlightSize : new Vector2(260f, 260f);
                UpdateWorldSpotlight(step.targetWorldTransform.position, sz);
            }
            else
            {
                SetFullScreenDim();
            }
        }

        private void HookTargetUIElement(TutorialStep step)
        {
            if (step.completionTrigger != TutorialCompletionTrigger.TargetButtonClick || _root == null) return;
            if (string.IsNullOrEmpty(step.targetElementName)) return;

            VisualElement target = _root.Q<VisualElement>(step.targetElementName);
            if (target is Button btn)
            {
                _hookedTargetButton = btn;
                _hookedTargetButton.clicked += OnHookedButtonClicked;
            }
            else if (target is Toggle toggle)
            {
                _hookedTargetToggle = toggle;
                _hookedTargetToggle.RegisterValueChangedCallback(OnHookedToggleChanged);
            }
        }

        private void UnhookCurrentTarget()
        {
            if (_hookedTargetButton != null)
            {
                _hookedTargetButton.clicked -= OnHookedButtonClicked;
                _hookedTargetButton = null;
            }
            if (_hookedTargetToggle != null)
            {
                _hookedTargetToggle.UnregisterValueChangedCallback(OnHookedToggleChanged);
                _hookedTargetToggle = null;
            }
        }

        private void OnHookedButtonClicked()
        {
            if (!IsTutorialRunning) return;
            if (CurrentStep != null && CurrentStep.completionTrigger == TutorialCompletionTrigger.TargetButtonClick)
                AdvanceStep();
        }

        private void OnHookedToggleChanged(ChangeEvent<bool> evt)
        {
            if (!IsTutorialRunning) return;
            if (CurrentStep != null && CurrentStep.completionTrigger == TutorialCompletionTrigger.TargetButtonClick)
                AdvanceStep();
        }

        private void OnOverlayTapped(ClickEvent evt)
        {
            if (_isEnemyIntroPopupActive)
            {
                CloseEnemyIntro();
                return;
            }

            if (!IsTutorialRunning || CurrentStep == null) return;
            if (CurrentStep.completionTrigger == TutorialCompletionTrigger.TapAnywhere)
            {
                SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
                AdvanceStep();
            }
        }

        private void OnSkipClicked() => SkipTutorial();
        #endregion

        #region Gameplay Event Hooks & Enemy Intro (Stage 2+)
        private void SubscribeGameplayEvents()
        {
            UnsubscribeGameplayEvents();

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchSucceeded += HandleMatchSucceeded;
                MatchManager.Instance.OnUnitPlaced += HandleUnitPlaced;
                MatchManager.Instance.OnPlayWaveButtonPressed += HandlePlayWavePressed;
            }

            if (EnemySpawner.Instance != null)
                EnemySpawner.Instance.OnEnemySpawned += HandleEnemySpawned;

            Enemy.OnAnyEnemyDied += HandleAnyEnemyDied;
        }

        private void UnsubscribeGameplayEvents()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchSucceeded -= HandleMatchSucceeded;
                MatchManager.Instance.OnUnitPlaced -= HandleUnitPlaced;
                MatchManager.Instance.OnPlayWaveButtonPressed -= HandlePlayWavePressed;
            }

            if (EnemySpawner.Instance != null)
                EnemySpawner.Instance.OnEnemySpawned -= HandleEnemySpawned;

            Enemy.OnAnyEnemyDied -= HandleAnyEnemyDied;
        }

        private bool HandleWaveStartIntercept()
        {
            if (!IsTutorialRunning || CurrentStep == null || _overlayRoot == null)
                return false;

            return true;
        }

        private void HandleMatchSucceeded(int unitIndex, int matchCount)
        {
            if (!IsTutorialRunning || CurrentStep == null) return;
            if (CurrentStep.completionTrigger != TutorialCompletionTrigger.MatchCompleted) return;
            if (unitIndex != 0) return;

            _currentProgressCount++;
            if (_currentProgressCount >= Mathf.Max(1, CurrentStep.requiredCount))
            {
                if (_overlayRoot != null)
                    _overlayRoot.schedule.Execute(AdvanceStep).StartingIn(450);
                else
                    AdvanceStep();
            }
        }

        private void HandleUnitPlaced(Unit unit)
        {
            if (!IsTutorialRunning || CurrentStep == null) return;
            if (CurrentStep.completionTrigger != TutorialCompletionTrigger.UnitPlaced) return;

            _currentProgressCount++;
            if (_currentProgressCount >= Mathf.Max(1, CurrentStep.requiredCount))
                AdvanceStep();
        }

        private void HandlePlayWavePressed()
        {
            if (!IsTutorialRunning || CurrentStep == null) return;
            if (CurrentStep.completionTrigger == TutorialCompletionTrigger.WaveStartPressed)
                AdvanceStep();
        }

        private void HandleAnyEnemyDied(Enemy enemy)
        {
            if (!IsTutorialRunning || CurrentStep == null) return;
            if (CurrentStep.completionTrigger != TutorialCompletionTrigger.TutorialEnemyKilled) return;

            if (_trackedEnemy == null || enemy == _trackedEnemy)
            {
                _trackedEnemy = null;
                AdvanceStep();
            }
        }

        private void HandleEnemySpawned(Enemy enemy)
        {
            if (enemy == null || IsTutorialRunning || _isEnemyIntroPopupActive) return;
            if (enemyIntroTriggers == null || enemyIntroTriggers.Count == 0) return;

            for (int i = 0; i < enemyIntroTriggers.Count; i++)
            {
                var intro = enemyIntroTriggers[i];
                if (intro == null || intro.targetEnemyId != enemy.EnemyId) continue;

                if (ProgramManager.Instance != null && ProgramManager.Instance.IsTutorialCompleted(intro.introTutorialId))
                    continue;

                ShowEnemyIntro(enemy, intro);
                break;
            }
        }

        private void ShowEnemyIntro(Enemy enemy, EnemyIntroTrigger intro)
        {
            _isEnemyIntroPopupActive = true;
            _activeEnemyIntro = intro;
            _trackedEnemy = enemy;

            GameManager.Instance?.SetTutorialPause(true);
            SetOverlayVisible(true);
            ApplyDialogPosition(TutorialDialogPosition.Bottom);

            if (_tapHintLabel != null) _tapHintLabel.style.display = DisplayStyle.Flex;
            RefreshLocalizedText();
            Vector2 sz = intro.spotlightSize.sqrMagnitude > 1f ? intro.spotlightSize : new Vector2(260f, 260f);
            UpdateWorldSpotlight(enemy.transform.position, sz);
        }

        private void CloseEnemyIntro()
        {
            if (!_isEnemyIntroPopupActive) return;

            if (_activeEnemyIntro != null)
                ProgramManager.Instance?.CompleteTutorial(_activeEnemyIntro.introTutorialId);

            _isEnemyIntroPopupActive = false;
            _activeEnemyIntro = null;
            _trackedEnemy = null;

            GameManager.Instance?.SetTutorialPause(false);
            SetOverlayVisible(false);
        }
        #endregion

        #region Spotlight Hole-Punch Geometry
        private void SetFullScreenDim()
        {
            if (_dimTop == null) return;
            SetRect(_dimTop, 0, 0, 100, 100, isPercent: true);
            SetDimHidden(_dimBottom);
            SetDimHidden(_dimLeft);
            SetDimHidden(_dimRight);

            if (_spotlightFrame != null)
                _spotlightFrame.style.display = DisplayStyle.None;
        }

        private void UpdateSlotAndFieldSpotlight(string slotElementName, float padding)
        {
            if (_overlayRoot == null || _root == null) return;

            if (!MatchManager.IsDraggingUnit)
            {
                string targetName = !string.IsNullOrEmpty(slotElementName) ? slotElementName : "store-slot-0";
                UpdateUISpotlight(targetName, padding);
                return;
            }

            float totalW = _overlayRoot.resolvedStyle.width;
            float totalH = _overlayRoot.resolvedStyle.height;
            if (totalW <= 1f || totalH <= 1f) return;

            VisualElement storeBoard = _root.Q<VisualElement>("store-board");
            float fieldTopY = totalH * 0.075f;
            float fieldBottomY = (storeBoard != null && storeBoard.worldBound.height > 1f)
                ? storeBoard.worldBound.yMin - 6f
                : totalH * 0.40f;

            Rect fieldHole = new Rect(0f, fieldTopY, totalW, Mathf.Max(100f, fieldBottomY - fieldTopY));
            ApplySpotlightHole(fieldHole);
        }

        private void UpdateUISpotlight(string elementName, float padding)
        {
            if (_root == null || string.IsNullOrEmpty(elementName))
            {
                SetFullScreenDim();
                return;
            }

            VisualElement target = _root.Q<VisualElement>(elementName);
            if (target == null || target.worldBound.width <= 1f || target.worldBound.height <= 1f)
            {
                SetFullScreenDim();
                return;
            }

            Rect r = target.worldBound;
            r.x -= padding;
            r.y -= padding;
            r.width += padding * 2f;
            r.height += padding * 2f;

            ApplySpotlightHole(r);
        }

        private void UpdateWorldSpotlight(Vector3 worldPos, Vector2 size)
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || _overlayRoot == null)
            {
                SetFullScreenDim();
                return;
            }

            Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f)
            {
                SetFullScreenDim();
                return;
            }

            float panelW = _overlayRoot.resolvedStyle.width;
            float panelH = _overlayRoot.resolvedStyle.height;
            if (panelW <= 1f || panelH <= 1f || Screen.width <= 0 || Screen.height <= 0)
            {
                SetFullScreenDim();
                return;
            }

            float px = (screenPos.x / Screen.width) * panelW;
            float py = (1f - (screenPos.y / Screen.height)) * panelH;

            Rect hole = new Rect(px - size.x * 0.5f, py - size.y * 0.5f, size.x, size.y);
            ApplySpotlightHole(hole);
        }

        private void ApplySpotlightHole(Rect hole)
        {
            if (_overlayRoot == null || _dimTop == null) return;

            float totalW = _overlayRoot.resolvedStyle.width;
            float totalH = _overlayRoot.resolvedStyle.height;
            if (totalW <= 1f || totalH <= 1f) return;

            float xMin = Mathf.Clamp(hole.xMin, 0f, totalW);
            float yMin = Mathf.Clamp(hole.yMin, 0f, totalH);
            float xMax = Mathf.Clamp(hole.xMax, xMin, totalW);
            float yMax = Mathf.Clamp(hole.yMax, yMin, totalH);

            SetRectPx(_dimTop, 0f, 0f, totalW, yMin);
            SetRectPx(_dimBottom, 0f, yMax, totalW, totalH - yMax);
            SetRectPx(_dimLeft, 0f, yMin, xMin, yMax - yMin);
            SetRectPx(_dimRight, xMax, yMin, totalW - xMax, yMax - yMin);

            if (_spotlightFrame != null)
            {
                _spotlightFrame.style.display = DisplayStyle.Flex;
                _spotlightFrame.style.left = xMin;
                _spotlightFrame.style.top = yMin;
                _spotlightFrame.style.width = xMax - xMin;
                _spotlightFrame.style.height = yMax - yMin;
            }
        }

        private static void SetRectPx(VisualElement el, float x, float y, float w, float h)
        {
            if (el == null) return;
            el.style.display = (w > 0.5f && h > 0.5f) ? DisplayStyle.Flex : DisplayStyle.None;
            el.style.left = x;
            el.style.top = y;
            el.style.width = Mathf.Max(0f, w);
            el.style.height = Mathf.Max(0f, h);
        }

        private static void SetRect(VisualElement el, float x, float y, float w, float h, bool isPercent)
        {
            if (el == null) return;
            el.style.display = DisplayStyle.Flex;
            el.style.left = isPercent ? Length.Percent(x) : x;
            el.style.top = isPercent ? Length.Percent(y) : y;
            el.style.width = isPercent ? Length.Percent(w) : w;
            el.style.height = isPercent ? Length.Percent(h) : h;
        }

        private static void SetDimHidden(VisualElement el)
        {
            if (el != null) el.style.display = DisplayStyle.None;
        }

        private void ApplyDialogPosition(TutorialDialogPosition pos)
        {
            if (_dialogBox == null) return;
            _dialogBox.EnableInClassList("tut-dialog--top", pos == TutorialDialogPosition.Top);
            _dialogBox.EnableInClassList("tut-dialog--center", pos == TutorialDialogPosition.Center);
            _dialogBox.EnableInClassList("tut-dialog--bottom", pos == TutorialDialogPosition.Bottom);

            if (pos == TutorialDialogPosition.Top)
            {
                _dialogBox.style.top = Length.Percent(13f);
                _dialogBox.style.bottom = StyleKeyword.Auto;
            }
            else if (pos == TutorialDialogPosition.Center)
            {
                _dialogBox.style.top = Length.Percent(38f);
                _dialogBox.style.bottom = StyleKeyword.Auto;
            }
            else
            {
                _dialogBox.style.top = StyleKeyword.Auto;
                _dialogBox.style.bottom = Length.Percent(12f);
            }
        }

        private void RefreshLocalizedText()
        {
            if (_skipButton != null)
                _skipButton.text = GetTextOrFallback("tut_skip", "SKIP");

            if (_tapHintLabel != null)
                _tapHintLabel.text = GetTextOrFallback("tut_tap_continue", "Tap to continue");

            if (_dialogText == null) return;

            string key = _isEnemyIntroPopupActive
                ? _activeEnemyIntro?.messageLocalizationKey
                : CurrentStep?.messageLocalizationKey;

            if (!string.IsNullOrEmpty(key))
                _dialogText.text = GetTextOrFallback(key, key);
        }

        private static string GetTextOrFallback(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string t = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(t) || t.StartsWith("#") ? fallback : t;
        }
        #endregion
    }
}
