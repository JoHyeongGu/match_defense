using System.Collections.Generic;
using UnityEngine;

namespace MatchDefense.DefenseGame
{
    public class Enemy : MonoBehaviour
    {
        [Header("<color=yellow>Stats</color>")]
        [SerializeField] private float maxHP = 50f;
        private float currentHP;

        [Header("<color=yellow>Movement</color>")]
        [SerializeField] private float moveSpeed = 3f;

        private List<Transform> path;
        private bool isPathReady = false;
        private int currentPathIndex = 0;


        #region Unity Methods

        private void Start() => currentHP = maxHP;
        private void Update() => Move();

        #endregion


        #region Public

        public void InitPath(List<Transform> _path)
        {
            path = _path;
            if (path != null && path.Count > 0)
            {
                isPathReady = true;
                transform.LookAt(path[currentPathIndex]);
            }
        }

        public void TakeDamage(float damage)
        {
            currentHP -= damage;
            if (currentHP <= 0) Die();
        }

        #endregion


        #region Private

        private void Move()
        {
            if (!isPathReady) return;

            Transform target = path[currentPathIndex];
            transform.position = Vector3.MoveTowards(
                transform.position,
                target.position,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, target.position) <= 0.01f)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    ReachDestination();
                }
                else
                {
                    transform.LookAt(path[currentPathIndex]);
                }
            }
        }

        private void ReachDestination()
        {
            Destroy(gameObject);
        }
        private void Die()
        {
            Destroy(gameObject);
        }

        #endregion
    }
}
