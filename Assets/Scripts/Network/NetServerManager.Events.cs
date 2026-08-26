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
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => isInContinuousMode);                                          // 是否在连续模式
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => continuousModeRemainingTime);                 // 获取连续模式剩余时间
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());                          // 获取当前场景窝料数量

        // ========== 玩家数据 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_INVENTORY", _ => GetPlayerInventory());                                        // 获取玩家背包
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_FISH_INVENTORY", _ => GetPlayerFishInventory());                               // 获取玩家鱼背包
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_BAG_CAPACITY", _ => GetFishBagCapacity());                                                 // 获取鱼篓容量
        CommunicateEvent.RegisterRequest<int, Dictionary<int, List<FishDetailData>>>("VIEW_EVENT_GET_FISH_DETAIL_DATA", _ => GetFishDetailData());                 // 获取鱼详情数据
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_GOLD", _ => GetPlayerGold());                                                                   // 获取金币

        // ========== 金币同步 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);                                                                                   // 金币同步事件

        // ========== 装备 ==========
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));                 // 获取已装备物品
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());                                          // 获取人物等级
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, itemId => GetComponentLevel(itemId));                               // 获取组件等级
        CommunicateEvent.RegisterRequest<int, PlayerNetworkData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());                                    // 获取玩家数据
        CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, OnEquipItem);                                                       // 装备物品
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_EQUIP_BAIT, OnEquipBait);                                                                            // 装备鱼饵
        CommunicateEvent.Register<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, OnUnequipBait);                                                          // 卸下鱼饵

        // ========== 人物 ==========
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, characterId => IsCharacterObtained(characterId));                 // 是否已获得人物
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId => IsSkillObtained(skillId));                                 // 是否已获得技能
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId => IsItemEquipped(itemId));                                     // 是否已装备物品
        CommunicateEvent.RegisterRequest<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", slot => IsSkillSlotUnlocked(slot));                                             // 技能槽是否解锁

        // ========== CharacterServerManager ==========
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());                          // 获取人物数据
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterManager_GetPlayerData", _ => GetPlayerCharacterData());                                // 获取人物数据
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevel());                                           // 获取升级所需经验
        CommunicateEvent.RegisterRequest<int, int>("CharacterManager_GetExpToNextLevel", _ => GetExpToNextLevel());                                                 // 获取升级所需经验

        // ========== 自动钓鱼 ==========
        CommunicateEvent.RegisterRequest<int, bool>("IsAutoFishing", _ => isAutoFishing);                                                                           // 是否自动钓鱼中
        CommunicateEvent.RegisterRequest<int, bool>("IsPaused", _ => isPaused);                                                                                     // 是否暂停
        CommunicateEvent.RegisterRequest<int, float>("GetTimeUntilNextFishing", _ => timeUntilNextFishing);                                                         // 获取下次钓鱼时间
        CommunicateEvent.RegisterRequest<int, int>("GetTrashStreak", _ => trashStreak);                                                                             // 获取垃圾连续次数
        CommunicateEvent.RegisterRequest<int, bool>("IsFishBagFull", _ => isFishBagFull);                                                                           // 鱼篓是否已满
        CommunicateEvent.RegisterRequest<int, string>("GetCurrentFishingMode", _ => currentFishingMode.ToString());                                                 // 获取当前钓鱼模式

        // ========== 售卖鱼 ==========
        CommunicateEvent.Register<List<int>>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItems);                                                              // 售卖鱼

        // ========== 装备解锁 ==========
        CommunicateEvent.Register<int>("Equip_Unlock", OnUnlockEquipment);                                                                                          // 解锁装备

        // ========== 商城 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, _ => GetMallItems());                           // 获取商城物品列表
        CommunicateEvent.RegisterRequest<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId => GetMallItem(itemId));                                   // 获取单个商城物品
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, OnPurchaseMallItem);                                                       // 购买商城物品

        // ========== 窝料消耗 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE, OnConsumeBaitAndEnterContinuousMode);                              // 消耗窝料进入连续模式

        // ========== 场景切换 ==========
        CommunicateEvent.Register<int>("Server_SceneSwitch", SwitchPlayerScene);                                                                                    // 切换玩家场景

        // ========== 人物等级奖励 ==========
        CommunicateEvent.RegisterRequest<int, bool>("HasClaimedLevelReward", playerId =>
        {
            // TODO: 后续优化 - 从服务器查询该等级奖励是否已领取
            // 目前返回 false，表示未领取（客户端可显示奖励按钮）
            return false;
        });                                                                                                   // 是否已领取等级奖励

        // ========== 多鱼缸事件 ==========

        // ===== 查询事件 =====

        CommunicateEvent.RegisterRequest<int, List<FishTankInfoData>>("VIEW_EVENT_GET_FISH_TANK_LIST", _ => _fishTankList);                                    // 获取所有鱼缸列表
        CommunicateEvent.RegisterRequest<int, FishTankStatusData>("VIEW_EVENT_GET_FISH_TANK_STATUS", OnGetFishTankStatus);                                     // 获取指定鱼缸状态
        CommunicateEvent.RegisterRequest<int, List<FishDetailData>>("VIEW_EVENT_GET_FISH_TANK_ITEMS", OnGetFishTankItems);                                   // ✅ 获取指定鱼缸中的鱼 - 返回 FishDetailData
        CommunicateEvent.RegisterRequest<int, FishTankUpgradeInfo>("VIEW_EVENT_GET_FISH_TANK_UPGRADE_INFO", OnGetFishTankUpgradeInfo);                         // 获取指定鱼缸升级信息
        CommunicateEvent.RegisterRequest<int, bool>("VIEW_EVENT_IS_FISH_TANK_UNLOCKED", OnIsFishTankUnlocked);                                                 // 检查指定鱼缸是否解锁
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_LEVEL", OnGetFishTankLevel);                                                      // 获取指定鱼缸等级
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_CAPACITY", OnGetFishTankCapacity);                                                // 获取指定鱼缸容量
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_COUNT", OnGetFishTankCount);                                                      // 获取指定鱼缸鱼数量
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_TANK_REMAINING_SPACE", OnGetFishTankRemainingSpace);                                   // 获取指定鱼缸剩余空间


        // ===== 操作事件 =====
        CommunicateEvent.Register("FISH_TANK_OPEN", OnFishTankOpen);
        CommunicateEvent.Register("FISH_TANK_SYNC_STATUS", OnSyncFishTankStatus);                                                                              // 同步鱼缸状态
        CommunicateEvent.Register<int>("FISH_TANK_UNLOCK", OnUnlockFishTankRequest);                                                                           // 解锁指定鱼缸
        CommunicateEvent.Register<int>("FISH_TANK_UPGRADE", OnUpgradeFishTankRequest);                                                                         // 升级指定鱼缸
        CommunicateEvent.Register<int, int>("FISH_TANK_MOVE_BAG_TO_TANK", OnMoveFishFromBagToTankRequest);                                                     // 从鱼篓放入指定鱼缸 (tankId, fishItemId)
        CommunicateEvent.Register<int>("FISH_TANK_MOVE_TANK_TO_BAG", OnMoveFishFromTankToBagRequest);                                                          // 从鱼缸取出到鱼篓
        CommunicateEvent.Register<int, List<int>>("FISH_TANK_BATCH_MOVE_BAG_TO_TANK", OnBatchMoveFishFromBagToTankRequest);                                    // 批量从鱼篓放入指定鱼缸 (tankId, fishItemIds)

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

    // ========== 鱼缸查询事件处理器 ==========

    private FishTankStatusData OnGetFishTankStatus(int tankId)
    {
        return GetFishTankStatus(tankId);
    }

    /// <summary>
    /// ✅ 获取指定鱼缸中的鱼 - 返回 FishDetailData
    /// </summary>
    private List<FishDetailData> OnGetFishTankItems(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.items ?? new List<FishDetailData>();
    }

    private FishTankUpgradeInfo OnGetFishTankUpgradeInfo(int tankId)
    {
        if (_fishTankStatusCache.TryGetValue(tankId, out var status))
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
        return new FishTankUpgradeInfo();
    }

    private bool OnIsFishTankUnlocked(int tankId)
    {
        return IsFishTankUnlocked(tankId);
    }

    private int OnGetFishTankLevel(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.level ?? 1;
    }

    private int OnGetFishTankCapacity(int tankId)
    {
        return GetFishTankCapacity(tankId);
    }

    private int OnGetFishTankCount(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.currentCount ?? 0;
    }

    private int OnGetFishTankRemainingSpace(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.remainingSpace ?? 0;
    }
}
