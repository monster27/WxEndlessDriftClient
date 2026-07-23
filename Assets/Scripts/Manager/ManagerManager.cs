using UnityEngine;
using System.Text;
using UnityEngine.SceneManagement;

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
        Debug.Log($"[ManagerManager] 场景加载完成: {scene.name}");

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
            if (!LoadDataManager.Instance.isDataLoaded)
            {
                Debug.Log("[ManagerManager] 等待 LoadDataManager 加载数据...");
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
        // 4. NetServerManager - 网络管理器
        // ====================================================================
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.SetEnabled(true);

            if (!NetServerManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[ManagerManager] NetServerManager 尚未初始化完成，等待...");
            }

            logBuilder.AppendLine($"  NetServerManager: 已就绪 (初始化完成: {NetServerManager.Instance.IsInitialized})");
        }
        else
        {
            Debug.LogError("[ManagerManager] NetServerManager 实例不存在！");
        }

        // ====================================================================
        // 5. UIManager - UI 管理器
        // ====================================================================
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.Init();
            logBuilder.AppendLine("  UIManager: 完成");
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
        // 9. 场景切换 - 根据服务器数据切换场景
        // ====================================================================
        if (NetServerManager.Instance != null && NetServerManager.Instance.IsInitialized)
        {
            // 获取服务器返回的场景ID
            int sceneId = EnvManager.Instance.currentSceneId;

            // 调用 SceneMatManager 切换场景
            SceneMatManager sceneMatManager = FindObjectOfType<SceneMatManager>();
            if (sceneMatManager != null)
            {
                string sceneIdStr = sceneId.ToString();
                Debug.Log($"[ManagerManager] 切换到场景: {sceneIdStr}");
                sceneMatManager.SwitchScene(sceneIdStr);
                logBuilder.AppendLine($"  场景切换: {sceneIdStr}");
            }
            else
            {
                Debug.LogWarning("[ManagerManager] SceneMatManager 未找到，无法切换场景");
                logBuilder.AppendLine("  场景切换: 失败 (SceneMatManager 未找到)");
            }
        }
        else
        {
            Debug.LogWarning("[ManagerManager] NetServerManager 未初始化完成，跳过场景切换");
            logBuilder.AppendLine("  场景切换: 跳过 (NetServerManager 未就绪)");
        }

        if (SceneMatManager.Instance != null)
        {
            SceneMatManager.Instance.Init();
        }

        if (FishFlyInManager.Instance != null)
        {
            if (SceneMatManager.Instance != null)
            {
                FishFlyInManager.Instance.Init(SceneMatManager.Instance.gameLayerQueue + 1);
            }
        }

        // ====================================================================
        // 完成加载
        // ====================================================================
        logBuilder.AppendLine("[ManagerManager] 游戏场景管理器初始化完成");
        Debug.Log(logBuilder.ToString());

        initializationComplete = true;

        // 所有加载完成，启用ClickManager
        OnAllLoadingComplete();
    }

    private void OnAllLoadingComplete()
    {
        Debug.Log("[ManagerManager] 所有加载完成，启用ClickManager");
        if (ClickManager.Instance != null)
        {
            ClickManager.Instance.IsEnabled = true;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
