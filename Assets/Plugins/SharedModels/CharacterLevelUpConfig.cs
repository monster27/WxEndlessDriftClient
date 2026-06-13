using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 人物升级配置（运行时使用，支持服务器/客户端传输）
    /// 纯数据类，不含Unity相关依赖
    /// </summary>
    [Serializable]
    public class CharacterLevelUpConfig
    {
        /// <summary>
        /// 升级经验配置（等级区间 -> 所需经验）
        /// </summary>
        public Dictionary<string, int> levelUpExpRequirements = new Dictionary<string, int>();

        /// <summary>
        /// 整十级金币奖励配置（等级 -> 金币奖励）
        /// </summary>
        public Dictionary<int, int> tenLevelGoldRewards = new Dictionary<int, int>();

        /// <summary>
        /// 运行时使用的等级经验配置（等级 -> 升级所需经验）
        /// </summary>
        private Dictionary<int, int> levelExpForLevel = new Dictionary<int, int>();

        private bool isInitialized = false;

        /// <summary>
        /// 初始化运行时字典
        /// </summary>
        private void InitializeRuntimeData()
        {
            if (isInitialized) return;

            levelExpForLevel.Clear();

            foreach (var kvp in levelUpExpRequirements)
            {
                string[] parts = kvp.Key.Split('-');
                if (parts.Length == 2)
                {
                    int startLevel = int.Parse(parts[0]);
                    int endLevel = int.Parse(parts[1]);
                    int expPerLevel = kvp.Value;

                    for (int level = startLevel; level < endLevel; level++)
                    {
                        levelExpForLevel[level] = expPerLevel;
                    }
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// 获取指定等级升级所需经验
        /// </summary>
        public int GetExpForLevel(int level)
        {
            InitializeRuntimeData();

            if (levelExpForLevel.TryGetValue(level, out int exp))
            {
                return exp;
            }
            return 999999;
        }

        /// <summary>
        /// 获取等级区间的总经验值
        /// </summary>
        /// <param name="rangeKey">区间键，如 "1-10"</param>
        /// <returns>该区间的总经验值</returns>
        public int GetExpForLevelRange(string rangeKey)
        {
            if (levelUpExpRequirements.TryGetValue(rangeKey, out int exp))
            {
                return exp;
            }
            return 1000; // 默认值
        }

        /// <summary>
        /// 配置版本号（用于数据同步）
        /// </summary>
        public int configVersion = 1;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public string lastUpdateTime;

        /// <summary>
        /// 从传输数据对象恢复
        /// </summary>
        public void FromTransferData(LevelUpConfigData data)
        {
            if (data == null) return;

            configVersion = data.configVersion;
            lastUpdateTime = data.lastUpdateTime;

            levelUpExpRequirements.Clear();
            if (data.levelUpExpList != null)
            {
                foreach (var item in data.levelUpExpList)
                {
                    levelUpExpRequirements[item.rangeKey] = item.expRequired;
                }
            }

            isInitialized = false;
        }

        /// <summary>
        /// JSON解析用的包装类
        /// </summary>
        [Serializable]
        public class JsonWrapper
        {
            public List<LevelRangeJson> levelRangeExpList = new List<LevelRangeJson>();
        }

        [Serializable]
        public class LevelRangeJson
        {
            public string rangeKey;
            public string rangeName;
            public int expRequired;
        }

        /// <summary>
        /// 获取等级列表（用于遍历所有配置的等级）
        /// </summary>
        public List<int> GetAllLevels()
        {
            InitializeRuntimeData();
            return new List<int>(levelExpForLevel.Keys);
        }

        /// <summary>
        /// 计算从当前等级升到目标等级所需的总经验
        /// </summary>
        public int CalculateTotalExp(int fromLevel, int toLevel)
        {
            InitializeRuntimeData();
            
            int totalExp = 0;
            for (int level = fromLevel; level < toLevel; level++)
            {
                if (levelExpForLevel.TryGetValue(level, out int exp))
                {
                    totalExp += exp;
                }
            }
            return totalExp;
        }
    }

    /// <summary>
    /// 升级配置传输数据类
    /// </summary>
    [Serializable]
    public class LevelUpConfigData
    {
        public int configVersion;
        public string lastUpdateTime;
        public List<LevelRangeData> levelUpExpList;
    }

    /// <summary>
    /// 等级区间数据
    /// </summary>
    [Serializable]
    public class LevelRangeData
    {
        public string rangeKey;
        public int expRequired;
    }

    /// <summary>
    /// 旧版兼容用包装类
    /// </summary>
    [Serializable]
    public class LevelUpExpWrapper
    {
        public Dictionary<int, int> levelUpExpRequirements = new Dictionary<int, int>();
    }
}