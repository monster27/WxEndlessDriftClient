using System.Collections.Generic;
using Newtonsoft.Json;

namespace ServerModels
{
    [System.Serializable]
    public class CharacterLevelUpConfig
    {
        public Dictionary<int, int> levelUpExpRequirements = new Dictionary<int, int>();

        public static CharacterLevelUpConfig ParseFromJson(string json)
        {
            var config = new CharacterLevelUpConfig();
            var wrapper = JsonConvert.DeserializeObject<LevelUpExpWrapper>(json);
            if (wrapper != null && wrapper.levelUpExpRequirements != null)
            {
                config.levelUpExpRequirements = wrapper.levelUpExpRequirements;
            }
            return config;
        }

        public int GetExpForLevel(int level)
        {
            if (levelUpExpRequirements.TryGetValue(level, out int exp))
                return exp;
            return 999999;
        }
    }

    [System.Serializable]
    public class LevelUpExpWrapper
    {
        public Dictionary<int, int> levelUpExpRequirements = new Dictionary<int, int>();
    }
}
