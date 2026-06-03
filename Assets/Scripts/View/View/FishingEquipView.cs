using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FishingEquipView : MonoBehaviour
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
    }

    public void SetCallback(System.Action<string, object[]> cb)
    {
        callback = cb;
    }

    public void Init()
    {
        LoadEquipmentIds();
        LoadAllIcons();
    }

    private void LoadEquipmentIds()
    {
        rodIds.Clear();
        lineIds.Clear();
        hookIds.Clear();

        var config = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (config != null && config.items != null)
        {
            foreach (var item in config.items)
            {
                if (item.id >= 3001 && item.id <= 3099)
                    rodIds.Add(item.id);
                else if (item.id >= 3101 && item.id <= 3199)
                    lineIds.Add(item.id);
                else if (item.id >= 3201 && item.id <= 3299)
                    hookIds.Add(item.id);
            }
        }
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();

        var fishingConfig = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
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
    }

    private Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        return null;
    }

    public void Show(FishingEquipType type)
    {
        currentType = type;
        currentPage = 0;

        if (equipItems != null)
        {
            itemsPerPage = equipItems.Count;
        }

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
        if (equipItems == null || equipItems.Count == 0) return;

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
                string name = LoadDataManager.Instance.GetComponentName(equipId);
                if (name == "未知组件")
                {
                    name = GetDefaultName(currentType);
                }
                EquipState state = GetEquipState(currentType, equipId);

                item.SetData(currentType, equipId, icon, name, state, OnEquipItemClick, OnEquipAction, OnWatchAd);
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
            case FishingEquipType.Rod:
                return "钓竿";
            case FishingEquipType.Line:
                return "钓线";
            case FishingEquipType.Hook:
                return "鱼钩";
            default:
                return "装备";
        }
    }

    private EquipState GetEquipState(FishingEquipType type, int equipId)
    {
        EquipmentSlotType slotType = GetSlotType(type);
        int equippedId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType);

        if (equippedId == equipId)
        {
            return EquipState.OwnerUse;
        }

        var inventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        if (inventory.ContainsKey(equipId))
        {
            return EquipState.OwnerUnUse;
        }

        return EquipState.Locked;
    }

    private EquipmentSlotType GetSlotType(FishingEquipType type)
    {
        switch (type)
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
            case FishingEquipType.Rod:
                return rodIds;
            case FishingEquipType.Line:
                return lineIds;
            case FishingEquipType.Hook:
                return hookIds;
            default:
                return rodIds;
        }
    }

    private void OnMaskClick()
    {
        callback?.Invoke("Back", null);
    }

    private void OnCloseClick()
    {
        callback?.Invoke("Back", null);
    }

    private void OnLeftClick()
    {
        List<int> currentIds = GetCurrentIds();
        int totalPages = Mathf.CeilToInt((float)currentIds.Count / itemsPerPage);
        if (totalPages <= 1) return;

        currentPage--;
        if (currentPage < 0)
        {
            currentPage = totalPages - 1;
        }
        UpdateEquipItems();
        UpdatePageText();
    }

    private void OnRightClick()
    {
        List<int> currentIds = GetCurrentIds();
        int totalPages = Mathf.CeilToInt((float)currentIds.Count / itemsPerPage);
        if (totalPages <= 1) return;

        currentPage++;
        if (currentPage >= totalPages)
        {
            currentPage = 0;
        }
        UpdateEquipItems();
        UpdatePageText();
    }

    private void OnEquipItemClick(FishingEquipType type, int equipId)
    {
        callback?.Invoke("OpenInfo", new object[] { type, equipId });
    }

    private void OnEquipAction(FishingEquipType type, int equipId)
    {
        Debug.Log($"[FishingEquipView] OnEquipAction - type={type}, equipId={equipId}");
        callback?.Invoke("EquipAction", new object[] { type, equipId });
    }

    private void OnWatchAd(FishingEquipType type, int equipId)
    {
        Debug.Log($"[FishingEquipView] OnWatchAd - type={type}, equipId={equipId}");
        string componentName = LoadDataManager.Instance.GetComponentName(equipId);
        string info = componentName != "未知组件" ? $"看广告解锁装备: {componentName}" : "看广告解锁装备";
        callback?.Invoke("OpenAd", new object[] { info, equipId, "看广告解锁", (System.Action)(() =>
        {
            CommunicateEvent.Modify("Equip_Unlock", equipId);
            UpdateDisplay();
            CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "解锁成功！");
        })});
    }
}