using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MatchDefense.Defense;
using MatchDefense.Tutorial;

namespace MatchDefense.Lobby
{
    public class LobbyInventoryController
    {
        private VisualElement _pageRoot;
        private VisualElement _equipSlotsContainer;
        private VisualElement _ownedGridContainer;

        private VisualElement _detailPanel;
        private VisualElement _detailIcon;
        private Label _detailNameLabel;
        private Label _detailLevelLabel;
        private Label _detailStatsLabel;
        private VisualElement _detailStackFill;
        private Label _detailStackLabel;
        private Button _equipToggleBtn;
        private Button _upgradeBtn;

        private string _selectedUnitId;

        public void Bind(VisualElement inventoryPageRoot)
        {
            Unbind();
            _pageRoot = inventoryPageRoot;
            if (_pageRoot == null) return;

            _equipSlotsContainer = _pageRoot.Q<VisualElement>("equip-slots-row");
            _ownedGridContainer = _pageRoot.Q<VisualElement>("owned-units-grid");

            _detailPanel = _pageRoot.Q<VisualElement>("inven-detail-panel");
            _detailIcon = _pageRoot.Q<VisualElement>("detail-unit-icon");
            _detailNameLabel = _pageRoot.Q<Label>("detail-unit-name");
            _detailLevelLabel = _pageRoot.Q<Label>("detail-unit-level");
            _detailStatsLabel = _pageRoot.Q<Label>("detail-unit-stats");
            _detailStackFill = _pageRoot.Q<VisualElement>("detail-stack-fill");
            _detailStackLabel = _pageRoot.Q<Label>("detail-unit-stack");
            _equipToggleBtn = _pageRoot.Q<Button>("detail-equip-btn");
            _upgradeBtn = _pageRoot.Q<Button>("detail-upgrade-btn");

            if (_equipToggleBtn != null) _equipToggleBtn.clicked += OnEquipToggleClicked;
            if (_upgradeBtn != null) _upgradeBtn.clicked += OnUpgradeClicked;

            if (ProgramManager.Instance != null)
            {
                ProgramManager.Instance.OnInventoryChanged += RefreshAll;
                ProgramManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            }

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged += RefreshAll;

            RefreshAll();
        }

        public void Unbind()
        {
            if (_equipToggleBtn != null) _equipToggleBtn.clicked -= OnEquipToggleClicked;
            if (_upgradeBtn != null) _upgradeBtn.clicked -= OnUpgradeClicked;

            if (ProgramManager.Instance != null)
            {
                ProgramManager.Instance.OnInventoryChanged -= RefreshAll;
                ProgramManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            }

            if (UIManager.Instance != null)
                UIManager.Instance.OnLanguageChanged -= RefreshAll;

            _pageRoot = null;
        }

        private void HandleCoinsChanged(int coins) => RefreshDetailPanel();

        public void RefreshAll()
        {
            if (ProgramManager.Instance == null || _pageRoot == null) return;

            var owned = ProgramManager.Instance.GetAllOwnedUnits();
            if (string.IsNullOrEmpty(_selectedUnitId) && owned.Count > 0)
                _selectedUnitId = owned[owned.Count - 1].unitId;

            RebuildEquipSlots();
            RebuildOwnedGrid();
            RefreshDetailPanel();
        }

        private void RebuildEquipSlots()
        {
            if (_equipSlotsContainer == null || ProgramManager.Instance == null) return;
            _equipSlotsContainer.Clear();

            IReadOnlyList<string> equippedIds = ProgramManager.Instance.GetEquippedUnitIds();

            for (int i = 0; i < ProgramManager.MaxEquipSlots; i++)
            {
                int slotIndex = i;
                string unitId = i < equippedIds.Count ? equippedIds[i] : string.Empty;
                UnitCatalogEntry entry = ProgramManager.Instance.GetUnitCatalogEntry(unitId);
                OwnedUnitRecord record = ProgramManager.Instance.GetOwnedUnit(unitId);

                Button slotBtn = new Button { name = $"equip-slot-{slotIndex}" };
                slotBtn.AddToClassList("inven-equip-slot");
                if (entry == null) slotBtn.AddToClassList("inven-equip-slot--empty");

                if (entry != null && record != null)
                {
                    VisualElement iconEl = new VisualElement { pickingMode = PickingMode.Ignore };
                    iconEl.AddToClassList("inven-slot-icon");
                    Sprite sp = entry.thumbnailSprite != null ? entry.thumbnailSprite : entry.itemVisual.defaultSprite;
                    if (sp != null) iconEl.style.backgroundImage = new StyleBackground(sp);
                    slotBtn.Add(iconEl);

                    Label lvLabel = new Label($"Lv.{record.level}") { pickingMode = PickingMode.Ignore };
                    lvLabel.AddToClassList("inven-slot-level");
                    slotBtn.Add(lvLabel);

                    slotBtn.clicked += () =>
                    {
                        SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
                        _selectedUnitId = unitId;
                        RefreshAll();
                    };
                }
                else
                {
                    Label emptyLabel = new Label("+") { pickingMode = PickingMode.Ignore };
                    emptyLabel.AddToClassList("inven-slot-plus");
                    slotBtn.Add(emptyLabel);

                    slotBtn.clicked += () =>
                    {
                        if (!string.IsNullOrEmpty(_selectedUnitId) && !ProgramManager.Instance.IsUnitEquipped(_selectedUnitId))
                        {
                            if (ProgramManager.Instance.EquipUnit(slotIndex, _selectedUnitId))
                            {
                                SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
                                TutorialManager.Instance?.NotifyUnitEquipped();
                            }
                        }
                    };
                }

                _equipSlotsContainer.Add(slotBtn);
            }
        }

