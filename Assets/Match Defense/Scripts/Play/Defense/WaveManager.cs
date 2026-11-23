using UnityEngine;
using System;
using System.Collections;

namespace MatchDefense.Defense
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("<color=yellow>References</color>")]
        [SerializeField] private EnemySpawner enemySpawner;

        [Header("<color=yellow>Wave Settings</color>")]
        [SerializeField] private int baseEnemyCount = 15;
        [SerializeField] private float baseSpawnInterval = 1.5f;
        [SerializeField] private float intervalDecreaseRate = 0.85f;
        [SerializeField] private float minSpawnInterval = 0.2f;
        [SerializeField] private float autoNextWaveDelay = 1.0f;

        public int CurrentWave => currentWave;
        public bool IsWaveActive => isWaveActive;

        private int currentWave;
        private bool isWaveActive;
        private Coroutine waveCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Wave Routine
        public bool StartWave(
            Func<bool> shouldAutoContinue,
            Action onWavesCompleted = null,
            Action<int> onWaveStarted = null)
        {
            if (isWaveActive) return false;

            if (enemySpawner == null) enemySpawner = EnemySpawner.Instance;
            if (enemySpawner == null) return false;

            waveCoroutine = StartCoroutine(WaveLoopRoutine(shouldAutoContinue, onWavesCompleted, onWaveStarted));
            return true;
        }

        public void StopWaves()
        {
            if (waveCoroutine != null)
            {
                StopCoroutine(waveCoroutine);
                waveCoroutine = null;
            }
            isWaveActive = false;
        }

        private IEnumerator WaveLoopRoutine(
            Func<bool> shouldAutoContinue,
            Action onWavesCompleted,
            Action<int> onWaveStarted)
        {
            isWaveActive = true;

            while (true)
            {
                currentWave++;
                onWaveStarted?.Invoke(currentWave);

                float currentInterval = Mathf.Max(
                    minSpawnInterval,
                    baseSpawnInterval * Mathf.Pow(intervalDecreaseRate, currentWave - 1));
                int currentEnemyCount = Mathf.RoundToInt(baseEnemyCount * (baseSpawnInterval / currentInterval));

                for (int i = 0; i < currentEnemyCount; i++)
                {
                    enemySpawner.SpawnEnemy();
                    for (float t = 0f; t < currentInterval; t += Time.deltaTime)
                        yield return null;
                }

                while (enemySpawner.ActiveEnemies.Count > 0)
                    yield return null;

                if (shouldAutoContinue == null || !shouldAutoContinue())
                    break;

                for (float t = 0f; t < autoNextWaveDelay; t += Time.deltaTime)
                    yield return null;

                if (shouldAutoContinue == null || !shouldAutoContinue())
                    break;
            }

            isWaveActive = false;
            waveCoroutine = null;
            onWavesCompleted?.Invoke();
        }
        #endregion
    }
}