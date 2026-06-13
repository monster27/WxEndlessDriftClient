using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 人物配置类
    /// 定义人物的基本属性、升级所需经验和技能奖励
    /// 纯数据类，不含Unity相关依赖
    /// </summary>
    [Serializable]
    public class CharacterConfig
    {
        /// <summary>
        /// 人物唯一ID
        /// </summary>
        public int id;
        
        /// <summary>
        /// 人物名称
        /// </summary>
        public string name = string.Empty;
        
        /// <summary>
        /// 人物描述
        /// </summary>
        public string description = string.Empty;
        
        /// <summary>
        /// 人物图标路径
        /// </summary>
        public string iconPath;
        
        /// <summary>
        /// 最大等级
        /// </summary>
        public int maxLevel = 100;
        
        /// <summary>
        /// 50级解锁的技能ID
        /// </summary>
        public int skillIdAtLevel50;
        
        /// <summary>
        /// 100级解锁的技能ID
        /// </summary>
        public int skillIdAtLevel100;
        
        /// <summary>
        /// 整十级奖励的金币数
        /// </summary>
        public int tenLevelGoldReward;
        
        /// <summary>
        /// 空闲动画列数
        /// </summary>
        public int idleColumns = 15;
        
        /// <summary>
        /// 空闲动画速度
        /// </summary>
        public float idleSpeed = 15.0f;
        
        /// <summary>
        /// 收杆动画列数
        /// </summary>
        public int reelColumns = 12;
        
        /// <summary>
        /// 收杆动画速度
        /// </summary>
        public float reelSpeed = 20.0f;
        
        /// <summary>
        /// 懒怠动画列数
        /// </summary>
        public int lazyColumns = 15;
        
        /// <summary>
        /// 懒怠动画速度
        /// </summary>
        public float lazySpeed = 18.0f;
        
        /// <summary>
        /// 空闲动画纹理路径
        /// </summary>
        public string idleTexturePath = string.Empty;
        
        /// <summary>
        /// 收杆动画纹理路径
        /// </summary>
        public string reelTexturePath = string.Empty;
        
        /// <summary>
        /// 懒怠动画纹理路径
        /// </summary>
        public string lazyTexturePath = string.Empty;
    }

    /// <summary>
    /// 人物配置列表包装器
    /// 纯数据类，Unity相关加载方法请参见BaseFramework/CharacterConfig.cs
    /// </summary>
    [Serializable]
    public class CharacterConfigList
    {
        public List<CharacterConfig> characters = new List<CharacterConfig>();

        /// <summary>
        /// 获取所有人物ID
        /// </summary>
        public List<int> GetAllCharacterIds()
        {
            var ids = new List<int>();
            if (characters == null) return ids;
            foreach (var c in characters)
            {
                ids.Add(c.id);
            }
            return ids;
        }

        /// <summary>
        /// 获取指定人物的技能ID
        /// </summary>
        public (int skillId50, int skillId100) GetCharacterSkillIds(int characterId)
        {
            if (characters == null) return (0, 0);
            var config = characters.Find(c => c.id == characterId);
            if (config == null) return (0, 0);
            return (config.skillIdAtLevel50, config.skillIdAtLevel100);
        }
    }

    /// <summary>
    /// 人物技能配置类
    /// 定义人物可解锁的技能配置
    /// </summary>
    [Serializable]
    public class CharacterSkillConfig
    {
        /// <summary>
        /// 配置ID
        /// </summary>
        public int id;
        
        /// <summary>
        /// 关联的人物ID
        /// </summary>
        public int characterId;
        
        /// <summary>
        /// 等级解锁的技能列表
        /// </summary>
        public List<LevelSkillUnlock> levelSkillUnlocks = new List<LevelSkillUnlock>();
    }

    /// <summary>
    /// 等级技能解锁配置
    /// </summary>
    [Serializable]
    public class LevelSkillUnlock
    {
        /// <summary>
        /// 解锁等级
        /// </summary>
        public int unlockLevel;
        
        /// <summary>
        /// 解锁的完整钓鱼技能ID
        /// </summary>
        public int skillId;
        
        /// <summary>
        /// 解锁描述
        /// </summary>
        public string description;
    }

    /// <summary>
    /// 人物技能配置列表包装器
    /// </summary>
    [Serializable]
    public class CharacterSkillConfigList
    {
        public List<CharacterSkillConfig> characterSkillConfigs;
    }

    /// <summary>
    /// 玩家人物数据类
    /// 存储玩家当前的人物状态
    /// </summary>
    [Serializable]
    public class PlayerCharacterData
    {
        /// <summary>
        /// 当前装备的人物ID
        /// </summary>
        public int equippedCharacterId = 0;
        
        /// <summary>
        /// 当前人物等级
        /// </summary>
        public int currentLevel = 1;
        
        /// <summary>
        /// 当前经验值
        /// </summary>
        public int currentExp = 0;
        
        /// <summary>
        /// 已解锁的技能ID列表
        /// </summary>
        public List<int> unlockedSkills = new List<int>();
        
        /// <summary>
        /// 是否已装备
        /// </summary>
        public bool isEquipped = false;
    }
}