        private void RebuildOwnedGrid()
        {
            if (_ownedGridContainer == null || ProgramManager.Instance == null) return;
            _ownedGridContainer.Clear();

            IReadOnlyList<OwnedUnitRecord> owned = ProgramManager.Instance.GetAllOwnedUnits();
            for (int i = 0; i < owned.Count; i++)
            {
                OwnedUnitRecord record = owned[i];
                UnitCatalogEntry entry = ProgramManager.Instance.GetUnitCatalogEntry(record.unitId);
                if (entry == null) continue;

                string capturedId = record.unitId;
                bool isEquipped = ProgramManager.Instance.IsUnitEquipped(capturedId);
                bool isSelected = capturedId == _selectedUnitId;
                int reqStacks = entry.GetRequiredStacksForNextLevel(record.level);
                bool isMax = record.level >= entry.maxLevel;
                bool stackReady = !isMax && record.stackCount >= reqStacks;

                Button card = new Button { name = $"inven-unit-{i}" };
                card.AddToClassList("inven-unit-card");
                if (isSelected) card.AddToClassList("inven-unit-card--selected");
                if (isEquipped) card.AddToClassList("inven-unit-card--equipped");

                VisualElement iconEl = new VisualElement { pickingMode = PickingMode.Ignore };
                iconEl.AddToClassList("inven-card-icon");
                Sprite sp = entry.thumbnailSprite != null ? entry.thumbnailSprite : entry.itemVisual.defaultSprite;
                if (sp != null) iconEl.style.backgroundImage = new StyleBackground(sp);
                card.Add(iconEl);

                Label lvLabel = new Label($"Lv.{record.level}") { pickingMode = PickingMode.Ignore };
                lvLabel.AddToClassList("inven-card-level");
                card.Add(lvLabel);

                VisualElement barBg = new VisualElement { pickingMode = PickingMode.Ignore };
                barBg.AddToClassList("inven-card-bar-bg");

                float ratio = isMax ? 1f : Mathf.Clamp01((float)record.stackCount / Mathf.Max(1, reqStacks));
                VisualElement barFill = new VisualElement { pickingMode = PickingMode.Ignore };
                barFill.AddToClassList("inven-card-bar-fill");
                if (stackReady) barFill.AddToClassList("inven-bar-fill--ready");
                barFill.style.width = Length.Percent(ratio * 100f);
                barBg.Add(barFill);

                Label stackLabel = new Label(isMax ? "MAX" : $"{record.stackCount}/{reqStacks}") { pickingMode = PickingMode.Ignore };
                stackLabel.AddToClassList("inven-card-bar-txt");
                barBg.Add(stackLabel);

                card.Add(barBg);

                if (isEquipped)
                {
                    Label eqBadge = new Label("E") { pickingMode = PickingMode.Ignore };
                    eqBadge.AddToClassList("inven-equipped-badge");
                    card.Add(eqBadge);
                }

                card.clicked += () =>
                {
                    SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
                    _selectedUnitId = capturedId;
                    RefreshAll();
                };

                _ownedGridContainer.Add(card);
            }
        }

