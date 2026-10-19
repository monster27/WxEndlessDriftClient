using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
//using SharedModels;

public class EquipmentView : BaseView
{
    public MainEquipmentPanel mainEquipmentView;
    public FishingEquipmentPanel fishingEquipView;
    public EquipPlayerEquipmentPanel equipPlayerView;
    public InfoSkillEquipmentPanel infoSkillView;
    public InfoFishEquipmentPanel infoFishEquipView;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private MonoBehaviour currentSubView;
    private bool _isRefreshing = false;

    // 问题1：BaseView.BaseViewInit 开头就置 isInitialized=true，不能复用，必须自己维护标志
    private bool _equipInited = false;

    // ============================================================
    // 生命周期接入（问题1：让 Init/Show 被调用）
    // ============================================================
    public override void BaseViewInit()
    {
        if (_equipInited) return;
        _equipInited = true;
        base.BaseViewInit();
        Init();
        Z_Logger.Log("[装备界面诊断] EquipmentView.BaseViewInit 完成");
    }

    public override void ShowView()
    {
        base.ShowView();
        Show();
        Z_Logger.Log("[装备界面诊断] EquipmentView.ShowView 完成");
    }

    // ============================================================
    // ✅ HideView 修正：不再调 Hide()（Hide() 里有 SetActive(false)），
    //    业务隐藏逻辑内联，再走 base.HideView()（PreHide + 通知 + SetActive(false)）
    // ============================================================
    public override void HideView()
    {
        // 业务隐藏：先切回主面板，再隐藏所有子 Panel
        HideAllSubViews();
        currentSubView = null;

        // 基类：PreHide + 通知 GameUIManager + SetActive(false)
        base.HideView();
        Z_Logger.Log("[装备界面诊断] EquipmentView.HideView 完成");
    }

    void OnEnable()
    {
        CommunicateEvent.Register("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        Z_Logger.Log("[装备界面诊断] EquipmentView.OnEnable 注册事件");
    }

    void OnDisable()
    {
        CommunicateEvent.Unregister("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
        Z_Logger.Log("[装备界面诊断] EquipmentView.OnDisable 注销事件");
    }

    void OnDestroy()
    {
        CommunicateEvent.Unregister("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    private void OnEquipmentRefresh()
    {
        Z_Logger.Log("[装备界面诊断] OnEquipmentRefresh - 刷新所有视图");
        RefreshAllViews();
    }

    private void OnEquipChanged((int, int) data)
    {
        Z_Logger.Log($"[装备界面诊断] OnEquipChanged - slotType={data.Item1}, itemId={data.Item2}");
        RefreshAllViews();
    }

    // ============================================================
    // 初始化（问题3：等图标加载完再刷新）
    // ============================================================
    public async void Init()
    {
        Z_Logger.Log("[装备界面诊断] EquipmentView.Init 开始");

        // ⚠️ 外部依赖：LoadDataManager 未加载完时，组件名会显示"未知组件"
        if (LoadDataManager.Instance == null || !LoadDataManager.Instance.isDataLoaded)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ LoadDataManager 尚未加载完成，组件名称可能显示为'未知组件'。建议在外部确保 LoadDataManager.isDataLoaded 后再打开装备界面。");
        }

        await LoadAllIconsAsync();

        if (mainEquipmentView != null)
        {
            mainEquipmentView.Init();
            mainEquipmentView.SetCallback(OnMainEquipmentCallback);
        }
        else Z_Logger.LogError("[装备界面诊断] mainEquipmentView 未挂载！");

        if (fishingEquipView != null)
        {
            fishingEquipView.Init();
            fishingEquipView.SetCallback(OnFishingEquipCallback);
        }
        else Z_Logger.LogError("[装备界面诊断] fishingEquipView 未挂载！");

        if (equipPlayerView != null)
        {
            equipPlayerView.Init();
            equipPlayerView.SetCallback(OnEquipPlayerCallback);
        }
        else Z_Logger.LogError("[装备界面诊断] equipPlayerView 未挂载！");

        if (infoSkillView != null)
        {
            infoSkillView.Init();
            infoSkillView.SetCallback(OnInfoSkillCallback);
        }
        else Z_Logger.LogError("[装备界面诊断] infoSkillView 未挂载！");

        if (infoFishEquipView != null)
        {
            infoFishEquipView.Init();
            infoFishEquipView.SetCallback(OnInfoFishEquipCallback);
        }
        else Z_Logger.LogError("[装备界面诊断] infoFishEquipView 未挂载！");

        // 问题3：加载完补一次刷新
        if (gameObject.activeInHierarchy) RefreshAllViews();
        Z_Logger.Log("[装备界面诊断] EquipmentView.Init 完成");
    }

    private async Task LoadAllIconsAsync()
    {
        iconCache.Clear();

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
                else Z_Logger.LogWarning($"[装备界面诊断] 图标加载失败: path={path}, id={kvp.Key}");
                AssetManager.ReleaseAddressable(handle);
            }
        }
        else Z_Logger.LogWarning("[装备界面诊断] fishing_components 配置加载失败");

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
                else Z_Logger.LogWarning($"[装备界面诊断] 人物图标加载失败: path={path}, id={id}");
                AssetManager.ReleaseAddressable(handle);
            }
        }
        else Z_Logger.LogWarning("[装备界面诊断] characters 配置加载失败");

