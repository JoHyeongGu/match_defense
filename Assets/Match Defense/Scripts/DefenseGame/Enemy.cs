using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHP = 50f;
    private float currentHP;

    [Header("Movement")]
    public float moveSpeed = 3f;

    private List<Transform> waypoints;
    private int currentWaypointIndex = 0;
    private bool isPathReady = false;

    private void Start()
    {
        currentHP = maxHP;
    }

    public void SetupPath(List<Transform> path)
    {
        waypoints = path;
        if (waypoints != null && waypoints.Count > 0)
        {
            isPathReady = true;
            transform.LookAt(waypoints[currentWaypointIndex]);
        }
    }

    private void Update()
    {
        if (!isPathReady)
            return;

        Transform target = waypoints[currentWaypointIndex];
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.position) <= 0.01f)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                ReachDestination();
            }
            else
            {
                transform.LookAt(waypoints[currentWaypointIndex]);
            }
        }
    }

    private void ReachDestination()
    {
        // TODO: 본진 체력 깎는 로직
        Destroy(gameObject);
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}
