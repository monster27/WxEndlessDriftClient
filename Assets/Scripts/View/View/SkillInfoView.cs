using UnityEngine;
using UnityEngine.UI;

public class SkillInfoView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public Image skillIcon;
    public Text skillNameText;
    public Text skillDescText;
    public Text currentLevelText;
    public Text nextLevelDescText;
    public Text upgradeCostText;
    public Text upgradeCostValueText;

    public Button upgradeBtn;
    public Button unlockBtn;
    public Button equipBtn;

    public GameObject ownerUseObj;
    public GameObject ownerUnUseObj;
    public GameObject lockedObj;

    public GameObject upgradeCostObj;

    public GameObject skillInfoObj;
    public GameObject unLockObj;

    private int currentSkillId = 0;
    private int currentSkillSlot = 1; // 记录当前是技能1还是技能2槽
    private System.Action<string, object[]> callback;
    private int currentGold = 0;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
        if (upgradeBtn != null) upgradeBtn.onClick.AddListener(OnUpgradeClick);
        if (unlockBtn != null) unlockBtn.onClick.AddListener(OnUnlockClick);
        if (equipBtn != null) equipBtn.onClick.AddListener(OnEquipClick);
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
        UpdateUpgradeCostDisplay();
    }

    private void OnEquipChanged((int, int) data)
    {
        int slotType = data.Item1;
        int itemId = data.Item2;
        if (slotType == (int)EquipmentSlotType.Skill1 || slotType == (int)EquipmentSlotType.Skill2)
        {
            UpdateStateDisplay();
        }
    } 

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
    }

    public void Show(int skillId, int skillSlot = 1)
    {
        currentSkillId = skillId;
        currentSkillSlot = skillSlot;
        UpdateDisplay();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdateDisplay()
    {
        LoadSkillIcon();
        UpdateTextInfo();
        UpdateStateDisplay();
        UpdateUpgradeCostDisplay();
    }

    private void LoadSkillIcon()
    {
        if (skillIcon != null && currentSkillId > 0)
        {
            string iconPath = $"UI/Icon/Equipment/Skill/{currentSkillId}";
            Sprite icon = Resources.Load<Sprite>(iconPath);
            if (icon != null)
            {
                skillIcon.sprite = icon;
                skillIcon.color = Color.white;
            }
        }
    }

    private void UpdateTextInfo()
    {
        if (currentSkillId <= 0) return;

        string componentName = LoadDataManager.Instance.GetComponentName(currentSkillId);
        if (skillNameText != null)
        {
            skillNameText.text = componentName;
        }

        int level = GetSkillLevel();

        if (currentLevelText != null)
        {
            currentLevelText.text = $"当前等级: {level}";
        }

        if (nextLevelDescText != null)
        {
            if (level >= 10)
            {
                nextLevelDescText.text = "已满级";
            }
            else
            {
                string nextLevelDesc = GetNextLevelDescription();
                nextLevelDescText.text = nextLevelDesc;
            }
        }
    }

    private string GetNextLevelDescription()
    {
        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config == null || config.items == null) return "升级后效果提升";

        var component = config.GetComponentById(currentSkillId);
        if (component == null) return "升级后效果提升";

        int currentLevel = GetSkillLevel();
        var levelData = component.GetLevelData(currentLevel + 1);
        if (levelData != null && !string.IsNullOrEmpty(levelData.levelDescription))
        {
            return levelData.levelDescription;
        }

        return "升级后效果提升";
    }

    private void UpdateStateDisplay()
    {
        EquipState state = GetSkillState();

        if (ownerUseObj != null)
        {
            ownerUseObj.SetActive(state == EquipState.OwnerUse);
        }
        if (ownerUnUseObj != null)
        {
            ownerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        }
        if (lockedObj != null)
        {
            lockedObj.SetActive(state == EquipState.Locked);
        }

        if (upgradeCostObj != null)
        {
            upgradeCostObj.SetActive(state == EquipState.OwnerUnUse);
        }

        if (unlockBtn != null)
        {
            unlockBtn.gameObject.SetActive(state == EquipState.Locked);
        }

        if (equipBtn != null)
        {
            equipBtn.gameObject.SetActive(state == EquipState.OwnerUnUse);
        }

        if (skillInfoObj != null)
        {
            skillInfoObj.SetActive(state != EquipState.Locked);
        }
        if (unLockObj != null)
        {
            unLockObj.SetActive(state == EquipState.Locked);
        }
    }

    private void UpdateUpgradeCostDisplay()
    {
        int level = GetSkillLevel();
        int cost = CalculateUpgradeCost(level);

        if (upgradeCostValueText != null)
        {
            upgradeCostValueText.text = cost.ToString();
            upgradeCostValueText.color = CanAffordUpgrade(cost) ? Color.black : Color.red;
        }
    }

    private EquipState GetSkillState()
    {
        if (!CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, currentSkillId))
        {
            return EquipState.Locked;
        }

        int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
        int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);

        if (equippedSkill1 == currentSkillId || equippedSkill2 == currentSkillId)
        {
            return EquipState.OwnerUse;
        }

        return EquipState.OwnerUnUse;
    }

    private int GetSkillLevel()
    {
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, currentSkillId);
    }

    private int CalculateUpgradeCost(int currentLevel)
    {
        return currentLevel * 100;
    }

    private bool CanAffordUpgrade(int cost)
    {
        return currentGold >= cost;
    }

    private void OnMaskClick()
    {
        Debug.Log("[SkillInfoView] OnMaskClick - 点击遮罩关闭");
        //callback?.Invoke("Back", null);
        gameObject.SetActive(false);
    }

    private void OnCloseClick()
    {
        Debug.Log("[SkillInfoView] OnCloseClick - 点击关闭按钮");
        //callback?.Invoke("Back", null);
        gameObject.SetActive(false);
    }

    private void OnUpgradeClick()
    {
        Debug.Log($"[SkillInfoView] OnUpgradeClick - skillId={currentSkillId}");
        int level = GetSkillLevel();
        if (level >= 10)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "技能已满级！");
            return;
        }

        int cost = CalculateUpgradeCost(level);
        Debug.Log($"[SkillInfoView] OnUpgradeClick - level={level}, cost={cost}, currentGold={currentGold}");
        if (currentGold >= cost)
        {
            Debug.Log($"[SkillInfoView] OnUpgradeClick - 执行金币升级");
            CommunicateEvent.Modify("Skill_UpgradeByGold", currentSkillId);
            UpdateDisplay();
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "升级成功！");
            callback?.Invoke("RefreshAllViews", null);
        }
        else
        {
            Debug.Log($"[SkillInfoView] OnUpgradeClick - 金币不足，跳转到看广告升级");
            OnAdUpgradeClick();
        }
    }

    private void OnAdUpgradeClick()
    {
        Debug.Log($"[SkillInfoView] OnAdUpgradeClick - skillId={currentSkillId}, callback={callback}");
        string componentName = LoadDataManager.Instance.GetComponentName(currentSkillId);
        string info = componentName != "未知组件" ? $"看广告升级技能: {componentName}" : "看广告升级技能";
        callback?.Invoke("OpenAd", new object[] { info, currentSkillId, "看广告升级", (System.Action)(() =>
        {
            Debug.Log($"[SkillInfoView] OnAdUpgradeClick 广告回调执行 - skillId={currentSkillId}");
            CommunicateEvent.Modify("Skill_UpgradeByAd", currentSkillId);
            UpdateDisplay();
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "升级成功！");
            callback?.Invoke("RefreshAllViews", null);
        })});
    }

    private void OnEquipClick()
    {
        Debug.Log($"[SkillInfoView] OnEquipClick - skillId={currentSkillId}, currentSkillSlot={currentSkillSlot}");

        EquipState state = GetSkillState();
        if (state == EquipState.OwnerUse)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "当前技能已装备！");
            return;
        }

        EquipmentSlotType slotType = currentSkillSlot == 1 ? EquipmentSlotType.Skill1 : EquipmentSlotType.Skill2;
        CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, (slotType, currentSkillId));

        UpdateDisplay();

        string componentName = LoadDataManager.Instance.GetComponentName(currentSkillId);
        string slotName = currentSkillSlot == 1 ? "技能1" : "技能2";
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"已装备 {componentName} 到 {slotName}！");

        callback?.Invoke("RefreshAllViews", null);
    }

    private void OnUnlockClick()
    {
        Debug.Log($"[SkillInfoView] OnUnlockClick - skillId={currentSkillId}, callback={callback}");
        string componentName = LoadDataManager.Instance.GetComponentName(currentSkillId);
        string info = componentName != "未知组件" ? $"看广告获取技能: {componentName}" : "看广告获取技能";
        callback?.Invoke("OpenAdWithResult", new object[] { info, currentSkillId, "看广告获取", (System.Action<bool>)((bool success) =>
        {
            Debug.Log($"[SkillInfoView] OnUnlockClick 广告回调执行 - skillId={currentSkillId}, success={success}");
            if (success)
            {
                CommunicateEvent.Modify("Skill_UnlockByAd", currentSkillId);
                UpdateDisplay();
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"成功获取 {componentName}！");
                callback?.Invoke("RefreshAllViews", null);
            }
            else
            {
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "广告播放失败");
            }
        })});
    }

    private void OnSkillInfoCallback(string eventName, object[] args)
    {
        switch (eventName)
        {
            case "Back":
                Hide();
                break;
            case "OpenAd":
                if (args != null && args.Length >= 4 && args[3] is System.Action adCallback)
                {
                    adCallback?.Invoke();
                }
                break;
            case "RefreshAllViews":
                UpdateDisplay();
                break;
        }
    }
}