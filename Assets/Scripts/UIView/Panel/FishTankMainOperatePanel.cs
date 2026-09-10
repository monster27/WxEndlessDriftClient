// 路径：Assets/Scripts/UIView/Panel/FishTankMainOperatePanel.cs
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using static PlayerDataManager;

public class FishTankMainOperatePanel : MonoBehaviour
{
    [Header("===== 顶部UI =====")]
    [SerializeField] private Button leftBtn;
    [SerializeField] private Button rightBtn;
    [SerializeField] private Text tankNameText;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text harvestText;
    [SerializeField] private Button lockBtn;
    [SerializeField] private GameObject lockIcon;

    [Header("===== 底部功能按钮 =====")]
    [SerializeField] private Button manageBtn;
    [SerializeField] private Button decorationBtn;

    private Action _onLeft;
    private Action _onRight;
    private Action _onLock;
    private Action _onManage;
    private Action _onDecoration;
    private bool _isInitialized = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void Init()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (leftBtn != null) leftBtn.onClick.AddListener(() => _onLeft?.Invoke());
        if (rightBtn != null) rightBtn.onClick.AddListener(() => _onRight?.Invoke());
        if (lockBtn != null) lockBtn.onClick.AddListener(() => _onLock?.Invoke());
        if (manageBtn != null) manageBtn.onClick.AddListener(() => _onManage?.Invoke());
        if (decorationBtn != null) decorationBtn.onClick.AddListener(() => _onDecoration?.Invoke());
    }

    public void SetLeftCallback(Action cb) => _onLeft = cb;
    public void SetRightCallback(Action cb) => _onRight = cb;
    public void SetLockCallback(Action cb) => _onLock = cb;
    public void SetManageCallback(Action cb) => _onManage = cb;
    public void SetDecorationCallback(Action cb) => _onDecoration = cb;

    public void OpenPanel() => gameObject.SetActive(true);
    public void ClosePanel() => gameObject.SetActive(false);

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void SetEmpty()
    {
        if (tankNameText != null) tankNameText.text = "暂无鱼缸";
        if (capacityText != null) capacityText.text = "0/0";
        if (harvestText != null) harvestText.gameObject.SetActive(false);
        if (lockIcon != null) lockIcon.SetActive(false);
        if (lockBtn != null) lockBtn.gameObject.SetActive(false);
        if (leftBtn != null) leftBtn.interactable = false;
        if (rightBtn != null) rightBtn.interactable = false;
    }

    public void RefreshTopUI(FishTankStatusData tank, bool canSwitch)
    {
        if (tank == null) { SetEmpty(); return; }

        var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
        var fishList = PlayerDataService.Instance?.GetTankFishList(tank.tankId) ?? new List<FishDetailData>();

        if (tankNameText != null)
            tankNameText.text = config?.name ?? $"鱼缸{tank.tankId}";

        if (capacityText != null)
            capacityText.text = $"{fishList.Count}/{tank.capacity}";

        if (harvestText != null)
        {
            if (config?.type == "special" && tank.isUnlocked && fishList.Count > 0)
            {
                harvestText.text = $"每小时: {fishList.Count * 10} 金币";
                harvestText.gameObject.SetActive(true);
            }
            else
            {
                harvestText.gameObject.SetActive(false);
            }
        }

        if (lockIcon != null) lockIcon.SetActive(!tank.isUnlocked);
        if (lockBtn != null) lockBtn.gameObject.SetActive(!tank.isUnlocked);
        if (leftBtn != null) leftBtn.interactable = canSwitch;
        if (rightBtn != null) rightBtn.interactable = canSwitch;
    }

    private void OnDestroy()
    {
        if (leftBtn != null) leftBtn.onClick.RemoveAllListeners();
        if (rightBtn != null) rightBtn.onClick.RemoveAllListeners();
        if (lockBtn != null) lockBtn.onClick.RemoveAllListeners();
        if (manageBtn != null) manageBtn.onClick.RemoveAllListeners();
        if (decorationBtn != null) decorationBtn.onClick.RemoveAllListeners();
    }
}
