using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// 场景材质管理器
/// </summary>
public class SceneMatManager : SingletonMonoFromScene<SceneMatManager>
{
    // ========== 枚举定义 ==========
    public enum RenderElementType
    {
        None,
        Timelmg,
        EnvBg,
        NestBaitsTouchArea,
        NestBaitsAni,
        NPC,
        Pet,
        Tent,
        FishBag,
        FishTip,
        EnvElement,
        EnvTreasureBox,
        Player,
        Weather,
        Indoor_Floor = 30,           // 室内装饰-地板
        Indoor_Wall,                 // 室内装饰-墙壁
        Indoor_Stair,                // 室内装饰-楼梯
        Indoor_LightStrip,           // 室内装饰-灯带
        Indoor_HungDecoration,       // 室内装饰-挂饰
        Indoor_Telescope,            // 室内装饰-望远镜
        Indoor_InsectRoom,           // 室内装饰-昆虫房
        Indoor_FishTank,             // 室内装饰-鱼缸
        Indoor_PetHouse,             // 室内装饰-宠物屋
        Indoor_Panda,                // 室内装饰-熊猫
        Indoor_Parrot,               // 室内装饰-鹦鹉
        Indoor_Table,                // 室内装饰-桌子
        FishTank_Backgroundk = 60,   // 鱼缸--装饰_背景 (对应子分类84)
        FishTank_Bottom,             // 鱼缸--装饰_底面 (对应子分类83)
        FishTank_Mask,               // 鱼缸--装饰_遮罩 
        FishTank_Frame,              // 鱼缸--装饰_边框 (对应子分类82)
        FishTank_Decoration,         // 鱼缸--装饰_摆设 (对应子分类80)
        FishTank_Hang,               // 鱼缸--装饰_挂饰 (对应子分类81)
        FishTank_Fish,               // 鱼缸--装饰_鱼类 
        FishTank_Btn = 70,           // 鱼缸--装饰_按钮
    }

    // ========== 渲染队列层级 ==========
    public enum RenderQueueLevel
    {
        TimeLayer = 0,
        Background = 1,
        GameLayer = 2,
        UpDefaultUILayer = 3
    }

    // ========== 资源基础路径 ==========
    private const string RESOURCE_BASE_PATH = "GameScene/Scene/";

