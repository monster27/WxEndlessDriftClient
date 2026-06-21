// ==================== JsonDataStructures.cs ====================
using System.Collections.Generic;

#region 物品数据结构

/// <summary>
/// 物品数据结构
/// </summary>
[System.Serializable]
public class ItemData
{
    public int id;             // 物品ID
    public string name;        // 物品名称
    public string description; // 物品描述
    public int sellPrice;      // 出售价格
    public int buyPrice;       // 购买价格
    public int itemType;       // 物品类型
    public int categoryId;     // 所属分类ID（参考物品分类框架）
    public string iconPath;    // 图标路径
}

/// <summary>
/// 物品列表包装器
/// </summary>
[System.Serializable]
public class ItemListWrapper
{
    public List<ItemData> items;
}

#endregion

#region 基础框架框架
// 岛屿
[System.Serializable] public class IslandData { public int id; public string name; }
[System.Serializable] public class IslandListWrapper { public List<IslandData> islands; }

// 稀有度
[System.Serializable] public class RarityData { public int id; public string name; public string color; public string colorCode; public int weight; public int exp; }
[System.Serializable] public class RarityListWrapper { public List<RarityData> rarities; }

// 时段
[System.Serializable] public class TimeSlotData { public int id; public string name; public string description; public int durationMinutes; public int weight; }
[System.Serializable] public class TimeSlotListWrapper { public List<TimeSlotData> timeSlots; }

// 天气
[System.Serializable] public class WeatherData { public int id; public string name; public string description; public int percentage; public int weight; }
[System.Serializable] public class WeatherListWrapper { public List<WeatherData> weathers; }

// 重量星级
[System.Serializable] public class StarRatingData { public int id; public string name; public string description; public float multiplier; public float weight; public string color; public int sortOrder; }
[System.Serializable] public class StarRatingListWrapper { public List<StarRatingData> starRatings; }

// 鱼类品种(浴缸中移动)
[System.Serializable] public class FishSpeciesData { public int id; public string name; public string description; public string movementType; public string positionType; }
[System.Serializable] public class FishSpeciesListWrapper { public List<FishSpeciesData> fishSpecies; }

// ==================== 钓鱼能力系统 ====================
/// <summary>
/// 基础技能数据（ID范围：701-799）
/// 单个独立的技能效果类型定义，仅表示技能种类，无具体数值
/// 具体数值由完整钓鱼技能的等级配置提供
/// </summary>
[System.Serializable]
public class AbilityData
{
    /// <summary>技能唯一ID（701-799为基础技能）</summary>
    public int id;
    /// <summary>技能名称</summary>
    public string name;
    /// <summary>技能描述</summary>
    public string description;
    /// <summary>
    /// 技能类型
    /// - RarityWeight: 稀有度权重加成（增加指定稀有度鱼类权重）
    /// - WeightBias: 重量倾向调整（调整重量随机偏向）
    /// - StruggleTime: 挣扎时间减少（减少鱼类挣扎时间）
    /// - CatchRate: 咬钩概率加成（增加鱼类咬钩概率，减少垃圾）
    /// - FishWeight: 鱼类权重加成（增加所有鱼类权重）
    /// - ShinyRate: 闪光率加成（增加闪光鱼概率）
    /// - MinigameDifficulty: 小游戏难度降低（降低钓鱼小游戏难度等级）
    /// - TrashProtection: 钓鱼保底（连续钓到垃圾次数上限）
    /// </summary>
    public string abilityType;
    /// <summary>目标稀有度ID（仅用于RarityWeight类型，0表示不指定）</summary>
    public int targetRarityId = 0;
}

/// <summary>基础技能列表包装器</summary>
[System.Serializable]
public class AbilityListWrapper { public List<AbilityData> abilities; }

/// <summary>
/// 挂载技能数据（ID范围：801-899）
/// 可以挂载多个基础技能，形成组合技能
/// 玩家装备挂载技能后，会获得其所挂载的所有基础技能效果
/// </summary>
[System.Serializable]
public class SkillData
{
    /// <summary>挂载技能唯一ID（801-899为挂载技能）</summary>
    public int id;
    /// <summary>挂载技能名称</summary>
    public string name;
    /// <summary>挂载技能描述</summary>
    public string description;
    /// <summary>挂载的基础技能ID列表（引用701开头的基础技能）</summary>
    public List<int> abilityIds;
}

/// <summary>挂载技能列表包装器</summary>
[System.Serializable]
public class SkillListWrapper { public List<SkillData> skills; }

#endregion

#region 游戏数据

#region 背包物品数据
// 鱼饵
[System.Serializable] public class BaitData { public int id; public string name; public string description; public int baseWeight; public int unlockScene; }
[System.Serializable] public class BaitListWrapper { public BaitData[] baits; }

// 鱼类参数
[System.Serializable]
public class FishData
{
    public int id;
    public string name;
    public string description;
    public int islandId;                    // 存在岛屿ID，0表示所有岛屿
    public int rarityId;                    // 稀有度ID
    public List<int> preferredIslandIds;    // 特属偏向岛屿ID列表
    public List<int> preferredTimeIds;      // 偏向时间ID列表
    public List<int> preferredBaitIds;      // 偏向鱼饵ID列表
    public List<int> preferredWeatherIds;   // 偏向天气ID列表
    public int fishSpeciesId;               // 鱼类品种ID
    public int struggleTime;                // 挣扎时间(秒)
    public float flashProbability;          // 闪光概率
    public float baseWeight;                // 基础重量(kg)
    public int baseExp;                     // 基础经验值
}

[System.Serializable]
public class FishListWrapper
{
    public List<FishData> fishes;
}

#endregion

#region 游戏内部框架数据
// 背包类别数据
[System.Serializable]
public class BagCategoryData
{
    public int id;
    public string folderName;
    public string categoryName;
    public int sortOrder;
}

[System.Serializable]
public class BagCategoryListWrapper
{
    public List<BagCategoryData> bagCategories;
}

// 物品分类框架数据
[System.Serializable]
public class SubCategoryData
{
    public int id;
    public string name;
    public string description;
    public int startId;
    public int endId;
}

[System.Serializable]
public class CategoryData
{
    public int id;
    public string name;
    public string code;
    public string description;
    public int startId;
    public int endId;
    public List<SubCategoryData> subCategories;
}

[System.Serializable]
public class ItemCategoryListWrapper
{
    public List<CategoryData> categories;
    public List<string> notes;
}

#endregion

#endregion