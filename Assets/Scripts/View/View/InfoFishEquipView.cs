using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class InfoFishEquipView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public GameObject rodTitleObj;
    public GameObject lineTitleObj;
    public GameObject hookTitleObj;

    public Image longIcon;
    public Image shortIcon;

    public Text equipNameText;
    public Text currentLevelText;
    public Text levelDescText;
    public Text nextLevelDescText;
    public Image levelIcon;

    public Text upgradeCostValueText;
    public Button upgradeBtn;
    public Button adUpgradeBtn;

    public GameObject ownerUseObj;
    public GameObject ownerUnUseObj;
    public GameObject lockedObj;

    public GameObject upgradeCostObj;
    public GameObject adUpgradeObj;
    public GameObject unlockObj;
    public Button unlockBtn;
    public Button watchAdBtn;
    public Button watchAd2Btn;
    public Button equipBtn;

    private FishingEquipType currentType = FishingEquipType.Rod;
    private int currentEquipId = 0;
    private System.Action<string, object[]> callback;
    private int currentGold = 0;
    private Dictionary<int, Sprite> levelIconCache = new Dictionary<int, Sprite>();

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
        if (upgradeBtn != null) upgradeBtn.onClick.AddListener(OnUpgradeClick);
        if (adUpgradeBtn != null) adUpgradeBtn.onClick.AddListener(OnAdUpgradeClick);
        if (unlockBtn != null) unlockBtn.onClick.AddListener(OnUnlockClick);
        if (watchAdBtn != null) watchAdBtn.onClick.AddListener(OnWatchAdClick);
        if (watchAd2Btn != null) watchAd2Btn?.onClick.AddListener(OnWatchAdClick);
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
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
    }

    private void UnregisterDataEvents()
    {
        CommunicateEvent.Unregister<int>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, OnItemQuantityChanged);
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
        EquipmentSlotType currentSlotType = GetSlotType();

        Debug.Log($"[InfoFishEquipView] OnEquipChanged - slotType={slotType}, itemId={itemId}, currentSlotType={currentSlotType}");

        // ✅ 如果当前显示的装备类型被更改，刷新整个界面
        if ((int)currentSlotType == slotType)
        {
            UpdateDisplay();
        }

        // ✅ 如果当前显示的装备被装备或卸下，也刷新
        if (itemId == currentEquipId || slotType == (int)currentSlotType)
        {
            UpdateDisplay();
        }
    }

    private void OnItemQuantityChanged((int, int) data)
    {
        int itemId = data.Item1;
        int quantity = data.Item2;
        if (itemId == currentEquipId)
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
        LoadLevelIcons();
    }

    private void LoadLevelIcons()
    {
        levelIconCache.Clear();
        for (int i = 1; i <= 10; i++)
        {
            string path = $"UI/Icon/Equipment/Level/{i}";
            Sprite sprite = Resources.Load<Sprite>(path);
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

    public void Show(FishingEquipType type, int equipId)
    {
        currentType = type;
        currentEquipId = equipId;
        SyncGoldFromServer();
        UpdateDisplay();
        gameObject.SetActive(true);
    }

    private void SyncGoldFromServer()
    {
        try
        {
            int gold = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_GOLD", 0);
            currentGold = gold;
            Debug.Log($"[InfoFishEquipView] 同步金币: {currentGold}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InfoFishEquipView] 同步金币失败: {ex.Message}");
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateDisplay()
    {
        UpdateTitleDisplay();
        UpdateIconDisplay();
        UpdateTextInfo();
        UpdateStateDisplay();
        UpdateUpgradeCostDisplay();
    }

    private void UpdateTitleDisplay()
    {
        if (rodTitleObj != null) rodTitleObj.SetActive(currentType == FishingEquipType.Rod);
        if (lineTitleObj != null) lineTitleObj.SetActive(currentType == FishingEquipType.Line);
        if (hookTitleObj != null) hookTitleObj.SetActive(currentType == FishingEquipType.Hook);
    }

    private void UpdateIconDisplay()
    {
        if (currentEquipId <= 0) return;

        string iconPath = GetIconPath(currentType, currentEquipId);
        Sprite icon = Resources.Load<Sprite>(iconPath);

        bool useLongIcon = currentType == FishingEquipType.Rod;

        if (longIcon != null)
        {
            longIcon.gameObject.SetActive(useLongIcon);
            if (useLongIcon && icon != null)
            {
                longIcon.sprite = icon;
                longIcon.color = Color.white;
            }
        }

        if (shortIcon != null)
        {
            shortIcon.gameObject.SetActive(!useLongIcon);
            if (!useLongIcon && icon != null)
            {
                shortIcon.sprite = icon;
                shortIcon.color = Color.white;
            }
        }
    }

    private string GetIconPath(FishingEquipType type, int equipId)
    {
        switch (type)
        {
            case FishingEquipType.Rod:
                return $"UI/Icon/Equipment/Rod/{equipId}";
            case FishingEquipType.Line:
                return $"UI/Icon/Equipment/Line/{equipId}";
            case FishingEquipType.Hook:
                return $"UI/Icon/Equipment/Hook/{equipId}";
            default:
                return $"UI/Icon/Equipment/Rod/{equipId}";
        }
    }

    private void UpdateTextInfo()
    {
        if (currentEquipId <= 0) return;

        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);
        if (componentName != "未知组件")
        {
            if (equipNameText != null)
            {
                equipNameText.text = componentName;
            }
        }

        int level = GetEquipLevel();

        if (currentLevelText != null)
        {
            currentLevelText.text = $"{LoadDataManager.Instance.GetEquipmentUIText("currentLevel")}: {level}";
        }

        if (levelDescText != null)
        {
            FishingComponentConfig config = LoadDataManager.Instance.GetComponentById(currentEquipId);
            if (config != null && config.levelDataList != null)
            {
                var currentLevelConfig = config.levelDataList.Find(l => l.level == level);
                if (currentLevelConfig != null && !string.IsNullOrEmpty(currentLevelConfig.levelDescription))
                {
                    levelDescText.text = currentLevelConfig.levelDescription;
                }
                else
                {
                    levelDescText.text = config.description;
                }
            }
            else if (config != null)
            {
                levelDescText.text = config.description;
            }
            else
            {
                levelDescText.text = string.Empty;
            }
        }

        if (levelIcon != null)
        {
            Sprite icon = GetLevelIcon(level);
            if (icon != null)
            {
                levelIcon.sprite = icon;
                levelIcon.color = Color.white;
                levelIcon.gameObject.SetActive(true);
            }
            else
            {
                levelIcon.gameObject.SetActive(false);
            }
        }

        if (nextLevelDescText != null)
        {
            FishingComponentConfig config = LoadDataManager.Instance.GetComponentById(currentEquipId);
            if (config != null && config.levelDataList != null && level < config.maxLevel)
            {
                var nextLevelConfig = config.levelDataList.Find(l => l.level == level + 1);
                if (nextLevelConfig != null && !string.IsNullOrEmpty(nextLevelConfig.upgradeDescription))
                {
                    nextLevelDescText.text = nextLevelConfig.upgradeDescription;
                }
                else
                {
                    nextLevelDescText.text = LoadDataManager.Instance.GetEquipmentUIText("nextLevelEffect");
                }
            }
            else
            {
                nextLevelDescText.text = LoadDataManager.Instance.GetEquipmentUIText("maxLevel");
            }
        }
    }

    private void UpdateStateDisplay()
    {
        EquipState state = GetEquipState();

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

        bool isUnlocked = state != EquipState.Locked;

        if (upgradeCostObj != null)
        {
            upgradeCostObj.SetActive(isUnlocked);
        }
        if (adUpgradeObj != null)
        {
            adUpgradeObj.SetActive(isUnlocked);
        }
        if (unlockObj != null)
        {
            unlockObj.SetActive(state == EquipState.Locked);
        }

        if (upgradeBtn != null)
        {
            upgradeBtn.gameObject.SetActive(isUnlocked);
        }
        if (adUpgradeBtn != null)
        {
            adUpgradeBtn.gameObject.SetActive(isUnlocked);
        }
        if (unlockBtn != null)
        {
            unlockBtn.gameObject.SetActive(state == EquipState.Locked);
        }
    }

    private void UpdateUpgradeCostDisplay()
    {
        int level = GetEquipLevel();
        int cost = CalculateUpgradeCost(level);
        bool canAfford = CanAffordUpgrade(cost);

        if (upgradeCostValueText != null)
        {
            upgradeCostValueText.text = cost.ToString();
            upgradeCostValueText.color = canAfford ? Color.black : Color.red;
        }
    }

    private EquipState GetEquipState()
    {
        EquipmentSlotType slotType = GetSlotType();
        int equippedId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType);

        if (equippedId == currentEquipId)
        {
            return EquipState.OwnerUse;
        }

        var inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        if (inventory.ContainsKey(currentEquipId))
        {
            return EquipState.OwnerUnUse;
        }

        return EquipState.Locked;
    }

    private EquipmentSlotType GetSlotType()
    {
        switch (currentType)
        {
            case FishingEquipType.Rod:
                return EquipmentSlotType.FishingRod;
            case FishingEquipType.Line:
                return EquipmentSlotType.FishingLine;
            case FishingEquipType.Hook:
                return EquipmentSlotType.FishingHook;
            default:
                return EquipmentSlotType.FishingRod;
        }
    }

    private int GetEquipLevel()
    {
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, currentEquipId);
    }

    private int CalculateUpgradeCost(int currentLevel)
    {
        FishingComponentConfig config = LoadDataManager.Instance.GetComponentById(currentEquipId);
        if (config != null && config.levelDataList != null)
        {
            var levelConfig = config.levelDataList.Find(l => l.level == currentLevel);
            if (levelConfig != null)
            {
                return levelConfig.upgradeCost;
            }
        }
        return currentLevel * 50;
    }

    private bool CanAffordUpgrade(int cost)
    {
        return currentGold >= cost;
    }

    private void OnMaskClick()
    {
        Debug.Log("[InfoFishEquipView] OnMaskClick - 点击遮罩返回");
        callback?.Invoke("Back", new object[] { currentType });
    }

    private void OnCloseClick()
    {
        Debug.Log("[InfoFishEquipView] OnCloseClick - 点击关闭按钮返回");
        callback?.Invoke("Back", new object[] { currentType });
    }

    private void OnUpgradeClick()
    {
        Debug.Log($"[InfoFishEquipView] OnUpgradeClick - currentEquipId={currentEquipId}");

        int level = GetEquipLevel();
        int cost = CalculateUpgradeCost(level);

        // 先检查金币是否足够
        if (currentGold < cost)
        {
            GameUIManager.ShowWarningMessage(LoadDataManager.Instance.GetEquipmentUIText("notEnoughGold"));
            Debug.LogWarning($"[InfoFishEquipView] 金币不足, 当前: {currentGold}, 需要: {cost}");
            return;
        }

        // 检查是否已经满级
        FishingComponentConfig config = LoadDataManager.Instance.GetComponentById(currentEquipId);
        int maxLevel = config != null ? config.maxLevel : 10;
        if (level >= maxLevel)
        {
            GameUIManager.ShowWarningMessage(LoadDataManager.Instance.GetEquipmentUIText("maxLevel"));
            return;
        }

        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);

        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.UpgradeEquipment(currentEquipId, (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[InfoFishEquipView] 装备升级成功: {message}");
                    UpdateDisplay();
                    string successInfo = componentName != "未知组件" ? $"{componentName} 升级成功！" : "装备升级成功！";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
                }
                else
                {
                    Debug.LogWarning($"[InfoFishEquipView] 装备升级失败: {message}");
                    string failMessage = string.IsNullOrEmpty(message) ? "装备升级失败！" : message;
                    GameUIManager.ShowWarningMessage(failMessage);
                }
            });
        }
        else
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "网络连接失败");
        }
    }

    private void OnAdUpgradeClick()
    {
        Debug.Log("[InfoFishEquipView] OnAdUpgradeClick - 点击看广告升级");
        int level = GetEquipLevel();

        // 检查是否已经满级（看广告升级有限制）
        if (level >= 10)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "装备已满级！");
            return;
        }

        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);
        int levelBeforeAd = GetEquipLevel();
        string info = componentName != "未知组件" ? $"看广告升级装备: {componentName}" : "看广告升级装备";
        callback?.Invoke("OpenAd", new object[] { info, currentEquipId, "看广告升级", (System.Action)(() =>
        {
            Debug.Log($"[InfoFishEquipView] 广告升级完成 - currentEquipId={currentEquipId}");
            CommunicateEvent.Modify("Equip_UpgradeByAd", currentEquipId);
            UpdateDisplay();

            // 检查等级是否变化，根据结果判断是否升级成功
            int levelAfterAd = GetEquipLevel();
            if (levelAfterAd > levelBeforeAd)
            {
                // 显示升级成功提示
                string successInfo = componentName != "未知组件" ? $"{componentName} 升级成功！" : "装备升级成功！";
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
            }
            else
            {
                // 显示升级失败提示
                string failInfo = componentName != "未知组件" ? $"{componentName} 升级失败" : "装备升级失败";
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, failInfo);
            }
        })});
    }

    private void OnUnlockClick()
    {
        Debug.Log("[InfoFishEquipView] OnUnlockClick - 点击看广告解锁");
        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);
        string info = componentName != "未知组件" ? $"看广告解锁装备: {componentName}" : "看广告解锁装备";
        callback?.Invoke("OpenAdWithResult", new object[] { info, currentEquipId, "看广告解锁", (System.Action<bool>)((bool success) =>
        {
            Debug.Log($"[InfoFishEquipView] 广告解锁回调 - success={success}, currentEquipId={currentEquipId}");

            if (success)
            {
                CommunicateEvent.Modify("Equip_Unlock", currentEquipId);
                UpdateDisplay();

                // 检查实际装备状态，根据结果显示不同提示
                EquipState state = GetEquipState();
                if (state != EquipState.Locked)
                {
                    // 显示解锁成功提示
                    string successInfo = componentName != "未知组件" ? $"恭喜解锁 {componentName}！" : "恭喜解锁装备！";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
                }
                else
                {
                    // 显示解锁失败提示
                    string failInfo = componentName != "未知组件" ? $"解锁 {componentName} 失败" : "解锁装备失败";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, failInfo);
                }
            }
            else
            {
                // 广告播放失败
                string failInfo = componentName != "未知组件" ? $"观看广告失败，未能解锁 {componentName}" : "观看广告失败，未能解锁装备";
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, failInfo);
            }
        })});
    }

    private void OnWatchAdClick()
    {
        Debug.Log($"[InfoFishEquipView] OnWatchAdClick - currentEquipId={currentEquipId}");
        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);
        string info = componentName != "未知组件" ? $"看广告获取装备: {componentName}" : "看广告获取装备";
        callback?.Invoke("OpenAd", new object[] { info, currentEquipId, "看广告获取", (System.Action)(() =>
        {
            CommunicateEvent.Modify("Equip_Unlock", currentEquipId);
            UpdateDisplay();

            // 检查实际装备状态，根据结果显示不同提示
            EquipState state = GetEquipState();
            if (state != EquipState.Locked)
            {
                // 显示获取成功提示
                string successInfo = componentName != "未知组件" ? $"成功获取 {componentName}！" : "成功获取装备！";
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
            }
            else
            {
                // 显示获取失败提示
                string failInfo = componentName != "未知组件" ? $"获取 {componentName} 失败" : "获取装备失败";
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, failInfo);
            }
        })});
    }

    private void OnEquipClick()
    {
        Debug.Log($"[InfoFishEquipView] OnEquipClick - currentType={currentType}, currentEquipId={currentEquipId}");

        EquipmentSlotType slotType = GetSlotType();
        string componentName = LoadDataManager.Instance.GetComponentName(currentEquipId);

        if (NetServerManager.Instance != null)
        {
            GameUIManager.Instance?.ShowTip($"正在装备 {componentName}...");

            NetServerManager.Instance.EquipItemWithCallback(slotType, currentEquipId, (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[InfoFishEquipView] 装备成功: {slotType} -> {currentEquipId}");

                    // ✅ 刷新显示
                    UpdateDisplay();

                    // ✅ 触发全局刷新
                    CommunicateEvent.Modify("Equipment_Refresh");

                    string successInfo = componentName != "未知组件" ? $"已装备 {componentName}！" : "已装备装备！";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
                }
                else
                {
                    Debug.LogWarning($"[InfoFishEquipView] 装备失败: {message}");
                    string failMessage = string.IsNullOrEmpty(message) ? "装备失败！" : message;
                    GameUIManager.ShowWarningMessage(failMessage);
                }
            });
        }
    }
}