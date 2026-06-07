using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class CharacterConfig
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public string iconPath;
        public int maxLevel = 100;
        public int skillIdAtLevel50;
        public int skillIdAtLevel100;
        public int tenLevelGoldReward;
        
        public int idleColumns = 15;
        public float idleSpeed = 15.0f;
        public int reelColumns = 12;
        public float reelSpeed = 20.0f;
        public int lazyColumns = 15;
        public float lazySpeed = 18.0f;
    }

    [System.Serializable]
    public class CharacterConfigList
    {
        public List<CharacterConfig> characters = new List<CharacterConfig>();
    }

    [System.Serializable]
    public class PlayerCharacterData
    {
        public int equippedCharacterId;
        public bool isEquipped;
        public int currentLevel;
        public int currentExp;
        public List<int> unlockedSkills = new List<int>();
    }
}