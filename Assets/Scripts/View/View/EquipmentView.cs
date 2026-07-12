using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SharedModels;

public class EquipmentView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public MainEquipmentView mainEquipmentView;
    public FishingEquipView fishingEquipView;
    public EquipPlayerView equipPlayerView;
    public InfoSkillView infoSkillView;
    public InfoFishEquipView infoFishEquipView;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private MonoBehaviour currentSubView;
    private bool _isRefreshing = false;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
    }

    void OnEnable()
    {
        // 注册装备刷新事件
        CommunicateEvent.Register("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    void OnDisable()
    {
        // 取消注册
        CommunicateEvent.Unregister("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    void OnDestroy()
    {
        CommunicateEvent.Unregister("Equipment_Refresh", OnEquipmentRefresh);
        CommunicateEvent.Unregister<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipChanged);
    }

    private void OnEquipmentRefresh()
    {
        Debug.Log("[EquipmentView] OnEquipmentRefresh - 刷新所有视图");
        RefreshAllViews();
    }

    private void OnEquipChanged((int, int) data)
    {
        Debug.Log($"[EquipmentView] OnEquipChanged - slotType={data.Item1}, itemId={data.Item2}");
        RefreshAllViews();
    }

    public void Init()
    {
        LoadAllIcons();

        if (mainEquipmentView != null)
        {
            mainEquipmentView.Init();
            mainEquipmentView.SetCallback(OnMainEquipmentCallback);
        }

        if (fishingEquipView != null)
        {
            fishingEquipView.Init();
            fishingEquipView.SetCallback(OnFishingEquipCallback);
        }

        if (equipPlayerView != null)
        {
            equipPlayerView.Init();
            equipPlayerView.SetCallback(OnEquipPlayerCallback);
        }

        if (infoSkillView != null)
        {
            infoSkillView.Init();
            infoSkillView.SetCallback(OnInfoSkillCallback);
        }

        if (infoFishEquipView != null)
        {
            infoFishEquipView.Init();
            infoFishEquipView.SetCallback(OnInfoFishEquipCallback);
        }
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

    public Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        return null;
    }

    public void Show()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null)
        {
            mainEquipmentView.Show();
        }
        gameObject.SetActive(true);

        // 显示时刷新所有数据
        RefreshAllViews();
    }

    public void Hide()
    {
        HideAllSubViews();
        currentSubView = null;
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

    /// <summary>
    /// 统一刷新所有视图
    /// </summary>
    // EquipmentView.cs - 修改 RefreshAllViews

    /// <summary>
    /// 统一刷新所有视图
    /// </summary>
    public void RefreshAllViews()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        Debug.Log("[EquipmentView] RefreshAllViews - 刷新所有视图");

        try
        {
            if (mainEquipmentView != null)
            {
                mainEquipmentView.UpdateDisplay();
            }

            if (fishingEquipView != null)
            {
                fishingEquipView.UpdateDisplay();
            }

            if (equipPlayerView != null)
            {
                equipPlayerView.UpdateDisplay();
            }

            if (infoFishEquipView != null)
            {
                infoFishEquipView.UpdateDisplay();
            }

            if (infoSkillView != null)
            {
                infoSkillView.UpdateSkillList();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void OnMaskClick()
    {
        Debug.Log("[EquipmentView] OnMaskClick - 点击遮罩关闭");
        Hide();
    }

    private void OnCloseClick()
    {
        Debug.Log("[EquipmentView] OnCloseClick - 点击关闭按钮");
        Hide();
    }

    private void OnMainEquipmentCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "OpenFishingRod":
                ShowFishingEquipView(FishingEquipType.Rod);
                break;
            case "OpenFishingLine":
                ShowFishingEquipView(FishingEquipType.Line);
                break;
            case "OpenFishingHook":
                ShowFishingEquipView(FishingEquipType.Hook);
                break;
            case "OpenSkill":
                int skillSlot = args != null && args.Length > 0 ? (int)args[0] : 1;
                ShowInfoSkillView(skillSlot);
                break;
            case "OpenCharacter":
                ShowEquipPlayerView();
                break;
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
            case "Back":
                ShowMainEquipmentView();
                break;
            case "OpenInfo":
                FishingEquipType type = (FishingEquipType)args[0];
                int equipId = (int)args[1];
                ShowInfoFishEquipView(type, equipId);
                break;
            case "EquipAction":
                FishingEquipType equipType = (FishingEquipType)args[0];
                int itemId = (int)args[1];
                OnEquipAction(equipType, itemId);
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
            case "Back":
                ShowMainEquipmentView();
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnInfoSkillCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                ShowMainEquipmentView();
                break;
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
                FishingEquipType type = (FishingEquipType)args[0];
                ShowFishingEquipView(type);
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void ShowMainEquipmentView()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null)
        {
            mainEquipmentView.Show();
        }
    }

    public void ShowFishingEquipView(FishingEquipType type)
    {
        HideAllSubViews();
        currentSubView = fishingEquipView;
        if (fishingEquipView != null)
        {
            fishingEquipView.Show(type);
        }
    }

    public void ShowEquipPlayerView()
    {
        HideAllSubViews();
        currentSubView = equipPlayerView;
        if (equipPlayerView != null)
        {
            equipPlayerView.Show();
        }
    }

    public void ShowInfoSkillView(int skillSlot = 1)
    {
        HideAllSubViews();
        currentSubView = infoSkillView;
        if (infoSkillView != null)
        {
            infoSkillView.Show(skillSlot);
        }
    }

    public void ShowInfoFishEquipView(FishingEquipType type, int equipId)
    {
        HideAllSubViews();
        currentSubView = infoFishEquipView;
        if (infoFishEquipView != null)
        {
            infoFishEquipView.Show(type, equipId);
        }
    }

    /// <summary>
    /// 装备操作（装备或卸下）- 使用带回调的版本，与广告逻辑一致
    /// </summary>
    private void OnEquipAction(FishingEquipType type, int equipId)
    {
        Debug.Log($"[EquipmentView] OnEquipAction - type={type}, equipId={equipId}");

        EquipmentSlotType slotType = EquipmentSlotType.FishingRod;
        switch (type)
        {
            case FishingEquipType.Rod:
                slotType = EquipmentSlotType.FishingRod;
                break;
            case FishingEquipType.Line:
                slotType = EquipmentSlotType.FishingLine;
                break;
            case FishingEquipType.Hook:
                slotType = EquipmentSlotType.FishingHook;
                break;
            default:
                return;
        }

        string componentName = LoadDataManager.Instance.GetComponentName(equipId);
        Debug.Log($"[EquipmentView] 尝试装备: {componentName} (ID:{equipId})");

        if (NetServerManager.Instance != null)
        {
            GameUIManager.Instance?.ShowTip($"正在装备 {componentName}...");

            NetServerManager.Instance.EquipItemWithCallback(slotType, equipId, (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[EquipmentView] 装备成功: {slotType} -> {equipId}");

                    // ✅ 装备成功后，NetServerManager 内部已经拉取了最新数据
                    // 只需要刷新 UI 即可
                    RefreshAllViews();

                    string successInfo = componentName != "未知组件" ? $"已装备 {componentName}！" : "已装备装备！";
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, successInfo);
                }
                else
                {
                    Debug.LogWarning($"[EquipmentView] 装备失败: {message}");
                    GameUIManager.Instance?.ShowTip($"装备失败: {message}");
                }
            });
        }
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
            if (adSuccess)
            {
                int playerId = NetServerManager.Instance?.GetCurrentPlayerId() ?? 1;

                // 判断是技能还是装备
                if (targetId >= 3301 && targetId < 3400)
                {
                    // 技能
                    Debug.Log($"[EquipmentView] 广告成功，开始解锁技能: playerId={playerId}, skillId={targetId}");

                    NetServerManager.Instance.UnlockSkill(targetId, (success) =>
                    {
                        if (success)
                        {
                            Debug.Log($"[EquipmentView] 服务器技能解锁成功，通知UI");
                            onConfirmWithResult?.Invoke(true);
                            RefreshAllViews();
                        }
                        else
                        {
                            Debug.LogError($"[EquipmentView] 服务器技能解锁失败");
                            GameUIManager.Instance.ShowTip("技能解锁失败");
                            onConfirmWithResult?.Invoke(false);
                        }
                    });
                }
                else
                {
                    // 装备
                    string equipmentType = GetEquipmentTypeFromId(targetId);

                    Debug.Log($"[EquipmentView] 广告成功，开始解锁装备: playerId={playerId}, equipmentId={targetId}, type={equipmentType}");

                    NetServerManager.Instance.UnlockEquipment(playerId, targetId, equipmentType, (success, message) =>
                    {
                        if (success)
                        {
                            Debug.Log($"[EquipmentView] 服务器解锁成功，通知UI");
                            onConfirmWithResult?.Invoke(true);
                            RefreshAllViews();
                        }
                        else
                        {
                            Debug.LogError($"[EquipmentView] 服务器解锁失败: {message}");
                            GameUIManager.Instance.ShowTip("解锁失败: " + message);
                            onConfirmWithResult?.Invoke(false);
                        }
                    });
                }
            }
            else
            {
                // 广告失败
                Debug.Log($"[EquipmentView] 广告失败");
                onConfirmWithResult?.Invoke(false);
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

    /// <summary>
    /// 根据装备ID获取装备类型
    /// </summary>
    private string GetEquipmentTypeFromId(int equipmentId)
    {
        if (equipmentId >= 3001 && equipmentId < 3100)
            return "Rod";
        else if (equipmentId >= 3101 && equipmentId < 3200)
            return "Line";
        else if (equipmentId >= 3201 && equipmentId < 3300)
            return "Hook";
        else if (equipmentId >= 3401 && equipmentId < 3500)
            return "Character";
        else if (equipmentId >= 3501 && equipmentId < 3600)
            return "Skill";
        else
            return "Unknown";
    }
}