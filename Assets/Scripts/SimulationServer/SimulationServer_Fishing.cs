using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SimulationServer 钓鱼相关方法部分
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region 钓鱼相关方法

    /// <summary>
    /// 执行钓鱼操作 - 服务器端执行，获取两个ID并处理
    /// 第一组数据：检测到的鱼（用于获取挣扎时间）
    /// 第二组数据：实际钓到的物品（如果是垃圾则是垃圾ID，否则与第一组相同）
    /// </summary>
    /// <returns>钓鱼结果</returns>
    public FishingResult DoFishing()
    {
        if (fishingAlgorithmManager == null || sceneFishPoolManager == null)
        {
            Debug.LogError("[SimulationServer] 钓鱼管理器未初始化");
            return null;
        }

        var result = new FishingResult();

        // 执行钓鱼算法，获取两组数据
        var (detectedFishId, actualItemId) = fishingAlgorithmManager.ExecuteFishingAlgorithm(
            sceneFishPoolManager.CurrentSceneId,
            0
        );

        // 填充两个ID
        result.detectedFishId = detectedFishId;
        result.actualItemId = actualItemId;

        // 获取检测到的鱼的数据（第一组数据）
        FishData detectedFish = fishingAlgorithmManager.GetFishDataById(detectedFishId);
        
        // 判断是否是垃圾：检测到的ID和实际ID不同则为垃圾
        bool isTrash = (detectedFishId != actualItemId);
        
        // 第二组数据：实际钓上来的物品
        FishData actualFish = isTrash ? null : detectedFish;

        result.isSuccess = !isTrash;
        result.detectedFish = detectedFish;
        result.actualFish = actualFish;
        result.isTrash = isTrash;
        
        // 获取挣扎时间：使用第一组数据（检测到的鱼）获取挣扎时间
        // 如果不是鱼类则使用默认3秒
        result.struggleTime = fishingAlgorithmManager.GetStruggleTime(detectedFishId);

        Debug.LogFormat("<color=orange>[SimulationServer] 钓鱼结果 - 第一组(检测到): ID={0}, 名称={1}</color>", detectedFishId, detectedFish?.name);
        Debug.LogFormat("<color=orange>[SimulationServer] 钓鱼结果 - 第二组(实际): 是否垃圾={0}, 物品ID={1}</color>", isTrash, actualItemId);
        Debug.LogFormat("<color=orange>[SimulationServer] 挣扎时间: {0}秒</color>", result.struggleTime);

        // 将实际钓上来的物品添加到鱼篓（服务器端）
        // 此时不更新客户端鱼篓数据，等待拉杆动画结束后再更新
        if (!isTrash && actualFish != null)
        {
            inventoryManager?.AddFish(actualFish.id, 1);
            Debug.LogFormat("<color=orange>[SimulationServer] 鱼已添加到鱼篓(服务端): {0}</color>", actualFish.name);

            // 消耗装备的鱼饵
            ConsumeEquippedBait();
            
            // 添加人物经验
            if (actualFish != null)
            {
                Debug.LogFormat("<color=orange>[SimulationServer] 开始添加人物经验检查...</color>");
                Debug.LogFormat("<color=orange>[SimulationServer] CharacterServerManager.Instance = {0}</color>", CharacterServerManager.Instance != null ? "非空" : "空");

                if (CharacterServerManager.Instance != null)
                {
                    var charData = CharacterServerManager.Instance.GetPlayerCharacterData();
                    Debug.LogFormat("<color=orange>[SimulationServer] 人物数据: isEquipped={0}, level={1}</color>", charData?.isEquipped, charData?.currentLevel);

                    int rarityId = actualFish.rarityId;
                    int exp = CharacterServerManager.Instance.GetFishExpByRarity(rarityId);
                    Debug.LogFormat("<color=orange>[SimulationServer] 鱼类稀有度: {0}, 经验: {1}</color>", rarityId, exp);

                    CharacterServerManager.Instance.AddCharacterExp(exp);
                    Debug.LogFormat("<color=orange>[SimulationServer] 人物获得经验: {0} (稀有度ID: {1})</color>", exp, rarityId);
                }
            }
        }
        else if (isTrash)
        {
            // 垃圾也要添加到鱼篓
            inventoryManager?.AddFish(actualItemId, 1);
            Debug.LogFormat("<color=orange>[SimulationServer] 垃圾已添加到鱼篓(服务端): ID={0}</color>", actualItemId);
        }
        
        return result;
    }

    /// <summary>
    /// 通知客户端钓鱼结果 - 由ServerManager调用
    /// </summary>
    /// <param name="result">钓鱼结果</param>
    public void NotifyFishingResultToClient(FishingResult result)
    {
        if (ServerManager.Instance != null)
        {
            ServerManager.Instance.OnServerFishingResult(result);
        }
    }

    /// <summary>
    /// 播放闲置动画
    /// </summary>
    public void PlayIdleAnimation()
    {
        ServerManager.Instance?.NotifyPlayIdleAnimation();
    }

    /// <summary>
    /// 播放拉杆动画
    /// </summary>
    public void PlayReelAnimation()
    {
        ServerManager.Instance?.NotifyPlayReelAnimation(3f, null);
    }

    /// <summary>
    /// 延迟播放空闲动画
    /// </summary>
    private void DelayedPlayIdleAnimation(float delay)
    {
        idleAnimationTimer = delay + 0.3f;
        isWaitingForIdleAnimation = true;
    }

    /// <summary>
    /// 开始自动钓鱼
    /// </summary>
    public void StartAutoFishing()
    {
        if (IsFishBagFull())
        {
            ServerManager.Instance?.NotifyPlayLazyAnimation();
            Debug.LogFormat("<color=orange>[SimulationServer] 鱼篓已满，进入Lazy模式</color>");
        }
        else
        {
            autoFishingManager?.StartAutoFishing();
            Debug.LogFormat("<color=orange>[SimulationServer] 开始自动钓鱼</color>");
        }
    }

    /// <summary>
    /// 停止自动钓鱼
    /// </summary>
    public void StopAutoFishing()
    {
        autoFishingManager?.StopAutoFishing();
        Debug.LogFormat("<color=orange>[SimulationServer] 停止自动钓鱼</color>");
    }

    /// <summary>
    /// 获取距离下次钓鱼的剩余时间
    /// </summary>
    /// <returns>剩余时间（秒）</returns>
    public float GetTimeUntilNextFishing()
    {
        return autoFishingManager?.GetTimeUntilNextFishing() ?? 0f;
    }

    /// <summary>
    /// 设置场景ID
    /// </summary>
    public void SetSceneId(int sceneId)
    {
        sceneFishPoolManager?.SwitchScene(sceneId);

        // 更新EnvManager的场景ID
        if (EnvManager.Instance != null)
        {
            EnvManager.Instance.currentSceneId = sceneId;
        }

        // 场景切换时，重新同步当前时间和天气状态给客户端
        Debug.LogFormat("<color=orange>[SimulationServer] 场景切换到: {0}，重新同步环境状态</color>", sceneId);
        SendInitialEnvState();
    }

    #region 连续钓鱼模式相关

    /// <summary>
    /// 连续钓鱼模式剩余时间（秒）
    /// </summary>
    private float continuousModeRemainingTime = 0f;

    /// <summary>
    /// 窝料初始数量配置（每个场景的默认窝料数量）
    /// </summary>
    private const int DEFAULT_BAIT_COUNT = 5;

    /// <summary>
    /// 每次点击增加的持续时间（秒）
    /// </summary>
    private const float CONTINUOUS_MODE_ADD_TIME = 30f;

    /// <summary>
    /// 连续钓鱼模式最大持续时间（秒） = 999分钟
    /// </summary>
    private const float CONTINUOUS_MODE_MAX_TIME = 999f * 60f;

    /// <summary>
    /// 获取连续钓鱼模式剩余时间
    /// </summary>
    public float ContinuousModeRemainingTime => continuousModeRemainingTime;

    /// <summary>
    /// 是否处于连续钓鱼模式
    /// </summary>
    public bool IsInContinuousMode => continuousModeRemainingTime > 0f;

    /// <summary>
    /// 默认窝料物品ID
    /// </summary>
    private const int DEFAULT_BAIT_ITEM_ID = 2501;

    /// <summary>
    /// 消耗装备的鱼饵
    /// 钓鱼成功后消耗背包中对应的鱼饵数量
    /// </summary>
    private void ConsumeEquippedBait()
    {
        // 获取装备的鱼饵ID
        int equippedBaitId = inventoryManager?.GetEquippedItem(EquipmentSlotType.Bait) ?? 0;
        
        if (equippedBaitId > 0)
        {
            // 获取背包中该鱼饵的数量
            int currentCount = inventoryManager.GetItemQuantity(equippedBaitId);
            
            if (currentCount > 0)
            {
                // 消耗一个鱼饵
                inventoryManager.RemoveItem(equippedBaitId, 1);
                int remainingCount = inventoryManager.GetItemQuantity(equippedBaitId);
                
                Debug.LogFormat("<color=orange>[SimulationServer] 消耗鱼饵: ID={0}, 剩余数量: {1}</color>", equippedBaitId, remainingCount);

                // 如果鱼饵用完了，卸下装备
                if (remainingCount <= 0)
                {
                    inventoryManager.UnequipItem(EquipmentSlotType.Bait);
                    Debug.LogFormat("<color=orange>[SimulationServer] 鱼饵已用完，自动卸下装备</color>");
                }
            }
            else
            {
                Debug.LogWarningFormat("[SimulationServer] 装备了鱼饵ID={0}但背包中没有该物品", equippedBaitId);
                // 卸下无效的装备
                inventoryManager.UnequipItem(EquipmentSlotType.Bait);
            }
        }
        else
        {
            Debug.LogFormat("<color=orange>[SimulationServer] 未装备鱼饵，无需消耗</color>");
        }
    }

    /// <summary>
    /// 获取当前场景的窝料数量
    /// 从背包中获取窝料数量
    /// </summary>
    public int GetCurrentSceneBaitCount()
    {
        return inventoryManager?.GetItemQuantity(DEFAULT_BAIT_ITEM_ID) ?? 0;
    }

    /// <summary>
    /// 设置当前场景的窝料数量
    /// 通过背包系统设置
    /// </summary>
    public void SetCurrentSceneBaitCount(int count)
    {
        int currentCount = inventoryManager?.GetItemQuantity(DEFAULT_BAIT_ITEM_ID) ?? 0;
        int diff = count - currentCount;
        
        if (diff > 0)
        {
            inventoryManager?.AddItem(DEFAULT_BAIT_ITEM_ID, diff);
            Debug.LogFormat("<color=orange>[SimulationServer] 添加窝料: {0}个，当前数量: {1}</color>", diff, count);
        }
        else if (diff < 0)
        {
            inventoryManager?.RemoveItem(DEFAULT_BAIT_ITEM_ID, -diff);
            Debug.LogFormat("<color=orange>[SimulationServer] 减少窝料: {0}个，当前数量: {1}</color>", -diff, count);
        }
    }

    /// <summary>
    /// 消耗窝料并进入连续钓鱼模式
    /// 服务器控制：检测窝料数量，如果大于0则减少1并进入连续钓鱼模式
    /// </summary>
    /// <returns>是否成功进入连续钓鱼模式</returns>
    public bool ConsumeBaitAndEnterContinuousMode()
    {
        int currentBait = GetCurrentSceneBaitCount();

        Debug.LogFormat("<color=orange>[SimulationServer] ConsumeBaitAndEnterContinuousMode - 当前窝料: {0}</color>", currentBait);

        if (currentBait > 0)
        {
            // 从背包中消耗一个窝料
            inventoryManager?.RemoveItem(DEFAULT_BAIT_ITEM_ID, 1);

            // 如果已经处于连续模式，累加时间；否则设置为30秒
            if (continuousModeRemainingTime > 0f)
            {
                continuousModeRemainingTime = Mathf.Min(continuousModeRemainingTime + CONTINUOUS_MODE_ADD_TIME, CONTINUOUS_MODE_MAX_TIME);
            }
            else
            {
                continuousModeRemainingTime = CONTINUOUS_MODE_ADD_TIME;
                SetFishingMode(AutoFishingServerManager.FishingMode.Continuous);
            }

            int remainingBait = GetCurrentSceneBaitCount();
            Debug.LogFormat("<color=orange>[SimulationServer] 消耗窝料成功，剩余: {0}，连续模式时间: {1}秒</color>", remainingBait, continuousModeRemainingTime);

            UIManager.ShowMessage($"窝料剩余: {remainingBait}");
            return true;
        }

        Debug.LogFormat("<color=orange>[SimulationServer] 窝料不足，无法进入连续模式，当前窝料: {0}</color>", currentBait);
        UIManager.ShowMessage("窝料不足");
        return false;
    }

    /// <summary>
    /// 增加连续钓鱼模式持续时间
    /// 每次点击增加30秒，不超过最大时间限制
    /// </summary>
    public void AddContinuousModeTime()
    {
        if (continuousModeRemainingTime > 0f)
        {
            continuousModeRemainingTime = Mathf.Min(continuousModeRemainingTime + CONTINUOUS_MODE_ADD_TIME, CONTINUOUS_MODE_MAX_TIME);
            Debug.LogFormat("<color=orange>[SimulationServer] 增加连续模式时间，当前剩余: {0}秒</color>", continuousModeRemainingTime);
        }
    }

    /// <summary>
    /// 更新连续钓鱼模式计时器（每帧调用）
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public void UpdateContinuousMode(float deltaTime)
    {
        if (continuousModeRemainingTime > 0f)
        {
            continuousModeRemainingTime -= deltaTime;
            if (continuousModeRemainingTime <= 0f)
            {
                continuousModeRemainingTime = 0f;
                SetFishingMode(AutoFishingServerManager.FishingMode.Normal);
                Debug.LogFormat("<color=orange>[SimulationServer] 连续钓鱼模式结束，恢复普通模式</color>");
            }
        }
    }

    /// <summary>
    /// 初始化场景窝料数量
    /// 通过背包系统添加窝料
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <param name="baitCount">窝料数量</param>
    public void InitSceneBaitCount(int sceneId, int baitCount)
    {
        // 获取当前背包中的窝料数量
        int currentCount = inventoryManager?.GetItemQuantity(DEFAULT_BAIT_ITEM_ID) ?? 0;
        
        // 只添加不足的部分
        if (baitCount > currentCount)
        {
            int addCount = baitCount - currentCount;
            inventoryManager?.AddItem(DEFAULT_BAIT_ITEM_ID, addCount);
            Debug.LogFormat("<color=orange>[SimulationServer] 初始化场景{0}窝料数量: 添加{1}个，当前总数: {2}</color>", sceneId, addCount, baitCount);
        }
        else
        {
            Debug.LogFormat("<color=orange>[SimulationServer] 初始化场景{0}窝料数量: 已有{1}个，无需添加</color>", sceneId, currentCount);
        }
    }

    #endregion

    #endregion
}