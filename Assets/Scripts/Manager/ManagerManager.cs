using UnityEngine;
using System.Text;

public class ManagerManager : SingletonMono<ManagerManager>
{
    private bool initializationComplete = false;

    protected override void Awake()
    {
        base.Awake();
        StartLoadingSequence();
    }

    private void StartLoadingSequence()
    {
        // 显示加载界面
        if (UIManager.Instance != null && UIManager.Instance.loadingView != null)
        {
            UIManager.Instance.loadingView.Show();
            UIManager.Instance.loadingView.AddLoadingTask("初始化系统");
            UIManager.Instance.loadingView.onAllLoadingComplete += OnAllLoadingComplete;
        }

        InitGameSceneManagers();
    }

    private void InitGameSceneManagers()
    {
        Debug.Log("[ManagerManager] 开始初始化游戏场景管理器...");
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[ManagerManager] 初始化管理器列表:");

        // ====================================================================
        // 1. LoadDataManager - 基础数据
        // ====================================================================
        if (LoadDataManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载基础数据");

            // 如果数据还没加载，等待加载完成
            if (!LoadDataManager.Instance.isDataLoaded)
            {
                Debug.Log("[ManagerManager] 等待 LoadDataManager 加载数据...");
                // 这里假设 LoadDataManager 会在 Awake 或 Start 中自动加载
            }

            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载基础数据");
            logBuilder.AppendLine("  LoadDataManager: 已就绪");
        }

        // ====================================================================
        // 2. ItemDataManager - 物品数据
        // ====================================================================
        if (ItemDataManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化物品数据");
            ItemDataManager.Instance.Init();
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化物品数据");
            logBuilder.AppendLine("  ItemDataManager: 完成");
        }

        // ====================================================================
        // 3. GameDataManager - 游戏配置
        // ====================================================================
        if (GameDataManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载游戏配置");
            GameDataManager.Instance.Init();
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载游戏配置");
            logBuilder.AppendLine("  GameDataManager: 完成");
        }

        // ====================================================================
        // 4. NetServerManager - 网络管理器（已在 LoadingScene 初始化完成）
        // ====================================================================
        if (NetServerManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("检查网络状态");

            // 确保网络管理器已启用
            NetServerManager.Instance.SetEnabled(true);

            // 如果网络尚未初始化完成，等待一下（正常情况下已在 LoadingScene 完成）
            if (!NetServerManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[ManagerManager] NetServerManager 尚未初始化完成，等待...");
                // 这里不阻塞，因为 PlayerDataManager 会通过事件等待
            }

            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("检查网络状态");
            logBuilder.AppendLine($"  NetServerManager: 已就绪 (初始化完成: {NetServerManager.Instance.IsInitialized})");
        }
        else
        {
            Debug.LogError("[ManagerManager] NetServerManager 实例不存在！");
        }

        // ====================================================================
        // 5. UIManager - UI 管理器
        // ====================================================================
        if (UIManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化UI");
            UIManager.Instance.Init();
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化UI");
            logBuilder.AppendLine("  UIManager: 完成");
        }

        // ====================================================================
        // 6. EnvManager - 环境管理器
        // ====================================================================
        EnvManager envManager = FindObjectOfType<EnvManager>();
        if (envManager != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化环境");
            envManager.Init();
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化环境");
            logBuilder.AppendLine("  EnvManager: 完成");
        }

        // ====================================================================
        // 7. PlayerDataManager - 玩家数据管理器
        // 注意：必须在 NetServerManager 之后初始化
        // ====================================================================
        if (PlayerDataManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("加载玩家数据");

            // PlayerDataManager 会订阅 NetServerManager 的初始化完成事件
            // 如果 NetServerManager 已初始化完成，它会立即同步数据
            PlayerDataManager.Instance.Init();

            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("加载玩家数据");
            logBuilder.AppendLine($"  PlayerDataManager: 完成 (就绪: {PlayerDataManager.Instance.IsReady})");
        }

        // ====================================================================
        // 8. PlayerAniManager - 动画管理器
        // ====================================================================
        if (PlayerAniManager.Instance != null)
        {
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.AddLoadingTask("初始化动画系统");
            PlayerAniManager.Instance.Init();
            if (UIManager.Instance?.loadingView != null)
                UIManager.Instance.loadingView.CompleteLoadingTask("初始化动画系统");
            logBuilder.AppendLine("  PlayerAniManager: 完成");
        }

        // ====================================================================
        // 完成加载
        // ====================================================================
        if (UIManager.Instance?.loadingView != null)
            UIManager.Instance.loadingView.CompleteLoadingTask("初始化系统");

        logBuilder.AppendLine("[ManagerManager] 游戏场景管理器初始化完成");
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