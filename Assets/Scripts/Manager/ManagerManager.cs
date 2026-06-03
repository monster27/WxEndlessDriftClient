// ==================== ManagerManager.cs ====================
using UnityEngine;
using System.Text;

public class ManagerManager : SingletonMono<ManagerManager>
{
    private bool initializationComplete = false;

    /// <summary>
    /// 是否为单机模式（离线模式）
    /// 注意：当前已强制设置为在线模式，此变量仅用于兼容旧代码
    /// </summary>
    [Header("运行模式设置")]
    [Tooltip("当前已强制为在线模式，此设置不再生效")]
    public bool isOfflineMode = false;

    protected override void Awake()
    {
        base.Awake();
        StartLoadingSequence();
    }

    private void StartLoadingSequence()
    {
        if (ClickManager.Instance != null)
        {
            ClickManager.Instance.IsEnabled = false;
        }

        if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
        {
            UIManager.Instance.loadingView.Show();
            UIManager.Instance.loadingView.AddLoadingTask("初始化系统");
            UIManager.Instance.loadingView.onAllLoadingComplete += OnAllLoadingComplete;
        }

        InitManagers();
    }

    private void InitManagers()
    {
        Debug.Log($"[ManagerManager] InitManagers() called, isOfflineMode={isOfflineMode}");
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine($"[ManagerManager] 开始初始化所有Manager... (模式: {(isOfflineMode ? "离线单机" : "在线网络")})");

        if (LoadDataManager.Instance != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载游戏数据");
            LoadDataManager.Instance.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载游戏数据");
            logBuilder.AppendLine("  LoadDataManager: 完成");
        }

        if (ItemDataManager.Instance != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化物品数据");
            ItemDataManager.Instance.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化物品数据");
            logBuilder.AppendLine("  ItemDataManager: 完成");
        }

        if (GameDataManager.Instance != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载游戏配置");
            GameDataManager.Instance.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载游戏配置");
            logBuilder.AppendLine("  GameDataManager: 完成");
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.Init();
            logBuilder.AppendLine("  UIManager: 完成");
        }

        EnvManager envManager = FindObjectOfType<EnvManager>();
        if (envManager != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化环境");
            envManager.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化环境");
            logBuilder.AppendLine("  EnvManager: 完成");
        }

        if (isOfflineMode)
        {
            if (ServerManager.Instance != null)
            {
                if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                    UIManager.Instance.loadingView.AddLoadingTask("初始化单机服务器");
                ServerManager.Instance.Init();
                ServerManager.Instance.SetEnabled(true);
                if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                    UIManager.Instance.loadingView.CompleteLoadingTask("初始化单机服务器");
                logBuilder.AppendLine("  ServerManager (离线模式): 完成");
            }

            if (NetServerManager.Instance != null)
            {
                NetServerManager.Instance.SetEnabled(false);
                logBuilder.AppendLine("  NetServerManager (离线模式): 已禁用");
            }
        }
        else
        {
            if (NetServerManager.Instance != null)
            {
                if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                    UIManager.Instance.loadingView.AddLoadingTask("初始化网络服务器");
                NetServerManager.Instance.Init();
                NetServerManager.Instance.SetEnabled(true);
                if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                    UIManager.Instance.loadingView.CompleteLoadingTask("初始化网络服务器");
                logBuilder.AppendLine("  NetServerManager (在线模式): 完成");
            }

            if (ServerManager.Instance != null)
            {
                ServerManager.Instance.SetEnabled(false);
                logBuilder.AppendLine("  ServerManager (在线模式): 已禁用");
            }
        }

        if (PlayerDataManager.Instance != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载玩家数据");
            PlayerDataManager.Instance.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载玩家数据");
            logBuilder.AppendLine("  PlayerDataManager: 完成");
        }

        if (PlayerAniManager.Instance != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化动画系统");
            PlayerAniManager.Instance.Init();
            if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化动画系统");
            logBuilder.AppendLine("  PlayerAniManager: 完成");
        }

        if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
            UIManager.Instance.loadingView.CompleteLoadingTask("初始化系统");

        logBuilder.AppendLine("[ManagerManager] 所有Manager初始化完成");
        Debug.Log(logBuilder.ToString());

        initializationComplete = true;
    }

    private void OnAllLoadingComplete()
    {
        Debug.Log("[ManagerManager] 所有加载完成，启用ClickManager");
        if (ClickManager.Instance != null)
        {
            ClickManager.Instance.IsEnabled = true;
        }
    }
}