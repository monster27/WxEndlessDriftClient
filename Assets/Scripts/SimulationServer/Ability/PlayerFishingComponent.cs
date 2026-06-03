using UnityEngine;

/// <summary>
/// 钓鱼组件参数ID常量定义
/// </summary>
public static class FishingParamIds
{
    public const int Param1 = 1001;
    public const int Param2 = 1002;
    public const int Param3 = 1003;
    public const int TargetRarityId = 1004;
}

/// <summary>
/// 玩家装备的钓鱼组件实例
/// 记录玩家实际装备的组件及其等级
/// 支持通过参数ID灵活配置效果
/// </summary>
public class PlayerFishingComponent : FishingAbilityBase
{
    /// <summary>
    /// 组件配置引用
    /// </summary>
    public FishingComponentConfig Config { get; private set; }
    
    /// <summary>
    /// 当前等级
    /// </summary>
    public int CurrentLevel { get; private set; }
    
    /// <summary>
    /// 组件类别
    /// </summary>
    public FishingComponentCategory Category => Config?.category ?? FishingComponentCategory.Skill;
    
    /// <summary>
    /// 当前等级数据
    /// </summary>
    public FishingComponentLevelData CurrentLevelData => Config?.GetLevelData(CurrentLevel);
    
    /// <summary>
    /// 当前等级参数1值（参数ID: 1001）
    /// </summary>
    public float CurrentParam1 => Config?.GetParamValue(CurrentLevel, 1001) ?? 0f;
    
    /// <summary>
    /// 当前等级参数2值（参数ID: 1002）
    /// </summary>
    public float CurrentParam2 => Config?.GetParamValue(CurrentLevel, 1002) ?? 0f;
    
    /// <summary>
    /// 当前等级参数3值（参数ID: 1003）
    /// </summary>
    public float CurrentParam3 => Config?.GetParamValue(CurrentLevel, 1003) ?? 0f;
    
    /// <summary>
    /// 主动技能剩余冷却时间
    /// </summary>
    public float RemainingCooldown { get; private set; }
    
    /// <summary>
    /// 是否处于冷却中
    /// </summary>
    public bool IsOnCooldown => RemainingCooldown > 0f;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">组件配置</param>
    /// <param name="currentLevel">当前等级</param>
    public PlayerFishingComponent(FishingComponentConfig config, int currentLevel = 1) : 
        base(config.id, config.name, config.GetParamValue(currentLevel, 1001), config.duration)
    {
        Config = config;
        CurrentLevel = Mathf.Clamp(currentLevel, 1, config.maxLevel);
        Description = config.description;
        RemainingCooldown = 0f;
    }
    
    /// <summary>
    /// 升级组件
    /// </summary>
    /// <param name="newLevel">新等级</param>
    /// <returns>是否升级成功</returns>
    public bool Upgrade(int newLevel)
    {
        if (newLevel <= CurrentLevel || newLevel > Config.maxLevel)
        {
            return false;
        }
        
        CurrentLevel = newLevel;
        Value = Config.GetParamValue(CurrentLevel, 1001);
        
        Debug.Log($"[组件{Name}] 升级到 {CurrentLevel} 级，参数1: {Value}");
        return true;
    }
    
    /// <summary>
    /// 设置等级（直接设置，不检查前置条件）
    /// </summary>
    /// <param name="level">目标等级</param>
    public void SetLevel(int level)
    {
        CurrentLevel = Mathf.Clamp(level, 1, Config.maxLevel);
        Value = Config.GetParamValue(CurrentLevel, 1001);
    }
    
    /// <summary>
    /// 检查是否可以升级到指定等级
    /// </summary>
    /// <param name="targetLevel">目标等级</param>
    /// <returns>是否可以升级</returns>
    public bool CanUpgradeTo(int targetLevel)
    {
        return targetLevel > CurrentLevel && targetLevel <= Config.maxLevel;
    }
    
