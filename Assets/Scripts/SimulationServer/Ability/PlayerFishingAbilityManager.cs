using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;

/// <summary>
/// 技能数据管理器
/// 用于加载和管理技能配置数据
/// </summary>
public static class SkillDataManager
{
    private static SkillListWrapper skillListWrapper;
    private static AbilityListWrapper abilityListWrapper;
    
    /// <summary>
    /// 加载所有技能数据
    /// </summary>
    public static void LoadSkillData()
    {
        // 加载基础技能数据
        TextAsset abilityText = Resources.Load<TextAsset>("JsonData/Ability/abilities");
        if (abilityText != null)
        {
            abilityListWrapper = JsonUtility.FromJson<AbilityListWrapper>(abilityText.text);
        }
        
        // 加载挂载技能数据
        TextAsset skillText = Resources.Load<TextAsset>("JsonData/Ability/skills");
        if (skillText != null)
        {
            skillListWrapper = JsonUtility.FromJson<SkillListWrapper>(skillText.text);
        }
    }
    
    /// <summary>
    /// 通过完整钓鱼技能ID获取对应的单一钓鱼技能列表
    /// </summary>
    /// <param name="skillId">完整钓鱼技能ID</param>
    /// <returns>单一钓鱼技能列表</returns>
    public static List<AbilityData> GetAbilitiesBySkillId(int skillId)
    {
        if (skillListWrapper == null || abilityListWrapper == null)
        {
            LoadSkillData();
        }
        
        var skillData = skillListWrapper.skills.Find(s => s.id == skillId);
        if (skillData == null)
        {
            return null;
        }
        
        List<AbilityData> abilities = new List<AbilityData>();
        foreach (int abilityId in skillData.abilityIds)
        {
            var ability = abilityListWrapper.abilities.Find(a => a.id == abilityId);
            if (ability != null)
            {
                abilities.Add(ability);
            }
        }
        
        return abilities;
    }
    
    /// <summary>
    /// 通过单一钓鱼技能ID获取对应的技能数据
    /// </summary>
    /// <param name="abilityId">单一钓鱼技能ID</param>
    /// <returns>单一钓鱼技能数据</returns>
    public static AbilityData GetAbilityById(int abilityId)
    {
        if (abilityListWrapper == null)
        {
            LoadSkillData();
        }
        
        return abilityListWrapper.abilities.Find(a => a.id == abilityId);
    }
}

/// <summary>
/// 玩家钓鱼能力管理器（新版）
/// 管理玩家的钓鱼组件装备，支持四种类别：钓竿、钓线、钓钩、技能
/// 每个组件支持等级配置，玩家装备时记录当前等级
/// 支持通过参数ID灵活配置效果
/// </summary>
public class PlayerFishingAbilityManager
{
    /// <summary>
    /// 玩家ID
    /// </summary>
    public int PlayerId { get; private set; }
    
    /// <summary>
    /// 装备的组件列表
    /// </summary>
    private List<PlayerFishingComponent> equippedComponents = new List<PlayerFishingComponent>();
    
    /// <summary>
    /// 基础属性
    /// </summary>
    private FishingBaseStats baseStats = new FishingBaseStats();
    
    /// <summary>
    /// 计算后的属性（应用所有组件后的最终属性）
    /// </summary>
    private FishingCalculatedStats calculatedStats = new FishingCalculatedStats();
    
    /// <summary>
    /// 能力池：存储所有单一能力类型(paramId)和对应的累加值
    /// key: paramId (单一能力类型ID)
    /// value: 累加后的能力值
    /// </summary>
    private Dictionary<int, float> abilityPool = new Dictionary<int, float>();
    
    /// <summary>
    /// 钓鱼组件配置缓存（从fishing_components.json加载）
    /// </summary>
    private static CompleteFishingSkillConfig componentConfigCache;
    
    /// <summary>
    /// 获取能力池副本（只读）
    /// </summary>
    public Dictionary<int, float> AbilityPool => new Dictionary<int, float>(abilityPool);
    
