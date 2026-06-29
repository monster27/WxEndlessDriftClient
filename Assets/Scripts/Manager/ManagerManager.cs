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

        // 场景加载完成后，立即确保 Player 对象存在
        //EnsurePlayerExists();

        InitGameSceneManagers();
    }

    //private void EnsurePlayerExists()
    //{
    //    GameObject player = GameObject.FindGameObjectWithTag("Player");
    //    if (player == null)
    //    {
    //        player = GameObject.Find("Player");
    //    }

    //    if (player == null)
    //    {
    //        Debug.Log("[ManagerManager] 未找到 Player 对象，正在创建...");
    //        player = new GameObject("Player");
    //        player.tag = "Player";
    //        PlayerAniCtrl aniCtrl = player.AddComponent<PlayerAniCtrl>();
    //        Debug.Log("[ManagerManager] 创建 Player 对象并添加 PlayerAniCtrl");
    //    }
    //    else
    //    {
    //        PlayerAniCtrl aniCtrl = player.GetComponent<PlayerAniCtrl>();
    //        if (aniCtrl == null)
    //        {
    //            aniCtrl = player.AddComponent<PlayerAniCtrl>();
    //            Debug.Log("[ManagerManager] 为现有 Player 对象添加 PlayerAniCtrl");
    //        }
    //    }
    //}

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