    /// <summary>
    /// 激活主动技能
    /// </summary>
    /// <param name="duration">持续时间（默认使用配置值）</param>
    /// <returns>是否激活成功</returns>
    public new bool Activate(float duration = 0f)
    {
        if (IsOnCooldown)
        {
            Debug.LogWarning($"[组件{Name}] 正在冷却中，剩余时间: {RemainingCooldown:F1}秒");
            return false;
        }
        
        if (!Config.isPassive)
        {
            base.Activate(duration);
            RemainingCooldown = Config.cooldownTime;
            Debug.Log($"[组件{Name}] 激活技能，持续时间: {RemainingDuration:F1}秒，冷却时间: {Config.cooldownTime:F1}秒");
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 更新组件状态
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public override void Update(float deltaTime)
    {
        // 更新技能持续时间
        base.Update(deltaTime);
        
        // 更新冷却时间
        if (RemainingCooldown > 0f)
        {
            RemainingCooldown -= deltaTime;
            if (RemainingCooldown < 0f)
            {
                RemainingCooldown = 0f;
            }
        }
    }
    
    /// <summary>
    /// 应用组件效果到计算属性
    /// 根据组件类别和参数ID应用效果
    /// </summary>
    /// <param name="stats">钓鱼计算属性</param>
    public override void Apply(FishingCalculatedStats stats)
    {
        if (!IsActive() && !Config.isPassive)
        {
            return;
        }
        
        // 获取当前等级的所有参数
        var levelData = CurrentLevelData;
        if (levelData == null || levelData.paramsList == null)
            return;
        
        // 遍历所有参数，根据参数ID应用对应的效果
        foreach (var param in levelData.paramsList)
        {
            ApplyParamEffect(param.paramId, param.value, stats);
        }
    }
    
    /// <summary>
    /// 根据参数ID应用对应的效果
    /// </summary>
    /// <param name="paramId">参数ID</param>
    /// <param name="value">参数值</param>
    /// <param name="stats">钓鱼计算属性</param>
    private void ApplyParamEffect(int paramId, float value, FishingCalculatedStats stats)
    {
        switch (paramId)
        {
            // === 通用参数 ===
            case 1001: // 主参数 - 根据类别应用不同效果
                ApplyPrimaryEffect(value, stats);
                break;
            case 1002: // 次参数 - 根据类别应用不同效果
                ApplySecondaryEffect(value, stats);
                break;
            case 1003: // 第三参数 - 根据类别应用不同效果
                ApplyTertiaryEffect(value, stats);
                break;
                
            // === 鱼类权重相关 ===
            case 2001: // 鱼类权重倍率（乘法）
                stats.fishWeightMultiplier *= value;
                break;
            case 2002: // 鱼类权重加成（加法）
                stats.fishWeightMultiplier += value;
                break;
            case 2003: // 最小鱼类权重
                stats.minFishWeight = Mathf.Max(stats.minFishWeight, value);
                break;
            case 2004: // 最大鱼类权重
                stats.maxFishWeight = Mathf.Max(stats.maxFishWeight, value);
                break;
                
            // === 垃圾概率相关 ===
            case 3001: // 垃圾概率减少（减法）
                stats.trashProbability = Mathf.Max(0.01f, stats.trashProbability - value);
                break;
            case 3002: // 垃圾概率倍率（乘法）
                stats.trashProbability *= value;
                break;
            case 3003: // 最大连续垃圾数
                stats.maxTrashStreak = Mathf.Max(stats.maxTrashStreak, (int)value);
                break;
            case 3004: // 保底机制 - 连续垃圾后必出鱼
                stats.guaranteedFishAfterTrash = Mathf.Max(stats.guaranteedFishAfterTrash, (int)value);
                break;
                
            // === 重量倾向相关 ===
            case 4001: // 重量倾向系数（乘法）
                stats.weightBiasFactor *= value;
                break;
            case 4002: // 重量倾向加成（加法）
                stats.weightBiasFactor += value;
                break;
            case 4003: // 偏向大鱼（增加大鱼概率）
                stats.heavyFishBonus += value;
                break;
            case 4004: // 偏向小鱼（增加小鱼概率）
                stats.lightFishBonus += value;
                break;
                
            // === 挣扎时间相关 ===
            case 5001: // 挣扎时间减少百分比
                stats.struggleTimeMultiplier *= (1f - value / 100f);
                break;
            case 5002: // 挣扎时间倍率
                stats.struggleTimeMultiplier *= value;
                break;
            case 5003: // 最大挣扎时间上限
                stats.maxStruggleTime = Mathf.Min(stats.maxStruggleTime, value);
                break;
                
            // === 小游戏难度 ===
            case 6001: // 小游戏难度降低等级
                stats.minigameDifficultyReduction += value;
                break;
            case 6002: // 小游戏成功率加成
                stats.minigameSuccessBonus += value / 100f;
                break;
            case 6003: // 完美时机窗口扩大
                stats.perfectTimingWindow += value / 100f;
                break;
                
            // === 闪光率相关 ===
            case 7001: // 闪光率加成百分比
                stats.shinyRateBonus += value / 100f;
                break;
            case 7002: // 闪光判定倍率
                stats.shinyMultiplier *= value;
                break;
                
            // === 稀有度加成 ===
            case 8001: // 稀有度1权重加成
                AddRarityBonus(stats, 1, value);
                break;
            case 8002: // 稀有度2权重加成
                AddRarityBonus(stats, 2, value);
                break;
            case 8003: // 稀有度3权重加成
                AddRarityBonus(stats, 3, value);
                break;
            case 8004: // 稀有度4权重加成
                AddRarityBonus(stats, 4, value);
                break;
            case 8005: // 稀有度5权重加成
                AddRarityBonus(stats, 5, value);
                break;
            case 8010: // 全稀有度权重加成
                for (int i = 1; i <= 5; i++)
                    AddRarityBonus(stats, i, value);
                break;
            case 8011: // 稀有度保底等级（低于此等级必提升）
                stats.minRarityGuarantee = Mathf.Max(stats.minRarityGuarantee, (int)value);
                break;
                
            // === 咬钩率相关 ===
            case 9001: // 咬钩概率加成百分比
                float biteReduction = (value / 100f) * 0.5f;
                stats.trashProbability = Mathf.Max(0.01f, stats.trashProbability - biteReduction);
                break;
            case 9002: // 咬钩速度加成
                stats.biteSpeedBonus += value / 100f;
                break;
                
            // === 其他效果 ===
            case 9999: // 特殊效果标记（用于触发特殊逻辑）
                stats.hasSpecialEffect = true;
                break;
        }
    }
    
    /// <summary>
    /// 添加稀有度加成
    /// </summary>
    private void AddRarityBonus(FishingCalculatedStats stats, int rarityId, float bonus)
    {
        if (!stats.rarityBonus.ContainsKey(rarityId))
            stats.rarityBonus[rarityId] = 0f;
        stats.rarityBonus[rarityId] += bonus;
    }
    
    /// <summary>
    /// 应用主参数效果（根据类别）
    /// </summary>
    private void ApplyPrimaryEffect(float value, FishingCalculatedStats stats)
    {
        switch (Category)
        {
            case FishingComponentCategory.Rod:
                stats.fishWeightMultiplier += value;
                break;
            case FishingComponentCategory.Line:
                stats.minigameDifficultyReduction += value;
                break;
            case FishingComponentCategory.Hook:
                stats.trashProbability = Mathf.Max(0f, stats.trashProbability - value);
                break;
            case FishingComponentCategory.Skill:
                stats.shinyRateBonus += value;
                break;
        }
    }
    
    /// <summary>
    /// 应用次参数效果（根据类别）
    /// </summary>
    private void ApplySecondaryEffect(float value, FishingCalculatedStats stats)
    {
        switch (Category)
        {
            case FishingComponentCategory.Rod:
                stats.struggleTimeMultiplier *= (1f - value * 0.1f);
                break;
            case FishingComponentCategory.Line:
                stats.weightBiasFactor += value;
                break;
            case FishingComponentCategory.Hook:
                stats.maxTrashStreak = Mathf.Max(stats.maxTrashStreak, (int)value);
                break;
            case FishingComponentCategory.Skill:
                for (int i = 1; i <= 5; i++)
                    AddRarityBonus(stats, i, value);
                break;
        }
    }
    
    /// <summary>
    /// 应用第三参数效果（根据类别）
    /// </summary>
    private void ApplyTertiaryEffect(float value, FishingCalculatedStats stats)
    {
        // 第三参数可用于额外效果，默认不做处理
        // 可以在编辑器中根据需要配置特定的参数ID
    }
    
    /// <summary>
    /// 获取组件信息描述
    /// </summary>
    /// <returns>组件信息字符串</returns>
    public string GetComponentInfo()
    {
        string status = IsActive() ? "激活" : (IsOnCooldown ? "冷却中" : "未激活");
        return $"{Name} (Lv.{CurrentLevel}/{Config.maxLevel}) - {Category} - {status}\n" +
               $"参数1: {CurrentParam1:F2}\n" +
               $"参数2: {CurrentParam2:F2}\n" +
               $"参数3: {CurrentParam3:F2}\n" +
               $"{Description}";
    }
}