using UnityEngine;
using System.Text;
using UnityEngine.SceneManagement;
using static SceneMatManager;

public class ManagerManager : SingletonMono<ManagerManager>
{
    private bool initializationComplete = false;

    protected override void Awake()
    {
        base.Awake();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Z_Logger.Log($"[ManagerManager] 场景加载完成: {scene.name}");

        InitGameSceneManagers();
    }

    private void InitGameSceneManagers()
    {
        Z_Logger.Log("[ManagerManager] 开始初始化游戏场景管理器...");
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[ManagerManager] 初始化管理器列表:");

        // ====================================================================
        // 1. LoadDataManager - 基础数据
        // ====================================================================
        if (LoadDataManager.Instance != null)
        {
            if (!LoadDataManager.Instance.isDataLoaded)
            {
                Z_Logger.Log("[ManagerManager] 等待 LoadDataManager 加载数据...");
            }
            logBuilder.AppendLine("  LoadDataManager: 已就绪");
        }

        // ====================================================================
        // 2. ItemDataManager - 物品数据
        // ====================================================================
        if (ItemDataManager.Instance != null)
        {
            ItemDataManager.Instance.Init();
            logBuilder.AppendLine("  ItemDataManager: 完成");
        }

        // ====================================================================
        // 3. GameDataManager - 游戏配置
        // ====================================================================
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.Init();
            logBuilder.AppendLine("  GameDataManager: 完成");
        }

        // ====================================================================
        // 4. ⭐ SkinManager - 皮肤管理器（提前初始化，确保 NetServerManager 查询时已存在）
        // ====================================================================
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.Init();
            logBuilder.AppendLine("  SkinManager: 完成");
        }
        else
        {
            Z_Logger.LogWarning("[ManagerManager] SkinManager 实例不存在，将延迟初始化");
        }

        // ====================================================================
        // 5. ⭐ NetServerManager - 网络管理器（放在 SkinManager 之后）
        // ====================================================================
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.SetEnabled(true);

            // ✅ 关键修复：启动网络数据初始化
            if (!NetServerManager.Instance.IsInitialized)
            {
                Z_Logger.Log("[ManagerManager] 启动 NetServerManager 数据初始化...");
                NetServerManager.Instance.StartInitialization();

                // ✅ 订阅初始化完成事件，在数据加载完成后才触发 UI 初始化
                NetServerManager.Instance.OnInitializationComplete += OnNetServerInitialized;
            }
            else
            {
                // 如果已经初始化完成，直接继续
                OnNetServerInitialized();
            }

            logBuilder.AppendLine($"  NetServerManager: 已启动初始化");
        }
        else
        {
            Z_Logger.LogError("[ManagerManager] NetServerManager 实例不存在！");
        }

        // ====================================================================
        // 6. EnvManager - 环境管理器
        // ====================================================================
        EnvManager envManager = FindObjectOfType<EnvManager>();
        if (envManager != null)
        {
            envManager.Init();
            logBuilder.AppendLine("  EnvManager: 完成");
        }

        // ====================================================================
        // 7. PlayerDataManager - 玩家数据管理器
        // ====================================================================
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.Init();
            logBuilder.AppendLine($"  PlayerDataManager: 完成 (就绪: {PlayerDataManager.Instance.IsReady})");
        }

        // ====================================================================
        // 8. PlayerAniManager - 动画管理器
        // ====================================================================
        if (PlayerAniManager.Instance != null)
        {
            PlayerAniManager.Instance.Init();
            logBuilder.AppendLine("  PlayerAniManager: 完成");
        }

        // ====================================================================
        // 9. SceneMatManager - 场景材质管理器
        // ====================================================================
        if (SceneMatManager.Instance != null)
        {
            SceneMatManager.Instance.Init();
            logBuilder.AppendLine("  SceneMatManager: 完成");
        }

        // ====================================================================
        // 10. FishFlyInManager - 鱼飞入管理器
        // ====================================================================
        if (FishFlyInManager.Instance != null)
        {
            if (SceneMatManager.Instance != null)
            {
                //FishFlyInManager.Instance.Init(SceneMatManager.Instance.gameLayerQueue + (int)RenderElementType.Player);
                FishFlyInManager.Instance.Init(SceneMatManager.Instance.gameLayerQueue);
            }
            logBuilder.AppendLine("  FishFlyInManager: 完成");
        }

        // ====================================================================
        // 11. UI 管理器 - 最后初始化（等待数据就绪才显示）
        // ====================================================================
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.Init();
            logBuilder.AppendLine("  UIManager: 完成");
        }

        // ====================================================================
        // 完成加载
        // ====================================================================
        logBuilder.AppendLine("[ManagerManager] 管理器初始化完成，等待网络数据...");
        Z_Logger.Log(logBuilder.ToString());

        initializationComplete = true;
    }

    /// <summary>
    /// NetServerManager 初始化完成回调
    /// </summary>
    private void OnNetServerInitialized()
    {
        Z_Logger.Log("[ManagerManager] NetServerManager 初始化完成，开始应用数据...");

        // 取消订阅，防止重复触发
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.OnInitializationComplete -= OnNetServerInitialized;
        }

        // ====================================================================
        // 场景切换 - 根据服务器数据切换场景
        // ====================================================================
        if (NetServerManager.Instance != null && NetServerManager.Instance.IsInitialized)
        {
            int sceneId = EnvManager.Instance.currentSceneId;
            string sceneIdStr = sceneId.ToString();
            Z_Logger.Log($"[ManagerManager] 切换到场景: {sceneIdStr}");

            if (SceneMatManager.Instance != null)
            {
                SceneMatManager.Instance.SwitchScene(sceneIdStr);
            }
        }

        // ====================================================================
        // 应用皮肤（此时服务器数据已返回）
        // ====================================================================
        if (SkinManager.Instance != null)
        {
            // SkinManager 会通过事件自动应用皮肤
            Z_Logger.Log("[ManagerManager] 皮肤数据将自动应用");
        }

        // ====================================================================
        // 触发所有加载完成事件
        // ====================================================================
        OnAllLoadingComplete();
    }

    private void OnAllLoadingComplete()
    {
        Z_Logger.Log("[ManagerManager] 所有加载完成，启用ClickManager");
        if (ClickManager.Instance != null)
        {
            ClickManager.Instance.IsEnabled = true;
        }

        // 1. 先发送 EVENT_ALL_LOADING_COMPLETE，触发 SkinManager.OnAllLoadingComplete → SyncSkinsFromNetServer
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_ALL_LOADING_COMPLETE, "All loading complete");

        // 2. ✅ 在 SkinManager 同步皮肤数据之后，再触发背包刷新
        //    确保 BagView.RefreshItems 执行时 SkinManager.equippedSkins 已包含服务器数据
        Z_Logger.Log("[ManagerManager] 皮肤数据已同步，触发背包刷新事件");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_REFRESH_BAG);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