    /// <summary>
    /// 获取基础属性（只读）
    /// </summary>
    public FishingBaseStats BaseStats => baseStats;
    
    /// <summary>
    /// 获取计算后的属性（只读）
    /// </summary>
    public FishingCalculatedStats CalculatedStats => calculatedStats;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="playerId">玩家ID</param>
    public PlayerFishingAbilityManager(int playerId)
    {
        PlayerId = playerId;
        
        // 预加载技能数据
        SkillDataManager.LoadSkillData();
    }
    
    /// <summary>
    /// 加载钓鱼组件配置
    /// </summary>
    private void LoadComponentConfig()
    {
        if (componentConfigCache == null)
        {
            componentConfigCache = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
            Debug.Log("[PlayerFishingAbilityManager] 钓鱼组件配置加载完成");
        }
    }
    
    /// <summary>
    /// 从玩家背包更新能力池（统一入口）
    /// 检索钓竿、钓线、钓钩、鱼饵、技能，解析所有单一能力类型和数值到池子中
    /// </summary>
    /// <param name="inventory">玩家背包管理器</param>
    public void UpdateAbilityPoolFromInventory(PlayerInventoryServerManager inventory)
    {
        if (inventory == null)
        {
            Debug.LogWarning("[PlayerFishingAbilityManager] 背包管理器为空，无法更新能力池");
            return;
        }
        
        // 清空能力池
        abilityPool.Clear();
        
        // 确保组件配置已加载
        LoadComponentConfig();
        
        // 1. 检索钓竿、钓线、钓钩
        UpdateGearAbilitiesFromInventory(inventory);
        
        // 2. 检索鱼饵
        UpdateBaitAbilitiesFromInventory(inventory);
        
        // 3. 检索技能
        UpdateSkillAbilitiesFromInventory(inventory);
        
        Debug.Log($"[PlayerFishingAbilityManager] 能力池更新完成，共 {abilityPool.Count} 个能力");
    }
    
    /// <summary>
    /// 从背包检索钓竿、钓线、钓钩的能力
    /// </summary>
    private void UpdateGearAbilitiesFromInventory(PlayerInventoryServerManager inventory)
    {
        // 获取钓竿
        int rodId = inventory.GetEquippedItem(EquipmentSlotType.FishingRod);
        if (rodId > 0)
        {
            int rodLevel = inventory.GetComponentLevel(rodId);
            AddComponentAbilitiesToPool(rodId, rodLevel);
            Debug.Log($"[能力池] 装备钓竿 ID:{rodId} Lv.{rodLevel}");
        }
        
        // 获取钓线
        int lineId = inventory.GetEquippedItem(EquipmentSlotType.FishingLine);
        if (lineId > 0)
        {
            int lineLevel = inventory.GetComponentLevel(lineId);
            AddComponentAbilitiesToPool(lineId, lineLevel);
            Debug.Log($"[能力池] 装备钓线 ID:{lineId} Lv.{lineLevel}");
        }
        
        // 获取钓钩
        int hookId = inventory.GetEquippedItem(EquipmentSlotType.FishingHook);
        if (hookId > 0)
        {
            int hookLevel = inventory.GetComponentLevel(hookId);
            AddComponentAbilitiesToPool(hookId, hookLevel);
            Debug.Log($"[能力池] 装备钓钩 ID:{hookId} Lv.{hookLevel}");
        }
    }
    
    /// <summary>
    /// 从背包检索鱼饵的能力
    /// </summary>
    private void UpdateBaitAbilitiesFromInventory(PlayerInventoryServerManager inventory)
    {
        int baitId = inventory.GetEquippedItem(EquipmentSlotType.Bait);
        if (baitId > 0)
        {
            int baitLevel = inventory.GetComponentLevel(baitId);
            AddComponentAbilitiesToPool(baitId, baitLevel);
            Debug.Log($"[能力池] 装备鱼饵 ID:{baitId} Lv.{baitLevel}");
        }
    }
    
