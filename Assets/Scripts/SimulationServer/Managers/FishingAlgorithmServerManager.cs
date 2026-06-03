// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 钓鱼算法服务器管理器
/// 负责执行核心钓鱼逻辑，包括鱼类选择、稀有度计算、垃圾判断等
/// </summary>
public class FishingAlgorithmServerManager
{
    private PlayerFishingAbilityManager abilityManager;
    private SceneFishPoolServerManager fishPoolManager;

    public void Initialize(PlayerFishingAbilityManager abilityManager, SceneFishPoolServerManager fishPoolManager)
    {
        this.abilityManager = abilityManager;
        this.fishPoolManager = fishPoolManager;
        Debug.Log("[FishingAlgorithmServerManager] 钓鱼算法服务器管理器初始化完成");
    }

    /// <summary>
    /// 执行钓鱼算法
    /// </summary>
    /// <param name="sceneId">场景ID（影响鱼库选择）</param>
    /// <param name="trashStreak">垃圾连续次数</param>
    /// <returns>元组(鱼类ID, 最终物品ID)</returns>
    public (int, int) ExecuteFishingAlgorithm(int sceneId, int trashStreak)
    {
        bool isTrash = DetermineIsTrash(trashStreak);
        int fishId = GetRandomFishByScene(sceneId);

        if (isTrash)
        {
            int trashId = GetRandomTrash();
            return (fishId, trashId);
        }
        else
        {
            return (fishId, fishId);
        }
    }

