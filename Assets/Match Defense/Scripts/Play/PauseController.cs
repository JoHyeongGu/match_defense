using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

namespace MatchDefense.UI
{
    public class PauseController : MonoBehaviour
    {
        private const string HideClass = "hide";
        private const string StarLostClass = "hp-star--lost";
        private const string HpHitClass = "hp-bar-fill--hit";
        private const string ResultStarEmptyClass = "result-star--empty";

        [Header("<color=yellow>References</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private GameManager gameManager;

        [Header("<color=yellow>UI Assets</color>")]
        [SerializeField] private VisualTreeAsset pausePopupAsset;
        [SerializeField] private VisualTreeAsset resultPopupAsset;
        [SerializeField] private Sprite victoryBannerSprite;

        public static VisualElement EnemyHpLayer { get; private set; }

        private int uiVersion = -1;
        private VisualElement root;

        private VisualElement hpBarBg;
        private VisualElement hpBarFill;
        private VisualElement hpHeart;
        private Label hpText;
        private Label waveText;
        private Button pauseBtn;

        private readonly VisualElement[] hpTicks = new VisualElement[5];
        private readonly VisualElement[] hpStars = new VisualElement[5];
        private readonly bool[] starActiveState = { true, true, true, true, true };
        private int lastKnownHp = -1;

        private Sequence heartHitSeq;
        private Sequence barShakeSeq;
        private Sequence clearCelebrationSeq;
        private Tween turnDrainTween;

        private SettingsPopupController settingsPopup;

        // Result & Clear Celebration Popup
        private VisualElement resultOverlay;
        private VisualElement resultCoinPill;
        private VisualElement resultCoinIcon;
        private Label resultCoinIconTxt;
        private Label resultCoinLabel;

        private VisualElement endlessChoicePanel;
        private VisualElement resultSuccessImage;
        private Label endlessTitleLabel;
        private readonly VisualElement[] resultStars = new VisualElement[5];
        private VisualElement clearRewardOrigin;
        private Label clearRewardTitleLabel;
        private Label clearRewardAmountLabel;
        private VisualElement resultTurnOrigin;
        private Label resultTurnCountLabel;
        private Label resultTurnCoinPreviewLabel;
        private Button endlessContinueBtn;
        private Button endlessStopBtn;

        private VisualElement finalResultPanel;
        private Label resultTitleLabel;
        private Label resultWaveLabel;
        private Button resultRetryBtn;
        private Button resultLobbyBtn;

        private bool isBoundToGameManager;

        #region Unity Lifecycle
        private void Awake()
        {
            if (panelRenderer == null)
                panelRenderer = GetComponent<PanelRenderer>();

            settingsPopup = new SettingsPopupController(
                isPlaySceneMode: true,
                onCloseRequested: () => ResolveGameManager()?.ResumeGame(),
                onRestartRequested: () => ResolveGameManager()?.RestartStage(),
                onLobbyRequested: () => ResolveGameManager()?.GoToLobby()
            );
        }

        private void OnEnable()
        {
            if (panelRenderer != null)
                panelRenderer.RegisterUIReloadCallback(OnUIReload);

            BindGameManager();
        }

        private void Start()
        {
            BindGameManager();
            RefreshAllUI();
        }

        private void OnDisable()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            heartHitSeq?.Kill();
            barShakeSeq?.Kill();
            clearCelebrationSeq?.Kill();
            turnDrainTween?.Kill();
            UnbindGameManager();
            UnbindUIEvents();
            EnemyHpLayer = null;
            settingsPopup?.Unbind();
        }
        #endregion

        #region UI Reload & Binding
        private void OnUIReload(PanelRenderer panel, VisualElement rootElement, int version)
        {
            if (version == uiVersion) return;
            uiVersion = version;

            UnbindUIEvents();
            root = rootElement;
            if (root == null) return;

            EnemyHpLayer = root.Q<VisualElement>("enemy-hp-layer");
            if (EnemyHpLayer == null)
            {
                EnemyHpLayer = new VisualElement
                {
                    name = "enemy-hp-layer",
                    pickingMode = PickingMode.Ignore
                };
                EnemyHpLayer.style.position = Position.Absolute;
                EnemyHpLayer.style.left = 0;
                EnemyHpLayer.style.right = 0;
                EnemyHpLayer.style.top = 0;
                EnemyHpLayer.style.bottom = 0;
                root.Insert(0, EnemyHpLayer);
            }

            BindTopHUD();
            settingsPopup?.Mount(root, pausePopupAsset);
            MountResultPopup();
            BindGameManager();
            RefreshAllUI();
        }

        private void BindTopHUD()
        {
            hpBarBg = root.Q<VisualElement>("hp-bar-bg");
            hpBarFill = root.Q<VisualElement>("hp-bar-fill");
            hpHeart = root.Q<VisualElement>("hp-heart");
            hpText = root.Q<Label>("hp-text");
            waveText = root.Q<Label>("wave-text");
            pauseBtn = root.Q<Button>("pause-btn");

            for (int i = 0; i < 5; i++)
            {
                hpTicks[i] = root.Q<VisualElement>($"hp-tick-{i}");
                hpStars[i] = root.Q<VisualElement>($"hp-star-{i}");
                starActiveState[i] = true;
            }

            ApplyStarThresholdPositions();

            if (pauseBtn != null)
                pauseBtn.clicked += OnPauseButtonClicked;
        }

        private void ApplyStarThresholdPositions()
        {
            for (int i = 0; i < GameManager.MaxStars; i++)
            {
                float pct = GameManager.GetStarThreshold(i) * 100f;
                if (hpTicks[i] != null) hpTicks[i].style.left = Length.Percent(pct);
                if (hpStars[i] != null) hpStars[i].style.left = Length.Percent(pct);
            }
        }

        private void MountResultPopup()
        {
            if (resultPopupAsset == null) return;

            resultOverlay = root.Q<VisualElement>("result-overlay");
            if (resultOverlay == null)
            {
                TemplateContainer container = resultPopupAsset.Instantiate();
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.pickingMode = PickingMode.Ignore;
                root.Add(container);
                resultOverlay = container.Q<VisualElement>("result-overlay");
            }

            if (resultOverlay == null) return;

            resultCoinPill = resultOverlay.Q<VisualElement>("result-coin-pill");
            resultCoinIcon = resultOverlay.Q<VisualElement>("result-coin-icon");
            resultCoinIconTxt = resultOverlay.Q<Label>("result-coin-icon-txt");
            resultCoinLabel = resultOverlay.Q<Label>("result-coin-label");
            CoinFlyEffect.ApplyCoinSprite(resultCoinIcon, resultCoinIconTxt);

            endlessChoicePanel = resultOverlay.Q<VisualElement>("endless-choice-panel");
            resultSuccessImage = resultOverlay.Q<VisualElement>("result-success-image");
            endlessTitleLabel = resultOverlay.Q<Label>("endless-title");
            clearRewardOrigin = resultOverlay.Q<VisualElement>("clear-reward-origin");
            clearRewardTitleLabel = resultOverlay.Q<Label>("clear-reward-title");
            clearRewardAmountLabel = resultOverlay.Q<Label>("clear-reward-amount");
            resultTurnOrigin = resultOverlay.Q<VisualElement>("result-turn-origin");
            resultTurnCountLabel = resultOverlay.Q<Label>("result-turn-count");
            resultTurnCoinPreviewLabel = resultOverlay.Q<Label>("result-turn-coin-preview");
            endlessContinueBtn = resultOverlay.Q<Button>("endless-continue-btn");
            endlessStopBtn = resultOverlay.Q<Button>("endless-stop-btn");

            for (int i = 0; i < 5; i++)
                resultStars[i] = resultOverlay.Q<VisualElement>($"result-star-{i}");

            if (resultSuccessImage != null)
            {
                if (victoryBannerSprite != null)
                {
                    resultSuccessImage.style.backgroundImage = new StyleBackground(victoryBannerSprite);
                    resultSuccessImage.style.display = DisplayStyle.Flex;
                }
                else
                {
                    resultSuccessImage.style.display = DisplayStyle.None;
                }
            }

            finalResultPanel = resultOverlay.Q<VisualElement>("final-result-panel");
            resultTitleLabel = resultOverlay.Q<Label>("result-title");
            resultWaveLabel = resultOverlay.Q<Label>("result-wave-label");
            resultRetryBtn = resultOverlay.Q<Button>("result-retry-btn");
            resultLobbyBtn = resultOverlay.Q<Button>("result-lobby-btn");

            if (endlessContinueBtn != null) endlessContinueBtn.clicked += OnEndlessContinueClicked;
            if (endlessStopBtn != null) endlessStopBtn.clicked += OnClearExitClicked;
            if (resultRetryBtn != null) resultRetryBtn.clicked += OnRestartClicked;
            if (resultLobbyBtn != null) resultLobbyBtn.clicked += OnLobbyClicked;

            resultOverlay.AddToClassList(HideClass);
        }

        private void UnbindUIEvents()
        {
            if (pauseBtn != null) pauseBtn.clicked -= OnPauseButtonClicked;
            if (endlessContinueBtn != null) endlessContinueBtn.clicked -= OnEndlessContinueClicked;
            if (endlessStopBtn != null) endlessStopBtn.clicked -= OnClearExitClicked;
            if (resultRetryBtn != null) resultRetryBtn.clicked -= OnRestartClicked;
            if (resultLobbyBtn != null) resultLobbyBtn.clicked -= OnLobbyClicked;
        }
        #endregion

        #region GameManager Binding
        private GameManager ResolveGameManager()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            return gameManager;
        }

