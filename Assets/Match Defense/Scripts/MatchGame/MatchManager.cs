using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MatchDefense.MatchGame
{
    public class MatchManager : MonoBehaviour
    {
        [Header("<color=yellow>Camera</color>")]
        [SerializeField] private Camera uiCamera;

        [Header("<color=yellow>Board</color>")]
        [SerializeField] private int widthCount = 6;
        [SerializeField] private int heightCount = 6;
        [SerializeField][Range(0.5f, 1f)] private float boardWidthPercent = 0.95f;
        [SerializeField] private Vector2 spacing = new Vector2(0.1f, 0.1f);
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private Sprite[] blockSprites;

        [Header("<color=yellow>Store</color>")]
        [SerializeField] private MatchStore matchStore;
        [SerializeField] private float storeMargin = 0.3f;

        private Vector2 boardAreaSize;
        private MatchBlock[,] board;
        private MatchBlock selectedBlock;
        private bool isAnimating = false;
        private Vector2 blockSize;
        private Vector3 startPos;

        private void Start()
        {
            InitBoardData();

            board = new MatchBlock[widthCount, heightCount];
            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    SpawnBlock(x, y);
                }
            }
            CheckBoardState();
        }

        private void Update()
        {
            if (isAnimating)
                return;

            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null)
                return;

            if (pointer.press.wasPressedThisFrame)
            {
                ProcessPointerDown(pointer.position.ReadValue());
            }
            else if (pointer.press.isPressed && selectedBlock != null)
            {
                ProcessPointerMove(pointer.position.ReadValue());
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                ReleaseBlock();
            }
        }

        private void ProcessPointerDown(Vector2 screenPos)
        {
            if (uiCamera == null)
                return;

            Vector3 worldPos = uiCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, Mathf.Abs(uiCamera.transform.position.z))
            );

            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit.collider != null)
            {
                MatchBlock block = hit.collider.GetComponent<MatchBlock>();
                if (block != null)
                {
                    SelectBlock(block);
                }
            }
        }

        private void ProcessPointerMove(Vector2 screenPos)
        {
            if (uiCamera == null || selectedBlock == null)
                return;

            Vector3 worldPos = uiCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, Mathf.Abs(uiCamera.transform.position.z))
            );

            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit.collider != null)
            {
                MatchBlock block = hit.collider.GetComponent<MatchBlock>();

                if (block != null && block != selectedBlock)
                {
                    HoverBlock(block);
                }
            }
        }

        private void InitBoardData()
        {
            float totalSpacingX = Mathf.Max(0, widthCount - 1) * spacing.x;
            float totalSpacingY = Mathf.Max(0, heightCount - 1) * spacing.y;

            if (uiCamera != null)
            {
                float camWorldWidth = uiCamera.orthographicSize * 2f * uiCamera.aspect;
                boardAreaSize.x = camWorldWidth * boardWidthPercent;

                float bSizeX = (boardAreaSize.x - totalSpacingX) / widthCount;
                boardAreaSize.y = (bSizeX * heightCount) + totalSpacingY;
            }

            if (matchStore != null && uiCamera != null)
            {
                float storeBottomY = matchStore.GetStoreBottomWorldY();
                float newY = storeBottomY - storeMargin - boardAreaSize.y;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            blockSize = new Vector2(
                (boardAreaSize.x - totalSpacingX) / widthCount,
                (boardAreaSize.y - totalSpacingY) / heightCount
            );

            startPos =
                transform.position
                - new Vector3(boardAreaSize.x / 2f, 0f, 0f)
                + new Vector3(blockSize.x / 2f, blockSize.y / 2f, 0f);
        }

        public Vector3 GetWorldPosition(int x, int y)
        {
            return startPos
                + new Vector3(x * (blockSize.x + spacing.x), y * (blockSize.y + spacing.y), 0f);
        }

        private void SpawnBlock(int x, int y)
        {
            Vector3 spawnPos = GetWorldPosition(x, y);
            GameObject newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
            newBlock.transform.SetParent(transform);

            MatchBlock matchBlock = newBlock.GetComponent<MatchBlock>();
            int randomType = Random.Range(0, blockSprites.Length);

            Vector3 spriteSize = blockSprites[randomType].bounds.size;
            Vector3 scale = new Vector3(blockSize.x / spriteSize.x, blockSize.y / spriteSize.y, 1f);

            matchBlock.Setup(x, y, randomType, blockSprites[randomType], this, scale);
            board[x, y] = matchBlock;
        }

        private Vector3 GetOriginalScale(MatchBlock block)
        {
            Vector3 spriteSize = blockSprites[block.TypeIndex].bounds.size;
            return new Vector3(blockSize.x / spriteSize.x, blockSize.y / spriteSize.y, 1f);
        }

        public void SelectBlock(MatchBlock block)
        {
            if (isAnimating)
                return;
            selectedBlock = block;
            selectedBlock.transform.DOScale(GetOriginalScale(selectedBlock) * 0.8f, 0.15f);
        }

        public void HoverBlock(MatchBlock block)
        {
            if (isAnimating || selectedBlock == null || selectedBlock == block)
                return;

            if (Mathf.Abs(selectedBlock.X - block.X) + Mathf.Abs(selectedBlock.Y - block.Y) == 1)
            {
                selectedBlock.transform.DOScale(GetOriginalScale(selectedBlock), 0.15f);

                StartCoroutine(SwapAndCheck(selectedBlock, block));
                selectedBlock = null;
            }
        }

        public void ReleaseBlock()
        {
            if (selectedBlock != null)
            {
                selectedBlock.transform.DOScale(GetOriginalScale(selectedBlock), 0.15f);
                selectedBlock = null;
            }
        }

        private IEnumerator SwapAndCheck(MatchBlock block1, MatchBlock block2)
        {
            isAnimating = true;

            SwapData(block1, block2);

            block1.MoveToPosition(GetWorldPosition(block1.X, block1.Y));
            block2.MoveToPosition(GetWorldPosition(block2.X, block2.Y));

            yield return new WaitForSeconds(0.3f);

            List<MatchBlock> matchedBlocks = FindAllMatches();

            if (matchedBlocks.Count > 0)
            {
                ProcessMatches(matchedBlocks);
            }
            else
            {
                SwapData(block1, block2);
                block1.MoveToPosition(GetWorldPosition(block1.X, block1.Y));
                block2.MoveToPosition(GetWorldPosition(block2.X, block2.Y));
                yield return new WaitForSeconds(0.3f);
                isAnimating = false;
            }
        }

        private void SwapData(MatchBlock b1, MatchBlock b2)
        {
            int tempX = b1.X;
            int tempY = b1.Y;

            board[b1.X, b1.Y] = b2;
            board[b2.X, b2.Y] = b1;

            b1.UpdatePosition(b2.X, b2.Y);
            b2.UpdatePosition(tempX, tempY);
        }

        private List<MatchBlock> FindAllMatches()
        {
            List<MatchBlock> matched = new List<MatchBlock>();

            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    MatchBlock current = board[x, y];
                    if (current == null)
                        continue;

                    if (x < widthCount - 2)
                    {
                        if (
                            board[x + 1, y] != null
                            && board[x + 2, y] != null
                            && board[x + 1, y].TypeIndex == current.TypeIndex
                            && board[x + 2, y].TypeIndex == current.TypeIndex
                        )
                        {
                            if (!matched.Contains(current))
                                matched.Add(current);
                            if (!matched.Contains(board[x + 1, y]))
                                matched.Add(board[x + 1, y]);
                            if (!matched.Contains(board[x + 2, y]))
                                matched.Add(board[x + 2, y]);
                        }
                    }

                    if (y < heightCount - 2)
                    {
                        if (
                            board[x, y + 1] != null
                            && board[x, y + 2] != null
                            && board[x, y + 1].TypeIndex == current.TypeIndex
                            && board[x, y + 2].TypeIndex == current.TypeIndex
                        )
                        {
                            if (!matched.Contains(current))
                                matched.Add(current);
                            if (!matched.Contains(board[x, y + 1]))
                                matched.Add(board[x, y + 1]);
                            if (!matched.Contains(board[x, y + 2]))
                                matched.Add(board[x, y + 2]);
                        }
                    }
                }
            }
            return matched;
        }

        private void ProcessMatches(List<MatchBlock> matchedBlocks)
        {
            Dictionary<int, int> matchCounts = new Dictionary<int, int>();

            foreach (MatchBlock block in matchedBlocks)
            {
                if (matchCounts.ContainsKey(block.TypeIndex))
                {
                    matchCounts[block.TypeIndex]++;
                }
                else
                {
                    matchCounts[block.TypeIndex] = 1;
                }

                board[block.X, block.Y] = null;
                block.DestroyBlock();
            }

            if (matchStore != null)
            {
                foreach (var kvp in matchCounts)
                {
                    matchStore.AddPoints(kvp.Key, kvp.Value);
                }
            }

            StartCoroutine(DropAndRefill());
        }

        private IEnumerator DropAndRefill()
        {
            yield return new WaitForSeconds(0.25f);

            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    if (board[x, y] == null)
                    {
                        for (int dropY = y + 1; dropY < heightCount; dropY++)
                        {
                            if (board[x, dropY] != null)
                            {
                                board[x, y] = board[x, dropY];
                                board[x, dropY] = null;
                                board[x, y].UpdatePosition(x, y);
                                board[x, y].MoveToPosition(GetWorldPosition(x, y));
                                break;
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.3f);

            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    if (board[x, y] == null)
                    {
                        SpawnBlock(x, y);
                        board[x, y].transform.position = GetWorldPosition(x, heightCount + 2);
                        board[x, y].MoveToPosition(GetWorldPosition(x, y));
                    }
                }
            }

            yield return new WaitForSeconds(0.3f);
            CheckBoardState();
        }

        private void CheckBoardState()
        {
            List<MatchBlock> newMatches = FindAllMatches();
            if (newMatches.Count > 0)
            {
                ProcessMatches(newMatches);
            }
            else if (!HasPossibleMoves())
            {
                StartCoroutine(ShuffleBoard());
            }
            else
            {
                isAnimating = false;
            }
        }

        private bool HasPossibleMoves()
        {
            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    if (x < widthCount - 1)
                    {
                        if (SimulateSwap(x, y, x + 1, y))
                            return true;
                    }
                    if (y < heightCount - 1)
                    {
                        if (SimulateSwap(x, y, x, y + 1))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool SimulateSwap(int x1, int y1, int x2, int y2)
        {
            MatchBlock b1 = board[x1, y1];
            MatchBlock b2 = board[x2, y2];

            board[x1, y1] = b2;
            board[x2, y2] = b1;

            bool hasMatch = FindAllMatches().Count > 0;

            board[x1, y1] = b1;
            board[x2, y2] = b2;

            return hasMatch;
        }

        private IEnumerator ShuffleBoard()
        {
            isAnimating = true;
            yield return new WaitForSeconds(0.5f);

            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    if (board[x, y] != null)
                    {
                        board[x, y].DestroyBlock();
                        board[x, y] = null;
                    }
                }
            }

            yield return new WaitForSeconds(0.3f);

            for (int x = 0; x < widthCount; x++)
            {
                for (int y = 0; y < heightCount; y++)
                {
                    SpawnBlock(x, y);
                }
            }

            yield return new WaitForSeconds(0.1f);
            CheckBoardState();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;

            InitBoardData();

            Gizmos.color = Color.green;
            Vector3 boardCenter = transform.position + new Vector3(0f, boardAreaSize.y / 2f, 0f);
            Gizmos.DrawWireCube(boardCenter, new Vector3(boardAreaSize.x, boardAreaSize.y, 0f));

            if (widthCount > 0 && heightCount > 0)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                for (int x = 0; x < widthCount; x++)
                {
                    for (int y = 0; y < heightCount; y++)
                    {
                        Vector3 pos = GetWorldPosition(x, y);
                        Gizmos.DrawWireCube(pos, new Vector3(blockSize.x, blockSize.y, 0f));
                    }
                }
            }
        }
#endif
    }
}