    /// <summary>
    /// 从背包检索技能的能力
    /// </summary>
    private void UpdateSkillAbilitiesFromInventory(PlayerInventoryServerManager inventory)
    {
        // 获取技能槽1
        int skill1Id = inventory.GetEquippedItem(EquipmentSlotType.Skill1);
        if (skill1Id > 0)
        {
            int skill1Level = inventory.GetComponentLevel(skill1Id);
            AddComponentAbilitiesToPool(skill1Id, skill1Level);
            Debug.Log($"[能力池] 装备技能1 ID:{skill1Id} Lv.{skill1Level}");
        }
        
        // 获取技能槽2
        int skill2Id = inventory.GetEquippedItem(EquipmentSlotType.Skill2);
        if (skill2Id > 0)
        {
            int skill2Level = inventory.GetComponentLevel(skill2Id);
            AddComponentAbilitiesToPool(skill2Id, skill2Level);
            Debug.Log($"[能力池] 装备技能2 ID:{skill2Id} Lv.{skill2Level}");
        }
    }
    
    /// <summary>
    /// 将组件的所有能力添加到能力池（累加同名能力）
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="level">组件等级</param>
    private void AddComponentAbilitiesToPool(int componentId, int level)
    {
        if (componentConfigCache == null) return;
        
        var config = componentConfigCache.GetComponentById(componentId);
        if (config == null)
        {
            Debug.LogWarning($"[能力池] 未找到组件配置 ID:{componentId}");
            return;
        }
        
        var levelData = config.GetLevelData(level);
        if (levelData == null || levelData.paramsList == null)
        {
            Debug.LogWarning($"[能力池] 组件 ID:{componentId} 等级 {level} 无效");
            return;
        }
        
        foreach (var param in levelData.paramsList)
        {
            if (param.paramId == 0) continue;
            
            if (abilityPool.ContainsKey(param.paramId))
            {
                abilityPool[param.paramId] += param.value;
            }
            else
            {
                abilityPool[param.paramId] = param.value;
            }
        }
    }
    
    /// <summary>
    /// 获取能力池中指定能力类型的值
    /// </summary>
    /// <param name="paramId">能力类型ID</param>
    /// <returns>能力值，如果不存在返回0</returns>
    public float GetAbilityValue(int paramId)
    {
        if (abilityPool.TryGetValue(paramId, out float value))
        {
            return value;
        }
        return 0f;
    }
    
    /// <summary>
    /// 通过完整钓鱼技能ID获取对应的单一钓鱼技能列表
    /// </summary>
    /// <param name="skillId">完整钓鱼技能ID</param>
    /// <returns>单一钓鱼技能列表</returns>
    public List<AbilityData> GetAbilitiesBySkillId(int skillId)
    {
        return SkillDataManager.GetAbilitiesBySkillId(skillId);
    }
    
    /// <summary>
    /// 通过单一钓鱼技能ID获取对应的技能数据
    /// </summary>
    /// <param name="abilityId">单一钓鱼技能ID</param>
    /// <returns>单一钓鱼技能数据</returns>
    public AbilityData GetAbilityById(int abilityId)
    {
        return SkillDataManager.GetAbilityById(abilityId);
    }
    
    /// <summary>
    /// 获取装备组件对应的单一钓鱼技能列表
    /// </summary>
    /// <param name="component">装备的组件</param>
    /// <returns>单一钓鱼技能列表</returns>
    public List<AbilityData> GetAbilitiesForComponent(PlayerFishingComponent component)
    {
        if (component == null) return null;
        
        // 通过组件ID获取对应的单一钓鱼技能
        return GetAbilitiesBySkillId(component.Id);
    }
    
