// ============================================================
// 文件: PetView.cs
// 说明: 宠物主界面
// 路径: Assets/Scripts/UI/
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PetView : BaseView
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    [Header("容器")]
    public Transform container;
    public GameObject petPrefab;

    [Header("排序按钮")]
    public Button sortByCatchOrderBtn;
    public Button sortByRarityBtn;
    public Button sortByPriceBtn;

    [Header("操作按钮")]
    public Button selectAllBtn;
    public Button sellBtn;
    public Button lockBtn;
    public Button viewInfoBtn;

    [Header("宠物栏")]
    public Text storageText;
    public Button upgradeStorageBtn;

    [Header("选中总价")]
    public Text sellPriceText;

    [Header("子面板")]
    public UI_PetInfoPanel petInfoPanel;
    public UI_PetFeedPanel petFeedPanel;

    // ============================================================
    // 运行时数据
    // ============================================================

    private enum SortType { CatchOrder, Rarity, Price }
    private SortType _currentSort = SortType.CatchOrder;

    private List<UI_PetPrefab> _petPrefabs = new List<UI_PetPrefab>();
    private List<PlayerPetData> _sortedPets = new List<PlayerPetData>();

    // ============================================================
    // BaseView 生命周期
    // ============================================================

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        // 引用校验（Inspector 拖拽，不兜底，直接报错）
        if (petInfoPanel == null)
            Z_Logger.LogError("[PetView] petInfoPanel 未在 Inspector 绑定");
        if (petFeedPanel == null)
            Z_Logger.LogError("[PetView] petFeedPanel 未在 Inspector 绑定");

        CloseAllSubPanels();
        BindButtons();

        // ★ 修复：PetsUpdated 必须用常量，与 PlayerDataManager 广播一致
        CommunicateEvent.Register(CommunicateEvent.EVENT_PETS_UPDATED, OnPetsUpdated);
        CommunicateEvent.Register(PlayerDataManager.PetMessage.PetStorageUpdated.ToString(), OnStorageUpdated);
        CommunicateEvent.Register(PlayerDataManager.PetMessage.PetLockChanged.ToString(), OnPetsUpdated);

        // 初始化子面板（注册事件 + 绑按钮）
        if (petInfoPanel != null) petInfoPanel.Init();
        if (petFeedPanel != null) petFeedPanel.Init();

        isInitialized = true;
    }

    private void CloseAllSubPanels()
    {
        if (petInfoPanel != null) petInfoPanel.gameObject.SetActive(false);
        if (petFeedPanel != null) petFeedPanel.gameObject.SetActive(false);
    }

    protected override void PreShow()
    {
        base.PreShow();

        CloseAllSubPanels();

        // ★ 修复：先刷本地缓存（可能为空），再拉网络，网络回来后由事件驱动再刷
        Refresh();

        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchPlayerPets();
            NetServerManager.Instance.FetchPetStorageStatus();
        }
    }

    protected override void PreHide()
    {
        base.PreHide();
        CloseAllSubPanels();
    }

    private void OnDestroy()
    {
        // ★ 修复：注销事件名必须和注册时一致
        CommunicateEvent.Unregister(CommunicateEvent.EVENT_PETS_UPDATED, OnPetsUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.PetMessage.PetStorageUpdated.ToString(), OnStorageUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.PetMessage.PetLockChanged.ToString(), OnPetsUpdated);

        // 销毁子面板（注销事件）
        if (petInfoPanel != null) petInfoPanel.Dispose();
        if (petFeedPanel != null) petFeedPanel.Dispose();
    }

    // ============================================================
    // 公开方法
    // ============================================================

    public void OpenPetView() => ShowView();
    public void ClosePetView() => HideView();

    // ============================================================
    // 按钮绑定
    // ============================================================

    private void BindButtons()
    {
        if (sortByCatchOrderBtn != null) sortByCatchOrderBtn.onClick.AddListener(() => SetSort(SortType.CatchOrder));
        if (sortByRarityBtn != null) sortByRarityBtn.onClick.AddListener(() => SetSort(SortType.Rarity));
        if (sortByPriceBtn != null) sortByPriceBtn.onClick.AddListener(() => SetSort(SortType.Price));

        if (selectAllBtn != null) selectAllBtn.onClick.AddListener(OnSelectAllClick);
        if (sellBtn != null) sellBtn.onClick.AddListener(OnSellClick);
        if (lockBtn != null) lockBtn.onClick.AddListener(OnLockClick);
        if (viewInfoBtn != null) viewInfoBtn.onClick.AddListener(OnViewInfoClick);

        if (upgradeStorageBtn != null) upgradeStorageBtn.onClick.AddListener(OnUpgradeStorageClick);
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnPetsUpdated() => Refresh();
    private void OnStorageUpdated() => RefreshStorageText();

    // ============================================================
    // 刷新
    // ============================================================

    private void Refresh()
    {
        if (PlayerDataManager.Instance == null) return;

        _sortedPets = SortPets(PlayerDataManager.Instance.PlayerPets);

        EnsurePetPrefabs(_sortedPets.Count);

        for (int i = 0; i < _petPrefabs.Count; i++)
        {
            if (i < _sortedPets.Count)
            {
                _petPrefabs[i].gameObject.SetActive(true);
                _petPrefabs[i].Init(_sortedPets[i], OnPetSelectionChanged);
            }
            else
            {
                _petPrefabs[i].gameObject.SetActive(false);
            }
        }

        RefreshStorageText();
        RefreshSellPriceText();
    }

    private void RefreshStorageText()
    {
        if (storageText == null) return;

        var status = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.PetStorageStatus : null;
        storageText.text = status != null ? $"{status.used}/{status.capacity}" : "0/0";
    }

    /// <summary>刷新"选中宠物总售价"</summary>
    private void RefreshSellPriceText()
    {
        if (sellPriceText == null) return;

        int total = 0;
        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf && p.IsSelected)
            {
                total += GetPetSellPrice(p.PetId);
            }
        }

        sellPriceText.text = total.ToString();
    }

    private List<PlayerPetData> SortPets(IReadOnlyList<PlayerPetData> pets)
    {
        var list = new List<PlayerPetData>(pets);

        switch (_currentSort)
        {
            case SortType.CatchOrder:
                list.Sort((a, b) => a.obtainedAt.CompareTo(b.obtainedAt));
                break;
            case SortType.Rarity:
                list.Sort((a, b) => b.rarityId.CompareTo(a.rarityId));
                break;
            case SortType.Price:
                list.Sort((a, b) => GetPetSellPrice(b.petId).CompareTo(GetPetSellPrice(a.petId)));
                break;
        }

        return list;
    }

    private int GetPetSellPrice(int petId)
    {
        var item = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetItemById(petId) : null;
        return item?.sellPrice ?? 0;
    }

    // ============================================================
    // 按钮池
    // ============================================================

    private void EnsurePetPrefabs(int count)
    {
        while (_petPrefabs.Count < count)
        {
            if (petPrefab == null || container == null)
            {
                Z_Logger.LogError("[PetView] petPrefab 或 container 未绑定");
                return;
            }

            var go = Instantiate(petPrefab, container);
            var prefab = go.GetComponent<UI_PetPrefab>();
            if (prefab == null)
            {
                Z_Logger.LogError("[PetView] petPrefab 上没有 UI_PetPrefab 组件");
                Destroy(go);
                return;
            }
            _petPrefabs.Add(prefab);
        }
    }

    // ============================================================
    // 排序
    // ============================================================

    private void SetSort(SortType type)
    {
        _currentSort = type;
        Refresh();
    }

    private void OnPetSelectionChanged(UI_PetPrefab prefab)
    {
        RefreshSellPriceText();
    }

    // ============================================================
    // 全选
    // ============================================================

    private void OnSelectAllClick()
    {
        bool allSelected = true;
        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf && !p.IsSelected)
            {
                allSelected = false;
                break;
            }
        }

        bool newState = !allSelected;

        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf)
            {
                p.SetSelection(newState);
            }
        }

        RefreshSellPriceText();
    }

    // ============================================================
    // 出售
    // ============================================================

    private void OnSellClick()
    {
        List<int> ids = new List<int>();
        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf && p.IsSelected)
            {
                ids.Add(p.PetInstanceId);
            }
        }

        if (ids.Count == 0)
        {
            ShowTip("请选择要出售的宠物");
            return;
        }

        string desc = $"确定出售选中的 {ids.Count} 只宠物？";
        GameUIManager.ShowInfoMessage(desc, () =>
        {
            if (ids.Count == 1)
                NetServerManager.Instance.SellPet(ids[0], OnSellResult);
            else
                NetServerManager.Instance.SellPetsBatch(ids, OnSellResult);
        });
    }

    private void OnSellResult(bool success, string message, int gold)
    {
        ShowTip(message);
        if (success)
        {
            Refresh();
        }
    }

    // ============================================================
    // 锁定
    // ============================================================

    private void OnLockClick()
    {
        List<int> ids = new List<int>();
        bool allLocked = true;

        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf && p.IsSelected)
            {
                ids.Add(p.PetInstanceId);
                if (!p.IsLocked) allLocked = false;
            }
        }

        if (ids.Count == 0)
        {
            ShowTip("请选择要锁定的宠物");
            return;
        }

        bool newLocked = !allLocked;

        if (ids.Count == 1)
        {
            NetServerManager.Instance.SetPetLocked(ids[0], newLocked, (success, message) =>
            {
                ShowTip(message);
                if (success) Refresh();
            });
        }
        else
        {
            NetServerManager.Instance.SetPetsLockedBatch(ids, newLocked, (success, message) =>
            {
                ShowTip(message);
                if (success) Refresh();
            });
        }
    }

    // ============================================================
    // 查看详情
    // ============================================================

    private void OnViewInfoClick()
    {
        if (petInfoPanel == null)
        {
            Z_Logger.LogWarning("[PetView] petInfoPanel 未绑定");
            return;
        }

        List<int> ids = new List<int>();
        foreach (var p in _petPrefabs)
        {
            if (p != null && p.gameObject.activeSelf && p.IsSelected)
            {
                ids.Add(p.PetInstanceId);
            }
        }

        if (ids.Count == 0) { ShowTip("请选择一只宠物"); return; }
        if (ids.Count > 1) { ShowTip("只能查看一只宠物"); return; }

        int targetId = ids[0];
        int startIdx = _sortedPets.FindIndex(p => p.petInstanceId == targetId);
        if (startIdx < 0) startIdx = 0;

        petInfoPanel.Open(_sortedPets, startIdx, petFeedPanel);
    }

    // ============================================================
    // 升级宠物栏
    // ============================================================

    private void OnUpgradeStorageClick()
    {
        var status = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.PetStorageStatus : null;
        if (status == null) return;

        if (status.level >= status.maxLevel)
        {
            ShowTip("宠物栏已满级");
            return;
        }

        int cost = GetNextUpgradeCost(status.level);
        int currentGold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);

        if (currentGold >= cost)
        {
            NetServerManager.Instance.UpgradePetStorage((success, message, data) =>
            {
                ShowTip(message);
                if (success) Refresh();
            });
        }
        else
        {
            string adInfo = $"金币不足！升级宠物栏需要{cost}金币，观看广告可免费升级！";
            GameUIManager.Instance.ShowAdvertising(adInfo, 0, "看广告升级", (bool adSuccess) =>
            {
                if (adSuccess)
                {
                    NetServerManager.Instance.UpgradePetStorageByAd((success, message, data) =>
                    {
                        ShowTip(message);
                        if (success) Refresh();
                    });
                }
                else
                {
                    ShowTip("广告未完成");
                }
            });
        }
    }

    private int GetNextUpgradeCost(int currentLevel)
    {
        return 1000;
    }

    // ============================================================
    // 工具
    // ============================================================

    private void ShowTip(string msg)
    {
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, msg);
    }
}
