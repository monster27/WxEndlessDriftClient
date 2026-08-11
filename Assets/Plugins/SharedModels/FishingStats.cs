using System;
using System.Collections.Generic;

[Serializable]
public class FishingStats
{
    // ===== 基础属性 =====
    public float trashProbability = 0.15f;
    public int maxTrashStreak = 0;
    public float fishWeightMultiplier = 1f;
    public float shinyRateBonus = 0f;
    public int minFishingInterval = 3;
    public int maxFishingInterval = 20;
    public float continuousPauseDuration = 1f;
    public float normalPauseDuration = 0.5f;
    public int fishBagCapacity = 20;
    public float struggleTimeMultiplier = 1f;
    public float maxStruggleTime = 10f;

    // ===== 稀有度权重（最终计算后的值） =====
    public Dictionary<int, int> rarityWeights = new Dictionary<int, int>();

    // ===== 新增：稀有度权重加成值（乘法用） =====
    public Dictionary<int, float> rarityWeightBonuses = new Dictionary<int, float>();

    // ===== 新增：重量倾向加成 =====
    public float weightBiasBonus = 0f;

    // ===== 新增：挣扎时间减少值 =====
    public float struggleTimeReduction = 0f;

    // ===== 新增：咬钩率加成 =====
    public float catchRateBonus = 0f;

    // ===== 新增：保底加成值 =====
    public float trashProtectionBonus = 0f;
}
