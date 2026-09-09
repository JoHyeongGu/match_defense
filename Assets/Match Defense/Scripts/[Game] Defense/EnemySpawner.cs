using DG.Tweening;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace MatchDefense.Defense
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private FieldPath fieldPath;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float maxInterval;
        [SerializeField] private float minInterval;

        public static EnemySpawner Instance { get; private set; }
        public readonly List<Enemy> ActiveEnemies = new List<Enemy>();
        public float TotalPathLength { get; private set; }
        public Vector3[] PathWaypoints { get; private set; }

        private Transform spawnPoint;
        private Coroutine spawnCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            DOTween.SetTweensCapacity(2000, 125);

            InitPath();
        }

        private void Start()
        {
            spawnPoint = fieldPath.Points[0];
            spawnCoroutine = StartCoroutine(SpawnEnemies());
        }

        private void OnDestroy()
        {
            if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); }
        }


        #region Spawn

        private IEnumerator SpawnEnemies()
        {
            while (true)
            {
                SpawnEnemy();
                float _interval = Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(_interval);
            }
        }

        private void SpawnEnemy()
        {
            GameObject _obj = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
            Enemy _enemy = _obj.GetComponent<Enemy>();

            ActiveEnemies.Add(_enemy);
            _enemy.Init(fieldPath.Points, TotalPathLength);
        }
        
        public void RemoveEnemy(Enemy enemy)
        {
            ActiveEnemies.Remove(enemy);
        }

        #endregion


        #region Path

        private void InitPath()
        {
            if (fieldPath != null && fieldPath.Points != null && fieldPath.Points.Length > 0)
            {
                PathWaypoints = fieldPath.Points.Select(p => p.position).ToArray();
                TotalPathLength = 0f;
                for (int i = 0; i < PathWaypoints.Length - 1; i++)
                {
                    TotalPathLength += Vector3.Distance(PathWaypoints[i], PathWaypoints[i + 1]);
                }
            }
        }

        #endregion
    }
}