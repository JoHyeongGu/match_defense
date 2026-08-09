using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Match
{
    public class MatchManager : MonoBehaviour
    {
        [Header("<color=yellow>Board Settings</color>")]
        [SerializeField] private MatchItem itemPrefab;
        [SerializeField] private int widthCount = 6;
        [SerializeField] private int heightCount = 6;
        [SerializeField] private int itemTypeCount = 5;
        [SerializeField, Range(0.1f, 1f)] private float boardWidthRatio = 0.85f;
        [SerializeField] private float bottomPixelOffset = 150f;

        [Header("<color=yellow>Optimization</color>")]
        [SerializeField] private int initialPoolSize = 64;

        private Camera mainCam;
        private int selectedX = -1;
        private int selectedY = -1;
        private bool isProcessing;
        private MatchItem[] boardItems;
        private Queue<MatchItem> itemPool;
        private Vector3 boardStartPos;
        private Plane boardPlane;
        private float currentItemSpacing;

        private HashSet<MatchItem> matchedSet = new HashSet<MatchItem>();
        private List<List<MatchItem>> matchGroups = new List<List<MatchItem>>();
        private Queue<MatchItem> bfsQueue = new Queue<MatchItem>();
        private int activeGroupCount = 0;

        #region Unity
        private void Awake()
        {
            DOTween.Init(true, true, LogBehaviour.Default).SetCapacity(500, 50);
            mainCam = Camera.main;
            for (int i = 0; i < 20; i++) matchGroups.Add(new List<MatchItem>(16));
            InitPool();
        }

        private void Start()
        {
            InitBoard();
            if (ProgramManager.Instance != null)
                ProgramManager.Instance.FinishLoading();
        }

        private void Update()
        {
            if (ProgramManager.Instance != null && ProgramManager.Instance.IsLoading) return;
            HandleInput();
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

                    // 상하좌우 1칸 움직였을 때만 스왑 발동
                    if (dx + dy == 1)
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
            Ray ray = mainCam.ScreenPointToRay(screenPos);
            if (boardPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                x = Mathf.RoundToInt((hitPoint.x - boardStartPos.x) / currentItemSpacing);
                y = Mathf.RoundToInt((hitPoint.z - boardStartPos.z) / currentItemSpacing);

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
                MatchItem item = Instantiate(itemPrefab, transform);
                item.gameObject.SetActive(false);
                itemPool.Enqueue(item);
            }
        }

        private MatchItem GetItemFromPool(Vector3 position)
        {
            MatchItem item;
            if (itemPool.Count > 0)
            {
                item = itemPool.Dequeue();
            }
            else
            {
                item = Instantiate(itemPrefab, transform);
            }

            item.transform.position = position;
            item.gameObject.SetActive(true);
            return item;
        }

        public void ReturnItemToPool(MatchItem item)
        {
            if (!item.gameObject.activeSelf) return;

            item.transform.DOKill();
            item.StopAllCoroutines();

            item.gameObject.SetActive(false);
            itemPool.Enqueue(item);
        }
        #endregion

        #region Board
        private void CalculateBoardLayout(Camera cam)
        {
            if (cam == null) return;

            boardPlane = new Plane(Vector3.up, transform.position);

            Ray leftRay = cam.ViewportPointToRay(new Vector3(0f, 0.5f, 0f));
            Ray rightRay = cam.ViewportPointToRay(new Vector3(1f, 0.5f, 0f));

            if (boardPlane.Raycast(leftRay, out float dL) && boardPlane.Raycast(rightRay, out float dR))
            {
                float worldWidth = Vector3.Distance(leftRay.GetPoint(dL), rightRay.GetPoint(dR));
                currentItemSpacing = (worldWidth * boardWidthRatio) / widthCount;
            }

            Ray centerRay = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, bottomPixelOffset, 0f));
            if (boardPlane.Raycast(centerRay, out float dC))
            {
                boardStartPos = centerRay.GetPoint(dC) - new Vector3((widthCount - 1) * currentItemSpacing * 0.5f, 0f, 0f);
            }
        }

        private void InitBoard()
        {
            CalculateBoardLayout(mainCam);
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
                            Vector3 spawnPos = boardStartPos + new Vector3(x * currentItemSpacing, 0f, y * currentItemSpacing);
                            newItem = GetItemFromPool(spawnPos);
                            newItem.BoardPos = spawnPos;
                            boardItems[index] = newItem;
                        }

                        int randomType = GetRandomTypeWithoutMatch(x, y);
                        newItem.ChangeMesh(randomType);
                    }
                }
                boardValid = CheckPossibleMoves();
                maxAttempts--;
            }
        }

        private int GetRandomTypeWithoutMatch(int x, int y)
        {
            int type = 0;
            for (int i = 0; i < 10; i++)
            {
                type = UnityEngine.Random.Range(0, itemTypeCount);
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

            Vector3 pos1 = GetWorldPosition(x1, y1);
            Vector3 pos2 = GetWorldPosition(x2, y2);

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

        private Vector3 GetWorldPosition(int x, int y)
        {
            return boardStartPos + new Vector3(x * currentItemSpacing, 0f, y * currentItemSpacing);
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
            PlaySubtleHaptic();
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
                            float dist = Vector3.Distance(curr.transform.position, neighbor.transform.position);
                            if (dist < currentItemSpacing * 1.5f)
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
                    center.PlayMatchEffect(() => ReturnItemToPool(center));
                    continue;
                }

                foreach (var item in group)
                {
                    if (item != center)
                    {
                        item.MoveAndMerge(center.transform.position, mergeDuration, () =>
                        {
                            ReturnItemToPool(item);
                            mergedCount++;
                            if (mergedCount >= totalToMerge)
                            {
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
                        item.SwapTo(GetWorldPosition(x, y - emptyCount), 0.3f);
                    }
                }
                for (int y = heightCount - emptyCount; y < heightCount; y++)
                {
                    Vector3 startPos = GetWorldPosition(x, heightCount + y);
                    Vector3 endPos = GetWorldPosition(x, y);

                    MatchItem newItem = GetItemFromPool(startPos);
                    int randomType = UnityEngine.Random.Range(0, itemTypeCount);
                    newItem.ChangeMesh(randomType);
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
                if (!CheckPossibleMoves())
                {
                    yield return StartCoroutine(ShuffleRoutine());
                }
                else
                {
                    isProcessing = false;
                }
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

                        Vector3 targetPos = GetWorldPosition(x, y);

                        if (idx == activeItems.Count - 1)
                        {
                            item.SwapTo(targetPos, 0.5f, () => swapFinished = true);
                        }
                        else
                        {
                            item.SwapTo(targetPos, 0.5f, null);
                        }
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
            else if (!CheckPossibleMoves())
            {
                yield return StartCoroutine(ShuffleRoutine());
            }
            else
            {
                isProcessing = false;
            }
        }
        #endregion

        #region Haptic
        private void PlaySubtleHaptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", 15L, 50);
                        vibrator.Call("vibrate", effect);
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
                        vibrator.Call("vibrate", 15L);
                    }
                }
                catch { }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
        private void ForceVibratePermissionHack() { Handheld.Vibrate(); }
        #endregion

        #region Editor UI
        private void OnGUI()
        {
            if (ProgramManager.Instance != null && ProgramManager.Instance.IsLoading) return;
            if (GUI.Button(new Rect(20, 20, 200, 60), "Test Deadlock (Shuffle)"))
            {
                if (!isProcessing) StartCoroutine(ShuffleRoutine());
            }
        }
        #endregion

#if UNITY_EDITOR
        #region Gizmo
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            CalculateBoardLayout(Camera.main);

            Gizmos.color = Color.cyan;
            for (int y = 0; y < heightCount; y++)
            {
                for (int x = 0; x < widthCount; x++)
                {
                    Vector3 pos = boardStartPos + new Vector3(x * currentItemSpacing, 0f, y * currentItemSpacing);
                    Gizmos.DrawWireSphere(pos, currentItemSpacing * 0.4f);
                }
            }
        }
        #endregion
#endif
    }
}