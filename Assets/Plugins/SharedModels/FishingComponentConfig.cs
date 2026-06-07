using UnityEngine;
using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 钓鱼组件类型枚举
    /// 定义钓鱼系统中的装备/技能类别
    /// </summary>
    public enum FishingComponentCategory
    {
        None = 0,
        /// <summary>
        /// 钓竿 - 影响钓鱼范围、力度等
        /// </summary>
        Rod = 1,
        
        /// <summary>
        /// 钓线 - 影响灵敏度、拉力等
        /// </summary>
        Line = 2,
        
        /// <summary>
        /// 钓钩 - 影响上钩率、脱钩率等
        /// </summary>
        Hook = 3,
        
        /// <summary>
        /// 技能 - 特殊能力，如增加稀有度、减少垃圾等
        /// </summary>
        Skill = 4,
        
        /// <summary>
        /// 人物 - 角色技能
        /// </summary>
        Character = 5,
        
        /// <summary>
        /// 鱼饵
        /// </summary>
        Bait = 6
    }

    /// <summary>
    /// 钓鱼组件参数数据
    /// 存储单个参数的ID和对应的值
    /// </summary>
    [Serializable]
    public class FishingComponentParam
    {
        /// <summary>
        /// 参数ID
        /// </summary>
        public int paramId;
        
        /// <summary>
        /// 参数值
        /// </summary>
        public float value;
    }

    /// <summary>
    /// 钓鱼组件等级数据
    /// 定义单个等级的参数配置
    /// </summary>
    [Serializable]
    public class FishingComponentLevelData
    {
        /// <summary>
        /// 等级（从1开始）
        /// </summary>
        public int level;
        
        /// <summary>
        /// 参数列表（最多3个参数，每个参数包含ID和值）
        /// </summary>
        public List<FishingComponentParam> paramsList;
        
        /// <summary>
        /// 技能等级描述
        /// 该等级技能的详细介绍
        /// </summary>
        public string levelDescription;
        
        /// <summary>
        /// 升级效果描述
        /// 从低等级升级到当前等级时显示的效果文本
        /// 例如："增加0.3s冷却"、"增加钓到稀有的500权重"
        /// </summary>
        public string upgradeDescription;
        
        /// <summary>
        /// 升级所需金币数量
        /// 默认值为-1，表示未配置或无法升级
        /// </summary>
        public int upgradeCost = -1;
    }

    /// <summary>
    /// 钓鱼组件获取状态枚举
    /// </summary>
    public enum FishingComponentObtainStatus
    {
        /// <summary>
        /// 未获取
        /// </summary>
        Unobtained = 0,
        
        /// <summary>
        /// 已获取
        /// </summary>
        Obtained = 1
    }

    /// <summary>
    /// 钓鱼组件装备状态枚举
    /// </summary>
    public enum FishingComponentEquipStatus
    {
        /// <summary>
        /// 未装备
        /// </summary>
        Unequipped = 0,
        
        /// <summary>
        /// 装备中
        /// </summary>
        Equipped = 1
    }

    /// <summary>
    /// 钓鱼组件配置类
    /// 定义一个钓鱼组件的完整配置，包含所有等级数据和服务器属性
    /// </summary>
    [Serializable]
    public class FishingComponentConfig
    {
        /// <summary>
        /// 组件唯一ID
        /// </summary>
        public int id;
        
        /// <summary>
        /// 组件名称
        /// </summary>
        public string name = string.Empty;
        
        /// <summary>
        /// 组件描述
        /// </summary>
        public string description = string.Empty;
        
        /// <summary>
        /// 组件类别
        /// </summary>
        public FishingComponentCategory category;
        
        /// <summary>
        /// 组件图标路径
        /// </summary>
        public string iconPath;
        
        /// <summary>
        /// 最大等级
        /// </summary>
        public int maxLevel;
        
        /// <summary>
        /// 是否为被动技能（装备后自动生效）
        /// </summary>
        public bool isPassive;
        
        /// <summary>
        /// 主动技能冷却时间（秒），0表示无冷却
        /// </summary>
        public float cooldownTime;
        
        /// <summary>
        /// 主动技能持续时间（秒），0表示瞬发
        /// </summary>
        public float duration;
        
        /// <summary>
        /// 各等级数据（按等级顺序排列）
        /// </summary>
        public List<FishingComponentLevelData> levelDataList;
        
        /// <summary>
        /// 获取状态（已获取/未获取）
        /// </summary>
        public FishingComponentObtainStatus obtainStatus = FishingComponentObtainStatus.Unobtained;
        
        /// <summary>
        /// 装备状态（装备中/未装备）
        /// 仅当obtainStatus为Obtained时有效
        /// </summary>
        public FishingComponentEquipStatus equipStatus = FishingComponentEquipStatus.Unequipped;
        
        // ========== 服务器用属性 ==========
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
        
        /// <summary>
        /// 获取指定等级的数据
        /// </summary>
        /// <param name="level">等级</param>
        /// <returns>等级数据，如果不存在返回null</returns>
        public FishingComponentLevelData GetLevelData(int level)
        {
            if (levelDataList == null) return null;
            return levelDataList.Find(data => data.level == level);
        }
        
        /// <summary>
        /// 获取指定等级中指定参数ID的值
        /// </summary>
        /// <param name="level">等级</param>
        /// <param name="paramId">参数ID</param>
        /// <returns>参数值，如果不存在返回0</returns>
        public float GetParamValue(int level, int paramId)
        {
            var levelData = GetLevelData(level);
            if (levelData == null || levelData.paramsList == null) return 0f;
            
            var param = levelData.paramsList.Find(p => p.paramId == paramId);
            return param != null ? param.value : 0f;
        }
        
        /// <summary>
        /// 获取指定等级的第index个参数（从0开始）
        /// </summary>
        /// <param name="level">等级</param>
        /// <param name="index">参数索引（0、1、2）</param>
        /// <returns>参数数据，如果不存在返回null</returns>
        public FishingComponentParam GetParamByIndex(int level, int index)
        {
            var levelData = GetLevelData(level);
            if (levelData == null || levelData.paramsList == null || index >= levelData.paramsList.Count) return null;
            return levelData.paramsList[index];
        }
        
        /// <summary>
        /// 获取指定等级的参数数量
        /// </summary>
        /// <param name="level">等级</param>
        /// <returns>参数数量</returns>
        public int GetParamCount(int level)
        {
            var levelData = GetLevelData(level);
            return levelData != null && levelData.paramsList != null ? levelData.paramsList.Count : 0;
        }
    }

    /// <summary>
    /// 完整钓鱼技能配置
    /// 包含所有钓鱼组件的配置集合
    /// </summary>
    [Serializable]
    public class CompleteFishingSkillConfig
    {
        /// <summary>
        /// 配置版本号
        /// </summary>
        public string version;
        
        /// <summary>
        /// 所有钓鱼组件配置列表
        /// </summary>
        public List<FishingComponentConfig> items;
        
        /// <summary>
        /// 根据ID获取组件配置
        /// </summary>
        /// <param name="id">组件ID</param>
        /// <returns>组件配置，如果未找到返回null</returns>
        public FishingComponentConfig GetComponentById(int id)
        {
            if (items == null) return null;
            return items.Find(c => c.id == id);
        }
        
        /// <summary>
        /// 根据类别获取组件配置列表
        /// </summary>
        /// <param name="category">组件类别</param>
        /// <returns>该类别的所有组件配置</returns>
        public List<FishingComponentConfig> GetComponentsByCategory(FishingComponentCategory category)
        {
            if (items == null) return new List<FishingComponentConfig>();
            return items.FindAll(c => c.category == category);
        }
        
        /// <summary>
        /// 根据名称获取组件配置
        /// </summary>
        /// <param name="name">组件名称</param>
        /// <returns>组件配置，如果未找到返回null</returns>
        public FishingComponentConfig GetComponentByName(string name)
        {
            if (items == null) return null;
            return items.Find(c => c.name == name);
        }

        /// <summary>
        /// 获取所有组件的图标路径字典
        /// 如果JSON中iconPath为空，则根据ID规则生成路径
        /// 规则：3001-3099=Rod, 3101-3199=Line, 3201-3299=Hook, 3301-3399=Skill
        /// </summary>
        /// <returns>图标ID到路径的字典</returns>
        public Dictionary<int, string> GetAllIconPaths()
        {
            var iconPaths = new Dictionary<int, string>();
            if (items == null) return iconPaths;

            foreach (var item in items)
            {
                string iconPath;
                if (!string.IsNullOrEmpty(item.iconPath))
                {
                    iconPath = item.iconPath;
                }
                else
                {
                    iconPath = GenerateIconPath(item.id);
                }
                iconPaths[item.id] = iconPath;
            }
            return iconPaths;
        }

        /// <summary>
        /// 根据ID生成图标路径
        /// </summary>
        private string GenerateIconPath(int id)
        {
            if (id >= 3001 && id <= 3099)
                return $"UI/Icon/Equipment/Rod/{id}";
            if (id >= 3101 && id <= 3199)
                return $"UI/Icon/Equipment/Line/{id}";
            if (id >= 3201 && id <= 3299)
                return $"UI/Icon/Equipment/Hook/{id}";
            if (id >= 3301 && id <= 3399)
                return $"UI/Icon/Equipment/Skill/{id}";
            return $"UI/Icon/Equipment/Unknown/{id}";
        }

        /// <summary>
        /// 从Resources路径加载配置
        /// </summary>
        /// <param name="path">Resources路径（不含拓展名）</param>
        /// <returns>配置实例，失败返回null</returns>
        public static CompleteFishingSkillConfig LoadFromResources(string path)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogError($"[CompleteFishingSkillConfig] 加载失败: {path}");
                return null;
            }

            var config = JsonUtility.FromJson<CompleteFishingSkillConfig>(textAsset.text);
            if (config == null)
            {
                Debug.LogError($"[CompleteFishingSkillConfig] 解析失败: {path}");
                return null;
            }

            Debug.Log($"[CompleteFishingSkillConfig] 加载成功，路径: {path}");
            return config;
        }
    }

    /// <summary>
    /// 钓鱼组件配置列表包装器
    /// 用于JsonUtility序列化/反序列化组件配置列表
    /// </summary>
    [Serializable]
    public class FishingComponentListWrapper
    {
        public List<FishingComponentConfig> fishingComponents = new List<FishingComponentConfig>();
    }

    /// <summary>
    /// 钓鱼组件配置数组包装类
    /// </summary>
    [Serializable]
    public class FishingComponentConfigArray
    {
        public FishingComponentConfig[] items;
    }

    /// <summary>
    /// 玩家装备信息
    /// </summary>
    [Serializable]
    public class PlayerEquipmentInfo
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
}