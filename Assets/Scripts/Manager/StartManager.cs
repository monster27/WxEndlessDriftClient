using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class StartManager : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";

    [Header("登录UI")]
    public InputField usernameInput;
    public InputField passwordInput;
    public Button loginBtn;
    public Text statusText;

    private void Start()
    {
        loginBtn.onClick.AddListener(OnLoginClicked);

        // 尝试自动登录（从PlayerPrefs读取上次登录的用户）
        TryAutoLogin();
    }

    private void TryAutoLogin()
    {
        string savedUsername = PlayerPrefs.GetString("LastUsername", "");
        string savedPassword = PlayerPrefs.GetString("LastPassword", "");

        if (!string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
        {
            usernameInput.text = savedUsername;
            passwordInput.text = savedPassword;
            StartCoroutine(LoginCoroutine(savedUsername, savedPassword, true));
        }
    }

    private void OnLoginClicked()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text.Trim();

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("请输入用户名", true);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus("请输入密码", true);
            return;
        }

        ShowStatus("正在登录...", false);
        StartCoroutine(LoginCoroutine(username, password, false));
    }

    private IEnumerator LoginCoroutine(string username, string password, bool isAutoLogin)
    {
        // 调用登录接口（服务器会自动注册新用户）
        string url = "http://localhost:5000/api/auth/login";
        string jsonData = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Post(url, jsonData, "application/json"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("登录响应: " + responseText);

                // 解析响应获取玩家ID
                LoginResponse response = null;
                bool parseSuccess = false;

                try
                {
                    response = JsonUtility.FromJson<LoginResponse>(responseText);
                    parseSuccess = true;
                }
                catch (System.Exception ex)
                {
                    ShowStatus("解析响应失败: " + ex.Message, true);
                }

                if (parseSuccess && response != null && response.success)
                {
                    // 保存登录状态
                    PlayerPrefs.SetString("LastUsername", username);
                    PlayerPrefs.SetString("LastPassword", password);
                    PlayerPrefs.SetInt("PlayerId", response.playerId);
                    PlayerPrefs.Save();

                    // 设置当前玩家ID到 NetServerManager
                    if (NetServerManager.Instance != null)
                    {
                        NetServerManager.Instance.SetCurrentPlayerId(response.playerId);
                        // 重置初始化状态（用于切换账号）
                        NetServerManager.Instance.ResetInitialization();
                    }
                    Debug.Log($"[StartManager] 登录成功，设置玩家ID为: {response.playerId}");

                    ShowStatus(response.isNewUser ? "注册成功!" : "登录成功!", false);

                    // 如果是新用户，初始化基础装备和人物
                    if (response.isNewUser)
                    {
                        Debug.Log($"[StartManager] 检测到新用户，开始初始化基础装备和人物...");
                        yield return new WaitForSeconds(0.2f);
                        StartCoroutine(InitializeNewPlayer(response.playerId));
                        yield return new WaitForSeconds(0.3f);
                    }

                    // 跳转到加载场景（而不是直接跳转游戏场景）
                    yield return new WaitForSeconds(response.isNewUser ? 0.8f : 0.3f);
                    LoadLoadingScene();
                }
                else if (parseSuccess && response != null)
                {
                    ShowStatus(response.message ?? "登录失败", true);
                }
            }
            else
            {
                if (!isAutoLogin)
                {
                    ShowStatus("连接失败: " + request.error, true);
                }
                else
                {
                    ShowStatus("自动登录失败，请手动登录", true);
                }
            }
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        statusText.text = message;
        statusText.color = isError ? Color.red : Color.green;
    }

    /// <summary>
    /// 跳转到加载场景
    /// </summary>
    private void LoadLoadingScene()
    {
        Debug.Log("[StartManager] 跳转到加载场景: " + loadingSceneName);
        SceneManager.LoadScene(loadingSceneName);
    }

    /// <summary>
    /// 初始化新玩家 - 添加基础装备和人物
    /// </summary>
    private IEnumerator InitializeNewPlayer(int playerId)
    {
        Debug.Log($"[StartManager] 开始初始化新玩家: playerId={playerId}");

        // 调用服务器的初始化API
        string url = "http://localhost:5000/api/player/init";
        string jsonData = $"{{\"playerId\":{playerId}}}";

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Post(url, jsonData, "application/json"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[StartManager] 新玩家初始化响应: {responseText}");

                try
                {
                    var initResponse = JsonUtility.FromJson<InitResponse>(responseText);
                    if (initResponse != null && initResponse.success)
                    {
                        Debug.Log($"[StartManager] 新玩家初始化成功！已添加：基础钓竿、钓钩、钓线、人物3401");
                        ShowStatus("基础装备已发放！", false);
                    }
                    else
                    {
                        Debug.LogWarning($"[StartManager] 新玩家初始化失败: {initResponse?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[StartManager] 解析初始化响应失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[StartManager] 新玩家初始化请求失败: {request.error}");
            }
        }
    }

    [System.Serializable]
    private class InitResponse
    {
        public bool success;
        public string message;
    }

    [System.Serializable]
    private class LoginResponse
    {
        public bool success;
        public int playerId;
        public string message;
        public bool isNewUser;
    }
}