    // ========== Inspector 参数 ==========
    [Header("=== 场景配置 ===")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private string currentSceneId = "101";
    [SerializeField] public string currentSceneName = "场景";

    [SerializeField] private bool currentSceneFlip = false;

    [Header("=== 渲染队列配置 ===")]
    [SerializeField] private int timeLayerQueue = 1000;
    [SerializeField] private int backgroundQueue = 2000;
    [SerializeField] public int gameLayerQueue = 3000;
    [SerializeField] private int effectLayerQueue = 4000;

    [Header("=== 数据路径 ===")]
    [SerializeField] public string sceneDataPath = "JsonData/Game/SceneTransData/mainTransData";

    [Header("=== 控制器列表 ===")]
    [SerializeField] private List<SceneMatCtrl> sceneControllers = new List<SceneMatCtrl>();

    // ========== 私有变量 ==========
    private SceneDataWrapper sceneDataWrapper;
    private Dictionary<RenderElementType, SceneMatCtrl> controllerDict = new Dictionary<RenderElementType, SceneMatCtrl>();
    private Dictionary<RenderQueueLevel, int> renderQueueMap;
    private bool isDataLoaded = false;
    private bool isInitialized = false;

    // ========== 公共属性 ==========
    public string CurrentSceneId => currentSceneId;
    public string CurrentSceneName => currentSceneName;
    public List<SceneMatCtrl> SceneControllers => sceneControllers;
    public bool CurrentSceneFlip => currentSceneFlip;
    public bool IsInitialized => isInitialized;

    // ========== Unity生命周期 ==========
    public async void Init()
    {
        InitializeRenderQueueMap();
        Z_Logger.Log($"[SceneMatManager] Awake - 渲染队列映射初始化完成");

        Z_Logger.Log($"[SceneMatManager] Start - 开始初始化场景系统");

        FindAndRegisterAllControllers();
        await LoadSceneData();
        ApplySceneData(currentSceneId);
        UpdateAllControllersRenderQueue();

        isInitialized = true;
        Z_Logger.Log($"[SceneMatManager] Start - ✅ 场景系统初始化完成");
    }

    public void InitializeRenderQueueMap()
    {
        renderQueueMap = new Dictionary<RenderQueueLevel, int>
        {
            { RenderQueueLevel.TimeLayer, timeLayerQueue },
            { RenderQueueLevel.Background, backgroundQueue },
            { RenderQueueLevel.GameLayer, gameLayerQueue },
            { RenderQueueLevel.UpDefaultUILayer, effectLayerQueue }
        };
    }

    // ========== 控制器管理 ==========

    public void RegisterController(SceneMatCtrl controller)
    {
        if (controller == null) return;
        if (!sceneControllers.Contains(controller))
        {
            sceneControllers.Add(controller);
            Z_Logger.Log($"[SceneMatManager] 注册控制器: {controller.ElementId}");
        }
        controllerDict[controller.ElementId] = controller;
    }

    public void UnregisterController(SceneMatCtrl controller)
    {
        if (controller == null) return;
        sceneControllers.Remove(controller);
        if (controllerDict.ContainsKey(controller.ElementId))
        {
            controllerDict.Remove(controller.ElementId);
        }
    }

    public SceneMatCtrl GetController(RenderElementType elementType)
    {
        if (controllerDict.TryGetValue(elementType, out SceneMatCtrl controller)) return controller;
        return sceneControllers.Find(c => c.ElementId == elementType);
    }

    public SceneMatCtrl GetController(string elementId)
    {
        if (Enum.TryParse<RenderElementType>(elementId, out RenderElementType type)) return GetController(type);
        return null;
    }

    public List<SceneMatCtrl> GetControllersByParameterType(SceneMatCtrl.ParameterType paramType)
    {
        List<SceneMatCtrl> result = new List<SceneMatCtrl>();
        foreach (var ctrl in sceneControllers)
        {
            if (ctrl != null && ctrl.ParamType == paramType)
            {
                result.Add(ctrl);
            }
        }
        return result;
    }

    // ========== 场景数据加载 ==========

    public async Task LoadSceneData()
    {
#if UNITY_EDITOR
        // 编辑器模式：直接从本地文件加载
        try
        {
            string filePath = Path.Combine(Application.dataPath, "Addressables/", sceneDataPath + ".json");

            if (File.Exists(filePath))
            {
                string jsonContent = File.ReadAllText(filePath);
                sceneDataWrapper = JsonUtility.FromJson<SceneDataWrapper>(jsonContent);
                if (sceneDataWrapper == null || sceneDataWrapper.scenes == null)
                {
                    sceneDataWrapper = new SceneDataWrapper();
                }
                isDataLoaded = true;
                Z_Logger.Log($"[SceneMatManager] 从本地文件加载场景数据完成，共 {sceneDataWrapper.scenes.Count} 个场景");
                return;
            }
            else
            {
                Z_Logger.LogWarning($"[SceneMatManager] 本地文件不存在: {filePath}，创建新数据");
                sceneDataWrapper = new SceneDataWrapper();
                isDataLoaded = true;
                return;
            }
        }
        catch (Exception e)
        {
            Z_Logger.LogError($"[SceneMatManager] 加载场景数据异常: {e.Message}");
            sceneDataWrapper = new SceneDataWrapper();
            isDataLoaded = true;
            return;
        }
#else
        // ✅ 运行模式：从 LoadDataManager 加载
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.isSceneDataLoaded)
        {
            sceneDataWrapper = LoadDataManager.Instance.sceneDataWrapper;
            isDataLoaded = true;
            Z_Logger.Log($"[SceneMatManager] 从 LoadDataManager 加载场景数据完成，共 {sceneDataWrapper?.scenes?.Count ?? 0} 个场景");
        }
        else
        {
            Z_Logger.LogWarning("[SceneMatManager] LoadDataManager 场景数据未加载，尝试重新加载");
            if (LoadDataManager.Instance != null)
            {
                await LoadDataManager.Instance.LoadSceneData();
            }
            if (LoadDataManager.Instance != null && LoadDataManager.Instance.isSceneDataLoaded)
            {
                sceneDataWrapper = LoadDataManager.Instance.sceneDataWrapper;
                isDataLoaded = true;
            }
            else
            {
                sceneDataWrapper = new SceneDataWrapper();
                isDataLoaded = true;
            }
        }
#endif
    }

