// ============================================
// 文件: LoadingManager.cs
// 功能: 加载场景管理器 - 显示进度并跳转
// ============================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Logger = Utils.Logger;

public class LoadingManager : MonoBehaviour
{
    [Header("UI 组件")]
    public Slider progressSlider;
    public Text progressText;
    public Text statusText;
    public Text tipText;

    [Header("设置")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private float minLoadTime = 1.5f;

    [Header("加载提示")]
    [SerializeField]
    private string[] loadingTips = new string[]
    {
        "鱼儿正在上钩...",
        "准备你的钓竿...",
        "选择最好的鱼饵...",
        "耐心等待大鱼...",
        "享受钓鱼的乐趣..."
    };

    private bool _isComplete = false;
    private float _startTime;

    private void Start()
    {
        _startTime = Time.time;

        if (tipText != null && loadingTips.Length > 0)
        {
            tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
        }

        // 【关键修复】使用局部变量，避免多次访问 Instance
        NetServerManager netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            // 先调用 Init() 注册事件处理器
            netManager.Init();
            Logger.Log("[LoadingManager] NetServerManager.Init() 已调用");

            // 然后再订阅事件
            netManager.OnProgressUpdated += OnProgressUpdated;
            netManager.OnInitializationComplete += OnInitializationComplete;
            netManager.OnInitializationFailed += OnInitializationFailed;

            // 如果已经初始化完成，直接处理
            if (netManager.IsInitialized)
            {
                OnInitializationComplete();
            }
            else
            {
                // 开始初始化
                Logger.Log("[LoadingManager] 开始网络数据初始化...");
                netManager.StartInitialization();
            }
        }
        else
        {
            Logger.LogError("[LoadingManager] NetServerManager 实例不存在！");
            StartCoroutine(DelayedLoad());
        }
    }

    private void OnProgressUpdated(float progress, string stepName)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{(progress * 100):F0}%";
        }

        if (statusText != null)
        {
            statusText.text = stepName;
        }

        Logger.Log($"[LoadingManager] 加载进度: {progress:P0} - {stepName}");
    }

    private void OnInitializationComplete()
    {
        _isComplete = true;
        Logger.Log("[LoadingManager] 数据加载完成，准备跳转");

        OnProgressUpdated(1f, "加载完成！");

        float elapsed = Time.time - _startTime;
        float delay = Mathf.Max(0, minLoadTime - elapsed);

        StartCoroutine(DelayedLoad(delay));
    }

    private void OnInitializationFailed(string errorMessage)
    {
        Logger.LogError($"[LoadingManager] 数据加载失败: {errorMessage}");

        if (statusText != null)
        {
            statusText.text = "加载失败：" + errorMessage;
            statusText.color = Color.red;
        }

        StartCoroutine(RetryAfterDelay());
    }

    private IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSeconds(3f);

        NetServerManager netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.ResetInitialization();
            netManager.StartInitialization();
        }
        else
        {
            SceneManager.LoadScene("StartScene");
        }
    }

    private IEnumerator DelayedLoad(float delay = 0f)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!_isComplete)
        {
            Logger.LogWarning("[LoadingManager] 强制跳转（超时）");
        }

        Logger.Log($"[LoadingManager] 跳转到游戏场景: {gameSceneName}");

        NetServerManager netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.OnProgressUpdated -= OnProgressUpdated;
            netManager.OnInitializationComplete -= OnInitializationComplete;
            netManager.OnInitializationFailed -= OnInitializationFailed;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OnDestroy()
    {
        NetServerManager netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.OnProgressUpdated -= OnProgressUpdated;
            netManager.OnInitializationComplete -= OnInitializationComplete;
            netManager.OnInitializationFailed -= OnInitializationFailed;
        }
    }
}