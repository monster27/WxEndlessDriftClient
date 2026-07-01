using UnityEngine;
using System.Collections.Generic;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager
{
    private void RegisterNetworkEvents()
    {
    }

    private void RegisterServerEvents()
    {
        Logger.Log("[NetServerManager] 开始注册网络模式下的事件处理器...");

        // ========== 连续模式 ==========
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => isInContinuousMode);
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => continuousModeRemainingTime);
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());

        // ========== 玩家数据 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_INVENTORY", _ => GetPlayerInventory());
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>("VIEW_EVENT_GET_FISH_INVENTORY", _ => GetPlayerFishInventory());
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_FISH_BAG_CAPACITY", _ => GetFishBagCapacity());
        CommunicateEvent.RegisterRequest<int, Dictionary<int, List<FishDetailData>>>("VIEW_EVENT_GET_FISH_DETAIL_DATA", _ => GetFishDetailData());
        CommunicateEvent.RegisterRequest<int, int>("VIEW_EVENT_GET_GOLD", _ => GetPlayerGold());

        // ========== 金币同步 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);

        // ========== 装备 ==========
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());
        CommunicateEvent.RegisterRequest<int, PlayerNetworkData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());
        CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, OnEquipItem);
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_EQUIP_BAIT, OnEquipBait);

        // ========== 人物 ==========
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, characterId => IsCharacterObtained(characterId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId => IsSkillObtained(skillId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId => IsItemEquipped(itemId));

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
        CommunicateEvent.Register<(List<int>, int)>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItems);

        // ========== 装备解锁 ==========
        CommunicateEvent.Register<int>("Equip_Unlock", OnUnlockEquipment);

        // ========== 商城 ==========
        CommunicateEvent.RegisterRequest<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, _ => GetMallItems());
        CommunicateEvent.RegisterRequest<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId => GetMallItem(itemId));
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, OnPurchaseMallItem);

        // ========== 窝料消耗 ==========
        CommunicateEvent.Register(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE, OnConsumeBaitAndEnterContinuousMode);

        // ✅ 新增：场景切换
        CommunicateEvent.Register<int>("Server_SceneSwitch", SwitchPlayerScene);

        Logger.Log("[NetServerManager] 事件处理器注册完成！");
    }

    private void OnSyncGold()
    {
        if (!_isEnabled)
            return;

        Logger.Log("[NetServerManager] 收到金币同步请求");

        int currentGold = playerGold;
        Logger.Log($"[NetServerManager] 当前金币: {currentGold}");

        var goldData = new Dictionary<string, object>
        {
            { "gold", currentGold },
            { "add", 0 },
            { "reduce", 0 }
        };

        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
    }
}