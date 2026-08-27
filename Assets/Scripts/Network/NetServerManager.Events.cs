using UnityEngine;
using System.Collections.Generic;
//using SharedModels;
//using Z_Logger = Utils.Z_Logger;

public partial class NetServerManager
{
    private void RegisterNetworkEvents()
    {
    }

    private void RegisterServerEvents()
    {
        Z_Logger.Log("[NetServerManager] 开始注册网络模式下的事件处理器...");

        // ========== 连续模式 ==========
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => isInContinuousMode);
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => continuousModeRemainingTime);
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());

        // ========== 玩家数据 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_INVENTORY", _ => GetPlayerInventory());
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_FISH_INVENTORY", _ => GetPlayerFishInventory());
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_BAG_CAPACITY", GetFishBagCapacityFromManager);
        CommunicateEvent.RegisterRequest<int, Dictionary<int, List<FishDetailData>>>("VIEW_EVENT_GET_FISH_DETAIL_DATA", GetFishDetailDataFromManager);
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_GOLD", _ => GetPlayerGold());

        // ========== 金币同步 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);

        // ========== 装备 ==========
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, itemId => GetComponentLevel(itemId));
        CommunicateEvent.RegisterRequest<int, PlayerNetworkData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());
        CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, OnEquipItem);
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_EQUIP_BAIT, OnEquipBait);
        CommunicateEvent.Register<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, OnUnequipBait);

        // ========== 人物 ==========
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, characterId => IsCharacterObtained(characterId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId => IsSkillObtained(skillId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId => IsItemEquipped(itemId));
        CommunicateEvent.RegisterRequest<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", slot => IsSkillSlotUnlocked(slot));

        // ========== CharacterServerManager ==========
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevel());
        CommunicateEvent.RegisterRequest<int, int>("CharacterManager_GetExpToNextLevel", _ => GetExpToNextLevel());

        // ========== 自动钓鱼 ==========
        CommunicateEvent.RegisterRequest<int, bool>("IsAutoFishing", _ => isAutoFishing);
        CommunicateEvent.RegisterRequest<int, bool>("IsPaused", _ => isPaused);
        CommunicateEvent.RegisterRequest<int, float>("GetTimeUntilNextFishing", _ => timeUntilNextFishing);
        CommunicateEvent.RegisterRequest<int, int>("GetTrashStreak", _ => trashStreak);
        CommunicateEvent.RegisterRequest<int, bool>("IsFishBagFull", _ => isFishBagFull);
        CommunicateEvent.RegisterRequest<int, string>("GetCurrentFishingMode", _ => currentFishingMode.ToString());

        // ========== 售卖鱼 ==========
        CommunicateEvent.Register<List<int>>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItems);

        // ========== 装备解锁 ==========
        CommunicateEvent.Register<int>("Equip_Unlock", OnUnlockEquipment);

        // ========== 商城 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, _ => GetMallItems());
        CommunicateEvent.RegisterRequest<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId => GetMallItem(itemId));
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, OnPurchaseMallItem);

        // ========== 窝料消耗 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE, OnConsumeBaitAndEnterContinuousMode);

        // ========== 场景切换 ==========
        CommunicateEvent.Register<int>("Server_SceneSwitch", SwitchPlayerScene);

        // ========== 人物等级奖励 ==========
        CommunicateEvent.RegisterRequest<int, bool>("HasClaimedLevelReward", playerId =>
        {
            return false;
        });

        // ========== 多鱼缸事件 ==========

        // ===== 查询事件 =====
        CommunicateEvent.RegisterRequest<int, List<FishTankInfoData>>("VIEW_EVENT_GET_FISH_TANK_LIST", OnGetFishTankList);
        CommunicateEvent.RegisterRequest<int, FishTankStatusData>("VIEW_EVENT_GET_FISH_TANK_STATUS", OnGetFishTankStatus);
        CommunicateEvent.RegisterRequest<int, List<FishDetailData>>("VIEW_EVENT_GET_FISH_TANK_ITEMS", OnGetFishTankItems);
        CommunicateEvent.RegisterRequest<int, FishTankUpgradeInfo>("VIEW_EVENT_GET_FISH_TANK_UPGRADE_INFO", OnGetFishTankUpgradeInfo);
        CommunicateEvent.RegisterRequest<int, bool>("VIEW_EVENT_IS_FISH_TANK_UNLOCKED", OnIsFishTankUnlocked);
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_LEVEL", OnGetFishTankLevel);
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_CAPACITY", OnGetFishTankCapacity);
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_COUNT", OnGetFishTankCount);
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_REMAINING_SPACE", OnGetFishTankRemainingSpace);

        // ===== 操作事件 =====
        CommunicateEvent.Register("FISH_TANK_OPEN", OnFishTankOpen);
        CommunicateEvent.Register("FISH_TANK_SYNC_STATUS", OnSyncFishTankStatus);
        CommunicateEvent.Register<int>("FISH_TANK_UNLOCK", OnUnlockFishTankRequest);
        CommunicateEvent.Register<int>("FISH_TANK_UPGRADE", OnUpgradeFishTankRequest);
        CommunicateEvent.Register<int, int>("FISH_TANK_MOVE_BAG_TO_TANK", OnMoveFishFromBagToTankRequest);
        CommunicateEvent.Register<int>("FISH_TANK_MOVE_TANK_TO_BAG", OnMoveFishFromTankToBagRequest);
        CommunicateEvent.Register<int, List<int>>("FISH_TANK_BATCH_MOVE_BAG_TO_TANK", OnBatchMoveFishFromBagToTankRequest);

        Z_Logger.Log("[NetServerManager] 事件处理器注册完成！");
    }

    private void OnSyncGold()
    {
        if (!_isEnabled)
            return;

        Z_Logger.Log("[NetServerManager] 收到金币同步请求");

        int currentGold = playerGold;
        Z_Logger.Log($"[NetServerManager] 当前金币: {currentGold}");

        var goldData = new Dictionary<string, object>
        {
            { "gold", currentGold },
            { "add", 0 },
            { "reduce", 0 }
        };

        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
        CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_GOLD_CHANGED, currentGold);
    }

    // ============================================================
    // 鱼篓数据查询（从 PlayerDataManager 获取，统一数据源）
    // ============================================================

    /// <summary>
    /// 从 PlayerDataManager 获取鱼篓容量
    /// </summary>
    private int GetFishBagCapacityFromManager(int _)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.fishBagCapacity;
        return 10;
    }

    /// <summary>
    /// 从 PlayerDataManager 获取鱼详情数据（统一数据源）
    /// </summary>
    private Dictionary<int, List<FishDetailData>> GetFishDetailDataFromManager(int _)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishDetailData();
        return new Dictionary<int, List<FishDetailData>>();
    }

    // ============================================================
    // 鱼缸查询事件处理器
    // ============================================================

    private List<FishTankInfoData> OnGetFishTankList(int _)
    {
        var result = new List<FishTankInfoData>();
        var statuses = FishTankList;
        foreach (var s in statuses)
        {
            var config = LoadDataManager.Instance?.GetFishTankConfig(s.tankId);
            result.Add(new FishTankInfoData
            {
                tankId = s.tankId,
                name = config?.name ?? $"鱼缸{s.tankId}",
                type = config?.type ?? "normal",
                purchaseCost = config?.purchaseCost ?? 0,
                isUnlocked = s.isUnlocked,
                level = s.level,
                capacity = s.capacity,
                currentCount = s.currentCount,
                remainingSpace = s.remainingSpace
            });
        }
        return result;
    }

    private FishTankStatusData OnGetFishTankStatus(int tankId)
    {
        return GetFishTankStatus(tankId);
    }

    private List<FishDetailData> OnGetFishTankItems(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.items ?? new List<FishDetailData>();
    }

    private FishTankUpgradeInfo OnGetFishTankUpgradeInfo(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        if (status != null)
        {
            return new FishTankUpgradeInfo
            {
                isUnlocked = status.isUnlocked,
                currentLevel = status.level,
                currentCapacity = status.capacity,
                nextLevel = status.level + 1,
                nextCapacity = status.capacity + 10,
                upgradeCost = 1000 * status.level,
                canUpgrade = status.level < 5,
                isMaxLevel = status.level >= 5
            };
        }
        return new FishTankUpgradeInfo
        {
            isUnlocked = false,
            currentLevel = 0,
            currentCapacity = 0,
            nextLevel = 1,
            nextCapacity = 10,
            upgradeCost = 1000,
            canUpgrade = false,
            isMaxLevel = false
        };
    }

    private bool OnIsFishTankUnlocked(int tankId)
    {
        return IsFishTankUnlocked(tankId);
    }

    private int OnGetFishTankLevel(int tankId)
    {
        return GetFishTankLevel(tankId);
    }

    private int OnGetFishTankCapacity(int tankId)
    {
        return GetFishTankCapacity(tankId);
    }

    private int OnGetFishTankCount(int tankId)
    {
        return GetFishTankCount(tankId);
    }

    private int OnGetFishTankRemainingSpace(int tankId)
    {
        return GetFishTankRemainingSpace(tankId);
    }
}
