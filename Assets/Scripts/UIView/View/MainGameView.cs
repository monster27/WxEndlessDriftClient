using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using SharedModels;

public class MainGameView : BaseView
{
    public TimeStatus timeStatus;

    public Button hidePanelBtn;
    public Button showPanelBtn;
    public Button bagBtn;
    public Button fishBagBtn;
    public Button mallBtn;
    public Button equipBtn;
    public Button weatherAndTimeBtn;
    public Button centerCameraBtn;
    public Button MapBtn;
    public Button homeBtn;
    public Button collectionBtn;

    public Button menuOpenBtn;
    public Button menuCloseBtn;

    public GameObject btnPanel;
    public Text weatherTxt;
    public Text gameTimeTxt;
    public Image weatherIcon;
    public Image timeIcon;
    public Text goldTxt;
    public Text baitCountdownTxt;
    public Text baitCountTxt;
    public Text fishCountTxt;
    public MainTile mainTile;
    public MainViewShowFishTip newItemTip;

    public GameObject menuPanel;
    public GameObject baitCountdownObj;

    private int currentBaitCount = 0;
    private bool isMenuOpen = false;

    private Coroutine fadeCoroutine;
    private bool isFading = false;

    private int currentWeatherId = 301;
    private int currentTimeSlotId = 401;

    private float localContinuousModeTime = 0f;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        CommunicateEvent.Register<Vector3>(CommunicateEvent.EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION, OnShowBaitCountdownAtPosition);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register("BaitCountChanged", OnBaitCountChanged);
        CommunicateEvent.Register("BaitDataUpdated", OnBaitDataUpdated);
        CommunicateEvent.Register("FishBagDataUpdated", OnFishBagDataUpdated);

        // ✅ 新增：等级奖励通知
        CommunicateEvent.Register<string>("OnLevelReward", OnLevelRewardReceived);

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
        if (MapBtn != null)
        {
            MapBtn.onClick.AddListener(OnMapBtnClick);
        }
        if (homeBtn != null)
        {
            homeBtn.onClick.AddListener(OnHomeBtnClick);
        }
        if (collectionBtn != null)
        {
            collectionBtn.onClick.AddListener(OnCollectionBtnClick);
        }

        if (mainTile != null)
        {
            Vector3 initialPos = mainTile.transform.position;
            mainTile.Init(initialPos);
        }

        if (newItemTip != null)
        {
            newItemTip.Init();
        }

        SetMenuPanelState(isMenuOpen);
        UpdateBaitCountDisplay();
        UpdateDisplayMode();
        SetBtnPanelInitialState();

        CommunicateEvent.Modify("UI_RequestUpdateAllData");

