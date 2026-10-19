using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
//using SharedModels;

public class EquipPlayerEquipmentPanel : MonoBehaviour
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
        Z_Logger.Log("[EquipPlayerView] OnBagRefresh - 背包数据已更新，刷新装备视图");
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
        Z_Logger.Log($"[EquipPlayerView] OnEquipChanged - slotType={data.Item1}, itemId={data.Item2}");
        UpdateDisplay();
    }

    private void OnItemQuantityChanged((int, int) data)
    {
        Z_Logger.Log($"[EquipPlayerView] OnItemQuantityChanged - itemId={data.Item1}, quantity={data.Item2}");
        UpdateDisplay();
    }

    private void UpdateCharacterExpDisplay(int currentExp, int requiredExp)
    {
        if (characterExpText != null) characterExpText.text = $"{currentExp}/{requiredExp}";
        if (characterExpSlider != null && requiredExp > 0) characterExpSlider.value = (float)currentExp / requiredExp;
        else if (characterExpSlider != null) characterExpSlider.value = 0f;
    }

    private void UpdateCharacterLevelDisplay(int level)
    {
        if (characterLevelText != null) characterLevelText.text = $"{level}";
    }

    private void RefreshCharacterData()
    {
        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 default
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] EquipPlayerEquipmentPanel.RefreshCharacterData - ⚠️ NetServerManager 未初始化，人物数据可能为 0");
            UpdateCharacterLevelDisplay(0);
            UpdateCharacterExpDisplay(0, 0);
            return;
        }

        int level = RequestCharacterLevel();
        UpdateCharacterLevelDisplay(level);

        var charData = RequestPlayerData();
        if (charData != null)
        {
            int requiredExp = RequestExpToNextLevel();
            UpdateCharacterExpDisplay(charData.currentExp, requiredExp);

            cachedLevel = level;
            cachedCurrentExp = charData.currentExp;
            cachedRequiredExp = requiredExp;
            hasCachedData = true;
        }
        else
        {
            Z_Logger.LogWarning("[装备界面诊断] EquipPlayerEquipmentPanel.RefreshCharacterData - ⚠️ 获取人物数据失败");
        }
    }

    // ============================================================
    // 辅助 Request 方法（补上，之前遗漏）
    // ============================================================
    private int RequestCharacterLevel()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return 0;
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, 0);
    }

    private PlayerCharacterData RequestPlayerData()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return null;
        // ✅ 统一用 CharacterManager_* 前缀
        return CommunicateEvent.Request<int, PlayerCharacterData>("CharacterManager_GetPlayerData", 0);
    }

    private int RequestExpToNextLevel()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return 0;
        return CommunicateEvent.Request<int, int>("CharacterManager_GetExpToNextLevel", 0);
    }

    private int RequestEquipped(EquipmentSlotType slot)
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ 请求装备槽 {slot} 时 NetServerManager 未初始化，返回 0");
            return 0;
        }
        return CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slot);
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
        // ⚠️ 外部依赖：LoadDataManager 未加载完时组件名会是"未知组件"
        if (LoadDataManager.Instance == null || !LoadDataManager.Instance.isDataLoaded)
        {
            Z_Logger.LogWarning("[装备界面诊断] EquipPlayerEquipmentPanel.Init - ⚠️ LoadDataManager 尚未加载完成");
        }
        LoadAllIcons();
    }

    // ========== 异步加载方法 ==========

    private async void LoadAllIcons()
    {
        iconCache.Clear();

        // ✅ 先加载人物ID列表
        await LoadCharacterIdsAsync();

        // ✅ 加载人物图标
        foreach (int characterId in characterIds)
        {
            await LoadCharacterIconAsync(characterId);
        }

        // ✅ 加载技能图标
        var config = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var item in config.items)
            {
                if (item.id >= 3301 && item.id <= 3399)
                {
                    await LoadSkillIconAsync(item.id);
                }
            }
        }
        else Z_Logger.LogWarning("[装备界面诊断] EquipPlayerEquipmentPanel - fishing_components 配置加载失败，技能图标可能为空");

        Z_Logger.Log($"[装备界面诊断] EquipPlayerEquipmentPanel.LoadAllIcons 完成，缓存了 {iconCache.Count} 个图标");

        // 问题3：加载完补刷新
        if (this != null && gameObject.activeInHierarchy) UpdateDisplay();
    }

    private async Task LoadCharacterIdsAsync()
    {
        characterIds.Clear();
        var config = await CharacterConfigListExtensions.LoadFromAddressablesAsync("JsonData/BaseFramework/characters");
        if (config != null)
        {
            var ids = config.GetAllCharacterIds();
            foreach (var id in ids) characterIds.Add(id);
        }
        else Z_Logger.LogWarning("[装备界面诊断] EquipPlayerEquipmentPanel - characters 配置加载失败，人物列表将为空");
    }

    private async Task LoadCharacterIconAsync(int characterId)
    {
        string path = $"UI/Icon/Equipment/Character/{characterId}";
        var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
        if (sprite != null) iconCache[characterId] = sprite;
        else Z_Logger.LogWarning($"[装备界面诊断] EquipPlayerEquipmentPanel 人物图标加载失败: id={characterId}, path={path}");
        AssetManager.ReleaseAddressable(handle);
    }

    private async Task LoadSkillIconAsync(int skillId)
    {
        string path = $"UI/Icon/Equipment/Skill/{skillId}";
        var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
        if (sprite != null) iconCache[skillId] = sprite;
        else Z_Logger.LogWarning($"[装备界面诊断] EquipPlayerEquipmentPanel 技能图标加载失败: id={skillId}, path={path}");
        AssetManager.ReleaseAddressable(handle);
    }

    private Sprite GetIcon(int id)
    {
        return iconCache.TryGetValue(id, out Sprite sprite) ? sprite : null;
    }

    public void Show()
    {
        Z_Logger.Log("[EquipPlayerView] Show() called");

        NetServerManager.Instance?.SyncCharacterDataFromServer();

        int equippedCharacterId = RequestEquipped(EquipmentSlotType.Character);

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
                Z_Logger.LogError("[装备界面诊断] EquipPlayerEquipmentPanel.UpdateDisplay - characterIds 为空！");
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, characterIds.Count - 1));
            currentCharacterId = characterIds[currentIndex];

            // 获取背包数据
            Dictionary<int, int> inventory = null;
            var playerData = PlayerDataManager.Instance;

            if (playerData != null && playerData.IsReady)
            {
                inventory = playerData.GetInventory();
            }
            else
            {
                Z_Logger.LogWarning("[装备界面诊断] ⚠️ EquipPlayerEquipmentPanel.UpdateDisplay - PlayerDataManager 未就绪，降级走 EVENT_GET_INVENTORY");
                inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
            }

            int equippedChar = RequestEquipped(EquipmentSlotType.Character);

            // 问题10：inventory 空判
            bool hasCharacter = inventory != null && inventory.ContainsKey(currentCharacterId);
            bool isEquipped = equippedChar == currentCharacterId;

            // ✅ 使用 UIUseStatus 更新人物状态
            if (characterStatus != null) characterStatus.SetStatus(hasCharacter, isEquipped);

            UpdateCharacterDisplayInfo();
            UpdatePageText();
        }
        finally { _isRefreshing = false; }
    }

    private void UpdateCharacterDisplayInfo()
    {
        if (currentCharacterId <= 0) return;

        if (characterIcon != null)
        {
            Sprite icon = GetIcon(currentCharacterId);
            if (icon != null) { characterIcon.sprite = icon; characterIcon.color = Color.white; }
            else { characterIcon.sprite = null; characterIcon.color = new Color(1, 1, 1, 0); }
        }

        if (characterNameText != null)
        {
            string name = LoadDataManager.Instance?.GetComponentName(currentCharacterId) ?? $"未知({currentCharacterId})";
            if (name == "未知组件")
            {
                Z_Logger.LogWarning($"[装备界面诊断] EquipPlayerEquipmentPanel 人物名称为'未知组件', id={currentCharacterId}");
                name = $"未知({currentCharacterId})";
            }
            characterNameText.text = name;
        }

        UpdateCharacterLevelAndExp();
        UpdateSkillDisplay();
    }

    private void UpdateCharacterLevelAndExp()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ UpdateCharacterLevelAndExp 时 NetServerManager 未初始化，显示为 0");
            if (characterLevelText != null) characterLevelText.text = "0";
            if (characterExpText != null) characterExpText.text = "0/0";
            if (characterExpSlider != null) characterExpSlider.value = 0f;
            return;
        }

        // ✅ 统一用 CharacterManager_* 前缀
        var playerData = RequestPlayerData();
        if (playerData != null)
        {
            int level = playerData.currentLevel;
            if (characterLevelText != null) characterLevelText.text = $"{level}";

            int requiredExp = RequestExpToNextLevel();
            if (characterExpText != null) characterExpText.text = $"{playerData.currentExp}/{requiredExp}";
            if (characterExpSlider != null && requiredExp > 0) characterExpSlider.value = (float)playerData.currentExp / requiredExp;
        }
        else
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ UpdateCharacterLevelAndExp 获取人物数据失败，显示为 0");
            if (characterLevelText != null) characterLevelText.text = "0";
            if (characterExpText != null) characterExpText.text = "0/0";
            if (characterExpSlider != null) characterExpSlider.value = 0f;
        }
    }

    // 问题11：加请求令牌，翻页时旧回调丢弃
    private async void UpdateSkillDisplay()
    {
        int requestCharacterId = currentCharacterId;

        var characterConfig = await CharacterConfigListExtensions.LoadFromAddressablesAsync("JsonData/BaseFramework/characters");
        // ✅ 翻页了，丢弃旧结果
        if (requestCharacterId != currentCharacterId) return;

        int skill1Id = 0;
        int skill2Id = 0;

        if (characterConfig != null)
        {
            (skill1Id, skill2Id) = characterConfig.GetCharacterSkillIds(currentCharacterId);
        }
        else
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ UpdateSkillDisplay 加载 characters 配置失败, characterId={requestCharacterId}");
        }

        UpdateSkill1Display(skill1Id);
        UpdateSkill2Display(skill2Id);
    }

    private void UpdateSkill1Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill1Status != null) skill1Status.SetStatus(UIUseStatus.Status.Locked);
            if (skill1NameText != null) skill1NameText.text = "";
            return;
        }

        if (skill1Icon != null)
        {
            Sprite icon = GetIcon(skillId);
            if (icon != null) { skill1Icon.sprite = icon; skill1Icon.color = Color.white; }
            else { skill1Icon.sprite = null; skill1Icon.color = new Color(1, 1, 1, 0); }
        }

        if (skill1NameText != null)
        {
            string name = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";
            if (name == "未知组件") Z_Logger.LogWarning($"[装备界面诊断] 技能1名称为'未知组件', id={skillId}");
            skill1NameText.text = name;
        }

        // ✅ 使用 UIUseStatus
        if (skill1Status != null)
        {
            if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
            {
                Z_Logger.LogWarning("[装备界面诊断] ⚠️ 技能1状态判断时 NetServerManager 未初始化，显示为 Locked");
                skill1Status.SetStatus(false, false);
                return;
            }

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
            if (skill2Status != null) skill2Status.SetStatus(UIUseStatus.Status.Locked);
            if (skill2NameText != null) skill2NameText.text = "";
            return;
        }

        if (skill2Icon != null)
        {
            Sprite icon = GetIcon(skillId);
            if (icon != null) { skill2Icon.sprite = icon; skill2Icon.color = Color.white; }
            else { skill2Icon.sprite = null; skill2Icon.color = new Color(1, 1, 1, 0); }
        }

        if (skill2NameText != null)
        {
            string name = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";
            if (name == "未知组件") Z_Logger.LogWarning($"[装备界面诊断] 技能2名称为'未知组件', id={skillId}");
            skill2NameText.text = name;
        }

        if (skill2Status != null)
        {
            if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
            {
                Z_Logger.LogWarning("[装备界面诊断] ⚠️ 技能2状态判断时 NetServerManager 未初始化，显示为 Locked");
                skill2Status.SetStatus(false, false);
                return;
            }

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

    private void OnMaskClick() { callback?.Invoke("Back", null); }
    private void OnCloseClick() { callback?.Invoke("Back", null); }

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

    private async void OnSkill1UnlockClick()
    {
        var characterConfig = await CharacterConfigListExtensions.LoadFromAddressablesAsync("JsonData/BaseFramework/characters");
        if (characterConfig != null)
        {
            var skillIds = characterConfig.GetCharacterSkillIds(currentCharacterId);
            if (skillIds.skillId50 > 0)
                OpenAdForSkillUnlock(skillIds.skillId50, "解锁技能");
        }
    }

    private async void OnSkill2UnlockClick()
    {
        var characterConfig = await CharacterConfigListExtensions.LoadFromAddressablesAsync("JsonData/BaseFramework/characters");
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
        string componentName = LoadDataManager.Instance?.GetComponentName(currentCharacterId) ?? $"未知({currentCharacterId})";
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
        Z_Logger.Log($"[EquipPlayerView] OnEquipClick - currentCharacterId={currentCharacterId}");

        // ✅ 走新版带回调接口，服务器返回完整 equipment 数据后刷新
        if (NetServerManager.Instance != null)
        {
            GameUIManager.Instance?.ShowTip($"正在装备...");
            NetServerManager.Instance.EquipItemWithCallback(EquipmentSlotType.Character, currentCharacterId, (success, message) =>
            {
                if (success)
                {
                    RefreshPlayerAnimation(currentCharacterId);
                    UpdateDisplay();
                    string componentName = LoadDataManager.Instance?.GetComponentName(currentCharacterId) ?? $"未知({currentCharacterId})";
                    string successInfo = componentName != "未知组件" ? $"已装备 {componentName}！" : "已装备人物！";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
                    // ✅ 兜底刷新（EVENT_EQUIP_CHANGED 类型不匹配时也能生效）
                    callback?.Invoke("RefreshAllViews", null);
                }
                else
                {
                    Z_Logger.LogWarning($"[装备界面诊断] EquipPlayerEquipmentPanel 装备失败: {message}");
                    GameUIManager.ShowWarningMessage(string.IsNullOrEmpty(message) ? "装备失败" : message);
                }
            });
        }
        else
        {
            Z_Logger.LogError("[装备界面诊断] ⚠️ NetServerManager 为空，无法装备人物");
        }
    }

    private void RefreshPlayerAnimation(int characterId)
    {
        if (PlayerAniManager.Instance != null)
            PlayerAniManager.Instance.SwitchCharacter(characterId);
    }
}
