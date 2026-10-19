using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
//using SharedModels;

public class MainEquipmentPanel : MonoBehaviour
{
    public Button fishingRodBtn;
    public Image fishingRodIcon;
    public Text fishingRodName;
    public Image fishingRodLevelIcon;
    public GameObject fishingRodEquippedObj;
    public GameObject fishingRodUnequippedObj;

    public Button fishingLineBtn;
    public Image fishingLineIcon;
    public Text fishingLineName;
    public Image fishingLineLevelIcon;
    public GameObject fishingLineEquippedObj;
    public GameObject fishingLineUnequippedObj;

    public Button fishingHookBtn;
    public Image fishingHookIcon;
    public Text fishingHookName;
    public Image fishingHookLevelIcon;
    public GameObject fishingHookEquippedObj;
    public GameObject fishingHookUnequippedObj;

    public Button skill1Btn;
    public Image skill1Icon;
    public Text skill1Name;
    public Image skill1LevelIcon;
    public GameObject skill1EquippedObj;
    public GameObject skill1UnequippedObj;
    public GameObject skill1LockedObj;

    public Button skill2Btn;
    public Image skill2Icon;
    public Text skill2Name;
    public Image skill2LevelIcon;
    public GameObject skill2EquippedObj;
    public GameObject skill2UnequippedObj;
    public GameObject skill2LockedObj;

    public Button characterBtn;
    public Image characterIcon;
    public Text characterName;
    public Text characterLevelText;
    public Text characterExpText;
    public Slider characterSlider;
    public GameObject characterEquippedObj;
    public GameObject characterUnequippedObj;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private Dictionary<int, Sprite> levelIconCache = new Dictionary<int, Sprite>();
    private System.Action<string, object[]> callback;

    // 缓存的人物数据
    private int cachedLevel;
    private int cachedCurrentExp;
    private int cachedRequiredExp;
    private bool hasCachedData = false;

    void Start()
    {
        if (fishingRodBtn != null) fishingRodBtn.onClick.AddListener(OnFishingRodClick);
        if (fishingLineBtn != null) fishingLineBtn.onClick.AddListener(OnFishingLineClick);
        if (fishingHookBtn != null) fishingHookBtn.onClick.AddListener(OnFishingHookClick);
        if (skill1Btn != null) skill1Btn.onClick.AddListener(() => OnSkillClick(1));
        if (skill2Btn != null) skill2Btn.onClick.AddListener(() => OnSkillClick(2));
        if (characterBtn != null) characterBtn.onClick.AddListener(OnCharacterClick);

        BindUnequippedObjClickEvents();
    }

    private void BindUnequippedObjClickEvents()
    {
        AddButtonToObj(fishingRodUnequippedObj, OnFishingRodClick);
        AddButtonToObj(fishingLineUnequippedObj, OnFishingLineClick);
        AddButtonToObj(fishingHookUnequippedObj, OnFishingHookClick);
        AddButtonToObj(skill1UnequippedObj, () => OnSkillClick(1));
        AddButtonToObj(skill2UnequippedObj, () => OnSkillClick(2));
        AddButtonToObj(skill1LockedObj, () => OnSkillClick(1));
        AddButtonToObj(skill2LockedObj, () => OnSkillClick(2));
        AddButtonToObj(characterUnequippedObj, OnCharacterClick);
    }

