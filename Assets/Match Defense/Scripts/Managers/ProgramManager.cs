using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using MatchDefense.Match;

namespace MatchDefense
{
    [RequireComponent(typeof(PanelRenderer))]
    public class ProgramManager : MonoBehaviour
    {
        public static ProgramManager Instance { get; private set; }

        public const int MaxEquipSlots = 5;
        private const string SaveDataPrefsKey = "MatchDefense_PlayerSaveData_v2";

        [Header("<color=yellow>Master Catalogs</color>")]
        [SerializeField] private List<UnitCatalogEntry> unitCatalog = new List<UnitCatalogEntry>();
        [SerializeField] private List<MainStageConfig> mainStages = new List<MainStageConfig>();
        [SerializeField] private List<QuestDefinition> questDefinitions = new List<QuestDefinition>();

        [Header("<color=yellow>Economy & Gacha</color>")]
        [SerializeField] private int gachaCostCoins = 100;
        [SerializeField] private int initialCoins = 0;
        [SerializeField] private Sprite coinIconSprite;

        public IReadOnlyList<UnitCatalogEntry> UnitCatalog => unitCatalog;
        public IReadOnlyList<MainStageConfig> MainStages => mainStages;
        public IReadOnlyList<QuestDefinition> QuestDefinitions => questDefinitions;
        public int GachaCostCoins => gachaCostCoins;
        public Sprite CoinIconSprite => coinIconSprite;

        public int CurrentMainStageIndex { get; private set; } = 0;
        public int CurrentSubStageIndex { get; private set; } = 0;
        public int PendingLobbySubStageIndex { get; private set; } = -1;
        public int PendingWorldTransitionFromMain { get; private set; } = -1;
        public int PendingWorldTransitionToMain { get; private set; } = -1;
        public bool PendingLobbyTutorial { get; private set; } = false;

        public int Coins => _saveData.coins;
        public int Jewels => _saveData.jewels;
        public int HighestUnlockedMainStage => _saveData.highestUnlockedMainStage;
        public int HighestUnlockedSubStage => _saveData.highestUnlockedSubStage;
        public float BgmVolume => _saveData.bgmVolume;
        public float SfxVolume => _saveData.sfxVolume;
        public bool IsVibrationEnabled => _saveData.isVibrationEnabled;

        public event Action<int> OnCoinsChanged;
        public event Action OnInventoryChanged;
        public event Action OnStageProgressChanged;
        public event Action OnQuestsChanged;
        public event Action<string> OnQuestClaimed;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
        public event Action<bool> OnVibrationChanged;
        public event Action OnPlayerDataReset;

        private PlayerSaveData _saveData = new PlayerSaveData();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                _panelRenderer = GetComponent<PanelRenderer>();
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);

