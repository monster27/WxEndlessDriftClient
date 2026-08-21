using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// 地图视图 - 根据玩家拥有的岛屿情报动态生成按钮
/// </summary>
public class MapView : BaseView
{
    [Header("UI References")]
    public Transform buttonParent;          // 按钮父物体
    public GameObject mapButtonPrefab;      // UI_MapPrefab 预制体
    public Button closeButton;              // 关闭按钮
    public Text titleText;                  // 标题文本
    public Text hintText;                   // 提示文本

    // 缓存数据
    private List<int> unlockedIslandIds = new List<int>();
    private List<IslandInfo> allIslands = new List<IslandInfo>();
    private List<GameObject> createdButtons = new List<GameObject>();

    private bool isLoading = false;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        RegisterEvents();
        BindButtons();

        isInitialized = true;
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>("SceneSwitchResponse", OnSceneSwitchResponse);
        CommunicateEvent.Register<List<int>>("IslandInfoUpdated", OnIslandInfoUpdated);
    }

    private void BindButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideView);
        }
    }

    public void OpenMap()
    {
        gameObject.SetActive(true);
        LoadIslandData();
    }

    private void LoadIslandData()
    {
        if (isLoading) return;
        isLoading = true;

        LoadAllIslandsFromConfig();
        RequestPlayerIslandInfo();
    }

    private void LoadAllIslandsFromConfig()
    {
        allIslands.Clear();

        Z_Logger.Log($"[MapView] === LoadAllIslandsFromConfig ===");

        if (LoadDataManager.Instance != null && LoadDataManager.Instance.islands != null)
        {
            Z_Logger.Log($"[MapView] LoadDataManager 中岛屿数量: {LoadDataManager.Instance.islands.Count}");

            foreach (var island in LoadDataManager.Instance.islands)
            {
                allIslands.Add(new IslandInfo
                {
                    id = island.id,
                    name = island.name
                });
                Z_Logger.Log($"[MapView] 加载岛屿: ID={island.id}, Name={island.name}");
            }
        }
        else
        {
            Z_Logger.LogWarning("[MapView] LoadDataManager.Instance 或 islands 为 null");
        }

        if (allIslands.Count == 0)
        {
            Z_Logger.LogWarning("[MapView] 未加载到岛屿配置，使用默认配置");
            allIslands.Add(new IslandInfo { id = 101, name = "融冠岛" });
            allIslands.Add(new IslandInfo { id = 102, name = "珊瑚环心岛" });
            Z_Logger.Log("[MapView] 使用默认岛屿: 101(融冠岛), 102(珊瑚环心岛)");
        }

        Z_Logger.Log($"[MapView] 最终 allIslands 数量: {allIslands.Count}");
        Z_Logger.Log($"[MapView] allIslands 列表: {string.Join(", ", allIslands.Select(i => $"{i.id}({i.name})"))}");
        Z_Logger.Log($"[MapView] ==========================================");
    }

    private void RequestPlayerIslandInfo()
    {
        if (NetServerManager.Instance == null)
        {
            Z_Logger.LogWarning("[MapView] NetServerManager 未初始化");
            unlockedIslandIds = new List<int> { 101 };
            BuildMapButtons();
            isLoading = false;
            return;
        }

        int playerId = NetServerManager.Instance.GetCurrentPlayerId();
        if (playerId <= 0)
        {
            Z_Logger.LogWarning("[MapView] 玩家ID无效");
            unlockedIslandIds = new List<int> { 101 };
            BuildMapButtons();
            isLoading = false;
            return;
        }

        Z_Logger.Log($"[MapView] 请求玩家岛屿情报: PlayerId={playerId}");
        NetServerManager.Instance.FetchPlayerIslandInfo(OnIslandInfoReceived);
    }

    private void OnIslandInfoReceived(List<int> islandIds)
    {
        Z_Logger.Log($"[MapView] ========================================");
        Z_Logger.Log($"[MapView] === OnIslandInfoReceived 回调 ===");
        Z_Logger.Log($"[MapView] 原始数据是否为null: {islandIds == null}");

        if (islandIds != null)
        {
            Z_Logger.Log($"[MapView] 原始数据数量: {islandIds.Count}");
            Z_Logger.Log($"[MapView] 原始数据列表: {string.Join(", ", islandIds)}");
        }
        else
        {
            Z_Logger.LogWarning("[MapView] 服务器返回的岛屿列表为null，使用默认值");
        }

        unlockedIslandIds = islandIds ?? new List<int> { 101 };

        // 确保101始终存在
        if (!unlockedIslandIds.Contains(101))
        {
            Z_Logger.Log("[MapView] 添加默认岛屿 101");
            unlockedIslandIds.Add(101);
        }

        Z_Logger.Log($"[MapView] 最终解锁列表: {string.Join(", ", unlockedIslandIds)}");
        Z_Logger.Log($"[MapView] ========================================");

        BuildMapButtons();
        isLoading = false;
    }

    private void BuildMapButtons()
    {
        ClearButtons();

        // ✅ 详细日志 - 打印所有数据
        Z_Logger.Log($"[MapView] ========================================");
        Z_Logger.Log($"[MapView] === BuildMapButtons 开始 ===");
        Z_Logger.Log($"[MapView] unlockedIslandIds 数量: {unlockedIslandIds.Count}");
        Z_Logger.Log($"[MapView] unlockedIslandIds 列表: {string.Join(", ", unlockedIslandIds)}");
        Z_Logger.Log($"[MapView] allIslands 数量: {allIslands.Count}");
        Z_Logger.Log($"[MapView] allIslands 列表: {string.Join(", ", allIslands.Select(i => $"{i.id}({i.name})"))}");
        Z_Logger.Log($"[MapView] ========================================");

        var unlockedIslands = allIslands
            .Where(island => unlockedIslandIds.Contains(island.id))
            .ToList();

        Z_Logger.Log($"[MapView] 构建地图按钮: 已解锁 {unlockedIslands.Count}/{allIslands.Count} 个岛屿");

        // 打印匹配到的岛屿
        foreach (var island in unlockedIslands)
        {
            Z_Logger.Log($"[MapView]   ✅ 匹配岛屿: {island.name} (ID: {island.id})");
        }

        // 打印未匹配的岛屿（已解锁但不在allIslands中）
        var unmatchedIds = unlockedIslandIds.Where(id => !allIslands.Any(i => i.id == id)).ToList();
        if (unmatchedIds.Count > 0)
        {
            Z_Logger.LogWarning($"[MapView]   ⚠️ 未匹配的ID: {string.Join(", ", unmatchedIds)}");
        }

        if (titleText != null)
        {
            titleText.text = $"已解锁 {unlockedIslands.Count} 个岛屿";
        }

        if (hintText != null)
        {
            hintText.gameObject.SetActive(unlockedIslands.Count == 0);
            if (unlockedIslands.Count == 0)
            {
                hintText.text = "暂无可用岛屿，请购买岛屿情报";
            }
        }

        if (unlockedIslands.Count == 0)
        {
            Z_Logger.LogWarning("[MapView] 没有已解锁的岛屿，不创建按钮");
            Z_Logger.Log($"[MapView] ========================================");
            return;
        }

        foreach (var island in unlockedIslands)
        {
            CreateIslandButton(island);
        }

        Z_Logger.Log($"[MapView] 创建了 {unlockedIslands.Count} 个岛屿按钮");
        Z_Logger.Log($"[MapView] ========================================");
    }

    private void CreateIslandButton(IslandInfo island)
    {
        if (mapButtonPrefab == null)
        {
            Z_Logger.LogError("[MapView] mapButtonPrefab 未设置!");
            return;
        }

        if (buttonParent == null)
        {
            Z_Logger.LogError("[MapView] buttonParent 未设置!");
            return;
        }

        GameObject btnObj = Instantiate(mapButtonPrefab, buttonParent);
        btnObj.SetActive(true);
        createdButtons.Add(btnObj);

        UI_MapPrefab mapPrefab = btnObj.GetComponent<UI_MapPrefab>();
        if (mapPrefab != null)
        {
            mapPrefab.SetIslandInfo(island.id, island.name);
            mapPrefab.SetOnClickCallback(() => OnIslandButtonClick(island.id));
        }
        else
        {
            // 降级处理
            Text nameText = btnObj.GetComponentInChildren<Text>();
            if (nameText != null)
            {
                nameText.text = island.name;
            }

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                int capturedId = island.id;
                btn.onClick.AddListener(() => OnIslandButtonClick(capturedId));
            }
        }

        Z_Logger.Log($"[MapView] 创建岛屿按钮: {island.name} (ID: {island.id})");
    }

    private void ClearButtons()
    {
        foreach (var btn in createdButtons)
        {
            if (btn != null)
                Destroy(btn);
        }
        createdButtons.Clear();
    }

    private void OnIslandButtonClick(int islandId)
    {
        Z_Logger.Log($"[MapView] 点击岛屿: {islandId}");

        if (!unlockedIslandIds.Contains(islandId))
        {
            Z_Logger.LogWarning($"[MapView] 未解锁岛屿 {islandId}");
            GameUIManager.ShowMessage("尚未解锁该岛屿的情报");
            return;
        }

        var requestData = new Dictionary<string, object>
        {
            { "sceneId", islandId }
        };

        CommunicateEvent.Modify<Dictionary<string, object>>("SceneSwitchRequest", requestData);
    }

    private void OnSceneSwitchResponse(Dictionary<string, object> data)
    {
        if (data == null) return;

        bool success = data.ContainsKey("success") && (bool)data["success"];
        int sceneId = data.ContainsKey("sceneId") ? (int)data["sceneId"] : 0;

        if (success)
        {
            Z_Logger.Log($"[MapView] 场景切换成功: {sceneId}");
            HideView();
        }
        else
        {
            string message = data.ContainsKey("message") ? (string)data["message"] : "切换失败";
            Z_Logger.LogWarning($"[MapView] 场景切换失败: {message}");
            GameUIManager.ShowMessage(message);
        }
    }

    private void OnIslandInfoUpdated(List<int> islandIds)
    {
        unlockedIslandIds = islandIds;
        Z_Logger.Log($"[MapView] 岛屿情报已更新: {islandIds.Count} 个岛屿");
        BuildMapButtons();
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<string, object>>("SceneSwitchResponse", OnSceneSwitchResponse);
        CommunicateEvent.Unregister<List<int>>("IslandInfoUpdated", OnIslandInfoUpdated);
        ClearButtons();
    }

    [System.Serializable]
    public class IslandInfo
    {
        public int id;
        public string name;
    }
}