        private void RefreshDetailPanel()
        {
            if (_detailPanel == null || ProgramManager.Instance == null) return;

            UnitCatalogEntry entry = ProgramManager.Instance.GetUnitCatalogEntry(_selectedUnitId);
            OwnedUnitRecord record = ProgramManager.Instance.GetOwnedUnit(_selectedUnitId);

            if (entry == null || record == null)
            {
                _detailPanel.style.display = DisplayStyle.None;
                return;
            }

            _detailPanel.style.display = DisplayStyle.Flex;

            if (_detailIcon != null)
            {
                Sprite sp = entry.thumbnailSprite != null ? entry.thumbnailSprite : entry.itemVisual.defaultSprite;
                _detailIcon.style.backgroundImage = sp != null ? new StyleBackground(sp) : StyleKeyword.None;
            }

            if (_detailNameLabel != null)
                _detailNameLabel.text = GetText(entry.nameLocalizationKey, entry.unitId);

            if (_detailLevelLabel != null)
                _detailLevelLabel.text = $"Lv.{record.level} / {entry.maxLevel}";

            if (_detailStatsLabel != null)
            {
                Unit prefabUnit = entry.unitPrefab != null ? entry.unitPrefab.GetComponent<Unit>() : null;
                float baseDmg = prefabUnit != null ? prefabUnit.damage : 1f;
                float baseCd = prefabUnit != null ? prefabUnit.cooldown : 1f;
                float curDmg = baseDmg + (record.level - 1) * entry.bonusDamagePerLevel;
                float curCd = baseCd * Mathf.Clamp(1f - (record.level - 1) * entry.cooldownReduceRatePerLevel, 0.4f, 1f);
                _detailStatsLabel.text = $"ATK {curDmg:0.0}  |  CD {curCd:0.00}s";
            }

            int reqStacks = entry.GetRequiredStacksForNextLevel(record.level);
            int coinCost = entry.GetUpgradeCoinCost(record.level);
            bool isMax = record.level >= entry.maxLevel;
            bool stackReady = !isMax && record.stackCount >= reqStacks;

            if (_detailStackFill != null)
            {
                float ratio = isMax ? 1f : Mathf.Clamp01((float)record.stackCount / Mathf.Max(1, reqStacks));
                _detailStackFill.style.width = Length.Percent(ratio * 100f);
                _detailStackFill.EnableInClassList("inven-bar-fill--ready", stackReady);
            }

            if (_detailStackLabel != null)
            {
                _detailStackLabel.text = isMax
                    ? "MAX LEVEL"
                    : stackReady
                        ? $"{GetText("inven_upgrade_ready", "READY!")} ({record.stackCount} / {reqStacks})"
                        : $"{record.stackCount} / {reqStacks}";
            }

            bool isEquipped = ProgramManager.Instance.IsUnitEquipped(_selectedUnitId);
            if (_equipToggleBtn != null)
            {
                _equipToggleBtn.text = isEquipped
                    ? GetText("inven_unequip", "UNEQUIP")
                    : GetText("inven_equip", "EQUIP");
            }

            if (_upgradeBtn != null)
            {
                bool canUpgrade = ProgramManager.Instance.CanUpgradeUnit(_selectedUnitId);
                _upgradeBtn.SetEnabled(canUpgrade);
                _upgradeBtn.text = isMax
                    ? "MAX"
                    : $"{GetText("inven_upgrade", "UPGRADE")} ({coinCost:N0} C)";
            }
        }

        private void OnEquipToggleClicked()
        {
            if (ProgramManager.Instance == null || string.IsNullOrEmpty(_selectedUnitId)) return;

            int currentSlot = ProgramManager.Instance.GetEquippedSlotIndex(_selectedUnitId);
            if (currentSlot >= 0)
            {
                if (ProgramManager.Instance.UnequipSlot(currentSlot))
                    SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
            }
            else
            {
                int emptySlot = ProgramManager.Instance.FindFirstEmptyEquipSlot();
                int targetSlot = emptySlot >= 0 ? emptySlot : ProgramManager.MaxEquipSlots - 1;
                if (ProgramManager.Instance.EquipUnit(targetSlot, _selectedUnitId))
                {
                    SoundManager.Instance?.PlaySFX(SfxType.ButtonClick);
                    TutorialManager.Instance?.NotifyUnitEquipped();
                }
            }
        }

        private void OnUpgradeClicked()
        {
            if (ProgramManager.Instance == null || string.IsNullOrEmpty(_selectedUnitId)) return;
            if (ProgramManager.Instance.TryUpgradeUnit(_selectedUnitId))
            {
                SoundManager.Instance?.PlaySFX(SfxType.UpgradeSuccess);
            }
        }

        private static string GetText(string key, string fallback)
        {
            if (UIManager.Instance == null) return fallback;
            string t = UIManager.Instance.GetText(key);
            return string.IsNullOrEmpty(t) || t.StartsWith("#") ? fallback : t;
        }
    }
}
