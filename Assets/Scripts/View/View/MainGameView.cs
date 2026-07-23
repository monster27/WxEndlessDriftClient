using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

public class MainGameView : BaseView
{
    public TimeStatus timeStatus;

    public Button hidePanelBtn;     // 右侧隐藏按钮（关闭菜单）
    public Button showPanelBtn;
    public Button bagBtn;
    public Button fishBagBtn;
    public Button mallBtn;
    public Button equipBtn;
    public Button weatherAndTimeBtn;
    public Button centerCameraBtn;  // 居中摄像头按钮
    public Button MapBtn;

    // 菜单控制按钮
    public Button menuOpenBtn;      // 打开菜单按钮
    public Button menuCloseBtn;     // 关闭菜单按钮（面板上的关闭按钮）

    public GameObject btnPanel;
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

    // 天气时段渐隐字段
    private Coroutine fadeCoroutine;
    private bool isFading = false;  // 是否正在渐隐中

    private int currentWeatherId = 301;
    private int currentTimeSlotId = 401;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        CommunicateEvent.Register<Vector3>(CommunicateEvent.EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION, OnShowBaitCountdownAtPosition);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register("BaitCountChanged", OnBaitCountChanged);
        CommunicateEvent.Register("BaitDataUpdated", OnBaitDataUpdated);
        CommunicateEvent.Register("FishBagDataUpdated", OnFishBagDataUpdated);

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
        if (hidePanelBtn != null)
        {
            hidePanelBtn.onClick.AddListener(OnHideBtnClick);
        }
        if (showPanelBtn != null)
        {
            showPanelBtn.onClick.AddListener(OnShowBtnClick);
        }
        if (centerCameraBtn != null)
        {
            centerCameraBtn.onClick.AddListener(OnCenterCameraBtnClick);
        }
        // ✅ 新增：地图按钮
        if (MapBtn != null)
        {
            MapBtn.onClick.AddListener(OnMapBtnClick);
        }

        if (mainTile != null)
        {
            Vector3 initialPos = mainTile.transform.position;
            mainTile.Init(initialPos);
        }

        SetMenuPanelState(isMenuOpen);
        UpdateBaitCountDisplay();
        //currentDisplayMode = DisplayMode.Text;
        UpdateDisplayMode();
        SetBtnPanelInitialState();

        CommunicateEvent.Modify("UI_RequestUpdateAllData");

        isInitialized = true;
    }

    // ✅ 新增：地图按钮点击
    private void OnMapBtnClick()
    {
        Debug.Log("[MainGameView] OnMapBtnClick - 点击地图按钮");
        CommunicateEvent.Modify("UI_OpenMap");
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
        TimeTextFadeOutText();
    }

    private void TimeTextFadeOutText()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            gameTimeTxt.color = new Color(gameTimeTxt.color.r, gameTimeTxt.color.g, gameTimeTxt.color.b, 1f);
        }
        fadeCoroutine = StartCoroutine(FadeOutText());
    }

    private IEnumerator FadeOutText()
    {
        Color c = gameTimeTxt.color;
        for (float t = 0; t < 1; t += Time.deltaTime / 1.5f)
        {
            c.a = 1 - t;
            gameTimeTxt.color = c;
            yield return null;
        }
        c.a = 0;
        gameTimeTxt.color = c;
        fadeCoroutine = null;
    }


    private void UpdateDisplayMode()
    {
        UpdateWeatherIcon(currentWeatherId);
        UpdateTimeIcon(currentTimeSlotId);
        Debug.Log($"[MainGameView] 更新显示");
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
            // ✅ 只有当图标真正发生变化时，才触发渐隐效果
            if (timeIcon.sprite != sprite)
            {
                // 先更新图标，再触发渐隐
                timeIcon.sprite = sprite;
                TimeTextFadeOutText();
                Debug.Log($"[MainGameView] 时段图标已更新: {timeSlotId}");
            }
            else
            {
                // 图标没变，不做任何操作
                Debug.Log($"[MainGameView] 时段图标未变化: {timeSlotId}");
            }
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
    private void OnHideBtnClick()
    {
        Debug.Log("[MainGameView] OnHideBtnClick - 点击隐藏右侧");
        btnPanel.SetActive(false);
        hidePanelBtn.gameObject.SetActive(false);
        showPanelBtn.gameObject.SetActive(true);
    }
    private void OnShowBtnClick()
    {
        Debug.Log("[MainGameView] OnShowBtnClick - 点击显示按钮");
        btnPanel.SetActive(true);
        hidePanelBtn.gameObject.SetActive(true);
        showPanelBtn.gameObject.SetActive(false);
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
        if (hidePanelBtn != null)
        {
            hidePanelBtn.gameObject.SetActive(open);
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

    void Update()
    {
        UpdateBaitCountdown();
    }

    private void UpdateBaitCountdown()
    {
        if (localContinuousModeTime > 0)
        {
            localContinuousModeTime -= Time.deltaTime;
            if (localContinuousModeTime < 0)
            {
                localContinuousModeTime = 0;
            }
        }

        if (baitCountdownObj != null)
        {
            baitCountdownObj.SetActive(localContinuousModeTime > 0);
        }

        if (localContinuousModeTime > 0 && baitCountdownTxt != null)
        {
            int minutes = Mathf.FloorToInt(localContinuousModeTime / 60f);
            int seconds = Mathf.FloorToInt(localContinuousModeTime % 60f);
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
    private void SetBtnPanelInitialState()
    {
        OnShowBtnClick();
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
            baitCountTxt.text = $"窝料:{currentBaitCount}";
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

    /// <summary>
    /// 更新鱼篓数量显示（由GameUIManager调用）
    /// </summary>
    public void UpdateFishCount(int currentCount, int maxCapacity)
    {
        if (fishCountTxt != null)
        {
            fishCountTxt.text = $"{currentCount}/{maxCapacity}";
        }
    }

    /// <summary>
    /// 更新窝料数量显示（由GameUIManager调用）
    /// </summary>
    public void UpdateBaitCount(int baitCount)
    {
        currentBaitCount = baitCount;
        if (baitCountTxt != null)
        {
            baitCountTxt.text = $"窝料:{baitCount}";
        }
    }

    public void UpdateContinuousModeTime(float remainingTime)
    {
        localContinuousModeTime = remainingTime;

        if (baitCountdownObj != null)
        {
            baitCountdownObj.SetActive(remainingTime > 0);
        }

        if (remainingTime > 0 && baitCountdownTxt != null)
        {
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            baitCountdownTxt.text = $"窝料: {minutes:00}:{seconds:00}";
        }
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
