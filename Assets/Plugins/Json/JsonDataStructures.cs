using System.Collections.Generic;


#region 图鉴数据结构（客户端应用，服务器的另有）

/// <summary>
/// 图鉴奖励数据
/// </summary>
[System.Serializable]
public class Reward
{
    public int percent;        // 完成度百分比要求
    public int rewardId;       // 奖励物品ID
    public int rewardAmount;   // 奖励数量
}

/// <summary>
/// 图鉴页面数据
/// </summary>
[System.Serializable]
public class CollectionPage
{
    public int id;                      // 页面ID
    public string pageName;             // 页面名称
    public List<Reward> rewards;        // 奖励列表
    public List<int> entries;           // 条目ID列表（鱼类ID等）
}

/// <summary>
/// 图鉴分类数据
/// </summary>
[System.Serializable]
public class CollectionCategory
{
    public int id;                      // 分类ID
    public string name;                 // 分类名称
    public string icon;                 // 图标名称
    public List<CollectionPage> pages;  // 页面列表
}

/// <summary>
/// 图鉴根数据
/// </summary>
[System.Serializable]
public class CollectionRoot
{
    public List<CollectionCategory> categories;
}

/// <summary>
/// 图鉴数据包装器
/// </summary>
[System.Serializable]
public class CollectionWrapper
{
    public CollectionRoot collection;
}

/// <summary>
/// 鱼类数据包装器（用于JSON反序列化）
/// </summary>
[System.Serializable]
public class FishWrapper
{
    public List<FishData> fishes;
}

#endregion

#region 物品数据结构

/// <summary>
/// 物品数据结构
/// </summary>
[System.Serializable]
public class ItemData
{
    public int id;
    public string name;
    public string description;
    public int sellPrice;
    public int buyPrice;
    public int itemType;
    public int categoryId;
    public string iconPath;
    public List<int> collectionInfoPages; // 所属图鉴情报页面ID列表
    public bool isUnique = true; // 是否唯一（饵料/窝料/水产默认不唯一，其他默认唯一）
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

#region 基础框架数据

// 岛屿
[System.Serializable]
public class IslandData
{
    public int id;
    public string name;
}

[System.Serializable]
public class IslandListWrapper
{
    public List<IslandData> islands;
}

// 稀有度
[System.Serializable]
public class RarityData
{
    public int id;
    public string name;
    public string color;
    public string colorCode;
    public int weight;
    public int exp;
}

[System.Serializable]
public class RarityListWrapper
{
    public List<RarityData> rarities;
}

// 时段
[System.Serializable]
public class TimeSlotData
{
    public int id;
    public string name;
    public string description;
    public int durationMinutes;
}

[System.Serializable]
public class TimeSlotListWrapper
{
    public List<TimeSlotData> timeSlots;
}

// 天气
[System.Serializable]
public class WeatherData
{
    public int id;
    public string name;
    public string description;
    public int percentage;
    public int weight;
}

[System.Serializable]
public class WeatherListWrapper
{
    public List<WeatherData> weathers;
}

// 重量星级
[System.Serializable]
public class StarRatingData
{
    public int id;
    public string name;
    public string description;
    public float multiplier;
    public float weight;
    public string color;
    public int sortOrder;
}

[System.Serializable]
public class StarRatingListWrapper
{
    public List<StarRatingData> starRatings;
}

// 鱼类品种(浴缸中移动)
[System.Serializable]
public class FishSpeciesData
{
    public int id;
    public string name;
    public string description;
    public string type;  // 英文类型标识，如 "FullScreenSwim"
}

[System.Serializable]
public class FishSpeciesListWrapper
{
    public List<FishSpeciesData> fishSpecies;
}

/// <summary>
/// 鱼类品种枚举（对应fishSpecies.json中的type字段）
/// ID与枚举值对应，可用于直接转换
/// </summary>
public enum FishSpeciesType
{
    FullScreenSwim = 601,
    FullScreenStatic = 602,
    BottomSwim = 603,
    BottomStatic = 604
}

#endregion

#region 场景数据

/// <summary>
/// 可序列化的三维向量（使用float字段，兼容其他C#引擎）
/// </summary>
[System.Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3()
    {
        x = 0f;
        y = 0f;
        z = 0f;
    }

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    /// <summary>
    /// 从Unity Vector3转换
    /// </summary>
    public static SerializableVector3 FromUnityVector(float x, float y, float z)
    {
        return new SerializableVector3(x, y, z);
    }
}

/// <summary>
/// 场景元素变换数据
/// </summary>
[System.Serializable]
public class SceneElementTransformData
{
    public SerializableVector3 position;
    public SerializableVector3 scale;

