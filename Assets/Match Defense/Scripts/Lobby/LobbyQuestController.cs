using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MatchDefense.UI;
using MatchDefense.Tutorial;

namespace MatchDefense.Lobby
{
    public class LobbyQuestController
    {
        private VisualElement _pageRoot;
        private VisualElement _lobbyRoot;
        private VisualElement _coinPill;
        private Label _coinLabel;
        private VisualElement _questListContainer;

        public void Bind(
            VisualElement questPageRoot,
            VisualElement lobbyRoot,
            VisualElement coinPill,
            Label coinLabel)
        {
            Unbind();
            _pageRoot = questPageRoot;
            _lobbyRoot = lobbyRoot;
            _coinPill = coinPill;
            _coinLabel = coinLabel;
            if (_pageRoot == null) return;

            _questListContainer = _pageRoot.Q<VisualElement>("quest-list-container");

            if (ProgramManager.Instance != null)
                ProgramManager.Instance.OnQuestsChanged += RefreshAll;

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged += RefreshAll;

            RefreshAll();
        }

        public void Unbind()
        {
            if (ProgramManager.Instance != null)
                ProgramManager.Instance.OnQuestsChanged -= RefreshAll;

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged -= RefreshAll;

            _pageRoot = null;
            _lobbyRoot = null;
            _coinPill = null;
            _coinLabel = null;
            _questListContainer = null;
        }

        public void RefreshAll()
        {
            if (_questListContainer == null || ProgramManager.Instance == null) return;

            _questListContainer.Clear();
            IReadOnlyList<QuestDefinition> defs = ProgramManager.Instance.QuestDefinitions;
            if (defs == null) return;

            bool taggedFirstReady = false;
            for (int i = 0; i < defs.Count; i++)
            {
                QuestDefinition def = defs[i];
                if (def == null) continue;

                QuestProgressRecord rec = ProgramManager.Instance.GetQuestProgress(def.questId);
                bool isCompleted = rec.currentCount >= def.targetCount;
                bool isClaimed = rec.isClaimed;

                VisualElement card = new VisualElement();
                card.AddToClassList("quest-card");
                if (isClaimed) card.AddToClassList("quest-card--claimed");
                else if (isCompleted) card.AddToClassList("quest-card--ready");

                VisualElement leftCol = new VisualElement();
                leftCol.AddToClassList("quest-left-col");

                Label titleLabel = new Label(GetText(def.titleLocalizationKey, def.fallbackTitle));
                titleLabel.AddToClassList("quest-item-title");
                leftCol.Add(titleLabel);

                VisualElement progressBg = new VisualElement();
                progressBg.AddToClassList("quest-progress-bg");

                float ratio = def.targetCount > 0 ? Mathf.Clamp01((float)rec.currentCount / def.targetCount) : 1f;
                VisualElement progressFill = new VisualElement();
                progressFill.AddToClassList("quest-progress-fill");
                progressFill.style.width = Length.Percent(ratio * 100f);
                progressBg.Add(progressFill);

                Label progressText = new Label($"{Mathf.Min(rec.currentCount, def.targetCount)} / {def.targetCount}");
                progressText.AddToClassList("quest-progress-text");
                progressBg.Add(progressText);

                leftCol.Add(progressBg);
                card.Add(leftCol);

                VisualElement rightCol = new VisualElement();
                rightCol.AddToClassList("quest-right-col");

                Label rewardLabel = new Label($"+{def.rewardCoins:N0} C");
                rewardLabel.AddToClassList("quest-reward-label");
                rightCol.Add(rewardLabel);

                string capturedId = def.questId;
                Button claimBtn = new Button();
                claimBtn.AddToClassList("quest-claim-btn");

                if (isClaimed)
                {
                    claimBtn.text = GetText("quest_claimed", "DONE");
                    claimBtn.AddToClassList("quest-claim-btn--done");
                    claimBtn.SetEnabled(false);
                }
                else if (isCompleted)
                {
                    claimBtn.text = GetText("quest_claim", "CLAIM");
                    claimBtn.AddToClassList("quest-claim-btn--ready");
                    if (!taggedFirstReady) { claimBtn.name = "quest-claim-ready-btn"; taggedFirstReady = true; }
                    claimBtn.SetEnabled(true);
                    claimBtn.clicked += () => OnClaimClicked(capturedId, claimBtn);
                }
                else
                {
                    claimBtn.text = GetText("quest_in_progress", "ONGOING");
                    claimBtn.AddToClassList("quest-claim-btn--locked");
                    claimBtn.SetEnabled(false);
                }

                rightCol.Add(claimBtn);
                card.Add(rightCol);

                _questListContainer.Add(card);
            }
        }

        private void OnClaimClicked(string questId, Button claimBtn)
        {
            if (ProgramManager.Instance == null) return;

            int beforeCoins = ProgramManager.Instance.Coins;
            Vector2 startPos = claimBtn != null ? claimBtn.worldBound.center : new Vector2(540f, 960f);

            if (!ProgramManager.Instance.TryClaimQuestReward(questId, out int reward, notifyCoinsImmediately: false))
                return;
            TutorialManager.Instance?.NotifyQuestClaimed();

            int afterCoins = ProgramManager.Instance.Coins;
            SoundManager.Instance?.PlaySFX(SfxType.QuestClaim);

            int coinParticles = Mathf.Clamp(reward / 20, 6, 14);
            CoinFlyEffect.Play(
                root: _lobbyRoot,
                startPos: startPos,
                targetCoinPill: _coinPill,
                coinLabel: _coinLabel,
                fromCoins: beforeCoins,
                toCoins: afterCoins,
                visualCoinCount: coinParticles
            );
        }

        private static string GetText(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string t = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(t) || t.StartsWith("#") ? fallback : t;
        }
    }
}
