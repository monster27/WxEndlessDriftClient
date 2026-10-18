// ============================================
// 文件: LoadingManager.cs
// 功能: 加载场景管理器 - 整合所有管理器初始化进度
//       场景跳转全部使用 Addressables
// ============================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public class LoadingManager : MonoBehaviour
{
    [Header("UI 组件")]
    public Slider progressSlider;
    public Text progressText;
    public Text statusText;
    public Text detailText;
    public Button skipButton;
    
    // ✅ 场景 Key 与 Addressables 里的 Address 完全一致（完整路径）
    private string gameSceneName = "Assets/Addressables/Scenes/GameScene.unity";
    private float minLoadTime = 1.5f;

    // 加载步骤定义
    private enum LoadStep
    {
        None,
        LoadDataManager,
        NetServerInit,
        PlayerDataSync,
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

    private const float WEIGHT_LOAD_DATA = 0.25f;
    private const float WEIGHT_NET_SERVER = 0.50f;
    private const float WEIGHT_PLAYER_DATA = 0.25f;

    void Start()
    {
        _startTime = Time.time;

        UpdateStatus("初始化加载系统...", "");
        UpdateProgress(0f);

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(ForceJumpToNextScene);
        }
        else
        {
            Z_Logger.LogWarning("[LoadingManager] skipButton 未绑定，强制跳转功能不可用");
        }

        StartCoroutine(LoadAllSystems());
    }

    private IEnumerator LoadAllSystems()
    {
        _currentStep = LoadStep.LoadDataManager;
        UpdateStatus("加载本地数据...", "正在加载配置文件和物品数据");
        UpdateProgress(0f);
        yield return StartCoroutine(LoadLoadDataManager());

        _currentStep = LoadStep.NetServerInit;
        UpdateStatus("连接服务器...", "正在初始化网络服务");
        UpdateProgress(WEIGHT_LOAD_DATA);
        yield return StartCoroutine(LoadNetServerManager());

        _currentStep = LoadStep.PlayerDataSync;
        UpdateStatus("同步玩家数据...", "正在获取背包和鱼篓数据");
        UpdateProgress(WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER);
        yield return StartCoroutine(LoadPlayerData());

        _currentStep = LoadStep.Complete;
        _isComplete = true;
        UpdateStatus("加载完成！", "准备进入游戏...");
        UpdateProgress(1f);

        float elapsed = Time.time - _startTime;
        float delay = Mathf.Max(0, minLoadTime - elapsed);
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        Z_Logger.Log("[LoadingManager] 所有系统加载完成，跳转到游戏场景");
        yield return StartCoroutine(LoadSceneWithFallback(gameSceneName));
    }

    public void ForceJumpToNextScene()
    {
        Z_Logger.Log("[LoadingManager] 用户点击强制跳转按钮");
        StopAllCoroutines();
        StartCoroutine(LoadSceneWithFallback(gameSceneName));
    }

    // ========== 场景诊断 ==========

    private void LogBuildSettingsScenes()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        Z_Logger.Log($"[LoadingManager] === Build Settings 场景列表 (共 {sceneCount} 个) ===");

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(scenePath))
            {
                Z_Logger.LogWarning($"[LoadingManager]   [{i}] (路径为空或无效)");
                continue;
            }
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            Z_Logger.Log($"[LoadingManager]   [{i}] {scenePath} -> 场景名: '{sceneName}'");
        }
        Z_Logger.Log("[LoadingManager] ============================================");
    }

    private void DiagnoseSceneLoading(string sceneName)
    {
        Z_Logger.Log($"[LoadingManager] === 场景加载诊断 (Addressables): '{sceneName}' ===");

        // 1. 检查 Build Settings 中的场景
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                Z_Logger.LogWarning($"[LoadingManager]   ⚠️ 场景 '{sceneName}' 同时存在于 Build Settings (索引 {i})，建议从 Build Settings 移除");
            }
        }

        // 2. 检查当前已加载的场景
        Z_Logger.Log($"[LoadingManager]   当前已加载场景数: {SceneManager.sceneCount}");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Z_Logger.Log($"[LoadingManager]     已加载[{i}]: '{scene.name}' (路径: {scene.path}, 有效: {scene.IsValid()})");
        }

