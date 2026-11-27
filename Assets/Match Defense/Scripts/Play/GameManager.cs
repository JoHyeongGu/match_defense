using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using MatchDefense.Defense;
using MatchDefense.Match;

namespace MatchDefense
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("<color=yellow>References</color>")]
        [SerializeField] private EnemySpawner enemySpawner;

        [Header("<color=yellow>Stage Identity (-1 = Use ProgramManager Selection)</color>")]
        [SerializeField] private int mainStageIndexOverride = -1;
        [SerializeField] private int subStageIndexOverride = -1;

        [Header("<color=yellow>Core Gameplay</color>")]
        [SerializeField] private int maxHP = 20;
        [SerializeField] private int maxWave = 40;
        [SerializeField] private float doubleSpeedScale = 2f;
        [SerializeField] private string lobbySceneName = "Lobby";

        [Header("<color=yellow>Rewards</color>")]
        [SerializeField] private int clearRewardCoins = 100;
        [SerializeField] private int coinsPerRemainingTurn = 5;

        [Header("<color=yellow>Wave Settings</color>")]
        [SerializeField] private int baseEnemyCount = 15;
        [SerializeField] private float baseSpawnInterval = 1.5f;
        [SerializeField] private float intervalDecreaseRate = 0.85f;
        [SerializeField] private float minSpawnInterval = 0.2f;
        [SerializeField] private float autoNextWaveDelay = 1.0f;

        public int MainStageIndex { get; private set; }
        public int SubStageIndex { get; private set; }
        public int MaxHP => maxHP;
        public int CurrentHP => currentHP;
        public const int MaxStars = 5;
        public static float GetStarThreshold(int starIndex) => (starIndex + 1f) / (MaxStars + 1f);
        public int MaxWave => maxWave;
        public int CurrentWave => currentWave;
        public int DisplayWave => isEndlessMode
            ? (isWaveActive ? currentWave : currentWave + 1)
            : Mathf.Clamp(isWaveActive ? currentWave : currentWave + 1, 1, maxWave);
        public bool IsWaveActive => isWaveActive;
        public bool IsPaused => isPaused;
        public bool IsTutorialPaused => isTutorialPaused;
        public bool IsDoubleSpeed => isDoubleSpeed;
        public bool IsGameOver => isGameOver;
        public bool IsStageCleared => isStageCleared;
        public bool IsEndlessMode => isEndlessMode;
        public bool IsAwaitingEndlessChoice => isAwaitingEndlessChoice;

        public float BgmVolume => ProgramManager.Instance != null ? ProgramManager.Instance.BgmVolume : 1f;
        public float SfxVolume => ProgramManager.Instance != null ? ProgramManager.Instance.SfxVolume : 1f;
        public bool IsVibrationEnabled => ProgramManager.Instance == null || ProgramManager.Instance.IsVibrationEnabled;

        public event Action<int, int> OnHPChanged;
        public event Action<int, int, bool> OnWaveChanged;
        public event Action<bool> OnPauseChanged;
        public event Action<bool> OnSpeedChanged;
        public event Action OnGameOver;
        public event Action OnStageCleared;
        public event Action<StageRewardSummary> OnEndlessChoiceRequested;
        public event Action<StageRewardSummary> OnStageResultReady;

        private int currentHP;
        private int currentWave;
        private bool isWaveActive;
        private bool isPaused;
        private bool isTutorialPaused;
        private float tutorialTimeScale = 1f;
        private bool isDoubleSpeed;
        private bool isGameOver;
        private bool isStageCleared;
        private bool isEndlessMode;
        private bool isAwaitingEndlessChoice;
        private bool rewardsClaimed;
        private Coroutine waveCoroutine;

        private Func<bool> cachedAutoContinue;
        private Action cachedOnWavesCompleted;
        private Action<int> cachedOnWaveStarted;

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            ResolveStageConfig();
            currentHP = maxHP;
            currentWave = 0;
            ApplyTimeScale();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Instance = null;
            }
        }

        private void ResolveStageConfig()
        {
            if (ProgramManager.Instance != null)
            {
                MainStageIndex = mainStageIndexOverride >= 0 ? mainStageIndexOverride : ProgramManager.Instance.CurrentMainStageIndex;
                SubStageIndex = subStageIndexOverride >= 0 ? subStageIndexOverride : ProgramManager.Instance.CurrentSubStageIndex;

                SubStageConfig subCfg = ProgramManager.Instance.GetSubStageConfig(MainStageIndex, SubStageIndex);
                if (subCfg != null)
                {
                    if (subCfg.targetClearWave > 0) maxWave = subCfg.targetClearWave;
                    if (subCfg.clearRewardCoins > 0) clearRewardCoins = subCfg.clearRewardCoins;
                    if (subCfg.coinsPerRemainingTurn >= 0) coinsPerRemainingTurn = subCfg.coinsPerRemainingTurn;
                }
            }
            else
            {
                MainStageIndex = Mathf.Max(0, mainStageIndexOverride);
                SubStageIndex = Mathf.Max(0, subStageIndexOverride);
            }
        }
        #endregion

        #region HP System
        public void TakeDamage(int amount)
        {
            if (amount <= 0 || isGameOver || (isStageCleared && !isEndlessMode)) return;

            currentHP = Mathf.Max(0, currentHP - amount);
            OnHPChanged?.Invoke(currentHP, maxHP);

            if (currentHP <= 0)
            {
                HandleGameOver();
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || isGameOver || (isStageCleared && !isEndlessMode)) return;

            currentHP = Mathf.Min(maxHP, currentHP + amount);
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        public int CalculateCurrentStars()
        {
            float hpRatio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            int stars = 0;
            for (int i = 0; i < MaxStars; i++)
            {
                if (hpRatio >= GetStarThreshold(i))
                    stars++;
            }
            return stars;
        }

        private void HandleGameOver()
        {
            if (isGameOver) return;

            isGameOver = true;
            isAwaitingEndlessChoice = false;
            StopWaves();
            ApplyTimeScale();

            StageRewardSummary summary = BuildRewardSummary(cleared: isEndlessMode);
            if (isEndlessMode && !rewardsClaimed)
            {
                GrantRewards(summary);
            }

            SoundManager.Instance?.PlaySFX(SfxType.GameOver);
            OnGameOver?.Invoke();
            OnStageResultReady?.Invoke(summary);
        }
        #endregion

        #region Wave Routine
        public bool StartWave(
            Func<bool> shouldAutoContinue,
            Action onWavesCompleted = null,
            Action<int> onWaveStarted = null)
        {
            if (isWaveActive || isGameOver || isAwaitingEndlessChoice) return false;
            if (!isEndlessMode && currentWave >= maxWave) return false;

            if (enemySpawner == null) enemySpawner = EnemySpawner.Instance;
            if (enemySpawner == null) return false;

            cachedAutoContinue = shouldAutoContinue;
            cachedOnWavesCompleted = onWavesCompleted;
            cachedOnWaveStarted = onWaveStarted;

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

            while (!isGameOver && (isEndlessMode || currentWave < maxWave))
            {
                currentWave++;
                OnWaveChanged?.Invoke(DisplayWave, maxWave, isEndlessMode);
                onWaveStarted?.Invoke(currentWave);

                WaveEnemyRule rule = enemySpawner.FindRuleForWave(currentWave);

                float currentInterval = (rule != null && rule.overrideSpawnInterval > 0f)
                    ? rule.overrideSpawnInterval
                    : Mathf.Max(minSpawnInterval, baseSpawnInterval * Mathf.Pow(intervalDecreaseRate, currentWave - 1));

                int currentEnemyCount = (rule != null && rule.overrideEnemyCount > 0)
                    ? rule.overrideEnemyCount
                    : Mathf.RoundToInt(baseEnemyCount * (baseSpawnInterval / currentInterval));

                for (int i = 0; i < currentEnemyCount; i++)
                {
                    if (isGameOver) break;

                    enemySpawner.SpawnEnemyForWave(currentWave, maxWave);
                    for (float t = 0f; t < currentInterval; t += Time.deltaTime)
                        yield return null;
                }

                while (!isGameOver && enemySpawner.ActiveEnemies.Count > 0)
                    yield return null;

                if (isGameOver) break;

                if (!isEndlessMode && currentWave >= maxWave)
                {
                    isWaveActive = false;
                    waveCoroutine = null;
                    HandleTargetWaveReached();
                    yield break;
                }

                OnWaveChanged?.Invoke(DisplayWave, maxWave, isEndlessMode);

                if (shouldAutoContinue == null || !shouldAutoContinue())
                    break;

                for (float t = 0f; t < autoNextWaveDelay; t += Time.deltaTime)
                    yield return null;

                if (isGameOver || shouldAutoContinue == null || !shouldAutoContinue())
                    break;
            }

            isWaveActive = false;
            waveCoroutine = null;

            if (!isGameOver && !isAwaitingEndlessChoice && !(isStageCleared && !isEndlessMode))
            {
                OnWaveChanged?.Invoke(DisplayWave, maxWave, isEndlessMode);
                onWavesCompleted?.Invoke();
            }
        }

        private void HandleTargetWaveReached()
        {
            if (isAwaitingEndlessChoice || isStageCleared) return;

            isAwaitingEndlessChoice = true;
            ApplyTimeScale();

            StageRewardSummary summary = BuildRewardSummary(cleared: true);
            if (!rewardsClaimed)
                GrantRewards(summary);

            OnStageCleared?.Invoke();
            OnEndlessChoiceRequested?.Invoke(summary);
        }

        public void FinishStageAndGoToLobby()
        {
            isAwaitingEndlessChoice = false;
            isStageCleared = true;
            StopWaves();
            if (!rewardsClaimed)
                GrantRewards(BuildRewardSummary(cleared: true));
            GoToLobby();
        }

        public void ContinueEndlessMode()
        {
            if (!isAwaitingEndlessChoice) return;

            isAwaitingEndlessChoice = false;
            isEndlessMode = true;
            ApplyTimeScale();

            OnWaveChanged?.Invoke(DisplayWave, maxWave, isEndlessMode);
            cachedOnWavesCompleted?.Invoke();
        }

        public StageRewardSummary FinishStageAndClaimRewards()
        {
            isAwaitingEndlessChoice = false;
            isStageCleared = true;
            StopWaves();
            ApplyTimeScale();

            StageRewardSummary summary = BuildRewardSummary(cleared: true);
            if (!rewardsClaimed)
            {
                GrantRewards(summary);
            }

            SoundManager.Instance?.PlaySFX(SfxType.StageClear);
            OnStageCleared?.Invoke();
            OnStageResultReady?.Invoke(summary);
            return summary;
        }

        private StageRewardSummary BuildRewardSummary(bool cleared)
        {
            int remainingTurns = MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0;
            int baseCoins = cleared ? clearRewardCoins : 0;
            int turnBonus = cleared ? remainingTurns * coinsPerRemainingTurn : 0;
            int stars = cleared ? Mathf.Max(1, CalculateCurrentStars()) : 0;

            return new StageRewardSummary
            {
                mainStageIndex = MainStageIndex,
                subStageIndex = SubStageIndex,
                reachedWave = currentWave,
                targetWave = maxWave,
                isStageCleared = cleared,
                starsEarned = stars,
                clearCoins = baseCoins,
                remainingTurns = remainingTurns,
                turnBonusCoins = turnBonus,
                totalCoins = baseCoins + turnBonus
            };
        }

        private void GrantRewards(StageRewardSummary summary)
        {
            rewardsClaimed = true;
            if (ProgramManager.Instance == null) return;

            if (summary.totalCoins > 0)
                ProgramManager.Instance.AddCoins(summary.totalCoins);

            if (summary.isStageCleared)
                ProgramManager.Instance.RecordSubStageClear(MainStageIndex, SubStageIndex, summary.starsEarned);
        }
        #endregion

        #region Pause & Speed
        public void PauseGame()
        {
            if (isPaused || isGameOver || isAwaitingEndlessChoice || (isStageCleared && !isEndlessMode)) return;

            isPaused = true;
            ApplyTimeScale();
            OnPauseChanged?.Invoke(true);
        }

        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;
            ApplyTimeScale();
            OnPauseChanged?.Invoke(false);
        }

        public void TogglePause()
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        public void SetTutorialPause(bool paused)
        {
            if (isTutorialPaused == paused) return;
            isTutorialPaused = paused;
            ApplyTimeScale();
        }

        public void SetTutorialTimeScale(float scale)
        {
            tutorialTimeScale = Mathf.Max(1f, scale);
            ApplyTimeScale();
        }

        public void SetDoubleSpeed(bool enabled)
        {
            if (isDoubleSpeed == enabled) return;

            isDoubleSpeed = enabled;
            ApplyTimeScale();
            OnSpeedChanged?.Invoke(isDoubleSpeed);
        }

        private void ApplyTimeScale()
        {
            if (isPaused || isTutorialPaused || isGameOver || isAwaitingEndlessChoice || (isStageCleared && !isEndlessMode))
            {
                Time.timeScale = 0f;
                return;
            }

            if (tutorialTimeScale > 1.01f)
            {
                Time.timeScale = tutorialTimeScale;
                return;
            }
            Time.timeScale = isDoubleSpeed ? doubleSpeedScale : 1f;
        }
        #endregion

        #region Settings & Scene Control
        public void SetBgmVolume(float value) => ProgramManager.Instance?.SetBgmVolume(value);
        public void SetSfxVolume(float value) => ProgramManager.Instance?.SetSfxVolume(value);
        public void SetVibrationEnabled(bool enabled) => ProgramManager.Instance?.SetVibrationEnabled(enabled);

        public void RestartStage()
        {
            isPaused = false;
            isTutorialPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToLobby()
        {
            isPaused = false;
            isTutorialPaused = false;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(lobbySceneName))
            {
                SceneManager.LoadScene(lobbySceneName);
            }
        }
        #endregion
    }
}
