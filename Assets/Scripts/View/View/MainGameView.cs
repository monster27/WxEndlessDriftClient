using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

public class MainGameView : BagViewBase
{
    public TimeStatus timeStatus;
    public Button bagBtn;
    public Button fishBagBtn;
    public Button mallBtn;
    public Button equipBtn;
    public Button weatherAndTimeBtn;
    public Button centerCameraBtn;  // 居中摄像头按钮

    // 菜单控制按钮
    public Button menuOpenBtn;      // 打开菜单按钮
    public Button menuCloseBtn;     // 关闭菜单按钮（面板上的关闭按钮）
    public Button hideRightBtn;     // 右侧隐藏按钮（关闭菜单）

    public Text weatherTxt;
    public Text gameTimeTxt;
    public Image weatherIcon;
    public Image timeIcon;
    public Text goldTxt;
    public Text baitCountdownTxt;
    public Text baitCountTxt;  // 窝料数量显示
    public Text fishCountTxt;  // 鱼篓数量显示
    public MainTile mainTile;

    // 需要控制显隐的UI面板（例如侧边栏菜单）
    public GameObject menuPanel;

    // 窝料倒计时Text的GameObject（用于控制显隐）
    public GameObject baitCountdownObj;

    // 当前窝料数量
    private int currentBaitCount = 0;

    // 菜单当前状态
    private bool isMenuOpen = false;

    public enum DisplayMode
    {
        Text,
        Icon
    }

    private DisplayMode currentDisplayMode = DisplayMode.Text;

    private int currentWeatherId = 301;
    private int currentTimeSlotId = 401;

    public override void Init()
    {
        if (isInitialized) return;
        base.Init();

        CommunicateEvent.Register<Vector3>(CommunicateEvent.EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION, OnShowBaitCountdownAtPosition);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        // 注册窝料数量变化事件
        CommunicateEvent.Register("BaitCountChanged", OnBaitCountChanged);
        // 注册鱼饵数据更新事件
        CommunicateEvent.Register("BaitDataUpdated", OnBaitDataUpdated);
        // 注册鱼篓数据更新事件
        CommunicateEvent.Register("FishBagDataUpdated", OnFishBagDataUpdated);
        // 天气和时间变化事件由EnvManager统一处理，然后调用UpdateWeather和UpdateTime方法

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
        if (weatherAndTimeBtn != null)
        {
            weatherAndTimeBtn.onClick.AddListener(OnWeatherAndTimeBtnClick);
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
        if (centerCameraBtn != null)
        {
            centerCameraBtn.onClick.AddListener(OnCenterCameraBtnClick);
        }

        if (mainTile != null)
        {
            Vector3 initialPos = mainTile.transform.position;
            mainTile.Init(initialPos);
        }

        // 初始化菜单状态
        SetMenuPanelState(isMenuOpen);

        // 初始化窝料数量显示
        UpdateBaitCountDisplay();

        // 初始化显示模式为Text模式
        currentDisplayMode = DisplayMode.Text;
        UpdateDisplayMode();

        isInitialized = true;
    }

    private void OnBagBtnClick()
    {
        Debug.Log("[MainGameView] OnBagBtnClick - 点击背包按钮");
        CommunicateEvent.Modify("UI_OpenBag");
    }

    private void OnFishBagBtnClick()
    {
        Debug.Log("[MainGameView] OnFishBagBtnClick - 点击鱼背包按钮");
        CommunicateEvent.Modify("UI_OpenFishBag");
    }

    private void OnMallBtnClick()
    {
        Debug.Log("[MainGameView] OnMallBtnClick - 点击商城按钮");
        CommunicateEvent.Modify("UI_OpenMall");
    }

    private void OnEquipBtnClick()
    {
        Debug.Log("[MainGameView] OnEquipBtnClick - 点击装备按钮");
        CommunicateEvent.Modify("UI_OpenEquipment");
    }

    private void OnWeatherAndTimeBtnClick()
    {
        SwitchDisplayMode();
    }

    private void SwitchDisplayMode()
    {
        if (currentDisplayMode == DisplayMode.Text)
        {
            currentDisplayMode = DisplayMode.Icon;
        }
        else
        {
            currentDisplayMode = DisplayMode.Text;
        }
        UpdateDisplayMode();
    }

    private void UpdateDisplayMode()
    {
        if (weatherTxt != null) weatherTxt.gameObject.SetActive(currentDisplayMode == DisplayMode.Text);
        if (gameTimeTxt != null) gameTimeTxt.gameObject.SetActive(currentDisplayMode == DisplayMode.Text);
        if (weatherIcon != null) weatherIcon.gameObject.SetActive(currentDisplayMode == DisplayMode.Icon);
        if (timeIcon != null) timeIcon.gameObject.SetActive(currentDisplayMode == DisplayMode.Icon);

        if (currentDisplayMode == DisplayMode.Icon)
        {
            UpdateWeatherIcon(currentWeatherId);
            UpdateTimeIcon(currentTimeSlotId);
        }
        Debug.Log($"[MainGameView] 切换显示模式: {currentDisplayMode}");
    }

    private void UpdateWeatherIcon(int weatherId)
    {
        if (weatherIcon == null) return;
        string path = $"UI/Icon/WeatherIcon/{weatherId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            weatherIcon.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"[MainGameView] 未找到天气图标: {path}");
        }
    }