#if UNITY_EDITOR
        // 3. 检查 Addressable 配置
        try
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            bool found = false;
            if (settings != null)
            {
                foreach (var group in settings.groups)
                {
                    if (group == null) continue;
                    foreach (var entry in group.entries)
                    {
                        if (entry == null) continue;
                        if (entry.AssetPath == sceneName ||
                            entry.address == sceneName ||
                            Path.GetFileNameWithoutExtension(entry.AssetPath) == Path.GetFileNameWithoutExtension(sceneName))
                        {
                            found = true;
                            Z_Logger.Log($"[LoadingManager]   ✅ 场景 '{sceneName}' 在 Addressable Group '{group.Name}' 中");
                            Z_Logger.Log($"[LoadingManager]      AssetPath: {entry.AssetPath}");
                            Z_Logger.Log($"[LoadingManager]      Address:   {entry.address}");
                        }
                    }
                }
            }

            if (!found)
            {
                Z_Logger.LogError($"[LoadingManager]   ❌ 场景 '{sceneName}' 未在任何 Addressable Group 中找到，请先标记为 Addressable");
            }
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"[LoadingManager]   Addressable 检查失败: {ex.Message}");
        }
#endif

        Z_Logger.Log("[LoadingManager] ============================================");
    }

    // ========== 场景加载（Addressables） ==========

    private IEnumerator LoadSceneWithFallback(string sceneName)
    {
        Z_Logger.Log($"[LoadingManager] ============================================");
        Z_Logger.Log($"[LoadingManager] 使用 Addressables 加载场景: '{sceneName}'");
        UpdateStatus("正在跳转场景...", $"目标: {sceneName}");

        LogBuildSettingsScenes();
        DiagnoseSceneLoading(sceneName);

        // 若目标场景已加载，直接激活
        Scene existingScene = SceneManager.GetSceneByPath(sceneName);
        if (existingScene.IsValid() && existingScene.isLoaded)
        {
            Z_Logger.Log($"[LoadingManager]   场景 '{sceneName}' 已加载，直接激活");
            SceneManager.SetActiveScene(existingScene);
            yield break;
        }

        // ★★★ 新增：加载场景前触发 tier1 预下载 ★★★
        // PreloadManager 会自动创建（SingletonMono），DontDestroyOnLoad 跨场景保留
        // 预下载跑在 PreloadManager 上，不受 LoadingScene 卸载影响
        if (PreloadManager.Instance != null)
        {
            PreloadManager.Instance.StartTier1();
            Z_Logger.Log("[LoadingManager] 已触发 tier1 预下载（与场景加载并行）");
        }
        // ★★★★★★★★★★★★★★★★★★★★★★★

        // Addressables 异步加载场景（Single 模式：替换当前场景）
        var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        float startTime = Time.time;
        float timeout = 30f;

        while (!handle.IsDone)
        {
            UpdateProgress(handle.PercentComplete);
            //Z_Logger.Log($"[LoadingManager]   加载进度: {handle.PercentComplete * 100:F1}%");
            yield return null;

            if (Time.time - startTime > timeout)
            {
                Z_Logger.LogError($"[LoadingManager] ❌ 场景 '{sceneName}' 加载超时 ({timeout}s)");
                break;
            }
        }

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            UpdateProgress(1f);
            Z_Logger.Log($"[LoadingManager] ✅ 场景 '{sceneName}' 加载成功");
            yield break;
        }

        // ==== 失败降级：尝试 SceneManager 按名称加载 ====
        string err = handle.OperationException != null
            ? handle.OperationException.Message
            : "未知错误";
        Z_Logger.LogError($"[LoadingManager] ❌ Addressables 加载场景失败: {err}");
        UpdateStatus("Addressables 加载失败", "尝试降级到 SceneManager");

        bool fallbackOk = false;
        string shortName = Path.GetFileNameWithoutExtension(sceneName);

        try
        {
            SceneManager.LoadScene(shortName);
            fallbackOk = true;
            Z_Logger.Log($"[LoadingManager]   降级到 SceneManager.LoadScene(\"{shortName}\") 成功");
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"[LoadingManager] 按名称降级失败: {ex.Message}");
        }

        if (!fallbackOk)
        {
            string fullError = $"场景跳转失败！请检查:\n" +
                               $"1. 场景 '{sceneName}' 是否已标记为 Addressable\n" +
                               $"2. Address 是否为 '{sceneName}'\n" +
                               $"3. 场景是否已从 Build Settings 移除\n" +
                               $"4. Catalog 是否已更新并上传到服务器";
            Z_Logger.LogError($"[LoadingManager] ❌ {fullError}");
            UpdateStatus("跳转失败", fullError);
        }

        Z_Logger.Log($"[LoadingManager] === 最终场景状态 ===");
        Z_Logger.Log($"[LoadingManager]   场景数: {SceneManager.sceneCount}");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Z_Logger.Log($"[LoadingManager]     [{i}] {scene.name} (路径: {scene.path})");
        }
        Z_Logger.Log($"[LoadingManager]   活跃场景: {SceneManager.GetActiveScene().name}");
        Z_Logger.Log("[LoadingManager] ====================");
    }


    // ========== 加载 LoadDataManager ==========
    private IEnumerator LoadLoadDataManager()
    {
        Z_Logger.Log("[LoadingManager] 开始加载 LoadDataManager...");

        if (LoadDataManager.Instance == null)
        {
            Z_Logger.LogWarning("[LoadingManager] LoadDataManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (LoadDataManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
            Z_Logger.Log($"[LoadingManager] 等待 LoadDataManager 耗时: {waitTime:F1}s");
        }

        var loadData = LoadDataManager.Instance;
        if (loadData == null)
        {
            Z_Logger.LogError("[LoadingManager] LoadDataManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("LoadDataManager");
            loadData = go.AddComponent<LoadDataManager>();
            Z_Logger.Log("[LoadingManager] 已创建 LoadDataManager GameObject");
        }

        loadData.onDataLoaded += OnLoadDataComplete;

        if (loadData.isDataLoaded)
        {
            Z_Logger.Log("[LoadingManager] LoadDataManager 数据已加载");
            OnLoadDataComplete();
            yield break;
        }

        Z_Logger.Log("[LoadingManager] 开始加载 LoadDataManager 数据...");
        loadData.Init();

        float elapsedTime = 0f;
        float timeoutDuration = 10f;
        while (!_loadDataComplete && elapsedTime < timeoutDuration)
        {
            float progress = Mathf.Min(elapsedTime / 3f, 0.95f);
            float stepProgress = progress * WEIGHT_LOAD_DATA;
            UpdateProgress(stepProgress);
            UpdateDetail($"加载本地数据... {Mathf.RoundToInt(progress * 100)}%");
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!_loadDataComplete)
        {
            Z_Logger.LogWarning($"[LoadingManager] LoadDataManager 加载超时 ({timeoutDuration}s)，强制继续");
            _loadDataComplete = true;
        }

        float finalProgress = WEIGHT_LOAD_DATA;
        UpdateProgress(finalProgress);
        UpdateDetail("本地数据加载完成");
        Z_Logger.Log("[LoadingManager] LoadDataManager 加载阶段完成");
        yield return new WaitForSeconds(0.2f);
    }

    private void OnLoadDataComplete()
    {
        _loadDataComplete = true;
        Z_Logger.Log("[LoadingManager] LoadDataManager 加载完成");

        var data = LoadDataManager.Instance;
        if (data != null)
        {
            string detail = $"加载完成: {data.items?.Count ?? 0}个物品, {data.fishes?.Count ?? 0}条鱼, {data.baits?.Count ?? 0}个鱼饵";
            UpdateDetail(detail);
            Z_Logger.Log($"[LoadingManager] {detail}");
        }
    }

    // ========== 加载 NetServerManager ==========
    private IEnumerator LoadNetServerManager()
    {
        Z_Logger.Log("[LoadingManager] 开始加载 NetServerManager...");

        if (NetServerManager.Instance == null)
        {
            Z_Logger.LogWarning("[LoadingManager] NetServerManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (NetServerManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
            Z_Logger.Log($"[LoadingManager] 等待 NetServerManager 耗时: {waitTime:F1}s");
        }

        var netManager = NetServerManager.Instance;
        if (netManager == null)
        {
            Z_Logger.LogError("[LoadingManager] NetServerManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("NetServerManager");
            netManager = go.AddComponent<NetServerManager>();
            Z_Logger.Log("[LoadingManager] 已创建 NetServerManager GameObject");
        }

        netManager.OnProgressUpdated += OnNetProgressUpdated;
        netManager.OnInitializationComplete += OnNetServerComplete;
        netManager.OnInitializationFailed += OnNetServerFailed;

        if (netManager.IsInitialized)
        {
            Z_Logger.Log("[LoadingManager] NetServerManager 已初始化");
            OnNetServerComplete();
            yield break;
        }

        Z_Logger.Log("[LoadingManager] 开始 NetServerManager 初始化...");
        netManager.StartInitialization();

        float elapsedTime = 0f;
        float timeoutDuration = 30f;
        while (!_netServerComplete && elapsedTime < timeoutDuration)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime % 5f < Time.deltaTime)
            {
                Z_Logger.Log($"[LoadingManager] 等待 NetServerManager 初始化... {elapsedTime:F1}s/{timeoutDuration}s");
            }
            yield return null;
        }

        if (!_netServerComplete)
        {
            Z_Logger.LogError($"[LoadingManager] NetServerManager 初始化超时 ({timeoutDuration}s)");
            _netServerComplete = true;
        }

        netManager.OnProgressUpdated -= OnNetProgressUpdated;
        netManager.OnInitializationComplete -= OnNetServerComplete;
        netManager.OnInitializationFailed -= OnNetServerFailed;

        Z_Logger.Log("[LoadingManager] NetServerManager 加载阶段完成");
    }

    private void OnNetProgressUpdated(float progress, string stepName)
    {
        float netProgress = WEIGHT_LOAD_DATA + (progress * WEIGHT_NET_SERVER);
        UpdateProgress(netProgress);
        UpdateDetail($"{stepName} ({Mathf.RoundToInt(progress * 100)}%)");
    }

    private void OnNetServerComplete()
    {
        _netServerComplete = true;
        Z_Logger.Log("[LoadingManager] ✅ NetServerManager 初始化完成");

        NetServerManager.Instance?.InitializeEquipmentData();
        NetServerManager.Instance?.FetchCurrentWeather();

        float progress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER;
        UpdateProgress(progress);
        UpdateDetail("网络连接成功，数据已加载");
    }

    private void OnNetServerFailed(string errorMessage)
    {
        _netServerComplete = true;
        Z_Logger.LogError($"[LoadingManager] ❌ NetServerManager 初始化失败: {errorMessage}");
        UpdateDetail($"网络初始化失败: {errorMessage}");
    }

    // ========== 加载 PlayerData ==========
    private IEnumerator LoadPlayerData()
    {
        Z_Logger.Log("[LoadingManager] 开始加载 PlayerData...");

        if (PlayerDataManager.Instance == null)
        {
            Z_Logger.LogWarning("[LoadingManager] PlayerDataManager 实例不存在，等待创建...");
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (PlayerDataManager.Instance == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
            Z_Logger.Log($"[LoadingManager] 等待 PlayerDataManager 耗时: {waitTime:F1}s");
        }

        var playerData = PlayerDataManager.Instance;
        if (playerData == null)
        {
            Z_Logger.LogError("[LoadingManager] PlayerDataManager 实例不存在，尝试创建...");
            GameObject go = new GameObject("PlayerDataManager");
            playerData = go.AddComponent<PlayerDataManager>();
            Z_Logger.Log("[LoadingManager] 已创建 PlayerDataManager GameObject");
        }

        playerData.Init();
        Z_Logger.Log("[LoadingManager] PlayerDataManager 已初始化");

        Z_Logger.Log("[LoadingManager] 开始同步玩家数据...");
        playerData.SyncInventoryFromServer();
        playerData.SyncGoldFromServer();

        float startTime = Time.time;
        float timeoutDuration = 10f;
        bool hasLoggedEmpty = false;
        int lastItemCount = -1;

        while (!_playerDataComplete && Time.time - startTime < timeoutDuration)
        {
            var inventory = playerData.GetInventory();
            var fishInventory = playerData.GetFishInventory();
            int totalItems = (inventory?.Count ?? 0) + (fishInventory?.Count ?? 0);

            if (totalItems != lastItemCount)
            {
                Z_Logger.Log($"[LoadingManager] 数据同步中: 背包{inventory?.Count ?? 0}种, 鱼篓{fishInventory?.Count ?? 0}种");
                lastItemCount = totalItems;
            }

            float elapsedRatio = Mathf.Min((Time.time - startTime) / 3f, 0.95f);
            float stepProgress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER + (elapsedRatio * WEIGHT_PLAYER_DATA);
            UpdateProgress(stepProgress);

            if (totalItems > 0)
            {
                string detail = $"同步完成: 背包{inventory?.Count ?? 0}种, 鱼篓{fishInventory?.Count ?? 0}种";
                UpdateDetail(detail);
                _playerDataComplete = true;
                Z_Logger.Log($"[LoadingManager] ✅ {detail}");
            }
            else if (elapsedRatio > 0.5f && !hasLoggedEmpty)
            {
                hasLoggedEmpty = true;
                UpdateDetail("等待数据同步... (背包为空)");
                Z_Logger.LogWarning("[LoadingManager] 数据同步中，背包为空，可能数据尚未从服务器返回");
            }

            yield return new WaitForSeconds(0.2f);
        }

        if (!_playerDataComplete)
        {
            var inventory = playerData.GetInventory();
            var fishInventory = playerData.GetFishInventory();
            if ((inventory?.Count ?? 0) > 0 || (fishInventory?.Count ?? 0) > 0)
            {
                _playerDataComplete = true;
                string detail = $"数据同步完成: 背包{inventory?.Count ?? 0}种, 鱼篓{fishInventory?.Count ?? 0}种";
                UpdateDetail(detail);
                Z_Logger.Log($"[LoadingManager] ✅ {detail}");
            }
            else
            {
                Z_Logger.LogWarning($"[LoadingManager] ⚠️ 玩家数据同步超时 ({timeoutDuration}s)，但继续加载");
                _playerDataComplete = true;
                UpdateDetail("数据同步完成 (无物品)");
            }
        }

        float finalProgress = WEIGHT_LOAD_DATA + WEIGHT_NET_SERVER + WEIGHT_PLAYER_DATA;
        UpdateProgress(finalProgress);
        Z_Logger.Log("[LoadingManager] PlayerData 加载阶段完成");
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

        Z_Logger.Log($"[LoadingManager] Status: {status} - {detail}");
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

    private void OnDestroy()
    {
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

        Z_Logger.Log("[LoadingManager] 已清理事件订阅");
    }
}
