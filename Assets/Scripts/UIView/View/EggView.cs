// ============================================================
// 文件: EggView.cs
// 说明: 蛋 / 孵化主界面
//       6 个按钮 = 6 个蛋种类（从 eggs.json 读，按 id 升序）
//       slotIndex 固定映射：10201→0, 10202→1, ..., 10206→5
// 路径: Assets/Scripts/UI/
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EggView : BaseView
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    [Header("槽位容器")]
    public Transform container;
    public GameObject eggBtnPrefab;

    [Header("蛋操作面板")]
    public UI_EggProducePanel eggProducePanel;

    [Header("孵化成功面板")]
    public UI_EggHatchSuccessPanel hatchSuccessPanel;

    // ============================================================
    // 运行时数据
    // ============================================================

    private List<UI_EggPrefab> _eggBtns = new List<UI_EggPrefab>();
    private readonly List<int> _localRemaining = new List<int>();
    private bool _btnInitialized = false;
    private float _timerAccumulator = 0f;
    private List<int> _eggIds = new List<int>();

    // ============================================================
    // BaseView 生命周期
    // ============================================================

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        // ✅ 初始化时关闭所有子面板
        CloseAllSubPanels();

        CollectEggIds();
        EnsureButtons();

        isInitialized = true;
    }

    /// <summary>关闭所有子面板</summary>
    private void CloseAllSubPanels()
    {
        if (eggProducePanel != null)
        {
            eggProducePanel.gameObject.SetActive(false);
        }
        if (hatchSuccessPanel != null)
        {
            hatchSuccessPanel.gameObject.SetActive(false);
        }
    }

    protected override void PreShow()
    {
        base.PreShow();

        // ✅ 每次打开时也把子面板关掉
        CloseAllSubPanels();

        // 打开时拉一次服务器数据
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchEggHatchSlots((success, slots) =>
            {
                if (success)
                {
                    RefreshAllSlots();
                }
            });
        }
        else
        {
            RefreshAllSlots();
        }

        CommunicateEvent.Register(PlayerDataManager.EggMessage.EggSlotsUpdated.ToString(), OnEggSlotsUpdated);
        CommunicateEvent.Register(PlayerDataManager.EggMessage.EggDataLoaded.ToString(), OnEggSlotsUpdated);
    }

    protected override void PreHide()
    {
        base.PreHide();

        // ✅ 关闭时也关掉子面板
        CloseAllSubPanels();

        CommunicateEvent.Unregister(PlayerDataManager.EggMessage.EggSlotsUpdated.ToString(), OnEggSlotsUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.EggMessage.EggDataLoaded.ToString(), OnEggSlotsUpdated);
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister(PlayerDataManager.EggMessage.EggSlotsUpdated.ToString(), OnEggSlotsUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.EggMessage.EggDataLoaded.ToString(), OnEggSlotsUpdated);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (!_btnInitialized) return;

        _timerAccumulator += Time.deltaTime;
        if (_timerAccumulator >= 1f)
        {
            _timerAccumulator -= 1f;
            TickLocalCountdown();
        }
    }

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    public void OpenEggView()
    {
        Z_Logger.Log("[EggView] 打开蛋界面");
        ClearAllLocalState();
        ShowView();

        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchEggHatchSlots((success, slots) =>
            {
                if (success) RefreshAllSlots();
            });
        }
    }

    public void CloseEggView()
    {
        Z_Logger.Log("[EggView] 关闭蛋界面");
        HideView();
    }

    private void ClearAllLocalState()
    {
        for (int i = 0; i < _localRemaining.Count; i++)
        {
            _localRemaining[i] = -1;
        }
    }

    // ============================================================
    // 收集蛋种类
    // ============================================================

    private void CollectEggIds()
    {
        _eggIds.Clear();

        if (LoadDataManager.Instance == null)
        {
            Z_Logger.LogError("[EggView] LoadDataManager 为 null");
            return;
        }

        var allEggs = LoadDataManager.Instance.GetAllEggs();
        if (allEggs == null || allEggs.Count == 0)
        {
            Z_Logger.LogWarning("[EggView] eggs.json 没有蛋数据");
            return;
        }

        foreach (var egg in allEggs)
        {
            _eggIds.Add(egg.id);
        }

        Z_Logger.Log($"[EggView] 收集到 {_eggIds.Count} 种蛋: {string.Join(", ", _eggIds)}");
    }

    // ============================================================
    // 按钮初始化
    // ============================================================

    private void EnsureButtons()
    {
        if (_btnInitialized) return;
        if (eggBtnPrefab == null)
        {
            Z_Logger.LogError("[EggView] eggBtnPrefab 未绑定");
            return;
        }
        if (container == null)
        {
            Z_Logger.LogError("[EggView] container 未绑定");
            return;
        }

        foreach (var b in _eggBtns)
        {
            if (b != null) Destroy(b.gameObject);
        }
        _eggBtns.Clear();
        _localRemaining.Clear();

        for (int i = 0; i < _eggIds.Count; i++)
        {
            int eggId = _eggIds[i];
            int slotIndex = eggId - _eggIds[0];

            var go = Instantiate(eggBtnPrefab, container);
            var btn = go.GetComponent<UI_EggPrefab>();
            if (btn == null)
            {
                // ✅ 修正文案
                Z_Logger.LogError("[EggView] eggBtnPrefab 上没有 UI_EggPrefab 组件");
                Destroy(go);
                continue;
            }

            btn.Init(eggId, slotIndex, OnEggBtnClicked);
            _eggBtns.Add(btn);
            _localRemaining.Add(-1);
        }

        _btnInitialized = true;
        Z_Logger.Log($"[EggView] 生成 {_eggBtns.Count} 个蛋按钮");
    }

    // ============================================================
    // 数据刷新
    // ============================================================

    private void OnEggSlotsUpdated()
    {
        RefreshAllSlots();
    }

    private void RefreshAllSlots()
    {
        if (PlayerDataManager.Instance == null) return;

        var slots = PlayerDataManager.Instance.EggHatchSlots;

        for (int i = 0; i < _eggBtns.Count; i++)
        {
            int eggId = _eggIds[i];
            int slotIndex = _eggBtns[i].SlotIndex;

            EggHatchSlotData slot = null;
            if (slots != null)
            {
                foreach (var s in slots)
                {
                    if (s != null && s.slotIndex == slotIndex && s.isOccupied && !s.isClaimed)
                    {
                        slot = s;
                        break;
                    }
                }
            }

            int invCount = PlayerDataManager.Instance.GetItemQuantity(eggId);

            if (slot != null)
            {
                _eggBtns[i].SetSlot(slot, invCount);
                _localRemaining[i] = slot.remainingSeconds;
            }
            else if (invCount > 0)
            {
                _eggBtns[i].SetUnproduced(eggId, invCount);
                _localRemaining[i] = -1;
            }
            else
            {
                _eggBtns[i].SetEmpty();
                _localRemaining[i] = -1;
            }
        }
    }

    // ============================================================
    // 本地倒计时
    // ============================================================

    private void TickLocalCountdown()
    {
        if (PlayerDataManager.Instance == null) return;

        for (int i = 0; i < _eggBtns.Count; i++)
        {
            if (_localRemaining[i] <= 0) continue;

            _localRemaining[i]--;
            if (_localRemaining[i] < 0) _localRemaining[i] = 0;

            bool completed = _localRemaining[i] <= 0;
            _eggBtns[i].UpdateRemainingSeconds(_localRemaining[i], completed);

            PlayerDataManager.Instance.UpdateSlotRemainingSeconds(_eggBtns[i].SlotIndex, _localRemaining[i]);
        }
    }

    // ============================================================
    // 按钮点击
    // ============================================================

    private void OnEggBtnClicked(int eggId, int slotIndex)
    {
        if (eggProducePanel == null)
        {
            Z_Logger.LogWarning("[EggView] eggProducePanel 未绑定");
            return;
        }

        eggProducePanel.Open(eggId, slotIndex, OnProducePanelClosed);
    }

    private void OnProducePanelClosed()
    {
        RefreshAllSlots();
    }

    // ============================================================
    // 孵化成功
    // ============================================================

    public void ShowHatchSuccess(PlayerPetData pet)
    {
        if (hatchSuccessPanel != null && pet != null)
        {
            hatchSuccessPanel.Open(pet);
        }
    }
}