        private void BindGameManager()
        {
            GameManager gm = ResolveGameManager();
            if (isBoundToGameManager || gm == null) return;

            gm.OnHPChanged += HandleHPChanged;
            gm.OnWaveChanged += HandleWaveChanged;
            gm.OnPauseChanged += HandlePauseChanged;
            gm.OnEndlessChoiceRequested += HandleClearCelebration;
            gm.OnStageResultReady += HandleDefeatOrFinalResult;
            isBoundToGameManager = true;
        }

        private void UnbindGameManager()
        {
            if (!isBoundToGameManager || gameManager == null) return;

            gameManager.OnHPChanged -= HandleHPChanged;
            gameManager.OnWaveChanged -= HandleWaveChanged;
            gameManager.OnPauseChanged -= HandlePauseChanged;
            gameManager.OnEndlessChoiceRequested -= HandleClearCelebration;
            gameManager.OnStageResultReady -= HandleDefeatOrFinalResult;
            isBoundToGameManager = false;
        }

        private void RefreshAllUI()
        {
            GameManager gm = ResolveGameManager();
            if (gm == null) return;

            lastKnownHp = gm.CurrentHP;
            ApplyStarThresholdPositions();
            HandleHPChanged(gm.CurrentHP, gm.MaxHP);
            HandleWaveChanged(gm.DisplayWave, gm.MaxWave, gm.IsEndlessMode);
            HandlePauseChanged(gm.IsPaused);
        }
        #endregion

