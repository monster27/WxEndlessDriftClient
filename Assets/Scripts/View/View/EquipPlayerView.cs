using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class EquipPlayerView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public Image characterIcon;
    public Text characterNameText;
    public Text characterLevelText;
    public Text characterExpText;

    public Button watchAdUnlockBtn;
    public Button equipBtn;

    public GameObject characterOwnerUseObj;
    public GameObject characterOwnerUnUseObj;
    public GameObject characterLockedObj;

    public Image skill1Icon;
    public Text skill1NameText;

    private int cachedLevel;
    private int cachedCurrentExp;
    private int cachedRequiredExp;
    private bool hasCachedData = false;
    public GameObject skill1OwnerUseObj;
    public GameObject skill1OwnerUnUseObj;
    public GameObject skill1LockedObj;
    public Button skill1UnlockBtn;

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

    // ✅ 添加刷新锁
    private bool _isRefreshing = false;

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
        RegisterDataEvents();

        RefreshCharacterData();
    }

    void OnDisable()
    {
        UnregisterDataEvents();
    }

    void OnDestroy()
    {
        UnregisterDataEvents();
    }

    private void RegisterDataEvents()
    {
        CommunicateEvent.Register<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);

        // ✅ 关键修复：监听背包刷新事件
        CommunicateEvent.Register("Bag_RefreshItems", OnBagRefresh);
    }

    private void UnregisterDataEvents()
    {
        CommunicateEvent.Unregister<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
        CommunicateEvent.Unregister("Bag_RefreshItems", OnBagRefresh);
    }

    // ✅ 背包刷新时直接更新显示
    private void OnBagRefresh()
    {
        Debug.Log("[EquipPlayerView] OnBagRefresh - 背包数据已更新，刷新装备视图");
        UpdateDisplay();
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
        Debug.Log($"[EquipPlayerView] OnEquipChanged - slotType={data.Item1}, itemId={data.Item2}");
        UpdateDisplay();
    }

    private void OnItemQuantityChanged((int, int) data)
    {
        Debug.Log($"[EquipPlayerView] OnItemQuantityChanged - itemId={data.Item1}, quantity={data.Item2}");
        UpdateDisplay();
    }

    private void UpdateCharacterExpDisplay(int currentExp, int requiredExp)
    {
        if (characterExpText != null)
        {
            characterExpText.text = $"{currentExp}/{requiredExp}";
        }
    }

    private void UpdateCharacterLevelDisplay(int level)
    {
        if (characterLevelText != null)
        {
            characterLevelText.text = $"等级: {level}";
        }
    }

    private void RefreshCharacterData()
    {
        int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, 0);
        UpdateCharacterLevelDisplay(level);

        var charData = CommunicateEvent.Request<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", 0);
        if (charData != null)
        {
            int requiredExp = CommunicateEvent.Request<int, int>("CharacterServerManager_GetExpToNextLevel", 0);
            UpdateCharacterExpDisplay(charData.currentExp, requiredExp);

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
        var config = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
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
        }
    }

    private void LoadSkillIcon(int skillId)
    {
        string path = $"UI/Icon/Equipment/Skill/{skillId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            iconCache[skillId] = sprite;
        }
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        return null;
    }

    private void LoadCharacterIds()
    {
        characterIds.Clear();
        var config = CharacterConfigListExtensions.LoadFromResources();
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

        NetServerManager.Instance?.SyncCharacterDataFromServer();

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
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateDisplay()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            Debug.Log($"[EquipPlayerView] UpdateDisplay() called, characterIds.Count={characterIds.Count}");

            if (characterIds.Count == 0)
            {
                Debug.LogError("[EquipPlayerView] characterIds is EMPTY!");
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, characterIds.Count - 1));
            currentCharacterId = characterIds[currentIndex];

            Debug.Log($"[EquipPlayerView] currentIndex={currentIndex}, currentCharacterId={currentCharacterId}");

            // ✅ 直接从 PlayerDataManager 获取背包数据
            var playerData = PlayerDataManager.Instance;
            Dictionary<int, int> inventory;

            if (playerData != null && playerData.IsReady)
            {
                inventory = playerData.GetInventory();
                Debug.Log($"[EquipPlayerView] 从 PlayerDataManager 获取背包数据，物品数: {inventory.Count}");
            }
            else
            {
                // 降级方案
                inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
                Debug.Log($"[EquipPlayerView] 从事件请求获取背包数据，物品数: {inventory.Count}");
            }

            // 获取当前装备的人物
            int equippedChar = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);
            Debug.Log($"[EquipPlayerView] 当前装备的人物: {equippedChar}");

            // ✅ 直接判断拥有状态
            bool hasCharacter = inventory.ContainsKey(currentCharacterId);
            bool isEquipped = equippedChar == currentCharacterId;

            Debug.Log($"[EquipPlayerView] 人物 {currentCharacterId} - 拥有: {hasCharacter}, 已装备: {isEquipped}");

            // 更新UI
            UpdateCharacterUI(hasCharacter, isEquipped);
            UpdateCharacterDisplayInfo();
            UpdatePageText();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // ✅ 新增：分离UI状态更新
    private void UpdateCharacterUI(bool hasCharacter, bool isEquipped)
    {
        EquipState state;
        if (isEquipped)
        {
            state = EquipState.OwnerUse;
        }
        else if (hasCharacter)
        {
            state = EquipState.OwnerUnUse;
        }
        else
        {
            state = EquipState.Locked;
        }

        Debug.Log($"[EquipPlayerView] UpdateCharacterUI - state={state}");

        if (characterOwnerUseObj != null)
        {
            characterOwnerUseObj.SetActive(state == EquipState.OwnerUse);
        }
        if (characterOwnerUnUseObj != null)
        {
            characterOwnerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        }
        if (characterLockedObj != null)
        {
            characterLockedObj.SetActive(state == EquipState.Locked);
        }
    }

    private void UpdateCharacterDisplayInfo()
    {
        if (currentCharacterId <= 0) return;

        if (characterIcon != null)
        {
            Sprite icon = GetIcon(currentCharacterId);
            if (icon != null)
            {
                characterIcon.sprite = icon;
                characterIcon.color = Color.white;
            }
        }

        if (characterNameText != null)
        {
            characterNameText.text = LoadDataManager.Instance.GetComponentName(currentCharacterId);
        }

        UpdateCharacterLevelAndExp();
        UpdateSkillDisplay();
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

        int skill1Id = 0;
        int skill2Id = 0;

        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        if (characterConfig != null)
        {
            (skill1Id, skill2Id) = characterConfig.GetCharacterSkillIds(currentCharacterId);
        }

        UpdateSkill1Display(skill1Id);
        UpdateSkill2Display(skill2Id);
    }

    private void UpdateSkill1Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill1OwnerUseObj != null) skill1OwnerUseObj.SetActive(false);
            if (skill1OwnerUnUseObj != null) skill1OwnerUnUseObj.SetActive(false);
            if (skill1LockedObj != null) skill1LockedObj.SetActive(false);
            if (skill1UnlockBtn != null) skill1UnlockBtn.gameObject.SetActive(false);
            return;
        }

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

        if (skill1OwnerUseObj != null) skill1OwnerUseObj.SetActive(state == EquipState.OwnerUse);
        if (skill1OwnerUnUseObj != null) skill1OwnerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        if (skill1LockedObj != null) skill1LockedObj.SetActive(state == EquipState.Locked);
        if (skill1UnlockBtn != null) skill1UnlockBtn.gameObject.SetActive(state == EquipState.Locked);
    }

    private void UpdateSkill2Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill2OwnerUseObj != null) skill2OwnerUseObj.SetActive(false);
            if (skill2OwnerUnUseObj != null) skill2OwnerUnUseObj.SetActive(false);
            if (skill2LockedObj != null) skill2LockedObj.SetActive(false);
            if (skill2UnlockBtn != null) skill2UnlockBtn.gameObject.SetActive(false);
            return;
        }

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

        if (skill2OwnerUseObj != null) skill2OwnerUseObj.SetActive(state == EquipState.OwnerUse);
        if (skill2OwnerUnUseObj != null) skill2OwnerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        if (skill2LockedObj != null) skill2LockedObj.SetActive(state == EquipState.Locked);
        if (skill2UnlockBtn != null) skill2UnlockBtn.gameObject.SetActive(state == EquipState.Locked);
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
        Debug.Log("[EquipPlayerView] OnMaskClick - 点击遮罩返回");
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        Debug.Log("[EquipPlayerView] OnCloseClick - 点击关闭按钮返回");
        callback?.Invoke("Back", null);
    }

    private void OnLeftClick()
    {
        if (characterIds.Count <= 1) return;
        currentIndex--;
        if (currentIndex < 0) currentIndex = characterIds.Count - 1;
        UpdateDisplay();
    }

    private void OnRightClick()
    {
        if (characterIds.Count <= 1) return;
        currentIndex++;
        if (currentIndex >= characterIds.Count) currentIndex = 0;
        UpdateDisplay();
    }

    private void OnSkill1UnlockClick()
    {
        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        if (characterConfig != null)
        {
            var skillIds = characterConfig.GetCharacterSkillIds(currentCharacterId);
            int skillId50 = skillIds.skillId50;
            if (skillId50 > 0)
            {
                OpenAdForSkillUnlock(skillId50, "解锁技能");
            }
        }
    }

    private void OnSkill2UnlockClick()
    {
        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        if (characterConfig != null)
        {
            var skillIds = characterConfig.GetCharacterSkillIds(currentCharacterId);
            int skillId100 = skillIds.skillId100;
            if (skillId100 > 0)
            {
                OpenAdForSkillUnlock(skillId100, "解锁技能");
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
        string componentName = LoadDataManager.Instance.GetComponentName(currentCharacterId);
        string info = componentName != "未知组件" ? $"看广告解锁人物: {componentName}" : "看广告解锁人物";
        callback?.Invoke("OpenAd", new object[] { info, currentCharacterId, "看广告解锁", (System.Action)(() =>
        {
            CommunicateEvent.Modify("Equip_Unlock", currentCharacterId);
            UpdateDisplay();
            string successInfo = componentName != "未知组件" ? $"恭喜解锁 {componentName}！" : "恭喜解锁人物！";
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
        })});
    }

    private void OnEquipClick()
    {
        Debug.Log($"[EquipPlayerView] OnEquipClick - currentCharacterId={currentCharacterId}");

        CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, (EquipmentSlotType.Character, currentCharacterId));

        RefreshPlayerAnimation(currentCharacterId);

        UpdateDisplay();

        string componentName = LoadDataManager.Instance.GetComponentName(currentCharacterId);
        string successInfo = componentName != "未知组件" ? $"已装备 {componentName}！" : "已装备人物！";
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
    }

    private void RefreshPlayerAnimation(int characterId)
    {
        if (PlayerAniManager.Instance != null)
        {
            PlayerAniManager.Instance.SwitchCharacter(characterId);
        }
    }
}