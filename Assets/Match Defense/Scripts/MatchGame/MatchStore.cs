using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public class MatchStore : MonoBehaviour
{
    [System.Serializable]
    public class UnitData
    {
        public int typeIndex;
        public int maxPoint;
        public Sprite cardSprite;
        public Sprite backSprite;
        public GameObject unitPrefab;
    }

    public List<UnitData> unitList;
    public VisualTreeAsset cardTemplate;

    [Header("Camera Setup")]
    public Camera uiCamera;
    public Camera worldCamera;

    [Header("Store Settings")]
    public float storeBottomPosition = 400f;
    public Vector2 cardSize = new Vector2(100f, 140f);

    [Header("3D Summon Settings")]
    public LayerMask groundLayer;
    public string groundTag = "Ground";

    [Header("Summon Check & Preview")]
    public LayerMask unitLayer;
    public float checkRadius = 0.5f;
    public float dummyHeight = 0.8f;

    private PanelRenderer panelRenderer;
    private GameObject dragDummy;
    private UnitData draggingUnit;
    private int currentPointerId = -1;

    private bool isValidSpawnPosition = false;
    private Vector3 validSpawnPoint;

    private class CardNode
    {
        public int currentPoint = 0;
        public int cardCount = 0;
        public UnitData data;
        public VisualElement root;
        public VisualElement fillContainer;
        public VisualElement fillImage;
        public Label countLabel;
    }

    private Dictionary<int, CardNode> cardDict = new Dictionary<int, CardNode>();

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDisable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        root.Clear();
        cardDict.Clear();

        VisualElement storeContainer = new VisualElement();
        storeContainer.style.position = Position.Absolute;
        storeContainer.style.bottom = storeBottomPosition;
        storeContainer.style.width = Length.Percent(100);
        storeContainer.style.flexDirection = FlexDirection.Row;
        storeContainer.style.justifyContent = Justify.Center;
        storeContainer.style.alignItems = Align.FlexEnd;
        root.Add(storeContainer);

        foreach (var unit in unitList)
        {
            TemplateContainer templateInstance = cardTemplate.Instantiate();
            CardNode node = new CardNode { data = unit };

            node.root = templateInstance.Q<VisualElement>("CardRoot");
            node.fillContainer = templateInstance.Q<VisualElement>("FillContainer");
            node.fillImage = templateInstance.Q<VisualElement>("FillImage");
            node.countLabel = templateInstance.Q<Label>("CountLabel");

            templateInstance.style.width = cardSize.x;
            templateInstance.style.height = cardSize.y;
            templateInstance.style.marginRight = 10;
            templateInstance.style.marginLeft = 10;

            node.root.style.position = Position.Relative;
            node.root.style.width = Length.Percent(100);
            node.root.style.height = Length.Percent(100);
            node.root.style.backgroundImage = new StyleBackground(unit.backSprite);

            node.fillContainer.style.position = Position.Absolute;
            node.fillContainer.style.bottom = 0;
            node.fillContainer.style.width = Length.Percent(100);
            node.fillContainer.style.height = Length.Percent(0);
            node.fillContainer.style.overflow = Overflow.Hidden;

            node.fillImage.style.position = Position.Absolute;
            node.fillImage.style.bottom = 0;
            node.fillImage.style.left = 0;
            node.fillImage.style.width = cardSize.x;
            node.fillImage.style.height = cardSize.y;
            node.fillImage.style.backgroundImage = new StyleBackground(unit.cardSprite);

            node.countLabel.text = "0";

            node.root.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(evt, node));
            node.root.RegisterCallback<PointerMoveEvent>(evt => OnPointerMove(evt, node));
            node.root.RegisterCallback<PointerUpEvent>(evt => OnPointerUp(evt, node));
            node.root.RegisterCallback<PointerCancelEvent>(evt => OnPointerCancel(evt, node));
            node.root.RegisterCallback<PointerCaptureOutEvent>(evt =>
                OnPointerCaptureOut(evt, node)
            );

            storeContainer.Add(templateInstance);
            cardDict.Add(unit.typeIndex, node);
        }
    }

    public float GetStoreBottomWorldY()
    {
        if (uiCamera == null || uiCamera.pixelHeight <= 0)
            return transform.position.y;

        float camHeight = uiCamera.pixelHeight;
        float camWidth = uiCamera.pixelWidth;
        float uiScale = 1f;

        PanelRenderer pr = GetComponent<PanelRenderer>();
        if (
            pr != null
            && pr.panelSettings != null
            && pr.panelSettings.scaleMode == PanelScaleMode.ScaleWithScreenSize
        )
        {
            Vector2 refRes = pr.panelSettings.referenceResolution;
            float match = pr.panelSettings.match;
            float scaleX = camWidth / refRes.x;
            float scaleY = camHeight / refRes.y;
            uiScale = Mathf.Lerp(scaleX, scaleY, match);
        }

        float scaledBottom = storeBottomPosition * uiScale;
        float zDepth = Mathf.Abs(uiCamera.transform.position.z);
        return uiCamera.ScreenToWorldPoint(new Vector3(camWidth / 2f, scaledBottom, zDepth)).y;
    }

    public void AddPoints(int typeIndex, int points)
    {
        if (!cardDict.TryGetValue(typeIndex, out CardNode node))
            return;

        node.currentPoint += points;

        float s = 1f;
        DOTween
            .To(
                () => s,
                x =>
                {
                    s = x;
                    node.root.style.scale = new Scale(new Vector2(x, x));
                },
                1.15f,
                0.1f
            )
            .SetLoops(2, LoopType.Yoyo);

        if (node.currentPoint >= node.data.maxPoint)
        {
            int extra = node.currentPoint - node.data.maxPoint;
            node.cardCount++;
            node.currentPoint = extra;
            node.countLabel.text = node.cardCount.ToString();

            float currentH = node.fillContainer.style.height.value.value;
            Sequence seq = DOTween.Sequence();
            seq.Append(
                DOTween.To(
                    () => currentH,
                    x =>
                    {
                        currentH = x;
                        node.fillContainer.style.height = Length.Percent(x);
                    },
                    100f,
                    0.15f
                )
            );
            seq.AppendCallback(() =>
            {
                node.fillContainer.style.height = Length.Percent(0f);
            });
            seq.Append(
                DOTween.To(
                    () => 0f,
                    x =>
                    {
                        currentH = x;
                        node.fillContainer.style.height = Length.Percent(x);
                    },
                    (node.currentPoint / (float)node.data.maxPoint) * 100f,
                    0.2f
                )
            );
        }
        else
        {
            float targetH = (node.currentPoint / (float)node.data.maxPoint) * 100f;
            float currentH = node.fillContainer.style.height.value.value;
            DOTween.To(
                () => currentH,
                x =>
                {
                    currentH = x;
                    node.fillContainer.style.height = Length.Percent(x);
                },
                targetH,
                0.2f
            );
        }
    }

    private void OnPointerDown(PointerDownEvent evt, CardNode node)
    {
        if (dragDummy != null)
            return;

        if (node.cardCount > 0)
        {
            isValidSpawnPosition = false;
            currentPointerId = evt.pointerId;
            draggingUnit = node.data;

            dragDummy = new GameObject("DragDummy");
            dragDummy.layer = LayerMask.NameToLayer("2D_Board");

            SpriteRenderer sr = dragDummy.AddComponent<SpriteRenderer>();
            sr.sprite = node.data.cardSprite;
            sr.sortingOrder = 100;

            UpdateDummyPosition();
            node.root.CapturePointer(evt.pointerId);
        }
    }

    private void OnPointerMove(PointerMoveEvent evt, CardNode node)
    {
        if (dragDummy != null && evt.pointerId == currentPointerId)
        {
            UpdateDummyPosition();
        }
    }

    private void OnPointerUp(PointerUpEvent evt, CardNode node)
    {
        if (dragDummy != null && evt.pointerId == currentPointerId)
        {
            FinishDrag(node, evt.pointerId, true);
        }
    }

    private void OnPointerCancel(PointerCancelEvent evt, CardNode node)
    {
        if (dragDummy != null && evt.pointerId == currentPointerId)
        {
            FinishDrag(node, evt.pointerId, false);
        }
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt, CardNode node)
    {
        if (dragDummy != null && evt.pointerId == currentPointerId)
        {
            FinishDrag(node, evt.pointerId, true);
        }
    }

    private void FinishDrag(CardNode node, int pointerId, bool trySpawn)
    {
        if (dragDummy != null)
        {
            if (trySpawn && isValidSpawnPosition)
            {
                // 소환될 유닛의 위치
                Instantiate(draggingUnit.unitPrefab, validSpawnPoint, Quaternion.identity);
                node.cardCount--;
                node.countLabel.text = node.cardCount.ToString();
            }

            Destroy(dragDummy);
            dragDummy = null;
            draggingUnit = null;
            currentPointerId = -1;
            isValidSpawnPosition = false;

            node.root.ReleasePointer(pointerId);
        }
    }

    private void UpdateDummyPosition()
    {
        if (dragDummy == null || uiCamera == null || worldCamera == null)
            return;

        Vector3 screenPixelPos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();

        if (
            UnityEngine.InputSystem.Touchscreen.current != null
            && UnityEngine.InputSystem.Touchscreen.current.touches.Count > 0
        )
        {
            foreach (var touch in UnityEngine.InputSystem.Touchscreen.current.touches)
            {
                if (touch.press.isPressed)
                {
                    screenPixelPos = touch.position.ReadValue();
                    break;
                }
            }
        }

        Ray ray = worldCamera.ScreenPointToRay(screenPixelPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            if (hit.collider.CompareTag(groundTag))
            {
                // 💡 [핵심 1] 겹침 방지: 바닥에서 살짝 위를 중심으로 구(Sphere)를 생성해 유닛이 있는지 검사합니다.
                Vector3 checkCenter = hit.point + Vector3.up * checkRadius;
                bool isBlocked = Physics.CheckSphere(checkCenter, checkRadius, unitLayer);

                if (!isBlocked)
                {
                    // 비어있음 = 설치 가능
                    isValidSpawnPosition = true;
                    validSpawnPoint = hit.point;
                    dragDummy.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 1f);
                }
                else
                {
                    // 막혀있음 = 땅 위라도 설치 불가 (빨간색)
                    isValidSpawnPosition = false;
                    dragDummy.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0f, 0.5f);
                }

                // 💡 [핵심 2] 땅 뚫림 방지: dummyHeight 만큼 Y축으로 띄워줍니다.
                dragDummy.transform.position = hit.point + (Vector3.up * dummyHeight);
                dragDummy.transform.forward = worldCamera.transform.forward;
                dragDummy.layer = LayerMask.NameToLayer("Default");
                return;
            }
        }

        isValidSpawnPosition = false;

        float defaultZDepth = Mathf.Abs(uiCamera.transform.position.z);
        Vector3 worldPos = uiCamera.ScreenToWorldPoint(
            new Vector3(screenPixelPos.x, screenPixelPos.y, defaultZDepth)
        );
        dragDummy.transform.position = worldPos;
        dragDummy.transform.forward = uiCamera.transform.forward;
        dragDummy.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0f, 0.5f);
        dragDummy.layer = LayerMask.NameToLayer("2D_Board");
    }

