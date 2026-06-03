using System.Collections.Generic;

/// <summary>
/// 钓鱼基础属性类
/// 存储钓鱼系统的基础配置值
/// </summary>
public class FishingBaseStats
{
    /// <summary>
    /// 垃圾概率（0-1）
    /// </summary>
    public float trashProbability = 0.15f;
    
    /// <summary>
    /// 重量倾向系数（影响随机重量的偏向）
    /// </summary>
    public float weightBiasFactor = 1.0f;
    
    /// <summary>
    /// 基础挣扎时间倍率
    /// </summary>
    public float baseStruggleMultiplier = 1.0f;
    
    /// <summary>
    /// 鱼类权重倍率
    /// </summary>
    public float fishWeightMultiplier = 1.0f;
    
    /// <summary>
    /// 闪光率加成（0-1）
    /// </summary>
    public float shinyRateBonus = 0f;
    
    /// <summary>
    /// 稀有度权重字典（稀有度ID -> 权重值）
    /// </summary>
    public Dictionary<int, int> rarityWeights = new Dictionary<int, int>();
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public FishingBaseStats()
    {
        // 初始化默认稀有度权重
        rarityWeights.Add(1, 200);  // 普通
        rarityWeights.Add(2, 150);  // 稀有
        rarityWeights.Add(3, 120);  // 精良
        rarityWeights.Add(4, 90);   // 史诗
        rarityWeights.Add(5, 60);   // 传说
    }
}

/// <summary>
/// 钓鱼计算属性类
/// 存储经过能力加成后的最终钓鱼属性
/// </summary>
public class FishingCalculatedStats
{
    // === 基础属性 ===
    /// <summary>
    /// 最终垃圾概率
    /// </summary>
    public float trashProbability;
    
    /// <summary>
    /// 最终重量倾向系数
    /// </summary>
    public float weightBiasFactor;
    
    /// <summary>
    /// 最终挣扎时间倍率
    /// </summary>
    public float struggleTimeMultiplier;
    
    /// <summary>
    /// 最终鱼类权重倍率
    /// </summary>
    public float fishWeightMultiplier;
    
    /// <summary>
    /// 最终闪光率加成
    /// </summary>
    public float shinyRateBonus;
    
    /// <summary>
    /// 小游戏难度降低等级
    /// </summary>
    public float minigameDifficultyReduction;
    
    /// <summary>
    /// 稀有度加成字典（稀有度ID -> 加成值）
    /// </summary>
    public Dictionary<int, float> rarityBonus = new Dictionary<int, float>();
    
    // === 保底相关 ===
    /// <summary>
    /// 钓鱼保底 - 连续垃圾次数上限（0表示无保底）
    /// </summary>
    public int maxTrashStreak;
    
    /// <summary>
    /// 钓鱼保底 - 当前连续垃圾次数
    /// </summary>
    public int currentTrashStreak;
    
    /// <summary>
    /// 保底机制 - 连续垃圾后必出鱼（0表示无此机制）
    /// </summary>
    public int guaranteedFishAfterTrash;
    
    // === 鱼类权重限制 ===
    /// <summary>
    /// 最小鱼类权重
    /// </summary>
    public float minFishWeight;
    
    /// <summary>
    /// 最大鱼类权重
    /// </summary>
    public float maxFishWeight;
    
    // === 重量倾向 ===
    /// <summary>
    /// 偏向大鱼（增加大鱼概率）
    /// </summary>
    public float heavyFishBonus;
    
    /// <summary>
    /// 偏向小鱼（增加小鱼概率）
    /// </summary>
    public float lightFishBonus;
    
    // === 挣扎时间 ===
    /// <summary>
    /// 最大挣扎时间上限（秒）
    /// </summary>
    public float maxStruggleTime = 30f;
    
    // === 小游戏相关 ===
    /// <summary>
    /// 小游戏成功率加成
    /// </summary>
    public float minigameSuccessBonus;
    
    /// <summary>
    /// 完美时机窗口扩大比例
    /// </summary>
    public float perfectTimingWindow;
    
    // === 闪光相关 ===
    /// <summary>
    /// 闪光判定倍率
    /// </summary>
    public float shinyMultiplier = 1.0f;
    
    // === 稀有度保底 ===
    /// <summary>
    /// 稀有度保底等级（低于此等级必提升）
    /// </summary>
    public int minRarityGuarantee;
    
    // === 咬钩相关 ===
    /// <summary>
    /// 咬钩速度加成
    /// </summary>
    public float biteSpeedBonus;
    
    // === 特殊效果 ===
    /// <summary>
    /// 是否有特殊效果
    /// </summary>
    public bool hasSpecialEffect;
    
    /// <summary>
    /// 重置所有属性为默认值
    /// </summary>
    public void Reset()
    {
        trashProbability = 0.15f;
        weightBiasFactor = 1.0f;
        struggleTimeMultiplier = 1.0f;
        fishWeightMultiplier = 1.0f;
        shinyRateBonus = 0f;
        minigameDifficultyReduction = 0f;
        rarityBonus.Clear();
        maxTrashStreak = 0;
        currentTrashStreak = 0;
        guaranteedFishAfterTrash = 0;
        minFishWeight = 0f;
        maxFishWeight = 0f;
        heavyFishBonus = 0f;
        lightFishBonus = 0f;
        maxStruggleTime = 30f;
        minigameSuccessBonus = 0f;
        perfectTimingWindow = 0f;
        shinyMultiplier = 1.0f;
        minRarityGuarantee = 0;
        biteSpeedBonus = 0f;
        hasSpecialEffect = false;
    }
}