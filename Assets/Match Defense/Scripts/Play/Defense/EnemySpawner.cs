using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace MatchDefense.Defense
{
    [Serializable]
    public class EnemySpawnWeight
    {
        public GameObject enemyPrefab;
        [Min(0.01f)] public float weight = 1f;
    }

    [Serializable]
    public class WaveEnemyRule
    {
        public int startWave = 1;
        public int endWave = 5;

        [Header("Spawn Pool")]
        public EnemySpawnWeight[] enemyPool;

        [Header("Stat Scaling")]
        [Min(0.1f)] public float hpMultiplier = 1f;
        [Min(0.1f)] public float speedMultiplier = 1f;

        [Header("Optional Wave Override")]
        public int overrideEnemyCount = 0;
        public float overrideSpawnInterval = 0f;

        public bool MatchesWave(int wave)
        {
            if (wave < startWave) return false;
            if (endWave > 0 && wave > endWave) return false;
            return true;
        }
    }

    public class EnemySpawner : MonoBehaviour
    {
        [Header("<color=yellow>Default Spawn Pool</color>")]
        [SerializeField] private EnemySpawnWeight[] defaultEnemyPool;
        [FormerlySerializedAs("enemyPrefab")]
        [SerializeField] private GameObject fallbackEnemyPrefab;

        [Header("<color=yellow>Wave Rules</color>")]
        [SerializeField] private WaveEnemyRule[] waveRules;

        [Header("<color=yellow>Endless Scaling</color>")]
        [SerializeField] private float endlessHpGrowthPerWave = 0.08f;
        [SerializeField] private float endlessSpeedGrowthPerWave = 0.015f;

        public static EnemySpawner Instance { get; private set; }
        public readonly List<Enemy> ActiveEnemies = new List<Enemy>();

        public float TotalPathLength { get; private set; }
        public Vector3[] PathWaypoints { get; private set; }

        public event Action<Enemy> OnEnemySpawned;

        private Vector3 spawnPoint;

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
                spawnPoint = PathWaypoints[0];
        }

        #region Spawn
        public GameObject GetFallbackEnemyPrefab()
        {
            if (fallbackEnemyPrefab != null) return fallbackEnemyPrefab;
            if (defaultEnemyPool != null)
            {
                for (int i = 0; i < defaultEnemyPool.Length; i++)
                {
                    if (defaultEnemyPool[i] != null && defaultEnemyPool[i].enemyPrefab != null)
                        return defaultEnemyPool[i].enemyPrefab;
                }
            }
            return null;
        }

        public Enemy SpawnEnemy() => SpawnEnemyForWave(1, 40);

        public Enemy SpawnEnemyForWave(int wave, int targetClearWave = 40)
        {
            if (PathWaypoints == null || PathWaypoints.Length == 0) InitPath();
            if (PathWaypoints == null || PathWaypoints.Length == 0) return null;

            WaveEnemyRule activeRule = FindRuleForWave(wave);
            EnemySpawnWeight[] pool = (activeRule != null && activeRule.enemyPool != null && activeRule.enemyPool.Length > 0)
                ? activeRule.enemyPool
                : defaultEnemyPool;

            GameObject prefab = PickPrefabFromPool(pool);
            if (prefab == null) prefab = fallbackEnemyPrefab;
            if (prefab == null) return null;

            float hpMult = activeRule != null ? activeRule.hpMultiplier : 1f;
            float speedMult = activeRule != null ? activeRule.speedMultiplier : 1f;

            if (wave > targetClearWave)
            {
                int extraWaves = wave - targetClearWave;
                hpMult *= (1f + extraWaves * endlessHpGrowthPerWave);
                speedMult *= (1f + extraWaves * endlessSpeedGrowthPerWave);
            }

            return SpawnSpecificEnemy(prefab, hpMult, speedMult);
        }

        public Enemy SpawnSpecificEnemy(GameObject prefab, float hpMultiplier = 1f, float speedMultiplier = 1f)
        {
            return SpawnEnemyAtProgress(prefab, 0f, hpMultiplier, speedMultiplier);
        }

        public Enemy SpawnEnemyAtProgress(GameObject prefab, float startProgress, float hpMultiplier = 1f, float speedMultiplier = 1f)
        {
            if (prefab == null) return null;
            if (PathWaypoints == null || PathWaypoints.Length == 0) InitPath();
            if (PathWaypoints == null || PathWaypoints.Length == 0) return null;

            Vector3[] subPath = BuildRemainingWaypoints(startProgress, out Vector3 startPos);
            GameObject obj = Instantiate(prefab, startPos, Quaternion.identity);
            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy != null)
            {
                ActiveEnemies.Add(enemy);
                enemy.Init(subPath, TotalPathLength, hpMultiplier, speedMultiplier, startProgress);
                OnEnemySpawned?.Invoke(enemy);
            }
            return enemy;
        }

        private Vector3[] BuildRemainingWaypoints(float progress, out Vector3 startPos)
        {
            if (progress <= 0.001f || PathWaypoints.Length < 2 || TotalPathLength <= 0.001f)
            {
                startPos = PathWaypoints[0];
                return PathWaypoints;
            }

            float targetDist = Mathf.Clamp01(progress) * TotalPathLength;
            float accum = 0f;
            List<Vector3> remaining = new List<Vector3>(PathWaypoints.Length);

            for (int i = 0; i < PathWaypoints.Length - 1; i++)
            {
                Vector3 a = PathWaypoints[i];
                Vector3 b = PathWaypoints[i + 1];
                float segLen = Vector3.Distance(a, b);

                if (accum + segLen >= targetDist)
                {
                    float t = segLen > 0.0001f ? (targetDist - accum) / segLen : 0f;
                    startPos = Vector3.Lerp(a, b, t);
                    remaining.Add(startPos);
                    for (int j = i + 1; j < PathWaypoints.Length; j++)
                        remaining.Add(PathWaypoints[j]);
                    return remaining.ToArray();
                }

                accum += segLen;
            }

            startPos = PathWaypoints[PathWaypoints.Length - 1];
            return new[] { startPos };
        }

        public WaveEnemyRule FindRuleForWave(int wave)
        {
            if (waveRules == null) return null;
            for (int i = waveRules.Length - 1; i >= 0; i--)
            {
                if (waveRules[i] != null && waveRules[i].MatchesWave(wave))
                    return waveRules[i];
            }
            return null;
        }

        private GameObject PickPrefabFromPool(EnemySpawnWeight[] pool)
        {
            if (pool == null || pool.Length == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].enemyPrefab != null)
                    totalWeight += Mathf.Max(0.01f, pool[i].weight);
            }

            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float sum = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null || pool[i].enemyPrefab == null) continue;
                sum += Mathf.Max(0.01f, pool[i].weight);
                if (roll <= sum) return pool[i].enemyPrefab;
            }

            return pool[pool.Length - 1].enemyPrefab;
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
                spawnPoint = PathWaypoints[0];
                for (int i = 0; i < PathWaypoints.Length - 1; i++)
                    TotalPathLength += Vector3.Distance(PathWaypoints[i], PathWaypoints[i + 1]);
            }
        }
        #endregion
    }
}
