// ========================================================
// ServerModels.NetworkData 已弃用
// 请使用 SharedModels.NetworkData
// ========================================================
/*
using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class ContinuousModeStatus
    {
        public bool isInContinuousMode;
        public float remainingTime;
    }

    [System.Serializable]
    public class BaitCountResponse
    {
        public int baitCount;
    }

    [System.Serializable]
    public class GoldResponse
    {
        public int gold;
    }

    [System.Serializable]
    public class InventoryResponse
    {
        public List<KeyValuePair> items;
    }

    [System.Serializable]
    public class KeyValuePair
    {
        public int key;
        public int value;
        public float weight;
        public int starRatingId;
        public long caughtTimestamp;
        public bool isShiny;  // 是否闪光鱼
    }

    [System.Serializable]
    public class CapacityResponse
    {
        public int capacity;
    }

    [System.Serializable]
    public class HeartbeatResponse
    {
        public long serverTime;
        public long clientTime;
        public bool isConnected;
    }

    [System.Serializable]
    public class HeartbeatRequest
    {
        public long clientTime;
    }

    [System.Serializable]
    public class PlayerNetworkData
    {
        public int playerId;
        public string nickname;
        public int gold;
        public int level;
        public int experience;
        public int currentSceneId;
        public int maxFishBagCapacity;
        public int FishBagCapacity => maxFishBagCapacity;
    }

    [System.Serializable]
    public class FishingCatchResponse
    {
        public bool success;
        public int fishId;
        public string fishName;
        public float weight;
        public int goldEarned;
        public int expEarned;
        public int goldBalance;
        public int expBalance;
        public int durability;
        public string message;
        public bool isTrash;
        public int trashStreak;
        public float struggleTime;
        public bool isShiny;  // 是否闪光鱼
    }

    [System.Serializable]
    public class AutoFishingResponse
    {
        public bool success;
        public string message;
        public int catchCount;
        public int totalGold;
        public int totalExp;
    }

    [System.Serializable]
    public class FishingStatusResponse
    {
        public bool success;
        public int level;
        public int gold;
        public int diamonds;
        public int exp;
        public int durability;
        public int todayFishCount;
        public int comboCount;
        public bool isAutoFishing;
        public bool isPaused;
        public int trashStreak;
        public float continuousModeRemainingTime;
        public float nextFishingTime;
        public LastCatchInfo lastCatch;
        public long baitEndTime;
        
        public int currentWeatherId;
        public int timeSlotId;
        public int timeStatus;
    }

    [System.Serializable]
    public class LastCatchInfo
    {
        public int fishId;
        public string fishName;
        public float weight;
        public int goldEarned;
        public int expEarned;
        public bool isTrash;
        public float struggleTime;
        public bool isShiny;  // 是否闪光鱼
    }

    [System.Serializable]
    public class EquipmentResponse
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

    [System.Serializable]
    public class CharacterSyncResponse
    {
        public int characterId;
        public int level;
        public int exp;
        public bool isActive;
    }
}
*/