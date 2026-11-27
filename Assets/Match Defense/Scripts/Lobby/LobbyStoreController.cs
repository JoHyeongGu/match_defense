using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using MatchDefense.Tutorial;
using MatchDefense.UI;

namespace MatchDefense.Lobby
{
    public class LobbyStoreController
    {
        private const string HideClass = "hide";

        private readonly MonoBehaviour _runner;
        private readonly string _tutorialGuaranteedUnitId;
        private readonly bool _freeGachaDuringTutorial;

        private VisualElement _pageRoot;
        private Button _drawButton;
        private Label _costLabel;
        private Label _statusLabel;

        private VisualElement _resultOverlay;
        private VisualElement _anticipationBox;
        private VisualElement _flashOverlay;
        private VisualElement _resultCard;
        private VisualElement _resultGlow;
        private VisualElement _resultUnitImage;
        private Label _resultBadgeLabel;
        private Label _resultUnitNameLabel;
        private Label _resultStackLabel;
        private Button _resultCloseBtn;

        private bool _isAnimating;

        public LobbyStoreController(
            MonoBehaviour runner,
            string tutorialGuaranteedUnitId = "baguette",
            bool freeGachaDuringTutorial = true)
        {
            _runner = runner;
            _tutorialGuaranteedUnitId = tutorialGuaranteedUnitId;
            _freeGachaDuringTutorial = freeGachaDuringTutorial;
        }

        public void Bind(VisualElement storePageRoot)
        {
            Unbind();
            _pageRoot = storePageRoot;
            if (_pageRoot == null) return;

            _drawButton = _pageRoot.Q<Button>("gacha-draw-btn");
            _costLabel = _pageRoot.Q<Label>("gacha-cost-label");
            _statusLabel = _pageRoot.Q<Label>("store-status-label");

            _resultOverlay = _pageRoot.Q<VisualElement>("gacha-result-overlay");
            _anticipationBox = _pageRoot.Q<VisualElement>("gacha-anticipation-box");
            _flashOverlay = _pageRoot.Q<VisualElement>("gacha-flash");
            _resultCard = _pageRoot.Q<VisualElement>("gacha-result-card");
            _resultGlow = _pageRoot.Q<VisualElement>("gacha-result-glow");
            _resultUnitImage = _pageRoot.Q<VisualElement>("gacha-result-icon");
            _resultBadgeLabel = _pageRoot.Q<Label>("gacha-result-badge");
            _resultUnitNameLabel = _pageRoot.Q<Label>("gacha-result-name");
            _resultStackLabel = _pageRoot.Q<Label>("gacha-result-stack");
            _resultCloseBtn = _pageRoot.Q<Button>("gacha-result-close-btn");

            if (_drawButton != null) _drawButton.clicked += OnDrawClicked;
            if (_resultCloseBtn != null) _resultCloseBtn.clicked += OnCloseResultClicked;

            if (ProgramManager.Instance != null)
                ProgramManager.Instance.OnCoinsChanged += HandleCoinsChanged;

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged += RefreshUI;

            RefreshUI();
        }

        public void Unbind()
        {
            if (_drawButton != null) _drawButton.clicked -= OnDrawClicked;
            if (_resultCloseBtn != null) _resultCloseBtn.clicked -= OnCloseResultClicked;

            if (ProgramManager.Instance != null)
                ProgramManager.Instance.OnCoinsChanged -= HandleCoinsChanged;

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged -= RefreshUI;

            _pageRoot = null;
        }

        public void RefreshUI()
        {
            if (ProgramManager.Instance == null) return;

            bool isTutorialGacha = IsTutorialGachaStep();
            int cost = ProgramManager.Instance.GachaCostCoins;
            bool canAfford = (isTutorialGacha && _freeGachaDuringTutorial) || ProgramManager.Instance.Coins >= cost;

            if (_costLabel != null)
            {
                _costLabel.text = (isTutorialGacha && _freeGachaDuringTutorial)
                    ? GetText("store_free", "FREE")
                    : $"{cost:N0} C";
            }

            if (_drawButton != null)
                _drawButton.SetEnabled(canAfford && !_isAnimating);

            if (_statusLabel != null)
            {
                _statusLabel.text = canAfford
                    ? GetText("store_ready_msg", "Draw a random unit!")
                    : GetText("store_not_enough_coins", "Not enough coins.");
            }
        }

        private void HandleCoinsChanged(int coins) => RefreshUI();

        private bool IsTutorialGachaStep()
        {
            return TutorialManager.Instance != null
                && TutorialManager.Instance.IsTutorialRunning
                && TutorialManager.Instance.CurrentStep != null
                && TutorialManager.Instance.CurrentStep.completionTrigger == TutorialCompletionTrigger.GachaDrawn;
        }

        private void OnDrawClicked()
        {
            if (_isAnimating || ProgramManager.Instance == null) return;

            bool isTutorial = IsTutorialGachaStep();
            string guaranteedId = isTutorial ? _tutorialGuaranteedUnitId : null;
            bool isFree = isTutorial && _freeGachaDuringTutorial;

            GachaResult result = ProgramManager.Instance.DrawUnitGacha(guaranteedId, isFree);
            if (!result.success || result.unit == null)
            {
                RefreshUI();
                return;
            }

            _runner.StartCoroutine(PlayGachaPresentationRoutine(result));
        }

