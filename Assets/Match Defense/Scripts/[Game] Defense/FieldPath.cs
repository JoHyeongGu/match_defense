using UnityEngine;

namespace MatchDefense.Defense
{
    public class FieldPath : MonoBehaviour
    {
        [SerializeField] private Color gizmoColor = Color.green;
        [SerializeField] private float gizmoRadius = 0.2f;

        private Transform[] points;
        public Transform[] Points => points;

        private void Awake()
        {
            points = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                points[i] = transform.GetChild(i);
            }
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            Transform previous = null;
            foreach (Transform child in transform)
            {
                Gizmos.DrawWireSphere(child.position, gizmoRadius);

                if (previous != null)
                {
                    Gizmos.DrawLine(previous.position, child.position);
                }

                previous = child;
            }
        }
    }
}