        #region HP Beaker Stars & Clay Hit Feedback
        private void HandleHPChanged(int current, int max)
        {
            bool tookDamage = lastKnownHp >= 0 && current < lastKnownHp;
            lastKnownHp = current;

            if (hpText != null)
                hpText.text = $"HP {current} / {max}";

            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (hpBarFill != null)
                hpBarFill.style.width = Length.Percent(ratio * 100f);

            if (tookDamage)
                PlayClayDamageFeedback();

            UpdateBeakerStars(ratio, animateLoss: tookDamage);
        }

        private void PlayClayDamageFeedback()
        {
            CoinFlyEffect.TriggerLightHaptic();

            if (hpHeart != null)
            {
                heartHitSeq?.Kill();
                hpHeart.style.scale = new Scale(Vector3.one);
                hpHeart.style.rotate = new Rotate(Angle.Degrees(0f));

                heartHitSeq = DOTween.Sequence().SetUpdate(true);
                heartHitSeq.Append(DOVirtual.Vector2(Vector2.one, new Vector2(1.34f, 0.70f), 0.08f,
                    v => hpHeart.style.scale = new Scale(new Vector3(v.x, v.y, 1f))).SetEase(Ease.OutQuad));
                heartHitSeq.Join(DOVirtual.Float(0f, -14f, 0.08f,
                    deg => hpHeart.style.rotate = new Rotate(Angle.Degrees(deg))));
                heartHitSeq.Append(DOVirtual.Vector2(new Vector2(1.34f, 0.70f), new Vector2(0.86f, 1.18f), 0.10f,
                    v => hpHeart.style.scale = new Scale(new Vector3(v.x, v.y, 1f))).SetEase(Ease.OutQuad));
                heartHitSeq.Join(DOVirtual.Float(-14f, 10f, 0.10f,
                    deg => hpHeart.style.rotate = new Rotate(Angle.Degrees(deg))));
                heartHitSeq.Append(DOVirtual.Vector2(new Vector2(0.86f, 1.18f), Vector2.one, 0.14f,
                    v => hpHeart.style.scale = new Scale(new Vector3(v.x, v.y, 1f))).SetEase(Ease.OutBack));
                heartHitSeq.Join(DOVirtual.Float(10f, 0f, 0.14f,
                    deg => hpHeart.style.rotate = new Rotate(Angle.Degrees(deg))));
            }

            if (hpBarBg != null)
            {
                barShakeSeq?.Kill();
                hpBarBg.style.translate = new Translate(0f, 0f);
                barShakeSeq = DOTween.Sequence().SetUpdate(true);
                barShakeSeq.Append(DOVirtual.Float(0f, -8f, 0.04f, x => hpBarBg.style.translate = new Translate(x, 0f)));
                barShakeSeq.Append(DOVirtual.Float(-8f, 7f, 0.05f, x => hpBarBg.style.translate = new Translate(x, 0f)));
                barShakeSeq.Append(DOVirtual.Float(7f, 0f, 0.06f, x => hpBarBg.style.translate = new Translate(x, 0f)));
            }

            if (hpBarFill != null)
            {
                hpBarFill.AddToClassList(HpHitClass);
                hpBarFill.schedule.Execute(() => hpBarFill?.RemoveFromClassList(HpHitClass)).StartingIn(140);
            }
        }

