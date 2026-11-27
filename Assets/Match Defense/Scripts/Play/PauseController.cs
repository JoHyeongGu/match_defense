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

        [Header("<color=yellow>References</color>")]
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private GameManager gameManager;

        [Header("<color=yellow>UI Assets</color>")]
        [SerializeField] private VisualTreeAsset pausePopupAsset;
        [SerializeField] private VisualTreeAsset resultPopupAsset;

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

        private SettingsPopupController settingsPopup;

        private VisualElement resultOverlay;
        private VisualElement endlessChoicePanel;
        private Label endlessInfoLabel;
        private Button endlessContinueBtn;
        private Button endlessStopClaimBtn;

        private VisualElement finalResultPanel;
        private Label resultTitleLabel;
        private Label resultWaveLabel;
        private VisualElement rewardBox;
        private Label clearCoinLabel;
        private Label turnBonusLabel;
        private Label totalCoinLabel;
        private Button resultRetryBtn;
        private Button resultLobbyBtn;

        private Tween coinCountTween;
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

            coinCountTween?.Kill();
            heartHitSeq?.Kill();
            barShakeSeq?.Kill();
            UnbindGameManager();
            UnbindUIEvents();
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

            endlessChoicePanel = resultOverlay.Q<VisualElement>("endless-choice-panel");
            endlessInfoLabel = resultOverlay.Q<Label>("endless-info-label");
            endlessContinueBtn = resultOverlay.Q<Button>("endless-continue-btn");
            endlessStopClaimBtn = resultOverlay.Q<Button>("endless-stop-btn");

            finalResultPanel = resultOverlay.Q<VisualElement>("final-result-panel");
            resultTitleLabel = resultOverlay.Q<Label>("result-title");
            resultWaveLabel = resultOverlay.Q<Label>("result-wave-label");
            rewardBox = resultOverlay.Q<VisualElement>("result-reward-box");
            clearCoinLabel = resultOverlay.Q<Label>("result-clear-coin");
            turnBonusLabel = resultOverlay.Q<Label>("result-turn-bonus");
            totalCoinLabel = resultOverlay.Q<Label>("result-total-coin");
            resultRetryBtn = resultOverlay.Q<Button>("result-retry-btn");
            resultLobbyBtn = resultOverlay.Q<Button>("result-lobby-btn");

            if (endlessContinueBtn != null) endlessContinueBtn.clicked += OnEndlessContinueClicked;
            if (endlessStopClaimBtn != null) endlessStopClaimBtn.clicked += OnEndlessStopClaimClicked;
            if (resultRetryBtn != null) resultRetryBtn.clicked += OnRestartClicked;
            if (resultLobbyBtn != null) resultLobbyBtn.clicked += OnLobbyClicked;

            resultOverlay.AddToClassList(HideClass);
        }

        private void UnbindUIEvents()
        {
            if (pauseBtn != null) pauseBtn.clicked -= OnPauseButtonClicked;
            if (endlessContinueBtn != null) endlessContinueBtn.clicked -= OnEndlessContinueClicked;
            if (endlessStopClaimBtn != null) endlessStopClaimBtn.clicked -= OnEndlessStopClaimClicked;
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
            gm.OnEndlessChoiceRequested += HandleEndlessChoiceRequested;
            gm.OnStageResultReady += HandleStageResultReady;
            isBoundToGameManager = true;
        }

        private void UnbindGameManager()
        {
            if (!isBoundToGameManager || gameManager == null) return;

            gameManager.OnHPChanged -= HandleHPChanged;
            gameManager.OnWaveChanged -= HandleWaveChanged;
            gameManager.OnPauseChanged -= HandlePauseChanged;
            gameManager.OnEndlessChoiceRequested -= HandleEndlessChoiceRequested;
            gameManager.OnStageResultReady -= HandleStageResultReady;
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
                {
                    PlayStarLostHeartbeat(star);
                }
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

        private void HandleEndlessChoiceRequested(StageRewardSummary preview)
        {
            if (resultOverlay == null) return;

            resultOverlay.RemoveFromClassList(HideClass);
            resultOverlay.BringToFront();

            if (endlessChoicePanel != null) endlessChoicePanel.style.display = DisplayStyle.Flex;
            if (finalResultPanel != null) finalResultPanel.style.display = DisplayStyle.None;

            if (endlessInfoLabel != null)
            {
                string msg = GetText("endless_prompt_desc", "You cleared Wave 40! Continue in Endless Mode or stop and claim your coins?");
                endlessInfoLabel.text = $"{msg}\n(+{preview.totalCoins:N0} C)";
            }

            if (endlessContinueBtn != null) endlessContinueBtn.text = GetText("endless_continue_btn", "ENDLESS MODE");
            if (endlessStopClaimBtn != null) endlessStopClaimBtn.text = GetText("endless_stop_btn", "STOP & CLAIM");
        }

        private void HandleStageResultReady(StageRewardSummary summary)
        {
            if (resultOverlay == null) return;

            resultOverlay.RemoveFromClassList(HideClass);
            resultOverlay.BringToFront();

            if (endlessChoicePanel != null) endlessChoicePanel.style.display = DisplayStyle.None;
            if (finalResultPanel != null) finalResultPanel.style.display = DisplayStyle.Flex;

            if (resultTitleLabel != null)
            {
                resultTitleLabel.text = summary.isStageCleared
                    ? GetText("result_victory", "STAGE CLEAR!")
                    : GetText("result_defeat", "GAME OVER");
            }

            if (resultWaveLabel != null)
                resultWaveLabel.text = $"WAVE {summary.reachedWave} / {summary.targetWave}";

            if (rewardBox != null)
                rewardBox.style.display = summary.isStageCleared ? DisplayStyle.Flex : DisplayStyle.None;

            if (summary.isStageCleared)
            {
                if (clearCoinLabel != null)
                    clearCoinLabel.text = $"{GetText("result_clear_reward", "Clear Reward")}: +{summary.clearCoins:N0} C";

                if (turnBonusLabel != null)
                    turnBonusLabel.text = $"{GetText("result_turn_bonus", "Remaining Turns")} ({summary.remainingTurns}): +{summary.turnBonusCoins:N0} C";

                PlayCoinCountUpAnimation(summary.totalCoins);
            }

            if (resultRetryBtn != null) resultRetryBtn.text = GetText("pause_restart", "RETRY");
            if (resultLobbyBtn != null) resultLobbyBtn.text = GetText("pause_lobby", "LOBBY");
        }

        private void PlayCoinCountUpAnimation(int targetCoins)
        {
            if (totalCoinLabel == null) return;

            coinCountTween?.Kill();
            int displayVal = 0;
            totalCoinLabel.text = "+0 C";

            coinCountTween = DOTween.To(
                () => displayVal,
                v =>
                {
                    displayVal = v;
                    totalCoinLabel.text = $"+{displayVal:N0} C";
                },
                targetCoins,
                0.9f
            ).SetEase(Ease.OutCubic).SetUpdate(true);
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
            resultOverlay?.AddToClassList(HideClass);
            ResolveGameManager()?.ContinueEndlessMode();
        }

        private void OnEndlessStopClaimClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            ResolveGameManager()?.FinishStageAndClaimRewards();
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
