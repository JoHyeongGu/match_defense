using UnityEngine;

namespace MatchDefense.DefenseGame
{
    [RequireComponent(typeof(LineRenderer))]
    public class Unit : MonoBehaviour
    {
        [Header("<color=yellow>Stats</color>")]
        public float attackRange = 3f;
        public float attackCooldown = 1f;
        public float damage = 10f;

        [Header("<color=yellow>Target</color>")]
        public LayerMask enemyLayer;

        private float lastAttackTime;
        private LineRenderer rangeIndicator;


        #region Unity Methods

        private void Awake()
        {
            rangeIndicator = GetComponent<LineRenderer>();
            SetupRangeIndicator();
            SetVisibleAtkRange(false);
        }

        private void Update()
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                FindAndAttackTarget();
            }
        }

        #endregion


        #region Public

        public void SetVisibleAtkRange(bool isVisible)
            => rangeIndicator.enabled = isVisible;

        #endregion


        #region Private
        private void FindAndAttackTarget()
        {
            Collider[] hitEnemies = Physics.OverlapSphere(
                transform.position,
                attackRange,
                enemyLayer
            );

            if (hitEnemies.Length > 0)
            {
                Enemy targetEnemy = hitEnemies[0].GetComponent<Enemy>();
                if (targetEnemy != null)
                {
                    targetEnemy.TakeDamage(damage);
                    lastAttackTime = Time.time;
                    transform.LookAt(targetEnemy.transform);
                }
            }
        }

        private void SetupRangeIndicator()
        {
            int segments = 50;
            rangeIndicator.positionCount = segments + 1;
            rangeIndicator.useWorldSpace = false;
            rangeIndicator.startWidth = 0.05f;
            rangeIndicator.endWidth = 0.05f;

            rangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
            rangeIndicator.startColor = new Color(0f, 1f, 1f, 0.5f);
            rangeIndicator.endColor = new Color(0f, 1f, 1f, 0.5f);

            float angle = 0f;
            for (int i = 0; i < (segments + 1); i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * attackRange;
                float z = Mathf.Cos(Mathf.Deg2Rad * angle) * attackRange;
                rangeIndicator.SetPosition(i, new Vector3(x, 0.1f, z));
                angle += (360f / segments);
            }
        }
        #endregion


#if UNITY_EDITOR
        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        #endregion
#endif
    }
}
