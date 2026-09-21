using UnityEngine;
using System.Collections.Generic;

namespace MatchDefense.Defense
{
    [RequireComponent(typeof(MeshCollider), typeof(MeshRenderer), typeof(MeshFilter))]
    public class UnitBoard : MonoBehaviour
    {
        public static UnitBoard Instance { get; private set; }

        [Header("Grid Settings")]
        public int gridSizeX = 5;
        public int gridSizeY = 5;

        [HideInInspector] public bool[] spawnableCells;
        [HideInInspector] public List<int> pathCells = new List<int>();

        [Header("Path Settings")]
        [SerializeField] private Material pathMaterial;
        [SerializeField] private float pathWidth = 1.0f;
        [SerializeField] private float yOffset = 0.01f;

        private const bool INVERT_UV_X = true;
        private const bool INVERT_UV_Y = true;

        private static readonly int LineColor = Shader.PropertyToID("_LineColor");
        private static readonly int FadeDistance = Shader.PropertyToID("_FadeDistance");
        private static readonly int HoverX = Shader.PropertyToID("_HoverCellX");
        private static readonly int HoverY = Shader.PropertyToID("_HoverCellY");
        private static readonly int MainColor = Shader.PropertyToID("_MainColor");

        private Material material;
        private Color defaultColor;
        private Mesh mesh;
        private Bounds bounds;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            material = GetComponent<MeshRenderer>().material;
            material.SetFloat("_GridSizeX", gridSizeX);
            material.SetFloat("_GridSizeY", gridSizeY);

            if (material.HasProperty(MainColor))
            {
                defaultColor = material.GetColor(MainColor);
            }

            EnsureMeshAndBounds();
            ClearHover();
        }

