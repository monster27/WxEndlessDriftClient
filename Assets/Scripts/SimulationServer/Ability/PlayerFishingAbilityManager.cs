// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家钓鱼能力管理器
/// 负责管理玩家的钓鱼能力属性和计算
/// </summary>
public class PlayerFishingAbilityManager
{
    /// <summary>基础属性</summary>
    public FishingBaseStats BaseStats { get; private set; }
    
    /// <summary>计算后的属性（包含所有加成）</summary>
    public FishingCalculatedStats CalculatedStats { get; private set; }

    private List<PlayerFishingComponent> components;  // 装备组件列表

    public PlayerFishingAbilityManager()
    {
        BaseStats = new FishingBaseStats();
        CalculatedStats = new FishingCalculatedStats();
        components = new List<PlayerFishingComponent>();
        Debug.Log("[PlayerFishingAbilityManager] 玩家钓鱼能力管理器初始化完成");
    }

    /// <summary>
    /// 初始化能力管理器
    /// </summary>
    public void Initialize()
    {
        LoadDefaultStats();
        Debug.Log("[PlayerFishingAbilityManager] 能力管理器初始化完成");
    }

    /// <summary>
    /// 加载默认属性
    /// </summary>
    private void LoadDefaultStats()
    {
        // 设置默认稀有度权重
        BaseStats.rarityWeights = new Dictionary<int, int>
        {
            { 1, 200 },
            { 2, 150 },
            { 3, 120 },
            { 4, 90 },
            { 5, 60 }
        };

        // 设置默认计算属性
        CalculatedStats.trashProbability = 0.15f;
        CalculatedStats.maxTrashStreak = 0;
        CalculatedStats.fishWeightMultiplier = 1f;
        CalculatedStats.shinyRateBonus = 0f;
        CalculatedStats.rarityBonus = new Dictionary<int, int>();
        CalculatedStats.struggleTimeMultiplier = 1f;
        CalculatedStats.maxStruggleTime = 10f;
    }

    /// <summary>
    /// 装备组件
    /// </summary>
    /// <param name="component">要装备的组件</param>
    public void EquipComponent(PlayerFishingComponent component)
    {
        if (!components.Contains(component))
        {
            components.Add(component);
            RecalculateStats();
            Debug.Log($"[PlayerFishingAbilityManager] 装备组件: {component.ComponentId}");
        }
    }

    /// <summary>
    /// 卸下组件
    /// </summary>
    /// <param name="component">要卸下的组件</param>
    public void UnequipComponent(PlayerFishingComponent component)
    {
        if (components.Contains(component))
        {
            components.Remove(component);
            RecalculateStats();
            Debug.Log($"[PlayerFishingAbilityManager] 卸下组件: {component.ComponentId}");
        }
    }

    /// <summary>
    /// 重新计算所有属性
    /// </summary>
    public void RecalculateStats()
    {
        // 重置计算属性为默认值
        CalculatedStats.trashProbability = 0.15f;
        CalculatedStats.maxTrashStreak = 0;
        CalculatedStats.fishWeightMultiplier = 1f;
        CalculatedStats.shinyRateBonus = 0f;
        CalculatedStats.rarityBonus.Clear();
        CalculatedStats.struggleTimeMultiplier = 1f;
        CalculatedStats.maxStruggleTime = 10f;

        // 应用每个装备组件的效果
        foreach (var component in components)
        {
            ApplyComponentEffects(component);
        }

        Debug.Log("[PlayerFishingAbilityManager] 属性重新计算完成");
    }

    /// <summary>
    /// 应用组件效果
    /// </summary>
    /// <param name="component">组件</param>
    private void ApplyComponentEffects(PlayerFishingComponent component)
    {
        if (component == null || component.Abilities == null)
            return;

        foreach (var ability in component.Abilities)
        {
            ability.ApplyEffect(this);
        }
    }

    /// <summary>
    /// 获取所有装备组件
    /// </summary>
    /// <returns>装备组件列表</returns>
    public List<PlayerFishingComponent> GetEquippedComponents()
    {
        return new List<PlayerFishingComponent>(components);
    }

    /// <summary>
    /// 清空所有装备组件
    /// </summary>
    public void ClearAllComponents()
    {
        components.Clear();
        RecalculateStats();
        Debug.Log("[PlayerFishingAbilityManager] 所有组件已卸下");
    }
}

/// <summary>
/// 钓鱼基础属性
/// </summary>
public class FishingBaseStats
{
    public Dictionary<int, int> rarityWeights = new Dictionary<int, int>(); // 稀有度权重
}

/// <summary>
/// 钓鱼计算属性（包含所有加成后的最终值）
/// </summary>
public class FishingCalculatedStats
{
    public float trashProbability = 0.15f;      // 垃圾概率
    public int maxTrashStreak = 0;              // 最大连续垃圾次数（保底）
    public float fishWeightMultiplier = 1f;     // 鱼类权重倍率
    public float shinyRateBonus = 0f;           // 闪光几率加成
    public Dictionary<int, int> rarityBonus = new Dictionary<int, int>(); // 稀有度加成
    public float struggleTimeMultiplier = 1f;   // 挣扎时间倍率
    public float maxStruggleTime = 10f;         // 最大挣扎时间
}
*/