    public SceneElementTransformData()
    {
        position = new SerializableVector3(0f, 0f, 0f);
        scale = new SerializableVector3(1f, 1f, 1f);
    }
}

/// <summary>
/// 场景元素数据
/// </summary>
[System.Serializable]
public class SceneElementData
{
    public string id;
    public string name;
    public SceneElementTransformData transform;

    public SceneElementData()
    {
        id = "";
        name = "";
        transform = new SceneElementTransformData();
    }
}

/// <summary>
/// 场景数据
/// </summary>
[System.Serializable]
public class SceneData
{
    public string sceneId;
    public string sceneName;
    public bool isFlipped;
    public List<SceneElementData> elements;

    public SceneData()
    {
        sceneId = "";
        sceneName = "";
        isFlipped = false;
        elements = new List<SceneElementData>();
    }
}

/// <summary>
/// 场景数据列表包装器
/// </summary>
[System.Serializable]
public class SceneDataWrapper
{
    public List<SceneData> scenes;

    public SceneDataWrapper()
    {
        scenes = new List<SceneData>();
    }
}

#endregion

#region 钓鱼能力系统

/// <summary>
/// 基础技能数据（ID范围：701-799）
/// </summary>
[System.Serializable]
public class AbilityData
{
    public int id;
    public string name;
    public string description;
    public string abilityType;
    public int targetRarityId = 0;
}

/// <summary>基础技能列表包装器</summary>
[System.Serializable]
public class AbilityListWrapper
{
    public List<AbilityData> abilities;
}

/// <summary>
/// 挂载技能数据（ID范围：801-899）
/// </summary>
[System.Serializable]
public class SkillData
{
    public int id;
    public string name;
    public string description;
    public List<int> abilityIds;
}

/// <summary>挂载技能列表包装器</summary>
[System.Serializable]
public class SkillListWrapper
{
    public List<SkillData> skills;
}

#endregion

#region 游戏数据

#region 背包物品数据

// 室外装饰皮肤
[System.Serializable]
public class OutdoorSkinData
{
    public int id;
    public string name = string.Empty;
    public string description = string.Empty;
    public int categoryId;
}

[System.Serializable]
public class OutdoorSkinListWrapper
{
    public List<OutdoorSkinData> decorations;
}

// 室内装饰皮肤
[System.Serializable]
public class IndoorSkinData
{
    public int id;
    public string name = string.Empty;
    public string description = string.Empty;
    public int categoryId;
}

[System.Serializable]
public class IndoorSkinListWrapper
{
    public List<IndoorSkinData> decorations;
}

// 鱼饵
[System.Serializable]
public class BaitData
{
    public int id;
    public string name;
    public string description;
    public int baseWeight;
    public int unlockScene;
}

[System.Serializable]
public class BaitListWrapper
{
    public BaitData[] baits;
}

// 窝料
[System.Serializable]
public class NestBaitData
{
    public int id;
    public string name;
    public string description;
    public int applicableScene;
}

[System.Serializable]
public class NestBaitConstants
{
    public int defaultBaitItemId;
    public float continuousModeAddTime;
    public float continuousModeMaxTime;
}

[System.Serializable]
public class NestBaitListWrapper
{
    public NestBaitConstants constants;
    public NestBaitData[] nestBaits;
}

#endregion

#region 鱼类数据

/// <summary>
/// 鱼类参数
/// </summary>
[System.Serializable]
public class FishData
{
    public int id;
    public string name;
    public string description;
    public int islandId;
    public int rarityId;
    public List<int> preferredIslandIds;
    public List<int> preferredTimeIds;
    public List<int> preferredBaitIds;
    public List<int> preferredWeatherIds;
    public int fishSpeciesId;
    public int struggleTime;
    public float flashProbability;
    public float baseWeight;
    public float maxWeight;  // 最大重量
    public int baseExp;
    public float scale = 1.0f;  // Scale 参数
}

/// <summary>
/// 鱼类列表包装器
/// </summary>
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
