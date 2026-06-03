// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 装备槽位类型枚举
/// </summary>
public enum EquipmentSlotType
{
    None = 0,
    Bait = 21,        // 鱼饵槽位（小分类ID 21）
    FishingRod = 31,  // 钓竿槽位（小分类ID 31）
    FishingLine = 32, // 钓线槽位（小分类ID 32）
    FishingHook = 33, // 钓钩槽位（小分类ID 33）
    Skill1 = 34,      // 技能槽位1（小分类ID 34）
    Skill2 = 35,      // 技能槽位2（小分类ID 35）
    Character = 36,   // 人物槽位（小分类ID 36）
    Decoration = 41   // 钓鱼场景装饰槽位（小分类ID 41）
}

/// <summary>
/// 玩家背包服务器管理器
/// 负责管理玩家的物品背包、鱼篓数据和装备系统
/// </summary>
public class PlayerInventoryServerManager
{
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();  // 玩家背包
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();    // 鱼篓数据
    private int fishBagCapacity = 20;  // 鱼篓容量

    // 装备系统 - 装备槽位到装备物品ID的映射
    private Dictionary<EquipmentSlotType, int> equippedItems = new Dictionary<EquipmentSlotType, int>();

    // 装备等级系统 - 装备物品ID到等级的映射
    private Dictionary<int, int> componentLevels = new Dictionary<int, int>();

    // 装备槽位名称映射
    private Dictionary<EquipmentSlotType, string> slotNames = new Dictionary<EquipmentSlotType, string>()
    {
        { EquipmentSlotType.Bait, "鱼饵" },
        { EquipmentSlotType.FishingRod, "钓竿" },
        { EquipmentSlotType.FishingLine, "钓线" },
        { EquipmentSlotType.FishingHook, "钓钩" },
        { EquipmentSlotType.Skill1, "技能1" },
        { EquipmentSlotType.Skill2, "技能2" },
        { EquipmentSlotType.Character, "人物" },
        { EquipmentSlotType.Decoration, "钓鱼场景装饰" }
    };

    /// <summary>
    /// 背包是否已初始化
    /// </summary>
    public bool IsInventoryInitialized { get; private set; }

    /// <summary>
    /// 初始化背包数据
    /// </summary>
    public void Initialize()
    {
        InitInventory();
        InitEquipmentSlots();
        EquipStarterGear();  // 装备新手初始物品
        RegisterAdEvents();   // 注册广告事件监听
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 背包服务器管理器初始化完成</color>");
    }

