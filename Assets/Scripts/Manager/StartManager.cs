using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class StartManager : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    
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
                    
                    ShowStatus(response.isNewUser ? "注册成功!" : "登录成功!", false);
                    
                    // 延迟加载游戏场景
                    yield return new WaitForSeconds(0.5f);
                    LoadGameScene();
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

    public void LoadGameScene()
    {
        Debug.Log("加载游戏场景: " + gameSceneName);
        SceneManager.LoadScene(gameSceneName);
    }

    [System.Serializable]
    private class LoginResponse
    {
        public bool success;
        public int playerId;
        public string message;
        public bool isNewUser;  // 是否为新注册用户
    }
}