using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Match
{
    [Serializable]
    public struct ItemVisual
    {
        public Sprite defaultSprite;
        public Sprite[] mashSprites;
    }

    [UxmlElement]
    public partial class MatchBoard : VisualElement
    {
        private const string BoardClass = "match-board";
        private const string CellClass = "match-cell";
        private const string CellTypeAClass = "cell-type-a";
        private const string CellTypeBClass = "cell-type-b";
        private const string ItemClass = "match-item";

        private int widthCount = 6;
        private int heightCount = 5;
        private float cellSpacing = 0.06f;

        [UxmlAttribute]
        public int WidthCount
        {
            get => widthCount;
            set { widthCount = Mathf.Max(1, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute]
        public int HeightCount
        {
            get => heightCount;
            set { heightCount = Mathf.Max(1, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute]
        public float CellSpacing
        {
            get => cellSpacing;
            set { cellSpacing = Mathf.Max(0f, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute] public float HintDelay { get; set; } = 3f;
        [UxmlAttribute] public int InitialPoolSize { get; set; } = 64;

        public bool IsMatchActive { get; private set; } = true;

        private readonly VisualElement cellLayer;
        private readonly VisualElement itemLayer;
        private readonly List<VisualElement> backgroundCells = new List<VisualElement>();

        private MonoBehaviour coroutineRunner;
        private MatchUnitData[] unitDatas;
        private Action<float, int> onMatchHaptic;
        private Action<int, int> onItemMatched;
        private Func<bool> canDragCheck;
        private Action onDragBlocked;
        private Action onTurnConsumed;

        private float itemSize;
        private float CellStride => itemSize * (1f + cellSpacing);
        private bool isInitialized;

        public MatchBoard()
        {
            AddToClassList(BoardClass);

            cellLayer = CreateLayer("match-board-cells");
            itemLayer = CreateLayer("match-board-items");
            Add(cellLayer);
            Add(itemLayer);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => ResetHint());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(OnPointerUp);
            schedule.Execute(OnUpdate).Every(16);
            for (int i = 0; i < 20; i++) matchGroups.Add(new List<MatchItemElement>(16));
        }

        private VisualElement CreateLayer(string layerName)
        {
            var layer = new VisualElement { name = layerName, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            return layer;
        }

        public void Initialize(
            MonoBehaviour runner,
            MatchUnitData[] datas,
            Action<float, int> hapticCallback,
            Action<int, int> matchCallback,
            Func<bool> canDragCallback = null,
            Action dragBlockedCallback = null,
            Action turnConsumedCallback = null)
        {
            coroutineRunner = runner;
            unitDatas = datas;
            onMatchHaptic = hapticCallback;
            onItemMatched = matchCallback;
            canDragCheck = canDragCallback;
            onDragBlocked = dragBlockedCallback;
            onTurnConsumed = turnConsumedCallback;

            CalculateTotalWeight();
            isInitialized = true;

            if (itemSize > 0) BuildBoard();
        }

        public void SetMatchActive(bool isActive)
        {
            IsMatchActive = isActive;
            if (!isActive)
            {
                DeselectNode();
                ResetHint();
            }
        }

        #region Layout
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateBoardLayout();
            if (isInitialized && boardItems == null && itemSize > 0) BuildBoard();
        }

        private void UpdateBoardLayout()
        {
            if (float.IsNaN(layout.width) || float.IsNaN(layout.height) ||
                layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            float denomX = widthCount + Mathf.Max(0, widthCount - 1) * cellSpacing;
            float denomY = heightCount + Mathf.Max(0, heightCount - 1) * cellSpacing;
            itemSize = Mathf.Max(0f, Mathf.Min(layout.width / denomX, layout.height / denomY));

            RefreshGridCells();
            RepositionActiveItems();
        }

        private void RefreshGridCells()
        {
            if (itemSize <= 0f) return;

            int totalCells = widthCount * heightCount;

            while (backgroundCells.Count < totalCells)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.AddToClassList(CellClass);
                cell.style.position = Position.Absolute;
                cellLayer.Add(cell);
                backgroundCells.Add(cell);
            }

            while (backgroundCells.Count > totalCells)
            {
                int last = backgroundCells.Count - 1;
                cellLayer.Remove(backgroundCells[last]);
                backgroundCells.RemoveAt(last);
            }

            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    VisualElement cell = backgroundCells[GetIndex(x, y)];
                    Vector2 pos = GetCellPosition(x, y);
                    cell.style.left = pos.x;
                    cell.style.top = pos.y;
                    cell.style.width = itemSize;
                    cell.style.height = itemSize;

                    bool isTypeA = (x + y) % 2 == 0;
                    cell.EnableInClassList(CellTypeAClass, isTypeA);
                    cell.EnableInClassList(CellTypeBClass, !isTypeA);
                }
            }
        }

        private void RepositionActiveItems()
        {
            if (boardItems == null || boardItems.Length != widthCount * heightCount) return;

            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    MatchItemElement item = GetItem(x, y);
                    if (item != null)
                    {
                        item.SetSize(itemSize);
                        item.SnapTo(GetCellPosition(x, y));
                    }
                }
            }
        }

        private Vector2 GetCellPosition(int x, int y) => new Vector2(x * CellStride, y * CellStride);
        #endregion

        #region Board & Pooling
        private MatchItemElement[] boardItems;
        private Queue<MatchItemElement> itemPool;

        private void BuildBoard()
        {
            if (unitDatas == null || unitDatas.Length == 0) return;

            boardItems = new MatchItemElement[WidthCount * HeightCount];
            itemPool = new Queue<MatchItemElement>(InitialPoolSize);

            int maxAttempts = 50;
            bool boardValid = false;

            while (!boardValid && maxAttempts > 0)
            {
                for (int y = 0; y < HeightCount; y++)
                {
                    for (int x = 0; x < WidthCount; x++)
                    {
                        int index = GetIndex(x, y);
                        MatchItemElement newItem = boardItems[index];

                        if (newItem == null)
                        {
                            newItem = GetItemFromPool(GetCellPosition(x, y));
                            boardItems[index] = newItem;
                        }

                        newItem.ChangeType(GetRandomTypeWithoutMatch(x, y));
                    }
                }
                boardValid = CheckPossibleMoves();
                maxAttempts--;
            }
        }

        private MatchItemElement GetItemFromPool(Vector2 spawnPos)
        {
            MatchItemElement item;
            if (itemPool.Count > 0)
            {
                item = itemPool.Dequeue();
                item.SetSize(itemSize);
            }
            else
            {
                item = new MatchItemElement(itemSize, unitDatas);
                itemLayer.Add(item);
            }

            item.style.display = DisplayStyle.Flex;
            item.SnapTo(spawnPos);
            return item;
        }

        private void ReturnItemToPool(MatchItemElement item)
        {
            item.style.display = DisplayStyle.None;
            itemPool.Enqueue(item);
        }

        private int GetIndex(int x, int y) => y * WidthCount + x;

        private MatchItemElement GetItem(int x, int y)
        {
            if (x < 0 || x >= WidthCount || y < 0 || y >= HeightCount) return null;
            return boardItems[GetIndex(x, y)];
        }
        #endregion

        #region Input
        private int selectedX = -1;
        private int selectedY = -1;
        private bool isPointerDown;

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!IsMatchActive || isProcessing || !isInitialized || boardItems == null) return;

            if (canDragCheck != null && !canDragCheck())
            {
                onDragBlocked?.Invoke();
                return;
            }

            ResetHint();
            isPointerDown = true;
            this.CapturePointer(evt.pointerId);

            if (GetGridIndex(evt.localPosition, out int x, out int y))
            {
                MatchItemElement item = GetItem(x, y);
                if (item != null)
                {
                    selectedX = x;
                    selectedY = y;
                    item.Hover(true);
                }
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!IsMatchActive || isProcessing || selectedX == -1 || !isPointerDown) return;

            if (GetGridIndex(evt.localPosition, out int x, out int y))
            {
                if (x != selectedX || y != selectedY)
                {
                    if (Mathf.Abs(x - selectedX) + Mathf.Abs(y - selectedY) == 1)
                    {
                        GetItem(selectedX, selectedY)?.Hover(false);
                        coroutineRunner.StartCoroutine(SwapRoutine(selectedX, selectedY, x, y));
                        selectedX = -1;
                        selectedY = -1;
                    }
                }
            }
        }

        private void OnPointerUp(PointerEventBase<PointerUpEvent> evt) => HandlePointerRelease(evt.pointerId);
        private void OnPointerUp(PointerEventBase<PointerLeaveEvent> evt) => HandlePointerRelease(evt.pointerId);

        private void HandlePointerRelease(int pointerId)
        {
            isPointerDown = false;
            this.ReleasePointer(pointerId);
            DeselectNode();
        }

        private void DeselectNode()
        {
            if (selectedX != -1 && selectedY != -1)
            {
                GetItem(selectedX, selectedY)?.Hover(false);
                selectedX = -1;
                selectedY = -1;
            }
        }

        private bool GetGridIndex(Vector2 localPos, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (CellStride <= 0f) return false;

            x = Mathf.FloorToInt(localPos.x / CellStride);
            y = Mathf.FloorToInt(localPos.y / CellStride);
            return x >= 0 && x < WidthCount && y >= 0 && y < HeightCount;
        }
        #endregion

        #region Core Logic
        private readonly HashSet<MatchItemElement> matchedSet = new HashSet<MatchItemElement>();
        private readonly List<List<MatchItemElement>> matchGroups = new List<List<MatchItemElement>>();
        private readonly Queue<MatchItemElement> bfsQueue = new Queue<MatchItemElement>();
        private readonly List<MatchItemElement> shuffleBuffer = new List<MatchItemElement>(64);
        private int activeGroupCount;
        private bool isProcessing;

        private IEnumerator Wait(float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                yield return null;
        }

        private IEnumerator SwapRoutine(int x1, int y1, int x2, int y2)
        {
            isProcessing = true;

            MatchItemElement item1 = GetItem(x1, y1);
            MatchItemElement item2 = GetItem(x2, y2);

            boardItems[GetIndex(x1, y1)] = item2;
            boardItems[GetIndex(x2, y2)] = item1;

            Vector2 pos1 = GetCellPosition(x1, y1);
            Vector2 pos2 = GetCellPosition(x2, y2);

            item1?.SwapTo(pos2);
            item2?.SwapTo(pos1);

            yield return Wait(0.25f);

            FindMatches();

            if (matchedSet.Count > 0)
            {
                onTurnConsumed?.Invoke();
                ProcessMatches(item1, item2);
                yield return Wait(0.7f);
                yield return coroutineRunner.StartCoroutine(CollapseAndRefillRoutine());
            }
            else
            {
                boardItems[GetIndex(x1, y1)] = item1;
                boardItems[GetIndex(x2, y2)] = item2;
                item1?.SwapTo(pos1);
                item2?.SwapTo(pos2);
                yield return Wait(0.25f);
                isProcessing = false;
            }
        }

        private void FindMatches()
        {
            matchedSet.Clear();
            for (int y = 0; y < HeightCount; y++)
            {
                for (int x = 0; x < WidthCount - 2; x++)
                {
                    MatchItemElement i1 = GetItem(x, y);
                    MatchItemElement i2 = GetItem(x + 1, y);
                    MatchItemElement i3 = GetItem(x + 2, y);

                    if (i1 != null && i2 != null && i3 != null &&
                        i1.ItemType == i2.ItemType && i2.ItemType == i3.ItemType)
                    {
                        matchedSet.Add(i1); matchedSet.Add(i2); matchedSet.Add(i3);
                    }
                }
            }
            for (int x = 0; x < WidthCount; x++)
            {
                for (int y = 0; y < HeightCount - 2; y++)
                {
                    MatchItemElement i1 = GetItem(x, y);
                    MatchItemElement i2 = GetItem(x, y + 1);
                    MatchItemElement i3 = GetItem(x, y + 2);

                    if (i1 != null && i2 != null && i3 != null &&
                        i1.ItemType == i2.ItemType && i2.ItemType == i3.ItemType)
                    {
                        matchedSet.Add(i1); matchedSet.Add(i2); matchedSet.Add(i3);
                    }
                }
            }
        }

        private void ProcessMatches(MatchItemElement swapped1, MatchItemElement swapped2)
        {
            activeGroupCount = 0;
            for (int i = 0; i < matchGroups.Count; i++) matchGroups[i].Clear();

            float neighborDistSqr = (CellStride * 1.5f) * (CellStride * 1.5f);

            foreach (var item in matchedSet)
            {
                if (IsItemInAnyGroup(item)) continue;

                if (activeGroupCount >= matchGroups.Count) matchGroups.Add(new List<MatchItemElement>(16));
                List<MatchItemElement> currentGroup = matchGroups[activeGroupCount];

                bfsQueue.Clear();
                bfsQueue.Enqueue(item);
                currentGroup.Add(item);

                while (bfsQueue.Count > 0)
                {
                    MatchItemElement curr = bfsQueue.Dequeue();
                    foreach (var neighbor in matchedSet)
                    {
                        if (currentGroup.Contains(neighbor)) continue;
                        if (curr.ItemType == neighbor.ItemType && (curr.BoardPos - neighbor.BoardPos).sqrMagnitude < neighborDistSqr)
                        {
                            currentGroup.Add(neighbor);
                            bfsQueue.Enqueue(neighbor);
                        }
                    }
                }
                activeGroupCount++;
            }

            onMatchHaptic?.Invoke(0.25f, activeGroupCount);

            for (int i = 0; i < activeGroupCount; i++)
            {
                List<MatchItemElement> group = matchGroups[i];
                MatchItemElement center = group.Contains(swapped1) ? swapped1 : (group.Contains(swapped2) ? swapped2 : group[0]);

                int totalToMerge = group.Count - 1;
                int mergedCount = 0;
                int groupSize = group.Count;

                if (totalToMerge <= 0)
                {
                    onItemMatched?.Invoke(center.ItemType, groupSize);
                    center.PlayMatchEffect(() => ReturnItemToPool(center));
                    continue;
                }

                for (int j = 0; j < group.Count; j++)
                {
                    MatchItemElement item = group[j];
                    if (item != center)
                    {
                        item.MoveAndMerge(center.BoardPos, 0.25f, () =>
                        {
                            ReturnItemToPool(item);
                            mergedCount++;
                            if (mergedCount >= totalToMerge)
                            {
                                onItemMatched?.Invoke(center.ItemType, groupSize);
                                center.PlayMatchEffect(() => ReturnItemToPool(center));
                            }
                        });
                    }
                }
            }

            for (int y = 0; y < HeightCount; y++)
            {
                for (int x = 0; x < WidthCount; x++)
                {
                    if (matchedSet.Contains(GetItem(x, y))) boardItems[GetIndex(x, y)] = null;
                }
            }
        }

        private IEnumerator CollapseAndRefillRoutine()
        {
            for (int x = 0; x < WidthCount; x++)
            {
                int emptyCount = 0;
                for (int y = HeightCount - 1; y >= 0; y--)
                {
                    MatchItemElement item = GetItem(x, y);
                    if (item == null) emptyCount++;
                    else if (emptyCount > 0)
                    {
                        boardItems[GetIndex(x, y + emptyCount)] = item;
                        boardItems[GetIndex(x, y)] = null;
                        item.SwapTo(GetCellPosition(x, y + emptyCount));
                    }
                }

                for (int y = 0; y < emptyCount; y++)
                {
                    Vector2 startPos = GetCellPosition(x, y - emptyCount);
                    Vector2 endPos = GetCellPosition(x, y);

                    MatchItemElement newItem = GetItemFromPool(startPos);
                    newItem.ChangeType(GetRandomTypeWeighted());
                    boardItems[GetIndex(x, y)] = newItem;

                    newItem.schedule.Execute(() => newItem.SwapTo(endPos)).StartingIn(20);
                }
            }
            yield return Wait(0.4f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItemElement firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;

                ProcessMatches(firstMatched, firstMatched);
                yield return Wait(0.7f);
                yield return coroutineRunner.StartCoroutine(CollapseAndRefillRoutine());
            }
            else
            {
                if (!CheckPossibleMoves()) yield return coroutineRunner.StartCoroutine(ShuffleRoutine());
                else isProcessing = false;
            }
        }

        private IEnumerator ShuffleRoutine()
        {
            isProcessing = true;
            shuffleBuffer.Clear();
            for (int y = 0; y < HeightCount; y++)
                for (int x = 0; x < WidthCount; x++)
                    if (GetItem(x, y) != null) shuffleBuffer.Add(GetItem(x, y));

            for (int i = 0; i < shuffleBuffer.Count; i++)
            {
                int rand = UnityEngine.Random.Range(i, shuffleBuffer.Count);
                (shuffleBuffer[i], shuffleBuffer[rand]) = (shuffleBuffer[rand], shuffleBuffer[i]);
            }

            int idx = 0;
            for (int y = 0; y < HeightCount; y++)
            {
                for (int x = 0; x < WidthCount; x++)
                {
                    if (GetItem(x, y) != null)
                    {
                        MatchItemElement item = shuffleBuffer[idx++];
                        boardItems[GetIndex(x, y)] = item;
                        item.SwapTo(GetCellPosition(x, y));
                    }
                }
            }
            yield return Wait(0.4f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItemElement firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;
                ProcessMatches(firstMatched, firstMatched);
                yield return Wait(0.7f);
                yield return coroutineRunner.StartCoroutine(CollapseAndRefillRoutine());
            }
            else if (!CheckPossibleMoves()) yield return coroutineRunner.StartCoroutine(ShuffleRoutine());
            else isProcessing = false;
        }
        #endregion

        #region Utility
        private float totalWeight;

        private bool CheckPossibleMoves() => GetPossibleMove(out _, out _, out _, out _);

        private bool GetPossibleMove(out int outX1, out int outY1, out int outX2, out int outY2)
        {
            outX1 = outY1 = outX2 = outY2 = -1;
            if (boardItems == null) return false;

            for (int y = 0; y < HeightCount; y++)
            {
                for (int x = 0; x < WidthCount; x++)
                {
                    if (GetItem(x, y) == null) continue;

                    if (x < WidthCount - 1 && GetItem(x + 1, y) != null)
                    {
                        SwapInArray(x, y, x + 1, y);
                        bool hasMatch = CheckMatchAt(x, y) || CheckMatchAt(x + 1, y);
                        SwapInArray(x, y, x + 1, y);
                        if (hasMatch) { outX1 = x; outY1 = y; outX2 = x + 1; outY2 = y; return true; }
                    }
                    if (y < HeightCount - 1 && GetItem(x, y + 1) != null)
                    {
                        SwapInArray(x, y, x, y + 1);
                        bool hasMatch = CheckMatchAt(x, y) || CheckMatchAt(x, y + 1);
                        SwapInArray(x, y, x, y + 1);
                        if (hasMatch) { outX1 = x; outY1 = y; outX2 = x; outY2 = y + 1; return true; }
                    }
                }
            }
            return false;
        }

        private void SwapInArray(int x1, int y1, int x2, int y2)
        {
            int idx1 = GetIndex(x1, y1);
            int idx2 = GetIndex(x2, y2);
            (boardItems[idx1], boardItems[idx2]) = (boardItems[idx2], boardItems[idx1]);
        }

        private bool CheckMatchAt(int x, int y)
        {
            MatchItemElement item = GetItem(x, y);
            if (item == null) return false;
            int type = item.ItemType;
            int count = 1;
            for (int i = x - 1; i >= 0; i--) { if (GetItem(i, y)?.ItemType == type) count++; else break; }
            for (int i = x + 1; i < WidthCount; i++) { if (GetItem(i, y)?.ItemType == type) count++; else break; }
            if (count >= 3) return true;
            count = 1;
            for (int j = y - 1; j >= 0; j--) { if (GetItem(x, j)?.ItemType == type) count++; else break; }
            for (int j = y + 1; j < HeightCount; j++) { if (GetItem(x, j)?.ItemType == type) count++; else break; }
            return count >= 3;
        }

        private void CalculateTotalWeight()
        {
            totalWeight = 0f;
            if (unitDatas == null) return;
            for (int i = 0; i < unitDatas.Length; i++) totalWeight += unitDatas[i].spawnWeight;
        }

        private int GetRandomTypeWeighted()
        {
            if (unitDatas == null || unitDatas.Length == 0 || totalWeight <= 0f) return 0;
            float randomVal = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;
            for (int i = 0; i < unitDatas.Length; i++)
            {
                currentSum += unitDatas[i].spawnWeight;
                if (randomVal <= currentSum) return i;
            }
            return unitDatas.Length - 1;
        }

        private int GetRandomTypeWithoutMatch(int x, int y)
        {
            int type = 0;
            for (int i = 0; i < 15; i++)
            {
                type = GetRandomTypeWeighted();
                bool matchX = (x >= 2 && GetItem(x - 1, y)?.ItemType == type && GetItem(x - 2, y)?.ItemType == type);
                bool matchY = (y >= 2 && GetItem(x, y - 1)?.ItemType == type && GetItem(x, y - 2)?.ItemType == type);
                if (!matchX && !matchY) break;
            }
            return type;
        }

        private bool IsItemInAnyGroup(MatchItemElement item)
        {
            for (int i = 0; i < activeGroupCount; i++)
                if (matchGroups[i].Contains(item)) return true;
            return false;
        }
        #endregion

        #region Hint
        private float idleTimer;
        private MatchItemElement currentHintItem;
        private Sequence hintSeq;

        private void OnUpdate()
        {
            if (!IsMatchActive || !isInitialized) return;

            if (canDragCheck != null && !canDragCheck())
            {
                ResetHint();
                return;
            }

            if (!isProcessing && selectedX == -1)
            {
                idleTimer += Time.unscaledDeltaTime;
                if (idleTimer >= HintDelay && currentHintItem == null) ShowHint();
            }
            else
            {
                ResetHint();
            }
        }

        private void ShowHint()
        {
            if (GetPossibleMove(out int x1, out int y1, out int x2, out int y2))
            {
                currentHintItem = GetItem(x1, y1);
                if (currentHintItem == null) return;

                Vector2 moveDir = (GetCellPosition(x2, y2) - currentHintItem.BoardPos).normalized * (itemSize * 0.3f);
                hintSeq = DOTween.Sequence().SetUpdate(true);
                hintSeq.Append(DOVirtual.Vector2(Vector2.zero, moveDir, 0.15f, v => currentHintItem.style.translate = new Translate(v.x, v.y)).SetEase(Ease.OutQuad));
                hintSeq.Append(DOVirtual.Vector2(moveDir, Vector2.zero, 0.15f, v => currentHintItem.style.translate = new Translate(v.x, v.y)).SetEase(Ease.InQuad));
                hintSeq.AppendInterval(1.0f);
                hintSeq.SetLoops(-1);
            }
        }

        private void ResetHint()
        {
            idleTimer = 0f;
            if (currentHintItem != null)
            {
                hintSeq?.Kill();
                hintSeq = null;
                currentHintItem.style.translate = new Translate(0f, 0f);
                currentHintItem.style.scale = new Scale(Vector3.one);
                currentHintItem = null;
            }
        }
        #endregion

        #region Item Element
        private class MatchItemElement : VisualElement
        {
            private static readonly StyleList<StylePropertyName> TransitionProps = new StyleList<StylePropertyName>(new List<StylePropertyName>
            {
                new StylePropertyName("left"), new StylePropertyName("top"), new StylePropertyName("scale")
            });
            private static readonly StyleList<TimeValue> DefaultTransitionDurations = new StyleList<TimeValue>(new List<TimeValue>
            {
                new TimeValue(0.25f, TimeUnit.Second), new TimeValue(0.25f, TimeUnit.Second), new TimeValue(0.1f, TimeUnit.Second)
            });
            private static readonly StyleList<TimeValue> SnapTransitionDurations = new StyleList<TimeValue>(new List<TimeValue>
            {
                new TimeValue(0f), new TimeValue(0f), new TimeValue(0.1f, TimeUnit.Second)
            });
            private static readonly StyleList<EasingFunction> TransitionEasings = new StyleList<EasingFunction>(new List<EasingFunction>
            {
                new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseInOut)
            });

            public int ItemType { get; private set; }
            public Vector2 BoardPos { get; private set; }

            private readonly MatchUnitData[] unitDatas;
            private readonly Action restoreTransitionAction;
            private IVisualElementScheduledItem stopMotionTask;

            public MatchItemElement(float size, MatchUnitData[] datas)
            {
                unitDatas = datas;
                restoreTransitionAction = () => style.transitionDuration = DefaultTransitionDurations;

                AddToClassList(ItemClass);
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                SetSize(size);
                style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

                style.transitionProperty = TransitionProps;
                style.transitionDuration = DefaultTransitionDurations;
                style.transitionTimingFunction = TransitionEasings;
            }

            public void SetSize(float size)
            {
                style.width = size;
                style.height = size;
            }

            public void ChangeType(int typeIndex)
            {
                ItemType = typeIndex;
                style.backgroundImage = new StyleBackground(unitDatas[typeIndex].itemVisual.defaultSprite);
            }

            public void SwapTo(Vector2 targetPos)
            {
                BoardPos = targetPos;
                style.left = targetPos.x;
                style.top = targetPos.y;
            }

            public void SnapTo(Vector2 targetPos)
            {
                BoardPos = targetPos;
                style.transitionDuration = SnapTransitionDurations;
                style.left = targetPos.x;
                style.top = targetPos.y;
                schedule.Execute(restoreTransitionAction).StartingIn(10);
            }

            public void Hover(bool isHover)
            {
                if (isHover)
                {
                    BringToFront();
                    style.scale = new Scale(new Vector3(1.15f, 1.15f, 1f));
                    style.top = BoardPos.y - (resolvedStyle.height * 0.15f);
                }
                else
                {
                    style.scale = new Scale(Vector3.one);
                    style.top = BoardPos.y;
                }
            }

            public void MoveAndMerge(Vector2 targetPos, float duration, Action onComplete)
            {
                style.left = targetPos.x;
                style.top = targetPos.y;
                schedule.Execute(() => onComplete?.Invoke()).StartingIn((long)(duration * 1000));
            }

            public void PlayMatchEffect(Action onComplete)
            {
                BringToFront();
                var frames = unitDatas[ItemType].itemVisual.mashSprites;
                if (frames == null || frames.Length == 0) { onComplete?.Invoke(); return; }

                int frameIndex = 0;
                stopMotionTask = schedule.Execute(() =>
                {
                    style.backgroundImage = new StyleBackground(frames[frameIndex]);
                    frameIndex = (frameIndex + 1) % frames.Length;
                }).Every((long)(1000f / 12f));

                schedule.Execute(() =>
                {
                    stopMotionTask?.Pause();
                    style.scale = new Scale(Vector3.zero);
                    schedule.Execute(() =>
                    {
                        style.scale = new Scale(Vector3.one);
                        style.backgroundImage = new StyleBackground(unitDatas[ItemType].itemVisual.defaultSprite);
                        onComplete?.Invoke();
                    }).StartingIn(150);
                }).StartingIn(400);
            }
        }
        #endregion
    }
}