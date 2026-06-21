using System;

namespace SharedModels
{
    [Serializable]
    public class FishingResult
    {
        public bool Success;
        public int FishId;
        public string FishName = "";
        public float Weight;
        public int StarRatingId;
        public int GoldEarned;
        public int ExpEarned;
        public int GoldBalance;
        public int ExpBalance;
        public int Durability;
        public string Message = "";
        public bool IsTrash;
        public int TrashStreak;
        public float StruggleTime;
        public bool IsFishBagFull;
        public bool ShouldPause = true;
        public float PauseDuration = 0.5f;

        // 兼容字段（小写命名）
        public int detectedFishId;
        public int actualItemId;
        public bool isTrash;        // 兼容小写命名
        public float struggleTime;  // 兼容小写命名
        public bool isSuccess;
    }
}