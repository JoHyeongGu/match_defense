using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Unit : MonoBehaviour
{
    [Header("Combat Settings")]
    public float attackRange = 3f;
    public float attackCooldown = 1f;
    public float damage = 10f;

    [Header("Targeting")]
    public LayerMask enemyLayer;

    private float lastAttackTime;
    private LineRenderer rangeIndicator;

    private void Awake()
    {
        rangeIndicator = GetComponent<LineRenderer>();
        SetupRangeIndicator();
        SetRangeVisible(false);
    }

    private void Update()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            FindAndAttackTarget();
        }
    }

    private void FindAndAttackTarget()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

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

    public void SetRangeVisible(bool isVisible)
    {
        rangeIndicator.enabled = isVisible;
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
