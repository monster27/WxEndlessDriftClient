using UnityEngine;
using System.Collections.Generic;

public class SkinManager : SingletonMonoFromScene<SkinManager>
{
    public GameObject indoorSceneObj;
    public GameObject outdoorSceneObj;

    private Dictionary<int, int> equippedSkins = new Dictionary<int, int>();
    private bool isSceneMatReady = false;
    private bool hasPendingSkins = false;
    private bool isSkinInitialized = false;

    private Dictionary<int, SceneMatManager.RenderElementType> slotTypeToRenderType = new Dictionary<int, SceneMatManager.RenderElementType>
    {
        { 41, SceneMatManager.RenderElementType.FishBag },
        { 42, SceneMatManager.RenderElementType.Tent },
        { 43, SceneMatManager.RenderElementType.FishTip },
        { 51, SceneMatManager.RenderElementType.Indoor_Wall },
        { 52, SceneMatManager.RenderElementType.Indoor_Floor },
        { 53, SceneMatManager.RenderElementType.Indoor_Stair },
        { 54, SceneMatManager.RenderElementType.Indoor_LightStrip },
        { 55, SceneMatManager.RenderElementType.Indoor_HungDecoration },
        { 56, SceneMatManager.RenderElementType.Indoor_Telescope },
        { 57, SceneMatManager.RenderElementType.Indoor_InsectRoom },
        { 58, SceneMatManager.RenderElementType.Indoor_PetHouse },
        { 59, SceneMatManager.RenderElementType.Indoor_FishTank },
        { 60, SceneMatManager.RenderElementType.Indoor_Panda },
        { 61, SceneMatManager.RenderElementType.Indoor_Parrot },
        { 62, SceneMatManager.RenderElementType.Indoor_Table }
    };

    private const string OUTDOOR_SKIN_PATH_PREFIX = "UI/Icon/OutdoorSkinIcons/";
    private const string INDOOR_SKIN_PATH_PREFIX = "UI/Icon/IndoorSkinIcons/";

    // 默认皮肤ID（与服务器 PlayerSkinManager.InitializeDefaultSkins 保持一致）
    private static readonly Dictionary<int, int> DefaultSkins = new Dictionary<int, int>
    {
        { 41, 4001 },  // 室外-鱼篓
        { 42, 4101 },  // 室外-帐篷
        { 43, 4201 },  // 室外-指示器
        { 51, 5001 },  // 室内-墙壁
        { 52, 5051 },  // 室内-地板
        { 53, 5101 },  // 室内-楼梯
        { 54, 5151 },  // 室内-灯带
        { 55, 5201 },  // 室内-挂饰
        { 56, 5251 },  // 室内-望远镜
        { 57, 5301 },  // 室内-昆虫房
        { 58, 5351 },  // 室内-宠物屋
        { 59, 5401 },  // 室内-鱼缸
        { 60, 5451 },  // 室内-熊猫
        { 61, 5501 },  // 室内-鹦鹉
        { 62, 5551 }   // 室内-桌子
    };