    /// <summary>
    /// 注册广告相关事件监听
    /// </summary>
    private void RegisterAdEvents()
    {
        CommunicateEvent.Register<int>("Skill_UnlockByAd", OnSkillUnlockByAd);
        CommunicateEvent.Register<int>("Skill_UpgradeByAd", OnSkillUpgradeByAd);
        CommunicateEvent.Register<int>("Skill_UpgradeByGold", OnSkillUpgradeByGold);
        CommunicateEvent.Register<int>("Equip_UpgradeByAd", OnEquipUpgradeByAd);
        CommunicateEvent.Register<int>("Equip_Unlock", OnEquipUnlock);
        CommunicateEvent.Register<int>("Equip_UpgradeByGold", OnEquipUpgradeByGold);
        CommunicateEvent.Register<int>("Skill_Unlock", OnSkillUnlock);

        // 注册View层发送的请求事件
        CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, OnEquipItemRequest);
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_EQUIP_BAIT, OnEquipBaitRequest);

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 广告事件监听器注册完成</color>");
    }

    /// <summary>
    /// 通过广告解锁技能
    /// </summary>
    private void OnSkillUnlockByAd(int skillId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlockByAd 开始 - skillId={0}</color>", skillId);

        if (skillObtainStatus == null)
        {
            Debug.LogErrorFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlockByAd - skillObtainStatus 为 null</color>");
            return;
        }

        SetSkillObtainStatus(skillId, FishingComponentObtainStatus.Obtained);
        SetComponentLevel(skillId, 1);

        // 验证结果
        var status = GetSkillObtainStatus(skillId);
        var level = GetComponentLevel(skillId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlockByAd 完成 - skillId={0}, status={1}, level={2}</color>", skillId, status, level);
    }

    /// <summary>
    /// 解锁技能
    /// </summary>
    private void OnSkillUnlock(int skillId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlock 开始 - skillId={0}</color>", skillId);

        if (skillObtainStatus == null)
        {
            Debug.LogErrorFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlock - skillObtainStatus 为 null</color>");
            return;
        }

        SetSkillObtainStatus(skillId, FishingComponentObtainStatus.Obtained);
        SetComponentLevel(skillId, 1);

        // 验证结果
        var status = GetSkillObtainStatus(skillId);
        var level = GetComponentLevel(skillId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUnlock 完成 - skillId={0}, status={1}, level={2}</color>", skillId, status, level);
    }

    /// <summary>
    /// 通过广告升级技能
    /// </summary>
    private void OnSkillUpgradeByAd(int skillId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByAd 开始 - skillId={0}</color>", skillId);

        // 检查技能是否已获取
        var status = GetSkillObtainStatus(skillId);
        if (status != FishingComponentObtainStatus.Obtained)
        {
            Debug.LogWarningFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByAd - 技能 {0} 未获取，无法升级, 当前状态={1}</color>", skillId, status);
            return;
        }

        int currentLevel = GetComponentLevel(skillId);
        SetComponentLevel(skillId, currentLevel + 1);

        // 验证结果
        var newLevel = GetComponentLevel(skillId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByAd 完成 - skillId={0}, 等级: {1} -> {2}</color>", skillId, currentLevel, newLevel);
    }

    /// <summary>
    /// 通过金币升级技能
    /// </summary>
    private void OnSkillUpgradeByGold(int skillId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByGold 开始 - skillId={0}</color>", skillId);

        var status = GetSkillObtainStatus(skillId);
        if (status != FishingComponentObtainStatus.Obtained)
        {
            Debug.LogWarningFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByGold - 技能 {0} 未获取，无法升级</color>", skillId);
            return;
        }

        int currentLevel = GetComponentLevel(skillId);
        int cost = currentLevel * 100;

        if (SimulationServer.Instance.Gold < cost)
        {
            Debug.LogWarningFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByGold - 金币不足, 需要={0}, 当前={1}</color>", cost, SimulationServer.Instance.Gold);
            return;
        }

        SimulationServer.Instance.DeductGold(cost);

        SetComponentLevel(skillId, currentLevel + 1);

        var newLevel = GetComponentLevel(skillId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnSkillUpgradeByGold 完成 - skillId={0}, 等级: {1} -> {2}, 消耗金币: {3}</color>", skillId, currentLevel, newLevel, cost);
    }

    /// <summary>
    /// 通过广告升级装备
    /// </summary>
    private void OnEquipUpgradeByAd(int equipId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUpgradeByAd 开始 - equipId={0}</color>", equipId);

        int currentLevel = GetComponentLevel(equipId);
        SetComponentLevel(equipId, currentLevel + 1);

        // 验证结果
        var newLevel = GetComponentLevel(equipId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUpgradeByAd 完成 - equipId={0}, 等级: {1} -> {2}</color>", equipId, currentLevel, newLevel);
    }

    /// <summary>
    /// 通过金币升级装备
    /// </summary>
    private void OnEquipUpgradeByGold(int equipId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUpgradeByGold 开始 - equipId={0}</color>", equipId);

        int currentLevel = GetComponentLevel(equipId);
        int cost = currentLevel * 50;

        // 检查是否已经满级
        if (currentLevel >= 10)
        {
            Debug.LogWarningFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUpgradeByGold - 已满级, equipId={0}</color>", equipId);
            return;
        }

        // 扣除金币
        bool success = SimulationServer.Instance.DeductGold(cost);
        if (!success)
        {
            return;
        }

        // 升级
        SetComponentLevel(equipId, currentLevel + 1);

        // 验证结果
        var newLevel = GetComponentLevel(equipId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUpgradeByGold 完成 - equipId={0}, 等级: {1} -> {2}, 花费金币: {3}</color>", equipId, currentLevel, newLevel, cost);
    }

    /// <summary>
    /// 通过广告解锁装备
    /// </summary>
    private void OnEquipUnlock(int equipId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUnlock 开始 - equipId={0}</color>", equipId);

        // 添加装备到背包
        playerInventory[equipId] = 1;  // 使用 [] 而不是 Add，确保覆盖
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUnlock - 已添加到背包, playerInventory[{0}] = 1</color>", equipId);

        // 设置装备等级为1
        SetComponentLevel(equipId, 1);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUnlock - 已设置等级, level=1</color>");

        // 如果是人物，设置获取状态（不自动装备，玩家需要点击装备按钮）
        if (equipId >= 3401 && equipId <= 3499)
        {
            Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUnlock - 检测到人物解锁, equipId={0}</color>", equipId);

            characterObtainStatus[equipId] = true;

            Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 人物 {0} 已解锁，等待玩家点击装备按钮</color>", equipId);
        }

        // 验证结果
        int count = playerInventory.ContainsKey(equipId) ? playerInventory[equipId] : 0;
        int level = GetComponentLevel(equipId);
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipUnlock 完成 - equipId={0}, 数量={1}, 等级={2}</color>", equipId, count, level);
    }

    /// <summary>
    /// 初始化装备槽位
    /// </summary>
    private void InitEquipmentSlots()
    {
        equippedItems.Clear();

        // 初始化所有装备槽位为空
        equippedItems[EquipmentSlotType.Bait] = 0;
        equippedItems[EquipmentSlotType.FishingRod] = 0;
        equippedItems[EquipmentSlotType.FishingLine] = 0;
        equippedItems[EquipmentSlotType.FishingHook] = 0;
        equippedItems[EquipmentSlotType.Skill1] = 0;
        equippedItems[EquipmentSlotType.Skill2] = 0;
        equippedItems[EquipmentSlotType.Character] = 0;
        equippedItems[EquipmentSlotType.Decoration] = 0;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 装备槽位初始化完成</color>");
    }

    /// <summary>
    /// 为新手玩家装备初始物品
    /// 默认装备：普通钓竿、普通钓线、普通钓钩
    /// 人物默认给一个，但设置为未装备状态（OwnerUnUse），玩家需要点击装备
    /// </summary>
    public void EquipStarterGear()
    {
        characterObtainStatus[3401] = true;

        EquipItem(EquipmentSlotType.FishingRod, 3001);
        EquipItem(EquipmentSlotType.FishingLine, 3101);
        EquipItem(EquipmentSlotType.FishingHook, 3201);
        EquipItem(EquipmentSlotType.Character, 3401);  // 装备新手人物

        SetComponentLevel(3001, 1);
        SetComponentLevel(3101, 1);
        SetComponentLevel(3201, 1);
        SetComponentLevel(3401, 1);

        if (CharacterServerManager.Instance != null)
        {
            CharacterServerManager.Instance.EquipCharacter(3401);
        }

        if (PlayerAniManager.Instance != null)
        {
            PlayerAniManager.Instance.SwitchCharacter(3401);
        }

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 新手初始装备已装备完成，人物3401已装备</color>");
    }

    /// <summary>
    /// 初始化玩家背包数据
    /// </summary>
    private void InitInventory()
    {
        StringBuilder logBuilder = new StringBuilder();

        // 清空现有数据
        playerInventory.Clear();
        fishInventory.Clear();

        // 添加测试数据 - 饵料（鱼饵2001-2499，窝料2501-2799）
        playerInventory.Add(2001, 3);   // 鱼饵1
        playerInventory.Add(2002, 8);   // 鱼饵2
        playerInventory.Add(2501, 5);   // 窝料1
        playerInventory.Add(2502, 2);   // 窝料2

        // 添加测试数据 - 装备（钓竿3001-3099，钓线3101-3199，钓钩3201-3299，技能3301-3399需要玩家获取）
        playerInventory.Add(3001, 1);   // 钓竿1
        playerInventory.Add(3101, 1);   // 钓线1
        playerInventory.Add(3201, 1);   // 钓钩1
        // 技能默认未解锁，玩家需要通过任务或商店获取
        playerInventory.Add(3401, 1);   // 人物1 - 初始人物（未装备状态）

        // 添加测试数据 - 装饰（钓鱼场景装饰4001-4299）
        playerInventory.Add(4001, 1);   // 钓鱼场景装饰1
        playerInventory.Add(4301, 1);   // 帐篷内装饰1
        playerInventory.Add(4501, 1);   // 鱼缸装饰1
        playerInventory.Add(4601, 1);   // 宠物屋装饰1

        // 添加测试数据 - 宠物（蛋类5001-5499，已孵化5501-5899）
        playerInventory.Add(5001, 2);   // 宠物蛋1

        // 添加测试数据 - 特殊（进阶材料6001-6299，其他物品6300-6999）
        playerInventory.Add(6001, 10);  // 进阶材料1
        playerInventory.Add(6301, 5);   // 其他物品1

        // 鱼篓数据
        fishInventory.Add(1001, 2);     // 鱼1
        fishInventory.Add(9001, 1);     // 垃圾

        logBuilder.AppendLine("[PlayerInventoryServerManager] 背包数据初始化完成:");
        foreach (var item in playerInventory)
        {
            logBuilder.AppendLine($"  背包物品ID: {item.Key}, 数量: {item.Value}");
        }

        logBuilder.AppendLine("[PlayerInventoryServerManager] 鱼篓数据初始化完成:");
        foreach (var item in fishInventory)
        {
            logBuilder.AppendLine($"  鱼篓物品ID: {item.Key}, 数量: {item.Value}");
        }

        Debug.Log(logBuilder.ToString());

        IsInventoryInitialized = true;
        RefreshInventoryData();
    }

    /// <summary>
    /// 刷新玩家数据到UI
    /// </summary>
    public void RefreshInventoryData()
    {
        if (ServerManager.Instance != null)
        {
            ServerManager.Instance.NotifySyncInventoryFromServer();
        }
    }

    // ==================== 背包物品管理 ====================

    /// <summary>
    /// 添加物品到背包
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    public void AddItem(int itemId, int quantity)
    {
        if (playerInventory.ContainsKey(itemId))
        {
            playerInventory[itemId] += quantity;
        }
        else
        {
            playerInventory[itemId] = quantity;
        }
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 添加物品: ID={0}, 数量={1}</color>", itemId, quantity);
        RefreshInventoryData();

        // 触发物品数量变更事件
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (itemId, playerInventory.ContainsKey(itemId) ? playerInventory[itemId] : 0));
    }

    /// <summary>
    /// 从背包移除物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    public void RemoveItem(int itemId, int quantity)
    {
        if (playerInventory.ContainsKey(itemId))
        {
            playerInventory[itemId] -= quantity;
            int remaining = playerInventory[itemId];
            if (remaining <= 0)
            {
                playerInventory.Remove(itemId);
                remaining = 0;
            }
            Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 移除物品: ID={0}, 数量={1}</color>", itemId, quantity);

            // 触发物品数量变更事件
            CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (itemId, remaining));
        }
    }

    /// <summary>
    /// 获取背包数据（副本）
    /// </summary>
    /// <returns>背包字典副本</returns>
    public Dictionary<int, int> GetInventory()
    {
        return new Dictionary<int, int>(playerInventory);
    }

    /// <summary>
    /// 获取物品数量
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>物品数量</returns>
    public int GetItemQuantity(int itemId)
    {
        return playerInventory.ContainsKey(itemId) ? playerInventory[itemId] : 0;
    }

    // ==================== 鱼篓管理 ====================

    /// <summary>
    /// 添加鱼到鱼篓
    /// </summary>
    /// <param name="itemId">鱼的物品ID</param>
    /// <param name="quantity">数量</param>
    public void AddFish(int itemId, int quantity)
    {
        if (fishInventory.ContainsKey(itemId))
        {
            fishInventory[itemId] += quantity;
        }
        else
        {
            fishInventory[itemId] = quantity;
        }
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 添加鱼: ID={0}, 数量={1}</color>", itemId, quantity);

        // 刷新UI显示
        RefreshInventoryData();

        // 通过ServerManager通知添加鱼事件
        if (ServerManager.Instance != null)
        {
            ServerManager.Instance.NotifyAddFish(itemId, quantity);
        }
    }

    /// <summary>
    /// 从鱼篓移除鱼
    /// </summary>
    /// <param name="itemId">鱼的物品ID</param>
    /// <param name="quantity">数量</param>
    public void RemoveFish(int itemId, int quantity)
    {
        if (fishInventory.ContainsKey(itemId))
        {
            fishInventory[itemId] -= quantity;
            if (fishInventory[itemId] <= 0)
            {
                fishInventory.Remove(itemId);
            }
            Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] 移除鱼: ID={0}, 数量={1}</color>", itemId, quantity);
        }
    }

    /// <summary>
    /// 获取鱼篓数据（副本）
    /// </summary>
    /// <returns>鱼篓字典副本</returns>
    public Dictionary<int, int> GetFishInventory()
    {
        return new Dictionary<int, int>(fishInventory);
    }

    /// <summary>
    /// 获取鱼的数量
    /// </summary>
    /// <param name="itemId">鱼的物品ID</param>
    /// <returns>鱼的数量</returns>
    public int GetFishQuantity(int itemId)
    {
        return fishInventory.ContainsKey(itemId) ? fishInventory[itemId] : 0;
    }

    // ==================== 技能状态管理 ====================

    /// <summary>
    /// 技能状态字典（技能ID -> 获取状态）
    /// </summary>
    private Dictionary<int, FishingComponentObtainStatus> skillObtainStatus = new Dictionary<int, FishingComponentObtainStatus>();

    /// <summary>
    /// 人物获取状态字典（人物ID -> 是否已获取）
    /// </summary>
    private Dictionary<int, bool> characterObtainStatus = new Dictionary<int, bool>();

    /// <summary>
    /// 技能装备状态字典（技能ID -> 装备状态）
    /// </summary>
    private Dictionary<int, FishingComponentEquipStatus> skillEquipStatus = new Dictionary<int, FishingComponentEquipStatus>();

    /// <summary>
    /// 设置技能的获取状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <param name="status">获取状态</param>
    public void SetSkillObtainStatus(int skillId, FishingComponentObtainStatus status)
    {
        skillObtainStatus[skillId] = status;

        // 如果设置为未获取，同时设置为未装备
        if (status == FishingComponentObtainStatus.Unobtained)
        {
            skillEquipStatus[skillId] = FishingComponentEquipStatus.Unequipped;
        }

        Debug.Log($"[PlayerInventoryServerManager] 设置技能获取状态: 技能ID={skillId}, 状态={status}");
    }

    /// <summary>
    /// 获取技能的获取状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>获取状态，默认未获取</returns>
    public FishingComponentObtainStatus GetSkillObtainStatus(int skillId)
    {
        if (skillObtainStatus.TryGetValue(skillId, out FishingComponentObtainStatus status))
        {
            return status;
        }
        return FishingComponentObtainStatus.Unobtained;
    }

    /// <summary>
    /// 设置技能的装备状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <param name="status">装备状态</param>
    /// <returns>是否设置成功（仅当技能已获取时成功）</returns>
    public bool SetSkillEquipStatus(int skillId, FishingComponentEquipStatus status)
    {
        // 检查技能是否已获取
        if (GetSkillObtainStatus(skillId) != FishingComponentObtainStatus.Obtained)
        {
            Debug.LogWarning($"[PlayerInventoryServerManager] 无法设置装备状态：技能ID={skillId} 未获取");
            return false;
        }

        skillEquipStatus[skillId] = status;
        Debug.Log($"[PlayerInventoryServerManager] 设置技能装备状态: 技能ID={skillId}, 状态={status}");
        return true;
    }

    /// <summary>
    /// 获取技能的装备状态
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>装备状态，默认未装备</returns>
    public FishingComponentEquipStatus GetSkillEquipStatus(int skillId)
    {
        if (skillEquipStatus.TryGetValue(skillId, out FishingComponentEquipStatus status))
        {
            return status;
        }
        return FishingComponentEquipStatus.Unequipped;
    }

    /// <summary>
    /// 检查技能是否已获取
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>是否已获取</returns>
    public bool IsSkillObtained(int skillId)
    {
        return GetSkillObtainStatus(skillId) == FishingComponentObtainStatus.Obtained;
    }

    /// <summary>
    /// 检查技能是否已装备
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>是否已装备（仅当技能已获取时有效）</returns>
    public bool IsSkillEquipped(int skillId)
    {
        return GetSkillObtainStatus(skillId) == FishingComponentObtainStatus.Obtained &&
               GetSkillEquipStatus(skillId) == FishingComponentEquipStatus.Equipped;
    }

    /// <summary>
    /// 检查人物是否已获取（解锁）
    /// </summary>
    /// <param name="characterId">人物ID</param>
    /// <returns>是否已获取</returns>
    public bool IsCharacterObtained(int characterId)
    {
        if (characterObtainStatus.TryGetValue(characterId, out bool obtained))
        {
            return obtained;
        }
        return false;
    }

    private bool CheckServerConnection()
    {
        if (ManagerManager.Instance != null && !ManagerManager.Instance.isOfflineMode)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 处理View层发送的装备物品请求
    /// </summary>
    /// <param name="request">(槽位类型, 物品ID)</param>
    private void OnEquipItemRequest((EquipmentSlotType, int) request)
    {
        if (!CheckServerConnection())
            return;

        var (slotType, itemId) = request;
        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipItemRequest - slot={0}, itemId={1}</color>", slotType, itemId);
        EquipItem(slotType, itemId);

        if (slotType == EquipmentSlotType.Character)
        {
            if (CharacterServerManager.Instance != null)
            {
                CharacterServerManager.Instance.EquipCharacter(itemId);
            }
            if (PlayerAniManager.Instance != null)
            {
                PlayerAniManager.Instance.SwitchCharacter(itemId);
            }
        }
    }

    /// <summary>
    /// 处理View层发送的装备鱼饵请求
    /// </summary>
    /// <param name="itemId">鱼饵ID</param>
    private void OnEquipBaitRequest(int itemId)
    {
        if (!CheckServerConnection())
            return;

        Debug.LogFormat("<color=orange>[PlayerInventoryServerManager] OnEquipBaitRequest - itemId={0}</color>", itemId);
        EquipItem(EquipmentSlotType.Bait, itemId);
    }

    /// <summary>
    /// 装备物品到指定槽位
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否装备成功</returns>
    public bool EquipItem(EquipmentSlotType slotType, int itemId)
    {
        // 检查是技能槽还是物品槽
        if (slotType == EquipmentSlotType.Skill1 || slotType == EquipmentSlotType.Skill2)
        {
            // 技能槽检查 skillObtainStatus
            if (!IsSkillObtained(itemId))
            {
                Debug.LogWarning($"[PlayerInventoryServerManager] 装备失败：没有获取过技能 ID={itemId}");
                return false;
            }
        }
        else
        {
            // 物品槽检查 playerInventory
            if (!playerInventory.ContainsKey(itemId) || playerInventory[itemId] <= 0)
            {
                Debug.LogWarning($"[PlayerInventoryServerManager] 装备失败：背包中没有物品 ID={itemId}");
                return false;
            }
        }

        // 获取当前装备的物品
        int currentEquippedItem = GetEquippedItem(slotType);

        // 如果槽位已有装备，先卸下
        if (currentEquippedItem > 0)
        {
            UnequipItem(slotType);
        }

        // 装备新物品
        equippedItems[slotType] = itemId;

        // 从背包移除一个物品（如果是物品，但鱼饵除外）
        // 鱼饵装备时不消耗数量，只在钓鱼成功时消耗
        if (slotType != EquipmentSlotType.Skill1 && slotType != EquipmentSlotType.Skill2 && slotType != EquipmentSlotType.Bait)
        {
            RemoveItem(itemId, 1);
        }

        Debug.Log($"[PlayerInventoryServerManager] 装备成功：{slotNames[slotType]} 槽位装备物品 ID={itemId}");
        RefreshInventoryData();

        // 触发装备变更事件
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, ((int)slotType, itemId));

        return true;
    }

    /// <summary>
    /// 从指定槽位卸下装备
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>是否卸下成功</returns>
    public bool UnequipItem(EquipmentSlotType slotType)
    {
        int equippedItemId = GetEquippedItem(slotType);

        if (equippedItemId == 0)
        {
            Debug.LogWarning($"[PlayerInventoryServerManager] 卸下失败：{slotNames[slotType]} 槽位没有装备");
            return false;
        }

        // 技能不需要放回背包，只需要清空槽位
        // 鱼饵也不需要放回背包，因为装备时没有消耗数量
        if (slotType != EquipmentSlotType.Skill1 && slotType != EquipmentSlotType.Skill2 && slotType != EquipmentSlotType.Bait)
        {
            // 将装备放回背包
            AddItem(equippedItemId, 1);
        }

        // 清空槽位
        equippedItems[slotType] = 0;

        Debug.Log($"[PlayerInventoryServerManager] 卸下成功：{slotNames[slotType]} 槽位卸下物品 ID={equippedItemId}");
        RefreshInventoryData();

        // 触发装备变更事件（槽位变为空）
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, ((int)slotType, 0));

        return true;
    }

    /// <summary>
    /// 获取指定槽位的装备物品ID
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>装备物品ID，0表示空槽位</returns>
    public int GetEquippedItem(EquipmentSlotType slotType)
    {
        if (equippedItems.TryGetValue(slotType, out int itemId))
        {
            return itemId;
        }
        return 0;
    }

    /// <summary>
    /// 获取所有装备槽位的数据
    /// </summary>
    /// <returns>槽位类型到装备物品ID的映射</returns>
    public Dictionary<EquipmentSlotType, int> GetAllEquippedItems()
    {
        return new Dictionary<EquipmentSlotType, int>(equippedItems);
    }

    /// <summary>
    /// 检查物品是否已装备
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>是否已装备</returns>
    public bool IsItemEquipped(int itemId)
    {
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value == itemId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取物品装备的槽位类型
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>装备槽位类型，None表示未装备</returns>
    public EquipmentSlotType GetItemEquippedSlot(int itemId)
    {
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value == itemId)
            {
                return kvp.Key;
            }
        }
        return EquipmentSlotType.None;
    }

    /// <summary>
    /// 获取装备槽位名称
    /// </summary>
    /// <param name="slotType">槽位类型</param>
    /// <returns>槽位名称</returns>
    public string GetSlotName(EquipmentSlotType slotType)
    {
        if (slotNames.TryGetValue(slotType, out string name))
        {
            return name;
        }
        return "未知槽位";
    }

    /// <summary>
    /// 根据物品ID获取适合的装备槽位
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>适合的槽位类型</returns>
    public EquipmentSlotType GetSlotTypeForItem(int itemId)
    {
        // 根据物品ID范围判断槽位类型
        if (itemId >= 2001 && itemId <= 2499)
        {
            return EquipmentSlotType.Bait;      // 鱼饵
        }
        else if (itemId >= 3001 && itemId <= 3099)
        {
            return EquipmentSlotType.FishingRod; // 钓竿
        }
        else if (itemId >= 3101 && itemId <= 3199)
        {
            return EquipmentSlotType.FishingLine; // 钓线
        }
        else if (itemId >= 3201 && itemId <= 3299)
        {
            return EquipmentSlotType.FishingHook; // 钓钩
        }
        else if (itemId >= 3301 && itemId <= 3399)
        {
            // 技能物品可以装备到任意技能槽位
            // 这里返回第一个可用的技能槽位
            if (equippedItems[EquipmentSlotType.Skill1] == 0)
                return EquipmentSlotType.Skill1;
            else if (equippedItems[EquipmentSlotType.Skill2] == 0)
                return EquipmentSlotType.Skill2;
            // 如果两个槽位都满了，返回第一个槽位（会替换已有装备）
            return EquipmentSlotType.Skill1;
        }
        else if (itemId >= 3401 && itemId <= 3499)
        {
            return EquipmentSlotType.Character;   // 人物
        }
        else if (itemId >= 4001 && itemId <= 4299)
        {
            return EquipmentSlotType.Decoration;  // 钓鱼场景装饰
        }

        return EquipmentSlotType.None;
    }

    // ==================== 数据统计 ====================

    /// <summary>
    /// 获取背包物品数量
    /// </summary>
    public int InventoryCount => playerInventory.Count;

    /// <summary>
    /// 获取鱼篓物品数量
    /// </summary>
    public int FishCount => fishInventory.Count;

    /// <summary>
    /// 获取背包物品数量（方法形式）
    /// </summary>
    public int GetInventoryCount()
    {
        return playerInventory.Count;
    }

    /// <summary>
    /// 获取鱼篓物品数量（方法形式）
    /// </summary>
    public int GetFishCount()
    {
        return fishInventory.Count;
    }

    /// <summary>
    /// 获取鱼篓容量
    /// </summary>
    public int FishBagCapacity => fishBagCapacity;

    /// <summary>
    /// 设置鱼篓容量
    /// </summary>
    /// <param name="capacity">新的容量</param>
    public void SetFishBagCapacity(int capacity)
    {
        fishBagCapacity = Mathf.Max(1, capacity);
        Debug.Log($"[PlayerInventoryServerManager] 鱼篓容量已设置为: {fishBagCapacity}");
    }

    /// <summary>
    /// 检查鱼篓是否已满
    /// </summary>
    /// <returns>鱼篓是否已满</returns>
    public bool IsFishBagFull()
    {
        return GetTotalFishCount() >= fishBagCapacity;
    }

    /// <summary>
    /// 获取鱼篓剩余空间
    /// </summary>
    /// <returns>剩余空间数量</returns>
    public int GetFishBagRemainingSpace()
    {
        return fishBagCapacity - GetTotalFishCount();
    }

    /// <summary>
    /// 获取鱼篓中物品的总数量（堆叠数量之和）
    /// </summary>
    /// <returns>总数量</returns>
    public int GetTotalFishCount()
    {
        int total = 0;
        foreach (var kvp in fishInventory)
        {
            total += kvp.Value;
        }
        return total;
    }

    /// <summary>
    /// 显示背包数据
    /// </summary>
    public void ShowInventory()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[PlayerInventoryServerManager] 背包数据:");
        foreach (var item in playerInventory)
        {
            logBuilder.AppendLine($"  物品ID: {item.Key}, 数量: {item.Value}");
        }
        Debug.Log(logBuilder.ToString());
    }

    /// <summary>
    /// 显示装备数据
    /// </summary>
    public void ShowEquipment()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[PlayerInventoryServerManager] 装备数据:");
        foreach (var kvp in equippedItems)
        {
            logBuilder.AppendLine($"  {slotNames[kvp.Key]}: {kvp.Value}");
        }
        Debug.Log(logBuilder.ToString());
    }

    /// <summary>
    /// 获取组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>组件等级，默认1级</returns>
    public int GetComponentLevel(int componentId)
    {
        if (componentLevels.TryGetValue(componentId, out int level))
        {
            return level;
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
        if (level <= 0) level = 1;
        componentLevels[componentId] = level;
    }

    /// <summary>
    /// 增加组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="delta">增加量</param>
    /// <returns>新的等级</returns>
    public int AddComponentLevel(int componentId, int delta = 1)
    {
        int currentLevel = GetComponentLevel(componentId);
        int newLevel = currentLevel + delta;
        SetComponentLevel(componentId, newLevel);
        return newLevel;
    }

    /// <summary>
    /// 获取所有组件等级数据
    /// </summary>
    /// <returns>组件ID到等级的字典副本</returns>
    public Dictionary<int, int> GetAllComponentLevels()
    {
        return new Dictionary<int, int>(componentLevels);
    }
}
*/