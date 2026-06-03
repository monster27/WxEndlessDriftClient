using UnityEngine;
using System;
using System.Collections.Generic;

namespace JsonData
{
    /// <summary>
    /// 人物升级配置（运行时使用，支持服务器/客户端传输）
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

            System.Text.StringBuilder debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("[CharacterLevelUpConfig] 初始化运行时数据:");

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
                    debugInfo.AppendLine($"  等级区间 {kvp.Key}: 每级 {expPerLevel} 经验");
                }
            }

            debugInfo.AppendLine($"  总计: levelExpForLevel.Count={levelExpForLevel.Count}");
            Debug.Log(debugInfo.ToString());

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
            return 100;
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
        /// 从JSON字符串解析
        /// </summary>
        public static CharacterLevelUpConfig ParseFromJson(string jsonString)
        {
            CharacterLevelUpConfig config = new CharacterLevelUpConfig();
            System.Text.StringBuilder debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("[CharacterLevelUpConfig] ParseFromJson 解析配置:");

            try
            {
                JsonWrapper wrapper = JsonUtility.FromJson<JsonWrapper>(jsonString);
                if (wrapper != null)
                {
                    debugInfo.AppendLine($"  levelRangeExpList.Count={wrapper.levelRangeExpList.Count}");
                    foreach (var item in wrapper.levelRangeExpList)
                    {
                        debugInfo.AppendLine($"    区间 {item.rangeKey}: {item.expRequired} 经验");
                        config.levelUpExpRequirements[item.rangeKey] = item.expRequired;
                    }
                }
                Debug.Log(debugInfo.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterLevelUpConfig] ParseFromJson failed: {e.Message}");
            }

            return config;
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
}