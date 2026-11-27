using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;
using MatchDefense.Defense;

namespace MatchDefense.Match
{
    [Serializable]
    public class MatchUnitData
    {
        public string unitId;
        public GameObject unitPrefab;
        public Sprite thumbnailSprite;
        public ItemVisual itemVisual;
        public float spawnWeight = 1f;
        public int maxCharge = 3;
        public Color sliderColor = Color.yellow;
        public bool isUnlocked = true;
        public int unitLevel = 1;
        public float bonusDamage = 0f;
        public float cooldownMultiplier = 1f;
    }

    public class MatchManager : MonoBehaviour
    {
        [Header("<color=yellow>References</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameManager gameManager;

        [Header("<color=yellow>Store System</color>")]
        [SerializeField] private LayerMask boardLayer;
        [SerializeField] private Color spawnableColor = Color.green;
        [SerializeField] private Color unspawnableColor = Color.red;

        public static MatchManager Instance { get; private set; }

        public event Action<int, int> OnMatchSucceeded;
        public event Action<Unit> OnUnitPlaced;
        public event Action OnPlayWaveButtonPressed;

        public Func<bool> InterceptWaveStart;

        private int uiVersion = -1;
        private MatchUnitData[] activeUnitDatas;
        private MatchBoard boardWidget;
        private StoreBoard storeWidget;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (mainCamera == null) mainCamera = Camera.main;
            updateTurnShakeAction = UpdateTurnShake;
        }

        private void OnEnable()
        {
            if (panelRenderer != null)
                panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            StopTurnWarningShake();

            if (IsDraggingUnit)
                HandleEndDrag(false);
        }

        private void OnUIReload(PanelRenderer panel, VisualElement rootElement, int version)
        {
            if (version == uiVersion) return;
            uiVersion = version;
            turnShakeTask = null;

            InitPlayButton(rootElement);
            InitTurnCount(rootElement);

            activeUnitDatas = ProgramManager.Instance != null
                ? ProgramManager.Instance.BuildPlaySceneUnitDatas()
                : null;

            boardWidget = rootElement.Q<MatchBoard>();
            storeWidget = rootElement.Q<StoreBoard>();
            boardWidget?.Initialize(this, activeUnitDatas, TriggerHapticAndSound, OnItemMatched, CanDragMatchBoard, PlayTurnWarningShake, ConsumeTurn);
            storeWidget?.Initialize(this, activeUnitDatas, HandleBeginDrag, HandleDrag, HandleEndDrag);
        }

        private void Start() => StartCoroutine(StartRoutine());

        private IEnumerator StartRoutine()
        {
            if (ProgramManager.Instance != null)
            {
                ProgramManager.Instance.FadeScreen(off: true, duration: 0f);
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                    yield return null;
                ProgramManager.Instance.FadeScreen(off: false, duration: 0.6f);
            }
        }

        private void OnDestroy()
        {
            GameManager.Instance?.SetDoubleSpeed(false);
        }

        #region [Match] Core & Tutorial Helpers
        public void SetMatchActive(bool isActive) => boardWidget?.SetMatchActive(isActive);

        public void SetupTutorialLShapeBoard(int targetUnitIndex = 0)
        {
            boardWidget?.SetupTutorialLShapeMatch(targetUnitIndex, lockOtherMoves: true);
        }

        public void ClearTutorialBoardLock()
        {
            boardWidget?.ClearTutorialMoveLock();
        }

        public void EnsureTutorialUnitAvailable(int slotIndex = 0)
        {
            storeWidget?.EnsureAtLeastOneUnit(slotIndex);
        }

        private void OnItemMatched(int unitIndex, int matchCount)
        {
            storeWidget?.AddCharge(unitIndex, matchCount);
            ProgramManager.Instance?.AddQuestProgress(QuestType.MatchCount, 1);
            OnMatchSucceeded?.Invoke(unitIndex, matchCount);
        }
        #endregion

        #region [Match] Play
        private Toggle playButton;
        private Toggle doubleBtn;
        private Toggle autoToggle;

        private bool isWavePlaying;
        private bool isAutoWave;
        private bool isDoubleSpeed;
        private Coroutine playUiCoroutine;

        private Func<bool> autoContinueCheck;
        private Action wavesCompletedCallback;

        private void InitPlayButton(VisualElement rootElement)
        {
            autoContinueCheck ??= () => isAutoWave;
            wavesCompletedCallback ??= OnWavesCompleted;

            playButton = rootElement.Q<Toggle>("play-btn");
            doubleBtn = rootElement.Q<Toggle>("double-btn");
            autoToggle = rootElement.Q<Toggle>("auto-toggle");

            playButton?.RegisterValueChangedCallback(OnPlayButtonToggled);
            doubleBtn?.RegisterValueChangedCallback(OnDoubleButtonToggled);
            autoToggle?.RegisterValueChangedCallback(OnAutoToggleChanged);

            if (autoToggle != null) isAutoWave = autoToggle.value;
            if (doubleBtn != null) isDoubleSpeed = doubleBtn.value;

            SyncWaveButtonState();
            ApplyGameSpeed();
        }

        public void OnPlayButtonToggled(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                if (isWavePlaying) return;

                OnPlayWaveButtonPressed?.Invoke();

                if (InterceptWaveStart != null && InterceptWaveStart.Invoke())
                {
                    playButton?.SetValueWithoutNotify(false);
                    return;
                }

                if (playUiCoroutine != null) StopCoroutine(playUiCoroutine);
                playUiCoroutine = StartCoroutine(PlayRoutine());
            }
            else
            {
                playButton.SetValueWithoutNotify(true);
            }
        }

