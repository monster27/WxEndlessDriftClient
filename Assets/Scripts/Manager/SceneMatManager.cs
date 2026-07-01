using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 场景材质管理器
/// </summary>
public class SceneMatManager : SingletonMonoFromScene<SceneMatManager>
{
    // ========== 枚举定义 ==========
    public enum ElementType
    {
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
        Weather
    }

    // ========== 渲染队列层级（四个层次） ==========
    public enum RenderQueueLevel
    {
        TimeLayer = 0,
        Background = 1,
        GameLayer = 2,
        EffectLayer = 3
    }

    // ========== 资源基础路径 ==========
    private const string RESOURCE_BASE_PATH = "GameScene/Scene/";

    // ========== 数据类定义 ==========
    [Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Vector3 scale;

        public TransformData()
        {
            position = Vector3.zero;
            scale = Vector3.one;
        }
    }

    [Serializable]
    public class SceneElementData
    {
        public string id;
        public string name;
        public TransformData transform;

        public SceneElementData()
        {
            id = "";
            name = "";
            transform = new TransformData();
        }
    }

    [Serializable]
    public class SceneData
    {
        public string sceneId;
        public string sceneName;
        public bool isFlipped;
        public List<SceneElementData> elements;

        public SceneData()
        {
            sceneId = "";
            sceneName = "";
            isFlipped = false;
            elements = new List<SceneElementData>();
        }
    }

    [Serializable]
    public class SceneDataWrapper
    {
        public List<SceneData> scenes;

        public SceneDataWrapper()
        {
            scenes = new List<SceneData>();
        }
    }

