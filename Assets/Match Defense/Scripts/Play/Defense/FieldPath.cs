using UnityEngine;

namespace MatchDefense.Defense
{
    public class FieldPath : MonoBehaviour
    {
        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = Color.green;
        [SerializeField] private float gizmoRadius = 0.2f;

        [Header("Path Rendering")]
        [SerializeField] private Material pathMaterial;
        [SerializeField] private float pathWidth = 1.0f;
        [SerializeField] private float yOffset = 0.01f;

        [SerializeField] private Transform[] points;
        public Transform[] Points => points;

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            Transform previous = null;
            foreach (Transform p in points)
            {
                if (p == null) continue;
                if (p.name == "PathRenderer") continue; 

                Gizmos.DrawWireSphere(p.position, gizmoRadius);

                if (previous != null)
                {
                    Gizmos.DrawLine(previous.position, p.position);
                }
                previous = p;
            }
        }

        #region Mesh
        [ContextMenu("Generate Path")]
        public void GeneratePathMesh()
        {
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
                if (points[i] != null)
                {
                    Vector3 offsetPos = points[i].position + new Vector3(0f, yOffset, 0f);
                    lineRenderer.SetPosition(i, offsetPos);
                }
            }
        }
        #endregion
    }
}