        isInitialized = true;
    }

    private void OnMapBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnMapBtnClick - 点击地图按钮");
        CommunicateEvent.Modify("UI_OpenMap");
    }

    private void OnHomeBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnHomeBtnClick - 点击切换室内外场景按钮");
        CommunicateEvent.Modify("UI_ToggleScene");
    }

    private void OnCollectionBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnCollectionBtnClick - 点击图鉴按钮");
        CommunicateEvent.Modify("UI_OpenCollection");
    }

    private void OnBagBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnBagBtnClick - 点击背包按钮");
        CommunicateEvent.Modify("UI_OpenBag");
    }

    private void OnFishBagBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnFishBagBtnClick - 点击鱼背包按钮");
        CommunicateEvent.Modify("UI_OpenFishBag");
    }

    private void OnMallBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnMallBtnClick - 点击商城按钮");
        CommunicateEvent.Modify("UI_OpenMall");
    }

    private void OnEquipBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnEquipBtnClick - 点击装备按钮");
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
        Z_Logger.Log($"[MainGameView] 更新显示");
    }

    private void UpdateWeatherIcon(int weatherId)
    {
        if (weatherIcon == null) return;
        string path = $"UI/Icon/WeatherIcon/{weatherId}";
        Sprite sprite = AssetManager.LoadFromResources<Sprite>(path);
        if (sprite != null)
        {
            weatherIcon.sprite = sprite;
        }
        else
        {
            Z_Logger.LogWarning($"[MainGameView] 未找到天气图标: {path}");
        }
    }

    private void UpdateTimeIcon(int timeSlotId)
    {
        if (timeIcon == null) return;

        string path = $"UI/Icon/TimeIcon/{timeSlotId}";
        Sprite sprite = AssetManager.LoadFromResources<Sprite>(path);

        if (sprite != null)
        {
            if (timeIcon.sprite != sprite)
            {
                timeIcon.sprite = sprite;
                TimeTextFadeOutText();
                Z_Logger.Log($"[MainGameView] 时段图标已更新: {timeSlotId}");
            }
            else
            {
                Z_Logger.Log($"[MainGameView] 时段图标未变化: {timeSlotId}");
            }
        }
        else
        {
            Z_Logger.LogWarning($"[MainGameView] 未找到时段图标: {path}");
        }
    }

    private void OnMenuOpenBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnMenuOpenBtnClick - 点击打开菜单");
        isMenuOpen = true;
        SetMenuPanelState(isMenuOpen);
    }

    private void OnMenuCloseBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnMenuCloseBtnClick - 点击关闭菜单");
        isMenuOpen = false;
        SetMenuPanelState(isMenuOpen);
    }

    private void OnHideBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnHideBtnClick - 点击隐藏右侧");
        btnPanel.SetActive(false);
        hidePanelBtn.gameObject.SetActive(false);
        showPanelBtn.gameObject.SetActive(true);
    }

    private void OnShowBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnShowBtnClick - 点击显示按钮");
        btnPanel.SetActive(true);
        hidePanelBtn.gameObject.SetActive(true);
        showPanelBtn.gameObject.SetActive(false);
    }

    private void OnCenterCameraBtnClick()
    {
        Z_Logger.Log("[MainGameView] OnCenterCameraBtnClick - 点击居中摄像头");
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.MoveToCenter();
        }
    }

    private void SetMenuPanelState(bool open)
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(open);
        }

        if (menuOpenBtn != null)
        {
            menuOpenBtn.gameObject.SetActive(!open);
        }

        if (menuCloseBtn != null)
        {
            menuCloseBtn.gameObject.SetActive(open);
        }
        if (hidePanelBtn != null)
        {
            hidePanelBtn.gameObject.SetActive(open);
        }
    }

    public void SetMenuState(bool open)
    {
        isMenuOpen = open;
        SetMenuPanelState(isMenuOpen);
    }

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

    public void UpdateBaitCountDisplay()
    {
        currentBaitCount = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, 0);

        if (baitCountTxt != null)
        {
            baitCountTxt.text = $"窝料:{currentBaitCount}";
        }
    }

    private void OnBaitCountChanged()
    {
        Z_Logger.Log("[MainGameView] OnBaitCountChanged - 窝料数量变化");
        UpdateBaitCountDisplay();
    }

    private void OnBaitDataUpdated()
    {
        Z_Logger.Log("[MainGameView] OnBaitDataUpdated - 鱼饵数据更新");
        UpdateBaitCountDisplay();
    }

    private void OnFishBagDataUpdated()
    {
        Z_Logger.Log("[MainGameView] OnFishBagDataUpdated - 鱼篓数据更新");
        UpdateFishCountDisplay();
    }

    /// <summary>
    /// 接收等级奖励通知（客户端显示）
    /// </summary>
    private void OnLevelRewardReceived(string rewardMessage)
    {
        Z_Logger.Log($"[MainGameView] 收到等级奖励: {rewardMessage}");
        GameUIManager.Instance?.ShowTip($"🎉 {rewardMessage}");
    }

    public void UpdateFishCount(int currentCount, int maxCapacity)
    {
        if (fishCountTxt != null)
        {
            fishCountTxt.text = $"{currentCount}/{maxCapacity}";
        }
    }

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
        Z_Logger.Log($"[MainGameView] UpdateTime called - status={status}, timeName={timeName}, gameTimeTxt={gameTimeTxt != null}");

        if (gameTimeTxt != null)
        {
            gameTimeTxt.text = timeName;
            Z_Logger.Log($"[MainGameView] 时间文本已更新: {timeName}");
        }
        else
        {
            Z_Logger.LogWarning("[MainGameView] gameTimeTxt 为 null，无法更新文本");
        }

        timeStatus = status;
        currentTimeSlotId = 401 + (int)status;
        Z_Logger.Log($"[MainGameView] currentTimeSlotId={currentTimeSlotId}");
        UpdateTimeIcon(currentTimeSlotId);
    }

    public void UpdateWeather(int weatherId, string weatherName)
    {
        Z_Logger.Log($"[MainGameView] UpdateWeather called - weatherId={weatherId}, weatherName={weatherName}, weatherTxt={weatherTxt != null}");

        if (weatherTxt != null)
        {
            weatherTxt.text = weatherName;
            Z_Logger.Log($"[MainGameView] 天气文本已更新: {weatherName}");
        }
        else
        {
            Z_Logger.LogWarning("[MainGameView] weatherTxt 为 null，无法更新文本");
        }

        currentWeatherId = weatherId;
        UpdateWeatherIcon(currentWeatherId);
    }

    /// <summary>
    /// 显示钓获结果
    /// </summary>
    /// <param name="itemName">物品名称</param>
    /// <param name="weight">重量</param>
    /// <param name="icon">图标</param>
    /// <param name="starRatingId">星级ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="isFish">是否为鱼类</param>
    /// <param name="isFirstCatch">是否为首次钓获该鱼</param>
    public void ShowCatchResult(string itemName, float weight, Sprite icon, int starRatingId = 0, int itemId = 0, bool isFish = true, bool isFirstCatch = false)
    {
        Z_Logger.Log($"ShowCatchResult - itemId:{itemId}, isFish:{isFish}, isFirstCatch:{isFirstCatch}");

        if (FishFlyInManager.Instance != null && itemId > 0)
        {
            FishFlyInManager.Instance.Fly(itemId, weight, isFish);
        }

        if (mainTile != null)
        {
            mainTile.EnqueueCatchResult(itemName, weight, icon);
        }

        // ✅ 只有鱼类且是首次钓获时，才显示 newItemTip（垃圾不触发）
        if (newItemTip != null && itemId > 0 && isFish && isFirstCatch)
        {
            Z_Logger.Log($"[MainGameView] 首次钓获新鱼: {itemName}, 显示 newItemTip");
            newItemTip.EnqueueNewItem(itemName, icon);
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

        // ✅ 取消注册等级奖励通知
        CommunicateEvent.Unregister<string>("OnLevelReward", OnLevelRewardReceived);
    }
}