        private IEnumerator PlayGachaPresentationRoutine(GachaResult result)
        {
            _isAnimating = true;
            RefreshUI();

            if (_resultOverlay != null)
            {
                _resultOverlay.RemoveFromClassList(HideClass);
                _resultOverlay.BringToFront();
            }

            if (_resultCard != null) _resultCard.style.display = DisplayStyle.None;
            if (_resultGlow != null) _resultGlow.style.display = DisplayStyle.None;
            if (_flashOverlay != null)
            {
                _flashOverlay.style.display = DisplayStyle.None;
                _flashOverlay.style.opacity = 0f;
            }

            SoundManager.Instance?.PlaySFX(SfxType.GachaDrumroll);

            if (_anticipationBox != null)
            {
                _anticipationBox.style.display = DisplayStyle.Flex;
                _anticipationBox.style.scale = new Scale(Vector3.one);
                _anticipationBox.style.rotate = new Rotate(Angle.Degrees(0f));

                float elapsed = 0f;
                float duration = 1.05f;
                int lastBeat = -1;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    float freq = Mathf.Lerp(16f, 44f, t);
                    float amp = Mathf.Lerp(7f, 18f, t);
                    float angle = Mathf.Sin(elapsed * freq) * amp;
                    float scale = t < 0.8f
                        ? Mathf.Lerp(1f, 1.24f, t / 0.8f)
                        : Mathf.Lerp(1.24f, 0.72f, (t - 0.8f) / 0.2f);

                    _anticipationBox.style.rotate = new Rotate(Angle.Degrees(angle));
                    _anticipationBox.style.scale = new Scale(new Vector3(scale, scale, 1f));

                    int beat = Mathf.FloorToInt(t * 4f);
                    if (beat != lastBeat && beat < 4)
                    {
                        lastBeat = beat;
                        CoinFlyEffect.TriggerLightHaptic();
                    }

                    yield return null;
                }

                _anticipationBox.style.display = DisplayStyle.None;
            }

            SoundManager.Instance?.PlaySFX(SfxType.GachaReveal);
            CoinFlyEffect.TriggerLightHaptic();

            if (_flashOverlay != null)
            {
                _flashOverlay.style.display = DisplayStyle.Flex;
                _flashOverlay.style.opacity = 0.9f;
                DOTween.To(() => 0.9f, a => _flashOverlay.style.opacity = a, 0f, 0.35f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .OnComplete(() => _flashOverlay.style.display = DisplayStyle.None);
            }

            if (_resultUnitImage != null)
            {
                Sprite icon = result.unit.thumbnailSprite != null
                    ? result.unit.thumbnailSprite
                    : result.unit.itemVisual.defaultSprite;
                _resultUnitImage.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
            }

            if (_resultBadgeLabel != null)
            {
                _resultBadgeLabel.text = result.isNewUnit
                    ? GetText("store_new_unit_badge", "NEW UNIT!")
                    : GetText("store_duplicate_badge", "+1 STACK");
                _resultBadgeLabel.EnableInClassList("badge--new", result.isNewUnit);
                _resultBadgeLabel.EnableInClassList("badge--stack", !result.isNewUnit);
            }

            if (_resultUnitNameLabel != null)
                _resultUnitNameLabel.text = GetText(result.unit.nameLocalizationKey, result.unit.unitId);

            if (_resultStackLabel != null)
            {
                _resultStackLabel.text = result.isNewUnit
                    ? GetText("store_check_inventory", "Check it in your Inventory!")
                    : $"Lv.{result.currentLevel}  ({result.currentStack} / {result.requiredStackForUpgrade})";
            }

            if (_resultCloseBtn != null)
                _resultCloseBtn.SetEnabled(false);

            if (_resultGlow != null)
            {
                _resultGlow.style.display = DisplayStyle.Flex;
                _resultGlow.style.rotate = new Rotate(Angle.Degrees(0f));
                DOTween.To(
                    () => 0f,
                    deg => _resultGlow.style.rotate = new Rotate(Angle.Degrees(deg)),
                    360f,
                    1.6f
                ).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (_resultCard != null)
            {
                _resultCard.style.display = DisplayStyle.Flex;
                _resultCard.style.scale = new Scale(new Vector3(0.2f, 0.2f, 1f));
                DOTween.To(
                    () => 0.2f,
                    v => _resultCard.style.scale = new Scale(new Vector3(v, v, 1f)),
                    1f,
                    0.45f
                ).SetEase(Ease.OutBack).SetUpdate(true);
            }

            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                yield return null;

            if (_resultCloseBtn != null)
                _resultCloseBtn.SetEnabled(true);

            _isAnimating = false;
            RefreshUI();
        }

        private void OnCloseResultClicked()
        {
            SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            _resultOverlay?.AddToClassList(HideClass);
            TutorialManager.Instance?.NotifyGachaDrawn();
        }

        private static string GetText(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string t = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(t) || t.StartsWith("#") ? fallback : t;
        }
    }
}
