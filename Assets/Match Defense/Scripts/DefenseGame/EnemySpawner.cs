using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MatchDefense.DefenseGame
{
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        public class SpawnAction
        {
            public GameObject prefab;
            public int spawnCount = 5;
            public float spawnInterval = 1f;
        }

        [Serializable]
        public class WaveData
        {
            public string waveName = "Wave 1";
            public float delayBeforeWave = 3f;
            public List<SpawnAction> spawnActions;
        }

        [Header("<color=yellow>Wave</color>")]
        public List<WaveData> waves;

        [Header("<color=yellow>Path</color>")]
        [SerializeField] private Transform pathRoot;
        private List<Transform> path = new();


        #region Unity Methods

        private void Start()
        {
            Transform root = pathRoot != null ? pathRoot : transform;
            foreach (Transform child in root)
            {
                if (child != transform)
                {
                    path.Add(child);
                }
            }
            StartCoroutine(WavesRoutine());
        }

        #endregion


        #region Private

        private IEnumerator WavesRoutine()
        {
            foreach (WaveData wave in waves)
            {
                yield return new WaitForSeconds(wave.delayBeforeWave);
                foreach (SpawnAction action in wave.spawnActions)
                {
                    for (int i = 0; i < action.spawnCount; i++)
                    {
                        SpawnEnemy(action.prefab);
                        yield return new WaitForSeconds(action.spawnInterval);
                    }
                }
            }
        }

        private void SpawnEnemy(GameObject prefab)
        {
            if (prefab == null) return;
            GameObject enemyObj = Instantiate(prefab, transform.position, Quaternion.identity);
            Enemy enemy = enemyObj.GetComponent<Enemy>();
            enemy?.InitPath(path);
        }

        #endregion


#if UNITY_EDITOR
        #region Gizmos

        private void OnDrawGizmos()
        {
            Transform root = pathRoot != null ? pathRoot : transform;
            List<Transform> previewPoints = new List<Transform>();

            foreach (Transform child in root)
            {
                if (child != transform)
                    previewPoints.Add(child);
            }
            if (previewPoints.Count == 0) return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, previewPoints[0].position);
            for (int i = 0; i < previewPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(previewPoints[i].position, previewPoints[i + 1].position);
            }
            foreach (Transform t in previewPoints)
            {
                Gizmos.DrawWireSphere(t.position, 0.3f);
            }
        }

        #endregion
#endif
    }
}