    public SceneData GetSceneData(string sceneId)
    {
        if (sceneDataWrapper == null) return null;
        return sceneDataWrapper.scenes.Find(s => s.sceneId == sceneId);
    }

    public List<SceneData> GetAllSceneData()
    {
        return sceneDataWrapper?.scenes;
    }

    // ========== 应用场景数据 ==========

    public void ApplySceneData(string sceneId)
    {
        if (sceneDataWrapper == null) LoadSceneData();
        if (sceneDataWrapper == null) return;

        SceneData sceneData = GetSceneData(sceneId);
        if (sceneData == null)
        {
            Z_Logger.LogWarning($"[SceneMatManager] 未找到场景数据: {sceneId}");
            CreateDefaultSceneData(sceneId);
            sceneData = GetSceneData(sceneId);
            if (sceneData == null) return;
        }

        Z_Logger.Log($"[SceneMatManager] 应用场景数据: {sceneId}, 名称: {sceneData.sceneName}, 元素数: {sceneData.elements.Count}");

        currentSceneId = sceneId;
        currentSceneName = sceneData.sceneName;
        currentSceneFlip = sceneData.isFlipped;

        ApplySceneFlip(currentSceneFlip);

        int loadedCount = 0;
        foreach (var elementData in sceneData.elements)
        {
            SceneMatCtrl controller = GetController(elementData.id);
            if (controller == null)
            {
                Z_Logger.LogWarning($"[SceneMatManager] 未找到控制器: {elementData.id}");
                continue;
            }

            if (elementData.transform != null)
            {
                Vector3 position = ToUnityVector(elementData.transform.position);
                Vector3 scale = ToUnityVector(elementData.transform.scale);
                controller.SetTransformData(position, scale);
            }

            if (controller.ParamType != SceneMatCtrl.ParameterType.SceneParameter) continue;

            if (!string.IsNullOrEmpty(elementData.name))
            {
                string imagePath = RESOURCE_BASE_PATH + sceneId + "/" + elementData.name;
                controller.SetMainTextureByPath(imagePath);
            }

            controller.SetSceneId(sceneId);
            loadedCount++;
        }

        ApplyStaticParameters();

        Z_Logger.Log($"[SceneMatManager] 应用场景数据完成: {sceneId}, 名称: {currentSceneName}, 镜像: {currentSceneFlip}, 加载元素: {loadedCount} 个");

        AdjustCameraBySceneFlip();
    }

    public UnityEngine.Vector3 ToUnityVector(SerializableVector3 v)
    {
        return new UnityEngine.Vector3(v.x, v.y, v.z);
    }

    private void AdjustCameraBySceneFlip()
    {
        if (CameraManager.Instance == null)
        {
            Z_Logger.LogWarning("[SceneMatManager] CameraManager 未找到");
            return;
        }

        if (currentSceneFlip)
        {
            CameraManager.Instance.MoveToXSmooth(CameraManager.Instance.maxX, 5f);
        }
        else
        {
            CameraManager.Instance.MoveToXSmooth(CameraManager.Instance.minX, 5f);
        }
    }

    private void ApplySceneFlip(bool isFlipped)
    {
        Z_Logger.Log($"[SceneMatManager] 应用场景镜像: {isFlipped}");
        foreach (var controller in sceneControllers)
        {
            if (controller == null) continue;
            if (controller.IsCanFlip)
            {
                controller.SetFlip(isFlipped);
            }
        }
    }

    private void ApplyStaticParameters()
    {
        var staticControllers = GetControllersByParameterType(SceneMatCtrl.ParameterType.StaticParameter);
        Z_Logger.Log($"[SceneMatManager] 应用静态参数, 共 {staticControllers.Count} 个控制器");
        foreach (var controller in staticControllers)
        {
            if (controller != null && controller.IsInitialized)
            {
                // ✅ 使用带元素偏移的版本
                int queueValue = GetRenderQueueValue(controller.RenderQueue, controller.ElementId);
                controller.SetRenderQueueValue(queueValue);
            }
        }
    }

    public void UpdateAllControllersRenderQueue()
    {
        Z_Logger.Log($"[SceneMatManager] ===== 开始更新所有控制器渲染队列 =====");
        foreach (var controller in sceneControllers)
        {
            if (controller != null && controller.IsInitialized)
            {
                // ✅ 使用带元素偏移的版本
                int queueValue = GetRenderQueueValue(controller.RenderQueue, controller.ElementId);
                controller.SetRenderQueueValue(queueValue);
            }
        }
        Z_Logger.Log($"[SceneMatManager] ===== 渲染队列更新完成 =====");
    }

