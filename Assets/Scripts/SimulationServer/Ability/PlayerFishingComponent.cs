// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;

/// <summary>
/// 玩家钓鱼组件
/// 代表玩家装备的钓鱼装备或技能
/// </summary>
public class PlayerFishingComponent
{
    /// <summary>组件ID</summary>
    public int ComponentId { get; private set; }
    
    /// <summary>组件名称</summary>
    public string Name { get; private set; }
    
    /// <summary>组件类型</summary>
    public FishingComponentCategory Category { get; private set; }
    
    /// <summary>组件等级</summary>
    public int Level { get; private set; }
    
    /// <summary>组件稀有度</summary>
    public int Rarity { get; private set; }
    
    /// <summary>组件技能列表</summary>
    public List<FishingAbilityBase> Abilities { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="name">组件名称</param>
    /// <param name="category">组件类型</param>
    /// <param name="level">组件等级</param>
    /// <param name="rarity">组件稀有度</param>
    public PlayerFishingComponent(int componentId, string name, FishingComponentCategory category, int level, int rarity)
    {
        ComponentId = componentId;
        Name = name;
        Category = category;
        Level = level;
        Rarity = rarity;
        Abilities = new List<FishingAbilityBase>();
    }

    /// <summary>
    /// 添加技能
    /// </summary>
    /// <param name="ability">技能</param>
    public void AddAbility(FishingAbilityBase ability)
    {
        if (!Abilities.Contains(ability))
        {
            Abilities.Add(ability);
        }
    }

    /// <summary>
    /// 移除技能
    /// </summary>
    /// <param name="ability">技能</param>
    public void RemoveAbility(FishingAbilityBase ability)
    {
        Abilities.Remove(ability);
    }

    /// <summary>
    /// 获取技能数量
    /// </summary>
    /// <returns>技能数量</returns>
    public int GetAbilityCount()
    {
        return Abilities.Count;
    }

    /// <summary>
    /// 清空所有技能
    /// </summary>
    public void ClearAbilities()
    {
        Abilities.Clear();
    }
}

/// <summary>
/// 钓鱼组件类型枚举
/// </summary>
public enum FishingComponentCategory
{
    None = 0,
    Rod = 1,       // 鱼竿
    Line = 2,      // 鱼线
    Hook = 3,      // 鱼钩
    Skill = 4      // 技能
}
*/