    // ========== Inspector 参数 ==========
    [Header("=== 场景配置 ===")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private string currentSceneId = "101";
    [SerializeField] public string currentSceneName = "场景";

    // ========== 当前场景镜像状态（存储在Manager中） ==========
    [SerializeField] private bool currentSceneFlip = false;

    [Header("=== 渲染队列配置（四个层次） ===")]
    [SerializeField] private int timeLayerQueue = 1000;
    [SerializeField] private int backgroundQueue = 2000;
    [SerializeField] private int gameLayerQueue = 3000;
    [SerializeField] private int effectLayerQueue = 4000;

    [Header("=== 数据路径 ===")]
    [SerializeField] public string sceneDataPath = "JsonData/Game/SceneTransData/mainTransData";

    [Header("=== 控制器列表 ===")]
    [SerializeField] private List<SceneMatCtrl> sceneControllers = new List<SceneMatCtrl>();

    // ========== 私有变量 ==========
    private SceneDataWrapper sceneDataWrapper;
    private Dictionary<ElementType, SceneMatCtrl> controllerDict = new Dictionary<ElementType, SceneMatCtrl>();
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
    private void Awake()
    {
        InitializeRenderQueueMap();
        Debug.Log($"[SceneMatManager] Awake - 渲染队列映射初始化完成: TimeLayer={timeLayerQueue}, Background={backgroundQueue}, GameLayer={gameLayerQueue}, EffectLayer={effectLayerQueue}");
    }

    private void Start()
    {
        if (loadOnStart)
        {
            Debug.Log($"[SceneMatManager] Start - 开始初始化场景系统");

            // 1. 先查找并注册所有控制器
            FindAndRegisterAllControllers();

            // 2. 加载场景数据
            LoadSceneData();

            // 3. 应用场景数据
            ApplySceneData(currentSceneId);

            // 4. 统一更新所有控制器的渲染队列
            UpdateAllControllersRenderQueue();

            isInitialized = true;
            Debug.Log($"[SceneMatManager] Start - ✅ 场景系统初始化完成");
        }
    }

    private void InitializeRenderQueueMap()
    {
        renderQueueMap = new Dictionary<RenderQueueLevel, int>
        {
            { RenderQueueLevel.TimeLayer, timeLayerQueue },
            { RenderQueueLevel.Background, backgroundQueue },
            { RenderQueueLevel.GameLayer, gameLayerQueue },
            { RenderQueueLevel.EffectLayer, effectLayerQueue }
        };
    }

    // ========== 控制器管理 ==========

    public void RegisterController(SceneMatCtrl controller)
    {
        if (controller == null) return;
        if (!sceneControllers.Contains(controller))
        {
            sceneControllers.Add(controller);
            Debug.Log($"[SceneMatManager] 注册控制器: {controller.ElementId} (游戏对象: {controller.gameObject.name})");
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
            Debug.Log($"[SceneMatManager] 注销控制器: {controller.ElementId}");
        }
    }

    public SceneMatCtrl GetController(ElementType elementType)
    {
        if (controllerDict.TryGetValue(elementType, out SceneMatCtrl controller)) return controller;
        return sceneControllers.Find(c => c.ElementId == elementType);
    }

    public SceneMatCtrl GetController(string elementId)
    {
        if (Enum.TryParse<ElementType>(elementId, out ElementType type)) return GetController(type);
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

    public void LoadSceneData()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>(sceneDataPath);
            if (jsonFile == null)
            {
                Debug.LogWarning($"[SceneMatManager] 无法加载场景数据文件: {sceneDataPath}，将创建新的数据");
                sceneDataWrapper = new SceneDataWrapper();
                isDataLoaded = true;
                return;
            }

            sceneDataWrapper = JsonUtility.FromJson<SceneDataWrapper>(jsonFile.text);
            if (sceneDataWrapper == null || sceneDataWrapper.scenes == null)
            {
                Debug.LogWarning("[SceneMatManager] 场景数据解析失败，将创建新的数据");
                sceneDataWrapper = new SceneDataWrapper();
            }

            isDataLoaded = true;
            Debug.Log($"[SceneMatManager] 加载场景数据完成，共 {sceneDataWrapper.scenes.Count} 个场景");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneMatManager] 加载场景数据异常: {e.Message}");
            sceneDataWrapper = new SceneDataWrapper();
            isDataLoaded = true;
        }
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
            Debug.LogWarning($"[SceneMatManager] 未找到场景数据: {sceneId}");
            CreateDefaultSceneData(sceneId);
            sceneData = GetSceneData(sceneId);
            if (sceneData == null) return;
        }

        Debug.Log($"[SceneMatManager] 应用场景数据: {sceneId}, 名称: {sceneData.sceneName}, 元素数: {sceneData.elements.Count}");

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
                Debug.LogWarning($"[SceneMatManager] 未找到控制器: {elementData.id}");
                continue;
            }

            if (elementData.transform != null)
            {
                controller.SetTransformData(elementData.transform.position, elementData.transform.scale);
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

        Debug.Log($"[SceneMatManager] 应用场景数据完成: {sceneId}, 名称: {currentSceneName}, 镜像: {currentSceneFlip}, 加载元素: {loadedCount} 个 (仅SceneParameter)");

        // ===== ✅ 场景加载完成后，根据镜像状态调整摄像头位置 =====
        AdjustCameraBySceneFlip();
    }

    /// <summary>
    /// 根据场景镜像状态调整摄像头位置
    /// 镜像模式：摄像头移动到最大位置 (maxX)
    /// 非镜像模式：摄像头移动到最小位置 (minX)
    /// </summary>
    private void AdjustCameraBySceneFlip()
    {
        if (CameraManager.Instance == null)
        {
            Debug.LogWarning("[SceneMatManager] CameraManager 未找到，无法调整摄像头");
            return;
        }

        // 同步 CameraManager 的镜像模式
        CameraManager.Instance.SetMirrorMode(currentSceneFlip);

        // 根据镜像模式调整摄像头位置
        if (currentSceneFlip)
        {
            CameraManager.Instance.MoveToXSmooth(CameraManager.Instance.maxX, 5f);
            Debug.Log($"[SceneMatManager] 场景镜像模式，摄像头移动到最大位置: {CameraManager.Instance.maxX}");
        }
        else
        {
            CameraManager.Instance.MoveToXSmooth(CameraManager.Instance.minX, 5f);
            Debug.Log($"[SceneMatManager] 非镜像模式，摄像头移动到最小位置: {CameraManager.Instance.minX}");
        }
    }

    /// <summary>
    /// 应用场景级别的镜像
    /// </summary>
    private void ApplySceneFlip(bool isFlipped)
    {
        Debug.Log($"[SceneMatManager] 应用场景镜像: {isFlipped}");
        foreach (var controller in sceneControllers)
        {
            if (controller == null) continue;
            //if (controller.ParamType != SceneMatCtrl.ParameterType.SceneParameter) continue;
            if (controller.IsCanFlip)
            {
                controller.SetFlip(isFlipped);
            }
        }
    }

    private void ApplyStaticParameters()
    {
        var staticControllers = GetControllersByParameterType(SceneMatCtrl.ParameterType.StaticParameter);
        Debug.Log($"[SceneMatManager] 应用静态参数, 共 {staticControllers.Count} 个控制器");
        foreach (var controller in staticControllers)
        {
            if (controller != null && controller.IsInitialized)
            {
                int queueValue = GetRenderQueueValue(controller.RenderQueue);
                Debug.Log($"[SceneMatManager] 静态控制器 {controller.ElementId} 渲染队列: {controller.RenderQueue} -> {queueValue}");
                controller.SetRenderQueueValue(queueValue);
            }
        }
    }

    /// <summary>
    /// 统一更新所有控制器的渲染队列
    /// </summary>
    public void UpdateAllControllersRenderQueue()
    {
        Debug.Log($"[SceneMatManager] ===== 开始更新所有控制器渲染队列 =====");
        foreach (var controller in sceneControllers)
        {
            if (controller != null && controller.IsInitialized)
            {
                int queueValue = GetRenderQueueValue(controller.RenderQueue);
                Debug.Log($"[SceneMatManager] 更新控制器 {controller.ElementId} ({controller.gameObject.name}) 渲染队列: {controller.RenderQueue} -> {queueValue}");
                controller.SetRenderQueueValue(queueValue);
            }
            else if (controller != null && !controller.IsInitialized)
            {
                Debug.Log($"[SceneMatManager] 跳过未初始化的控制器: {controller.ElementId}");
            }
        }
        Debug.Log($"[SceneMatManager] ===== 渲染队列更新完成 =====");
    }

    public void SwitchScene(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            Debug.LogError($"[SceneMatManager] 切换场景失败: sceneId为空");
            return;
        }

        // ✅ 检查场景是否存在
        SceneData targetSceneData = GetSceneData(sceneId);
        if (targetSceneData == null)
        {
            Debug.LogWarning($"[SceneMatManager] 场景 {sceneId} 不存在，创建默认场景数据");
            CreateDefaultSceneData(sceneId);
        }

        Debug.Log($"[SceneMatManager] 切换场景: {currentSceneId} -> {sceneId}");
        ApplySceneData(sceneId);
        UpdateAllControllersRenderQueue();
    }

    /// <summary>
    /// 创建默认场景数据（当场景不存在时）
    /// </summary>
    private void CreateDefaultSceneData(string sceneId)
    {
        if (sceneDataWrapper == null) sceneDataWrapper = new SceneDataWrapper();

        // ✅ 检查是否已存在（防止重复创建）
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

        // ✅ 同步更新 currentSceneName
        if (currentSceneId == sceneId)
        {
            currentSceneName = newScene.sceneName;
        }

        Debug.Log($"[SceneMatManager] 已创建默认场景数据: {sceneId}, 名称: {newScene.sceneName}");
    }

    // ========== 场景镜像控制 ==========

    /// <summary>
    /// 设置场景镜像
    /// </summary>
    public void SetSceneFlip(bool isFlipped)
    {
        currentSceneFlip = isFlipped;
        ApplySceneFlip(isFlipped);

        // 更新场景数据中的镜像
        var sceneData = GetSceneData(currentSceneId);
        if (sceneData != null)
        {
            sceneData.isFlipped = isFlipped;
        }

        Debug.Log($"[SceneMatManager] 场景镜像已设置为: {isFlipped}");
    }

    /// <summary>
    /// 获取场景镜像
    /// </summary>
    public bool GetSceneFlip()
    {
        return currentSceneFlip;
    }

    // ========== 渲染队列控制（由Manager统一管理） ==========

    /// <summary>
    /// 获取渲染队列值（由Manager统一管理）
    /// </summary>
    public int GetRenderQueueValue(RenderQueueLevel level)
    {
        int value = renderQueueMap.TryGetValue(level, out int val) ? val : 3000;
        Debug.Log($"[SceneMatManager] GetRenderQueueValue - {level} -> {value}");
        return value;
    }

    /// <summary>
    /// 获取渲染队列映射的详细日志
    /// </summary>
    public void LogRenderQueueMap()
    {
        Debug.Log($"[SceneMatManager] ===== 渲染队列映射 =====");
        foreach (var kvp in renderQueueMap)
        {
            Debug.Log($"[SceneMatManager] {kvp.Key} -> {kvp.Value}");
        }
        Debug.Log($"[SceneMatManager] =========================");
    }

    /// <summary>
    /// 设置单个控制器的渲染队列
    /// </summary>
    public void SetControllerRenderQueue(SceneMatCtrl controller, RenderQueueLevel level)
    {
        if (controller == null) return;
        int queueValue = GetRenderQueueValue(level);
        Debug.Log($"[SceneMatManager] 设置控制器 {controller.ElementId} 渲染队列: {level} -> {queueValue}");
        controller.SetRenderQueueValue(queueValue);
    }

    /// <summary>
    /// 设置所有控制器的渲染队列
    /// </summary>
    public void SetAllRenderQueue(RenderQueueLevel level)
    {
        int queueValue = GetRenderQueueValue(level);
        Debug.Log($"[SceneMatManager] 设置所有控制器渲染队列: {level} -> {queueValue}");
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetRenderQueueValue(queueValue);
            }
        }
    }

    /// <summary>
    /// 按类型设置渲染队列
    /// </summary>
    public void SetRenderQueueByType(SceneMatCtrl.ParameterType paramType, RenderQueueLevel level)
    {
        int queueValue = GetRenderQueueValue(level);
        var controllers = GetControllersByParameterType(paramType);
        Debug.Log($"[SceneMatManager] 按类型 {paramType} 设置渲染队列: {level} -> {queueValue}, 共 {controllers.Count} 个控制器");
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.SetRenderQueueValue(queueValue);
            }
        }
    }

    /// <summary>
    /// 更新渲染队列映射
    /// </summary>
    public void UpdateRenderQueueMap(RenderQueueLevel level, int value)
    {
        Debug.Log($"[SceneMatManager] 更新渲染队列映射: {level} -> {value} (旧值: {renderQueueMap[level]})");
        renderQueueMap[level] = value;
    }

    // ========== 初始化场景 ==========

    public void InitializeScene(string sceneId)
    {
        Debug.Log($"[SceneMatManager] InitializeScene - 场景ID: {sceneId}");
        FindAndRegisterAllControllers();

        foreach (var controller in sceneControllers)
        {
            if (controller != null) controller.Initialize();
        }

        ApplySceneData(sceneId);
        UpdateAllControllersRenderQueue();
        isInitialized = true;
        Debug.Log($"[SceneMatManager] InitializeScene - ✅ 完成");
    }

    // ========== 数据收集和保存 ==========

    /// <summary>
    /// 从所有控制器收集数据（保存所有场景，更新或新增当前场景）
    /// </summary>
    /// <summary>
    /// 从所有控制器收集数据（保存所有场景，更新或新增当前场景）
    /// </summary>
    public void CollectDataFromControllers()
    {
        if (sceneDataWrapper == null) sceneDataWrapper = new SceneDataWrapper();

        // ===== 查找当前场景是否存在 =====
        SceneData currentSceneData = GetSceneData(currentSceneId);
        bool isNewScene = false;

        if (currentSceneData == null)
        {
            // ===== 不存在则创建新场景 =====
            currentSceneData = new SceneData
            {
                sceneId = currentSceneId,
                sceneName = currentSceneName,  // ✅ 使用 currentSceneName
                isFlipped = currentSceneFlip,
                elements = new List<SceneElementData>()
            };
            sceneDataWrapper.scenes.Add(currentSceneData);
            isNewScene = true;
        }
        else
        {
            // ✅ 【关键修复】强制使用编辑器中修改的名称覆盖
            // 不管场景数据中原来的名称是什么，都用 currentSceneName 替换
            currentSceneData.sceneName = currentSceneName;

            // ✅ 同步镜像状态
            currentSceneData.isFlipped = currentSceneFlip;
        }

        // ✅ 确保 currentSceneName 与场景数据一致（双向同步）
        if (string.IsNullOrEmpty(currentSceneName) && currentSceneData != null)
        {
            currentSceneName = currentSceneData.sceneName;
        }

        // ===== 保存旧数据用于对比 =====
        string oldData = JsonUtility.ToJson(currentSceneData, true);

        // ===== 清空当前场景的元素，重新收集 =====
        currentSceneData.elements.Clear();

        // ===== 保存所有控制器到当前场景 =====
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

            element.transform = new TransformData
            {
                position = transformData.position,
                scale = transformData.scale
            };

            currentSceneData.elements.Add(element);
        }

        currentSceneData.isFlipped = currentSceneFlip;

        string newData = JsonUtility.ToJson(currentSceneData, true);

        // ===== 打印日志 =====
        Debug.Log($"[SceneMatManager] ===== 场景数据更新 =====");
        Debug.Log($"[SceneMatManager] 场景ID: {currentSceneData.sceneId}, 名称: {currentSceneData.sceneName}");
        Debug.Log($"[SceneMatManager] currentSceneName (编辑器中的值): {currentSceneName}");
        Debug.Log($"[SceneMatManager] 场景镜像: {currentSceneData.isFlipped}");
        Debug.Log($"[SceneMatManager] 是否为新场景: {isNewScene}");
        Debug.Log($"[SceneMatManager] 保存元素数量: {currentSceneData.elements.Count} (所有控制器)");
        Debug.Log($"[SceneMatManager] JSON中共有 {sceneDataWrapper.scenes.Count} 个场景");

        if (!isNewScene)
        {
            Debug.Log($"[SceneMatManager] --- 替换前 ---\n{oldData}");
            Debug.Log($"[SceneMatManager] --- 替换后 ---\n{newData}");
        }
        else
        {
            Debug.Log($"[SceneMatManager] --- 新增场景 ---\n{newData}");
        }

        Debug.Log($"[SceneMatManager] =================================");
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
            Debug.Log($"[SceneMatManager] 数据已保存到: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneMatManager] 保存数据失败: {e.Message}");
        }
    }

    public void SaveToDefaultPath()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources", sceneDataPath + ".json");
        SaveSceneDataToFile(fullPath);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
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

        Debug.Log($"[SceneMatManager] 找到并注册了 {foundControllers.Length} 个控制器");

        // 打印所有注册的控制器
        foreach (var kvp in controllerDict)
        {
            Debug.Log($"[SceneMatManager] 已注册: {kvp.Key} -> {kvp.Value.gameObject.name}");
        }
    }

    public void RefreshAllControllers()
    {
        foreach (var controller in sceneControllers)
        {
            if (controller != null) controller.SetSceneId(currentSceneId);
        }
    }

    /// <summary>
    /// 获取完整图片路径
    /// </summary>
    public string GetFullImagePath(string sceneId, string imageName)
    {
        return RESOURCE_BASE_PATH + sceneId + "/" + imageName;
    }

    /// <summary>
    /// 打印所有控制器的材质信息（用于调试）
    /// </summary>
    public void LogAllControllerMaterials()
    {
        Debug.Log($"[SceneMatManager] ===== 所有控制器材质信息 =====");
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                Material mat = controller.Material;
                Debug.Log($"[SceneMatManager] {controller.ElementId} ({controller.gameObject.name}): 材质={mat?.name ?? "null"}, 实例ID={mat?.GetInstanceID() ?? 0}, 渲染队列={mat?.renderQueue ?? 0}, 初始化={controller.IsInitialized}");
            }
        }
        Debug.Log($"[SceneMatManager] =================================");
    }
}