        Z_Logger.Log($"[装备界面诊断] 图标加载完成，共 {iconCache.Count} 个");
    }

    public Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite)) return sprite;
        return null;
    }

    public void Show()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null) mainEquipmentView.Show();
        gameObject.SetActive(true);
        RefreshAllViews();
        Z_Logger.Log("[装备界面诊断] EquipmentView.Show 完成");
    }

    // ✅ Hide() 保留为业务隐藏方法（不 SetActive，由 HideView 统一处理），
    //    如果外部有直接调 Hide() 的地方，也能走 UI_ViewSelfHide 通知。
    public void Hide()
    {
        HideAllSubViews();
        currentSubView = null;

        // ✅ 外部直接调 Hide() 时也通知 GameUIManager（如果 viewKey 已注入）
        if (!string.IsNullOrEmpty(viewKey) && !_isHidingByManager)
        {
            CommunicateEvent.Modify<string>("UI_ViewSelfHide", viewKey);
        }

        gameObject.SetActive(false);
    }

    private void HideAllSubViews()
    {
        if (mainEquipmentView != null) mainEquipmentView.Hide();
        if (fishingEquipView != null) fishingEquipView.Hide();
        if (equipPlayerView != null) equipPlayerView.Hide();
        if (infoSkillView != null) infoSkillView.Hide();
        if (infoFishEquipView != null) infoFishEquipView.Hide();
    }

    public void RefreshAllViews()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        // ⚠️ 外部依赖：NetServerManager 未初始化时，EVENT_GET_EQUIPPED_ITEM 等 Request 返回 default(0)，
        // 装备状态会全部显示"未装备"
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ NetServerManager 未初始化，装备状态可能全部显示为'未装备'。请确保 NetServerManager.StartInitialization() 已完成。");
        }
        if (PlayerDataManager.Instance == null || !PlayerDataManager.Instance.IsReady)
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ PlayerDataManager 未就绪，背包数据可能为空。");
        }

        try
        {
            if (mainEquipmentView != null) mainEquipmentView.UpdateDisplay();
            if (fishingEquipView != null) fishingEquipView.UpdateDisplay();
            if (equipPlayerView != null) equipPlayerView.UpdateDisplay();
            if (infoFishEquipView != null) infoFishEquipView.UpdateDisplay();
            if (infoSkillView != null) infoSkillView.UpdateSkillList();
        }
        finally { _isRefreshing = false; }
    }

    // ============================================================
    // 回调
    // ============================================================
    private void OnMainEquipmentCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "OpenFishingRod": ShowFishingEquipView(FishingEquipType.Rod); break;
            case "OpenFishingLine": ShowFishingEquipView(FishingEquipType.Line); break;
            case "OpenFishingHook": ShowFishingEquipView(FishingEquipType.Hook); break;
            case "OpenSkill":
                int skillSlot = args != null && args.Length > 0 ? (int)args[0] : 1;
                ShowInfoSkillView(skillSlot);
                break;
            case "OpenCharacter": ShowEquipPlayerView(); break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
            case "OpenAdWithResult":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action<bool>)args[3]);
                break;
        }
    }

    private void OnFishingEquipCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back": ShowMainEquipmentView(); break;
            case "OpenInfo":
                ShowInfoFishEquipView((FishingEquipType)args[0], (int)args[1]);
                break;
            case "EquipAction":
                OnEquipAction((FishingEquipType)args[0], (int)args[1]);
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnEquipPlayerCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back": ShowMainEquipmentView(); break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnInfoSkillCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back": ShowMainEquipmentView(); break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
            case "OpenAdWithResult":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action<bool>)args[3]);
                break;
            case "RefreshAllViews":
                RefreshAllViews();
                break;
        }
    }

    private void OnInfoFishEquipCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                ShowFishingEquipView((FishingEquipType)args[0]);
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
            // ✅ 兜底：InfoFishEquip 装备/升级/解锁成功后强制刷新，
            // 绕开 NetServerManager 里 EVENT_EQUIP_CHANGED 用 (EquipmentSlotType,int) 发、本 View 用 (int,int) 收导致的收不到
            case "RefreshAllViews":
                RefreshAllViews();
                break;
        }
    }

    private void ShowMainEquipmentView()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null) mainEquipmentView.Show();
    }

    public void ShowFishingEquipView(FishingEquipType type)
    {
        HideAllSubViews();
        currentSubView = fishingEquipView;
        if (fishingEquipView != null) fishingEquipView.Show(type);
    }

    public void ShowEquipPlayerView()
    {
        HideAllSubViews();
        currentSubView = equipPlayerView;
        if (equipPlayerView != null) equipPlayerView.Show();
    }

    public void ShowInfoSkillView(int skillSlot = 1)
    {
        HideAllSubViews();
        currentSubView = infoSkillView;
        if (infoSkillView != null) infoSkillView.Show(skillSlot);
    }

    public void ShowInfoFishEquipView(FishingEquipType type, int equipId)
    {
        HideAllSubViews();
        currentSubView = infoFishEquipView;
        if (infoFishEquipView != null) infoFishEquipView.Show(type, equipId);
    }

    /// <summary>装备操作（装备或卸下）- 使用带回调的版本</summary>
    private void OnEquipAction(FishingEquipType type, int equipId)
    {
        Z_Logger.Log($"[装备界面诊断] OnEquipAction - type={type}, equipId={equipId}");

        EquipmentSlotType slotType;
        switch (type)
        {
            case FishingEquipType.Rod: slotType = EquipmentSlotType.FishingRod; break;
            case FishingEquipType.Line: slotType = EquipmentSlotType.FishingLine; break;
            case FishingEquipType.Hook: slotType = EquipmentSlotType.FishingHook; break;
            default: return;
        }

        if (NetServerManager.Instance == null)
        {
            Z_Logger.LogError("[装备界面诊断] ⚠️ NetServerManager 为空，无法装备");
            return;
        }

        string componentName = LoadDataManager.Instance?.GetComponentName(equipId) ?? $"未知({equipId})";
        if (componentName == "未知组件")
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ 组件名称为'未知组件'，可能 LoadDataManager 未加载完或 equipId={equipId} 不在配置中");

        GameUIManager.Instance?.ShowTip($"正在装备 {componentName}...");

        NetServerManager.Instance.EquipItemWithCallback(slotType, equipId, (success, message) =>
        {
            if (success)
            {
                Z_Logger.Log($"[装备界面诊断] 装备成功: {slotType} -> {equipId}");
                // ⚠️ 外部依赖：EquipItemWithCallback 内部发的 EVENT_EQUIP_CHANGED 是 (EquipmentSlotType, int)，
                // 本 View 注册的是 (int, int)，收不到。所以这里手动刷新兜底。
                RefreshAllViews();
                CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, $"已装备 {componentName}！");
            }
            else
            {
                Z_Logger.LogWarning($"[装备界面诊断] 装备失败: {message}");
                GameUIManager.ShowWarningMessage(string.IsNullOrEmpty(message) ? "装备失败" : message);
            }
        });
    }

    private void OpenAdvertisingView(string info, int targetId, string btnText, System.Action onConfirm)
    {
        string callbackId = CommunicateEvent.RegisterCallback(onConfirm);
        var request = new CommunicateEvent.AdvertisingRequest
        {
            info = info,
            targetId = targetId,
            btnText = btnText,
            callbackId = callbackId
        };
        CommunicateEvent.Modify<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, request);
    }

    private void OpenAdvertisingView(string info, int targetId, string btnText, System.Action<bool> onConfirmWithResult)
    {
        // 包装回调，先调用服务器API
        System.Action<bool> wrappedCallback = (bool adSuccess) =>
        {
            if (!adSuccess)
            {
                Z_Logger.Log("[装备界面诊断] 广告失败");
                onConfirmWithResult?.Invoke(false);
                return;
            }

            int playerId = NetServerManager.Instance?.GetCurrentPlayerId() ?? 1;

            // 判断是技能还是装备
            if (targetId >= 3301 && targetId < 3400)
            {
                Z_Logger.Log($"[装备界面诊断] 广告成功，开始解锁技能: playerId={playerId}, skillId={targetId}");
                NetServerManager.Instance.UnlockSkill(targetId, (success) =>
                {
                    if (success)
                    {
                        onConfirmWithResult?.Invoke(true);
                        RefreshAllViews();
                    }
                    else
                    {
                        Z_Logger.LogError("[装备界面诊断] 服务器技能解锁失败");
                        GameUIManager.Instance.ShowTip("技能解锁失败");
                        onConfirmWithResult?.Invoke(false);
                    }
                });
            }
            else
            {
                string equipmentType = GetEquipmentTypeFromId(targetId);
                Z_Logger.Log($"[装备界面诊断] 广告成功，开始解锁装备: playerId={playerId}, equipmentId={targetId}, type={equipmentType}");
                NetServerManager.Instance.UnlockEquipment(playerId, targetId, equipmentType, (success, message) =>
                {
                    if (success)
                    {
                        onConfirmWithResult?.Invoke(true);
                        RefreshAllViews();
                    }
                    else
                    {
                        Z_Logger.LogError($"[装备界面诊断] 服务器解锁失败: {message}");
                        GameUIManager.Instance.ShowTip("解锁失败: " + message);
                        onConfirmWithResult?.Invoke(false);
                    }
                });
            }
        };

        string callbackId = CommunicateEvent.RegisterCallback(wrappedCallback);
        var request = new CommunicateEvent.AdvertisingRequest
        {
            info = info,
            targetId = targetId,
            btnText = btnText,
            callbackId = callbackId
        };
        CommunicateEvent.Modify<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, request);
    }

    /// <summary>根据装备ID获取装备类型</summary>
    private string GetEquipmentTypeFromId(int equipmentId)
    {
        if (equipmentId >= 3001 && equipmentId < 3100) return "Rod";
        if (equipmentId >= 3101 && equipmentId < 3200) return "Line";
        if (equipmentId >= 3201 && equipmentId < 3300) return "Hook";
        if (equipmentId >= 3401 && equipmentId < 3500) return "Character";
        if (equipmentId >= 3501 && equipmentId < 3600) return "Skill";
        // ✅ 修正：技能实际 ID 段是 3301~3399，原逻辑把它归到 Unknown
        if (equipmentId >= 3301 && equipmentId <= 3399) return "Skill";
        Z_Logger.LogWarning($"[装备界面诊断] GetEquipmentTypeFromId 未匹配: id={equipmentId}");
        return "Unknown";
    }
}

public enum FishingEquipType
{
    Rod,
    Line,
    Hook
}
