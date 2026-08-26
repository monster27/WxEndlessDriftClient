using System;
using System.Collections.Generic;

/// <summary>
/// 钓鱼组件类型枚举
/// </summary>
public enum FishingComponentCategory
{
    None = 0,
    Rod = 1,
    Line = 2,
    Hook = 3,
    Skill = 4,
    Character = 5,
    Bait = 6
}

/// <summary>
/// 钓鱼组件参数数据
/// </summary>
public class FishingComponentParam
{
    public int paramId;
    public float value;
}

/// <summary>
/// 钓鱼组件等级数据
/// </summary>
public class FishingComponentLevelData
{
    public int level;
    public List<FishingComponentParam> paramsList;
    public string levelDescription;
    public string upgradeDescription;
    public int upgradeCost = -1;
}

/// <summary>
/// 钓鱼组件获取状态枚举
/// </summary>
public enum FishingComponentObtainStatus
{
    Unobtained = 0,
    Obtained = 1
}

/// <summary>
/// 钓鱼组件装备状态枚举
/// </summary>
public enum FishingComponentEquipStatus
{
    Unequipped = 0,
    Equipped = 1
}

/// <summary>
/// 钓鱼组件配置类
/// </summary>
public class FishingComponentConfig
{
    public int id;
    public string name = string.Empty;
    public string description = string.Empty;
    public FishingComponentCategory category;
    public string iconPath;
    public int maxLevel;
    public bool isPassive;
    public float cooldownTime;
    public float duration;
    public List<FishingComponentLevelData> levelDataList;
    public FishingComponentObtainStatus obtainStatus = FishingComponentObtainStatus.Unobtained;
    public FishingComponentEquipStatus equipStatus = FishingComponentEquipStatus.Unequipped;

    // ===== 服务器用属性 =====
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

    // ===== 新增：重量倾向加成 =====
    public float weightBiasBonus = 0f;

    // ===== 新增：挣扎时间减少 =====
    public float struggleTimeReduction = 0f;

    // ===== 新增：咬钩率加成 =====
    public float catchRateBonus = 0f;

    // ===== 新增：保底加成值 =====
    public float trashProtectionBonus = 0f;
}

/// <summary>
/// 完整钓鱼技能配置
/// </summary>
public class CompleteFishingSkillConfig
{
    public string version;
    public List<FishingComponentConfig> items;

    public FishingComponentConfig GetComponentById(int id)
    {
        if (items == null) return null;
        return items.Find(c => c.id == id);
    }

    public List<FishingComponentConfig> GetComponentsByCategory(FishingComponentCategory category)
    {
        if (items == null) return new List<FishingComponentConfig>();
        return items.FindAll(c => c.category == category);
    }

    public FishingComponentConfig GetComponentByName(string name)
    {
        if (items == null) return null;
        return items.Find(c => c.name == name);
    }
}

/// <summary>
/// 钓鱼组件配置列表包装器
/// </summary>
public class FishingComponentListWrapper
{
    public List<FishingComponentConfig> fishingComponents = new List<FishingComponentConfig>();
}

/// <summary>
/// 钓鱼组件配置数组包装类
/// </summary>
public class FishingComponentConfigArray
{
    public FishingComponentConfig[] items;
}

/// <summary>
/// 玩家装备信息
/// </summary>
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