    /// <summary>
    /// 添加能力组件（兼容旧接口）
    /// 根据能力名称自动匹配对应的参数ID
    /// </summary>
    /// <param name="ability">能力组件</param>
    public void AddAbility(FishingAbilityBase ability)
    {
        var config = new FishingComponentConfig
        {
            id = ability.Id,
            name = ability.Name,
            description = ability.Description,
            iconPath = "",
            maxLevel = 1,
            isPassive = ability.Duration == 0,
            cooldownTime = ability.Duration > 0 ? ability.Duration * 2 : 0f,
            duration = ability.Duration,
            levelDataList = new System.Collections.Generic.List<FishingComponentLevelData>()
        };

        // 根据能力名称确定类别和参数ID
        var paramList = new System.Collections.Generic.List<FishingComponentParam>();
        
        if (ability.Name.Contains("钓竿") || ability.Name.Contains("Rod"))
        {
            config.category = FishingComponentCategory.Rod;
            paramList.Add(new FishingComponentParam { paramId = 2002, value = ability.Value });
        }
        else if (ability.Name.Contains("钓线") || ability.Name.Contains("Line"))
        {
            config.category = FishingComponentCategory.Line;
            paramList.Add(new FishingComponentParam { paramId = 6001, value = ability.Value });
        }
        else if (ability.Name.Contains("钓钩") || ability.Name.Contains("Hook"))
        {
            config.category = FishingComponentCategory.Hook;
            paramList.Add(new FishingComponentParam { paramId = 3001, value = ability.Value });
        }
        else if (ability.Name.Contains("闪光"))
        {
            config.category = FishingComponentCategory.Skill;
            paramList.Add(new FishingComponentParam { paramId = 7001, value = ability.Value });
        }
        else if (ability.Name.Contains("稀有度"))
        {
            config.category = FishingComponentCategory.Skill;
            paramList.Add(new FishingComponentParam { paramId = 8010, value = ability.Value });
        }
        else if (ability.Name.Contains("挣扎"))
        {
            config.category = FishingComponentCategory.Rod;
            paramList.Add(new FishingComponentParam { paramId = 5001, value = ability.Value });
        }
        else if (ability.Name.Contains("重量"))
        {
            config.category = FishingComponentCategory.Line;
            paramList.Add(new FishingComponentParam { paramId = 4001, value = ability.Value });
        }
        else if (ability.Name.Contains("小游戏"))
        {
            config.category = FishingComponentCategory.Line;
            paramList.Add(new FishingComponentParam { paramId = 6001, value = ability.Value });
        }
        else if (ability.Name.Contains("咬钩"))
        {
            config.category = FishingComponentCategory.Hook;
            paramList.Add(new FishingComponentParam { paramId = 9001, value = ability.Value });
        }
        else if (ability.Name.Contains("保底") || ability.Name.Contains("Trash"))
        {
            config.category = FishingComponentCategory.Hook;
            paramList.Add(new FishingComponentParam { paramId = 3003, value = ability.Value });
        }
        else
        {
            config.category = FishingComponentCategory.Skill;
            paramList.Add(new FishingComponentParam { paramId = 1001, value = ability.Value });
        }

        config.levelDataList.Add(new FishingComponentLevelData
        {
            level = 1,
            paramsList = paramList
        });

        EquipComponent(config, 1);
    }
    
    /// <summary>
    /// 装备组件
    /// </summary>
    /// <param name="config">组件配置</param>
    /// <param name="level">装备等级</param>
    /// <returns>装备的组件实例</returns>
    public PlayerFishingComponent EquipComponent(FishingComponentConfig config, int level = 1)
    {
        // 检查是否已装备同类型组件（钓竿、钓线、钓钩每个类别只能装备一个）
        if (config.category != FishingComponentCategory.Skill)
        {
            var existingComponent = GetEquippedComponentByCategory(config.category);
            if (existingComponent != null)
            {
                Debug.LogWarning($"[玩家{PlayerId}] 已装备同类型组件: {existingComponent.Name}，将替换为: {config.name}");
                UnequipComponent(existingComponent);
            }
        }
        
        var component = new PlayerFishingComponent(config, level);
        equippedComponents.Add(component);
        
        Debug.Log($"[玩家{PlayerId}] 装备组件: {component.GetComponentInfo()}");
        return component;
    }
    
