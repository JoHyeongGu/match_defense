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

    [Header("Store Settings")]
    public float fieldMinY = 0f;
    public float storeBottomPosition = 400f;
    public Vector2 cardSize = new Vector2(100f, 140f);

    private PanelRenderer panelRenderer;
    private GameObject dragDummy;
    private UnitData draggingUnit;

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

            storeContainer.Add(templateInstance);
            cardDict.Add(unit.typeIndex, node);
        }
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
        if (node.cardCount > 0)
        {
            draggingUnit = node.data;
            dragDummy = new GameObject("DragDummy");
            SpriteRenderer sr = dragDummy.AddComponent<SpriteRenderer>();
            sr.sprite = node.data.cardSprite;
            sr.sortingOrder = 100;
            UpdateDummyPosition(evt.position);

            node.root.CapturePointer(evt.pointerId);
        }
    }

    private void OnPointerMove(PointerMoveEvent evt, CardNode node)
    {
        if (dragDummy != null)
        {
            UpdateDummyPosition(evt.position);
        }
    }

    private void OnPointerUp(PointerUpEvent evt, CardNode node)
    {
        if (dragDummy != null)
        {
            Vector3 dropPos = dragDummy.transform.position;
            Destroy(dragDummy);

            if (dropPos.y >= fieldMinY)
            {
                Instantiate(draggingUnit.unitPrefab, dropPos, Quaternion.identity);
                node.cardCount--;
                node.countLabel.text = node.cardCount.ToString();
            }

            node.root.ReleasePointer(evt.pointerId);
            draggingUnit = null;
        }
    }

    private void UpdateDummyPosition(Vector2 screenPos)
    {
        Vector2 invertedYPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(invertedYPos);
        worldPos.z = 0;
        dragDummy.transform.position = worldPos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Camera.main == null || !Camera.main.orthographic)
            return;

        float camHeight = Camera.main.pixelHeight;
        float camWidth = Camera.main.pixelWidth;
        if (camHeight <= 0)
            return;

        float zDepth = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 bottomWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(camWidth / 2f, storeBottomPosition, zDepth)
        );
        Vector3 topWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(camWidth / 2f, storeBottomPosition + cardSize.y, zDepth)
        );
        Vector3 rightWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(camWidth / 2f + cardSize.x, storeBottomPosition, zDepth)
        );
        Vector3 marginWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(camWidth / 2f + 10f, storeBottomPosition, zDepth)
        );

        float wHeight = topWorld.y - bottomWorld.y;
        float wWidth = rightWorld.x - bottomWorld.x;
        float wMargin = marginWorld.x - bottomWorld.x;

        int count = unitList != null ? unitList.Count : 0;

        if (count > 0)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.7f); 
            float totalW = (wWidth + wMargin * 2) * count;
            float startX =
                Camera.main.transform.position.x - (totalW / 2f) + (wWidth / 2f) + wMargin;
            float centerY = bottomWorld.y + (wHeight / 2f);

            for (int i = 0; i < count; i++)
            {
                Vector3 center = new Vector3(startX + i * (wWidth + wMargin * 2), centerY, 0f);
                Gizmos.DrawWireCube(center, new Vector3(wWidth, wHeight, 0f));
            }
        }

        Gizmos.color = Color.red;
        Vector3 lineStart = Camera.main.ScreenToWorldPoint(
            new Vector3(0, storeBottomPosition, zDepth)
        );
        Vector3 lineEnd = Camera.main.ScreenToWorldPoint(
            new Vector3(camWidth, storeBottomPosition, zDepth)
        );
        Gizmos.DrawLine(lineStart, lineEnd);

        Gizmos.color = Color.green;
        Vector3 fieldMinStart = new Vector3(
            Camera.main.ScreenToWorldPoint(new Vector3(0, 0, zDepth)).x,
            fieldMinY,
            0f
        );
        Vector3 fieldMinEnd = new Vector3(
            Camera.main.ScreenToWorldPoint(new Vector3(camWidth, 0, zDepth)).x,
            fieldMinY,
            0f
        );
        Gizmos.DrawLine(fieldMinStart, fieldMinEnd);
    }
#endif
}
