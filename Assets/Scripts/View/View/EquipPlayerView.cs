using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class EquipPlayerView : MonoBehaviour
{
    [Header("基础UI")]
    public Button maskBtn;
    public Button closeBtn;

    [Header("人物信息显示")]
    public Image characterIcon;
    public Text characterNameText;
    public Text characterLevelText;
    public Text characterExpText;
    public Slider characterExpSlider;

    [Header("人物状态（使用 UIUseStatus 组件）")]
    public UIUseStatus characterStatus;

    [Header("技能1")]
    public Image skill1Icon;
    public Text skill1NameText;
    public UIUseStatus skill1Status;

    [Header("技能2")]
    public Image skill2Icon;
    public Text skill2NameText;
    public UIUseStatus skill2Status;

    [Header("翻页")]
    public Button leftBtn;
    public Button rightBtn;
    public Text pageText;

    [Header("操作按钮")]
    public Button watchAdUnlockBtn;
    public Button equipBtn;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private List<int> characterIds = new List<int>();
    private int currentIndex = 0;
    private int currentCharacterId = 0;
    private System.Action<string, object[]> callback;

    private int cachedLevel;
    private int cachedCurrentExp;
    private int cachedRequiredExp;
    private bool hasCachedData = false;

    // ✅ 添加刷新锁
    private bool _isRefreshing = false;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);

        if (watchAdUnlockBtn != null) watchAdUnlockBtn.onClick.AddListener(OnWatchAdUnlockClick);
        if (equipBtn != null) equipBtn.onClick.AddListener(OnEquipClick);

        // ✅ 技能解锁按钮事件（通过 UIUseStatus 中的按钮）
        if (skill1Status != null && skill1Status.unlockBtn != null)
            skill1Status.unlockBtn.onClick.AddListener(OnSkill1UnlockClick);

        if (skill2Status != null && skill2Status.unlockBtn != null)
            skill2Status.unlockBtn.onClick.AddListener(OnSkill2UnlockClick);
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
        CommunicateEvent.Register("Bag_RefreshItems", OnBagRefresh);
    }

    private void UnregisterDataEvents()
    {
        CommunicateEvent.Unregister<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
        CommunicateEvent.Unregister("Bag_RefreshItems", OnBagRefresh);
    }

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
            characterExpText.text = $"{currentExp}/{requiredExp}";

        if (characterExpSlider != null)
            characterExpSlider.value = (float)currentExp / (float)requiredExp;
    }

    private void UpdateCharacterLevelDisplay(int level)
    {
        if (characterLevelText != null)
            characterLevelText.text = $"{level}";
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
            LoadCharacterIcon(characterId);

        var config = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var item in config.items)
            {
                if (item.id >= 3301 && item.id <= 3399)
                    LoadSkillIcon(item.id);
            }
        }

        Debug.Log($"[EquipPlayerView] LoadAllIcons 完成，缓存了 {iconCache.Count} 个图标");
    }

    private void LoadCharacterIcon(int characterId)
    {
        string path = $"UI/Icon/Equipment/Character/{characterId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            iconCache[characterId] = sprite;
    }

    private void LoadSkillIcon(int skillId)
    {
        string path = $"UI/Icon/Equipment/Skill/{skillId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            iconCache[skillId] = sprite;
    }

    private Sprite GetIcon(int id)
    {
        return iconCache.TryGetValue(id, out Sprite sprite) ? sprite : null;
    }

    private void LoadCharacterIds()
    {
        characterIds.Clear();
        var config = CharacterConfigListExtensions.LoadFromResources();
        if (config != null)
        {
            var ids = config.GetAllCharacterIds();
            foreach (var id in ids)
                characterIds.Add(id);
        }
    }

    public void Show()
    {
        Debug.Log("[EquipPlayerView] Show() called");

        NetServerManager.Instance?.SyncCharacterDataFromServer();

        int equippedCharacterId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);

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
            if (characterIds.Count == 0)
            {
                Debug.LogError("[EquipPlayerView] characterIds is EMPTY!");
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, characterIds.Count - 1));
            currentCharacterId = characterIds[currentIndex];

            // 获取背包数据
            var playerData = PlayerDataManager.Instance;
            Dictionary<int, int> inventory;

            if (playerData != null && playerData.IsReady)
            {
                inventory = playerData.GetInventory();
            }
            else
            {
                inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
            }

            int equippedChar = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);

            bool hasCharacter = inventory.ContainsKey(currentCharacterId);
            bool isEquipped = equippedChar == currentCharacterId;

            // ✅ 使用 UIUseStatus 更新人物状态
            if (characterStatus != null)
                characterStatus.SetStatus(hasCharacter, isEquipped);

            // 更新人物显示信息
            UpdateCharacterDisplayInfo();
            UpdatePageText();
        }
        finally
        {
            _isRefreshing = false;
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
                characterLevelText.text = $"{level}";

            int requiredExp = CommunicateEvent.Request<int, int>("CharacterManager_GetExpToNextLevel", 0);
            if (characterExpText != null)
                characterExpText.text = $"{playerData.currentExp}/{requiredExp}";
            if (characterExpSlider != null)
                characterExpSlider.value = (float)playerData.currentExp / (float)requiredExp;
        }
        else
        {
            if (characterLevelText != null)
                characterLevelText.text = $"{0}";
            if (characterExpText != null)
                characterExpText.text = $"{0}/{0}";
            if (characterExpSlider != null)
                characterExpSlider.value = (float)0 / (float)0;
        }
    }

    private void UpdateSkillDisplay()
    {
        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        int skill1Id = 0;
        int skill2Id = 0;

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
            if (skill1Status != null)
                skill1Status.SetStatus(UIUseStatus.Status.Locked);
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
            skill1NameText.text = LoadDataManager.Instance.GetComponentName(skillId);

        // ✅ 使用 UIUseStatus
        if (skill1Status != null)
        {
            bool hasSkill = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId);
            int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
            int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);
            bool isEquipped = equippedSkill1 == skillId || equippedSkill2 == skillId;

            skill1Status.SetStatus(hasSkill, isEquipped);
        }
    }

    private void UpdateSkill2Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill2Status != null)
                skill2Status.SetStatus(UIUseStatus.Status.Locked);
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
            skill2NameText.text = LoadDataManager.Instance.GetComponentName(skillId);

        // ✅ 使用 UIUseStatus
        if (skill2Status != null)
        {
            bool hasSkill = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId);
            int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
            int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);
            bool isEquipped = equippedSkill1 == skillId || equippedSkill2 == skillId;

            skill2Status.SetStatus(hasSkill, isEquipped);
        }
    }

    private void UpdatePageText()
    {
        if (pageText != null)
            pageText.text = $"{currentIndex + 1}/{Mathf.Max(1, characterIds.Count)}";
    }

    // ========== 按钮事件 ==========

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
            if (skillIds.skillId50 > 0)
                OpenAdForSkillUnlock(skillIds.skillId50, "解锁技能");
        }
    }

    private void OnSkill2UnlockClick()
    {
        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        if (characterConfig != null)
        {
            var skillIds = characterConfig.GetCharacterSkillIds(currentCharacterId);
            if (skillIds.skillId100 > 0)
                OpenAdForSkillUnlock(skillIds.skillId100, "解锁技能");
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
            PlayerAniManager.Instance.SwitchCharacter(characterId);
    }
}
