using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainGameView : BagViewBase
{
    public TimeStatus timeStatus;
    public Button weatherBtn;
    public Button gameTimeBtn;
    public Button bagBtn;
    public Button fishBagBtn;
    public Button mallBtn;
    public Button equipBtn;

    // 菜单控制按钮
    public Button menuOpenBtn;      // 打开菜单按钮
    public Button menuCloseBtn;     // 关闭菜单按钮（面板上的关闭按钮）
    public Button hideRightBtn;     // 右侧隐藏按钮（关闭菜单）

    public Text weatherTxt;
    public Text gameTimeTxt;
    public Text goldTxt;
    public Text baitCountdownTxt;
    public MainTile mainTile;

    // 需要控制显隐的UI面板（例如侧边栏菜单）
    public GameObject menuPanel;

    // 窝料倒计时Text的GameObject（用于控制显隐）
    public GameObject baitCountdownObj;

    // 菜单当前状态
    private bool isMenuOpen = false;

    public override void Init()
    {
        if (isInitialized) return;
        base.Init();

        CommunicateEvent.Register<Vector3>(CommunicateEvent.EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION, OnShowBaitCountdownAtPosition);

        if (bagBtn != null)
        {
            bagBtn.onClick.AddListener(OnBagBtnClick);
        }
        if (fishBagBtn != null)
        {
            fishBagBtn.onClick.AddListener(OnFishBagBtnClick);
        }
        if (mallBtn != null)
        {
            mallBtn.onClick.AddListener(OnMallBtnClick);
        }
        if (equipBtn != null)
        {
            equipBtn.onClick.AddListener(OnEquipBtnClick);
        }
        if (menuOpenBtn != null)
        {
            menuOpenBtn.onClick.AddListener(OnMenuOpenBtnClick);
        }
        if (menuCloseBtn != null)
        {
            menuCloseBtn.onClick.AddListener(OnMenuCloseBtnClick);
        }
        if (hideRightBtn != null)
        {
            hideRightBtn.onClick.AddListener(OnHideRightBtnClick);
        }

        if (mainTile != null)
        {
            Vector3 initialPos = mainTile.transform.position;
            mainTile.Init(initialPos);
        }

        // 初始化菜单状态
        SetMenuPanelState(isMenuOpen);

        isInitialized = true;
    }

    private void OnBagBtnClick()
    {
        CommunicateEvent.Modify("UI_OpenBag");
    }

    private void OnFishBagBtnClick()
    {
        CommunicateEvent.Modify("UI_OpenFishBag");
    }

    private void OnMallBtnClick()
    {
        CommunicateEvent.Modify("UI_OpenMall");
    }

    private void OnEquipBtnClick()
    {
        CommunicateEvent.Modify("UI_OpenEquipment");
    }

    /// <summary>
    /// 打开菜单按钮点击
    /// </summary>
    private void OnMenuOpenBtnClick()
    {
        isMenuOpen = true;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 关闭菜单按钮点击（面板上的关闭按钮）
    /// </summary>
    private void OnMenuCloseBtnClick()
    {
        isMenuOpen = false;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 右侧隐藏按钮点击
    /// </summary>
    private void OnHideRightBtnClick()
    {
        isMenuOpen = false;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 设置菜单面板的显隐状态
    /// </summary>
    /// <param name="open">是否打开</param>
    private void SetMenuPanelState(bool open)
    {
        // 控制面板显隐
        if (menuPanel != null)
        {
            menuPanel.SetActive(open);
        }

        // 控制打开按钮的显隐：菜单关闭时显示，菜单打开时隐藏
        if (menuOpenBtn != null)
        {
            menuOpenBtn.gameObject.SetActive(!open);
        }

        // 关闭按钮和隐藏按钮的显隐：菜单打开时显示，菜单关闭时隐藏
        if (menuCloseBtn != null)
        {
            menuCloseBtn.gameObject.SetActive(open);
        }
        if (hideRightBtn != null)
        {
            hideRightBtn.gameObject.SetActive(open);
        }
    }

    /// <summary>
    /// 外部调用：设置菜单状态（用于初始化或强制设置）
    /// </summary>
    public void SetMenuState(bool open)
    {
        isMenuOpen = open;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 获取菜单当前状态
    /// </summary>
    public bool IsMenuOpen()
    {
        return isMenuOpen;
    }

    void Update()
    {
        UpdateBaitCountdown();
    }

    private void UpdateBaitCountdown()
    {
        bool isContinuousMode = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, 0);

        if (baitCountdownObj != null)
        {
            baitCountdownObj.SetActive(isContinuousMode);
        }

        if (isContinuousMode && baitCountdownTxt != null)
        {
            float remainingTime = CommunicateEvent.Request<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, 0);
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            baitCountdownTxt.text = $"窝料: {minutes:00}:{seconds:00}";
        }
    }

    private void OnShowBaitCountdownAtPosition(Vector3 worldPosition)
    {
        if (baitCountdownObj != null)
        {
            baitCountdownObj.transform.position = worldPosition;
            baitCountdownObj.SetActive(true);
        }
    }

    public void InitTimeNameDic()
    {
    }

    public void UpdateTime(TimeStatus status, string timeName)
    {
        gameTimeTxt.text = timeName;
        timeStatus = status;
    }

    public void UpdateWeather(int weatherId, string weatherName)
    {
        if (weatherTxt != null)
        {
            weatherTxt.text = weatherName;
        }
    }

    public void ShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Debug.Log("ShowCatchResult");
        if (mainTile != null)
        {
            mainTile.EnqueueCatchResult(itemName, weight, icon);
        }
    }

    public void UpdateGold(int goldAmount)
    {
        if (goldTxt != null)
        {
            goldTxt.text = $"金币: {goldAmount}";
        }
    }
}