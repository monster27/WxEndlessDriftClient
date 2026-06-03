// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;

/// <summary>
/// 钓鱼能力基类
/// 所有钓鱼能力都继承于此基类
/// </summary>
public abstract class FishingAbilityBase
{
    /// <summary>能力ID</summary>
    public int AbilityId { get; protected set; }
    
    /// <summary>能力名称</summary>
    public string Name { get; protected set; }
    
    /// <summary>能力描述</summary>
    public string Description { get; protected set; }
    
    /// <summary>能力等级</summary>
    public int Level { get; protected set; }
    
    /// <summary>能力类型</summary>
    public AbilityType Type { get; protected set; }
    
    /// <summary>能力效果值</summary>
    public float Value { get; protected set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="abilityId">能力ID</param>
    /// <param name="name">能力名称</param>
    /// <param name="description">能力描述</param>
    /// <param name="level">能力等级</param>
    /// <param name="type">能力类型</param>
    /// <param name="value">能力效果值</param>
    protected FishingAbilityBase(int abilityId, string name, string description, int level, AbilityType type, float value)
    {
        AbilityId = abilityId;
        Name = name;
        Description = description;
        Level = level;
        Type = type;
        Value = value;
    }

    /// <summary>
    /// 应用能力效果
    /// </summary>
    /// <param name="manager">玩家钓鱼能力管理器</param>
    public abstract void ApplyEffect(PlayerFishingAbilityManager manager);

    /// <summary>
    /// 移除能力效果
    /// </summary>
    /// <param name="manager">玩家钓鱼能力管理器</param>
    public abstract void RemoveEffect(PlayerFishingAbilityManager manager);

    /// <summary>
    /// 获取能力效果描述
    /// </summary>
    /// <returns>效果描述</returns>
    public virtual string GetEffectDescription()
    {
        return $"{Name} (Lv.{Level}): {Description}";
    }
}

/// <summary>
/// 能力类型枚举
/// </summary>
public enum AbilityType
{
    None = 0,
    TrashReduction,       // 减少垃圾概率
    RareFishIncrease,     // 增加稀有鱼概率
    ShinyIncrease,        // 增加闪光概率
    StruggleTimeReduction, // 减少挣扎时间
    CatchRateIncrease,    // 增加捕获成功率
    QualityIncrease,      // 增加品质
    FishWeightIncrease,   // 增加鱼类权重
    MaxTrashStreak,       // 设置最大连续垃圾次数
    Other = 99            // 其他类型
}

/// <summary>
/// 减少垃圾概率能力
/// </summary>
public class TrashReductionAbility : FishingAbilityBase
{
    public TrashReductionAbility(int abilityId, string name, string description, int level, float reductionAmount)
        : base(abilityId, name, description, level, AbilityType.TrashReduction, reductionAmount)
    {
    }

    public override void ApplyEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.trashProbability -= Value;
            manager.CalculatedStats.trashProbability = Mathf.Max(0.01f, manager.CalculatedStats.trashProbability);
        }
    }

    public override void RemoveEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.trashProbability += Value;
            manager.CalculatedStats.trashProbability = Mathf.Min(0.5f, manager.CalculatedStats.trashProbability);
        }
    }
}

/// <summary>
/// 增加稀有鱼概率能力
/// </summary>
public class RareFishIncreaseAbility : FishingAbilityBase
{
    public RareFishIncreaseAbility(int abilityId, string name, string description, int level, float increaseAmount)
        : base(abilityId, name, description, level, AbilityType.RareFishIncrease, increaseAmount)
    {
    }

    public override void ApplyEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            // 增加稀有度加成
            if (!manager.CalculatedStats.rarityBonus.ContainsKey(4))
            {
                manager.CalculatedStats.rarityBonus[4] = 0;
            }
            manager.CalculatedStats.rarityBonus[4] += (int)(Value * 10);
        }
    }

    public override void RemoveEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null && manager.CalculatedStats.rarityBonus.ContainsKey(4))
        {
            manager.CalculatedStats.rarityBonus[4] -= (int)(Value * 10);
            if (manager.CalculatedStats.rarityBonus[4] < 0)
            {
                manager.CalculatedStats.rarityBonus[4] = 0;
            }
        }
    }
}

/// <summary>
/// 增加闪光概率能力
/// </summary>
public class ShinyIncreaseAbility : FishingAbilityBase
{
    public ShinyIncreaseAbility(int abilityId, string name, string description, int level, float increaseAmount)
        : base(abilityId, name, description, level, AbilityType.ShinyIncrease, increaseAmount)
    {
    }

    public override void ApplyEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.shinyRateBonus += Value;
        }
    }

    public override void RemoveEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.shinyRateBonus -= Value;
            manager.CalculatedStats.shinyRateBonus = Mathf.Max(0f, manager.CalculatedStats.shinyRateBonus);
        }
    }
}

/// <summary>
/// 减少挣扎时间能力
/// </summary>
public class StruggleTimeReductionAbility : FishingAbilityBase
{
    public StruggleTimeReductionAbility(int abilityId, string name, string description, int level, float reductionMultiplier)
        : base(abilityId, name, description, level, AbilityType.StruggleTimeReduction, reductionMultiplier)
    {
    }

    public override void ApplyEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.struggleTimeMultiplier *= (1f - Value);
        }
    }

    public override void RemoveEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null && Value != 1f)
        {
            manager.CalculatedStats.struggleTimeMultiplier /= (1f - Value);
        }
    }
}

/// <summary>
/// 设置最大连续垃圾次数能力（保底）
/// </summary>
public class MaxTrashStreakAbility : FishingAbilityBase
{
    public MaxTrashStreakAbility(int abilityId, string name, string description, int level, int maxStreak)
        : base(abilityId, name, description, level, AbilityType.MaxTrashStreak, maxStreak)
    {
    }

    public override void ApplyEffect(PlayerFishingAbilityManager manager)
    {
        if (manager != null)
        {
            manager.CalculatedStats.maxTrashStreak = Mathf.Max(manager.CalculatedStats.maxTrashStreak, (int)Value);
        }
    }

    public override void RemoveEffect(PlayerFishingAbilityManager manager)
    {
        // 移除效果较复杂，通常在重新计算时处理
        if (manager != null)
        {
            manager.RecalculateStats();
        }
    }
}
*/
