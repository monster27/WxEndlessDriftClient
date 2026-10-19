using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
//using SharedModels;

//应该是Panel
public class InfoSkillEquipmentPanel : MonoBehaviour
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
            if (item != null) item.RefreshDisplay();
        }
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    // 问题12：串行化 Init
    public async void Init()
    {
        Z_Logger.Log("[装备界面诊断] InfoSkillEquipmentPanel.Init 开始");
        // ⚠️ 外部依赖：LoadDataManager 未加载完时组件名会是"未知组件"
        if (LoadDataManager.Instance == null || !LoadDataManager.Instance.isDataLoaded)
        {
            Z_Logger.LogWarning("[装备界面诊断] InfoSkillEquipmentPanel.Init - ⚠️ LoadDataManager 尚未加载完成");
        }
        await LoadConfigAndIdsAsync();
        await LoadAllIconsAsync();
        CreateSkillItems();
        if (this != null && gameObject.activeInHierarchy) UpdateSkillList();
        Z_Logger.Log("[装备界面诊断] InfoSkillEquipmentPanel.Init 完成");
    }

    private async Task LoadConfigAndIdsAsync()
    {
        skillIds.Clear();
        var config = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
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
        else Z_Logger.LogWarning("[装备界面诊断] InfoSkillEquipmentPanel - fishing_components 配置加载失败，技能列表将为空");
        skillIds.Sort();
        Z_Logger.Log($"[装备界面诊断] InfoSkillEquipmentPanel 技能ID加载完成: {skillIds.Count} 个");
    }

    private async Task LoadAllIconsAsync()
    {
        iconCache.Clear();

        var config = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var component in config.items)
            {
                if (component.id >= 3301 && component.id <= 3399)
                {
                    string path = $"UI/Icon/Equipment/Skill/{component.id}";
                    var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
                    if (sprite != null) iconCache[component.id] = sprite;
                    else Z_Logger.LogWarning($"[装备界面诊断] InfoSkillEquipmentPanel 技能图标加载失败: id={component.id}, path={path}");
                    AssetManager.ReleaseAddressable(handle);
                }
            }
        }

        Z_Logger.Log($"[装备界面诊断] InfoSkillEquipmentPanel 图标加载完成，共 {iconCache.Count} 个");
    }

    private void CreateSkillItems()
    {
        if (skillListParent == null)
        {
            Z_Logger.LogError("[装备界面诊断] InfoSkillEquipmentPanel.CreateSkillItems - skillListParent 未挂载！");
            return;
        }
        if (skillPrefab == null)
        {
            Z_Logger.LogError("[装备界面诊断] InfoSkillEquipmentPanel.CreateSkillItems - skillPrefab 未挂载！");
            return;
        }

        foreach (Transform child in skillListParent)
        {
            Destroy(child.gameObject);
        }
        skillItems.Clear();

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
        Z_Logger.Log($"[装备界面诊断] InfoSkillEquipmentPanel 创建技能项完成: {skillItems.Count} 个");
    }

    private async Task<string> GetSkillDescriptionAsync(int skillId, int level)
    {
        var config = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (config == null || config.items == null) return "";

        var component = config.GetComponentById(skillId);
        if (component == null) return "";

        var levelData = FishingComponentConfigExtensions.GetLevelData(component, level);
        if (levelData != null && !string.IsNullOrEmpty(levelData.levelDescription))
        {
            return levelData.levelDescription;
        }
        return component.description;
    }

    public void Show(int skillSlot = 1)
    {
        currentSkillSlot = skillSlot;
        SyncGoldFromServer();
        UpdateSkillList();
        skillInfoView?.Hide();
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
            Z_Logger.Log($"[InfoSkillView] 同步金币: {currentGold}");
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogError($"[InfoSkillView] 同步金币失败: {ex.Message}");
        }
    }

    public void UpdateSkillList()
    {
        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 default
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] InfoSkillEquipmentPanel.UpdateSkillList - ⚠️ NetServerManager 未初始化，技能状态可能全部显示为 Locked");
        }

        for (int i = 0; i < skillItems.Count && i < skillIds.Count; i++)
        {
            UpdateSkillItem(skillItems[i], skillIds[i]);
        }
    }

    private async void UpdateSkillItem(UI_InfoSkillPrefab item, int skillId)
    {
        if (item == null || skillId <= 0) return;

        Sprite icon = GetIcon(skillId);
        string name = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";
        if (name == "未知组件") Z_Logger.LogWarning($"[装备界面诊断] InfoSkillEquipmentPanel 技能名称为'未知组件', id={skillId}");

        int level = GetSkillLevel(skillId);
        string description = await GetSkillDescriptionAsync(skillId, level);
        // ⚠️ 注意：这里 await 后可能已被下一次 UpdateSkillList 覆盖，但 UI_InfoSkillPrefab 内部应该幂等
        EquipState state = GetSkillState(skillId);
        string unlockCondition = GetSkillUnlockCondition(skillId);

        item.SetData(skillId, icon, name, level, description, state, unlockCondition,
                     OnDetailClick, OnUpgradeClick, OnWatchAdClick, OnEquipClick);
    }

    private string GetSkillUnlockCondition(int skillId)
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return "";
        return CommunicateEvent.Request<int, string>("CharacterManager_GetSkillUnlockCondition", skillId);
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite)) return sprite;
        return null;
    }

    private EquipState GetSkillState(int skillId)
    {
        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 false
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ GetSkillState 时 NetServerManager 未初始化, skillId={skillId} 返回 Locked");
            return EquipState.Locked;
        }

        // ✅ 优先用 PlayerDataManager 判断"是否拥有"（问题6）
        bool obtained = false;
        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsReady)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            obtained = inventory != null && inventory.TryGetValue(skillId, out int c) && c > 0;
        }
        if (!obtained)
        {
            // 降级走原接口
            obtained = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId);
        }
        if (!obtained) return EquipState.Locked;

        int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
        int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);

        if (equippedSkill1 == skillId || equippedSkill2 == skillId) return EquipState.OwnerUse;
        return EquipState.OwnerUnUse;
    }

    private int GetSkillLevel(int skillId)
    {
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized) return 1;
        return CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
    }

    private void OnMaskClick()
    {
        Z_Logger.Log("[InfoSkillView] OnMaskClick - 点击遮罩返回");
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        Z_Logger.Log("[InfoSkillView] OnCloseClick - 点击关闭按钮返回");
        callback?.Invoke("Back", null);
    }

    private void OnDetailClick(int skillId)
    {
        skillInfoView?.Show(skillId, currentSkillSlot);
    }

    private void OnUpgradeClick(int skillId)
    {
        int level = GetSkillLevel(skillId);
        int cost = level * 30;

        if (currentGold >= cost)
        {
            string componentName = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";

            if (NetServerManager.Instance != null)
            {
                NetServerManager.Instance.UpgradeSkill(skillId, level + 1, (success) =>
                {
                    if (success)
                    {
                        Z_Logger.Log($"[InfoSkillView] 技能升级成功: skillId={skillId}");
                        UpdateSkillList();
                        // ✅ 兜底：NetServerManager 发的 EVENT_EQUIP_CHANGED 是 (int,int)，本 Panel 注册的也是 (int,int)，能收到；
                        // 但为了保险，这里再主动回调一次 RefreshAllViews
                        callback?.Invoke("RefreshAllViews", null);
                        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"{componentName} 升级成功！");
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[InfoSkillView] 技能升级失败: skillId={skillId}");
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
        string componentName = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";
        string info = componentName != "未知组件" ? $"看广告获取技能: {componentName}" : "看广告获取技能";
        callback?.Invoke("OpenAdWithResult", new object[] { info, skillId, "看广告获取", (System.Action<bool>)((bool success) =>
        {
            if (success)
            {
                if (NetServerManager.Instance != null)
                {
                    NetServerManager.Instance.UnlockSkill(skillId, (unlockSuccess) =>
                    {
                        if (unlockSuccess)
                        {
                            Z_Logger.Log($"[InfoSkillView] 技能解锁成功（广告）: skillId={skillId}");
                            CommunicateEvent.Modify("Skill_UnlockByAd", skillId);
                            UpdateSkillList();
                            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"成功获取 {componentName}！");
                            callback?.Invoke("RefreshAllViews", null);
                        }
                        else
                        {
                            Z_Logger.LogWarning($"[InfoSkillView] 技能解锁失败（广告）: skillId={skillId}");
                            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "解锁失败，请重试");
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
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "广告播放失败");
            }
        })});
    }

    private void OnEquipClick(int skillId)
    {
        Z_Logger.Log($"[InfoSkillView] OnEquipClick - skillId={skillId}, currentSkillSlot={currentSkillSlot}");

        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogError("[装备界面诊断] ⚠️ NetServerManager 未初始化，无法装备技能");
            return;
        }

        // 检查技能槽位是否已解锁
        bool isSlotUnlocked = CommunicateEvent.Request<int, bool>("EVENT_IS_SKILL_SLOT_UNLOCKED", currentSkillSlot);
        if (!isSlotUnlocked)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"技能{currentSkillSlot}槽位未解锁，请先看广告解锁");
            return;
        }

        EquipState state = GetSkillState(skillId);
        if (state == EquipState.OwnerUse)
        {
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "当前技能已装备！");
            return;
        }

        EquipmentSlotType slotType = currentSkillSlot == 1 ? EquipmentSlotType.Skill1 : EquipmentSlotType.Skill2;
        string componentName = LoadDataManager.Instance?.GetComponentName(skillId) ?? $"未知({skillId})";
        string slotName = currentSkillSlot == 1 ? "技能1" : "技能2";

        // ✅ 走新版带回调接口
        NetServerManager.Instance.EquipItemWithCallback(slotType, skillId, (success, message) =>
        {
            if (success)
            {
                UpdateSkillList();
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"已装备 {componentName} 到 {slotName}！");
                // ✅ 兜底刷新（EVENT_EQUIP_CHANGED 类型不匹配时也能生效）
                callback?.Invoke("RefreshAllViews", null);
            }
            else
            {
                Z_Logger.LogWarning($"[装备界面诊断] InfoSkillEquipmentPanel 装备技能失败: {message}");
                GameUIManager.ShowWarningMessage(string.IsNullOrEmpty(message) ? "装备失败" : message);
            }
        });
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
