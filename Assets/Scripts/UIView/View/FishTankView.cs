// 路径：Assets/Scripts/UIView/View/FishTankView.cs
using UnityEngine;
using UnityEngine.UI;
using static PlayerDataManager;

public class FishTankView : BaseView
{
    [Header("===== 调试 =====")]
    public bool enableDebugLog = false;

    [Header("===== 主面板引用 =====")]
    public FishTankMainPanel fishTankMainPanel;

    protected override void Awake()
    {
        base.Awake();
        if (closeBtn != null)
            closeBtn.onClick.AddListener(OnCloseButtonClick);
    }

    public void OpenFishTank()
    {
        LogDebug("OpenFishTank");
        ShowView();
        if (fishTankMainPanel != null)
            fishTankMainPanel.OpenPanel();
        CommunicateEvent.Modify(FishTankMessage.OpenFishTank.ToString());
    }

    public void CloseFishTank()
    {
        LogDebug("CloseFishTank");
        if (fishTankMainPanel != null)
            fishTankMainPanel.ClosePanel();
        HideView();
        CommunicateEvent.Modify(FishTankMessage.CloseFishTank.ToString());
    }

    public void RefreshAll()
    {
        if (fishTankMainPanel != null)
            fishTankMainPanel.RefreshAll();
    }

    protected override void OnCloseButtonClick()
    {
        base.OnCloseButtonClick();
        CloseFishTank();
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankView] {msg}");
    }
}
