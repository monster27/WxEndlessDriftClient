// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SimulationServer 背包和商城相关方法部分
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region 背包代理方法

    /// <summary>
    /// 获取玩家背包
    /// </summary>
    /// <returns>背包字典</returns>
    public Dictionary<int, int> GetInventory()
    {
        return inventoryManager?.GetInventory() ?? new Dictionary<int, int>();
    }

    /// <summary>
    /// 获取鱼篓数据
    /// </summary>
    /// <returns>鱼篓字典</returns>
    public Dictionary<int, int> GetFishInventory()
    {
        return inventoryManager?.GetFishInventory() ?? new Dictionary<int, int>();
    }

    /// <summary>
    /// 添加物品到背包
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    public void AddItem(int itemId, int quantity)
    {
        inventoryManager?.AddItem(itemId, quantity);
    }

    /// <summary>
    /// 从背包移除物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    public void RemoveItem(int itemId, int quantity)
    {
        inventoryManager?.RemoveItem(itemId, quantity);
    }

    /// <summary>
    /// 添加鱼到鱼篓
    /// </summary>
    /// <param name="itemId">鱼的物品ID</param>
    /// <param name="quantity">数量</param>
    public void AddFish(int itemId, int quantity)
    {
        if (IsFishBagFull())
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 鱼篓已满，无法添加新鱼</color>");
            ServerManager.Instance?.NotifyPlayLazyAnimation();
            return;
        }

        inventoryManager?.AddFish(itemId, quantity);
        MarkAsNewlyCaughtFish(itemId);

        if (IsFishBagFull())
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 鱼篓已满，设置玩家状态为Lazy</color>");
            ServerManager.Instance?.NotifyPlayLazyAnimation();
        }
    }

    /// <summary>
    /// 从鱼篓移除鱼
    /// </summary>
    /// <param name="itemId">鱼的物品ID</param>
    /// <param name="quantity">数量</param>
    public void RemoveFish(int itemId, int quantity)
    {
        inventoryManager?.RemoveFish(itemId, quantity);
    }

    /// <summary>
    /// 获取鱼篓容量
    /// </summary>
    /// <returns>鱼篓容量</returns>
    public int GetFishBagCapacity()
    {
        return inventoryManager?.FishBagCapacity ?? 20;
    }

    /// <summary>
    /// 设置鱼篓容量
    /// </summary>
    /// <param name="capacity">新的容量</param>
    public void SetFishBagCapacity(int capacity)
    {
        inventoryManager?.SetFishBagCapacity(capacity);
    }

    /// <summary>
    /// 获取鱼篓剩余空间
    /// </summary>
    /// <returns>剩余空间数量</returns>
    public int GetFishBagRemainingSpace()
    {
        return inventoryManager?.GetFishBagRemainingSpace() ?? 0;
    }

    /// <summary>
    /// 获取鱼篓中物品的总数量（堆叠数量之和）
    /// </summary>
    /// <returns>总数量</returns>
    public int GetTotalFishCount()
    {
        return inventoryManager?.GetTotalFishCount() ?? 0;
    }

    #endregion

    #region 装备系统相关方法

    /// <summary>
    /// 装备物品到指定槽位
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否装备成功</returns>
    public bool EquipItem(EquipmentSlotType slotType, int itemId)
    {
        return inventoryManager?.EquipItem(slotType, itemId) ?? false;
    }

    /// <summary>
    /// 装备饵料到鱼饵槽位
    /// </summary>
    /// <param name="itemId">饵料物品ID</param>
    /// <returns>是否装备成功</returns>
    public bool EquipBait(int itemId)
    {
        return EquipItem(EquipmentSlotType.Bait, itemId);
    }

    /// <summary>
    /// 从指定槽位卸下装备
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>是否卸下成功</returns>
    public bool UnequipItem(EquipmentSlotType slotType)
    {
        return inventoryManager?.UnequipItem(slotType) ?? false;
    }

    /// <summary>
    /// 获取指定槽位的装备物品ID
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>装备物品ID，0表示空槽位</returns>
    public int GetEquippedItem(EquipmentSlotType slotType)
    {
        return inventoryManager?.GetEquippedItem(slotType) ?? 0;
    }

    /// <summary>
    /// 获取所有装备槽位的数据
    /// </summary>
    /// <returns>槽位类型到装备物品ID的映射</returns>
    public Dictionary<EquipmentSlotType, int> GetAllEquippedItems()
    {
        return inventoryManager?.GetAllEquippedItems() ?? new Dictionary<EquipmentSlotType, int>();
    }

    /// <summary>
    /// 检查物品是否已装备
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否已装备</returns>
    public bool IsItemEquipped(int itemId)
    {
        return inventoryManager?.IsItemEquipped(itemId) ?? false;
    }

    /// <summary>
    /// 获取物品装备的槽位类型
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>装备槽位类型，None表示未装备</returns>
    public EquipmentSlotType GetItemEquippedSlot(int itemId)
    {
        return inventoryManager?.GetItemEquippedSlot(itemId) ?? EquipmentSlotType.None;
    }

    /// <summary>
    /// 获取装备槽位名称
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>槽位名称</returns>
    public string GetSlotName(EquipmentSlotType slotType)
    {
        return inventoryManager?.GetSlotName(slotType) ?? "未知槽位";
    }

    /// <summary>
    /// 根据物品ID获取适合的装备槽位
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>适合的槽位类型</returns>
    public EquipmentSlotType GetSlotTypeForItem(int itemId)
    {
        return inventoryManager?.GetSlotTypeForItem(itemId) ?? EquipmentSlotType.None;
    }

    /// <summary>
    /// 显示装备数据
    /// </summary>
    public void ShowEquipment()
    {
        inventoryManager?.ShowEquipment();
    }

    /// <summary>
    /// 获取组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>组件等级</returns>
    public int GetComponentLevel(int componentId)
    {
        return inventoryManager?.GetComponentLevel(componentId) ?? 0;
    }

    /// <summary>
    /// 获取当前人物等级
    /// </summary>
    /// <returns>人物等级</returns>
    public int GetCharacterLevel()
    {
        if (CharacterServerManager.Instance != null)
        {
            var charData = CharacterServerManager.Instance.GetPlayerCharacterData();
            if (charData != null)
            {
                return charData.currentLevel;
            }
        }
        return 1;
    }

    /// <summary>
    /// 设置组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="level">等级</param>
    public void SetComponentLevel(int componentId, int level)
    {
        inventoryManager?.SetComponentLevel(componentId, level);
    }

    #endregion

    #region 技能状态管理方法

    /// <summary>
    /// 检查技能是否已获取
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>是否已获取</returns>
    public bool IsSkillObtained(int skillId)
    {
        return inventoryManager?.IsSkillObtained(skillId) ?? false;
    }
    
    /// <summary>
    /// 检查人物是否已获取（解锁）
    /// </summary>
    /// <param name="characterId">人物ID</param>
    /// <returns>是否已获取</returns>
    public bool IsCharacterObtained(int characterId)
    {
        return inventoryManager?.IsCharacterObtained(characterId) ?? false;
    }

    /// <summary>
    /// 设置技能的获取状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <param name="status">获取状态</param>
    public void SetSkillObtainStatus(int skillId, FishingComponentObtainStatus status)
    {
        inventoryManager?.SetSkillObtainStatus(skillId, status);
    }

    /// <summary>
    /// 获取技能的获取状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>获取状态</returns>
    public FishingComponentObtainStatus GetSkillObtainStatus(int skillId)
    {
        return inventoryManager?.GetSkillObtainStatus(skillId) ?? FishingComponentObtainStatus.Unobtained;
    }

    /// <summary>
    /// 获取所有技能获取状态的字典副本
    /// </summary>
    /// <returns>技能ID到获取状态的字典</returns>
    public Dictionary<int, FishingComponentObtainStatus> GetAllSkillObtainStatus()
    {
        var result = new Dictionary<int, FishingComponentObtainStatus>();
        if (inventoryManager == null) return result;

        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var component in config.items)
            {
                if (component.category == FishingComponentCategory.Skill)
                {
                    result[component.id] = inventoryManager.GetSkillObtainStatus(component.id);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 解锁所有技能（测试用）
    /// </summary>
    public void UnlockAllSkills()
    {
        if (inventoryManager == null) return;

        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var component in config.items)
            {
                if (component.category == FishingComponentCategory.Skill)
                {
                    inventoryManager.SetSkillObtainStatus(component.id, FishingComponentObtainStatus.Obtained);
                    inventoryManager.SetComponentLevel(component.id, 1);
                }
            }
        }
        Debug.LogFormat("<color=orange>[SimulationServer] 已解锁所有技能</color>");
    }

    #endregion

    #region 商城相关方法

    /// <summary>
    /// 获取商城物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>商城物品数据</returns>
    public MallItemData GetMallItem(int itemId)
    {
        return mallManager?.GetMallItem(itemId);
    }

    /// <summary>
    /// 获取所有商城物品
    /// </summary>
    /// <returns>商城物品字典</returns>
    public Dictionary<int, MallItemData> GetMallItems()
    {
        return mallManager?.GetAllMallItems() ?? new Dictionary<int, MallItemData>();
    }

    /// <summary>
    /// 购买商城物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    /// <returns>是否购买成功</returns>
    public bool PurchaseMallItem(int itemId, int quantity)
    {
        if (mallManager == null)
            return false;

        string message;
        bool success = mallManager.PurchaseItem(itemId, quantity, Gold, out message);
        
        if (success)
        {
            int totalPrice = mallManager.GetMallItem(itemId).price * quantity;
            DeductGold(totalPrice);
            AddItem(itemId, quantity);
            Debug.LogFormat("<color=orange>[SimulationServer] 购买成功: 物品ID={0}, 数量={1}, 花费金币={2}</color>", itemId, quantity, totalPrice);
        }
        else
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 购买失败: {0}</color>", message);
        }
        
        return success;
    }

    /// <summary>
    /// 添加金币
    /// </summary>
    /// <param name="amount">金币数量</param>
    public void AddGold(int amount)
    {
        Gold += amount;
        Debug.LogFormat("<color=orange>[SimulationServer] 金币增加: {0}, 当前金币: {1}</color>", amount, Gold);
        
        Dictionary<string, object> goldData = new Dictionary<string, object>
        {
            { "gold", Gold },
            { "addedAmount", amount }
        };
        CommunicateEvent.Modify(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
    }

    /// <summary>
    /// 扣除金币
    /// </summary>
    /// <param name="amount">金币数量</param>
    public bool DeductGold(int amount)
    {
        if (Gold < amount)
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] DeductGold - 金币不足, 当前: {0}, 需要: {1}</color>", Gold, amount);
            return false;
        }
        Gold -= amount;
        Debug.LogFormat("<color=orange>[SimulationServer] DeductGold - 扣除 {0} 金币, 现在: {1}</color>", amount, Gold);
        
        Dictionary<string, object> goldData = new Dictionary<string, object>
        {
            { "gold", Gold },
            { "deductedAmount", amount }
        };
        CommunicateEvent.Modify(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
        return true;
    }

    #endregion
}
*/