    public void SwitchScene(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            Z_Logger.LogError($"[SceneMatManager] 切换场景失败: sceneId为空");
            return;
        }

        SceneData targetSceneData = GetSceneData(sceneId);
        if (targetSceneData == null)
        {
            Z_Logger.LogWarning($"[SceneMatManager] 场景 {sceneId} 不存在，创建默认场景数据");
            CreateDefaultSceneData(sceneId);
        }

        Z_Logger.Log($"[SceneMatManager] 切换场景: {currentSceneId} -> {sceneId}");
        ApplySceneData(sceneId);
        UpdateAllControllersRenderQueue();
    }

    private void CreateDefaultSceneData(string sceneId)
    {
        if (sceneDataWrapper == null) sceneDataWrapper = new SceneDataWrapper();

        var existing = GetSceneData(sceneId);
        if (existing != null) return;

        var newScene = new SceneData
        {
            sceneId = sceneId,
            sceneName = $"场景_{sceneId}",
            isFlipped = false,
            elements = new List<SceneElementData>()
        };

        sceneDataWrapper.scenes.Add(newScene);

        if (currentSceneId == sceneId)
        {
            currentSceneName = newScene.sceneName;
        }

        Z_Logger.Log($"[SceneMatManager] 已创建默认场景数据: {sceneId}");
    }

    // ========== 场景镜像控制 ==========

    public void SetSceneFlip(bool isFlipped)
    {
        currentSceneFlip = isFlipped;
        ApplySceneFlip(isFlipped);

        var sceneData = GetSceneData(currentSceneId);
        if (sceneData != null)
        {
            sceneData.isFlipped = isFlipped;
        }
    }

    public bool GetSceneFlip()
    {
        return currentSceneFlip;
    }

    // ========== 渲染队列控制 ==========

    public int GetRenderQueueValue(RenderQueueLevel level)
    {
        return renderQueueMap.TryGetValue(level, out int val) ? val : 2999;
    }

    /// <summary>
    /// 获取带 RenderElementType 偏移的渲染队列值
    /// </summary>
    public int GetRenderQueueValue(RenderQueueLevel level, RenderElementType elementType)
    {
        // 1. 获取基础队列值
        int baseQueue = GetRenderQueueValue(level);

        // 2. 加上元素枚举偏移（跳过 None=0）
        int offset = elementType == RenderElementType.None ? 0 : (int)elementType;

        return baseQueue + offset;
    }

    public void SetControllerRenderQueue(SceneMatCtrl controller, RenderQueueLevel level)
    {
        if (controller == null) return;
        int queueValue = GetRenderQueueValue(level, controller.ElementId);
        controller.SetRenderQueueValue(queueValue);
    }

    public void SetAllRenderQueue(RenderQueueLevel level)
    {
        int queueValue = GetRenderQueueValue(level);
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetRenderQueueValue(queueValue + (int)controller.ElementId);
            }
        }
    }

    public void SetRenderQueueByType(SceneMatCtrl.ParameterType paramType, RenderQueueLevel level)
    {
        int queueValue = GetRenderQueueValue(level);
        var controllers = GetControllersByParameterType(paramType);
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.SetRenderQueueValue(queueValue + (int)controller.ElementId);
            }
        }
    }

    public void UpdateRenderQueueMap(RenderQueueLevel level, int value)
    {
        renderQueueMap[level] = value;
    }

    // ========== 初始化场景 ==========

    public void InitializeScene(string sceneId)
    {
        Z_Logger.Log($"[SceneMatManager] InitializeScene - 场景ID: {sceneId}");
        FindAndRegisterAllControllers();

        foreach (var controller in sceneControllers)
        {
            if (controller != null) controller.Initialize();
        }

        ApplySceneData(sceneId);
        UpdateAllControllersRenderQueue();
        isInitialized = true;
    }

    // ========== 数据收集和保存 ==========

    public void CollectDataFromControllers()
    {
        if (sceneDataWrapper == null) sceneDataWrapper = new SceneDataWrapper();

        SceneData currentSceneData = GetSceneData(currentSceneId);
        bool isNewScene = false;

        if (currentSceneData == null)
        {
            currentSceneData = new SceneData
            {
                sceneId = currentSceneId,
                sceneName = currentSceneName,
                isFlipped = currentSceneFlip,
                elements = new List<SceneElementData>()
            };
            sceneDataWrapper.scenes.Add(currentSceneData);
            isNewScene = true;
        }
        else
        {
            currentSceneData.sceneName = currentSceneName;
            currentSceneData.isFlipped = currentSceneFlip;
        }

        if (string.IsNullOrEmpty(currentSceneName) && currentSceneData != null)
        {
            currentSceneName = currentSceneData.sceneName;
        }

        currentSceneData.elements.Clear();

        foreach (var controller in sceneControllers)
        {
            if (controller == null) continue;

            var transformData = controller.GetTransformData();

            string elementName = controller.ElementPath;
            if (!string.IsNullOrEmpty(elementName))
            {
                elementName = Path.GetFileNameWithoutExtension(elementName);
            }
            else
            {
                elementName = controller.ElementId.ToString();
            }

            var element = new SceneElementData
            {
                id = controller.ElementId.ToString(),
                name = elementName
            };

            element.transform = new SceneElementTransformData
            {
                position = SerializableVector3.FromUnityVector(transformData.position.x, transformData.position.y, transformData.position.z),
                scale = SerializableVector3.FromUnityVector(transformData.scale.x, transformData.scale.y, transformData.scale.z)
            };

            currentSceneData.elements.Add(element);
        }

        currentSceneData.isFlipped = currentSceneFlip;

        Z_Logger.Log($"[SceneMatManager] 场景数据更新: {currentSceneData.sceneId}, 元素: {currentSceneData.elements.Count}");
    }

    public string SaveSceneDataToJson()
    {
        CollectDataFromControllers();
        if (sceneDataWrapper == null) sceneDataWrapper = new SceneDataWrapper();
        return JsonUtility.ToJson(sceneDataWrapper, true);
    }

    public void SaveSceneDataToFile(string filePath)
    {
        try
        {
            CollectDataFromControllers();

            string json = JsonUtility.ToJson(sceneDataWrapper, true);

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, json);

#if !UNITY_EDITOR
            if (LoadDataManager.Instance != null)
            {
                LoadDataManager.Instance.sceneDataWrapper = sceneDataWrapper;
                LoadDataManager.Instance.isSceneDataLoaded = true;
            }
#endif

            Z_Logger.Log($"[SceneMatManager] 数据已保存到: {filePath}");
        }
        catch (Exception e)
        {
            Z_Logger.LogError($"[SceneMatManager] 保存数据失败: {e.Message}");
        }
    }

    public void SaveToDefaultPath()
    {
#if UNITY_EDITOR
        string fullPath = Path.Combine(Application.dataPath, "Addressables", sceneDataPath + ".json");
        SaveSceneDataToFile(fullPath);
        UnityEditor.AssetDatabase.Refresh();
#else
        CollectDataFromControllers();
        if (LoadDataManager.Instance != null)
        {
            LoadDataManager.Instance.sceneDataWrapper = sceneDataWrapper;
            LoadDataManager.Instance.isSceneDataLoaded = true;
            Z_Logger.Log("[SceneMatManager] 场景数据已保存到 LoadDataManager");
        }
#endif
    }

    // ========== 工具方法 ==========

    public void FindAndRegisterAllControllers()
    {
        SceneMatCtrl[] foundControllers = FindObjectsOfType<SceneMatCtrl>(true);
        sceneControllers.Clear();
        controllerDict.Clear();

        foreach (var controller in foundControllers)
        {
            RegisterController(controller);
        }

        Z_Logger.Log($"[SceneMatManager] 找到并注册了 {foundControllers.Length} 个控制器");
    }

    public void RefreshAllControllers()
    {
        foreach (var controller in sceneControllers)
        {
            if (controller != null) controller.SetSceneId(currentSceneId);
        }
    }

    public string GetFullImagePath(string sceneId, string imageName)
    {
        return RESOURCE_BASE_PATH + sceneId + "/" + imageName;
    }

    public void LogAllControllerMaterials()
    {
        Z_Logger.Log($"[SceneMatManager] ===== 所有控制器材质信息 =====");
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                Material mat = controller.Material;
                Z_Logger.Log($"[SceneMatManager] {controller.ElementId}: 材质={mat?.name ?? "null"}, 渲染队列={mat?.renderQueue ?? 0}");
            }
        }
    }
}