        private void OnDoubleButtonToggled(ChangeEvent<bool> evt)
        {
            isDoubleSpeed = evt.newValue;
            ApplyGameSpeed();
        }

        private void OnAutoToggleChanged(ChangeEvent<bool> evt) => isAutoWave = evt.newValue;

        private void ApplyGameSpeed() => GameManager.Instance?.SetDoubleSpeed(isWavePlaying && isDoubleSpeed);

        private void SyncWaveButtonState()
        {
            playButton?.EnableInClassList("hide", isWavePlaying);
            playButton?.SetValueWithoutNotify(isWavePlaying);
            doubleBtn?.EnableInClassList("hide", !isWavePlaying);
        }

        private IEnumerator PlayRoutine()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                playButton?.SetValueWithoutNotify(false);
                playUiCoroutine = null;
                yield break;
            }

            isWavePlaying = true;
            ApplyGameSpeed();

            playButton?.AddToClassList("hide");
            gameManager.StartWave(autoContinueCheck, wavesCompletedCallback);

            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                yield return null;

            doubleBtn?.RemoveFromClassList("hide");
            playUiCoroutine = null;
        }

        private void OnWavesCompleted()
        {
            if (playUiCoroutine != null) StopCoroutine(playUiCoroutine);
            playUiCoroutine = StartCoroutine(EndWaveRoutine());
        }

        private IEnumerator EndWaveRoutine()
        {
            isWavePlaying = false;
            ApplyGameSpeed();

            doubleBtn?.AddToClassList("hide");
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                yield return null;

            playButton?.SetValueWithoutNotify(false);
            playButton?.RemoveFromClassList("hide");
            playUiCoroutine = null;
        }
        #endregion

        #region [Match] Turn
        private const float TurnShakeDuration = 0.5f;
        private const float TurnShakeStrength = 6f;

        private int currentTurn = 5;
        private VisualElement turnCountRect;
        private TextElement turnCountText;
        private IVisualElementScheduledItem turnShakeTask;
        private Action updateTurnShakeAction;
        private float turnShakeElapsed;

        public int CurrentTurn => currentTurn;

        private void InitTurnCount(VisualElement rootElement)
        {
            turnCountRect = rootElement.Q<VisualElement>("turn-count-rect");
            turnCountText = turnCountRect?.Q<TextElement>("turn-count");
            UpdateTurnUI();
            StopTurnWarningShake();
        }

        public void SetTurn(int turn) { currentTurn = Mathf.Max(0, turn); UpdateTurnUI(); }
        public void AddTurn(int amount) { currentTurn = Mathf.Max(0, currentTurn + amount); UpdateTurnUI(); }
        public void ResetTurn(int turn = 5) { currentTurn = Mathf.Max(0, turn); UpdateTurnUI(); }

        private bool CanDragMatchBoard() => currentTurn > 0;

        private void ConsumeTurn()
        {
            if (currentTurn <= 0) return;
            currentTurn--;
            UpdateTurnUI();
        }

        private void UpdateTurnUI()
        {
            if (turnCountText != null) turnCountText.text = $"{currentTurn}";
        }

        private void PlayTurnWarningShake()
        {
            if (turnCountText == null) return;
            turnShakeElapsed = 0f;
            if (turnShakeTask == null)
                turnShakeTask = turnCountText.schedule.Execute(updateTurnShakeAction).Every(16);
            else
                turnShakeTask.Resume();
        }

        private void StopTurnWarningShake()
        {
            turnShakeTask?.Pause();
            if (turnCountText != null)
                turnCountText.style.translate = new Translate(0f, 0f);
        }

        private void UpdateTurnShake()
        {
            if (turnCountText == null) { turnShakeTask?.Pause(); return; }

            turnShakeElapsed += Time.unscaledDeltaTime;
            float t = turnShakeElapsed / TurnShakeDuration;
            if (t >= 1f)
            {
                turnCountText.style.translate = new Translate(0f, 0f);
                turnShakeTask?.Pause();
                return;
            }

            float offsetX = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * TurnShakeStrength;
            turnCountText.style.translate = new Translate(offsetX, 0f);
        }
        #endregion

        #region [Match] Sound & Haptics
        private void TriggerHapticAndSound(float delayTime, int matchCount)
        {
            SoundManager.Instance?.PlayMatchComboSFX(matchCount);
            StartCoroutine(DelayedHapticRoutine(delayTime));
        }

        private IEnumerator DelayedHapticRoutine(float delayTime)
        {
            for (float elapsed = 0f; elapsed < delayTime; elapsed += Time.unscaledDeltaTime)
                yield return null;

            bool vibrationEnabled = ProgramManager.Instance == null || ProgramManager.Instance.IsVibrationEnabled;
            if (!vibrationEnabled) yield break;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                    {
                        using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                        {
                            AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", 300L, 5);
                            vibrator.Call("vibrate", effect);
                        }
                    }
                }
            }
            catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
        #endregion

        #region [Store] Unit Spawn
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        public static bool IsDraggingUnit { get; private set; }
        public readonly List<Unit> SpawnedUnits = new();

        private MatchUnitData currentDragUnitData;
        private GameObject currentDragPrefab;
        private GameObject previewInstance;
        private GameObject previewRangeIndicator;
        private MaterialPropertyBlock previewMpb;
        private readonly List<(Renderer renderer, int matIndex, Color baseColor)> previewTargets = new();
        private UnitBoard activeBoard;
        private Collider lastHitCollider;
        private UnitBoard lastHitBoard;
        private bool canSpawn;
        private bool hasLastPreviewState;
        private bool lastPreviewValid;
        private Vector3 spawnPos;

        public void ResetStore() => storeWidget?.ResetAllSlots();

        public void AddSpawnedUnit(Unit unit)
        {
            if (unit != null && !SpawnedUnits.Contains(unit))
                SpawnedUnits.Add(unit);
        }

        public void RemoveSpawnedUnit(Unit unit) => SpawnedUnits.Remove(unit);

        private void HandleBeginDrag(MatchUnitData unitData)
        {
            if (unitData == null || unitData.unitPrefab == null || !unitData.isUnlocked) return;

            currentDragUnitData = unitData;
            currentDragPrefab = unitData.unitPrefab;
            IsDraggingUnit = true;

            if (UnitBoard.Instance != null)
            {
                UnitBoard.Instance.ToggleGridVisibility(true);
                Unit unitComp = currentDragPrefab.GetComponent<Unit>();
                float range = unitComp != null ? unitComp.range : 2f;
                UnitBoard.Instance.SetHoverRange(range);
            }

            previewInstance = Instantiate(currentDragPrefab);
            Unit previewUnit = previewInstance.GetComponent<Unit>();
            if (previewUnit != null) previewUnit.enabled = false;

            CachePreviewVisuals();
            previewInstance.SetActive(false);
        }

        private void HandleDrag(Vector2 screenPos)
        {
            if (currentDragPrefab == null || mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            Vector3 targetPos = Vector3.zero;
            canSpawn = false;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, boardLayer))
            {
                if (hit.collider != lastHitCollider)
                {
                    lastHitCollider = hit.collider;
                    lastHitBoard = hit.collider.GetComponent<UnitBoard>();
                }

                if (lastHitBoard != null)
                {
                    activeBoard = lastHitBoard;
                    lastHitBoard.UpdateHover(hit.textureCoord);

                    if (lastHitBoard.IsValidSpawnPosition(hit.textureCoord, out Vector3 snappedPos))
                    {
                        targetPos = snappedPos;
                        spawnPos = snappedPos;
                        Vector2Int targetCell = lastHitBoard.WorldToCell(snappedPos);
                        bool isOccupied = false;

                        for (int i = 0; i < SpawnedUnits.Count; i++)
                        {
                            Unit existingUnit = SpawnedUnits[i];
                            if (existingUnit != null && existingUnit.gameObject != previewInstance && existingUnit.GridPos == targetCell)
                            {
                                isOccupied = true;
                                break;
                            }
                        }

                        canSpawn = !isOccupied;
                    }
                    else
                    {
                        targetPos = hit.point;
                    }

                    lastHitBoard.SetBorderColor(canSpawn ? spawnableColor : unspawnableColor);
                }
            }
            else
            {
                if (activeBoard != null)
                {
                    activeBoard.ClearHover();
                    activeBoard = null;
                }

                if (GroundPlane.Raycast(ray, out float enter))
                    targetPos = ray.GetPoint(enter);
            }

            if (previewInstance != null)
            {
                if (!previewInstance.activeSelf)
                    previewInstance.SetActive(true);
                previewInstance.transform.position = targetPos;
                UpdatePreviewColor(canSpawn);
            }
        }

        private bool HandleEndDrag(bool confirmSpawn)
        {
            if (currentDragPrefab == null) return false;

            IsDraggingUnit = false;

            if (UnitBoard.Instance != null)
                UnitBoard.Instance.ToggleGridVisibility(false);

            if (activeBoard != null)
            {
                activeBoard.ClearHover();
                activeBoard = null;
            }

            bool spawned = false;
            if (confirmSpawn && canSpawn)
            {
                GameObject instance = Instantiate(currentDragPrefab, spawnPos, Quaternion.identity);
                Unit unitComp = instance.GetComponent<Unit>();
                if (unitComp != null && currentDragUnitData != null)
                {
                    unitComp.ApplyUpgradeStats(
                        currentDragUnitData.unitLevel,
                        currentDragUnitData.bonusDamage,
                        currentDragUnitData.cooldownMultiplier);
                }
                spawned = true;
                SoundManager.Instance?.PlaySFX(SfxType.UnitPlace);
                ProgramManager.Instance?.AddQuestProgress(QuestType.PlaceUnit, 1);
                OnUnitPlaced?.Invoke(unitComp);
            }

            if (previewInstance != null) Destroy(previewInstance);
            previewInstance = null;
            previewRangeIndicator = null;
            previewTargets.Clear();
            lastHitCollider = null;
            lastHitBoard = null;
            hasLastPreviewState = false;
            canSpawn = false;
            currentDragPrefab = null;
            currentDragUnitData = null;

            return spawned;
        }

        private void CachePreviewVisuals()
        {
            previewRangeIndicator = null;
            previewTargets.Clear();
            hasLastPreviewState = false;

            if (previewInstance == null) return;

            foreach (Transform child in previewInstance.transform)
            {
                if (child.localScale.y <= 0.5f)
                {
                    previewRangeIndicator = child.gameObject;
                    break;
                }
            }

            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (previewRangeIndicator != null && r.gameObject == previewRangeIndicator) continue;

                Material[] sharedMats = r.sharedMaterials;
                for (int j = 0; j < sharedMats.Length; j++)
                {
                    Material mat = sharedMats[j];
                    if (mat != null && mat.HasProperty(BaseColorId))
                        previewTargets.Add((r, j, mat.GetColor(BaseColorId)));
                }
            }
        }

        private void UpdatePreviewColor(bool isValid)
        {
            if (hasLastPreviewState && lastPreviewValid == isValid) return;
            hasLastPreviewState = true;
            lastPreviewValid = isValid;

            if (previewRangeIndicator != null)
                previewRangeIndicator.SetActive(isValid);

            previewMpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < previewTargets.Count; i++)
            {
                var (r, matIndex, baseColor) = previewTargets[i];
                if (r == null) continue;

                Color c = baseColor;
                c.a = isValid ? 1f : 0.4f;
                if (!isValid) c.r = Mathf.Min(c.r + 0.3f, 1f);

                r.GetPropertyBlock(previewMpb, matIndex);
                previewMpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(previewMpb, matIndex);
            }
        }
        #endregion
    }
}
