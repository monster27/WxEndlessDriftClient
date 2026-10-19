using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
//using SharedModels;

public class FishingEquipmentPanel : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public List<FishingEquipItem> equipItems;

    public GameObject rodNameObj;
    public GameObject lineNameObj;
    public GameObject hookNameObj;

    public Button leftBtn;
    public Button rightBtn;
    public Text pageText;

    private FishingEquipType currentType = FishingEquipType.Rod;
    private int currentPage = 0;
    private int totalCount = 0;
    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();
    private System.Action<string, object[]> callback;

    private List<int> rodIds = new List<int>();
    private List<int> lineIds = new List<int>();
    private List<int> hookIds = new List<int>();

    private int itemsPerPage = 1;

    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);

        if (equipItems != null)
        {
            itemsPerPage = equipItems.Count;
            foreach (var item in equipItems)
            {
                if (item != null) item.Init();
            }
        }

        // ⚠️ 注意：这里不再注册 "Equipment_Refresh"。
        // EquipmentView 已注册并会统一调 FishingEquipView.UpdateDisplay()，
        // 重复注册会导致一次事件触发两层刷新（问题8）。
        // 原代码（已移除）：
        // CommunicateEvent.Register("Equipment_Refresh", OnEquipmentRefresh);
    }

    void OnDestroy()
    {
        // 原代码（已移除）：
        // CommunicateEvent.Unregister("Equipment_Refresh", OnEquipmentRefresh);
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    // 问题12：串行化 Init，避免 LoadEquipmentIds / LoadAllIcons 并发导致列表空/顺序乱
    public async void Init()
    {
        Z_Logger.Log("[装备界面诊断] FishingEquipmentPanel.Init 开始");
        await LoadEquipmentIdsAsync();
        await LoadAllIconsAsync();
        if (this != null && gameObject.activeInHierarchy) UpdateDisplay();
        Z_Logger.Log("[装备界面诊断] FishingEquipmentPanel.Init 完成");
    }

    private async Task LoadEquipmentIdsAsync()
    {
        rodIds.Clear();
        lineIds.Clear();
        hookIds.Clear();

        // ✅ 从 Addressables 异步加载
        var config = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var item in config.items)
            {
                if (item.id >= 3001 && item.id <= 3099) rodIds.Add(item.id);
                else if (item.id >= 3101 && item.id <= 3199) lineIds.Add(item.id);
                else if (item.id >= 3201 && item.id <= 3299) hookIds.Add(item.id);
            }
        }
        else
        {
            Z_Logger.LogWarning("[装备界面诊断] FishingEquipmentPanel - fishing_components 配置加载失败，装备列表将为空");
        }

        Z_Logger.Log($"[装备界面诊断] FishingEquipmentPanel 装备ID加载完成: Rod={rodIds.Count}, Line={lineIds.Count}, Hook={hookIds.Count}");
    }

    private async Task LoadAllIconsAsync()
    {
        iconCache.Clear();

        // ✅ 从 Addressables 异步加载
        var fishingConfig = await CompleteFishingSkillConfigExtensions.LoadFromAddressablesAsync("JsonData/Ability/fishing_components");
        if (fishingConfig != null)
        {
            var iconPaths = fishingConfig.GetAllIconPaths();
            foreach (var kvp in iconPaths)
            {
                string path = kvp.Value;
                var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(path);
                if (sprite != null) iconCache[kvp.Key] = sprite;
                else Z_Logger.LogWarning($"[装备界面诊断] FishingEquipmentPanel 图标加载失败: id={kvp.Key}, path={path}");
                AssetManager.ReleaseAddressable(handle);
            }
        }
        else Z_Logger.LogWarning("[装备界面诊断] FishingEquipmentPanel - fishing_components 配置加载失败");

        Z_Logger.Log($"[装备界面诊断] FishingEquipmentPanel 图标加载完成，共 {iconCache.Count} 个");
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite)) return sprite;
        return null;
    }

    public void Show(FishingEquipType type)
    {
        currentType = type;
        currentPage = 0;

        if (equipItems != null) itemsPerPage = equipItems.Count;

        UpdateDisplay();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateDisplay()
    {
        UpdateNameObjects();
        UpdateEquipItems();
        UpdatePageText();
    }

    private void UpdateNameObjects()
    {
        if (rodNameObj != null) rodNameObj.SetActive(currentType == FishingEquipType.Rod);
        if (lineNameObj != null) lineNameObj.SetActive(currentType == FishingEquipType.Line);
        if (hookNameObj != null) hookNameObj.SetActive(currentType == FishingEquipType.Hook);
    }

    private void UpdateEquipItems()
    {
        if (equipItems == null || equipItems.Count == 0)
        {
            Z_Logger.LogWarning("[装备界面诊断] FishingEquipmentPanel.UpdateEquipItems - equipItems 未挂载或为空");
            return;
        }

        List<int> currentIds = GetCurrentIds();
        totalCount = currentIds.Count;

        int totalPages = Mathf.CeilToInt((float)totalCount / itemsPerPage);
        totalPages = Mathf.Max(1, totalPages);
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        int startIndex = currentPage * itemsPerPage;

        for (int i = 0; i < equipItems.Count; i++)
        {
            FishingEquipItem item = equipItems[i];
            if (item == null) continue;

            int dataIndex = startIndex + i;
            if (dataIndex < totalCount)
            {
                int equipId = currentIds[dataIndex];
                Sprite icon = GetIcon(equipId);
                string name = LoadDataManager.Instance?.GetComponentName(equipId) ?? $"未知({equipId})";
                if (name == "未知组件")
                {
                    Z_Logger.LogWarning($"[装备界面诊断] FishingEquipmentPanel 组件名称为'未知组件', equipId={equipId}");
                    name = GetDefaultName(currentType);
                }

                EquipState state = GetEquipState(currentType, equipId);

                // ✅ 获取装备等级
                int level = 1;
                if (NetServerManager.Instance != null && NetServerManager.Instance.IsInitialized)
                {
                    level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, equipId);
                }
                else
                {
                    Z_Logger.LogWarning($"[装备界面诊断] ⚠️ FishingEquipmentPanel 获取等级时 NetServerManager 未初始化, equipId={equipId} 返回 1");
                }

                item.SetData(currentType, equipId, icon, name, state, OnEquipItemClick, OnEquipAction, OnWatchAd, level);
                item.gameObject.SetActive(true);
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }
    }

    private string GetDefaultName(FishingEquipType type)
    {
        switch (type)
        {
            case FishingEquipType.Rod: return LoadDataManager.Instance?.GetSubCategoryNameById(31) ?? "钓竿";
            case FishingEquipType.Line: return LoadDataManager.Instance?.GetSubCategoryNameById(32) ?? "钓线";
            case FishingEquipType.Hook: return LoadDataManager.Instance?.GetSubCategoryNameById(33) ?? "钓钩";
            default: return LoadDataManager.Instance?.GetCategoryNameByFolderName("C") ?? "装备";
        }
    }

    private EquipState GetEquipState(FishingEquipType type, int equipId)
    {
        EquipmentSlotType slotType = GetSlotType(type);

        // ⚠️ 外部依赖：NetServerManager 未初始化时 Request 返回 0
        if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ GetEquipState 时 NetServerManager 未初始化, equipId={equipId} 返回 Locked");
            return EquipState.Locked;
        }

        // ✅ 从本地缓存获取装备ID
        int equippedId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType);
        if (equippedId == equipId) return EquipState.OwnerUse;

        // ✅ 优先用 PlayerDataManager，避免 NetServerManager 内部字典被 Clear 的中间态（问题6）
        Dictionary<int, int> inventory = null;
        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsReady)
        {
            inventory = PlayerDataManager.Instance.GetInventory();
        }
        else
        {
            Z_Logger.LogWarning("[装备界面诊断] ⚠️ PlayerDataManager 未就绪，降级走 EVENT_GET_INVENTORY");
            inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        }

        if (inventory == null)
        {
            Z_Logger.LogWarning($"[装备界面诊断] ⚠️ GetEquipState 背包数据为 null, equipId={equipId}");
            return EquipState.Locked;
        }

        if (inventory.TryGetValue(equipId, out int count) && count > 0) return EquipState.OwnerUnUse;

        return EquipState.Locked;
    }

    private EquipmentSlotType GetSlotType(FishingEquipType type)
    {
        switch (type)
        {
            case FishingEquipType.Rod: return EquipmentSlotType.FishingRod;
            case FishingEquipType.Line: return EquipmentSlotType.FishingLine;
            case FishingEquipType.Hook: return EquipmentSlotType.FishingHook;
            default: return EquipmentSlotType.FishingRod;
        }
    }

    private void UpdatePageText()
    {
        if (pageText != null)
        {
            int totalPages = Mathf.CeilToInt((float)totalCount / itemsPerPage);
            totalPages = Mathf.Max(1, totalPages);
            pageText.text = $"{currentPage + 1}/{totalPages}";
        }
    }

    private List<int> GetCurrentIds()
    {
        switch (currentType)
        {
            case FishingEquipType.Rod: return rodIds;
            case FishingEquipType.Line: return lineIds;
            case FishingEquipType.Hook: return hookIds;
            default: return rodIds;
        }
    }

    private void OnMaskClick()
    {
        Z_Logger.Log("[FishingEquipView] OnMaskClick - 点击遮罩返回");
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        Z_Logger.Log("[FishingEquipView] OnCloseClick - 点击关闭按钮返回");
        callback?.Invoke("Back", null);
    }

    private void OnLeftClick()
    {
        Z_Logger.Log("[FishingEquipView] OnLeftClick - 点击左箭头翻页");
        List<int> currentIds = GetCurrentIds();
        int totalPages = Mathf.CeilToInt((float)currentIds.Count / itemsPerPage);
        if (totalPages <= 1) return;

        currentPage--;
        if (currentPage < 0) currentPage = totalPages - 1;
        UpdateEquipItems();
        UpdatePageText();
    }

    private void OnRightClick()
    {
        Z_Logger.Log("[FishingEquipView] OnRightClick - 点击右箭头翻页");
        List<int> currentIds = GetCurrentIds();
        int totalPages = Mathf.CeilToInt((float)currentIds.Count / itemsPerPage);
        if (totalPages <= 1) return;

        currentPage++;
        if (currentPage >= totalPages) currentPage = 0;
        UpdateEquipItems();
        UpdatePageText();
    }

    private void OnEquipItemClick(FishingEquipType type, int equipId)
    {
        callback?.Invoke("OpenInfo", new object[] { type, equipId });
    }

    private void OnEquipAction(FishingEquipType type, int equipId)
    {
        Z_Logger.Log($"[FishingEquipView] OnEquipAction - type={type}, equipId={equipId}");
        callback?.Invoke("EquipAction", new object[] { type, equipId });
    }

    private void OnWatchAd(FishingEquipType type, int equipId)
    {
        Z_Logger.Log($"[FishingEquipView] OnWatchAd - type={type}, equipId={equipId}");
        string componentName = LoadDataManager.Instance?.GetComponentName(equipId) ?? $"未知({equipId})";
        string info = componentName != "未知组件" ? $"看广告解锁装备: {componentName}" : "看广告解锁装备";
        callback?.Invoke("OpenAd", new object[] { info, equipId, "看广告解锁", (System.Action)(() =>
        {
            CommunicateEvent.Modify("Equip_Unlock", equipId);
            UpdateDisplay();
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "解锁成功！");
        })});
    }
}
