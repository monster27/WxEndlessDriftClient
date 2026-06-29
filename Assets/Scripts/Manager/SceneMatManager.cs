using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 场景材质管理器 - 控制游戏场景图片内容
/// 包含所有数据类定义
/// </summary>
public class SceneMatManager : MonoBehaviour
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

    public enum RenderQueueLevel
    {
        Background = 0,
        Environment = 1,
        Character = 2,
        Foreground = 3,
        UI = 4
    }

    // ========== 数据类定义 ==========
    [System.Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Vector3 scale;

        public TransformData()
        {
            position = Vector3.zero;
            scale = Vector3.one;
        }

        public TransformData(Vector3 pos, Vector3 scl)
        {
            position = pos;
            scale = scl;
        }
    }

    [System.Serializable]
    public class SceneElementData
    {
        public string id;
        public string name;
        public string imagePath;
        public Vector3 position;
        public Vector3 scale;
        public string renderLevel;
        public bool isFlipped;
        public bool isLockFlip;
        public string sceneId;

        public SceneElementData()
        {
            id = "";
            name = "";
            imagePath = "";
            position = Vector3.zero;
            scale = Vector3.one;
            renderLevel = "Environment";
            isFlipped = false;
            isLockFlip = false;
            sceneId = "";
        }
    }

    [System.Serializable]
    public class SceneData
    {
        public string sceneId;
        public string sceneName;
        public List<SceneElementData> elements;

        public SceneData()
        {
            sceneId = "";
            sceneName = "";
            elements = new List<SceneElementData>();
        }
    }

    [System.Serializable]
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
    [SerializeField] private string currentSceneId = "1";
    [SerializeField] private bool loadOnStart = true;

    [Header("=== 渲染队列配置 ===")]
    [SerializeField] private int backgroundQueue = 1000;
    [SerializeField] private int environmentQueue = 2000;
    [SerializeField] private int characterQueue = 3000;
    [SerializeField] private int foregroundQueue = 4000;
    [SerializeField] private int uiQueue = 5000;

    [Header("=== 资源路径 ===")]
    [SerializeField] private string sceneDataPath = "JsonData/SceneTransData/mainTransData";
    [SerializeField] private string imageResourcesPath = "GameScene/";

    [Header("=== 控制器列表 ===")]
    [SerializeField] private List<SceneMatCtrl> sceneControllers = new List<SceneMatCtrl>();

    // ========== 私有变量 ==========
    private SceneDataWrapper sceneDataWrapper;
    private Dictionary<ElementType, SceneMatCtrl> controllerDict = new Dictionary<ElementType, SceneMatCtrl>();
    private bool isDataLoaded = false;

    // ========== 公共属性 ==========
    public string CurrentSceneId => currentSceneId;
    public List<SceneMatCtrl> SceneControllers => sceneControllers;

    // ========== 渲染队列映射 ==========
    private Dictionary<RenderQueueLevel, int> renderQueueMap;

    // ========== Unity生命周期 ==========
    private void Awake()
    {
        InitializeRenderQueueMap();
    }

    private void Start()
    {
        if (loadOnStart)
        {
            LoadSceneData();
            ApplySceneData(currentSceneId);
        }
    }

    private void InitializeRenderQueueMap()
    {
        renderQueueMap = new Dictionary<RenderQueueLevel, int>
        {
            { RenderQueueLevel.Background, backgroundQueue },
            { RenderQueueLevel.Environment, environmentQueue },
            { RenderQueueLevel.Character, characterQueue },
            { RenderQueueLevel.Foreground, foregroundQueue },
            { RenderQueueLevel.UI, uiQueue }
        };
    }

    // ========== 控制器管理 ==========

    /// <summary>
    /// 注册控制器
    /// </summary>
    public void RegisterController(SceneMatCtrl controller)
    {
        if (controller == null) return;

        if (!sceneControllers.Contains(controller))
        {
            sceneControllers.Add(controller);
        }

        controllerDict[controller.ElementId] = controller;
    }

    /// <summary>
    /// 注销控制器
    /// </summary>
    public void UnregisterController(SceneMatCtrl controller)
    {
        if (controller == null) return;

        sceneControllers.Remove(controller);
        if (controllerDict.ContainsKey(controller.ElementId))
        {
            controllerDict.Remove(controller.ElementId);
        }
    }

    /// <summary>
    /// 通过元素类型获取控制器
    /// </summary>
    public SceneMatCtrl GetController(ElementType elementType)
    {
        if (controllerDict.TryGetValue(elementType, out SceneMatCtrl controller))
        {
            return controller;
        }

        // 如果在字典中找不到，从列表中查找
        return sceneControllers.Find(c => c.ElementId == elementType);
    }

    /// <summary>
    /// 通过字符串ID获取控制器
    /// </summary>
    public SceneMatCtrl GetController(string elementId)
    {
        if (Enum.TryParse<ElementType>(elementId, out ElementType type))
        {
            return GetController(type);
        }
        return null;
    }

    // ========== 场景数据加载 ==========

    /// <summary>
    /// 加载场景数据（从Resources）
    /// </summary>
    public void LoadSceneData()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>(sceneDataPath);
            if (jsonFile == null)
            {
                Debug.LogError($"[SceneMatManager] 无法加载场景数据文件: {sceneDataPath}");
                return;
            }

            sceneDataWrapper = JsonUtility.FromJson<SceneDataWrapper>(jsonFile.text);
            if (sceneDataWrapper == null || sceneDataWrapper.scenes == null)
            {
                Debug.LogError("[SceneMatManager] 场景数据解析失败");
                return;
            }

            isDataLoaded = true;
            Debug.Log($"[SceneMatManager] 加载场景数据完成，共 {sceneDataWrapper.scenes.Count} 个场景");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneMatManager] 加载场景数据异常: {e.Message}");
        }
    }

    /// <summary>
    /// 加载场景数据（从文件路径）
    /// </summary>
    public void LoadSceneDataFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SceneMatManager] 文件不存在: {filePath}");
                return;
            }

            string json = File.ReadAllText(filePath);
            sceneDataWrapper = JsonUtility.FromJson<SceneDataWrapper>(json);
            if (sceneDataWrapper == null || sceneDataWrapper.scenes == null)
            {
                Debug.LogError("[SceneMatManager] 场景数据解析失败");
                return;
            }

            isDataLoaded = true;
            Debug.Log($"[SceneMatManager] 从文件加载场景数据完成，共 {sceneDataWrapper.scenes.Count} 个场景");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneMatManager] 加载场景数据异常: {e.Message}");
        }
    }

    /// <summary>
    /// 获取场景数据
    /// </summary>
    public SceneData GetSceneData(string sceneId)
    {
        if (!isDataLoaded || sceneDataWrapper == null) return null;

        return sceneDataWrapper.scenes.Find(s => s.sceneId == sceneId);
    }

    /// <summary>
    /// 获取所有场景数据
    /// </summary>
    public List<SceneData> GetAllSceneData()
    {
        return sceneDataWrapper?.scenes;
    }

    // ========== 应用场景数据 ==========

    /// <summary>
    /// 应用场景数据到所有控制器
    /// </summary>
    public void ApplySceneData(string sceneId)
    {
        if (!isDataLoaded)
        {
            LoadSceneData();
            if (!isDataLoaded) return;
        }

        SceneData sceneData = GetSceneData(sceneId);
        if (sceneData == null)
        {
            Debug.LogWarning($"[SceneMatManager] 未找到场景数据: {sceneId}");
            return;
        }

        currentSceneId = sceneId;

        // 应用每个元素的数据
        foreach (var elementData in sceneData.elements)
        {
            SceneMatCtrl controller = GetController(elementData.id);
            if (controller == null)
            {
                Debug.LogWarning($"[SceneMatManager] 未找到控制器: {elementData.id}");
                continue;
            }

            ApplyElementData(controller, elementData);
        }

        Debug.Log($"[SceneMatManager] 应用场景数据完成: {sceneId}");
    }

    private void ApplyElementData(SceneMatCtrl controller, SceneElementData data)
    {
        if (controller == null || data == null) return;

        // 设置纹理
        if (!string.IsNullOrEmpty(data.imagePath))
        {
            string fullPath = imageResourcesPath + data.imagePath;
            controller.SetMainTextureByPath(fullPath);
        }

        // 设置位置和大小
        controller.SetTransformData(data.position, data.scale);

        // 设置渲染层级
        if (!string.IsNullOrEmpty(data.renderLevel))
        {
            if (Enum.TryParse<RenderQueueLevel>(data.renderLevel, out RenderQueueLevel level))
            {
                controller.SetRenderQueue(level);
            }
        }

        // 设置镜像
        controller.SetFlip(data.isFlipped);
        controller.SetLockFlip(data.isLockFlip);

        // 设置场景ID
        controller.SetSceneId(currentSceneId);
    }

    // ========== 渲染队列控制 ==========

    /// <summary>
    /// 设置所有控制器的渲染队列层级
    /// </summary>
    public void SetAllRenderQueue(RenderQueueLevel level)
    {
        int queueValue = GetRenderQueueValue(level);
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetRenderQueue(level);
            }
        }
    }

    /// <summary>
    /// 设置特定元素的渲染队列
    /// </summary>
    public void SetElementRenderQueue(ElementType elementType, RenderQueueLevel level)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetRenderQueue(level);
        }
    }

    /// <summary>
    /// 获取渲染队列值
    /// </summary>
    public int GetRenderQueueValue(RenderQueueLevel level)
    {
        if (renderQueueMap.TryGetValue(level, out int value))
        {
            return value;
        }
        return 2000;
    }

    /// <summary>
    /// 更新渲染队列映射
    /// </summary>
    public void UpdateRenderQueueMap(RenderQueueLevel level, int value)
    {
        renderQueueMap[level] = value;
    }

    // ========== 镜像控制 ==========

    /// <summary>
    /// 设置所有控制器的镜像状态（受锁定影响）
    /// </summary>
    public void SetAllFlip(bool flipped)
    {
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetFlip(flipped);
            }
        }
    }

    /// <summary>
    /// 设置特定元素的镜像状态
    /// </summary>
    public void SetElementFlip(ElementType elementType, bool flipped)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetFlip(flipped);
        }
    }

    /// <summary>
    /// 切换特定元素的镜像状态
    /// </summary>
    public void ToggleElementFlip(ElementType elementType)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.ToggleFlip();
        }
    }

    // ========== 纹理切换功能 ==========

    /// <summary>
    /// 切换所有控制器的主纹理（简便切换带渐变）
    /// </summary>
    public void SetAllMainTexture(Texture2D texture, float duration = 0.5f)
    {
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetMainTextureSmooth(texture, duration);
            }
        }
    }

    /// <summary>
    /// 切换特定元素的主纹理
    /// </summary>
    public void SetElementMainTexture(ElementType elementType, Texture2D texture, bool smooth = false, float duration = 0.5f)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            if (smooth)
            {
                controller.SetMainTextureSmooth(texture, duration);
            }
            else
            {
                controller.SetMainTexture(texture);
            }
        }
    }

    /// <summary>
    /// 通过路径切换纹理
    /// </summary>
    public void SetElementTextureByPath(ElementType elementType, string path, bool smooth = false, float duration = 0.5f)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture != null)
        {
            SetElementMainTexture(elementType, texture, smooth, duration);
        }
        else
        {
            Debug.LogError($"[SceneMatManager] 无法加载纹理: {path}");
        }
    }

    // ========== 闪烁控制 ==========

    /// <summary>
    /// 设置特定元素的闪烁状态
    /// </summary>
    public void SetElementBlink(ElementType elementType, bool enabled)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetBlinkEnabled(enabled);
        }
    }

    /// <summary>
    /// 设置特定元素的闪烁颜色
    /// </summary>
    public void SetElementBlinkColor(ElementType elementType, Color color)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetBlinkColor(color);
        }
    }

    /// <summary>
    /// 设置特定元素的闪烁图片
    /// </summary>
    public void SetElementBlinkTexture(ElementType elementType, Texture2D texture)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetBlinkTexture(texture);
        }
    }

    ///// <summary>
    ///// 让特定元素闪烁一次
    ///// </summary>
    //public void BlinkElementOnce(ElementType elementType, Color color, float duration = 0.5f, float interval = 0.5f)
    //{
    //    SceneMatCtrl controller = GetController(elementType);
    //    if (controller != null)
    //    {
    //        controller.BlinkOnce(color, duration, interval);
    //    }
    //}

    /// <summary>
    /// 设置特定元素的闪烁间隔
    /// </summary>
    public void SetElementBlinkInterval(ElementType elementType, float interval)
    {
        SceneMatCtrl controller = GetController(elementType);
        if (controller != null)
        {
            controller.SetBlinkInterval(interval);
        }
    }

    // ========== 工具方法 ==========

    /// <summary>
    /// 获取所有控制器
    /// </summary>
    public List<SceneMatCtrl> GetAllControllers()
    {
        return sceneControllers;
    }

    /// <summary>
    /// 刷新所有控制器
    /// </summary>
    public void RefreshAllControllers()
    {
        foreach (var controller in sceneControllers)
        {
            if (controller != null)
            {
                controller.SetSceneId(currentSceneId);
            }
        }
    }

    /// <summary>
    /// 查找场景中的所有控制器并注册
    /// </summary>
    public void FindAndRegisterAllControllers()
    {
        SceneMatCtrl[] foundControllers = FindObjectsOfType<SceneMatCtrl>();
        foreach (var controller in foundControllers)
        {
            RegisterController(controller);
        }
        Debug.Log($"[SceneMatManager] 找到并注册了 {foundControllers.Length} 个控制器");
    }

    /// <summary>
    /// 保存场景数据到JSON字符串
    /// </summary>
    public string SaveSceneDataToJson()
    {
        if (sceneDataWrapper == null)
        {
            sceneDataWrapper = new SceneDataWrapper();
        }
        return JsonUtility.ToJson(sceneDataWrapper, true);
    }

    /// <summary>
    /// 保存场景数据到文件
    /// </summary>
    public void SaveSceneDataToFile(string filePath)
    {
        try
        {
            string json = SaveSceneDataToJson();
            File.WriteAllText(filePath, json);
            Debug.Log($"[SceneMatManager] 数据已保存到: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneMatManager] 保存数据失败: {e.Message}");
        }
    }
}