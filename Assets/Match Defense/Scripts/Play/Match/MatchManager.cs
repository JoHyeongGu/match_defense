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
        public GameObject unitPrefab;
        public Sprite thumbnailSprite;
        public ItemVisual itemVisual;
        public float spawnWeight = 1f;
        public int maxCharge = 3;
        public Color sliderColor = Color.yellow;
    }

    public class MatchManager : MonoBehaviour
    {
        [Header("<color=yellow>References</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private WaveManager waveManager;

        public static MatchManager Instance { get; private set; }

        private int uiVersion = -1;
        private VisualElement bottomRect;

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
            bottomRect = rootElement.Q<VisualElement>("bottom-rect");
            InitPlayButton(rootElement);
            InitTurnCount(rootElement);
            boardWidget = rootElement.Q<MatchBoard>();
            storeWidget = rootElement.Q<StoreBoard>();
            boardWidget?.Initialize(this, unitDatas, TriggerHapticAndSound, OnItemMatched, CanDragMatchBoard, PlayTurnWarningShake, ConsumeTurn);
            storeWidget?.Initialize(this, unitDatas, HandleBeginDrag, HandleDrag, HandleEndDrag);
        }

        private void Start() => StartCoroutine(StartRoutine());

        private IEnumerator StartRoutine()
        {
            ProgramManager.Instance.FadeScreen(off: true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
                yield return null;
            ProgramManager.Instance.FadeScreen(off: false, duration: 1f);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        #region [Match] Core
        [Header("<color=yellow>Match System</color>")]
        [SerializeField] private MatchUnitData[] unitDatas;

        private MatchBoard boardWidget;

        public void SetMatchActive(bool isActive)
        {
            boardWidget?.SetMatchActive(isActive);
        }

        private void OnItemMatched(int unitIndex, int matchCount)
        {
            storeWidget?.AddCharge(unitIndex, matchCount);
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

        private void OnAutoToggleChanged(ChangeEvent<bool> evt)
        {
            isAutoWave = evt.newValue;
        }

        private void ApplyGameSpeed()
        {
            Time.timeScale = (isWavePlaying && isDoubleSpeed) ? 2f : 1f;
        }

        private void SyncWaveButtonState()
        {
            playButton?.EnableInClassList("hide", isWavePlaying);
            playButton?.SetValueWithoutNotify(isWavePlaying);
            doubleBtn?.EnableInClassList("hide", !isWavePlaying);
        }

        private IEnumerator PlayRoutine()
        {
            WaveManager targetWaveManager = waveManager != null ? waveManager : WaveManager.Instance;
            if (targetWaveManager == null)
            {
                playButton?.SetValueWithoutNotify(false);
                playUiCoroutine = null;
                yield break;
            }

            isWavePlaying = true;
            ApplyGameSpeed();

            playButton?.AddToClassList("hide");
            targetWaveManager.StartWave(autoContinueCheck, wavesCompletedCallback);

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

        private IEnumerator HideRoutine()
        {
            playButton?.AddToClassList("hide");
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                yield return null;

            cameraController?.ResetAndMoveZ(0.6f, 1f);
            boardWidget?.AddToClassList("hide");
            storeWidget?.AddToClassList("hide");
            bottomRect?.AddToClassList("hide");
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                yield return null;

            turnCountRect?.AddToClassList("hide");
            doubleBtn?.RemoveFromClassList("hide");
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
            turnCountText = turnCountRect.Q<TextElement>("turn-count");
            UpdateTurnUI();
            StopTurnWarningShake();
        }

        public void SetTurn(int turn)
        {
            currentTurn = Mathf.Max(0, turn);
            UpdateTurnUI();
        }

        public void AddTurn(int amount)
        {
            currentTurn = Mathf.Max(0, currentTurn + amount);
            UpdateTurnUI();
        }

        public void ResetTurn(int turn = 5)
        {
            currentTurn = Mathf.Max(0, turn);
            UpdateTurnUI();
        }

        private bool CanDragMatchBoard() => currentTurn > 0;

        private void ConsumeTurn()
        {
            if (currentTurn <= 0) return;
            currentTurn--;
            UpdateTurnUI();
        }

        private void UpdateTurnUI()
        {
            if (turnCountText != null)
                turnCountText.text = $"{currentTurn}";
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
            if (turnCountText == null)
            {
                turnShakeTask?.Pause();
                return;
            }

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
        [SerializeField] private AudioClip[] matchClips;

        private static readonly WaitForSeconds SfxIntervalWait = new WaitForSeconds(0.05f);

        private void TriggerHapticAndSound(float delayTime, int matchCount)
        {
            StartCoroutine(DelayedHapticRoutine(delayTime, matchCount));
        }

        private IEnumerator DelayedHapticRoutine(float delayTime, int matchCount)
        {
            if (SoundManager.Instance != null && matchClips != null && matchClips.Length > 0)
            {
                int safeMatchCount = Mathf.Max(1, matchCount);

                for (int i = 0; i < safeMatchCount; i++)
                {
                    float pitch = 1f + (i * 0.08f);
                    SoundManager.Instance.PlayRandomSFX(matchClips, 1f, pitch);
                    if (i < safeMatchCount - 1)
                    {
                        for (float t = 0f; t < 0.05f; t += Time.unscaledDeltaTime)
                            yield return null;
                    }
                }
            }

            for (float elapsed = 0f; elapsed < delayTime; elapsed += Time.unscaledDeltaTime)
                yield return null;

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
            catch
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    {
                        vibrator.Call("vibrate", 150L);
                    }
                }
                catch { }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
        #endregion

        #region [Store] Unit Spawn
        [Header("<color=yellow>Store System</color>")]
        [SerializeField] private LayerMask boardLayer;
        [SerializeField] private Color spawnableColor = Color.green;
        [SerializeField] private Color unspawnableColor = Color.red;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        public static bool IsDraggingUnit { get; private set; }
        public readonly List<Unit> SpawnedUnits = new();

        private StoreBoard storeWidget;
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

        public void ResetStore()
        {
            storeWidget?.ResetAllSlots();
        }

        public void AddSpawnedUnit(Unit unit)
        {
            if (unit != null && !SpawnedUnits.Contains(unit))
                SpawnedUnits.Add(unit);
        }

        public void RemoveSpawnedUnit(Unit unit)
        {
            SpawnedUnits.Remove(unit);
        }

        private void HandleBeginDrag(GameObject unitPrefab)
        {
            if (unitPrefab == null) return;

            currentDragPrefab = unitPrefab;
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
                Instantiate(currentDragPrefab, spawnPos, Quaternion.identity);
                spawned = true;
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