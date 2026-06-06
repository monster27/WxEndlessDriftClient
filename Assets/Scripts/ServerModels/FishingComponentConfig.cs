using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class FishingComponentConfig
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int rarityId;
        public int slotTypeId;
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
    }

    [System.Serializable]
    public class FishingComponentListWrapper
    {
        public List<FishingComponentConfig> fishingComponents = new List<FishingComponentConfig>();
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
}