    private void AddButtonToObj(GameObject obj, UnityEngine.Events.UnityAction onClick)
    {
        if (obj == null) return;

        Button btn = obj.GetComponent<Button>();
        if (btn == null)
        {
            btn = obj.AddComponent<Button>();
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
    }

    void OnEnable()
    {
        RegisterCharacterEvents();

        // 主动请求一次人物数据，确保显示最新状态
        RefreshCharacterData();
    }

    void OnDisable()
    {
        UnregisterCharacterEvents();
    }

    void OnDestroy()
    {
        UnregisterCharacterEvents();
    }

    private void RegisterCharacterEvents()
    {
        // 使用 CommunicateEvent 订阅人物数据变更事件
        CommunicateEvent.Register<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
    }

    private void UnregisterCharacterEvents()
    {
        // 取消订阅 CommunicateEvent 事件
        CommunicateEvent.Unregister<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);
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

    private void OnCharacterExpChanged(int currentExp, int requiredExp)
    {
        UpdateCharacterExpDisplay(currentExp, requiredExp);
    }

    private void OnCharacterLevelChanged()
    {
        UpdateCharacterLevelDisplay();
    }

    private void UpdateCharacterExpDisplay(int currentExp, int requiredExp)
    {
        if (characterExpText != null)
        {
            characterExpText.text = $"{currentExp}/{requiredExp}";
        }

        if (characterSlider != null)
        {
            characterSlider.value = requiredExp > 0 ? (float)currentExp / requiredExp : 0f;
        }
    }

    private void UpdateCharacterLevelDisplay()
    {
        int level = RequestCharacterLevel();
        UpdateCharacterLevelDisplay(level);
    }

    private void UpdateCharacterLevelDisplay(int level)
    {
        if (characterLevelText != null)
        {
            characterLevelText.text = $"{level}";
        }
    }

    /// <summary>主动刷新人物数据</summary>
    private void RefreshCharacterData()
    {
        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 default
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] MainEquipmentPanel.RefreshCharacterData - ⚠️ NetServerManager 未初始化，人物数据可能为 0");
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
            Z_Logger.LogWarning("[装备界面诊断] MainEquipmentPanel.RefreshCharacterData - ⚠️ 获取人物数据失败，等级/经验显示可能为 0");
        }
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
        // ⚠️ 外部依赖：LoadDataManager 未加载完时，GetComponentName 会返回"未知组件"
        if (LoadDataManager.Instance == null || !LoadDataManager.Instance.isDataLoaded)
        {
            Z_Logger.LogWarning("[装备界面诊断] MainEquipmentPanel.Init - ⚠️ LoadDataManager 尚未加载完成，组件名称可能显示为'未知组件'");
        }
        LoadAllIcons();
    }

    // 问题3：加载完主动刷新
    private async void LoadAllIcons()
    {
        iconCache.Clear();
        levelIconCache.Clear();

        // ✅ 从 Addressables 异步加载钓鱼组件配置
        var fishingConfig = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (fishingConfig != null)
        {
            var iconPaths = fishingConfig.GetAllIconPaths();
            foreach (var kvp in iconPaths)
            {
                string path = kvp.Value;
                var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
                if (sprite != null) iconCache[kvp.Key] = sprite;
                else Z_Logger.LogWarning($"[装备界面诊断] MainEquipmentPanel 图标加载失败: id={kvp.Key}, path={path}");
                AssetManager.ReleaseAddressable(handle);
            }
        }
        else Z_Logger.LogWarning("[装备界面诊断] MainEquipmentPanel - fishing_components 配置加载失败");

        // ✅ 从 Addressables 异步加载人物配置
        var characterConfig = await CharacterConfigListExtensions.LoadFromAddressablesAsync("JsonData/BaseFramework/characters");
        if (characterConfig != null)
        {
            var characterIds = characterConfig.GetAllCharacterIds();
            foreach (var id in characterIds)
            {
                string path = $"UI/Icon/Equipment/Character/{id}";
                var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
                if (sprite != null) iconCache[id] = sprite;
                else Z_Logger.LogWarning($"[装备界面诊断] MainEquipmentPanel 人物图标加载失败: id={id}, path={path}");
                AssetManager.ReleaseAddressable(handle);
            }
        }

        // ✅ 加载等级图标
        for (int i = 1; i <= 10; i++)
        {
            string path = $"UI/Icon/Equipment/Level/{i}";
            var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
            if (sprite != null) levelIconCache[i] = sprite;
            else Z_Logger.LogWarning($"[装备界面诊断] MainEquipmentPanel 等级图标加载失败: level={i}, path={path}");
            AssetManager.ReleaseAddressable(handle);
        }

        Z_Logger.Log($"[装备界面诊断] MainEquipmentPanel 图标加载完成: icon={iconCache.Count}, levelIcon={levelIconCache.Count}");

        // 问题3：加载完补一次刷新，避免首次打开图标全空
        if (this != null && gameObject.activeInHierarchy) UpdateDisplay();
    }

    private Sprite GetLevelIcon(int level)
    {
        if (levelIconCache.TryGetValue(level, out Sprite sprite)) return sprite;
        return null;
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite)) return sprite;
        return null;
    }

    public void Show()
    {
        UpdateDisplay();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateDisplay()
    {
        int rodId = RequestEquipped(EquipmentSlotType.FishingRod);
        int lineId = RequestEquipped(EquipmentSlotType.FishingLine);
        int hookId = RequestEquipped(EquipmentSlotType.FishingHook);
        int skill1Id = RequestEquipped(EquipmentSlotType.Skill1);
        int skill2Id = RequestEquipped(EquipmentSlotType.Skill2);
        int characterId = RequestEquipped(EquipmentSlotType.Character);

        UpdateFishingRodDisplay(rodId);
        UpdateFishingLineDisplay(lineId);
        UpdateFishingHookDisplay(hookId);
        UpdateSkill1Display(skill1Id);
        UpdateSkill2Display(skill2Id);
        UpdateCharacterDisplay(characterId);
    }

    private int RequestEquipped(EquipmentSlotType slot)
    {
        // ⚠️ 外部依赖：NetServerManager 未注册时 Request 返回 0，装备状态全部显示"未装备"
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ 请求装备槽 {slot} 时 NetServerManager 未初始化，返回 0（显示为未装备）");
            return 0;
        }
        return CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slot);
    }

    private int RequestLevel(int id)
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ 请求等级时 NetServerManager 未初始化，id={id} 返回 1");
            return 1;
        }
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, id);
    }

    private string RequestName(int id)
    {
        var name = LoadDataManager.Instance?.GetComponentName(id);
        if (string.IsNullOrEmpty(name) || name == "未知组件")
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ GetComponentName({id}) 返回'未知组件'，LoadDataManager.isDataLoaded={LoadDataManager.Instance?.isDataLoaded}");
            return $"未知({id})";
        }
        return name;
    }

    private int RequestCharacterLevel()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return 0;
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, 0);
    }

    private PlayerCharacterData RequestPlayerData()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return null;
        // ✅ 统一使用 CharacterManager_* 前缀（NetServerManager 里两套 key 都注册了，选一套避免混淆）
        return CommunicateEvent.Request<int, PlayerCharacterData>("CharacterManager_GetPlayerData", 0);
    }

    private int RequestExpToNextLevel()
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return 0;
        return CommunicateEvent.Request<int, int>("CharacterManager_GetExpToNextLevel", 0);
    }

    /// <summary>图标找不到时明确清空，避免残留旧 sprite</summary>
    private void SetIconOrClear(Image img, int id)
    {
        if (img == null) return;
        var sprite = GetIcon(id);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(1, 1, 1, 0);
        }
    }

    private void SetLevelIconOrHide(Image img, int level)
    {
        if (img == null) return;
        var levelIcon = GetLevelIcon(level);
        if (levelIcon != null)
        {
            img.sprite = levelIcon;
            img.color = Color.white;
            img.gameObject.SetActive(true);
        }
        else
        {
            img.gameObject.SetActive(false);
        }
    }

    private void UpdateFishingRodDisplay(int rodId)
    {
        bool isEquipped = rodId > 0;

        if (fishingRodEquippedObj != null) fishingRodEquippedObj.SetActive(isEquipped);
        if (fishingRodUnequippedObj != null) fishingRodUnequippedObj.SetActive(!isEquipped);

        if (rodId <= 0) return;

        SetIconOrClear(fishingRodIcon, rodId);

        if (fishingRodName != null)
        {
            int level = RequestLevel(rodId);
            fishingRodName.text = $"{RequestName(rodId)} Lv.{level}";
        }

        SetLevelIconOrHide(fishingRodLevelIcon, RequestLevel(rodId));
    }

    private void UpdateFishingLineDisplay(int lineId)
    {
        bool isEquipped = lineId > 0;

        if (fishingLineEquippedObj != null) fishingLineEquippedObj.SetActive(isEquipped);
        if (fishingLineUnequippedObj != null) fishingLineUnequippedObj.SetActive(!isEquipped);

        if (lineId <= 0) return;

        SetIconOrClear(fishingLineIcon, lineId);

        if (fishingLineName != null)
        {
            int level = RequestLevel(lineId);
            fishingLineName.text = $"{RequestName(lineId)} Lv.{level}";
        }

        SetLevelIconOrHide(fishingLineLevelIcon, RequestLevel(lineId));
    }

    private void UpdateFishingHookDisplay(int hookId)
    {
        bool isEquipped = hookId > 0;

        if (fishingHookEquippedObj != null) fishingHookEquippedObj.SetActive(isEquipped);
        if (fishingHookUnequippedObj != null) fishingHookUnequippedObj.SetActive(!isEquipped);

        if (hookId <= 0) return;

        SetIconOrClear(fishingHookIcon, hookId);

        if (fishingHookName != null)
        {
            int level = RequestLevel(hookId);
            fishingHookName.text = $"{RequestName(hookId)} Lv.{level}";
        }

        SetLevelIconOrHide(fishingHookLevelIcon, RequestLevel(hookId));
    }

    private void UpdateSkill1Display(int skillId)
    {
        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 false
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ 技能1判断时 NetServerManager 未初始化，显示为未解锁");
            if (skill1LockedObj != null) skill1LockedObj.SetActive(true);
            if (skill1EquippedObj != null) skill1EquippedObj.SetActive(false);
            if (skill1UnequippedObj != null) skill1UnequippedObj.SetActive(false);
            if (skill1Name != null) skill1Name.text = "未知";
            return;
        }

        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", 1);

        if (skill1LockedObj != null) skill1LockedObj.SetActive(!isSlotUnlocked);

        if (!isSlotUnlocked)
        {
            if (skill1EquippedObj != null) skill1EquippedObj.SetActive(false);
            if (skill1UnequippedObj != null) skill1UnequippedObj.SetActive(false);
            if (skill1Name != null) skill1Name.text = "未解锁";
            return;
        }

        bool isEquipped = skillId > 0;

        if (skill1EquippedObj != null) skill1EquippedObj.SetActive(isEquipped);
        if (skill1UnequippedObj != null) skill1UnequippedObj.SetActive(!isEquipped);

        if (skillId <= 0)
        {
            if (skill1Name != null)
            {
                skill1Name.text = LoadDataManager.Instance?.GetEquipmentUIText("notEquipped") ?? "未装备";
            }
            return;
        }

        SetIconOrClear(skill1Icon, skillId);

        if (skill1Name != null)
        {
            int level = RequestLevel(skillId);
            skill1Name.text = $"{RequestName(skillId)} Lv.{level}";
        }

        SetLevelIconOrHide(skill1LevelIcon, RequestLevel(skillId));
    }

    private void UpdateSkill2Display(int skillId)
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ 技能2判断时 NetServerManager 未初始化，显示为未解锁");
            if (skill2LockedObj != null) skill2LockedObj.SetActive(true);
            if (skill2EquippedObj != null) skill2EquippedObj.SetActive(false);
            if (skill2UnequippedObj != null) skill2UnequippedObj.SetActive(false);
            if (skill2Name != null) skill2Name.text = "未知";
            return;
        }

        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", 2);

        if (skill2LockedObj != null) skill2LockedObj.SetActive(!isSlotUnlocked);

        if (!isSlotUnlocked)
        {
            if (skill2EquippedObj != null) skill2EquippedObj.SetActive(false);
            if (skill2UnequippedObj != null) skill2UnequippedObj.SetActive(false);
            if (skill2Name != null) skill2Name.text = "未解锁";
            return;
        }

        bool isEquipped = skillId > 0;

        if (skill2EquippedObj != null) skill2EquippedObj.SetActive(isEquipped);
        if (skill2UnequippedObj != null) skill2UnequippedObj.SetActive(!isEquipped);

        if (skillId <= 0)
        {
            if (skill2Name != null)
            {
                skill2Name.text = LoadDataManager.Instance?.GetEquipmentUIText("notEquipped") ?? "未装备";
            }
            return;
        }

        SetIconOrClear(skill2Icon, skillId);

        if (skill2Name != null)
        {
            int level = RequestLevel(skillId);
            skill2Name.text = $"{RequestName(skillId)} Lv.{level}";
        }

        SetLevelIconOrHide(skill2LevelIcon, RequestLevel(skillId));
    }

    private void UpdateCharacterDisplay(int characterId)
    {
        bool isEquipped = characterId > 0;

        if (characterEquippedObj != null) characterEquippedObj.SetActive(isEquipped);
        if (characterUnequippedObj != null) characterUnequippedObj.SetActive(!isEquipped);

        if (characterId <= 0) return;

        SetIconOrClear(characterIcon, characterId);

        if (characterName != null)
        {
            characterName.text = RequestName(characterId);
        }

        var playerData = RequestPlayerData();
        if (playerData != null)
        {
            if (characterLevelText != null) characterLevelText.text = $"{playerData.currentLevel}";

            int requiredExp = RequestExpToNextLevel();
            if (characterExpText != null) characterExpText.text = $"{playerData.currentExp}/{requiredExp}";
            if (characterSlider != null && requiredExp > 0) characterSlider.value = (float)playerData.currentExp / requiredExp;
        }
        else
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ UpdateCharacterDisplay 获取人物数据失败，等级/经验显示为 0");
            if (characterLevelText != null) characterLevelText.text = "0";
            if (characterExpText != null) characterExpText.text = "0/0";
            if (characterSlider != null) characterSlider.value = 0f;
        }
    }

    private void OnFishingRodClick()
    {
        Z_Logger.Log("[MainEquipmentView] OnFishingRodClick - 点击钓竿按钮");
        callback?.Invoke("OpenFishingRod", null);
    }

    private void OnFishingLineClick()
    {
        Z_Logger.Log("[MainEquipmentView] OnFishingLineClick - 点击钓线按钮");
        callback?.Invoke("OpenFishingLine", null);
    }

    private void OnFishingHookClick()
    {
        Z_Logger.Log("[MainEquipmentView] OnFishingHookClick - 点击钓钩按钮");
        callback?.Invoke("OpenFishingHook", null);
    }

    private void OnSkillClick(int skillSlot)
    {
        Z_Logger.Log($"[MainEquipmentView] OnSkillClick - 点击技能按钮, skillSlot={skillSlot}");

        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogError("[装备界面诊断] ⚠️ NetServerManager 未初始化，技能槽点击无响应");
            return;
        }

        // 检查技能槽位是否已解锁
        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", skillSlot);
        if (!isSlotUnlocked)
        {
            Z_Logger.Log($"[MainEquipmentView] OnSkillClick - 技能槽位 {skillSlot} 未解锁，触发看广告解锁");
            string slotName = skillSlot == 1 ? "技能1" : "技能2";
            string info = $"看广告解锁{slotName}槽位";
            callback?.Invoke("OpenAd", new object[] { info, skillSlot, "看广告解锁", (System.Action)(() =>
            {
                Z_Logger.Log($"[MainEquipmentView] 看广告解锁技能槽位回调执行 - slot={skillSlot}");
                if (NetServerManager.Instance != null)
                {
                    NetServerManager.Instance.UnlockSkillSlot(skillSlot, (success) =>
                    {
                        if (success)
                        {
                            Z_Logger.Log($"[MainEquipmentView] 技能槽位 {skillSlot} 解锁成功");
                            UpdateDisplay();
                            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"{slotName}槽位解锁成功！");
                        }
                        else
                        {
                            Z_Logger.LogWarning($"[MainEquipmentView] 技能槽位 {skillSlot} 解锁失败");
                            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "解锁失败，请重试");
                        }
                    });
                }
            })});
            return;
        }

        callback?.Invoke("OpenSkill", new object[] { skillSlot });
    }

    private void OnCharacterClick()
    {
        Z_Logger.Log("[MainEquipmentView] OnCharacterClick - 点击人物按钮");
        callback?.Invoke("OpenCharacter", null);
    }
}