        private void UpdateBeakerStars(float hpRatio, bool animateLoss)
        {
            for (int i = 0; i < GameManager.MaxStars; i++)
            {
                VisualElement star = hpStars[i];
                if (star == null) continue;

                bool isAlive = hpRatio >= GameManager.GetStarThreshold(i);
                bool wasAlive = starActiveState[i];
                starActiveState[i] = isAlive;

                star.EnableInClassList(StarLostClass, !isAlive);

                if (animateLoss && wasAlive && !isAlive)
                    PlayStarLostHeartbeat(star);
            }
        }

        private static void PlayStarLostHeartbeat(VisualElement star)
        {
            if (star == null) return;
            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(DOVirtual.Float(1f, 1.50f, 0.09f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutQuad));
            seq.Append(DOVirtual.Float(1.50f, 0.78f, 0.09f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.InQuad));
            seq.Append(DOVirtual.Float(0.78f, 1.15f, 0.07f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutQuad));
            seq.Append(DOVirtual.Float(1.15f, 1f, 0.09f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.InOutQuad));
        }

        private void HandleWaveChanged(int current, int max, bool isEndless)
        {
            if (waveText != null)
                waveText.text = isEndless ? $"WAVE {current} (ENDLESS)" : $"WAVE {current} / {max}";
        }

        private void HandlePauseChanged(bool isPaused)
        {
            if (isPaused) settingsPopup?.Open();
            else settingsPopup?.SetVisible(false);
        }
        #endregion

