using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnAction
    {
        public GameObject enemyPrefab; // 소환할 몬스터 프리팹
        public int spawnCount = 5; // 몇 마리를 소환할지
        public float spawnInterval = 1f; // 몇 초 간격으로 소환할지
    }

    [System.Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";
        public float delayBeforeWave = 3f; // 이 웨이브가 시작되기 전 대기 시간
        public List<SpawnAction> spawnActions; // 웨이브 내에서 순차적으로 소환될 그룹들
    }

    [Header("Wave Settings")]
    public List<WaveData> waves;

    [Header("Path Settings")]
    [Tooltip("비워두면 이 스포너의 자식 오브젝트들을 경로로 사용합니다.")]
    public Transform pathRoot;

    private List<Transform> waypoints = new List<Transform>();

    private void Start()
    {
        Transform root = pathRoot != null ? pathRoot : transform;
        foreach (Transform child in root)
        {
            if (child != transform)
            {
                waypoints.Add(child);
            }
        }

        StartCoroutine(SpawnWavesRoutine());
    }

    private IEnumerator SpawnWavesRoutine()
    {
        foreach (WaveData wave in waves)
        {
            yield return new WaitForSeconds(wave.delayBeforeWave);
            foreach (SpawnAction action in wave.spawnActions)
            {
                for (int i = 0; i < action.spawnCount; i++)
                {
                    SpawnEnemy(action.enemyPrefab);
                    yield return new WaitForSeconds(action.spawnInterval);
                }
            }
        }
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject enemyObj = Instantiate(prefab, transform.position, Quaternion.identity);
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        enemy?.SetupPath(waypoints);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Transform root = pathRoot != null ? pathRoot : transform;
        List<Transform> previewPoints = new List<Transform>();

        foreach (Transform child in root)
        {
            if (child != transform)
                previewPoints.Add(child);
        }

        if (previewPoints.Count == 0)
            return;

        Gizmos.color = Color.red;

        // 스포너 위치에서 첫 번째 웨이포인트까지 선 긋기
        Gizmos.DrawLine(transform.position, previewPoints[0].position);

        // 웨이포인트들끼리 연결
        for (int i = 0; i < previewPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(previewPoints[i].position, previewPoints[i + 1].position);
        }

        // 웨이포인트 지점에 둥근 구슬 표시
        foreach (Transform t in previewPoints)
        {
            Gizmos.DrawWireSphere(t.position, 0.3f);
        }
    }
#endif
}
