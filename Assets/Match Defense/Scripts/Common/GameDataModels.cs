using System;
using System.Collections.Generic;
using UnityEngine;
using MatchDefense.Match;

namespace MatchDefense
{
    [Serializable]
    public class UnitCatalogEntry
    {
        [Header("Identity")]
        public string unitId = "bread";
        public string nameLocalizationKey = "unit_bread_name";
        public string descLocalizationKey = "unit_bread_desc";
        public bool unlockedByDefault = false;

        [Header("Visual & Prefab")]
        public GameObject unitPrefab;
        public Sprite thumbnailSprite;
        public ItemVisual itemVisual;
        public Color sliderColor = Color.yellow;

        [Header("Match & Spawn")]
        public float spawnWeight = 1f;
        public int maxCharge = 3;

        [Header("Gacha & Upgrade")]
        [Min(0.01f)] public float gachaWeight = 10f;
        public int maxLevel = 10;
        public int baseUpgradeStacks = 2;
        public int upgradeStackIncrement = 1;
        public int baseUpgradeCoinCost = 50;
        public int upgradeCoinIncrement = 50;

        [Header("Upgrade Stat Scaling")]
        public float bonusDamagePerLevel = 0.5f;
        [Range(0f, 0.1f)] public float cooldownReduceRatePerLevel = 0.04f;

        public int GetRequiredStacksForNextLevel(int currentLevel)
        {
            return Mathf.Max(1, baseUpgradeStacks + (currentLevel - 1) * upgradeStackIncrement);
        }

        public int GetUpgradeCoinCost(int currentLevel)
        {
            return Mathf.Max(0, baseUpgradeCoinCost + (currentLevel - 1) * upgradeCoinIncrement);
        }

        public MatchUnitData ToMatchUnitData(bool isUnlocked, int unitLevel)
        {
            return new MatchUnitData
            {
                unitId = unitId,
                unitPrefab = unitPrefab,
                thumbnailSprite = thumbnailSprite,
                itemVisual = itemVisual,
                spawnWeight = spawnWeight,
                maxCharge = maxCharge,
                sliderColor = sliderColor,
                isUnlocked = isUnlocked,
                unitLevel = Mathf.Max(1, unitLevel),
                bonusDamage = Mathf.Max(0, unitLevel - 1) * bonusDamagePerLevel,
                cooldownMultiplier = Mathf.Clamp(1f - Mathf.Max(0, unitLevel - 1) * cooldownReduceRatePerLevel, 0.4f, 1f)
            };
        }
    }

    [Serializable]
    public class SubStageConfig
    {
        [Header("Sub Stage Info")]
        public string subTitleLocalizationKey = "stage_1_sub_1";
        public string sceneName = "Stage_01";

        [Header("Gameplay & Rewards")]
        public int targetClearWave = 40;
        public int clearRewardCoins = 100;
        public int coinsPerRemainingTurn = 5;
    }

    [Serializable]
    public class MainStageConfig
    {
        [Header("Main Stage Info")]
        public string mainStageId = "world_1";
        public string titleLocalizationKey = "stage_1_title";

        [Header("Sub Stages")]
        public List<SubStageConfig> subStages = new List<SubStageConfig>();
    }

    [Serializable]
    public class SubStageStarRecord
    {
        public int mainStageIndex;
        public int subStageIndex;
        public int bestStars;
    }

    public enum QuestType
    {
        MatchCount,
        PlaceUnit,
        KillEnemy,
        DrawGacha,
        UpgradeUnit,
        ClearStage
    }

    [Serializable]
    public class QuestDefinition
    {
        public string questId = "quest_match_5";
        public string titleLocalizationKey = "quest_match_5_title";
        public string fallbackTitle = "Match 5 Times";
        public QuestType questType = QuestType.MatchCount;
        public int targetCount = 5;
        public int rewardCoins = 100;
    }

    [Serializable]
    public class QuestProgressRecord
    {
        public string questId;
        public int currentCount;
        public bool isClaimed;
    }

    [Serializable]
    public class OwnedUnitRecord
    {
        public string unitId;
        public int level = 1;
        public int stackCount = 0;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int coins = 0;
        public int jewels = 0;

        public int highestUnlockedMainStage = 0;
        public int highestUnlockedSubStage = 0;
        public bool hasClearedFirstStage = false;
        public List<SubStageStarRecord> subStageStars = new List<SubStageStarRecord>();

        public List<OwnedUnitRecord> ownedUnits = new List<OwnedUnitRecord>();
        public List<string> equippedUnitIds = new List<string>();
        public List<string> completedTutorials = new List<string>();
        public List<QuestProgressRecord> questProgress = new List<QuestProgressRecord>();

        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool isVibrationEnabled = true;
    }

    public struct GachaResult
    {
        public bool success;
        public UnitCatalogEntry unit;
        public bool isNewUnit;
        public int currentStack;
        public int requiredStackForUpgrade;
        public int currentLevel;
    }

    public struct StageRewardSummary
    {
        public int mainStageIndex;
        public int subStageIndex;
        public int reachedWave;
        public int targetWave;
        public bool isStageCleared;
        public int starsEarned;
        public int clearCoins;
        public int remainingTurns;
        public int turnBonusCoins;
        public int totalCoins;
    }
}
