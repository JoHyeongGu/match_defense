using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using DG.Tweening;

namespace MatchDefense.Defense
{
    public class WaveManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private Button waveStartButton;
        [SerializeField] private TMP_Text waveCountTxt;

        [Header("UI & Camera")]
        [SerializeField] private RectTransform matchBoard;
        [SerializeField] private RectTransform subPanel;
        [SerializeField] private RectTransform storePanel;
        [SerializeField] private CameraController cameraController;

        [Header("Speed Control")]
        [SerializeField] private Button speedToggleButton;
        [SerializeField] private TMP_Text speedToggleTxt;

        [Header("Wave Settings")]
        [SerializeField] private int baseEnemyCount = 15;
        [SerializeField] private float baseSpawnInterval = 1.5f;
        [SerializeField] private float intervalDecreaseRate = 0.85f;
        [SerializeField] private float minSpawnInterval = 0.2f;
        [SerializeField] private float transitionDuration = 0.5f;

        private int currentWave = 0;
        private bool isWaveActive = false;
        private bool isDoubleSpeed = false;

        private float originMatchBoardHeight;
        private float originStorePanelY;
        private float originCameraZ;
        private float originSubPanelScaleY;

        private void Awake()
        {
            if (waveStartButton != null)
                waveStartButton.onClick.AddListener(OnWaveStartButtonClicked);

            if (speedToggleButton != null)
            {
                speedToggleButton.onClick.AddListener(OnSpeedToggleButtonClicked);
                speedToggleButton.gameObject.SetActive(false);
            }

            if (matchBoard != null) originMatchBoardHeight = matchBoard.sizeDelta.y;
            if (storePanel != null) originStorePanelY = storePanel.anchoredPosition.y;
            if (subPanel != null) originSubPanelScaleY = subPanel.localScale.y;

            if (cameraController != null) originCameraZ = cameraController.transform.position.z;
        }

        private void OnDestroy()
        {
            if (waveStartButton != null)
                waveStartButton.onClick.RemoveListener(OnWaveStartButtonClicked);

            if (speedToggleButton != null)
                speedToggleButton.onClick.RemoveListener(OnSpeedToggleButtonClicked);

            Time.timeScale = 1f;
        }

        #region Speed Control
        private void OnSpeedToggleButtonClicked()
        {
            isDoubleSpeed = !isDoubleSpeed;

            Time.timeScale = isDoubleSpeed ? 2.0f : 1.0f;

            if (speedToggleTxt != null)
            {
                speedToggleTxt.text = isDoubleSpeed ? "x2" : "x1";
            }
        }
        #endregion

        #region Wave Routine
        private void OnWaveStartButtonClicked()
        {
            if (isWaveActive) return;

            currentWave++;
            waveStartButton.gameObject.SetActive(false);
            speedToggleButton.gameObject.SetActive(true);

            PlayWaveStartTransition();
            StartCoroutine(WaveRoutine());
        }

        private IEnumerator WaveRoutine()
        {
            isWaveActive = true;
            if (isDoubleSpeed) Time.timeScale = 2.0f;

            float currentInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval * Mathf.Pow(intervalDecreaseRate, currentWave - 1));
            int currentEnemyCount = Mathf.RoundToInt(baseEnemyCount * (baseSpawnInterval / currentInterval));

            for (int i = 0; i < currentEnemyCount; i++)
            {
                enemySpawner.SpawnEnemy();
                yield return new WaitForSeconds(currentInterval);
            }

            yield return new WaitUntil(() => enemySpawner.ActiveEnemies.Count == 0);

            isWaveActive = false;
            waveCountTxt.text = $"{currentWave + 1} WAVE";
            waveStartButton.gameObject.SetActive(true);
            speedToggleButton.gameObject.SetActive(false);
            Time.timeScale = 1.0f;

            PlayWaveEndTransition();
        }
        #endregion

        #region Transitions
        private void PlayWaveStartTransition()
        {
            if (Match.MatchManager.Instance != null)
                Match.MatchManager.Instance.SetMatchActive(false);

            if (matchBoard != null)
                matchBoard.DOSizeDelta(new Vector2(matchBoard.sizeDelta.x, 0f), transitionDuration).SetEase(Ease.OutQuad);

            if (subPanel != null)
                subPanel.DOScaleY(0f, transitionDuration).SetEase(Ease.OutQuad);

            if (storePanel != null)
                storePanel.DOAnchorPosY(10f, transitionDuration).SetEase(Ease.OutQuad);

            if (cameraController != null)
                cameraController.ResetAndMoveZ(0f, transitionDuration);
        }

        private void PlayWaveEndTransition()
        {
            if (Match.MatchManager.Instance != null)
                Match.MatchManager.Instance.SetMatchActive(true);

            if (matchBoard != null)
                matchBoard.DOSizeDelta(new Vector2(matchBoard.sizeDelta.x, originMatchBoardHeight), transitionDuration).SetEase(Ease.OutQuad);

            if (subPanel != null)
                subPanel.DOScaleY(originSubPanelScaleY, transitionDuration).SetEase(Ease.OutQuad);

            if (storePanel != null)
                storePanel.DOAnchorPosY(originStorePanelY, transitionDuration).SetEase(Ease.OutQuad);

            if (cameraController != null)
                cameraController.ResetAndMoveZ(originCameraZ, transitionDuration);
        }
        #endregion
    }
}