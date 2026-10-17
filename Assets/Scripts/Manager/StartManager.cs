using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public class StartManager : MonoBehaviour
{
    public string username = "1";
    public string password = "1";
    public InputField usernameInputField;
    public InputField passwordInputField;
    public Button localModeButton;
    public Text modeText;
    public Button loginButton;
    public Image testImage;          // 在 Inspector 中拖入一个 Image 组件

    // ✅ 场景 Key 与 Addressables 里的 Address 完全一致（完整路径）
    private const string LoadingSceneKey = "Assets/Addressables/Scenes/LoadingScene.unity";

    private AsyncOperationHandle<Sprite> _imageHandle;

    private void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_imageHandle);
    }

    private void Start()
    {
        LoadTestImage();

        UpdateModeButton();

        if (localModeButton != null)
        {
            localModeButton.onClick.AddListener(ToggleLocalMode);
        }

        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClick);
        }

        if (usernameInputField != null)
        {
            usernameInputField.text = username;
            usernameInputField.onValueChanged.AddListener((value) => { username = value; });
        }

        if (passwordInputField != null)
        {
            passwordInputField.text = password;
            passwordInputField.onValueChanged.AddListener((value) => { password = value; });
        }

#if UNITY_EDITOR
        bool currentMode = ServerUrls.IsLocalMode;
        if (!currentMode)
        {
            ServerUrls.SetLocalMode(!currentMode);
            UpdateModeButton();
        }
#endif
    }

    private void OnLoginButtonClick()
    {
        Z_Logger.Log($"[StartManager] 点击登录按钮，当前模式: {(ServerUrls.IsLocalMode ? "本地" : "远程")}");
        StartCoroutine(LoginCoroutine(username, password));
    }

    private void ToggleLocalMode()
    {
        bool currentMode = ServerUrls.IsLocalMode;
        ServerUrls.SetLocalMode(!currentMode);
        UpdateModeButton();
    }

    private void UpdateModeButton()
    {
        if (modeText != null)
        {
            modeText.text = ServerUrls.IsLocalMode ? "本地模式" : "远程模式";
        }

        if (localModeButton != null)
        {
            var colors = localModeButton.colors;
            if (ServerUrls.IsLocalMode)
            {
                colors.normalColor = Color.green;
                colors.highlightedColor = Color.green;
            }
            else
            {
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.grey;
            }
            localModeButton.colors = colors;
        }
    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        var loginData = new LoginRequest
        {
            Username = username,
            Password = password
        };

        string json = JsonUtility.ToJson(loginData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        string url = ServerUrls.GetFullUrl(ServerUrls.Auth.Login);
        Z_Logger.Log($"[StartManager] 发送登录请求: {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Z_Logger.Log($"登录响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<LoginResponse>(responseText);

                    if (response != null && response.success)
                    {
                        NetServerManager.Instance.SetCurrentPlayerId(response.playerId);
                        NetServerManager.Instance.ResetInitialization();

                        Z_Logger.Log($"[StartManager] 登录成功，设置玩家ID为: {response.playerId}");

                        if (response.sceneId > 0)
                        {
                            if (EnvManager.Instance != null)
                            {
                                EnvManager.Instance.currentSceneId = response.sceneId;
                                Z_Logger.Log($"[StartManager] 从服务器获取场景ID: {response.sceneId}");
                            }
                            else
                            {
                                Z_Logger.LogWarning("[StartManager] EnvManager 不存在，延迟设置场景ID");
                                StartCoroutine(DelayedSetSceneId(response.sceneId));
                            }
                        }

                        LoadLoadingScene();
                    }
                    else
                    {
                        Z_Logger.LogError($"[StartManager] 登录失败: {response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Z_Logger.LogError($"[StartManager] 解析登录响应失败: {ex.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[StartManager] 登录请求失败: {request.error}");
            }
        }
    }

    private IEnumerator DelayedSetSceneId(int sceneId)
    {
        int maxAttempts = 30;
        int attempts = 0;

        while (EnvManager.Instance == null && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }

        if (EnvManager.Instance != null)
        {
            EnvManager.Instance.currentSceneId = sceneId;
            Z_Logger.Log($"[StartManager] 延迟设置场景ID成功: {sceneId}");
        }
        else
        {
            Z_Logger.LogWarning("[StartManager] 延迟设置场景ID失败 - EnvManager 未找到");
        }
    }

    // ========== 使用 Addressables 跳转 LoadingScene ==========

    private void LoadLoadingScene()
    {
        Z_Logger.Log($"[StartManager] 使用 Addressables 跳转到加载场景: {LoadingSceneKey}");
        StartCoroutine(LoadSceneByAddressables(LoadingSceneKey));
    }

    private IEnumerator LoadSceneByAddressables(string sceneKey)
    {
        var handle = Addressables.LoadSceneAsync(sceneKey, LoadSceneMode.Single);

        while (!handle.IsDone)
        {
            yield return null;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Z_Logger.Log($"[StartManager] ✅ 场景 '{sceneKey}' 加载成功");
        }
        else
        {
            string err = handle.OperationException != null
                ? handle.OperationException.Message
                : "未知错误";
            Z_Logger.LogError($"[StartManager] ❌ 场景 '{sceneKey}' 加载失败: {err}");
        }
    }

    /// <summary>
    /// ✅ 加载测试图片，验证 Addressables 是否正常工作
    /// </summary>
    private async void LoadTestImage()
    {
        Z_Logger.Log("[StartManager] ⏳ 开始加载测试图片...");

        // ✅ 使用带文件夹前缀的 key，让 AssetManager 规范化后能得到
        //    Assets/Addressables/UI/DefaultIcon.png
        var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>("UI/DefaultIcon");
        _imageHandle = handle;

        if (sprite != null && testImage != null)
        {
            testImage.sprite = sprite;
            testImage.color = Color.white;
            Z_Logger.Log("[StartManager] ✅ 测试图片加载成功: UI/DefaultIcon");
        }
        else
        {
            Z_Logger.LogWarning("[StartManager] ⚠️ 测试图片加载失败，使用默认颜色");
            if (testImage != null)
            {
                testImage.color = Color.gray;
            }
        }
    }

    [System.Serializable]
    public class LoginRequest
    {
        public string Username;
        public string Password;
    }

    [System.Serializable]
    public class LoginResponse
    {
        public bool success;
        public int playerId;
        public string message;
        public bool isNewUser;
        public bool autoFishingStarted;
        public string autoFishingMessage;
        public int sceneId;
    }
}
