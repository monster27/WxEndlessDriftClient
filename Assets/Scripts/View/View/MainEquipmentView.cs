using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class MainEquipmentView : MonoBehaviour
{
    public Button fishingRodBtn;
    public Image fishingRodIcon;
    public Text fishingRodName;

    public Button fishingLineBtn;
    public Image fishingLineIcon;
    public Text fishingLineName;

    public Button fishingHookBtn;
    public Image fishingHookIcon;
    public Text fishingHookName;

    public Button skill1Btn;
    public Image skill1Icon;
    public Text skill1Name;

    public Button skill2Btn;
    public Image skill2Icon;
    public Text skill2Name;

    public Button characterBtn;
    public Image characterIcon;
    public Text characterName;
    public Text characterLevelText;
    public Text characterExpText;
    public Slider characterSlider;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
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
        if (skill1Btn != null) skill1Btn.onClick.AddListener(OnSkillClick);
        if (skill2Btn != null) skill2Btn.onClick.AddListener(OnSkillClick);
        if (characterBtn != null) characterBtn.onClick.AddListener(OnCharacterClick);
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
        LoadAllIcons();
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();

        var fishingConfig = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
        if (fishingConfig != null)
        {
            var iconPaths = fishingConfig.GetAllIconPaths();
            foreach (var kvp in iconPaths)
            {
                string path = kvp.Value;
                Sprite sprite = Resources.Load<Sprite>(path);
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
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    iconCache[id] = sprite;
                }
            }
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
            fishingRodName.text = LoadDataManager.Instance.GetComponentName(rodId);
        }
    }

    private void UpdateFishingLineDisplay(int lineId)
    {
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
            fishingLineName.text = LoadDataManager.Instance.GetComponentName(lineId);
        }
    }

    private void UpdateFishingHookDisplay(int hookId)
    {
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
            fishingHookName.text = LoadDataManager.Instance.GetComponentName(hookId);
        }
    }

    private void UpdateSkill1Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill1Name != null)
            {
                skill1Name.text = "未放置";
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
            skill1Name.text = LoadDataManager.Instance.GetComponentName(skillId);
        }
    }

    private void UpdateSkill2Display(int skillId)
    {
        if (skillId <= 0)
        {
            if (skill2Name != null)
            {
                skill2Name.text = "未放置";
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
            skill2Name.text = LoadDataManager.Instance.GetComponentName(skillId);
        }
    }

    private void UpdateCharacterDisplay(int characterId)
    {
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
                characterLevelText.text = $"等级: {playerData.currentLevel}";
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

    private void OnSkillClick()
    {
        int skillSlot = 1;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == skill2Btn?.gameObject)
                {
                    skillSlot = 2;
                }
            }
        }
        Debug.Log($"[MainEquipmentView] OnSkillClick - 点击技能按钮, skillSlot={skillSlot}");
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
