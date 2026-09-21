using DG.Tweening;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MatchDefense.Defense
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float maxInterval;
        [SerializeField] private float minInterval;

        public static EnemySpawner Instance { get; private set; }
        public readonly List<Enemy> ActiveEnemies = new List<Enemy>();
        
        public float TotalPathLength { get; private set; }
        public Vector3[] PathWaypoints { get; private set; }

        private Vector3 spawnPoint;
        private Coroutine spawnRoutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            DOTween.SetTweensCapacity(2000, 125);
        }

        private void Start()
        {
            InitPath();
            if (PathWaypoints != null && PathWaypoints.Length > 0)
            {
                spawnPoint = PathWaypoints[0];
                spawnRoutine = StartCoroutine(SpawnRoutine());
            }
        }

        private void OnDestroy()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        }

        #region Spawn
        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                SpawnEnemy();
                float interval = Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnEnemy()
        {
            GameObject obj = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
            Enemy enemy = obj.GetComponent<Enemy>();

            ActiveEnemies.Add(enemy);
            enemy.Init(PathWaypoints, TotalPathLength);
        }

        public void RemoveEnemy(Enemy enemy)
        {
            ActiveEnemies.Remove(enemy);
        }
        #endregion

        #region Path
        private void InitPath()
        {
            if (UnitBoard.Instance == null) return;

            PathWaypoints = UnitBoard.Instance.GetPathWaypoints();
            TotalPathLength = 0f;

            if (PathWaypoints != null && PathWaypoints.Length > 0)
            {
                for (int i = 0; i < PathWaypoints.Length - 1; i++)
                {
                    TotalPathLength += Vector3.Distance(PathWaypoints[i], PathWaypoints[i + 1]);
                }
            }
        }
        #endregion
    }
}