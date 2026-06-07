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

        public int detectedFishId;
        public int actualItemId;
        public bool isSuccess;
    }
}