        #region Data
        private void EnsureMeshAndBounds()
        {
            if (mesh == null)
            {
                mesh = GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null) bounds = mesh.bounds;
            }
        }
        #endregion

        #region Hover
        public void SetBorderColor(Color color)
        {
            if (material == null || !material.HasProperty(LineColor)) return;
            material.SetColor(LineColor, color);
        }

        public void SetHoverRange(float range)
        {
            if (material == null) return;
            material.SetFloat(FadeDistance, range + 0.5f);
        }

        public void UpdateHover(Vector2 uv)
        {
            int shaderX = Mathf.FloorToInt(uv.x * gridSizeX);
            int shaderY = Mathf.FloorToInt(uv.y * gridSizeY);

            material.SetFloat(HoverX, shaderX);
            material.SetFloat(HoverY, shaderY);
        }

        public void ClearHover()
        {
            if (material == null) return;
            material.SetFloat(HoverX, -10f);
            material.SetFloat(HoverY, -10f);
        }

        public void ToggleGridVisibility(bool isVisible)
        {
            if (material != null && material.HasProperty(MainColor))
            {
                Color c = defaultColor;
                c.a = isVisible ? Mathf.Clamp01(defaultColor.a + 0.15f) : defaultColor.a;
                material.SetColor(MainColor, c);
            }
        }

        public void ShowUnitRange(Vector3 worldPos, float range)
        {
            Vector2Int cell = WorldToCell(worldPos);

            material.SetFloat(HoverX, cell.x);
            material.SetFloat(HoverY, cell.y);
            material.SetFloat("_FadeDistance", range + 0.5f);
            material.SetColor("_LineColor", Color.white);

            ToggleGridVisibility(true);
        }
        #endregion

        #region Position
        public bool IsValidSpawnPosition(Vector2 uv, out Vector3 snappedPos)
        {
            float u = INVERT_UV_X ? 1f - uv.x : uv.x;
            float v = INVERT_UV_Y ? 1f - uv.y : uv.y;

            int cellX = Mathf.FloorToInt(u * gridSizeX);
            int cellY = Mathf.FloorToInt(v * gridSizeY);

            snappedPos = Vector3.zero;

            if (cellX < 0 || cellX >= gridSizeX || cellY < 0 || cellY >= gridSizeY) return false;

            int index = cellY * gridSizeX + cellX;
            if (spawnableCells == null || index >= spawnableCells.Length || !spawnableCells[index]) return false;

            float centerX = (cellX + 0.5f) / gridSizeX;
            float centerY = (cellY + 0.5f) / gridSizeY;

            GetLocalPosition(centerX, centerY, out float localX, out float localY, out float localZ);

            MeshCollider meshCollider = GetComponent<MeshCollider>();
            EnsureMeshAndBounds();
            Ray ray = new Ray(transform.TransformPoint(new Vector3(localX, bounds.max.y + 10f, localZ)), Vector3.down);

            if (meshCollider.Raycast(ray, out RaycastHit hit, 20f)) snappedPos = hit.point;
            else snappedPos = transform.TransformPoint(new Vector3(localX, bounds.max.y, localZ));

            return true;
        }

        private void GetLocalPosition(float centerX, float centerY, out float localX, out float localY, out float localZ)
        {
            EnsureMeshAndBounds();

            localX = Mathf.Lerp(bounds.min.x, bounds.max.x, centerX);
            localY = bounds.center.y;
            localZ = bounds.center.z;

            if (bounds.size.z < 0.01f && bounds.size.y > 0.01f)
            {
                localY = Mathf.Lerp(bounds.min.y, bounds.max.y, centerY);
            }
            else
            {
                localZ = Mathf.Lerp(bounds.min.z, bounds.max.z, centerY);
            }
        }

        public Vector2 WorldToGridPos(Vector3 worldPos)
        {
            EnsureMeshAndBounds();
            if (mesh == null) return Vector2.zero;

            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            float u = (localPos.x - bounds.min.x) / bounds.size.x;
            float v = (bounds.size.z < 0.01f && bounds.size.y > 0.01f)
                ? (localPos.y - bounds.min.y) / bounds.size.y
                : (localPos.z - bounds.min.z) / bounds.size.z;

            if (INVERT_UV_X) u = 1f - u;
            if (INVERT_UV_Y) v = 1f - v;

            return new Vector2(u * gridSizeX, v * gridSizeY);
        }

        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            Vector2 gridPos = WorldToGridPos(worldPos);
            return new Vector2Int(Mathf.FloorToInt(gridPos.x), Mathf.FloorToInt(gridPos.y));
        }
        #endregion

        #region Path
        public Vector3[] GetPathWaypoints()
        {
            if (pathCells == null || pathCells.Count == 0) return new Vector3[0];

            Vector3[] waypoints = new Vector3[pathCells.Count];
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            EnsureMeshAndBounds();

            for (int i = 0; i < pathCells.Count; i++)
            {
                int index = pathCells[i];
                int cellX = index % gridSizeX;
                int cellY = index / gridSizeX;

                float centerX = (cellX + 0.5f) / gridSizeX;
                float centerY = (cellY + 0.5f) / gridSizeY;

                GetLocalPosition(centerX, centerY, out float localX, out float localY, out float localZ);

                Ray ray = new Ray(transform.TransformPoint(new Vector3(localX, bounds.max.y + 10f, localZ)), Vector3.down);

                if (meshCollider != null && meshCollider.Raycast(ray, out RaycastHit hit, 20f))
                {
                    waypoints[i] = hit.point;
                }
                else
                {
                    waypoints[i] = transform.TransformPoint(new Vector3(localX, bounds.max.y, localZ));
                }
            }
            return waypoints;
        }

        public void GeneratePathMesh()
        {
            Vector3[] points = GetPathWaypoints();
            if (points == null || points.Length == 0) return;

            Transform rendererTransform = transform.Find("PathRenderer");
            GameObject rendererObj = rendererTransform != null ? rendererTransform.gameObject : new GameObject("PathRenderer");
            if (rendererTransform == null) rendererObj.transform.SetParent(transform);

            rendererObj.transform.localPosition = Vector3.zero;
            rendererObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            LineRenderer lineRenderer = rendererObj.GetComponent<LineRenderer>();
            if (lineRenderer == null) lineRenderer = rendererObj.AddComponent<LineRenderer>();

            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.numCornerVertices = 8;
            lineRenderer.numCapVertices = 8;
            lineRenderer.startWidth = pathWidth;
            lineRenderer.endWidth = pathWidth;

            if (pathMaterial != null) lineRenderer.sharedMaterial = pathMaterial;

            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = points.Length;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 offsetPos = points[i] + new Vector3(0f, yOffset, 0f);
                lineRenderer.SetPosition(i, offsetPos);
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            EnsureMeshAndBounds();
            if (mesh == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            float width = bounds.size.x;
            float depth = bounds.size.z < 0.01f ? bounds.size.y : bounds.size.z;
            Vector3 cellSize = new Vector3(width / gridSizeX, 0f, depth / gridSizeY);

            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    int index = y * gridSizeX + x;
                    bool isSpawnable = (spawnableCells != null && index < spawnableCells.Length && spawnableCells[index]);
                    bool isPath = (pathCells != null && pathCells.Contains(index));

                    float centerX = (x + 0.5f) / gridSizeX;
                    float centerY = (y + 0.5f) / gridSizeY;

                    GetLocalPosition(centerX, centerY, out float localX, out float localY, out float localZ);

                    Vector3 center = new Vector3(localX, bounds.max.y + 0.01f, localZ);

                    if (bounds.size.z < 0.01f && bounds.size.y > 0.01f)
                    {
                        center = new Vector3(localX, localY, 0.01f);
                        cellSize = new Vector3(width / gridSizeX, depth / gridSizeY, 0f);
                    }

                    if (isPath)
                    {
                        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
                        Gizmos.DrawCube(center, new Vector3(cellSize.x * 0.95f, cellSize.y * 0.95f, cellSize.z * 0.95f));
                        Gizmos.color = Color.blue;
                        Gizmos.DrawWireCube(center, cellSize);
                    }
                    else if (isSpawnable)
                    {
                        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                        Gizmos.DrawCube(center, new Vector3(cellSize.x * 0.95f, cellSize.y * 0.95f, cellSize.z * 0.95f));
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(center, cellSize);
                    }
                    else
                    {
                        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
                        Gizmos.DrawCube(center, new Vector3(cellSize.x * 0.95f, cellSize.y * 0.95f, cellSize.z * 0.95f));
                        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                        Gizmos.DrawWireCube(center, cellSize);
                    }
                }
            }

            if (pathCells != null && pathCells.Count > 1)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.cyan;
                Vector3[] waypoints = GetPathWaypoints();
                for (int i = 0; i < waypoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
                }
            }
        }
        #endregion
    }
}