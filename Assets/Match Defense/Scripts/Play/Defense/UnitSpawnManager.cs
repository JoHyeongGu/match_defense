using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using DG.Tweening;

namespace MatchDefense.Defense
{
    public class UnitSpawnManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static UnitSpawnManager Instance { get; private set; }

        [Header("UI Settings")]
        [SerializeField] private Slider chargeSlider;
        [SerializeField] private Image filledImage;
        [SerializeField] private TextMeshProUGUI unitCountText;
        [SerializeField] private RectTransform bounceIcon;

        [Header("Spawn Settings")]
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LayerMask boardLayer;
        [SerializeField] private float chargePerItem = 0.2f;

        [Header("Preview Settings")]
        [SerializeField] private GameObject previewPrefab;
        [SerializeField] private Color spawnableColor = Color.green;
        [SerializeField] private Color unspawnableColor = Color.red;

        public static bool IsDraggingUnit { get; private set; }

        private float charge = 0f;
        private int availableUnits = 0;
        private bool animating = false;

        private GameObject previewInstance;
        private UnitBoard activeBoard;
        private bool canSpawn = false;
        private Vector3 spawnPos = Vector3.zero;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            chargeSlider.value = 0f;
            filledImage.fillAmount = 0f;
            UpdateUIText();
        }

        #region Charge System
        public void AddCharge(int matchCount)
        {
            float amount = matchCount * chargePerItem;
            StartCoroutine(ChargeRoutine(amount, matchCount));
        }

        private IEnumerator ChargeRoutine(float amount, int matchCount)
        {
            while (animating) yield return null;
            animating = true;

            bounceIcon.DOKill(true);
            bounceIcon.localScale = Vector3.one;
            Sequence bounceSeq = DOTween.Sequence();
            
            for (int i = 0; i < matchCount; i++)
            {
                bounceSeq.Append(bounceIcon.DOScale(Vector3.one * 1.3f, 0.08f).SetEase(Ease.OutQuad));
                bounceSeq.Append(bounceIcon.DOScale(Vector3.one, 0.08f).SetEase(Ease.InQuad));
            }

            charge += amount;

            if (charge >= 1f)
            {
                chargeSlider.DOValue(1f, 0.2f);
                if (availableUnits == 0) filledImage.DOFillAmount(1f, 0.2f);

                yield return new WaitForSeconds(0.2f);

                while (charge >= 1f)
                {
                    availableUnits++;
                    charge -= 1f;
                }
                UpdateUIText();
                filledImage.fillAmount = 1f;
                chargeSlider.DOValue(0f, 0.2f).SetEase(Ease.InOutQuad);
                
                yield return new WaitForSeconds(0.2f);
            }

            chargeSlider.DOValue(charge, 0.2f);

            if (availableUnits == 0)
            {
                filledImage.DOFillAmount(charge, 0.2f);
            }

            yield return new WaitForSeconds(0.2f);
            animating = false;
        }

        private void UpdateUIText()
        {
            if (unitCountText != null) unitCountText.text = availableUnits.ToString();
        }
        #endregion

        #region Drag & Drop
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (availableUnits <= 0) return;

            IsDraggingUnit = true;

            if (UnitBoard.Instance != null)
            {
                UnitBoard.Instance.ToggleGridVisibility(true);

                Unit unit = unitPrefab.GetComponent<Unit>();
                float range = unit != null ? unit.AttackRange : 2f;
                UnitBoard.Instance.SetHoverRange(range);
            }

            if (previewPrefab != null)
            {
                previewInstance = Instantiate(previewPrefab);
                previewInstance.SetActive(false);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (availableUnits <= 0) return;

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
                        canSpawn = true;
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

        public void OnEndDrag(PointerEventData eventData)
        {
            IsDraggingUnit = false;

            if (UnitBoard.Instance != null) UnitBoard.Instance.ToggleGridVisibility(false);

            if (activeBoard != null)
            {
                activeBoard.ClearHover();
                activeBoard = null;
            }

            if (availableUnits <= 0)
            {
                if (previewInstance != null) Destroy(previewInstance);
                return;
            }

            if (canSpawn)
            {
                Instantiate(unitPrefab, spawnPos, Quaternion.identity);
                availableUnits--;
                UpdateUIText();

                if (availableUnits == 0)
                {
                    filledImage.DOFillAmount(charge, 0.3f).SetEase(Ease.OutQuad);
                }
            }

            if (previewInstance != null) Destroy(previewInstance);
            canSpawn = false;
        }
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