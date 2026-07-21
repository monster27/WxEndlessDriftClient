using System.Collections.Generic;

namespace ServerModels
{
    public enum FishingComponentCategory
    {
        None = 0,
        Rod = 1,
        Line = 2,
        Hook = 3,
        Skill = 4,
        Character = 5,
        Bait = 6
    }

    [System.Serializable]
    public class FishingComponentLevel
    {
        public int level;
        public int upgradeCost;
        public string upgradeDescription;
        public string levelDescription;
    }

    [System.Serializable]
    public class FishingComponentConfig
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int rarityId;
        public int slotTypeId;
        public FishingComponentCategory category;
        public float trashProbability;
        public int maxTrashStreak;
        public float fishWeightMultiplier;
        public float shinyRateBonus;
        public int minFishingInterval;
        public int maxFishingInterval;
        public Dictionary<int, int> rarityBonus = new Dictionary<int, int>();
        public Dictionary<int, int> rarityWeights = new Dictionary<int, int>();
        public int continuousPauseDuration;
        public int normalPauseDuration;
        public int fishBagCapacity;
        public int maxLevel;
        public List<FishingComponentLevel> levels;
    }

    [System.Serializable]
    public class FishingComponentListWrapper
    {
        public List<FishingComponentConfig> fishingComponents = new List<FishingComponentConfig>();
    }

    [System.Serializable]
    public class FishingComponentConfigArray
    {
        public FishingComponentConfig[] items;
    }

    public enum FishingComponentObtainStatus
    {
        Unobtained = 0,
        Obtained = 1
    }

    public enum FishingComponentEquipStatus
    {
        Unequipped = 0,
        Equipped = 1
    }

    [System.Serializable]
    public class PlayerEquipmentInfo
    {
        public int rodId;
        public int rodLevel;
        public int lineId;
        public int lineLevel;
        public int hookId;
        public int hookLevel;
        public int skill1Id;
        public int skill1Level;
        public int skill2Id;
        public int skill2Level;
        public int characterId;
        public int characterLevel;
        public int baitId;
        public int baitLevel;
    }
}