using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EquipPlayerView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public Image characterIcon;
    public Text characterNameText;
    public Text characterLevelText;
    public Text characterExpText;

    public Button watchAdUnlockBtn; // 看视频获取按钮
    public Button equipBtn;         // 装备按钮
    
    // 人物三状态Obj
    public GameObject characterOwnerUseObj;    // 已装备状态
    public GameObject characterOwnerUnUseObj;  // 拥有未装备状态
    public GameObject characterLockedObj;      // 未解锁状态

    // Skill 1 Group
    public Image skill1Icon;
    public Text skill1NameText;

    // 缓存的人物数据
    private int cachedLevel;
    private int cachedCurrentExp;
    private int cachedRequiredExp;
    private bool hasCachedData = false;
    public GameObject skill1OwnerUseObj;
    public GameObject skill1OwnerUnUseObj;
    public GameObject skill1LockedObj;
    public Button skill1UnlockBtn;

    // Skill 2 Group
    public Image skill2Icon;
    public Text skill2NameText;
    public GameObject skill2OwnerUseObj;
    public GameObject skill2OwnerUnUseObj;
    public GameObject skill2LockedObj;
    public Button skill2UnlockBtn;

    public Button leftBtn;
    public Button rightBtn;
    public Text pageText;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private List<int> characterIds = new List<int>();
    private int currentIndex = 0;
    private int currentCharacterId = 0;
    private System.Action<string, object[]> callback;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);

        if (skill1UnlockBtn != null) skill1UnlockBtn.onClick.AddListener(OnSkill1UnlockClick);
        if (skill2UnlockBtn != null) skill2UnlockBtn.onClick.AddListener(OnSkill2UnlockClick);
        
        if (watchAdUnlockBtn != null) watchAdUnlockBtn.onClick.AddListener(OnWatchAdUnlockClick);
        if (equipBtn != null) equipBtn.onClick.AddListener(OnEquipClick);
    }

    void OnEnable()
    {
        RegisterCharacterEvents();
        RegisterDataEvents();
        
        // 主动请求一次人物数据，确保显示最新状态
        RefreshCharacterData();
    }

    void OnDisable()
    {
        UnregisterCharacterEvents();
        UnregisterDataEvents();
    }

    void OnDestroy()
    {
        UnregisterCharacterEvents();
        UnregisterDataEvents();
    }

    private void RegisterCharacterEvents()
    {
    }

    private void UnregisterCharacterEvents()
    {
    }

    private void RegisterDataEvents()
    {
        CommunicateEvent.Register<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
    }

    private void UnregisterDataEvents()
    {
        CommunicateEvent.Unregister<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
    }

    private void OnCharacterExpChanged(int currentExp, int requiredExp)
    {
        UpdateCharacterExpDisplay(currentExp, requiredExp);
    }

    private void OnCharacterLevelChanged()
    {
        UpdateCharacterLevelDisplay();
    }

    private void OnCharacterDataChanged((int, int, int) data)
    {
        int level = data.Item1;
        int currentExp = data.Item2;
        int requiredExp = data.Item3;
        Debug.Log($"[EquipPlayerView] OnCharacterDataChanged: level={level}, currentExp={currentExp}, requiredExp={requiredExp}");
        cachedLevel = level;
        cachedCurrentExp = currentExp;
        cachedRequiredExp = requiredExp;
        hasCachedData = true;

        UpdateCharacterLevelDisplay(level);
        UpdateCharacterExpDisplay(currentExp, requiredExp);
    }

    private void OnEquipChanged((int, int) data)
    {
        UpdateCharacterDisplay();
        UpdateSkillDisplay();
    }

    private void OnItemQuantityChanged((int, int) data)
    {
        UpdateCharacterDisplay();
    }

    private void UpdateCharacterExpDisplay(int currentExp, int requiredExp)
    {
        if (characterExpText != null)
        {
            characterExpText.text = $"{currentExp}/{requiredExp}";
        }
    }

    private void UpdateCharacterLevelDisplay()
    {
        int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, 0);
        UpdateCharacterLevelDisplay(level);
    }

    private void UpdateCharacterLevelDisplay(int level)
    {
        if (characterLevelText != null)
        {
            characterLevelText.text = $"等级: {level}";
        }
    }

    /// <summary>
    /// 主动刷新人物数据
    /// </summary>
    private void RefreshCharacterData()
    {
        // 请求人物等级
        int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, 0);
        UpdateCharacterLevelDisplay(level);
        
        // 请求人物经验数据
        var charData = CommunicateEvent.Request<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", 0);
        if (charData != null)
        {
            int requiredExp = CommunicateEvent.Request<int, int>("CharacterServerManager_GetExpToNextLevel", 0);
            UpdateCharacterExpDisplay(charData.currentExp, requiredExp);
            
            // 更新缓存
            cachedLevel = level;
            cachedCurrentExp = charData.currentExp;
            cachedRequiredExp = requiredExp;
            hasCachedData = true;
        }
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
        LoadCharacterIds();
        LoadAllIcons();
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();

        foreach (int characterId in characterIds)
        {
            LoadCharacterIcon(characterId);
        }

        var skillIds = new List<int>();
        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var item in config.items)
            {
                if (item.id >= 3301 && item.id <= 3399)
                    skillIds.Add(item.id);
            }
        }

        foreach (int skillId in skillIds)
        {
            LoadSkillIcon(skillId);
        }

        Debug.Log($"[EquipPlayerView] LoadAllIcons 完成，缓存了 {iconCache.Count} 个图标");
    }

    private void LoadCharacterIcon(int characterId)
    {
        string path = $"UI/Icon/Equipment/Character/{characterId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            iconCache[characterId] = sprite;
            Debug.Log($"[EquipPlayerView] LoadCharacterIcon 成功 - id={characterId}, path={path}");
        }
        else
        {
            Debug.LogWarning($"[EquipPlayerView] LoadCharacterIcon 失败 - id={characterId}, path={path}");
        }
    }

    private void LoadSkillIcon(int skillId)
    {
        string path = $"UI/Icon/Equipment/Skill/{skillId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            iconCache[skillId] = sprite;
            Debug.Log($"[EquipPlayerView] LoadSkillIcon 成功 - id={skillId}, path={path}");
        }
        else
        {
            Debug.LogWarning($"[EquipPlayerView] LoadSkillIcon 失败 - id={skillId}, path={path}");
        }
    }

    private int ExtractIdFromPath(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int id))
        {
            return id;
        }
        return 0;
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        
        // 如果缓存中没有，立即尝试加载
        Debug.LogWarning($"[EquipPlayerView] GetIcon - 缓存中未找到 id={id}，尝试立即加载");
        LoadCharacterIcon(id);
        
        // 再次尝试从缓存获取
        if (iconCache.TryGetValue(id, out sprite))
        {
            return sprite;
        }
        
        return null;
    }

    private void LoadCharacterIds()
    {
        characterIds.Clear();
        var config = CharacterConfigList.LoadFromResources();
        if (config != null)
        {
            var ids = config.GetAllCharacterIds();
            foreach (var id in ids)
            {
                characterIds.Add(id);
            }
        }
    }

    public void Show()
    {
        Debug.Log("[EquipPlayerView] Show() called");

        int equippedCharacterId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);
        Debug.Log($"[EquipPlayerView] 当前装备的人物ID: {equippedCharacterId}");

        currentIndex = 0;
        if (equippedCharacterId > 0)
        {
            currentIndex = characterIds.IndexOf(equippedCharacterId);
            if (currentIndex < 0) currentIndex = 0;
        }
        
        UpdateDisplay();
        gameObject.SetActive(true);
        Debug.Log($"[EquipPlayerView] Set gameObject active = true, currentIndex={currentIndex}");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateDisplay()
    {
        Debug.Log($"[EquipPlayerView] UpdateDisplay() called, characterIds.Count={characterIds.Count}");
        
        if (characterIds.Count == 0)
        {
            Debug.LogError("[EquipPlayerView] characterIds is EMPTY! LoadCharacterIds() was not called or failed!");
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, characterIds.Count - 1));
        currentCharacterId = characterIds[currentIndex];

        Debug.Log($"[EquipPlayerView] currentIndex={currentIndex}, currentCharacterId={currentCharacterId}");

        // 打印当前背包状态
        var inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        Debug.Log($"[EquipPlayerView] 背包状态 - 3401: {(inventory.ContainsKey(3401) ? "有" : "无")}, 3402: {(inventory.ContainsKey(3402) ? "有" : "无")}");
        int equippedChar = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);
        Debug.Log($"[EquipPlayerView] 当前装备的人物: {equippedChar}");

        UpdateCharacterDisplay();
        UpdatePageText();
    }

    private void UpdateCharacterDisplay()
    {
        Debug.Log($"[EquipPlayerView] UpdateCharacterDisplay - currentCharacterId={currentCharacterId}");
        
        if (currentCharacterId <= 0)
        {
            Debug.LogWarning("[EquipPlayerView] UpdateCharacterDisplay - currentCharacterId <= 0，跳过");
            return;
        }

        if (characterIcon != null)
        {
            Sprite icon = GetIcon(currentCharacterId);
            Debug.Log($"[EquipPlayerView] UpdateCharacterDisplay - GetIcon({currentCharacterId}) = {(icon != null ? "成功" : "失败")}");
            
            if (icon != null)
            {
                characterIcon.sprite = icon;
                characterIcon.color = Color.white;
                Debug.Log($"[EquipPlayerView] UpdateCharacterDisplay - 图标设置成功");
            }
            else
            {
                Debug.LogWarning($"[EquipPlayerView] UpdateCharacterDisplay - 图标为空，无法设置");
            }
        }
        else
        {
            Debug.LogWarning("[EquipPlayerView] UpdateCharacterDisplay - characterIcon 为空");
        }

        if (characterNameText != null)
        {
            string characterName = LoadDataManager.Instance.GetComponentName(currentCharacterId);
            characterNameText.text = characterName;
            Debug.Log($"[EquipPlayerView] UpdateCharacterDisplay - 人物名称: {characterName}");
        }
        else
        {
            Debug.LogWarning("[EquipPlayerView] UpdateCharacterDisplay - characterNameText 为空");
        }

        UpdateCharacterLevelAndExp();
        UpdateSkillDisplay();
        UpdateCharacterButtons();
    }

    private void UpdateCharacterButtons()
    {
        EquipState state = GetCharacterState();
        Debug.Log($"[EquipPlayerView] UpdateCharacterButtons - state={state}");

        // 人物三状态Obj切换
        if (characterOwnerUseObj != null)
        {
            bool showOwnerUse = state == EquipState.OwnerUse;
            characterOwnerUseObj.SetActive(showOwnerUse);
            Debug.Log($"[EquipPlayerView] characterOwnerUseObj.SetActive({showOwnerUse})");
        }
        
        if (characterOwnerUnUseObj != null)
        {
            bool showOwnerUnUse = state == EquipState.OwnerUnUse;
            characterOwnerUnUseObj.SetActive(showOwnerUnUse);
            Debug.Log($"[EquipPlayerView] characterOwnerUnUseObj.SetActive({showOwnerUnUse})");
        }
        
        if (characterLockedObj != null)
        {
            bool showLocked = state == EquipState.Locked;
            characterLockedObj.SetActive(showLocked);
            Debug.Log($"[EquipPlayerView] characterLockedObj.SetActive({showLocked})");
        }

    }

    private EquipState GetCharacterState()
    {
        Debug.Log($"[EquipPlayerView] GetCharacterState called, currentCharacterId={currentCharacterId}");

        bool isObtained = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, currentCharacterId);
        Debug.Log($"[EquipPlayerView] IsCharacterObtained check - currentCharacterId={currentCharacterId}, isObtained={isObtained}");

        if (!isObtained)
        {
            return EquipState.Locked;
        }

        int equippedCharacter = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);
        Debug.Log($"[EquipPlayerView] equippedCharacter={equippedCharacter}, currentCharacterId={currentCharacterId}");

        if (equippedCharacter == currentCharacterId)
        {
            return EquipState.OwnerUse;
        }

        return EquipState.OwnerUnUse;
    }

    private void UpdateCharacterLevelAndExp()
    {
        var playerData = CommunicateEvent.Request<int, PlayerCharacterData>("CharacterManager_GetPlayerData", 0);
        if (playerData != null)
        {
            int level = playerData.currentLevel;
            if (characterLevelText != null)
            {
                characterLevelText.text = $"等级: {level}";
            }

            int requiredExp = CommunicateEvent.Request<int, int>("CharacterManager_GetExpToNextLevel", 0);
            if (characterExpText != null)
            {
                characterExpText.text = $"{playerData.currentExp}/{requiredExp}";
            }
        }
    }

    private void UpdateSkillDisplay()
    {
        Debug.Log($"[EquipPlayerView] UpdateSkillDisplay - currentCharacterId={currentCharacterId}");
        
        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config == null)
        {
            Debug.LogWarning("[EquipPlayerView] UpdateSkillDisplay - config is null");
            return;
        }

        var component = config.GetComponentById(currentCharacterId);
        if (component == null)
        {
            Debug.LogWarning($"[EquipPlayerView] UpdateSkillDisplay - component for {currentCharacterId} is null");
        }

        int skill1Id = 0;
        int skill2Id = 0;

        var characterConfig = CharacterConfigList.LoadFromResources();
        if (characterConfig != null)
        {
            (skill1Id, skill2Id) = characterConfig.GetCharacterSkillIds(currentCharacterId);
        }

        Debug.Log($"[EquipPlayerView] UpdateSkillDisplay - skill1Id={skill1Id}, skill2Id={skill2Id}");

        UpdateSkill1Display(skill1Id);
        UpdateSkill2Display(skill2Id);
    }

    private void UpdateSkill1Display(int skillId)
    {
        if (skillId <= 0) return;

        Debug.Log($"[EquipPlayerView] UpdateSkill1Display - skillId={skillId}");
        
        if (skill1Icon != null)
        {
            Sprite icon = GetIcon(skillId);
            if (icon != null)
            {
                skill1Icon.sprite = icon;
                skill1Icon.color = Color.white;
            }
        }

        if (skill1NameText != null)
        {
            skill1NameText.text = LoadDataManager.Instance.GetComponentName(skillId);
        }

        EquipState state = GetSkillState(skillId);
        Debug.Log($"[EquipPlayerView] UpdateSkill1Display - skillId={skillId}, state={state}");

        if (skill1OwnerUseObj != null)
        {
            skill1OwnerUseObj.SetActive(state == EquipState.OwnerUse);
        }
        if (skill1OwnerUnUseObj != null)
        {
            skill1OwnerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        }
        if (skill1LockedObj != null)
        {
            skill1LockedObj.SetActive(state == EquipState.Locked);
        }

        if (skill1UnlockBtn != null)
        {
            skill1UnlockBtn.gameObject.SetActive(state == EquipState.Locked);
        }
    }

    private void UpdateSkill2Display(int skillId)
    {
        if (skillId <= 0) return;
        
        Debug.Log($"[EquipPlayerView] UpdateSkill2Display - skillId={skillId}");

        if (skill2Icon != null)
        {
            Sprite icon = GetIcon(skillId);
            if (icon != null)
            {
                skill2Icon.sprite = icon;
                skill2Icon.color = Color.white;
            }
        }

        if (skill2NameText != null)
        {
            skill2NameText.text = LoadDataManager.Instance.GetComponentName(skillId);
        }

        EquipState state = GetSkillState(skillId);
        Debug.Log($"[EquipPlayerView] UpdateSkill2Display - skillId={skillId}, state={state}");

        if (skill2OwnerUseObj != null)
        {
            skill2OwnerUseObj.SetActive(state == EquipState.OwnerUse);
        }
        if (skill2OwnerUnUseObj != null)
        {
            skill2OwnerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        }
        if (skill2LockedObj != null)
        {
            skill2LockedObj.SetActive(state == EquipState.Locked);
        }

        if (skill2UnlockBtn != null)
        {
            skill2UnlockBtn.gameObject.SetActive(state == EquipState.Locked);
        }
    }

    private EquipState GetSkillState(int skillId)
    {
        if (!CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId))
        {
            return EquipState.Locked;
        }

        int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
        int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);

        if (equippedSkill1 == skillId || equippedSkill2 == skillId)
        {
            return EquipState.OwnerUse;
        }

        return EquipState.OwnerUnUse;
    }

    private void UpdatePageText()
    {
        if (pageText != null)
        {
            pageText.text = $"{currentIndex + 1}/{Mathf.Max(1, characterIds.Count)}";
        }
    }

    private void OnMaskClick()
    {
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        callback?.Invoke("Back", null);
    }

    private void OnLeftClick()
    {
        if (characterIds.Count <= 1) return;

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = characterIds.Count - 1;
        }
        UpdateDisplay();
    }

    private void OnRightClick()
    {
        if (characterIds.Count <= 1) return;

        currentIndex++;
        if (currentIndex >= characterIds.Count)
        {
            currentIndex = 0;
        }
        UpdateDisplay();
    }

    private void OnSkill1UnlockClick()
    {
        var characterConfig = CharacterConfigList.LoadFromResources();
        if (characterConfig != null)
        {
            (int skillId50, _) = characterConfig.GetCharacterSkillIds(currentCharacterId);
            if (skillId50 > 0)
            {
                var fishingConfig = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
                string skillName = "技能";
                if (fishingConfig != null)
                {
                    var skillConfig = fishingConfig.GetComponentById(skillId50);
                    if (skillConfig != null) skillName = skillConfig.name;
                }
                OpenAdForSkillUnlock(skillId50, $"解锁{skillName}");
            }
        }
    }

    private void OnSkill2UnlockClick()
    {
        var characterConfig = CharacterConfigList.LoadFromResources();
        if (characterConfig != null)
        {
            (_, int skillId100) = characterConfig.GetCharacterSkillIds(currentCharacterId);
            if (skillId100 > 0)
            {
                var fishingConfig = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
                string skillName = "技能";
                if (fishingConfig != null)
                {
                    var skillConfig = fishingConfig.GetComponentById(skillId100);
                    if (skillConfig != null) skillName = skillConfig.name;
                }
                OpenAdForSkillUnlock(skillId100, $"解锁{skillName}");
            }
        }
    }

    private void OpenAdForSkillUnlock(int skillId, string info)
    {
        callback?.Invoke("OpenAd", new object[] { info, skillId, "看广告解锁", (System.Action)(() => 
        {
            CommunicateEvent.Modify("Skill_Unlock", skillId);
            UpdateDisplay();
        })});
    }

    private void OnWatchAdUnlockClick()
    {
        Debug.Log($"[EquipPlayerView] OnWatchAdUnlockClick - currentCharacterId={currentCharacterId}");

        string componentName = LoadDataManager.Instance.GetComponentName(currentCharacterId);
        string info = componentName != "未知组件" ? $"看广告解锁人物: {componentName}" : "看广告解锁人物";
        callback?.Invoke("OpenAd", new object[] { info, currentCharacterId, "看广告解锁", (System.Action)(() =>
        {
            Debug.Log($"[EquipPlayerView] OnWatchAdUnlockClick callback - 准备触发 Equip_Unlock 事件, characterId={currentCharacterId}");
            CommunicateEvent.Modify("Equip_Unlock", currentCharacterId);
            UpdateDisplay();

            // 显示解锁成功提示
            string successInfo = componentName != "未知组件" ? $"恭喜解锁 {componentName}！" : "恭喜解锁人物！";
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
        })});
    }

    private void OnEquipClick()
    {
        Debug.Log($"[EquipPlayerView] OnEquipClick - currentCharacterId={currentCharacterId}");

        CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, (EquipmentSlotType.Character, currentCharacterId));

        // 主动刷新动画管理器的人物数据
        RefreshPlayerAnimation(currentCharacterId);
        
        UpdateDisplay();

        string componentName = LoadDataManager.Instance.GetComponentName(currentCharacterId);
        string successInfo = componentName != "未知组件" ? $"已装备 {componentName}！" : "已装备人物！";
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
    }

    /// <summary>
    /// 主动刷新玩家动画
    /// </summary>
    private void RefreshPlayerAnimation(int characterId)
    {
        if (PlayerAniManager.Instance != null)
        {
            // 确保动画数据已加载
            PlayerAniManager.Instance.SwitchCharacter(characterId);
            Debug.Log($"[EquipPlayerView] RefreshPlayerAnimation - 已调用 SwitchCharacter({characterId})");
        }
        else
        {
            Debug.LogWarning("[EquipPlayerView] RefreshPlayerAnimation - PlayerAniManager.Instance 为 null");
        }
    }
}