    /// <summary>
    /// 卸下组件
    /// </summary>
    /// <param name="component">要卸下的组件</param>
    /// <returns>是否卸下成功</returns>
    public bool UnequipComponent(PlayerFishingComponent component)
    {
        if (equippedComponents.Remove(component))
        {
            Debug.Log($"[玩家{PlayerId}] 卸下组件: {component.Name} (Lv.{component.CurrentLevel})");
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 根据ID卸下组件
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>是否卸下成功</returns>
    public bool UnequipComponentById(int componentId)
    {
        var component = GetComponentById(componentId);
        if (component != null)
        {
            return UnequipComponent(component);
        }
        return false;
    }
    
    /// <summary>
    /// 根据ID获取组件
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>组件实例，如果未找到返回null</returns>
    public PlayerFishingComponent GetComponentById(int componentId)
    {
        return equippedComponents.Find(c => c.Id == componentId);
    }
    
    /// <summary>
    /// 根据类别获取装备的组件
    /// </summary>
    /// <param name="category">组件类别</param>
    /// <returns>该类别的组件，如果未装备返回null</returns>
    public PlayerFishingComponent GetEquippedComponentByCategory(FishingComponentCategory category)
    {
        return equippedComponents.FirstOrDefault(c => c.Category == category);
    }
    
    /// <summary>
    /// 获取某类别的所有组件（主要用于技能，允许多个）
    /// </summary>
    /// <param name="category">组件类别</param>
    /// <returns>该类别的所有组件列表</returns>
    public List<PlayerFishingComponent> GetComponentsByCategory(FishingComponentCategory category)
    {
        return equippedComponents.FindAll(c => c.Category == category);
    }
    
    /// <summary>
    /// 升级组件
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="newLevel">新等级</param>
    /// <returns>是否升级成功</returns>
    public bool UpgradeComponent(int componentId, int newLevel)
    {
        var component = GetComponentById(componentId);
        if (component != null)
        {
            bool success = component.Upgrade(newLevel);
            if (success)
            {
                Debug.Log($"[玩家{PlayerId}] 组件 {component.Name} 升级成功，当前等级: {component.CurrentLevel}");
            }
            return success;
        }
        return false;
    }
    
    /// <summary>
    /// 设置组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="level">目标等级</param>
    public void SetComponentLevel(int componentId, int level)
    {
        var component = GetComponentById(componentId);
        if (component != null)
        {
            component.SetLevel(level);
            Debug.Log($"[玩家{PlayerId}] 组件 {component.Name} 等级设置为: {component.CurrentLevel}");
        }
    }
    
    /// <summary>
    /// 激活主动技能
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>是否激活成功</returns>
    public bool ActivateSkill(int componentId)
    {
        var component = GetComponentById(componentId);
        if (component != null && !component.Config.isPassive)
        {
            return component.Activate();
        }
        return false;
    }
    
    /// <summary>
    /// 批量装备组件
    /// </summary>
    /// <param name="components">组件配置和等级列表</param>
    public void EquipComponents(List<(FishingComponentConfig config, int level)> components)
    {
        foreach (var (config, level) in components)
        {
            EquipComponent(config, level);
        }
    }
    
    /// <summary>
    /// 卸下所有组件
    /// </summary>
    public void UnequipAllComponents()
    {
        Debug.Log($"[玩家{PlayerId}] 卸下所有组件，共 {equippedComponents.Count} 个");
        equippedComponents.Clear();
    }
    
    /// <summary>
    /// 更新所有组件并重新计算属性
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public void UpdateAll(float deltaTime)
    {
        // 重置计算属性
        calculatedStats.Reset();
        calculatedStats.trashProbability = baseStats.trashProbability;
        calculatedStats.weightBiasFactor = baseStats.weightBiasFactor;
        calculatedStats.struggleTimeMultiplier = baseStats.baseStruggleMultiplier;
        calculatedStats.fishWeightMultiplier = baseStats.fishWeightMultiplier;
        calculatedStats.shinyRateBonus = baseStats.shinyRateBonus;
        
        // 更新每个组件并应用效果
        foreach (var component in equippedComponents)
        {
            component.Update(deltaTime);
            component.Apply(calculatedStats);
        }
    }
    
    /// <summary>
    /// 获取装备的组件数量
    /// </summary>
    /// <returns>装备数量</returns>
    public int GetEquippedCount()
    {
        return equippedComponents.Count;
    }
    
    /// <summary>
    /// 获取激活的能力数量（兼容旧接口）
    /// </summary>
    /// <returns>激活的能力数量</returns>
    public int GetActiveAbilityCount()
    {
        return equippedComponents.Count(c => c.IsActive());
    }
    
    /// <summary>
    /// 获取所有装备的组件副本列表
    /// </summary>
    /// <returns>组件列表副本</returns>
    public List<PlayerFishingComponent> GetAllEquippedComponents()
    {
        return new List<PlayerFishingComponent>(equippedComponents);
    }
    
    /// <summary>
    /// 检查是否装备了指定类别
    /// </summary>
    /// <param name="category">组件类别</param>
    /// <returns>是否装备</returns>
    public bool HasCategoryEquipped(FishingComponentCategory category)
    {
        return equippedComponents.Any(c => c.Category == category);
    }
    
    /// <summary>
    /// 打印当前装备的所有组件
    /// </summary>
    public void PrintEquippedComponents()
    {
        Debug.Log($"=== 玩家{PlayerId} 的钓鱼装备 ===");
        
        // 按类别分组显示
        var groups = equippedComponents.GroupBy(c => c.Category);
        
        foreach (var group in groups)
        {
            Debug.Log($"【{group.Key}】");
            foreach (var component in group)
            {
                string status = component.IsActive() ? "激活" : (component.IsOnCooldown ? $"冷却中({component.RemainingCooldown:F1}s)" : "未激活");
                Debug.Log($"  {component.Name} Lv.{component.CurrentLevel}/{component.Config.maxLevel} - {status}");
            }
        }
        
        Debug.Log("================================");
    }
    
    /// <summary>
    /// 打印能力组件（兼容旧接口）
    /// </summary>
    public void PrintAbilities()
    {
        PrintEquippedComponents();
    }
    
    /// <summary>
    /// 打印当前计算后的属性
    /// </summary>
    public void PrintCalculatedStats()
    {
        Debug.Log($"=== 玩家{PlayerId} 当前钓鱼属性 ===");
        Debug.Log($"垃圾概率: {calculatedStats.trashProbability * 100:F1}%");
        Debug.Log($"重量倾向系数: {calculatedStats.weightBiasFactor:F2}");
        Debug.Log($"挣扎时间倍率: {calculatedStats.struggleTimeMultiplier:F2}");
        Debug.Log($"鱼类权重倍率: {calculatedStats.fishWeightMultiplier:F2}");
        Debug.Log($"闪光率加成: {calculatedStats.shinyRateBonus * 100:F1}%");
        Debug.Log($"小游戏难度降低: {calculatedStats.minigameDifficultyReduction:F2}");
        foreach (var pair in calculatedStats.rarityBonus)
        {
            Debug.Log($"稀有度{pair.Key}权重加成: {pair.Value:F1}");
        }
        Debug.Log($"保底连续垃圾上限: {calculatedStats.maxTrashStreak}");
        Debug.Log("================================");
    }
    
    /// <summary>
    /// 获取组件等级数据（用于存储和记录）
    /// </summary>
    /// <returns>组件ID和等级的字典</returns>
    public Dictionary<int, int> GetComponentLevelData()
    {
        var data = new Dictionary<int, int>();
        foreach (var component in equippedComponents)
        {
            data[component.Id] = component.CurrentLevel;
        }
        return data;
    }
    
    /// <summary>
    /// 从等级数据恢复组件装备
    /// </summary>
    /// <param name="levelData">组件ID和等级的字典</param>
    /// <param name="skillConfig">完整钓鱼技能配置</param>
    public void RestoreFromLevelData(Dictionary<int, int> levelData, CompleteFishingSkillConfig skillConfig)
    {
        foreach (var (componentId, level) in levelData)
        {
            var config = skillConfig.GetComponentById(componentId);
            if (config != null)
            {
                EquipComponent(config, level);
            }
        }
    }
}