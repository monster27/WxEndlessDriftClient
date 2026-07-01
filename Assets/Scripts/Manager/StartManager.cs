using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class StartManager : MonoBehaviour
{
    public string username = "1";
    public string password = "1";

    private void Start()
    {
        StartCoroutine(LoginCoroutine(username, password));
    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        // 构建登录请求
        var loginData = new LoginRequest
        {
            Username = username,
            Password = password
        };

        string json = JsonUtility.ToJson(loginData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        string url = "http://localhost:5000/api/auth/login";
        Debug.Log($"[StartManager] 发送登录请求: {url}");

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
                Debug.Log($"登录响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<LoginResponse>(responseText);

                    if (response != null && response.success)
                    {
                        // 设置玩家ID
                        NetServerManager.Instance.SetCurrentPlayerId(response.playerId);
                        NetServerManager.Instance.ResetInitialization();

                        Debug.Log($"[StartManager] 登录成功，设置玩家ID为: {response.playerId}");

                        // ✅ 从响应中获取场景ID
                        if (response.sceneId > 0)
                        {
                            // 确保EnvManager存在
                            if (EnvManager.Instance != null)
                            {
                                EnvManager.Instance.currentSceneId = response.sceneId;
                                Debug.Log($"[StartManager] 从服务器获取场景ID: {response.sceneId}");
                            }
                            else
                            {
                                Debug.LogWarning("[StartManager] EnvManager 不存在，延迟设置场景ID");
                                // 延迟设置，等待EnvManager初始化
                                StartCoroutine(DelayedSetSceneId(response.sceneId));
                            }
                        }

                        // 跳转到加载场景
                        LoadLoadingScene();
                    }
                    else
                    {
                        Debug.LogError($"[StartManager] 登录失败: {response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[StartManager] 解析登录响应失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[StartManager] 登录请求失败: {request.error}");
            }
        }
    }

    // ✅ 新增：延迟设置场景ID（等待EnvManager初始化）
    private IEnumerator DelayedSetSceneId(int sceneId)
    {
        int maxAttempts = 30; // 最多等待3秒
        int attempts = 0;

        while (EnvManager.Instance == null && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }

        if (EnvManager.Instance != null)
        {
            EnvManager.Instance.currentSceneId = sceneId;
            Debug.Log($"[StartManager] 延迟设置场景ID成功: {sceneId}");
        }
        else
        {
            Debug.LogWarning("[StartManager] 延迟设置场景ID失败 - EnvManager 未找到");
        }
    }

    private void LoadLoadingScene()
    {
        Debug.Log("[StartManager] 跳转到加载场景: LoadingScene");
        SceneManager.LoadScene("LoadingScene");
    }

    // ========== 数据类 ==========

    [System.Serializable]
    public class LoginRequest
    {
        public string Username;
        public string Password;
    }

    // ✅ 修改：添加 sceneId 字段
    [System.Serializable]
    public class LoginResponse
    {
        public bool success;
        public int playerId;
        public string message;
        public bool isNewUser;
        public bool autoFishingStarted;
        public string autoFishingMessage;
        public int sceneId;  // ✅ 新增
    }
}