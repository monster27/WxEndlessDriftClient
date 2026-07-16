using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class InfoSkillView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public Transform skillListParent;
    public GameObject skillPrefab;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private List<int> skillIds = new List<int>();
    private List<UI_InfoSkillPrefab> skillItems = new List<UI_InfoSkillPrefab>();
    private System.Action<string, object[]> callback;
    private int currentSkillSlot = 1;
    public SkillInfoView skillInfoView;
    private int currentGold = 0;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);

        if (skillInfoView != null)
        {
            skillInfoView.Init();
            skillInfoView.SetCallback(OnSkillInfoCallback);
        }
    }

    void OnEnable()
    {
        RegisterDataEvents();
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
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    private void UnregisterDataEvents()
    {
        CommunicateEvent.Unregister<int>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    private void OnGoldChanged(int gold)
    {
        currentGold = gold;
        RefreshSkillItems();
    }

    private void OnEquipChanged((int, int) data)
    {
        int slotType = data.Item1;
        int itemId = data.Item2;
        if (slotType == (int)EquipmentSlotType.Skill1 || slotType == (int)EquipmentSlotType.Skill2)
        {
            RefreshSkillItems();
        }
    }

    private void RefreshSkillItems()
    {
        foreach (var item in skillItems)
        {
            item.RefreshDisplay();
        }
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
        LoadSkillIds();
        LoadAllIcons();
        CreateSkillItems();
    }

    private void LoadSkillIds()
    {
        skillIds.Clear();
        var config = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var component in config.items)
            {
                if (component.id >= 3301 && component.id <= 3399)
                {
                    skillIds.Add(component.id);
                }
            }
        }
        skillIds.Sort();
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();
        foreach (int skillId in skillIds)
        {
            string path = $"UI/Icon/Equipment/Skill/{skillId}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                iconCache[skillId] = sprite;
            }
        }
    }

    private void CreateSkillItems()
    {
        foreach (Transform child in skillListParent)
        {
            Destroy(child.gameObject);
        }
        skillItems.Clear();

        if (skillPrefab == null || skillListParent == null) return;

        foreach (int skillId in skillIds)
        {
            GameObject obj = Instantiate(skillPrefab, skillListParent);
            UI_InfoSkillPrefab item = obj.GetComponent<UI_InfoSkillPrefab>();
            if (item != null)
            {
                item.Init();
                skillItems.Add(item);
            }
        }
    }

    public void Show(int skillSlot = 1)
    {
        currentSkillSlot = skillSlot;
        SyncGoldFromServer();
        UpdateSkillList();
        skillInfoView.Hide();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SyncGoldFromServer()
    {
        try
        {
            int gold = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_GOLD", 0);
            currentGold = gold;
            Debug.Log($"[InfoSkillView] 同步金币: {currentGold}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InfoSkillView] 同步金币失败: {ex.Message}");
        }
    }

    public void UpdateSkillList()
    {
        for (int i = 0; i < skillItems.Count && i < skillIds.Count; i++)
        {
            UpdateSkillItem(skillItems[i], skillIds[i]);
        }
    }

    private void UpdateSkillItem(UI_InfoSkillPrefab item, int skillId)
    {
        if (item == null || skillId <= 0) return;

        Sprite icon = GetIcon(skillId);
        string name = LoadDataManager.Instance.GetComponentName(skillId);
        int level = GetSkillLevel(skillId);
        string description = GetSkillDescription(skillId, level);
        EquipState state = GetSkillState(skillId);
        string unlockCondition = GetSkillUnlockCondition(skillId);

        item.SetData(skillId, icon, name, level, description, state, unlockCondition,
                     OnDetailClick, OnUpgradeClick, OnWatchAdClick, OnEquipClick);
    }

    private string GetSkillUnlockCondition(int skillId)
    {
        // 获取技能的解锁条件（人物等级要求）
        return CommunicateEvent.Request<int, string>("CharacterManager_GetSkillUnlockCondition", skillId);
    }

    private string GetSkillDescription(int skillId, int level)
    {
        var config = CompleteFishingSkillConfigExtensions.LoadFromResources("JsonData/Ability/fishing_components");
        if (config == null || config.items == null) return "";

        var component = config.GetComponentById(skillId);
        if (component == null) return "";

        var levelData = component.GetLevelData(level);
        if (levelData != null && !string.IsNullOrEmpty(levelData.levelDescription))
        {
            return levelData.levelDescription;
        }

        return component.description;
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        return null;
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

    private int GetSkillLevel(int skillId)
    {
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
    }

    private void OnMaskClick()
    {
        Debug.Log("[InfoSkillView] OnMaskClick - 点击遮罩返回");
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        Debug.Log("[InfoSkillView] OnCloseClick - 点击关闭按钮返回");
        callback?.Invoke("Back", null);
    }

    private void OnDetailClick(int skillId)
    {
        if (skillInfoView != null)
        {
            skillInfoView.Show(skillId, currentSkillSlot);
        }
    }

    private void OnUpgradeClick(int skillId)
    {
        int level = GetSkillLevel(skillId);
        int cost = level * 30;

        if (currentGold >= cost)
        {
            string componentName = LoadDataManager.Instance.GetComponentName(skillId);

            if (NetServerManager.Instance != null)
            {
                NetServerManager.Instance.UpgradeSkill(skillId, level + 1, (success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[InfoSkillView] 技能升级成功: skillId={skillId}");
                        UpdateSkillList();
                        callback?.Invoke("RefreshAllViews", null);
                        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"{componentName} 升级成功！");
                    }
                    else
                    {
                        Debug.LogWarning($"[InfoSkillView] 技能升级失败: skillId={skillId}");
                    }
                });
            }
            else
            {
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "网络连接失败");
            }
        }
        else
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "金币不足！");
        }
    }

    private void OnWatchAdClick(int skillId)
    {
        string componentName = LoadDataManager.Instance.GetComponentName(skillId);
        string info = componentName != "未知组件" ? $"看广告获取技能: {componentName}" : "看广告获取技能";
        callback?.Invoke("OpenAdWithResult", new object[] { info, skillId, "看广告获取", (System.Action<bool>)((bool success) =>
        {
            if (success)
            {
                CommunicateEvent.Modify("Skill_UnlockByAd", skillId);
                UpdateSkillList();
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"成功获取 {componentName}！");
                callback?.Invoke("RefreshAllViews", null);
            }
            else
            {
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "广告播放失败");
            }
        })});
    }

    private void OnEquipClick(int skillId)
    {
        Debug.Log($"[InfoSkillView] OnEquipClick - skillId={skillId}, currentSkillSlot={currentSkillSlot}");

        EquipState state = GetSkillState(skillId);
        if (state == EquipState.OwnerUse)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "当前技能已装备！");
            return;
        }

        EquipmentSlotType slotType = currentSkillSlot == 1 ? EquipmentSlotType.Skill1 : EquipmentSlotType.Skill2;
        CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, (slotType, skillId));

        UpdateSkillList();

        string componentName = LoadDataManager.Instance.GetComponentName(skillId);
        string slotName = currentSkillSlot == 1 ? "技能1" : "技能2";
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"已装备 {componentName} 到 {slotName}！");

        callback?.Invoke("RefreshAllViews", null);
    }

    private void OnSkillInfoCallback(string eventName, object[] args)
    {
        switch (eventName)
        {
            case "Back":
                Hide();
                break;
            case "OpenAd":
                callback?.Invoke("OpenAd", args);
                break;
            case "OpenAdWithResult":
                callback?.Invoke("OpenAdWithResult", args);
                break;
            case "RefreshAllViews":
                UpdateSkillList();
                callback?.Invoke("RefreshAllViews", null);
                break;
        }
    }
}