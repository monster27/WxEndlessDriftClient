// ============================================
// 文件: LoadingManager.cs
// 功能: 加载场景管理器 - 整合所有管理器初始化进度
// ============================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Logger = Utils.Logger;

public class LoadingManager : MonoBehaviour
{
    [Header("UI 组件")]
    public Slider progressSlider;
    public Text progressText;
    public Text statusText;          // 显示当前加载步骤
    public Text detailText;          // 显示详细信息（进度、日志等）

    [Header("设置")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private float minLoadTime = 1.5f;

    // 加载步骤定义
    private enum LoadStep
    {
        None,
        LoadDataManager,      // 加载本地配置数据
        NetServerInit,        // 网络服务器初始化
        PlayerDataSync,       // 玩家数据同步
        Complete
    }

    private LoadStep _currentStep = LoadStep.None;
    private float _stepProgress = 0f;
    private float _totalProgress = 0f;
    private float _startTime;
    private bool _isComplete = false;
    private bool _loadDataComplete = false;
    private bool _netServerComplete = false;
    private bool _playerDataComplete = false;

    // 步骤权重
    private const float WEIGHT_LOAD_DATA = 0.25f;      // 25%
    private const float WEIGHT_NET_SERVER = 0.50f;     // 50%
    private const float WEIGHT_PLAYER_DATA = 0.25f;    // 25%

    void Start()
    {
        _startTime = Time.time;

        // 设置初始状态
        UpdateStatus("初始化加载系统...", "");
        UpdateProgress(0f);

        // 启动加载流程
        StartCoroutine(LoadAllSystems());
    }

    private IEnumerator LoadAllSystems()
    {
        // ========== 第一步：加载 LoadDataManager ==========
        _currentStep = LoadStep.LoadDataManager;
        UpdateStatus("加载本地数据...", "正在加载配置文件和物品数据");
        UpdateProgress(0f);

        yield return StartCoroutine(LoadLoadDataManager());

        // ========== 第二步：初始化 NetServerManager ==========
        _currentStep = LoadStep.NetServerInit;
        UpdateStatus("连接服务器...", "正在初始化网络服务");
        UpdateProgress(WEIGHT_LOAD_DATA);

        yield return StartCoroutine(LoadNetServerManager());

        // ========== 第三步：同步玩家数据 ==========
        _currentStep = LoadStep.PlayerDataSync;
        UpdateStatus("同步玩家数据...", "正在获取背包和鱼篓数据");
        UpdateProgress(WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER);

        yield return StartCoroutine(LoadPlayerData());

        // ========== 完成 ==========
        _currentStep = LoadStep.Complete;
        _isComplete = true;
        UpdateStatus("加载完成！", "准备进入游戏...");
        UpdateProgress(1f);

        // 等待最短加载时间
        float elapsed = Time.time - _startTime;
        float delay = Mathf.Max(0, minLoadTime - elapsed);
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        // 跳转场景
        Logger.Log("[LoadingManager] 所有系统加载完成，跳转到游戏场景");
        SceneManager.LoadScene(gameSceneName);
    }

    // ========== 加载 LoadDataManager ==========
    private IEnumerator LoadLoadDataManager()
    {
        // 等待 LoadDataManager 实例
        if (LoadDataManager.Instance == null)
        {
            Logger.LogWarning("[LoadingManager] LoadDataManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (LoadDataManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
        }

        var loadData = LoadDataManager.Instance;
        if (loadData == null)
        {
            Logger.LogError("[LoadingManager] LoadDataManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("LoadDataManager");
            loadData = go.AddComponent<LoadDataManager>();
        }

        // 订阅加载完成事件
        loadData.onDataLoaded += OnLoadDataComplete;

        // 如果已经加载完成，直接处理
        if (loadData.isDataLoaded)
        {
            Logger.Log("[LoadingManager] LoadDataManager 数据已加载");
            OnLoadDataComplete();
            yield break;
        }

        // ✅ 关键修复：调用 Init() 开始加载数据
        Logger.Log("[LoadingManager] 开始加载 LoadDataManager 数据...");
        loadData.Init();

        // 等待加载完成（带超时）
        float elapsedTime = 0f;
        float timeoutDuration = 10f;
        while (!_loadDataComplete && elapsedTime < timeoutDuration)
        {
            // 模拟进度
            float progress = Mathf.Min(elapsedTime / 3f, 0.95f);
            float stepProgress = progress * WEIGHT_LOAD_DATA;
            UpdateProgress(stepProgress);
            UpdateDetail($"加载本地数据... {Mathf.RoundToInt(progress * 100)}%");

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!_loadDataComplete)
        {
            Logger.LogWarning("[LoadingManager] LoadDataManager 加载超时，强制继续");
            _loadDataComplete = true;
        }

        // 完成
        float finalProgress = WEIGHT_LOAD_DATA;
        UpdateProgress(finalProgress);
        UpdateDetail("本地数据加载完成");
        yield return new WaitForSeconds(0.2f);
    }

    private void OnLoadDataComplete()
    {
        _loadDataComplete = true;
        Logger.Log("[LoadingManager] LoadDataManager 加载完成");

        // 打印数据统计
        var data = LoadDataManager.Instance;
        if (data != null)
        {
            UpdateDetail($"加载完成: {data.items.Count}个物品, {data.fishes.Count}条鱼, {data.baits.Count}个鱼饵");
        }
    }

    // ========== 加载 NetServerManager ==========
    private IEnumerator LoadNetServerManager()
    {
        // 等待 NetServerManager 实例
        if (NetServerManager.Instance == null)
        {
            Logger.LogWarning("[LoadingManager] NetServerManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (NetServerManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
        }

        var netManager = NetServerManager.Instance;
        if (netManager == null)
        {
            Logger.LogError("[LoadingManager] NetServerManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("NetServerManager");
            netManager = go.AddComponent<NetServerManager>();
        }

        // 订阅事件
        netManager.OnProgressUpdated += OnNetProgressUpdated;
        netManager.OnInitializationComplete += OnNetServerComplete;
        netManager.OnInitializationFailed += OnNetServerFailed;

        // 如果已经初始化完成
        if (netManager.IsInitialized)
        {
            OnNetServerComplete();
            yield break;
        }

        // 开始初始化
        Logger.Log("[LoadingManager] 开始 NetServerManager 初始化...");
        netManager.StartInitialization();

        // 等待完成（带超时）
        float elapsedTime = 0f;
        float timeoutDuration = 30f;
        while (!_netServerComplete && elapsedTime < timeoutDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!_netServerComplete)
        {
            Logger.LogError("[LoadingManager] NetServerManager 初始化超时");
            _netServerComplete = true;
        }

        // 取消订阅
        netManager.OnProgressUpdated -= OnNetProgressUpdated;
        netManager.OnInitializationComplete -= OnNetServerComplete;
        netManager.OnInitializationFailed -= OnNetServerFailed;
    }

    private void OnNetProgressUpdated(float progress, string stepName)
    {
        // 网络初始化占总进度的 50%
        float netProgress = WEIGHT_LOAD_DATA + (progress * WEIGHT_NET_SERVER);
        UpdateProgress(netProgress);
        UpdateDetail($"{stepName} ({Mathf.RoundToInt(progress * 100)}%)");
    }

    private void OnNetServerComplete()
    {
        _netServerComplete = true;
        Logger.Log("[LoadingManager] NetServerManager 初始化完成");

        float progress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER;
        UpdateProgress(progress);
        UpdateDetail("网络连接成功，数据已加载");
    }

    private void OnNetServerFailed(string errorMessage)
    {
        _netServerComplete = true;
        Logger.LogError($"[LoadingManager] NetServerManager 初始化失败: {errorMessage}");
        UpdateDetail($"网络初始化失败: {errorMessage}");
    }

    // ========== 加载 PlayerData ==========
    private IEnumerator LoadPlayerData()
    {
        // 等待 PlayerDataManager 实例
        if (PlayerDataManager.Instance == null)
        {
            Logger.LogWarning("[LoadingManager] PlayerDataManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (PlayerDataManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
        }

        var playerData = PlayerDataManager.Instance;
        if (playerData == null)
        {
            Logger.LogError("[LoadingManager] PlayerDataManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("PlayerDataManager");
            playerData = go.AddComponent<PlayerDataManager>();
        }

        // 确保 PlayerDataManager 已初始化
        playerData.Init();

        // 强制同步数据
        Logger.Log("[LoadingManager] 开始同步玩家数据...");
        playerData.SyncInventoryFromServer();
        playerData.SyncGoldFromServer();

        // 等待数据同步完成
        float startTime = Time.time;
        float timeoutDuration = 10f;
        bool hasLoggedEmpty = false;

        while (!_playerDataComplete && Time.time - startTime < timeoutDuration)
        {
            // 检查是否有数据
            var inventory = playerData.GetInventory();
            var fishInventory = playerData.GetFishInventory();
            int totalItems = (inventory?.Count ?? 0) + (fishInventory?.Count ?? 0);

            // 更新进度（逐步增加）
            float elapsedRatio = Mathf.Min((Time.time - startTime) / 3f, 0.95f);
            float stepProgress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER + (elapsedRatio * WEIGHT_PLAYER_DATA);
            UpdateProgress(stepProgress);

            // 更新详细信息
            if (totalItems > 0)
            {
                UpdateDetail($"同步完成: 背包{inventory?.Count ?? 0}种, 鱼篓{fishInventory?.Count ?? 0}种");
                _playerDataComplete = true;
            }
            else if (elapsedRatio > 0.5f && !hasLoggedEmpty)
            {
                hasLoggedEmpty = true;
                UpdateDetail("等待数据同步... (背包为空)");
            }

            yield return new WaitForSeconds(0.2f);
        }

        // 如果超时但已经有数据，也算完成
        if (!_playerDataComplete)
        {
            var inventory = playerData.GetInventory();
            var fishInventory = playerData.GetFishInventory();
            if ((inventory?.Count ?? 0) > 0 || (fishInventory?.Count ?? 0) > 0)
            {
                _playerDataComplete = true;
                UpdateDetail($"数据同步完成: 背包{inventory?.Count ?? 0}种, 鱼篓{fishInventory?.Count ?? 0}种");
            }
            else
            {
                Logger.LogWarning("[LoadingManager] 玩家数据同步超时，但继续加载");
                _playerDataComplete = true;
                UpdateDetail("数据同步完成 (无物品)");
            }
        }

        // 最终进度
        float finalProgress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER + WEIGHT_PLAYER_DATA;
        UpdateProgress(finalProgress);
        yield return new WaitForSeconds(0.2f);
    }

    // ========== UI 更新方法 ==========

    private void UpdateStatus(string status, string detail = "")
    {
        if (statusText != null)
        {
            statusText.text = status;
        }

        if (detailText != null && !string.IsNullOrEmpty(detail))
        {
            detailText.text = detail;
        }

        Logger.Log($"[LoadingManager] {status} - {detail}");
    }

    private void UpdateDetail(string detail)
    {
        if (detailText != null)
        {
            detailText.text = detail;
        }
    }

    private void UpdateProgress(float progress)
    {
        _totalProgress = Mathf.Clamp01(progress);

        if (progressSlider != null)
        {
            progressSlider.value = _totalProgress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(_totalProgress * 100)}%";
        }
    }

    // ========== 辅助方法 ==========

    private void OnDestroy()
    {
        // 取消事件订阅
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.OnProgressUpdated -= OnNetProgressUpdated;
            netManager.OnInitializationComplete -= OnNetServerComplete;
            netManager.OnInitializationFailed -= OnNetServerFailed;
        }

        var loadData = LoadDataManager.Instance;
        if (loadData != null)
        {
            loadData.onDataLoaded -= OnLoadDataComplete;
        }
    }
}