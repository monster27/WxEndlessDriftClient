// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JsonData;

/// <summary>
/// 人物服务器管理器
/// 负责管理人物配置、升级和技能解锁
/// </summary>
public class CharacterServerManager
{
    private static CharacterServerManager instance;
    private static bool requestHandlersRegistered = false;
    public static CharacterServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new CharacterServerManager();
                instance.LoadConfig();
                instance.RegisterRequestHandlers();
            }
            return instance;
        }
    }

    public event System.Action<int, int> OnExpChanged;
    public event System.Action OnLevelChanged;

    private List<CharacterConfig> characterConfigs = new List<CharacterConfig>();
    private CharacterLevelUpConfig levelUpExpConfig = new CharacterLevelUpConfig();
    private PlayerCharacterData playerCharacterData = new PlayerCharacterData();

    /// <summary>
    /// 加载配置文件
    /// </summary>
    private void LoadConfig()
    {
        // 加载人物配置
        TextAsset characterText = Resources.Load<TextAsset>("JsonData/BaseFramework/characters");
        if (characterText != null)
        {
            CharacterConfigList characterList = JsonUtility.FromJson<CharacterConfigList>(characterText.text);
            characterConfigs = characterList.characters;

            // 构建人物配置成功日志
            string successLog = $"[CharacterServerManager] 人物配置加载成功，共 {characterConfigs.Count} 个人物\n";
            foreach (var config in characterConfigs)
            {
                successLog += $"    - ID: {config.id}, 名称: {config.name}\n";
            }
            Debug.Log(successLog);
        }
        else
        {
            Debug.LogError("[CharacterServerManager] 人物配置加载失败: 未找到 JsonData/BaseFramework/characters 文件");
        }

        // 加载升级经验配置
        TextAsset expText = Resources.Load<TextAsset>("JsonData/BaseFramework/level_up_exp_config");
        if (expText != null)
        {
            levelUpExpConfig = CharacterLevelUpConfig.ParseFromJson(expText.text);

            // 构建经验配置成功日志
            string successLog = $"[CharacterServerManager] 经验配置加载成功，共 {levelUpExpConfig.levelUpExpRequirements.Count} 个区间\n";
            foreach (var kvp in levelUpExpConfig.levelUpExpRequirements)
            {
                successLog += $"    - 区间 {kvp.Key}: {kvp.Value} 经验\n";
            }
            Debug.Log(successLog);
        }
        else
        {
            Debug.LogError("[CharacterServerManager] 经验配置加载失败: 未找到 JsonData/BaseFramework/level_up_exp_config 文件");
        }
    }

    /// <summary>
    /// 装备人物
    /// </summary>
    /// <param name="characterId">人物ID</param>
    public void EquipCharacter(int characterId)
    {
        CharacterConfig config = characterConfigs.Find(c => c.id == characterId);
        if (config == null)
        {
            Debug.LogWarning($"[CharacterServerManager] 人物ID {characterId} 不存在");
            return;
        }

        playerCharacterData.equippedCharacterId = characterId;
        playerCharacterData.isEquipped = true;
        playerCharacterData.currentLevel = 1;
        playerCharacterData.currentExp = 0;
        playerCharacterData.unlockedSkills.Clear();

        SyncLevelToInventoryManager();

        // 触发经验和等级变化事件，通知UI更新
        int requiredExp = GetExpToNextLevelPublic();
        OnExpChanged?.Invoke(playerCharacterData.currentExp, requiredExp);
        OnLevelChanged?.Invoke();

        // 通过CommunicateEvent触发人物数据变更事件
        CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (playerCharacterData.currentLevel, playerCharacterData.currentExp, requiredExp));

        Debug.Log($"[CharacterServerManager] 装备人物: {config.name}, isEquipped={playerCharacterData.isEquipped}, level={playerCharacterData.currentLevel}");
    }

    /// <summary>
    /// 卸下人物
    /// </summary>
    public void UnequipCharacter()
    {
        playerCharacterData.isEquipped = false;
        playerCharacterData.equippedCharacterId = 0;
        Debug.Log("[CharacterServerManager] 卸下人物");
    }

    /// <summary>
    /// 注册请求处理器
    /// </summary>
    private void RegisterRequestHandlers()
    {
        if (requestHandlersRegistered) return;
        requestHandlersRegistered = true;

        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevelPublic());
        CommunicateEvent.RegisterRequest<int, string>("CharacterServerManager_GetSkillUnlockCondition", GetSkillUnlockCondition);

        Debug.Log("[CharacterServerManager] 请求处理器注册完成");
    }

    /// <summary>
    /// 获取玩家人物数据
    /// </summary>
    public PlayerCharacterData GetPlayerCharacterData()
    {
        return playerCharacterData;
    }

    /// <summary>
    /// 获取指定等级所需的总经验
    /// </summary>
    public int GetRequiredExpForLevel(int level)
    {
        return levelUpExpConfig.GetExpForLevel(level);
    }

    /// <summary>
    /// 添加人物经验（钓到鱼时调用）
    /// </summary>
    /// <param name="exp">经验值</param>
    public void AddCharacterExp(int exp)
    {
        // 1. 检查是否装备了人物
        if (!playerCharacterData.isEquipped)
        {
            Debug.Log("[CharacterServerManager] 未装备人物，不获得经验");
            return;
        }

        // 2. 检查是否满级
        if (playerCharacterData.currentLevel >= 100)
        {
            Debug.Log("[CharacterServerManager] 人物已满级，不再获得经验");
            return;
        }

        int oldLevel = playerCharacterData.currentLevel;
        playerCharacterData.currentExp += exp;

        Debug.Log($"[CharacterServerManager] 添加经验: {exp}, 当前经验: {playerCharacterData.currentExp}, 当前等级: {playerCharacterData.currentLevel}");

        // 3. 循环升级
        while (CanLevelUp())
        {
            int requiredExp = GetExpToNextLevel(); // 升到下一级所需经验
            playerCharacterData.currentExp -= requiredExp;
            LevelUp();
        }

        // 4. 触发经验变化事件
        int newRequiredExp = GetExpToNextLevel();
        OnExpChanged?.Invoke(playerCharacterData.currentExp, newRequiredExp);

        // 5. 等级变化时触发事件
        if (oldLevel != playerCharacterData.currentLevel)
        {
            Debug.Log($"[CharacterServerManager] 等级变化: {oldLevel} -> {playerCharacterData.currentLevel}");
            OnLevelChanged?.Invoke();
        }

        // 通过CommunicateEvent触发人物数据变更事件
        CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (playerCharacterData.currentLevel, playerCharacterData.currentExp, newRequiredExp));
    }

    /// <summary>
    /// 检查是否可以升级
    /// </summary>
    /// <returns>是否可以升级</returns>
    private bool CanLevelUp()
    {
        if (playerCharacterData.currentLevel >= 100)
        {
            return false;
        }

        int requiredExp = GetExpToNextLevel();
        bool canLevelUp = playerCharacterData.currentExp >= requiredExp;
        Debug.Log($"[CharacterServerManager] CanLevelUp: level={playerCharacterData.currentLevel}, exp={playerCharacterData.currentExp}, required={requiredExp}, can={canLevelUp}");
        return canLevelUp;
    }

    /// <summary>
    /// 升级逻辑
    /// </summary>
    private void LevelUp()
    {
        int oldLevel = playerCharacterData.currentLevel;
        playerCharacterData.currentLevel++;

        Debug.Log($"[CharacterServerManager] 人物升级: {oldLevel} -> {playerCharacterData.currentLevel}");

        // 1. 同步等级到装备系统
        SyncLevelToInventoryManager();

        // 2. 检查整十级金币奖励（10,20,30...90级）
        if (playerCharacterData.currentLevel % 10 == 0 && playerCharacterData.currentLevel < 100)
        {
            GiveTenLevelGoldReward();
        }

        // 3. 检查技能解锁（50级和100级）
        CheckSkillUnlock();
    }

    /// <summary>
    /// 获取升到下一级所需经验（公共方法，供UI调用）
    /// </summary>
    public int GetExpToNextLevelPublic()
    {
        return GetExpToNextLevel();
    }

    /// <summary>
    /// 获取技能的解锁条件（人物等级要求）
    /// </summary>
    /// <param name="skillId">技能ID</param>
    /// <returns>解锁条件描述，如"小明等级50"</returns>
    public string GetSkillUnlockCondition(int skillId)
    {
        foreach (var config in characterConfigs)
        {
            if (config.skillIdAtLevel50 == skillId)
            {
                return $"{config.name}等级50";
            }
            if (config.skillIdAtLevel100 == skillId)
            {
                return $"{config.name}等级100";
            }
        }
        return "";
    }

    /// <summary>
    /// 获取升到下一级所需经验
    /// </summary>
    private int GetExpToNextLevel()
    {
        int currentLevel = playerCharacterData.currentLevel;

        // 根据等级区间获取每级经验（区间总经验 / 10）
        if (currentLevel >= 1 && currentLevel <= 10)
            return levelUpExpConfig.GetExpForLevelRange("1-10") / 10;
        if (currentLevel >= 11 && currentLevel <= 20)
            return levelUpExpConfig.GetExpForLevelRange("11-20") / 10;
        if (currentLevel >= 21 && currentLevel <= 30)
            return levelUpExpConfig.GetExpForLevelRange("21-30") / 10;
        if (currentLevel >= 31 && currentLevel <= 40)
            return levelUpExpConfig.GetExpForLevelRange("31-40") / 10;
        if (currentLevel >= 41 && currentLevel <= 50)
            return levelUpExpConfig.GetExpForLevelRange("41-50") / 10;
        if (currentLevel >= 51 && currentLevel <= 60)
            return levelUpExpConfig.GetExpForLevelRange("51-60") / 10;
        if (currentLevel >= 61 && currentLevel <= 70)
            return levelUpExpConfig.GetExpForLevelRange("61-70") / 10;
        if (currentLevel >= 71 && currentLevel <= 80)
            return levelUpExpConfig.GetExpForLevelRange("71-80") / 10;
        if (currentLevel >= 81 && currentLevel <= 90)
            return levelUpExpConfig.GetExpForLevelRange("81-90") / 10;
        if (currentLevel >= 91 && currentLevel <= 99)
            return levelUpExpConfig.GetExpForLevelRange("91-100") / 10;

        return 100; // 默认值
    }

    /// <summary>
    /// 发放整十级金币奖励
    /// </summary>
    private void GiveTenLevelGoldReward()
    {
        int level = playerCharacterData.currentLevel;
        int goldReward = 0;

        // 优先从配置表获取
        if (levelUpExpConfig.tenLevelGoldRewards != null &&
            levelUpExpConfig.tenLevelGoldRewards.ContainsKey(level))
        {
            goldReward = levelUpExpConfig.tenLevelGoldRewards[level];
        }
        else
        {
            // 默认规则：等级 × 50
            goldReward = level * 50;
        }

        if (SimulationServer.Instance != null)
        {
            SimulationServer.Instance.AddGold(goldReward);
            Debug.Log($"[CharacterServerManager] {level}级奖励: {goldReward}金币");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTip($"人物达到{level}级，获得{goldReward}金币！");
            }
        }
    }

    /// <summary>
    /// 同步等级到背包管理器
    /// </summary>
    private void SyncLevelToInventoryManager()
    {
        if (SimulationServer.Instance != null && playerCharacterData.equippedCharacterId > 0)
        {
            SimulationServer.Instance.SetComponentLevel(playerCharacterData.equippedCharacterId, playerCharacterData.currentLevel);
        }
    }

    /// <summary>
    /// 检查是否解锁技能
    /// </summary>
    private void CheckSkillUnlock()
    {
        if (playerCharacterData.equippedCharacterId == 0)
        {
            return;
        }

        CharacterConfig config = characterConfigs.Find(c => c.id == playerCharacterData.equippedCharacterId);
        if (config == null)
        {
            return;
        }

        // 50级解锁技能
        if (playerCharacterData.currentLevel == 50 && config.skillIdAtLevel50 > 0)
        {
            if (!playerCharacterData.unlockedSkills.Contains(config.skillIdAtLevel50))
            {
                playerCharacterData.unlockedSkills.Add(config.skillIdAtLevel50);
                Debug.Log($"[CharacterServerManager] 50级解锁技能: {config.skillIdAtLevel50}");

                // 通知服务器添加技能到背包
                if (SimulationServer.Instance != null)
                {
                    SimulationServer.Instance.AddItem(config.skillIdAtLevel50, 1);
                    Debug.Log($"[CharacterServerManager] 添加技能到背包: {config.skillIdAtLevel50}");

                    // 显示提示信息
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowTip("恭喜！人物达到50级，解锁特殊技能！");
                    }
                }
            }
        }

        // 100级解锁技能
        if (playerCharacterData.currentLevel == 100 && config.skillIdAtLevel100 > 0)
        {
            if (!playerCharacterData.unlockedSkills.Contains(config.skillIdAtLevel100))
            {
                playerCharacterData.unlockedSkills.Add(config.skillIdAtLevel100);
                Debug.Log($"[CharacterServerManager] 100级解锁技能: {config.skillIdAtLevel100}");

                // 通知服务器添加技能到背包
                if (SimulationServer.Instance != null)
                {
                    SimulationServer.Instance.AddItem(config.skillIdAtLevel100, 1);
                    Debug.Log($"[CharacterServerManager] 添加技能到背包: {config.skillIdAtLevel100}");

                    // 显示提示信息
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowTip("恭喜！人物达到100级满级，解锁终极技能！");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 根据鱼类稀有度获取经验值
    /// </summary>
    /// <param name="rarityId">稀有度ID</param>
    /// <returns>经验值</returns>
    public int GetFishExpByRarity(int rarityId)
    {
        int exp = LoadDataManager.Instance.GetRarityExp(rarityId);
        Debug.Log($"[CharacterServerManager] GetFishExpByRarity: rarityId={rarityId} -> exp={exp}");
        return exp;
    }

    /// <summary>
    /// 获取当前人物数据
    /// </summary>
    /// <returns>人物数据</returns>
    public PlayerCharacterData GetCurrentCharacterData()
    {
        return playerCharacterData;
    }

    /// <summary>
    /// 获取人物配置
    /// </summary>
    /// <param name="characterId">人物ID</param>
    /// <returns>人物配置</returns>
    public CharacterConfig GetCharacterConfig(int characterId)
    {
        return characterConfigs.Find(c => c.id == characterId);
    }

    /// <summary>
    /// 获取所有人物配置
    /// </summary>
    /// <returns>人物配置列表</returns>
    public List<CharacterConfig> GetAllCharacterConfigs()
    {
        return characterConfigs;
    }
}
*/