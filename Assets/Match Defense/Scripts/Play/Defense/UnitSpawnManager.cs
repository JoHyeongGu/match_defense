using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Defense
{
    [System.Serializable]
    public class UnitData
    {
        public int itemIndex;
        public GameObject unitPrefab;
        public float chargePerItem = 0.2f;
        public GameObject dragArea;
        public Image sliderFilled;
        public Image thumbFilled;
        public TextMeshProUGUI unitCountText;
        public RectTransform bounceIcon;
        public Color sliderColor = Color.yellow;

        [HideInInspector] public float charge;
        [HideInInspector] public int availableUnits;
        [HideInInspector] public bool animating;
        [HideInInspector] public Vector3 unitStoreScale = Vector3.one;
    }

    public class UnitSpawnManager : MonoBehaviour
    {
        public static UnitSpawnManager Instance { get; private set; }
        [SerializeField] private Camera mainCamera;

        [Header("<color=yellow>Unit Settings</color>")]
        [SerializeField] private UnitData[] units;

        [Header("<color=yellow>Spawn</color>")]
        [SerializeField] private LayerMask boardLayer;
        [SerializeField] private Color spawnableColor = Color.green;
        [SerializeField] private Color unspawnableColor = Color.red;

        public static bool IsDraggingUnit { get; private set; }

        public readonly List<Unit> SpawnedUnits = new();
        private UnitData currentDragUnit;
        private GameObject previewInstance;
        private UnitBoard activeBoard;
        private bool canSpawn = false;
        private Vector3 spawnPos = Vector3.zero;

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            foreach (var unit in units)
            {
                if (unit.sliderFilled != null) unit.sliderFilled.fillAmount = 0f;
                if (unit.thumbFilled != null) unit.thumbFilled.fillAmount = 0f;
                UpdateUIText(unit);

                if (unit.dragArea != null)
                {
                    EventTrigger trigger = unit.dragArea.GetComponent<EventTrigger>();
                    if (trigger == null) trigger = unit.dragArea.AddComponent<EventTrigger>();

                    EventTrigger.Entry beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
                    beginDrag.callback.AddListener((data) => OnBeginDragUnit(unit, (PointerEventData)data));
                    trigger.triggers.Add(beginDrag);

                    EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                    drag.callback.AddListener((data) => OnDragUnit(unit, (PointerEventData)data));
                    trigger.triggers.Add(drag);

                    EventTrigger.Entry endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
                    endDrag.callback.AddListener((data) => OnEndDragUnit(unit, (PointerEventData)data));
                    trigger.triggers.Add(endDrag);
                }
                unit.sliderFilled.color = unit.sliderColor;
                unit.unitStoreScale = unit.bounceIcon.localScale;
            }
        }
        #endregion

        #region Charge System
        public void AddCharge(int itemIndex, int matchCount)
        {
            foreach (var unit in units)
            {
                if (unit.itemIndex == itemIndex)
                {
                    StartCoroutine(ChargeRoutine(unit, matchCount));
                    break;
                }
            }
        }

        private IEnumerator ChargeRoutine(UnitData unit, int matchCount)
        {
            while (unit.animating) yield return null;
            unit.animating = true;

            if (unit.bounceIcon != null)
            {
                unit.bounceIcon.DOKill(true);
                unit.bounceIcon.localScale = unit.unitStoreScale;
                Sequence bounceSeq = DOTween.Sequence();

                for (int i = 0; i < matchCount; i++)
                {
                    bounceSeq.Append(unit.bounceIcon.DOScale(unit.unitStoreScale * 1.3f, 0.08f).SetEase(Ease.OutQuad));
                    bounceSeq.Append(unit.bounceIcon.DOScale(unit.unitStoreScale, 0.08f).SetEase(Ease.InQuad));
                }
            }

            unit.charge += matchCount * unit.chargePerItem;

            if (unit.charge >= 1f)
            {
                if (unit.sliderFilled != null) unit.sliderFilled.DOFillAmount(1f, 0.2f);
                if (unit.availableUnits == 0 && unit.thumbFilled != null) unit.thumbFilled.DOFillAmount(1f, 0.2f);

                yield return new WaitForSeconds(0.2f);

                while (unit.charge >= 1f)
                {
                    unit.availableUnits++;
                    unit.charge -= 1f;
                }

                UpdateUIText(unit);

                if (unit.thumbFilled != null) unit.thumbFilled.fillAmount = 1f;
                if (unit.sliderFilled != null) unit.sliderFilled.DOFillAmount(0f, 0.2f).SetEase(Ease.InOutQuad);

                yield return new WaitForSeconds(0.2f);
            }

            if (unit.sliderFilled != null) unit.sliderFilled.DOFillAmount(unit.charge, 0.2f);

            if (unit.availableUnits == 0 && unit.thumbFilled != null)
            {
                unit.thumbFilled.DOFillAmount(unit.charge, 0.2f);
            }

            yield return new WaitForSeconds(0.2f);
            unit.animating = false;
        }

        private void UpdateUIText(UnitData unit)
        {
            if (unit.unitCountText != null) unit.unitCountText.text = unit.availableUnits.ToString();
        }
        #endregion

        #region Drag & Drop
        private void OnBeginDragUnit(UnitData unit, PointerEventData eventData)
        {
            if (unit == null || unit.availableUnits <= 0) return;

            currentDragUnit = unit;
            IsDraggingUnit = true;

            if (UnitBoard.Instance != null)
            {
                UnitBoard.Instance.ToggleGridVisibility(true);

                Unit unitComp = currentDragUnit.unitPrefab.GetComponent<Unit>();
                float range = unitComp != null ? unitComp.range : 2f;
                UnitBoard.Instance.SetHoverRange(range);
            }

            if (currentDragUnit.unitPrefab != null)
            {
                previewInstance = Instantiate(currentDragUnit.unitPrefab);

                Unit previewUnit = previewInstance.GetComponent<Unit>();
                if (previewUnit != null) previewUnit.enabled = false;

                previewInstance.SetActive(false);
            }
        }

        private void OnDragUnit(UnitData unit, PointerEventData eventData)
        {
            if (currentDragUnit == null || currentDragUnit.availableUnits <= 0) return;

            Ray ray = mainCamera.ScreenPointToRay(eventData.position);
            Vector3 targetPos = Vector3.zero;
            canSpawn = false;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, boardLayer))
            {
                UnitBoard board = hit.collider.GetComponent<UnitBoard>();
                if (board != null)
                {
                    activeBoard = board;
                    board.UpdateHover(hit.textureCoord);

                    if (board.IsValidSpawnPosition(hit.textureCoord, out Vector3 snappedPos))
                    {
                        targetPos = snappedPos;
                        spawnPos = snappedPos;
                        Vector2Int targetCell = board.WorldToCell(snappedPos);
                        bool isOccupied = false;

                        foreach (var existingUnit in SpawnedUnits)
                        {
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

                    board.SetBorderColor(canSpawn ? spawnableColor : unspawnableColor);
                }
            }
            else
            {
                if (activeBoard != null)
                {
                    activeBoard.ClearHover();
                    activeBoard = null;
                }

                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    targetPos = ray.GetPoint(enter);
                }
            }

            if (previewInstance != null)
            {
                previewInstance.SetActive(true);
                previewInstance.transform.position = targetPos;
                UpdatePreviewColor(canSpawn);
            }
        }

        private void OnEndDragUnit(UnitData unit, PointerEventData eventData)
        {
            if (currentDragUnit == null) return;

            IsDraggingUnit = false;

            if (UnitBoard.Instance != null) UnitBoard.Instance.ToggleGridVisibility(false);

            if (activeBoard != null)
            {
                activeBoard.ClearHover();
                activeBoard = null;
            }

            if (currentDragUnit.availableUnits <= 0)
            {
                if (previewInstance != null) Destroy(previewInstance);
                return;
            }

            if (canSpawn)
            {
                Instantiate(currentDragUnit.unitPrefab, spawnPos, Quaternion.identity);
                currentDragUnit.availableUnits--;
                UpdateUIText(currentDragUnit);

                if (currentDragUnit.availableUnits == 0 && currentDragUnit.thumbFilled != null)
                {
                    currentDragUnit.thumbFilled.DOFillAmount(currentDragUnit.charge, 0.3f).SetEase(Ease.OutQuad);
                }
            }

            if (previewInstance != null) Destroy(previewInstance);
            canSpawn = false;
            currentDragUnit = null;
        }

        public void AddSpawnedUnit(Unit unit) => SpawnedUnits.Add(unit);
        public void RemoveSpawnedUnit(Unit unit) => SpawnedUnits.Remove(unit);
        #endregion

        #region Visuals
        private void UpdatePreviewColor(bool isValid)
        {
            Transform rangeIndicator = null;

            foreach (Transform child in previewInstance.transform)
            {
                if (child.localScale.y <= 0.5f)
                {
                    rangeIndicator = child;
                    break;
                }
            }

            if (rangeIndicator != null) rangeIndicator.gameObject.SetActive(isValid);

            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (rangeIndicator != null && r.gameObject == rangeIndicator.gameObject) continue;

                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = isValid ? 1f : 0.4f;
                        c.r = isValid ? c.r : Mathf.Min(c.r + 0.3f, 1f);
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
        }
        #endregion
    }
}