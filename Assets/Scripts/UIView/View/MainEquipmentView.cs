using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
//using SharedModels;

public class MainEquipmentView : MonoBehaviour
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
            characterSlider.value =  (float)currentExp/ (float)requiredExp;
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
            characterLevelText.text = $"{level}";
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
        LoadAllIcons();
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();
        levelIconCache.Clear();

        var fishingConfig = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
        if (fishingConfig != null)
        {
            var iconPaths = fishingConfig.GetAllIconPaths();
            foreach (var kvp in iconPaths)
            {
                string path = kvp.Value;
                Sprite sprite = AssetManager.LoadFromResources<Sprite>(path);
                if (sprite != null)
                {
                    iconCache[kvp.Key] = sprite;
                }
            }
        }

        var characterConfig = CharacterConfigListExtensions.LoadFromResources();
        if (characterConfig != null)
        {
            var characterIds = characterConfig.GetAllCharacterIds();
            foreach (var id in characterIds)
            {
                string path = $"UI/Icon/Equipment/Character/{id}";
                Sprite sprite = AssetManager.LoadFromResources<Sprite>(path);
                if (sprite != null)
                {
                    iconCache[id] = sprite;
                }
            }
        }

        for (int i = 1; i <= 10; i++)
        {
            string path = $"UI/Icon/Equipment/Level/{i}";
            Sprite sprite = AssetManager.LoadFromResources<Sprite>(path);
            if (sprite != null)
            {
                levelIconCache[i] = sprite;
            }
        }
    }

    private Sprite GetLevelIcon(int level)
    {
        if (levelIconCache.TryGetValue(level, out Sprite sprite))
        {
            return sprite;
        }
        return null;
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
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
        int rodId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.FishingRod);
        int lineId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.FishingLine);
        int hookId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.FishingHook);
        int skill1Id = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
        int skill2Id = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);
        int characterId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Character);

        UpdateFishingRodDisplay(rodId);
        UpdateFishingLineDisplay(lineId);
        UpdateFishingHookDisplay(hookId);
        UpdateSkill1Display(skill1Id);
        UpdateSkill2Display(skill2Id);
        UpdateCharacterDisplay(characterId);
    }

    private void UpdateFishingRodDisplay(int rodId)
    {
        bool isEquipped = rodId > 0;

        if (fishingRodEquippedObj != null)
        {
            fishingRodEquippedObj.SetActive(isEquipped);
        }
        if (fishingRodUnequippedObj != null)
        {
            fishingRodUnequippedObj.SetActive(!isEquipped);
        }

        if (rodId <= 0) return;

        if (fishingRodIcon != null)
        {
            Sprite icon = GetIcon(rodId);
            if (icon != null)
            {
                fishingRodIcon.sprite = icon;
                fishingRodIcon.color = Color.white;
            }
        }

        if (fishingRodName != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, rodId);
            string name = LoadDataManager.Instance.GetComponentName(rodId);
            fishingRodName.text = $"{name} Lv.{level}";
        }

        if (fishingRodLevelIcon != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, rodId);
            Sprite levelIcon = GetLevelIcon(level);
            if (levelIcon != null)
            {
                fishingRodLevelIcon.sprite = levelIcon;
                fishingRodLevelIcon.color = Color.white;
                fishingRodLevelIcon.gameObject.SetActive(true);
            }
            else
            {
                fishingRodLevelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateFishingLineDisplay(int lineId)
    {
        bool isEquipped = lineId > 0;

        if (fishingLineEquippedObj != null)
        {
            fishingLineEquippedObj.SetActive(isEquipped);
        }
        if (fishingLineUnequippedObj != null)
        {
            fishingLineUnequippedObj.SetActive(!isEquipped);
        }

        if (lineId <= 0) return;

        if (fishingLineIcon != null)
        {
            Sprite icon = GetIcon(lineId);
            if (icon != null)
            {
                fishingLineIcon.sprite = icon;
                fishingLineIcon.color = Color.white;
            }
        }

        if (fishingLineName != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, lineId);
            string name = LoadDataManager.Instance.GetComponentName(lineId);
            fishingLineName.text = $"{name} Lv.{level}";
        }

        if (fishingLineLevelIcon != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, lineId);
            Sprite levelIcon = GetLevelIcon(level);
            if (levelIcon != null)
            {
                fishingLineLevelIcon.sprite = levelIcon;
                fishingLineLevelIcon.color = Color.white;
                fishingLineLevelIcon.gameObject.SetActive(true);
            }
            else
            {
                fishingLineLevelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateFishingHookDisplay(int hookId)
    {
        bool isEquipped = hookId > 0;

        if (fishingHookEquippedObj != null)
        {
            fishingHookEquippedObj.SetActive(isEquipped);
        }
        if (fishingHookUnequippedObj != null)
        {
            fishingHookUnequippedObj.SetActive(!isEquipped);
        }

        if (hookId <= 0) return;

        if (fishingHookIcon != null)
        {
            Sprite icon = GetIcon(hookId);
            if (icon != null)
            {
                fishingHookIcon.sprite = icon;
                fishingHookIcon.color = Color.white;
            }
        }

        if (fishingHookName != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, hookId);
            string name = LoadDataManager.Instance.GetComponentName(hookId);
            fishingHookName.text = $"{name} Lv.{level}";
        }

        if (fishingHookLevelIcon != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, hookId);
            Sprite levelIcon = GetLevelIcon(level);
            if (levelIcon != null)
            {
                fishingHookLevelIcon.sprite = levelIcon;
                fishingHookLevelIcon.color = Color.white;
                fishingHookLevelIcon.gameObject.SetActive(true);
            }
            else
            {
                fishingHookLevelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateSkill1Display(int skillId)
    {
        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", 1);

        if (skill1LockedObj != null)
            skill1LockedObj.SetActive(!isSlotUnlocked);

        if (!isSlotUnlocked)
        {
            if (skill1EquippedObj != null) skill1EquippedObj.SetActive(false);
            if (skill1UnequippedObj != null) skill1UnequippedObj.SetActive(false);
            if (skill1Name != null) skill1Name.text = "未解锁";
            return;
        }

        bool isEquipped = skillId > 0;

        if (skill1EquippedObj != null)
        {
            skill1EquippedObj.SetActive(isEquipped);
        }
        if (skill1UnequippedObj != null)
        {
            skill1UnequippedObj.SetActive(!isEquipped);
        }

        if (skillId <= 0)
        {
            if (skill1Name != null)
            {
                skill1Name.text = LoadDataManager.Instance.GetEquipmentUIText("notEquipped");
            }
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

        if (skill1Name != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
            string name = LoadDataManager.Instance.GetComponentName(skillId);
            skill1Name.text = $"{name} Lv.{level}";
        }

        if (skill1LevelIcon != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
            Sprite levelIcon = GetLevelIcon(level);
            if (levelIcon != null)
            {
                skill1LevelIcon.sprite = levelIcon;
                skill1LevelIcon.color = Color.white;
                skill1LevelIcon.gameObject.SetActive(true);
            }
            else
            {
                skill1LevelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateSkill2Display(int skillId)
    {
        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", 2);

        if (skill2LockedObj != null)
            skill2LockedObj.SetActive(!isSlotUnlocked);

        if (!isSlotUnlocked)
        {
            if (skill2EquippedObj != null) skill2EquippedObj.SetActive(false);
            if (skill2UnequippedObj != null) skill2UnequippedObj.SetActive(false);
            if (skill2Name != null) skill2Name.text = "未解锁";
            return;
        }

        bool isEquipped = skillId > 0;

        if (skill2EquippedObj != null)
        {
            skill2EquippedObj.SetActive(isEquipped);
        }
        if (skill2UnequippedObj != null)
        {
            skill2UnequippedObj.SetActive(!isEquipped);
        }

        if (skillId <= 0)
        {
            if (skill2Name != null)
            {
                skill2Name.text = LoadDataManager.Instance.GetEquipmentUIText("notEquipped");
            }
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

        if (skill2Name != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
            string name = LoadDataManager.Instance.GetComponentName(skillId);
            skill2Name.text = $"{name} Lv.{level}";
        }

        if (skill2LevelIcon != null)
        {
            int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
            Sprite levelIcon = GetLevelIcon(level);
            if (levelIcon != null)
            {
                skill2LevelIcon.sprite = levelIcon;
                skill2LevelIcon.color = Color.white;
                skill2LevelIcon.gameObject.SetActive(true);
            }
            else
            {
                skill2LevelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateCharacterDisplay(int characterId)
    {
        bool isEquipped = characterId > 0;

        if (characterEquippedObj != null)
        {
            characterEquippedObj.SetActive(isEquipped);
        }
        if (characterUnequippedObj != null)
        {
            characterUnequippedObj.SetActive(!isEquipped);
        }

        if (characterId <= 0) return;

        if (characterIcon != null)
        {
            Sprite icon = GetIcon(characterId);
            if (icon != null)
            {
                characterIcon.sprite = icon;
                characterIcon.color = Color.white;
            }
        }

        if (characterName != null)
        {
            characterName.text = LoadDataManager.Instance.GetComponentName(characterId);
        }

        var playerData = CommunicateEvent.Request<int, PlayerCharacterData>("CharacterManager_GetPlayerData", 0);
        if (playerData != null)
        {
            if (characterLevelText != null)
            {
                characterLevelText.text = $"{playerData.currentLevel}";
            }

            int requiredExp = CommunicateEvent.Request<int, int>("CharacterManager_GetExpToNextLevel", 0);
            if (characterExpText != null)
            {
                characterExpText.text = $"{playerData.currentExp}/{requiredExp}";
            }
        }
    }

    private void OnFishingRodClick()
    {
        Debug.Log("[MainEquipmentView] OnFishingRodClick - 点击钓竿按钮");
        callback?.Invoke("OpenFishingRod", null);
    }

    private void OnFishingLineClick()
    {
        Debug.Log("[MainEquipmentView] OnFishingLineClick - 点击钓线按钮");
        callback?.Invoke("OpenFishingLine", null);
    }

    private void OnFishingHookClick()
    {
        Debug.Log("[MainEquipmentView] OnFishingHookClick - 点击钓钩按钮");
        callback?.Invoke("OpenFishingHook", null);
    }

    private void OnSkillClick(int skillSlot)
    {
        Debug.Log($"[MainEquipmentView] OnSkillClick - 点击技能按钮, skillSlot={skillSlot}");

        // 检查技能槽位是否已解锁
        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", skillSlot);
        if (!isSlotUnlocked)
        {
            Debug.Log($"[MainEquipmentView] OnSkillClick - 技能槽位 {skillSlot} 未解锁，触发看广告解锁");
            string slotName = skillSlot == 1 ? "技能1" : "技能2";
            string info = $"看广告解锁{slotName}槽位";
            callback?.Invoke("OpenAd", new object[] { info, skillSlot, "看广告解锁", (System.Action)(() =>
            {
                Debug.Log($"[MainEquipmentView] 看广告解锁技能槽位回调执行 - slot={skillSlot}");
                if (NetServerManager.Instance != null)
                {
                    NetServerManager.Instance.UnlockSkillSlot(skillSlot, (success) =>
                    {
                        if (success)
                        {
                            Debug.Log($"[MainEquipmentView] 技能槽位 {skillSlot} 解锁成功");
                            UpdateDisplay();
                            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"{slotName}槽位解锁成功！");
                        }
                        else
                        {
                            Debug.LogWarning($"[MainEquipmentView] 技能槽位 {skillSlot} 解锁失败");
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
        Debug.Log("[MainEquipmentView] OnCharacterClick - 点击人物按钮");
        callback?.Invoke("OpenCharacter", null);
    }
}

public enum FishingEquipType
{
    Rod,
    Line,
    Hook
}