                LoadPlayerData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            DestroyFadeScreen();
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            SyncAudioSettings();
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            InitFadeScreen(root);
        }

        #region Fade Screen
        public bool IsLoading => _fadeScreen == null ? _currentAlpha > 0.05f
                                : _fadeScreen.resolvedStyle.opacity > 0.05f;
        private Tween _fadeTween;
        private VisualElement _fadeScreen;
        private PanelRenderer _panelRenderer;
        private Color _currentColor = Color.black;
        private float _currentAlpha = 1f;

        private void InitFadeScreen(VisualElement root)
        {
            _fadeScreen = new VisualElement { pickingMode = PickingMode.Ignore };
            _fadeScreen.style.position = Position.Absolute;
            _fadeScreen.style.top = 0;
            _fadeScreen.style.bottom = 0;
            _fadeScreen.style.left = 0;
            _fadeScreen.style.right = 0;
            UpdateFadeVisual(_currentAlpha, _currentColor);
            root.Add(_fadeScreen);
        }

        private void DestroyFadeScreen()
        {
            _fadeTween?.Kill();
        }

        public void FadeScreen(bool off, Color? color = null, float duration = 0.5f)
        {
            _currentColor = color ?? Color.black;
            float targetAlpha = off ? 1f : 0f;
            _fadeTween?.Kill();
            if (_fadeScreen == null)
            {
                _currentAlpha = targetAlpha;
                return;
            }
            if (duration <= 0.01f)
            {
                _currentAlpha = targetAlpha;
                UpdateFadeVisual(_currentAlpha, _currentColor);
                return;
            }
            _fadeScreen.pickingMode = off ? PickingMode.Position : PickingMode.Ignore;
            _fadeTween = DOTween.To(
                () => _fadeScreen.resolvedStyle.opacity,
                alpha =>
                {
                    _currentAlpha = alpha;
                    UpdateFadeVisual(alpha, _currentColor);
                },
                targetAlpha,
                duration
            ).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        private void UpdateFadeVisual(float alpha, Color color)
        {
            if (_fadeScreen == null) return;

            _fadeScreen.style.backgroundColor = color;
            _fadeScreen.style.opacity = alpha;
            _fadeScreen.pickingMode = alpha > 0.05f ? PickingMode.Position : PickingMode.Ignore;
            _fadeScreen.style.display = alpha > 0.005f ? DisplayStyle.Flex : DisplayStyle.None;
        }
        #endregion

        #region Save / Load / Reset
        public void LoadPlayerData()
        {
            if (PlayerPrefs.HasKey(SaveDataPrefsKey))
            {
                string json = PlayerPrefs.GetString(SaveDataPrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        _saveData = JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData();
                    }
                    catch
                    {
                        _saveData = CreateDefaultSaveData();
                    }
                }
                else
                {
                    _saveData = CreateDefaultSaveData();
                }
            }
            else
            {
                _saveData = CreateDefaultSaveData();
                SavePlayerData();
            }

            ValidateSaveData();
            SyncAudioSettings();
        }

        private PlayerSaveData CreateDefaultSaveData()
        {
            var data = new PlayerSaveData
            {
                coins = initialCoins,
                jewels = 0,
                highestUnlockedMainStage = 0,
                highestUnlockedSubStage = 0,
                hasClearedFirstStage = false,
                subStageStars = new List<SubStageStarRecord>(),
                ownedUnits = new List<OwnedUnitRecord>(),
                equippedUnitIds = new List<string>(),
                completedTutorials = new List<string>(),
                questProgress = new List<QuestProgressRecord>(),
                bgmVolume = 1f,
                sfxVolume = 1f,
                isVibrationEnabled = true
            };

            UnitCatalogEntry defaultUnit = FindDefaultUnlockedUnit();
            if (defaultUnit != null)
            {
                data.ownedUnits.Add(new OwnedUnitRecord
                {
                    unitId = defaultUnit.unitId,
                    level = 1,
                    stackCount = 0
                });
                data.equippedUnitIds.Add(defaultUnit.unitId);
            }

            while (data.equippedUnitIds.Count < MaxEquipSlots)
                data.equippedUnitIds.Add(string.Empty);

            return data;
        }

        private void ValidateSaveData()
        {
            _saveData.subStageStars ??= new List<SubStageStarRecord>();
            _saveData.ownedUnits ??= new List<OwnedUnitRecord>();
            _saveData.equippedUnitIds ??= new List<string>();
            _saveData.completedTutorials ??= new List<string>();
            _saveData.questProgress ??= new List<QuestProgressRecord>();

            if (_saveData.highestUnlockedMainStage < 0) _saveData.highestUnlockedMainStage = 0;
            if (_saveData.highestUnlockedSubStage < 0) _saveData.highestUnlockedSubStage = 0;

            if (_saveData.ownedUnits.Count == 0)
            {
                UnitCatalogEntry defaultUnit = FindDefaultUnlockedUnit();
                if (defaultUnit != null)
                {
                    _saveData.ownedUnits.Add(new OwnedUnitRecord
                    {
                        unitId = defaultUnit.unitId,
                        level = 1,
                        stackCount = 0
                    });
                }
            }

            while (_saveData.equippedUnitIds.Count < MaxEquipSlots)
                _saveData.equippedUnitIds.Add(string.Empty);

            if (_saveData.equippedUnitIds.Count > MaxEquipSlots)
                _saveData.equippedUnitIds.RemoveRange(MaxEquipSlots, _saveData.equippedUnitIds.Count - MaxEquipSlots);

            bool anyEquipped = false;
            for (int i = 0; i < _saveData.equippedUnitIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(_saveData.equippedUnitIds[i]))
                {
                    anyEquipped = true;
                    break;
                }
            }
            if (!anyEquipped && _saveData.ownedUnits.Count > 0)
                _saveData.equippedUnitIds[0] = _saveData.ownedUnits[0].unitId;
        }

        private UnitCatalogEntry FindDefaultUnlockedUnit()
        {
            if (unitCatalog == null || unitCatalog.Count == 0) return null;
            for (int i = 0; i < unitCatalog.Count; i++)
            {
                if (unitCatalog[i] != null && unitCatalog[i].unlockedByDefault)
                    return unitCatalog[i];
            }
            return unitCatalog[0];
        }

        public void SavePlayerData()
        {
            string json = JsonUtility.ToJson(_saveData);
            PlayerPrefs.SetString(SaveDataPrefsKey, json);
            PlayerPrefs.Save();
        }

        public void ResetAllPlayerData()
        {
            float savedBgm = _saveData.bgmVolume;
            float savedSfx = _saveData.sfxVolume;
            bool savedVib = _saveData.isVibrationEnabled;

            PlayerPrefs.DeleteKey(SaveDataPrefsKey);
            _saveData = CreateDefaultSaveData();
            _saveData.bgmVolume = savedBgm;
            _saveData.sfxVolume = savedSfx;
            _saveData.isVibrationEnabled = savedVib;

            CurrentMainStageIndex = 0;
            CurrentSubStageIndex = 0;
            PendingLobbySubStageIndex = 0;
            PendingWorldTransitionFromMain = -1;
            PendingWorldTransitionToMain = -1;
            PendingLobbyTutorial = false;

            SavePlayerData();
            SyncAudioSettings();

            OnCoinsChanged?.Invoke(_saveData.coins);
            OnInventoryChanged?.Invoke();
            OnStageProgressChanged?.Invoke();
            OnQuestsChanged?.Invoke();
            OnPlayerDataReset?.Invoke();
        }
        #endregion

        #region Quest System
        public QuestProgressRecord GetQuestProgress(string questId)
        {
            for (int i = 0; i < _saveData.questProgress.Count; i++)
            {
                if (_saveData.questProgress[i].questId == questId)
                    return _saveData.questProgress[i];
            }

            var record = new QuestProgressRecord { questId = questId, currentCount = 0, isClaimed = false };
            _saveData.questProgress.Add(record);
            return record;
        }

        public void AddQuestProgress(QuestType type, int amount = 1)
        {
            if (amount <= 0 || questDefinitions == null) return;

            bool changed = false;
            for (int i = 0; i < questDefinitions.Count; i++)
            {
                QuestDefinition def = questDefinitions[i];
                if (def == null || def.questType != type) continue;

                QuestProgressRecord rec = GetQuestProgress(def.questId);
                if (rec.isClaimed) continue;

                int next = Mathf.Min(def.targetCount, rec.currentCount + amount);
                if (next != rec.currentCount)
                {
                    rec.currentCount = next;
                    changed = true;
                }
            }

            if (changed)
            {
                SavePlayerData();
                OnQuestsChanged?.Invoke();
            }
        }

        public bool CanClaimQuest(string questId)
        {
            if (questDefinitions == null) return false;
            for (int i = 0; i < questDefinitions.Count; i++)
            {
                QuestDefinition def = questDefinitions[i];
                if (def == null || def.questId != questId) continue;
                QuestProgressRecord rec = GetQuestProgress(questId);
                return !rec.isClaimed && rec.currentCount >= def.targetCount;
            }
            return false;
        }

        public bool TryClaimQuestReward(string questId, out int rewardCoins, bool notifyCoinsImmediately = true)
        {
            rewardCoins = 0;
            if (!CanClaimQuest(questId)) return false;

            for (int i = 0; i < questDefinitions.Count; i++)
            {
                QuestDefinition def = questDefinitions[i];
                if (def == null || def.questId != questId) continue;

                QuestProgressRecord rec = GetQuestProgress(questId);
                rec.isClaimed = true;
                rewardCoins = def.rewardCoins;
                _saveData.coins += def.rewardCoins;
                SavePlayerData();

                if (notifyCoinsImmediately) OnCoinsChanged?.Invoke(_saveData.coins);
                OnQuestsChanged?.Invoke();
                OnQuestClaimed?.Invoke(questId);
                return true;
            }
            return false;
        }
        #endregion

        #region Main & Sub Stage Management
        public MainStageConfig GetMainStageConfig(int mainIndex)
        {
            if (mainStages == null || mainStages.Count == 0) return null;
            return mainStages[Mathf.Clamp(mainIndex, 0, mainStages.Count - 1)];
        }

        public SubStageConfig GetSubStageConfig(int mainIndex, int subIndex)
        {
            MainStageConfig main = GetMainStageConfig(mainIndex);
            if (main == null || main.subStages == null || main.subStages.Count == 0) return null;
            return main.subStages[Mathf.Clamp(subIndex, 0, main.subStages.Count - 1)];
        }

        public MainStageConfig GetCurrentMainStageConfig() => GetMainStageConfig(CurrentMainStageIndex);
        public SubStageConfig GetCurrentSubStageConfig() => GetSubStageConfig(CurrentMainStageIndex, CurrentSubStageIndex);

        public int GetSubStageCount(int mainIndex)
        {
            MainStageConfig main = GetMainStageConfig(mainIndex);
            return (main != null && main.subStages != null) ? main.subStages.Count : 0;
        }

        public void SelectStage(int mainIndex, int subIndex)
        {
            int maxMain = mainStages != null && mainStages.Count > 0 ? mainStages.Count - 1 : 0;
            CurrentMainStageIndex = Mathf.Clamp(mainIndex, 0, maxMain);
            int subCount = GetSubStageCount(CurrentMainStageIndex);
            CurrentSubStageIndex = subCount > 0 ? Mathf.Clamp(subIndex, 0, subCount - 1) : Mathf.Max(0, subIndex);
        }

        public bool IsMainStageUnlocked(int mainIndex)
        {
            if (mainIndex <= 0) return true;
            return mainIndex <= _saveData.highestUnlockedMainStage;
        }

        public bool IsSubStageUnlocked(int mainIndex, int subIndex)
        {
            if (mainIndex < 0 || subIndex < 0) return false;
            if (mainIndex < _saveData.highestUnlockedMainStage) return true;
            if (mainIndex > _saveData.highestUnlockedMainStage) return false;
            return subIndex <= _saveData.highestUnlockedSubStage;
        }

        public int GetSubStageStars(int mainIndex, int subIndex)
        {
            for (int i = 0; i < _saveData.subStageStars.Count; i++)
            {
                var rec = _saveData.subStageStars[i];
                if (rec.mainStageIndex == mainIndex && rec.subStageIndex == subIndex)
                    return rec.bestStars;
            }
            return 0;
        }

        private void SetBestSubStageStars(int mainIndex, int subIndex, int stars)
        {
            int clamped = Mathf.Clamp(stars, 0, 5);
            for (int i = 0; i < _saveData.subStageStars.Count; i++)
            {
                var rec = _saveData.subStageStars[i];
                if (rec.mainStageIndex == mainIndex && rec.subStageIndex == subIndex)
                {
                    rec.bestStars = Mathf.Max(rec.bestStars, clamped);
                    return;
                }
            }

            _saveData.subStageStars.Add(new SubStageStarRecord
            {
                mainStageIndex = mainIndex,
                subStageIndex = subIndex,
                bestStars = clamped
            });
        }

        public void RecordSubStageClear(int mainIndex, int subIndex, int starsEarned)
        {
            if (mainIndex < 0 || subIndex < 0) return;

            bool isFirstClearOfStage1 = (mainIndex == 0 && subIndex == 0 && !_saveData.hasClearedFirstStage);
            if (mainIndex == 0 && subIndex == 0)
                _saveData.hasClearedFirstStage = true;

            SetBestSubStageStars(mainIndex, subIndex, starsEarned);

            int subCount = Mathf.Max(1, GetSubStageCount(mainIndex));
            bool clearedLastSubInMain = (subIndex >= subCount - 1);
            bool hasNextMainStage = mainStages != null && (mainIndex + 1 < mainStages.Count);

            if (clearedLastSubInMain && hasNextMainStage)
            {
                int nextMain = mainIndex + 1;
                if (nextMain > _saveData.highestUnlockedMainStage)
                {
                    _saveData.highestUnlockedMainStage = nextMain;
                    _saveData.highestUnlockedSubStage = 0;
                }

                PendingWorldTransitionFromMain = mainIndex;
                PendingWorldTransitionToMain = nextMain;
                PendingLobbySubStageIndex = 0;
                CurrentMainStageIndex = nextMain;
                CurrentSubStageIndex = 0;
            }
            else
            {
                int nextSub = Mathf.Min(subIndex + 1, subCount - 1);
                if (mainIndex == _saveData.highestUnlockedMainStage && nextSub > _saveData.highestUnlockedSubStage)
                {
                    _saveData.highestUnlockedSubStage = nextSub;
                }

                PendingLobbySubStageIndex = nextSub;
            }

            if (isFirstClearOfStage1 && !IsTutorialCompleted("lobby_gacha_equip"))
                PendingLobbyTutorial = true;

            AddQuestProgress(QuestType.ClearStage, 1);
            SavePlayerData();
            OnStageProgressChanged?.Invoke();
        }

        public bool ConsumePendingWorldTransition(out int fromMain, out int toMain)
        {
            if (PendingWorldTransitionFromMain >= 0 && PendingWorldTransitionToMain >= 0)
            {
                fromMain = PendingWorldTransitionFromMain;
                toMain = PendingWorldTransitionToMain;
                PendingWorldTransitionFromMain = -1;
                PendingWorldTransitionToMain = -1;
                return true;
            }

            fromMain = -1;
            toMain = -1;
            return false;
        }

        public int ConsumePendingLobbySubStageIndex()
        {
            int idx = PendingLobbySubStageIndex;
            PendingLobbySubStageIndex = -1;
            return idx;
        }

        public bool ConsumePendingLobbyTutorial()
        {
            bool flag = PendingLobbyTutorial;
            PendingLobbyTutorial = false;
            return flag;
        }
        #endregion

        #region Currency (Coins)
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _saveData.coins += amount;
            SavePlayerData();
            OnCoinsChanged?.Invoke(_saveData.coins);
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0) return false;
            if (_saveData.coins < amount) return false;

            _saveData.coins -= amount;
            SavePlayerData();
            OnCoinsChanged?.Invoke(_saveData.coins);
            return true;
        }
        #endregion

        #region Unit Ownership, Gacha, Upgrade & Equip
        public UnitCatalogEntry GetUnitCatalogEntry(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            for (int i = 0; i < unitCatalog.Count; i++)
            {
                if (unitCatalog[i] != null && unitCatalog[i].unitId == unitId)
                    return unitCatalog[i];
            }
            return null;
        }

        public OwnedUnitRecord GetOwnedUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            for (int i = 0; i < _saveData.ownedUnits.Count; i++)
            {
                if (_saveData.ownedUnits[i].unitId == unitId)
                    return _saveData.ownedUnits[i];
            }
            return null;
        }

        public IReadOnlyList<OwnedUnitRecord> GetAllOwnedUnits() => _saveData.ownedUnits;
        public IReadOnlyList<string> GetEquippedUnitIds() => _saveData.equippedUnitIds;

        public bool IsUnitEquipped(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            return _saveData.equippedUnitIds.Contains(unitId);
        }

        public int GetEquippedSlotIndex(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return -1;
            return _saveData.equippedUnitIds.IndexOf(unitId);
        }

        public int FindFirstEmptyEquipSlot()
        {
            for (int i = 0; i < MaxEquipSlots; i++)
            {
                if (string.IsNullOrEmpty(_saveData.equippedUnitIds[i]))
                    return i;
            }
            return -1;
        }

        public bool EquipUnit(int slotIndex, string unitId)
        {
            if (slotIndex < 0 || slotIndex >= MaxEquipSlots) return false;
            if (GetOwnedUnit(unitId) == null) return false;

            int existingSlot = GetEquippedSlotIndex(unitId);
            if (existingSlot == slotIndex) return true;

            if (existingSlot >= 0)
            {
                string prevInTarget = _saveData.equippedUnitIds[slotIndex];
                _saveData.equippedUnitIds[existingSlot] = prevInTarget;
            }

            _saveData.equippedUnitIds[slotIndex] = unitId;
            SavePlayerData();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool UnequipSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEquipSlots) return false;
            if (string.IsNullOrEmpty(_saveData.equippedUnitIds[slotIndex])) return false;

            int equippedCount = 0;
            for (int i = 0; i < MaxEquipSlots; i++)
            {
                if (!string.IsNullOrEmpty(_saveData.equippedUnitIds[i])) equippedCount++;
            }
            if (equippedCount <= 1) return false;

            _saveData.equippedUnitIds[slotIndex] = string.Empty;
            SavePlayerData();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public GachaResult DrawUnitGacha(string guaranteedUnitId = null, bool freeForTutorial = false)
        {
            var result = new GachaResult { success = false };
            if (unitCatalog == null || unitCatalog.Count == 0) return result;

            if (!freeForTutorial && !TrySpendCoins(gachaCostCoins))
                return result;

            UnitCatalogEntry picked = null;
            if (!string.IsNullOrEmpty(guaranteedUnitId))
                picked = GetUnitCatalogEntry(guaranteedUnitId);

            if (picked == null)
                picked = PickRandomUnitWeighted();

            if (picked == null) return result;

            OwnedUnitRecord record = GetOwnedUnit(picked.unitId);
            bool isNew = false;

            if (record == null)
            {
                isNew = true;
                record = new OwnedUnitRecord
                {
                    unitId = picked.unitId,
                    level = 1,
                    stackCount = 0
                };
                _saveData.ownedUnits.Add(record);
            }
            else
            {
                record.stackCount++;
            }

            AddQuestProgress(QuestType.DrawGacha, 1);
            SavePlayerData();
            OnInventoryChanged?.Invoke();

            result.success = true;
            result.unit = picked;
            result.isNewUnit = isNew;
            result.currentStack = record.stackCount;
            result.currentLevel = record.level;
            result.requiredStackForUpgrade = picked.GetRequiredStacksForNextLevel(record.level);
            return result;
        }

        private UnitCatalogEntry PickRandomUnitWeighted()
        {
            float totalWeight = 0f;
            for (int i = 0; i < unitCatalog.Count; i++)
            {
                if (unitCatalog[i] != null) totalWeight += Mathf.Max(0.01f, unitCatalog[i].gachaWeight);
            }

            if (totalWeight <= 0f) return unitCatalog[0];

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float sum = 0f;
            for (int i = 0; i < unitCatalog.Count; i++)
            {
                if (unitCatalog[i] == null) continue;
                sum += Mathf.Max(0.01f, unitCatalog[i].gachaWeight);
                if (roll <= sum) return unitCatalog[i];
            }

            return unitCatalog[unitCatalog.Count - 1];
        }

        public bool CanUpgradeUnit(string unitId)
        {
            UnitCatalogEntry entry = GetUnitCatalogEntry(unitId);
            OwnedUnitRecord record = GetOwnedUnit(unitId);
            if (entry == null || record == null) return false;
            if (record.level >= entry.maxLevel) return false;

            int reqStacks = entry.GetRequiredStacksForNextLevel(record.level);
            int coinCost = entry.GetUpgradeCoinCost(record.level);
            return record.stackCount >= reqStacks && _saveData.coins >= coinCost;
        }

        public bool TryUpgradeUnit(string unitId)
        {
            if (!CanUpgradeUnit(unitId)) return false;

            UnitCatalogEntry entry = GetUnitCatalogEntry(unitId);
            OwnedUnitRecord record = GetOwnedUnit(unitId);

            int reqStacks = entry.GetRequiredStacksForNextLevel(record.level);
            int coinCost = entry.GetUpgradeCoinCost(record.level);

            if (!TrySpendCoins(coinCost)) return false;

            record.stackCount -= reqStacks;
            record.level++;
            AddQuestProgress(QuestType.UpgradeUnit, 1);
            SavePlayerData();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public MatchUnitData[] BuildPlaySceneUnitDatas()
        {
            if (unitCatalog == null || unitCatalog.Count == 0) return null;

            MatchUnitData[] result = new MatchUnitData[MaxEquipSlots];
            HashSet<string> usedIds = new HashSet<string>();

            for (int i = 0; i < MaxEquipSlots; i++)
            {
                string equippedId = _saveData.equippedUnitIds[i];
                if (!string.IsNullOrEmpty(equippedId))
                {
                    UnitCatalogEntry entry = GetUnitCatalogEntry(equippedId);
                    OwnedUnitRecord owned = GetOwnedUnit(equippedId);
                    if (entry != null && owned != null)
                    {
                        result[i] = entry.ToMatchUnitData(isUnlocked: true, unitLevel: owned.level);
                        usedIds.Add(equippedId);
                    }
                }
            }

            int catalogCursor = 0;
            for (int i = 0; i < MaxEquipSlots; i++)
            {
                if (result[i] != null) continue;

                UnitCatalogEntry lockedEntry = null;
                while (catalogCursor < unitCatalog.Count)
                {
                    var candidate = unitCatalog[catalogCursor++];
                    if (candidate != null && !usedIds.Contains(candidate.unitId))
                    {
                        lockedEntry = candidate;
                        usedIds.Add(candidate.unitId);
                        break;
                    }
                }

                if (lockedEntry != null)
                    result[i] = lockedEntry.ToMatchUnitData(isUnlocked: false, unitLevel: 1);
            }

            return result;
        }
        #endregion

        #region Tutorial Progress
        public bool IsTutorialCompleted(string tutorialId)
        {
            if (string.IsNullOrEmpty(tutorialId)) return false;
            return _saveData.completedTutorials.Contains(tutorialId);
        }

        public void CompleteTutorial(string tutorialId)
        {
            if (string.IsNullOrEmpty(tutorialId)) return;
            if (!_saveData.completedTutorials.Contains(tutorialId))
            {
                _saveData.completedTutorials.Add(tutorialId);
                SavePlayerData();
            }
        }
        #endregion

        #region Settings (Shared between Lobby & Play)
        public void SetBgmVolume(float value)
        {
            _saveData.bgmVolume = Mathf.Clamp01(value);
            SavePlayerData();
            SyncAudioSettings();
            OnBgmVolumeChanged?.Invoke(_saveData.bgmVolume);
        }

        public void SetSfxVolume(float value)
        {
            _saveData.sfxVolume = Mathf.Clamp01(value);
            SavePlayerData();
            SyncAudioSettings();
            OnSfxVolumeChanged?.Invoke(_saveData.sfxVolume);
        }

        public void SetVibrationEnabled(bool enabled)
        {
            _saveData.isVibrationEnabled = enabled;
            SavePlayerData();
            OnVibrationChanged?.Invoke(_saveData.isVibrationEnabled);
        }

        private void SyncAudioSettings()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetMasterBgmVolume(_saveData.bgmVolume);
                SoundManager.Instance.SetMasterSfxVolume(_saveData.sfxVolume);
            }
        }
        #endregion
    }
}
