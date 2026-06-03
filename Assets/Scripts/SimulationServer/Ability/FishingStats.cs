// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;

/// <summary>
/// 钓鱼属性定义类
/// 包含钓鱼相关的各种属性和常量
/// </summary>
public class FishingStats
{
    // 基础属性
    public float biteRate = 1.0f;          // 咬钩率
    public float catchRate = 1.0f;         // 捕获成功率
    public float struggleTime = 1.0f;      // 挣扎时间倍率
    public float minigameDifficulty = 1.0f; // 小游戏难度倍率
    public float rareFishRate = 1.0f;      // 稀有鱼概率加成
    public float shinyRate = 1.0f;         // 闪光概率加成
    public float qualityRate = 1.0f;       // 品质加成
    public float trashProbability = 0.15f; // 垃圾概率
    public int maxTrashStreak = 0;         // 最大连续垃圾次数（保底）

    // 能力等级相关
    public int rodLevel = 1;      // 鱼竿等级
    public int lineLevel = 1;     // 鱼线等级
    public int hookLevel = 1;     // 鱼钩等级

    // 稀有度权重
    public int commonWeight = 200;    // 普通鱼权重
    public int uncommonWeight = 150;  // 优秀鱼权重
    public int rareWeight = 120;      // 稀有鱼权重
    public int epicWeight = 90;       // 史诗鱼权重
    public int legendaryWeight = 60;  // 传说鱼权重

    // 状态标志
    public bool isAutoFishing = false;   // 是否自动钓鱼
    public bool isLuckyMode = false;     // 是否幸运模式

    /// <summary>
    /// 获取稀有度权重
    /// </summary>
    /// <param name="rarityId">稀有度ID</param>
    /// <returns>权重值</returns>
    public int GetRarityWeight(int rarityId)
    {
        switch (rarityId)
        {
            case 1: return commonWeight;
            case 2: return uncommonWeight;
            case 3: return rareWeight;
            case 4: return epicWeight;
            case 5: return legendaryWeight;
            default: return 100;
        }
    }

    /// <summary>
    /// 重置所有属性为默认值
    /// </summary>
    public void ResetToDefault()
    {
        biteRate = 1.0f;
        catchRate = 1.0f;
        struggleTime = 1.0f;
        minigameDifficulty = 1.0f;
        rareFishRate = 1.0f;
        shinyRate = 1.0f;
        qualityRate = 1.0f;
        trashProbability = 0.15f;
        maxTrashStreak = 0;
        rodLevel = 1;
        lineLevel = 1;
        hookLevel = 1;
        isAutoFishing = false;
        isLuckyMode = false;
    }

    /// <summary>
    /// 复制属性
    /// </summary>
    /// <param name="source">源属性对象</param>
    public void CopyFrom(FishingStats source)
    {
        if (source == null) return;
        
        biteRate = source.biteRate;
        catchRate = source.catchRate;
        struggleTime = source.struggleTime;
        minigameDifficulty = source.minigameDifficulty;
        rareFishRate = source.rareFishRate;
        shinyRate = source.shinyRate;
        qualityRate = source.qualityRate;
        trashProbability = source.trashProbability;
        maxTrashStreak = source.maxTrashStreak;
        rodLevel = source.rodLevel;
        lineLevel = source.lineLevel;
        hookLevel = source.hookLevel;
        commonWeight = source.commonWeight;
        uncommonWeight = source.uncommonWeight;
        rareWeight = source.rareWeight;
        epicWeight = source.epicWeight;
        legendaryWeight = source.legendaryWeight;
        isAutoFishing = source.isAutoFishing;
        isLuckyMode = source.isLuckyMode;
    }
}
*/