        #region Stage Clear Celebration & Coin / Turn Fly Sequence
        private void HandleClearCelebration(StageRewardSummary summary)
        {
            if (resultOverlay == null) return;

            clearCelebrationSeq?.Kill();
            turnDrainTween?.Kill();

            resultOverlay.RemoveFromClassList(HideClass);
            resultOverlay.BringToFront();

            if (endlessChoicePanel != null) endlessChoicePanel.style.display = DisplayStyle.Flex;
            if (finalResultPanel != null) finalResultPanel.style.display = DisplayStyle.None;
            if (resultCoinPill != null) resultCoinPill.style.display = DisplayStyle.Flex;

            CoinFlyEffect.ApplyCoinSprite(resultCoinIcon, resultCoinIconTxt);

            int coinsAfterGrant = ProgramManager.Instance != null ? ProgramManager.Instance.Coins : summary.totalCoins;
            int coinsBeforeClear = Mathf.Max(0, coinsAfterGrant - summary.totalCoins);
            int coinsAfterClearOnly = coinsBeforeClear + summary.clearCoins;

            if (resultCoinLabel != null) resultCoinLabel.text = $"{coinsBeforeClear:N0}";
            if (endlessTitleLabel != null) endlessTitleLabel.text = GetText("result_victory", "STAGE CLEAR!");
            if (clearRewardTitleLabel != null) clearRewardTitleLabel.text = GetText("result_clear_reward", "Clear Reward");
            if (clearRewardAmountLabel != null) clearRewardAmountLabel.text = $"+{summary.clearCoins:N0}";

            if (resultTurnOrigin != null)
                resultTurnOrigin.style.display = summary.remainingTurns > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (resultTurnCountLabel != null)
                resultTurnCountLabel.text = $"{summary.remainingTurns}";
            if (resultTurnCoinPreviewLabel != null)
                resultTurnCoinPreviewLabel.text = $"+{summary.turnBonusCoins:N0}";

            bool isFirstStageTutorialClear = summary.mainStageIndex == 0
                && summary.subStageIndex == 0
                && ProgramManager.Instance != null
                && !ProgramManager.Instance.IsTutorialCompleted("lobby_gacha_equip");

            if (endlessContinueBtn != null)
            {
                endlessContinueBtn.text = GetText("endless_continue_btn", "CONTINUE");
                endlessContinueBtn.style.display = isFirstStageTutorialClear ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (endlessStopBtn != null)
            {
                endlessStopBtn.text = GetText("endless_stop_btn", "EXIT");
                endlessStopBtn.style.width = isFirstStageTutorialClear ? Length.Percent(100) : Length.Percent(48);
            }

            // Reset initial animated states
            if (endlessChoicePanel != null) endlessChoicePanel.style.scale = new Scale(new Vector3(0.4f, 0.4f, 1f));
            if (resultCoinPill != null) resultCoinPill.style.translate = new Translate(260f, 0f);

            for (int i = 0; i < 5; i++)
            {
                if (resultStars[i] == null) continue;
                resultStars[i].AddToClassList(ResultStarEmptyClass);
                resultStars[i].style.scale = new Scale(Vector3.one);
            }

            SoundManager.Instance?.PlaySFX(SfxType.StageClear);

            clearCelebrationSeq = DOTween.Sequence().SetUpdate(true);

            // 1. Pop in the main victory panel with "Ta-da!" bounce + slide in top-right coin pill
            if (endlessChoicePanel != null)
            {
                clearCelebrationSeq.Append(DOVirtual.Float(0.4f, 1.08f, 0.24f,
                    s => endlessChoicePanel.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutBack));
                clearCelebrationSeq.Append(DOVirtual.Float(1.08f, 1f, 0.14f,
                    s => endlessChoicePanel.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.InOutSine));
            }

            if (resultCoinPill != null)
            {
                clearCelebrationSeq.Join(DOVirtual.Float(260f, 0f, 0.32f,
                    x => resultCoinPill.style.translate = new Translate(x, 0f)).SetEase(Ease.OutBack));
            }

            // 2. Pop banner image & title with celebratory tilt
            if (resultSuccessImage != null && victoryBannerSprite != null)
            {
                resultSuccessImage.style.scale = new Scale(Vector3.zero);
                clearCelebrationSeq.Join(DOVirtual.Float(0f, 1.15f, 0.26f,
                    s => resultSuccessImage.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutBack));
                clearCelebrationSeq.Append(DOVirtual.Float(1.15f, 1f, 0.12f,
                    s => resultSuccessImage.style.scale = new Scale(new Vector3(s, s, 1f))));
            }

            // 3. Fill earned stars one by one (Thump! Thump! Thump!)
            int earned = Mathf.Clamp(summary.starsEarned, 0, 5);
            for (int i = 0; i < earned; i++)
            {
                int starIdx = i;
                clearCelebrationSeq.AppendInterval(0.14f);
                clearCelebrationSeq.AppendCallback(() =>
                {
                    VisualElement star = resultStars[starIdx];
                    if (star == null) return;
                    star.RemoveFromClassList(ResultStarEmptyClass);
                    CoinFlyEffect.TriggerLightHaptic();
                    SoundManager.Instance?.PlaySFX(SfxType.CoinTick, pitch: 0.95f + starIdx * 0.1f, volumeMultiplier: 0.9f);

                    Sequence sSeq = DOTween.Sequence().SetUpdate(true);
                    sSeq.Append(DOVirtual.Float(0.2f, 1.45f, 0.14f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutBack));
                    sSeq.Append(DOVirtual.Float(1.45f, 1f, 0.12f, s => star.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.InOutQuad));
                });
            }

            // 4. Fly Clear Reward coins into top-right Coin Pill
            clearCelebrationSeq.AppendInterval(0.25f);
            clearCelebrationSeq.AppendCallback(() =>
            {
                Vector2 clearStart = clearRewardOrigin != null ? clearRewardOrigin.worldBound.center : new Vector2(540f, 960f);
                if (summary.clearCoins > 0)
                {
                    CoinFlyEffect.Play(
                        resultOverlay,
                        clearStart,
                        resultCoinPill,
                        resultCoinLabel,
                        coinsBeforeClear,
                        coinsAfterClearOnly,
                        visualCoinCount: 10,
                        onComplete: () => PlayTurnToCoinDrain(summary, coinsAfterClearOnly, coinsAfterGrant)
                    );
                }
                else
                {
                    PlayTurnToCoinDrain(summary, coinsAfterClearOnly, coinsAfterGrant);
                }
            });
        }

        private void PlayTurnToCoinDrain(StageRewardSummary summary, int fromCoins, int toCoins)
        {
            if (summary.remainingTurns <= 0 || summary.turnBonusCoins <= 0)
            {
                if (resultCoinLabel != null) resultCoinLabel.text = $"{toCoins:N0}";
                return;
            }

            int startTurns = summary.remainingTurns;
            float drainDuration = Mathf.Clamp(startTurns * 0.08f, 0.45f, 1.1f);

            turnDrainTween?.Kill();
            turnDrainTween = DOVirtual.Float(startTurns, 0f, drainDuration, val =>
            {
                if (resultTurnCountLabel != null)
                    resultTurnCountLabel.text = $"{Mathf.CeilToInt(val)}";
            }).SetEase(Ease.InOutQuad).SetUpdate(true).OnComplete(() =>
            {
                if (resultTurnCountLabel != null) resultTurnCountLabel.text = "0";
            });

            Vector2 turnStart = resultTurnOrigin != null ? resultTurnOrigin.worldBound.center : new Vector2(540f, 1080f);
            int coinVisuals = Mathf.Clamp(startTurns, 5, 14);
            CoinFlyEffect.Play(
                resultOverlay,
                turnStart,
                resultCoinPill,
                resultCoinLabel,
                fromCoins,
                toCoins,
                visualCoinCount: coinVisuals
            );
        }

        private void HandleDefeatOrFinalResult(StageRewardSummary summary)
        {
            if (resultOverlay == null) return;
            // On Stage Clear exit, GoToLobby is called directly; only show Defeat panel here
            if (summary.isStageCleared) return;

            resultOverlay.RemoveFromClassList(HideClass);
            resultOverlay.BringToFront();

            if (endlessChoicePanel != null) endlessChoicePanel.style.display = DisplayStyle.None;
            if (resultCoinPill != null) resultCoinPill.style.display = DisplayStyle.None;
            if (finalResultPanel != null) finalResultPanel.style.display = DisplayStyle.Flex;

            if (resultTitleLabel != null)
                resultTitleLabel.text = GetText("result_defeat", "GAME OVER");
            if (resultWaveLabel != null)
                resultWaveLabel.text = $"WAVE {summary.reachedWave} / {summary.targetWave}";
            if (resultRetryBtn != null)
                resultRetryBtn.text = GetText("pause_restart", "RETRY");
            if (resultLobbyBtn != null)
                resultLobbyBtn.text = GetText("pause_lobby", "LOBBY");
        }
        #endregion

        #region Input Handlers
        private void OnPauseButtonClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            ResolveGameManager()?.PauseGame();
        }

        private void OnEndlessContinueClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            clearCelebrationSeq?.Kill();
            turnDrainTween?.Kill();
            resultOverlay?.AddToClassList(HideClass);
            ResolveGameManager()?.ContinueEndlessMode();
        }

        private void OnClearExitClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            clearCelebrationSeq?.Kill();
            turnDrainTween?.Kill();
            ResolveGameManager()?.FinishStageAndGoToLobby();
        }

        private void OnRestartClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            ResolveGameManager()?.RestartStage();
        }

        private void OnLobbyClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            ResolveGameManager()?.GoToLobby();
        }

        private static string GetText(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string t = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(t) || t.StartsWith("#") ? fallback : t;
        }
        #endregion
    }
}
