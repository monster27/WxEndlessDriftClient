using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class FishingStats
    {
        public float trashProbability = 0.15f;
        public int maxTrashStreak = 0;
        public float fishWeightMultiplier = 1f;
        public float shinyRateBonus = 0f;
        public int minFishingInterval = 3;
        public int maxFishingInterval = 20;
        public float continuousPauseDuration = 1f;
        public float normalPauseDuration = 0.5f;
        public int fishBagCapacity = 20;
        public Dictionary<int, int> rarityBonus = new Dictionary<int, int>();
        public Dictionary<int, int> rarityWeights = new Dictionary<int, int>();
    }
}