    /// <summary>
    /// Unity Awake：确保即使 ManagerManager 在 Instance 就绪前调用失败，
    /// SkinManager 也能在自身 Awake 时完成事件注册与默认皮肤初始化。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        // 自主初始化，防止 ManagerManager.OnSceneLoaded 触发时 Instance 尚未就绪导致 Init 未被调用
        if (!isSkinInitialized)
        {
            Init();
        }
    }

    public void Init()
    {
        if (isSkinInitialized)
        {
            Debug.Log("[SkinManager] 已初始化，跳过重复调用");
            return;
        }
        isSkinInitialized = true;

        // 用默认皮肤初始化equippedSkins，确保在服务器数据未返回前皮肤也显示为"已装备"
        equippedSkins = new Dictionary<int, int>(DefaultSkins);
        Debug.Log($"[SkinManager] Init: 用默认皮肤初始化，共 {equippedSkins.Count} 个");

        RegisterEvents();
        CheckAndApplySkins();
    }

    private void CheckAndApplySkins()
    {
        if (equippedSkins.Count > 0 && SceneMatManager.Instance != null && SceneMatManager.Instance.IsInitialized)
        {
            Debug.Log("[SkinManager] Awake时发现已有皮肤数据，立即应用");
            ApplyAllSkins();
        }
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, OnSkinDataUpdated);
        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_ALL_LOADING_COMPLETE, OnAllLoadingComplete);
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, OnSkinDataUpdated);
        CommunicateEvent.Unregister<string>(CommunicateEvent.EVENT_ALL_LOADING_COMPLETE, OnAllLoadingComplete);
    }

    private void OnAllLoadingComplete(string message)
    {
        Debug.Log("[SkinManager] 收到所有加载完成事件，开始应用皮肤");
        isSceneMatReady = true;

        // ✅ 主动从 NetServerManager 同步皮肤数据（降级方案，不依赖 EVENT_SKIN_DATA_UPDATED 事件）
        // 解决事件时序问题导致服务器皮肤数据未流向 SkinManager 的问题
        SyncSkinsFromNetServer();

        if (hasPendingSkins && equippedSkins.Count > 0)
        {
            ApplyAllSkins();
        }
    }

    /// <summary>
    /// 确保皮肤数据已从 NetServerManager 同步（供外部模块如 BagView.OpenBag 主动调用）
    /// </summary>
    public void EnsureSkinsSynced()
    {
        SyncSkinsFromNetServer();
    }

    /// <summary>
    /// 从 NetServerManager 主动拉取皮肤数据（不依赖事件，确保数据同步可靠）
    /// </summary>
    private void SyncSkinsFromNetServer()
    {
        if (NetServerManager.Instance == null)
        {
            Debug.LogWarning("[SkinManager] SyncSkinsFromNetServer - NetServerManager 为空，跳过");
            return;
        }

        var skinsData = NetServerManager.Instance.GetEquippedSkinsData();
        if (skinsData == null || skinsData.Count == 0)
        {
            Debug.LogWarning("[SkinManager] SyncSkinsFromNetServer - 服务器皮肤数据为空，保持默认值");
            return;
        }

        // 使用与 OnSkinDataUpdated 相同的合并逻辑
        equippedSkins = new Dictionary<int, int>(DefaultSkins);
        int serverOverrideCount = 0;
        foreach (var kvp in skinsData)
        {
            if (kvp.Value > 0)
            {
                equippedSkins[kvp.Key] = kvp.Value;
                serverOverrideCount++;
            }
        }
        Debug.Log($"[SkinManager] SyncSkinsFromNetServer - 主动同步成功，服务器覆盖 {serverOverrideCount} 个，合并后共 {equippedSkins.Count} 个皮肤");
    }

    public void SwitchToIndoorScene()
    {
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(true);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(false);
        }
        Debug.Log("[SkinManager] 切换到室内场景");
    }

    public void SwitchToOutdoorScene()
    {
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        Debug.Log("[SkinManager] 切换到室外场景");
    }

    public void EquipSkin(int slotType, int skinId)
    {
        equippedSkins[slotType] = skinId;
        Debug.Log($"[SkinManager] 装备皮肤: slotType={slotType}, skinId={skinId}");
        ApplySkinRender(slotType, skinId);
    }

    public int GetEquippedSkin(int slotType)
    {
        if (equippedSkins.TryGetValue(slotType, out int skinId) && skinId > 0)
        {
            return skinId;
        }
        // 未装备皮肤时返回默认皮肤ID
        if (DefaultSkins.TryGetValue(slotType, out int defaultSkinId))
        {
            return defaultSkinId;
        }
        return 0;
    }

    public bool IsSkinEquipped(int itemId)
    {
        return equippedSkins.ContainsValue(itemId);
    }

    public Dictionary<int, int> GetAllEquippedSkins()
    {
        return new Dictionary<int, int>(equippedSkins);
    }

    public void ApplyAllSkins()
    {
        Debug.Log($"[SkinManager] 开始应用所有皮肤，已装备 {equippedSkins.Count} 个");

        // 合并已装备皮肤和默认皮肤：已装备的优先，未装备的使用默认值
        var allSkins = new Dictionary<int, int>(DefaultSkins);
        foreach (var kvp in equippedSkins)
        {
            if (kvp.Value > 0)
            {
                allSkins[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in allSkins)
        {
            ApplySkinRender(kvp.Key, kvp.Value);
        }
        hasPendingSkins = false;
    }

    private void OnSkinDataUpdated(Dictionary<int, int> skins)
    {
        // 先重置为默认皮肤，确保所有槽位都有初始值
        equippedSkins = new Dictionary<int, int>(DefaultSkins);

        // 用服务器数据覆盖默认值（服务器数据优先，值为0的表示该槽位无皮肤）
        int serverOverrideCount = 0;
        foreach (var kvp in skins)
        {
            if (kvp.Value > 0)
            {
                equippedSkins[kvp.Key] = kvp.Value;
                serverOverrideCount++;
            }
        }
        Debug.Log($"[SkinManager] 皮肤数据更新，服务器返回 {skins.Count} 个，覆盖默认 {serverOverrideCount} 个，合并后共 {equippedSkins.Count} 个皮肤");

        if (isSceneMatReady && SceneMatManager.Instance != null && SceneMatManager.Instance.IsInitialized)
        {
            ApplyAllSkins();
        }
        else
        {
            hasPendingSkins = true;
            Debug.Log("[SkinManager] SceneMatManager尚未就绪，延迟应用皮肤");
        }
    }

    private void ApplySkinRender(int slotType, int skinId)
    {
        if (!slotTypeToRenderType.TryGetValue(slotType, out SceneMatManager.RenderElementType renderType))
        {
            Debug.LogWarning($"[SkinManager] 未找到slotType={slotType}对应的渲染类型");
            return;
        }

        if (SceneMatManager.Instance == null)
        {
            Debug.LogWarning("[SkinManager] SceneMatManager未找到");
            return;
        }

        if (!SceneMatManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[SkinManager] SceneMatManager尚未初始化");
            return;
        }

        SceneMatCtrl controller = SceneMatManager.Instance.GetController(renderType);
        if (controller == null)
        {
            Debug.LogWarning($"[SkinManager] 未找到渲染类型{renderType}对应的控制器");
            return;
        }

        string skinPath = GetSkinPath(slotType, skinId);
        Debug.Log($"[SkinManager] 应用皮肤: slotType={slotType}, skinId={skinId}, renderType={renderType}, path={skinPath}");

        controller.SetMainTextureByPath(skinPath);
    }

    private string GetSkinPath(int slotType, int skinId)
    {
        if (slotType >= 41 && slotType <= 43)
        {
            return OUTDOOR_SKIN_PATH_PREFIX + skinId;
        }
        else if (slotType >= 51)
        {
            return INDOOR_SKIN_PATH_PREFIX + skinId;
        }
        return "";
    }
}
