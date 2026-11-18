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
        [Tooltip("평상시 보여질 기본 스프라이트")]
        public Sprite defaultSprite;
        [Tooltip("매치될 때 재생될 스톱모션 스프라이트 배열")]
        public Sprite[] mashSprites;
    }

    [UxmlElement]
    public partial class MatchBoard : VisualElement
    {
        [UxmlAttribute] public int WidthCount { get; set; } = 6;
        [UxmlAttribute] public int HeightCount { get; set; } = 6;
        [UxmlAttribute] public float HintDelay { get; set; } = 3f;
        [UxmlAttribute] public int InitialPoolSize { get; set; } = 64;

        public bool IsMatchActive { get; private set; } = true;

        private MonoBehaviour coroutineRunner;
        private ItemVisual[] visualTypes;
        private float[] itemTypeWeights;
        private Action<float, int> onMatchHaptic;

        private MatchItemElement[] boardItems;
        private Queue<MatchItemElement> itemPool;
        private float itemSize;

        private int selectedX = -1;
        private int selectedY = -1;
        private bool isProcessing;
        private bool isPointerDown = false;
        private bool isInitialized = false;

        private HashSet<MatchItemElement> matchedSet = new HashSet<MatchItemElement>();
        private List<List<MatchItemElement>> matchGroups = new List<List<MatchItemElement>>();
        private Queue<MatchItemElement> bfsQueue = new Queue<MatchItemElement>();
        private int activeGroupCount = 0;
        private float totalWeight;

        private float idleTimer = 0f;
        private MatchItemElement currentHintItem = null;
        private Sequence hintSeq;
        private Vector2 hintOriginalPos;

        // 생성자 (UI Builder에서 배치되는 순간 실행됨)
        public MatchBoard()
        {
            // 이 위젯 자체의 이벤트를 수신
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(OnPointerUp); // 영역을 벗어나면 드래그 취소

            // 프레임 업데이트 스케줄러 (힌트 시스템용)
            schedule.Execute(OnUpdate).Every(16);

            for (int i = 0; i < 20; i++) matchGroups.Add(new List<MatchItemElement>(16));
        }

        /// <summary>
        /// MatchPlayManager에서 외부 데이터(스프라이트 등)를 주입할 때 호출
        /// </summary>
        public void Initialize(MonoBehaviour runner, ItemVisual[] visuals, float[] weights, Action<float, int> hapticCallback)
        {
            coroutineRunner = runner;
            visualTypes = visuals;
            itemTypeWeights = weights;
            onMatchHaptic = hapticCallback;

            CalculateTotalWeight();
            isInitialized = true;

            // 크기 계산이 완료되어 있다면 바로 보드 생성
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

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // 위젯의 크기(레이아웃)가 변경되면 아이템 크기를 재계산
            float maxItemWidth = layout.width / WidthCount;
            float maxItemHeight = layout.height / HeightCount;
            itemSize = Mathf.Min(maxItemWidth, maxItemHeight);

            if (isInitialized && boardItems == null)
            {
                BuildBoard();
            }
        }

        private void OnUpdate()
        {
            if (!IsMatchActive || !isInitialized) return;

            if (!isProcessing && selectedX == -1)
            {
                idleTimer += Time.deltaTime;
                if (idleTimer >= HintDelay && currentHintItem == null)
                {
                    ShowHint();
                }
            }
            else
            {
                ResetHint();
            }
        }

        #region Board & Pooling
        private void BuildBoard()
        {
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
                        Vector2 spawnPos = new Vector2(x * itemSize, y * itemSize);

                        if (newItem == null)
                        {
                            newItem = GetItemFromPool(spawnPos);
                            boardItems[index] = newItem;
                        }

                        int randomType = GetRandomTypeWithoutMatch(x, y);
                        newItem.ChangeType(randomType);
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
            }
            else
            {
                item = new MatchItemElement(itemSize, visualTypes);
                Add(item); // 위젯 내부에 자식 요소로 추가
            }

            item.style.display = DisplayStyle.Flex;
            item.SnapTo(spawnPos); // 수정됨: SwapTo 대신 SnapTo 호출
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

        #region Input Handling
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!IsMatchActive || isProcessing || !isInitialized) return;

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
                    int dx = Mathf.Abs(x - selectedX);
                    int dy = Mathf.Abs(y - selectedY);

                    if (dx + dy == 1)
                    {
                        GetItem(selectedX, selectedY)?.Hover(false);
                        coroutineRunner.StartCoroutine(SwapRoutine(selectedX, selectedY, x, y));
                        selectedX = -1;
                        selectedY = -1;
                    }
                }
            }
        }

        private void OnPointerUp(PointerEventBase<PointerUpEvent> evt) { HandlePointerRelease(evt.pointerId); }
        private void OnPointerUp(PointerEventBase<PointerLeaveEvent> evt) { HandlePointerRelease(evt.pointerId); }

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
            x = Mathf.FloorToInt(localPos.x / itemSize);
            y = Mathf.FloorToInt(localPos.y / itemSize);
            return x >= 0 && x < WidthCount && y >= 0 && y < HeightCount;
        }
        #endregion

        #region Core Match Logic
        private IEnumerator SwapRoutine(int x1, int y1, int x2, int y2)
        {
            isProcessing = true;

            MatchItemElement item1 = GetItem(x1, y1);
            MatchItemElement item2 = GetItem(x2, y2);

            boardItems[GetIndex(x1, y1)] = item2;
            boardItems[GetIndex(x2, y2)] = item1;

            Vector2 pos1 = new Vector2(x1 * itemSize, y1 * itemSize);
            Vector2 pos2 = new Vector2(x2 * itemSize, y2 * itemSize);

            item1?.SwapTo(pos2);
            item2?.SwapTo(pos1);

            yield return new WaitForSeconds(0.25f);

            FindMatches();

            if (matchedSet.Count > 0)
            {
                ProcessMatches(item1, item2);
                yield return new WaitForSeconds(0.7f);
                yield return coroutineRunner.StartCoroutine(CollapseAndRefillRoutine());
            }
            else
            {
                // 역스왑
                boardItems[GetIndex(x1, y1)] = item1;
                boardItems[GetIndex(x2, y2)] = item2;
                item1?.SwapTo(pos1);
                item2?.SwapTo(pos2);
                yield return new WaitForSeconds(0.25f);
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
            foreach (var group in matchGroups) group.Clear();

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
                        if (curr.ItemType == neighbor.ItemType)
                        {
                            float dist = Vector2.Distance(curr.BoardPos, neighbor.BoardPos);
                            if (dist < itemSize * 1.5f)
                            {
                                currentGroup.Add(neighbor);
                                bfsQueue.Enqueue(neighbor);
                            }
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

                if (totalToMerge <= 0)
                {
                    if (Defense.UnitSpawnManager.Instance != null)
                        Defense.UnitSpawnManager.Instance.AddCharge(center.ItemType, group.Count);
                    center.PlayMatchEffect(() => ReturnItemToPool(center));
                    continue;
                }

                foreach (var item in group)
                {
                    if (item != center)
                    {
                        item.MoveAndMerge(center.BoardPos, 0.25f, () =>
                        {
                            ReturnItemToPool(item);
                            mergedCount++;
                            if (mergedCount >= totalToMerge)
                            {
                                if (Defense.UnitSpawnManager.Instance != null)
                                    Defense.UnitSpawnManager.Instance.AddCharge(center.ItemType, group.Count);
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
                        item.SwapTo(new Vector2(x * itemSize, (y + emptyCount) * itemSize));
                    }
                }

                for (int y = 0; y < emptyCount; y++)
                {
                    Vector2 startPos = new Vector2(x * itemSize, (y - emptyCount) * itemSize);
                    Vector2 endPos = new Vector2(x * itemSize, y * itemSize);

                    MatchItemElement newItem = GetItemFromPool(startPos);
                    newItem.ChangeType(GetRandomTypeWeighted());
                    boardItems[GetIndex(x, y)] = newItem;

                    // 수정됨: 엔진이 순간이동을 인식할 20ms 틈을 준 뒤 목적지로 낙하 시작
                    newItem.schedule.Execute(() =>
                    {
                        newItem.SwapTo(endPos);
                    }).StartingIn(20);
                }
            }
            yield return new WaitForSeconds(0.4f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItemElement firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;

                ProcessMatches(firstMatched, firstMatched);
                yield return new WaitForSeconds(0.7f);
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
            List<MatchItemElement> activeItems = new List<MatchItemElement>();
            for (int y = 0; y < HeightCount; y++)
                for (int x = 0; x < WidthCount; x++)
                    if (GetItem(x, y) != null) activeItems.Add(GetItem(x, y));

            for (int i = 0; i < activeItems.Count; i++)
            {
                int rand = UnityEngine.Random.Range(i, activeItems.Count);
                (activeItems[i], activeItems[rand]) = (activeItems[rand], activeItems[i]);
            }

            int idx = 0;
            for (int y = 0; y < HeightCount; y++)
            {
                for (int x = 0; x < WidthCount; x++)
                {
                    if (GetItem(x, y) != null)
                    {
                        MatchItemElement item = activeItems[idx];
                        boardItems[GetIndex(x, y)] = item;
                        item.SwapTo(new Vector2(x * itemSize, y * itemSize));
                        idx++;
                    }
                }
            }
            yield return new WaitForSeconds(0.4f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItemElement firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;
                ProcessMatches(firstMatched, firstMatched);
                yield return new WaitForSeconds(0.7f);
                yield return coroutineRunner.StartCoroutine(CollapseAndRefillRoutine());
            }
            else if (!CheckPossibleMoves()) yield return coroutineRunner.StartCoroutine(ShuffleRoutine());
            else isProcessing = false;
        }
        #endregion

        #region Utility & Logic Checks
        private bool CheckPossibleMoves() => GetPossibleMove(out _, out _, out _, out _);

        private bool GetPossibleMove(out int outX1, out int outY1, out int outX2, out int outY2)
        {
            outX1 = outY1 = outX2 = outY2 = -1;
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
            foreach (float w in itemTypeWeights) totalWeight += w;
        }

        private int GetRandomTypeWeighted()
        {
            if (totalWeight <= 0f) return 0;
            float randomVal = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;
            for (int i = 0; i < itemTypeWeights.Length; i++)
            {
                currentSum += itemTypeWeights[i];
                if (randomVal <= currentSum) return i;
            }
            return itemTypeWeights.Length - 1;
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

        #region Hint Animation
        private void ShowHint()
        {
            if (GetPossibleMove(out int x1, out int y1, out int x2, out int y2))
            {
                currentHintItem = GetItem(x1, y1);
                if (currentHintItem == null) return;
                hintOriginalPos = currentHintItem.BoardPos;
                Vector2 moveDir = (new Vector2(x2 * itemSize, y2 * itemSize) - hintOriginalPos).normalized * (itemSize * 0.3f);
                hintSeq = DOTween.Sequence();
                hintSeq.Append(DOVirtual.Vector2(hintOriginalPos, hintOriginalPos + moveDir, 0.15f, v => { currentHintItem.style.left = v.x; currentHintItem.style.top = v.y; }).SetEase(Ease.OutQuad));
                hintSeq.Append(DOVirtual.Vector2(hintOriginalPos + moveDir, hintOriginalPos, 0.15f, v => { currentHintItem.style.left = v.x; currentHintItem.style.top = v.y; }).SetEase(Ease.InQuad));
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
                currentHintItem.style.left = hintOriginalPos.x;
                currentHintItem.style.top = hintOriginalPos.y;
                currentHintItem.style.scale = new Scale(Vector3.one);
                currentHintItem = null;
            }
        }
        #endregion

        // ==============================================================================
        // 내부 비공개 클래스 (MatchItemElement 코드를 위젯 안으로 완전히 숨김)
        // ==============================================================================
        private class MatchItemElement : VisualElement
        {
            public int ItemType { get; private set; }
            public Vector2 BoardPos { get; private set; }
            private ItemVisual[] visualTypes;
            private IVisualElementScheduledItem stopMotionTask;

            public MatchItemElement(float size, ItemVisual[] visuals)
            {
                visualTypes = visuals;
                style.position = Position.Absolute;
                style.width = size;
                style.height = size;
                style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

                style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> {
                    new StylePropertyName("left"), new StylePropertyName("top"), new StylePropertyName("scale")
                });
                style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> {
                    new TimeValue(0.25f, TimeUnit.Second), new TimeValue(0.25f, TimeUnit.Second), new TimeValue(0.1f, TimeUnit.Second)
                });
                style.transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction> {
                    new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseInOut)
                });
            }

            public void ChangeType(int typeIndex)
            {
                ItemType = typeIndex;
                style.backgroundImage = new StyleBackground(visualTypes[typeIndex].defaultSprite);
            }

            public void SwapTo(Vector2 targetPos)
            {
                BoardPos = targetPos;
                style.left = targetPos.x;
                style.top = targetPos.y;
            }

            // 추가됨: 이동 트랜지션을 0초로 강제 설정하여 즉시 이동시킴
            public void SnapTo(Vector2 targetPos)
            {
                BoardPos = targetPos;

                style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> {
                    new TimeValue(0),
                    new TimeValue(0),
                    new TimeValue(0.1f, TimeUnit.Second) // 스케일(크기) 애니메이션은 유지
                });

                style.left = targetPos.x;
                style.top = targetPos.y;

                // 10ms 뒤에 다시 원래 트랜지션 시간 복구
                schedule.Execute(() =>
                {
                    style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> {
                        new TimeValue(0.25f, TimeUnit.Second),
                        new TimeValue(0.25f, TimeUnit.Second),
                        new TimeValue(0.1f, TimeUnit.Second)
                    });
                }).StartingIn(10);
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
                var frames = visualTypes[ItemType].mashSprites;
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
                        style.backgroundImage = new StyleBackground(visualTypes[ItemType].defaultSprite);
                        onComplete?.Invoke();
                    }).StartingIn(150);
                }).StartingIn(400);
            }
        }
    }
}