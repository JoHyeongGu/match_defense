using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Match
{
    public class MatchManager : MonoBehaviour
    {
        [Header("<color=yellow>UI Board Settings</color>")]
        [Tooltip("매치 아이템들이 배치될 빈 UI 패널 (RectTransform)")]
        [SerializeField] private RectTransform boardContainer;
        [SerializeField] private MatchItem itemPrefab;
        [SerializeField] private int widthCount = 6;
        [SerializeField] private int heightCount = 6;

        [Header("<color=yellow>Item Spawn Probabilities</color>")]
        [Tooltip("각 아이템 인덱스별 등장 가중치. 값이 높을수록 잘 나옵니다.")]
        [SerializeField] private float[] itemTypeWeights = new float[] { 1f, 1f, 1f, 1f, 1f }; // 기본 5개, 동일 확률

        [Header("<color=yellow>Optimization</color>")]
        [SerializeField] private int initialPoolSize = 64;

        private int selectedX = -1;
        private int selectedY = -1;
        private bool isProcessing;
        private MatchItem[] boardItems;
        private Queue<MatchItem> itemPool;

        private float itemSize;
        private Vector2 boardStartPos;
        private Vector2 containerBottomLeft;

        private HashSet<MatchItem> matchedSet = new HashSet<MatchItem>();
        private List<List<MatchItem>> matchGroups = new List<List<MatchItem>>();
        private Queue<MatchItem> bfsQueue = new Queue<MatchItem>();
        private int activeGroupCount = 0;

        // 가중치 누적 계산용
        private float _totalWeight;

        #region Unity
        private void Awake()
        {
            DOTween.Init(true, true, LogBehaviour.Default).SetCapacity(500, 50);
            for (int i = 0; i < 20; i++) matchGroups.Add(new List<MatchItem>(16));

            CalculateTotalWeight();
            InitPool();
        }

        private void Start()
        {
            InitBoard();
            ProgramManager.Instance?.FadeScreen(off: false);
        }

        private void Update()
        {
            if (ProgramManager.Instance != null && ProgramManager.Instance.IsLoading) return;
            HandleInput();
        }

        private void OnValidate()
        {
            // Inspector에서 값이 바뀔 때마다 총 가중치를 재계산 (에디터 편의성)
            CalculateTotalWeight();
        }
        #endregion

        #region Input
        private void HandleInput()
        {
            if (Pointer.current == null || isProcessing) return;

            Vector2 pointerPos = Pointer.current.position.ReadValue();

            if (Pointer.current.press.wasPressedThisFrame)
            {
                SelectNode(pointerPos);
            }
            else if (Pointer.current.press.isPressed && selectedX != -1)
            {
                TryDragNode(pointerPos);
            }
            else if (Pointer.current.press.wasReleasedThisFrame)
            {
                DeselectNode();
            }
        }

        private void SelectNode(Vector2 screenPos)
        {
            if (GetGridIndex(screenPos, out int x, out int y))
            {
                MatchItem item = GetItem(x, y);
                if (item != null)
                {
                    selectedX = x;
                    selectedY = y;
                    item.Hover(true);
                }
            }
        }

        private void TryDragNode(Vector2 screenPos)
        {
            if (GetGridIndex(screenPos, out int x, out int y))
            {
                if (x != selectedX || y != selectedY)
                {
                    int dx = Mathf.Abs(x - selectedX);
                    int dy = Mathf.Abs(y - selectedY);

                    if (dx + dy == 1) // 상하좌우 1칸 스왑
                    {
                        StartCoroutine(SwapRoutine(selectedX, selectedY, x, y));
                        selectedX = -1;
                        selectedY = -1;
                    }
                }
            }
        }

        private void DeselectNode()
        {
            if (selectedX != -1 && selectedY != -1)
            {
                MatchItem item = GetItem(selectedX, selectedY);
                if (item != null) item.Hover(false);
                selectedX = -1;
                selectedY = -1;
            }
        }

        private bool GetGridIndex(Vector2 screenPos, out int x, out int y)
        {
            x = -1; y = -1;
            if (boardContainer == null) return false;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardContainer, screenPos, null, out Vector2 localPoint))
            {
                Vector2 pointFromBottomLeft = localPoint - containerBottomLeft;

                float offsetX = boardStartPos.x - (itemSize / 2f);
                float offsetY = boardStartPos.y - (itemSize / 2f);

                x = Mathf.FloorToInt((pointFromBottomLeft.x - offsetX) / itemSize);
                y = Mathf.FloorToInt((pointFromBottomLeft.y - offsetY) / itemSize);

                if (x >= 0 && x < widthCount && y >= 0 && y < heightCount) return true;
            }
            return false;
        }
        #endregion

        #region Pooling
        private void InitPool()
        {
            itemPool = new Queue<MatchItem>(initialPoolSize);
            for (int i = 0; i < initialPoolSize; i++)
            {
                MatchItem item = Instantiate(itemPrefab, boardContainer);
                item.gameObject.SetActive(false);
                itemPool.Enqueue(item);
            }
        }

        private MatchItem GetItemFromPool(Vector2 anchoredPosition)
        {
            MatchItem item;
            if (itemPool.Count > 0) item = itemPool.Dequeue();
            else item = Instantiate(itemPrefab, boardContainer);

            item.RectTr.anchorMin = Vector2.zero;
            item.RectTr.anchorMax = Vector2.zero;
            item.RectTr.pivot = new Vector2(0.5f, 0.5f);

            item.SetSize(itemSize);
            item.RectTr.anchoredPosition = anchoredPosition;
            item.gameObject.SetActive(true);
            return item;
        }

        public void ReturnItemToPool(MatchItem item)
        {
            if (!item.gameObject.activeSelf) return;

            item.RectTr.DOKill();
            item.StopAllCoroutines();

            item.gameObject.SetActive(false);
            itemPool.Enqueue(item);
        }
        #endregion

        #region Board Layout
        private void CalculateBoardLayout()
        {
            if (boardContainer == null) return;

            float maxItemWidth = boardContainer.rect.width / widthCount;
            float maxItemHeight = boardContainer.rect.height / heightCount;
            itemSize = Mathf.Min(maxItemWidth, maxItemHeight);

            float boardWidth = itemSize * widthCount;
            float boardHeight = itemSize * heightCount;
            float offsetX = (boardContainer.rect.width - boardWidth) / 2f;
            float offsetY = (boardContainer.rect.height - boardHeight) / 2f;

            boardStartPos = new Vector2(offsetX + (itemSize / 2f), offsetY + (itemSize / 2f));
            containerBottomLeft = boardContainer.rect.min;
        }

        private Vector2 GetAnchoredPosition(int x, int y)
        {
            return boardStartPos + new Vector2(x * itemSize, y * itemSize);
        }

        private void InitBoard()
        {
            CalculateBoardLayout();
            if (boardItems == null) boardItems = new MatchItem[widthCount * heightCount];

            int maxAttempts = 50;
            bool boardValid = false;

            while (!boardValid && maxAttempts > 0)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    for (int x = 0; x < widthCount; x++)
                    {
                        int index = GetIndex(x, y);
                        MatchItem newItem = boardItems[index];

                        if (newItem == null)
                        {
                            Vector2 spawnPos = GetAnchoredPosition(x, y);
                            newItem = GetItemFromPool(spawnPos);
                            newItem.BoardPos = spawnPos;
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

        // [신규] 총 가중치 계산
        private void CalculateTotalWeight()
        {
            _totalWeight = 0f;
            foreach (float weight in itemTypeWeights)
            {
                _totalWeight += weight;
            }
        }

        // [신규] 가중치 기반 랜덤 타입 선택
        private int GetRandomTypeWeighted()
        {
            if (_totalWeight <= 0f) return 0;

            float randomVal = UnityEngine.Random.Range(0f, _totalWeight);
            float currentSum = 0f;

            for (int i = 0; i < itemTypeWeights.Length; i++)
            {
                currentSum += itemTypeWeights[i];
                if (randomVal <= currentSum)
                {
                    return i;
                }
            }
            return itemTypeWeights.Length - 1; // Fallback
        }

        private int GetRandomTypeWithoutMatch(int x, int y)
        {
            int type = 0;
            for (int i = 0; i < 15; i++) // 시도 횟수 약간 증가
            {
                type = GetRandomTypeWeighted(); // 가중치 기반 랜덤 호출
                bool matchX = (x >= 2 && GetItem(x - 1, y)?.ItemType == type && GetItem(x - 2, y)?.ItemType == type);
                bool matchY = (y >= 2 && GetItem(x, y - 1)?.ItemType == type && GetItem(x, y - 2)?.ItemType == type);
                if (!matchX && !matchY) break;
            }
            return type;
        }

        private int GetIndex(int x, int y)
        {
            return y * widthCount + x;
        }

        public MatchItem GetItem(int x, int y)
        {
            if (x < 0 || x >= widthCount || y < 0 || y >= heightCount) return null;
            return boardItems[GetIndex(x, y)];
        }
        #endregion

        #region Match Logic
        private IEnumerator SwapRoutine(int x1, int y1, int x2, int y2)
        {
            isProcessing = true;
            MatchItem item1 = GetItem(x1, y1);
            MatchItem item2 = GetItem(x2, y2);

            if (item1 != null) item1.Hover(false);
            boardItems[GetIndex(x1, y1)] = item2;
            boardItems[GetIndex(x2, y2)] = item1;

            Vector2 pos1 = GetAnchoredPosition(x1, y1);
            Vector2 pos2 = GetAnchoredPosition(x2, y2);

            bool swapFinished = false;
            item1.SwapTo(pos2, 0.25f, () => swapFinished = true);
            item2.SwapTo(pos1, 0.25f, null);

            yield return new WaitUntil(() => swapFinished);

            FindMatches();

            if (matchedSet.Count > 0)
            {
                ProcessMatches(item1, item2);
                yield return new WaitForSeconds(0.7f);
                yield return StartCoroutine(CollapseAndRefillRoutine());
            }
            else
            {
                boardItems[GetIndex(x1, y1)] = item1;
                boardItems[GetIndex(x2, y2)] = item2;

                swapFinished = false;
                item1.SwapTo(pos1, 0.25f, () => swapFinished = true);
                item2.SwapTo(pos2, 0.25f, null);

                yield return new WaitUntil(() => swapFinished);
                isProcessing = false;
            }
        }

        private void FindMatches()
        {
            matchedSet.Clear();
            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount - 2; x++)
                {
                    MatchItem i1 = GetItem(x, y);
                    MatchItem i2 = GetItem(x + 1, y);
                    MatchItem i3 = GetItem(x + 2, y);

                    if (i1 != null && i2 != null && i3 != null &&
                        i1.ItemType == i2.ItemType && i2.ItemType == i3.ItemType)
                    {
                        matchedSet.Add(i1); matchedSet.Add(i2); matchedSet.Add(i3);
                    }
                }
            }
            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount - 2; y++)
                {
                    MatchItem i1 = GetItem(x, y);
                    MatchItem i2 = GetItem(x, y + 1);
                    MatchItem i3 = GetItem(x, y + 2);

                    if (i1 != null && i2 != null && i3 != null &&
                        i1.ItemType == i2.ItemType && i2.ItemType == i3.ItemType)
                    {
                        matchedSet.Add(i1); matchedSet.Add(i2); matchedSet.Add(i3);
                    }
                }
            }
        }

        private void ProcessMatches(MatchItem swapped1, MatchItem swapped2)
        {
            StartCoroutine(DelayedHapticRoutine(0.3f));

            activeGroupCount = 0;
            foreach (var group in matchGroups) group.Clear();

            foreach (var item in matchedSet)
            {
                if (IsItemInAnyGroup(item)) continue;

                if (activeGroupCount >= matchGroups.Count) matchGroups.Add(new List<MatchItem>(16));
                List<MatchItem> currentGroup = matchGroups[activeGroupCount];

                bfsQueue.Clear();
                bfsQueue.Enqueue(item);
                currentGroup.Add(item);

                while (bfsQueue.Count > 0)
                {
                    MatchItem curr = bfsQueue.Dequeue();
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

            for (int i = 0; i < activeGroupCount; i++)
            {
                List<MatchItem> group = matchGroups[i];
                MatchItem center = group.Contains(swapped1) ? swapped1 : (group.Contains(swapped2) ? swapped2 : group[0]);

                float mergeDuration = 0.25f;
                int mergedCount = 0;
                int totalToMerge = group.Count - 1;

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
                        item.MoveAndMerge(center.RectTr.anchoredPosition, mergeDuration, () =>
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
            
            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    if (matchedSet.Contains(GetItem(x, y)))
                    {
                        boardItems[GetIndex(x, y)] = null;
                    }
                }
            }
        }

        private bool IsItemInAnyGroup(MatchItem item)
        {
            for (int i = 0; i < activeGroupCount; i++)
            {
                if (matchGroups[i].Contains(item)) return true;
            }
            return false;
        }

        private IEnumerator CollapseAndRefillRoutine()
        {
            for (int x = 0; x < widthCount; x++)
            {
                int emptyCount = 0;
                for (int y = 0; y < heightCount; y++)
                {
                    MatchItem item = GetItem(x, y);
                    if (item == null)
                    {
                        emptyCount++;
                    }
                    else if (emptyCount > 0)
                    {
                        boardItems[GetIndex(x, y - emptyCount)] = item;
                        boardItems[GetIndex(x, y)] = null;
                        item.SwapTo(GetAnchoredPosition(x, y - emptyCount), 0.3f);
                    }
                }
                for (int y = heightCount - emptyCount; y < heightCount; y++)
                {
                    Vector2 startPos = GetAnchoredPosition(x, heightCount + y);
                    Vector2 endPos = GetAnchoredPosition(x, y);

                    MatchItem newItem = GetItemFromPool(startPos);
                    int randomType = GetRandomTypeWeighted(); // 여기도 가중치 랜덤으로 변경
                    newItem.ChangeType(randomType);
                    newItem.BoardPos = startPos;

                    boardItems[GetIndex(x, y)] = newItem;
                    newItem.SwapTo(endPos, 0.3f);
                }
            }
            yield return new WaitForSeconds(0.4f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItem firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;

                ProcessMatches(firstMatched, firstMatched);
                yield return new WaitForSeconds(0.7f);
                yield return StartCoroutine(CollapseAndRefillRoutine());
            }
            else
            {
                if (!CheckPossibleMoves()) yield return StartCoroutine(ShuffleRoutine());
                else isProcessing = false;
            }
        }

        private bool CheckPossibleMoves()
        {
            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    if (GetItem(x, y) == null) continue;

                    if (x < widthCount - 1 && GetItem(x + 1, y) != null)
                    {
                        SwapInArray(x, y, x + 1, y);
                        bool hasMatch = CheckMatchAt(x, y) || CheckMatchAt(x + 1, y);
                        SwapInArray(x, y, x + 1, y);
                        if (hasMatch) return true;
                    }

                    if (y < heightCount - 1 && GetItem(x, y + 1) != null)
                    {
                        SwapInArray(x, y, x, y + 1);
                        bool hasMatch = CheckMatchAt(x, y) || CheckMatchAt(x, y + 1);
                        SwapInArray(x, y, x, y + 1);
                        if (hasMatch) return true;
                    }
                }
            }
            return false;
        }

        private void SwapInArray(int x1, int y1, int x2, int y2)
        {
            int idx1 = GetIndex(x1, y1);
            int idx2 = GetIndex(x2, y2);
            MatchItem temp = boardItems[idx1];
            boardItems[idx1] = boardItems[idx2];
            boardItems[idx2] = temp;
        }

        private bool CheckMatchAt(int x, int y)
        {
            MatchItem item = GetItem(x, y);
            if (item == null) return false;
            int type = item.ItemType;

            int count = 1;
            for (int i = x - 1; i >= 0; i--) { if (GetItem(i, y)?.ItemType == type) count++; else break; }
            for (int i = x + 1; i < widthCount; i++) { if (GetItem(i, y)?.ItemType == type) count++; else break; }
            if (count >= 3) return true;

            count = 1;
            for (int j = y - 1; j >= 0; j--) { if (GetItem(x, j)?.ItemType == type) count++; else break; }
            for (int j = y + 1; j < heightCount; j++) { if (GetItem(x, j)?.ItemType == type) count++; else break; }
            if (count >= 3) return true;

            return false;
        }

        private IEnumerator ShuffleRoutine()
        {
            isProcessing = true;

            List<MatchItem> activeItems = new List<MatchItem>();
            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    MatchItem item = GetItem(x, y);
                    if (item != null) activeItems.Add(item);
                }
            }

            for (int i = 0; i < activeItems.Count; i++)
            {
                int rand = UnityEngine.Random.Range(i, activeItems.Count);
                MatchItem temp = activeItems[i];
                activeItems[i] = activeItems[rand];
                activeItems[rand] = temp;
            }

            bool swapFinished = false;
            int idx = 0;

            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    if (GetItem(x, y) != null)
                    {
                        MatchItem item = activeItems[idx];
                        boardItems[GetIndex(x, y)] = item;

                        Vector2 targetPos = GetAnchoredPosition(x, y);

                        if (idx == activeItems.Count - 1) item.SwapTo(targetPos, 0.5f, () => swapFinished = true);
                        else item.SwapTo(targetPos, 0.5f, null);

                        idx++;
                    }
                }
            }

            yield return new WaitUntil(() => swapFinished);
            yield return new WaitForSeconds(0.1f);

            FindMatches();
            if (matchedSet.Count > 0)
            {
                MatchItem firstMatched = null;
                var enumerator = matchedSet.GetEnumerator();
                if (enumerator.MoveNext()) firstMatched = enumerator.Current;

                ProcessMatches(firstMatched, firstMatched);
                yield return StartCoroutine(CollapseAndRefillRoutine());
            }
            else if (!CheckPossibleMoves()) yield return StartCoroutine(ShuffleRoutine());
            else isProcessing = false;
        }
        #endregion

        #region Haptic
        // 유니티 빌더가 안드로이드 VIBRATE 권한을 넣도록 유도하는 더미 코드 (절대 지우지 마세요!)
        private void ForceVibrationPermissionDummy()
        {
            Handheld.Vibrate();
        }

        // 0.5초 딜레이를 주기 위한 코루틴 함수
        private IEnumerator DelayedHapticRoutine(float delayTime)
        {
            // 지정한 시간(0.5초)만큼 대기
            yield return new WaitForSeconds(delayTime);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                    {
                        // [핵심] 150ms 동안 울리되, 세기(Amplitude)를 50으로 아주 약하게 설정 (최대 255)
                        // 이렇게 하면 날카로운 '틱!'이 아니라 부드럽고 약한 '지이잉~' 느낌이 납니다.
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
                // 구형 안드로이드 기기에서 세기 조절 API가 안 먹힐 경우를 대비한 안전 장치
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
    }
}