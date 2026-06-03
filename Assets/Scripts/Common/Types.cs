// ========================================================
// 公共类型定义文件
// 这些类型被多个模块引用，保留在此以避免编译错误
// ========================================================

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备槽位类型枚举
/// </summary>
public enum EquipmentSlotType
{
    None = 0,
    Bait = 21,        // 鱼饵槽位（小分类ID 21）
    FishingRod = 31,  // 钓竿槽位（小分类ID 31）
    FishingLine = 32, // 钓线槽位（小分类ID 32）
    FishingHook = 33, // 钓钩槽位（小分类ID 33）
    Skill1 = 41,      // 技能槽位1
    Skill2 = 42,      // 技能槽位2
    Character = 34,   // 人物槽位（大分类ID 34）
    Decoration = 43   // 钓鱼场景装饰
}

/// <summary>
/// 商城物品数据结构
/// </summary>
[System.Serializable]
public class MallItemData
{
    public int id;             // 物品ID
    public string name;        // 物品名称
    public string description; // 物品描述
    public int price;          // 物品价格
    public int type;           // 物品类型
    public int count;          // 购买数量
    public int iconId;         // 图标ID
    public bool isHot;         // 是否热门
    public bool isNew;         // 是否新品
    public int stock;          // 库存数量
}

/// <summary>
/// 商城分类数据结构
/// </summary>
[System.Serializable]
public class MallCategoryData
{
    public int id;              // 分类ID
    public string name;         // 分类名称
    public string iconName;     // 图标名称
    public bool isDefault;      // 是否默认分类
}

/// <summary>
/// 商城配置数据结构
/// </summary>
[System.Serializable]
public class MallConfigData
{
    public int mallId;                  // 商城ID
    public string mallName;             // 商城名称
    public MallCategoryData[] categories; // 分类列表
    public MallItemData[] items;        // 物品列表
}

/// <summary>
/// 钓鱼结果数据结构
/// </summary>
public class FishingResult
{
    public int detectedFishId;   // 检测到的鱼ID
    public int actualItemId;     // 实际获得的物品ID
    public bool isTrash;         // 是否是垃圾
    public float struggleTime;   // 挣扎时间（秒）
}

/// <summary>
/// 自动钓鱼管理器（仅用于兼容旧代码引用）
/// </summary>
public class AutoFishingManager
{
    public void ResetNotificationState() { }
}

/// <summary>
/// 人物服务器管理器（仅用于兼容旧代码引用）
/// 实际的 CharacterConfig 类型定义在 Plugins/JsonData/BaseFramework/CharacterConfig.cs 中
/// </summary>
public class CharacterServerManager
{
    private static CharacterServerManager instance;
    public static CharacterServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new CharacterServerManager();
            }
            return instance;
        }
    }
    
    public void Initialize() { }
    public void EquipCharacter(int characterId) { }
    public List<CharacterConfig> GetAllCharacterConfigs() { return new List<CharacterConfig>(); }
}

/// <summary>
/// 空的SimulationServer类（仅用于兼容旧代码引用）
/// 实际功能已迁移到网络服务器
/// </summary>
public class SimulationServer
{
    public static SimulationServer Instance => null;
    
    public FishingResult CurrentFishingResult => null;
    public AutoFishingManager AutoFishingManager => null;
    
    public void Initialize() { }
    public void Update(float deltaTime) { }
    public bool IsRunning() => false;
    public void AddItem(int itemId, int quantity) { }
    public void RemoveItem(int itemId, int quantity) { }
    public void AddFish(int fishId, int quantity) { }
    public bool IsFishBagFull() => false;
    public void ProcessHeartbeat(Dictionary<string, object> data) { }
}
