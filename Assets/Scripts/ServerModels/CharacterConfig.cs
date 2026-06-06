using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class CharacterConfig
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
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