    private void UpdateTimeIcon(int timeSlotId)
    {
        if (timeIcon == null) return;
        string path = $"UI/Icon/TimeIcon/{timeSlotId}";
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            timeIcon.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"[MainGameView] 未找到时段图标: {path}");
        }
    }

    /// <summary>
    /// 打开菜单按钮点击
    /// </summary>
    private void OnMenuOpenBtnClick()
    {
        Debug.Log("[MainGameView] OnMenuOpenBtnClick - 点击打开菜单");
        isMenuOpen = true;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 关闭菜单按钮点击（面板上的关闭按钮）
    /// </summary>
    private void OnMenuCloseBtnClick()
    {
        Debug.Log("[MainGameView] OnMenuCloseBtnClick - 点击关闭菜单");
        isMenuOpen = false;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 右侧隐藏按钮点击
    /// </summary>
    private void OnHideRightBtnClick()
    {
        Debug.Log("[MainGameView] OnHideRightBtnClick - 点击隐藏右侧");
        isMenuOpen = false;
        SetMenuPanelState(isMenuOpen);
    }

    /// <summary>
    /// 居中摄像头按钮点击
    /// </summary>
    private void OnCenterCameraBtnClick()
    {
        Debug.Log("[MainGameView] OnCenterCameraBtnClick - 点击居中摄像头");
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.MoveToCenter();
        }
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

    // 本地连续模式剩余时间（用于UI倒计时显示）
    private float localContinuousModeTime = 0f;
    private bool isLocalTimeSynced = false;

    void Update()
    {
        UpdateBaitCountdown();
    }

    private void UpdateBaitCountdown()
    {
        // 每帧从服务器获取最新的剩余时间（用于同步）
        float serverRemainingTime = CommunicateEvent.Request<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, 0);
        
        // 确保剩余时间非负
        serverRemainingTime = Mathf.Max(0f, serverRemainingTime);
        
        // 同步服务器时间到本地（当时间变化超过1秒时）
        if (Mathf.Abs(serverRemainingTime - localContinuousModeTime) > 1f || !isLocalTimeSynced)
        {
            localContinuousModeTime = serverRemainingTime;
            isLocalTimeSynced = true;
        }
        
        // 本地倒计时递减
        if (localContinuousModeTime > 0)
        {
            localContinuousModeTime -= Time.deltaTime;
            if (localContinuousModeTime < 0)
            {
                localContinuousModeTime = 0;
            }
        }
        
        float remainingTime = localContinuousModeTime;

        if (baitCountdownObj != null)
        {
            // 只有当剩余时间大于0时才显示倒计时
            baitCountdownObj.SetActive(remainingTime > 0);
        }

        if (remainingTime > 0 && baitCountdownTxt != null)
        {
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

    /// <summary>
    /// 更新窝料数量显示
    /// </summary>
    public void UpdateBaitCountDisplay()
    {
        // 从服务器获取当前窝料数量
        currentBaitCount = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, 0);
        
        if (baitCountTxt != null)
        {
            baitCountTxt.text = $"窝料: {currentBaitCount}";
        }
    }

    /// <summary>
    /// 窝料数量变化事件处理器
    /// </summary>
    private void OnBaitCountChanged()
    {
        Debug.Log("[MainGameView] OnBaitCountChanged - 窝料数量变化");
        UpdateBaitCountDisplay();
    }

    private void OnBaitDataUpdated()
    {
        Debug.Log("[MainGameView] OnBaitDataUpdated - 鱼饵数据更新");
        UpdateBaitCountDisplay();
    }

    private void OnFishBagDataUpdated()
    {
        Debug.Log("[MainGameView] OnFishBagDataUpdated - 鱼篓数据更新");
        UpdateFishCountDisplay();
    }

    private void UpdateFishCountDisplay()
    {
        if (fishCountTxt == null) return;

        Dictionary<int, int> fishInventory = PlayerDataManager.Instance?.GetFishInventory();
        if (fishInventory == null) { fishCountTxt.text = " 0/0"; return; }

        int totalCount = 0;
        foreach (var kvp in fishInventory)
            totalCount += kvp.Value;

        int maxCapacity = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, 0);
        fishCountTxt.text = $"{totalCount}/{maxCapacity}";
    }

    public void InitTimeNameDic()
    {
    }

    public void UpdateTime(TimeStatus status, string timeName)
    {
        Debug.Log($"[MainGameView] UpdateTime called - status={status}, timeName={timeName}, gameTimeTxt={gameTimeTxt != null}");
        
        if (gameTimeTxt != null)
        {
            gameTimeTxt.text = timeName;
            Debug.Log($"[MainGameView] 时间文本已更新: {timeName}");
        }
        else
        {
            Debug.LogWarning("[MainGameView] gameTimeTxt 为 null，无法更新文本");
        }
        
        timeStatus = status;
        currentTimeSlotId = 401 + (int)status;
        Debug.Log($"[MainGameView] currentTimeSlotId={currentTimeSlotId}");
        UpdateTimeIcon(currentTimeSlotId);
    }

    public void UpdateWeather(int weatherId, string weatherName)
    {
        Debug.Log($"[MainGameView] UpdateWeather called - weatherId={weatherId}, weatherName={weatherName}, weatherTxt={weatherTxt != null}");
        
        if (weatherTxt != null)
        {
            weatherTxt.text = weatherName;
            Debug.Log($"[MainGameView] 天气文本已更新: {weatherName}");
        }
        else
        {
            Debug.LogWarning("[MainGameView] weatherTxt 为 null，无法更新文本");
        }
        
        currentWeatherId = weatherId;
        UpdateWeatherIcon(currentWeatherId);
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

    private void OnGoldChanged(Dictionary<string, object> data)
    {
        if (data.TryGetValue("gold", out object goldObj))
        {
            int gold = System.Convert.ToInt32(goldObj);
            UpdateGold(gold);
        }
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Unregister("BaitCountChanged", OnBaitCountChanged);
        CommunicateEvent.Unregister("BaitDataUpdated", OnBaitDataUpdated);
        CommunicateEvent.Unregister("FishBagDataUpdated", OnFishBagDataUpdated);
    }
}