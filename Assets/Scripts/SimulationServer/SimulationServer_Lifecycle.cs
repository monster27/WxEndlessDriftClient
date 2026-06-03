// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SimulationServer 单例和生命周期部分
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region 单例

    private static SimulationServer instance;
    private bool isOriginalInstance = false;

    public static SimulationServer Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SimulationServer>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("SimulationServer");
                    instance = obj.AddComponent<SimulationServer>();
                }
            }
            return instance;
        }
    }

    #endregion

    #region 初始化与生命周期

    private void Awake()
    {
        // 检查是否已经有实例存在
        if (instance != null && instance != this)
        {
            // 如果已经存在实例，但不是这个对象，则销毁这个对象
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 发现重复的实例，销毁多余对象</color>");
            Destroy(gameObject);
            return;
        }

        // 这是第一个实例（或者是场景中唯一的实例）
        if (instance == null)
        {
            instance = this;
            isOriginalInstance = true;
            DontDestroyOnLoad(gameObject);
            Debug.LogFormat("<color=orange>[SimulationServer] 创建新实例，设置为例程</color>");
        }
        else
        {
            // instance 已经存在，但可能是从场景加载的
            isOriginalInstance = false;
            Debug.LogFormat("<color=orange>[SimulationServer] 使用已存在的实例</color>");
        }
    }

    private void Start()
    {
        // 只有原始实例才执行初始化
        if (isOriginalInstance || instance == this)
        {
            Initialize();
        }
    }

    private void Update()
    {
        // 更新自动钓鱼管理器
        autoFishingManager?.Update(Time.deltaTime);

        // 更新连续钓鱼模式计时器
        UpdateContinuousMode(Time.deltaTime);

        // 处理闲置动画延迟
        if (isWaitingForIdleAnimation)
        {
            idleAnimationTimer += Time.deltaTime;
            if (idleAnimationTimer >= 2f)
            {
                isWaitingForIdleAnimation = false;
                ServerManager.Instance?.NotifyPlayIdleAnimation();
            }
        }
    }

    private void Initialize()
    {
        Debug.LogFormat("<color=orange>[SimulationServer] 开始初始化...</color>");

        // 首先注册View层请求事件，确保事件处理器在任何其他初始化之前就绪
        RegisterViewRequestEvents();

        // 创建并初始化时间槽服务器管理器
        timeSlotManager = new TimeSlotServerManager();
        timeSlotManager.Initialize(EnvManager.Instance);

        // 创建并初始化天气服务器管理器
        weatherManager = new WeatherServerManager();
        weatherManager.Initialize();

        // 创建并初始化场景鱼库服务器管理器
        sceneFishPoolManager = new SceneFishPoolServerManager();
        sceneFishPoolManager.Initialize();

        // 创建玩家背包服务器管理器
        inventoryManager = new PlayerInventoryServerManager();
        inventoryManager.Initialize();

        // 创建玩家能力管理器
        playerAbilityManager = new PlayerFishingAbilityManager(1);

        // 创建钓鱼算法服务器管理器（需要在能力管理器和鱼库管理器之后创建）
        fishingAlgorithmManager = new FishingAlgorithmServerManager();
        fishingAlgorithmManager.Initialize(playerAbilityManager, sceneFishPoolManager);

        // 创建自动钓鱼服务器管理器
        autoFishingManager = new AutoFishingServerManager(this);

        // 创建商城服务器管理器
        mallManager = new MallServerManager();
        mallManager.Initialize();

        Debug.LogFormat("<color=orange>[SimulationServer] 所有子管理器初始化完成</color>");

        // 发送初始环境状态
        SendInitialEnvState();

        // 开始运行
        isRunning = true;

        // 启动自动钓鱼
        StartAutoFishing();
    }

    private void RegisterViewRequestEvents()
    {
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, OnPurchaseMallItemRequest);
        CommunicateEvent.Register<(List<int>, int)>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItemsRequest);
        CommunicateEvent.Register(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE, () => { 
            if (CheckServerConnection())
            {
                ConsumeBaitAndEnterContinuousMode();
            }
        });
        CommunicateEvent.Register(CommunicateEvent.EVENT_ADD_CONTINUOUS_MODE_TIME, () => {
            if (CheckServerConnection())
            {
                AddContinuousModeTime();
            }
        });
        RegisterDataRequestHandlers();
        Debug.LogFormat("<color=orange>[SimulationServer] View请求事件监听器注册完成</color>");
    }

    private void RegisterDataRequestHandlers()
    {
        CommunicateEvent.Register<Dictionary<string, object>>("HeartbeatRequest", data => ProcessHeartbeat(data));
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, itemId => GetComponentLevel(itemId));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId => IsSkillObtained(skillId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, characterId => IsCharacterObtained(characterId));
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, _ => GetInventory());
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId => IsItemEquipped(itemId));
        CommunicateEvent.RegisterRequest<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, _ => GetMallItems());
        CommunicateEvent.RegisterRequest<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId => GetMallItem(itemId));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_GOLD, _ => Gold);
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, _ => FishBagCapacity);
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_FISH_INVENTORY, _ => GetFishInventory());
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => IsInContinuousMode);
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => ContinuousModeRemainingTime);
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());

        Debug.LogFormat("<color=orange>[SimulationServer] 数据查询请求处理器注册完成</color>");
    }

    /// <summary>
    /// 检查是否可以执行服务器操作
    /// </summary>
    /// <returns>如果可以执行返回true，否则返回false并显示错误提示</returns>
    private bool CheckServerConnection()
    {
        if (ManagerManager.Instance != null && !ManagerManager.Instance.isOfflineMode)
        {
            return false;
        }
        return true;
    }

    private void OnPurchaseMallItemRequest((int, int) request)
    {
        if (!CheckServerConnection())
            return;

        var (itemId, quantity) = request;
        Debug.LogFormat("<color=orange>[SimulationServer] OnPurchaseMallItemRequest - itemId={0}, quantity={1}</color>", itemId, quantity);
        PurchaseMallItem(itemId, quantity);
    }

    private void OnSellFishItemsRequest((List<int>, int) request)
    {
        if (!CheckServerConnection())
            return;

        var (fishIds, quantity) = request;
        Debug.LogFormat("<color=orange>[SimulationServer] OnSellFishItemsRequest - fishIds={0}, quantity={1}</color>", string.Join(",", fishIds), quantity);
        SellFishItems(fishIds, quantity);
    }

    private void SendInitialEnvState()
    {
        // 发送初始时间段
        int timeSlotId = (int)CurrentTimeStatus + 1;
        var timeSlotData = LoadDataManager.Instance?.GetTimeSlotById(timeSlotId);
        string timeSlotName = timeSlotData?.name ?? CurrentTimeStatus.ToString();
        
        Dictionary<string, object> timeEventData = new Dictionary<string, object>
        {
            { "timeStatus", (int)CurrentTimeStatus },
            { "timeSlotName", timeSlotName },
            { "weatherId", CurrentWeatherId }
        };
        CommunicateEvent.Modify(CommunicateEvent.EVENT_TIME_SLOT_CHANGED, timeEventData);
        Debug.LogFormat("<color=yellow>[SimulationServer] 发送初始时间段状态: {0}, 名称: {1}</color>", CurrentTimeStatus, timeSlotName);
        
        // 发送初始天气
        string weatherName = LoadDataManager.Instance?.GetWeatherName(CurrentWeatherId) ?? "未知天气";
        Dictionary<string, object> weatherEventData = new Dictionary<string, object>
        {
            { "weatherId", CurrentWeatherId },
            { "weatherName", weatherName }
        };
        CommunicateEvent.Modify(CommunicateEvent.EVENT_WEATHER_CHANGED, weatherEventData);
        Debug.LogFormat("<color=yellow>[SimulationServer] 发送初始天气状态: ID={0}, 名称: {1}</color>", CurrentWeatherId, weatherName);
    }

    #endregion

    #region 心跳相关

    private bool isRunning = false;

    public bool IsRunning()
    {
        return isRunning;
    }

    public void ProcessHeartbeat(Dictionary<string, object> heartbeatData)
    {
        if (heartbeatData.TryGetValue("clientTime", out object clientTimeObj))
        {
            long clientTime = System.Convert.ToInt64(clientTimeObj);
            long serverTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var responseData = new Dictionary<string, object>
            {
                { "serverTime", serverTime },
                { "clientTime", clientTime }
            };
            
            Debug.LogFormat("<color=cyan>[SimulationServer] 处理心跳: clientTime={0}, serverTime={1}</color>", clientTime, serverTime);
            
            CommunicateEvent.Modify("HeartbeatResponse", responseData);
        }
    }

    #endregion
}
*/