#if UNITY_EDITOR
    // (OnDrawGizmos 로직은 이전과 동일하므로 길이상 생략하지 않고 그대로 두시면 됩니다!)
    private void OnDrawGizmos()
    {
        if (uiCamera == null || !uiCamera.orthographic)
            return;
        float camHeight = uiCamera.pixelHeight;
        float camWidth = uiCamera.pixelWidth;
        if (camHeight <= 0)
            return;

        float zDepth = Mathf.Abs(uiCamera.transform.position.z);
        float uiScale = 1f;

        PanelRenderer pr = GetComponent<PanelRenderer>();
        if (pr != null && pr.panelSettings != null)
        {
            if (pr.panelSettings.scaleMode == PanelScaleMode.ScaleWithScreenSize)
            {
                Vector2 refRes = pr.panelSettings.referenceResolution;
                float match = pr.panelSettings.match;
                float scaleX = camWidth / refRes.x;
                float scaleY = camHeight / refRes.y;
                uiScale = Mathf.Lerp(scaleX, scaleY, match);
            }
        }

        float scaledBottom = storeBottomPosition * uiScale;
        float scaledCardWidth = cardSize.x * uiScale;
        float scaledCardHeight = cardSize.y * uiScale;
        float scaledMargin = 10f * uiScale;

        Vector3 bottomWorld = uiCamera.ScreenToWorldPoint(
            new Vector3(camWidth / 2f, scaledBottom, zDepth)
        );
        Vector3 topWorld = uiCamera.ScreenToWorldPoint(
            new Vector3(camWidth / 2f, scaledBottom + scaledCardHeight, zDepth)
        );
        Vector3 rightWorld = uiCamera.ScreenToWorldPoint(
            new Vector3(camWidth / 2f + scaledCardWidth, scaledBottom, zDepth)
        );
        Vector3 marginWorld = uiCamera.ScreenToWorldPoint(
            new Vector3(camWidth / 2f + scaledMargin, scaledBottom, zDepth)
        );

        float wHeight = topWorld.y - bottomWorld.y;
        float wWidth = rightWorld.x - bottomWorld.x;
        float wMargin = marginWorld.x - bottomWorld.x;

        int count = unitList != null ? unitList.Count : 0;

        if (count > 0)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.7f);
            float totalW = (wWidth + wMargin * 2) * count;
            float startX = uiCamera.transform.position.x - (totalW / 2f) + (wWidth / 2f) + wMargin;
            float centerY = bottomWorld.y + (wHeight / 2f);

            for (int i = 0; i < count; i++)
            {
                Vector3 center = new Vector3(startX + i * (wWidth + wMargin * 2), centerY, 0f);
                Gizmos.DrawWireCube(center, new Vector3(wWidth, wHeight, 0f));
            }
        }

        Gizmos.color = Color.red;
        Vector3 lineStart = uiCamera.ScreenToWorldPoint(new Vector3(0, scaledBottom, zDepth));
        Vector3 lineEnd = uiCamera.ScreenToWorldPoint(new Vector3(camWidth, scaledBottom, zDepth));
        Gizmos.DrawLine(lineStart, lineEnd);
    }
#endif
}