    /// <summary>
    /// 判断是否钓到垃圾（考虑玩家能力和钓鱼保底）
    /// </summary>
    /// <param name="trashStreak">垃圾连续次数</param>
    /// <returns>是否是垃圾</returns>
    public bool DetermineIsTrash(int trashStreak)
    {
        float baseTrashProbability = 0.15f;
        int maxTrashStreak = 0;

        // 应用玩家能力修正
        if (abilityManager != null)
        {
            baseTrashProbability = abilityManager.CalculatedStats.trashProbability;
            maxTrashStreak = abilityManager.CalculatedStats.maxTrashStreak;
        }

        baseTrashProbability = Mathf.Clamp(baseTrashProbability, 0.01f, 0.5f);

        // 检查钓鱼保底：如果设置了保底上限且当前连续次数已达到，必定不钓垃圾
        if (maxTrashStreak > 0 && trashStreak >= maxTrashStreak)
        {
            Debug.Log($"[钓鱼保底触发] 连续垃圾次数({trashStreak})达到上限({maxTrashStreak})，必定钓上非垃圾");
            return false;
        }

        // 垃圾连续次数越多，概率越低
        if (trashStreak < 5)
        {
            return Random.value < baseTrashProbability;
        }
        else if (trashStreak < 9)
        {
            float trashProbability = baseTrashProbability - (trashStreak - 4) * 0.02f;
            return Random.value < Mathf.Max(0.01f, trashProbability);
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// 获取随机垃圾ID
    /// </summary>
    /// <returns>垃圾物品ID</returns>
    public int GetRandomTrash()
    {
        return Random.Range(9001, 9004);
    }

    /// <summary>
    /// 根据场景获取随机鱼（场景影响鱼库选择）
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <returns>鱼类ID</returns>
    public int GetRandomFishByScene(int sceneId)
    {
        // 获取场景对应的鱼库（GetSceneFishPool已包含后备逻辑）
        List<FishData> sceneFishes = fishPoolManager.GetSceneFishPool(sceneId);

        // 如果鱼库为空，返回默认鱼ID
        if (sceneFishes == null || sceneFishes.Count == 0)
        {
            Debug.LogError("[FishingAlgorithmServerManager] 鱼库为空，返回默认鱼ID");
            return 1001;
        }

        WeightPool<FishData> fishPool = new WeightPool<FishData>();
        foreach (var fish in sceneFishes)
        {
            int totalWeight = CalculateFishWeight(fish);
            fishPool.Add(fish, totalWeight);
        }

        FishData selectedFish = fishPool.Get();

        // 确保总是返回有效的鱼ID
        int resultId = selectedFish != null ? selectedFish.id : 1001;
        Debug.Log($"[FishingAlgorithmServerManager] 选择鱼ID: {resultId}");
        return resultId;
    }

    /// <summary>
    /// 计算鱼类权重（考虑玩家能力）
    /// </summary>
    /// <param name="fish">鱼类数据</param>
    /// <returns>权重值</returns>
    public int CalculateFishWeight(FishData fish)
    {
        int baseWeight = (int)(fish.baseWeight * 3);

        // 应用玩家能力：鱼类权重倍率
        float fishWeightMultiplier = 1f;
        if (abilityManager != null)
        {
            fishWeightMultiplier = abilityManager.CalculatedStats.fishWeightMultiplier;
        }
        baseWeight = (int)(baseWeight * fishWeightMultiplier);

        // 获取稀有度权重
        int rarityWeight = GetRarityWeight(fish.rarityId);

        // 应用玩家能力：稀有度加成
        if (abilityManager != null && abilityManager.CalculatedStats.rarityBonus.ContainsKey(fish.rarityId))
        {
            rarityWeight += (int)abilityManager.CalculatedStats.rarityBonus[fish.rarityId];
        }

        int timeWeight = 100;
        int weatherWeight = 100;

        // 计算闪光加成（考虑玩家能力）
        int shinyBonus = 0;
        float actualFlashProb = fish.flashProbability;
        if (abilityManager != null)
        {
            actualFlashProb += abilityManager.CalculatedStats.shinyRateBonus;
        }
        if (Random.value < actualFlashProb)
        {
            shinyBonus = 50 + (int)(abilityManager != null ? abilityManager.CalculatedStats.shinyRateBonus * 100 : 0);
        }

        int totalWeight = (baseWeight * rarityWeight * timeWeight * weatherWeight) / 10000 + shinyBonus;

        return Mathf.Max(1, totalWeight);
    }

    /// <summary>
    /// 获取稀有度权重
    /// </summary>
    /// <param name="rarityId">稀有度ID</param>
    /// <returns>稀有度权重</returns>
    private int GetRarityWeight(int rarityId)
    {
        if (abilityManager != null && abilityManager.BaseStats.rarityWeights.ContainsKey(rarityId))
        {
            return abilityManager.BaseStats.rarityWeights[rarityId];
        }

        // 默认稀有度权重
        switch (rarityId)
        {
            case 1: return 200;
            case 2: return 150;
            case 3: return 120;
            case 4: return 90;
            case 5: return 60;
            default: return 100;
        }
    }

    /// <summary>
    /// 获取挣扎时间（考虑玩家能力）
    /// 使用鱼表中配置的 struggleTime 字段
    /// </summary>
    /// <param name="fishId">鱼ID</param>
    /// <returns>挣扎时间（秒）</returns>
    public float GetStruggleTime(int fishId)
    {
        // 非鱼类（如垃圾）的挣扎时间固定为3秒
        if (fishId >= 3001 && fishId <= 3003)
        {
            Debug.Log($"[FishingAlgorithmServerManager] GetStruggleTime - fishId={fishId} 是垃圾，固定挣扎时间=3秒");
            return 3.0f;
        }

        float baseStruggleTime = 2.0f;
        bool hasFishData = false;

        FishData fishData = GetFishDataById(fishId);
        if (fishData != null)
        {
            // 使用鱼表中配置的 struggleTime（秒）
            baseStruggleTime = fishData.struggleTime;
            hasFishData = true;
            Debug.Log($"[FishingAlgorithmServerManager] GetStruggleTime - fishId={fishId}, 鱼名={fishData.name}, 表中挣扎时间={baseStruggleTime}秒");
        }
        else
        {
            Debug.LogWarning($"[FishingAlgorithmServerManager] GetStruggleTime - fishId={fishId} 未找到鱼类数据，使用默认挣扎时间={baseStruggleTime}秒");
        }

        // 如果有鱼表数据，直接使用鱼表中的挣扎时间
        // 如果没有鱼表数据，才应用玩家能力修正
        float finalStruggleTime = baseStruggleTime;

        if (!hasFishData && abilityManager != null)
        {
            float struggleTimeMultiplier = abilityManager.CalculatedStats.struggleTimeMultiplier;
            float maxStruggleTime = abilityManager.CalculatedStats.maxStruggleTime;

            Debug.Log($"[FishingAlgorithmServerManager] GetStruggleTime - 无鱼表数据，应用玩家能力修正: 倍率={struggleTimeMultiplier:F2}, 上限={maxStruggleTime}秒");

            finalStruggleTime *= struggleTimeMultiplier;
            finalStruggleTime = Mathf.Min(finalStruggleTime, maxStruggleTime);
        }

        finalStruggleTime = Mathf.Max(1.0f, finalStruggleTime);

        Debug.Log($"[FishingAlgorithmServerManager] GetStruggleTime - fishId={fishId}, 最终挣扎时间={finalStruggleTime}秒 (来源={(hasFishData ? "鱼表" : "默认×倍率")})");

        return finalStruggleTime;
    }

    /// <summary>
    /// 判断是否是鱼类ID
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否是鱼类</returns>
    public bool IsFishId(int itemId)
    {
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.fishes != null)
        {
            return LoadDataManager.Instance.fishes.Exists(fish => fish.id == itemId);
        }
        return false;
    }

    /// <summary>
    /// 根据鱼类ID获取鱼类数据
    /// </summary>
    /// <param name="fishId">鱼类ID</param>
    /// <returns>鱼类数据</returns>
    public FishData GetFishDataById(int fishId)
    {
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.fishes != null)
        {
            return LoadDataManager.Instance.fishes.Find(fish => fish.id == fishId);
        }
        return null;
    }
}
*/
