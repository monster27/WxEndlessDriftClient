// ==================== SingletonMono.cs ====================
using UnityEngine;

/// <summary>
/// 单例基类 - 自动创建（如果场景中没有则动态创建）
/// </summary>
/// <typeparam name="T">子类类型</typeparam>
public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] {typeof(T)} 已经在应用退出时销毁，不再返回实例");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    if (_instance == null)
                    {
                        Debug.LogWarning($"[Singleton] {typeof(T)} 没有实例，重新生成实例");
                        GameObject go = new GameObject(typeof(T).Name);
                        _instance = go.AddComponent<T>();
                        DontDestroyOnLoad(go);

                        go.name = $"{typeof(T).Name}(动态创建)";
                        Debug.Log($"[Singleton] {typeof(T)} 动态创建完成");
                    }
                    else
                    {
                        _instance.gameObject.name = $"{typeof(T).Name}(场景预制)";
                        Debug.Log($"[Singleton] {typeof(T)} 找到场景中的实例");
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[Singleton] {typeof(T)} 已有实例，销毁重复对象");
            Destroy(gameObject);
            return;
        }

        if (_instance == null || _instance == this)
        {
            _applicationIsQuitting = false;
        }

        _instance = this as T;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        if (!gameObject.name.Contains("(动态创建)") && !gameObject.name.Contains("(场景预制)"))
        {
            gameObject.name = $"{typeof(T).Name}(Awake初始化)";
        }

        Debug.Log($"[Singleton] {typeof(T)} Awake完成: {gameObject.name}");
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            Debug.Log($"[Singleton] {typeof(T)} 实例已销毁: {gameObject.name}");
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
        _instance = null;
    }
}

// ==================== SingletonMonoFromScene.cs ====================

/// <summary>
/// 单例基类 - 仅从场景中获取（不自动创建）
/// 如果场景中没有实例，Instance 返回 null
/// </summary>
/// <typeparam name="T">子类类型</typeparam>
public class SingletonMonoFromScene<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[SingletonFromScene] {typeof(T)} 已经在应用退出时销毁，不再返回实例");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    if (_instance != null)
                    {
                        _instance.gameObject.name = $"{typeof(T).Name}(场景预制)";
                        Debug.Log($"[SingletonFromScene] {typeof(T)} 从场景中找到实例");

                        // 确保已持久化
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                    else
                    {
                        Debug.LogWarning($"[SingletonFromScene] {typeof(T)} 场景中不存在实例，返回 null");
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[SingletonFromScene] {typeof(T)} 已有实例，销毁重复对象");
            Destroy(gameObject);
            return;
        }

        if (_instance == null || _instance == this)
        {
            _applicationIsQuitting = false;
        }

        _instance = this as T;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        if (!gameObject.name.Contains("(场景预制)"))
        {
            gameObject.name = $"{typeof(T).Name}(场景预制)";
        }

        Debug.Log($"[SingletonFromScene] {typeof(T)} Awake完成: {gameObject.name}");
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            Debug.Log($"[SingletonFromScene] {typeof(T)} 实例已销毁: {gameObject.name}");
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
        _instance = null;
    }
}