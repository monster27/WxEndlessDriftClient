using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 服务器测试界面 - 用于调试和测试服务器接口
/// </summary>
public class ServerTestView : MonoBehaviour
{
    public Button btnGetPlayerInfo;
    public Button btnGetNextFishingTime;
    public Button btnStartAutoFishing;
    public Button btnStopAutoFishing;
    public Text outputText;

    private string serverUrl = "http://localhost:5000";
    private UnityEngine.Networking.UnityWebRequest request;

    void Start()
    {
        if (btnGetPlayerInfo != null)
            btnGetPlayerInfo.onClick.AddListener(OnClickGetPlayerInfo);

        if (btnGetNextFishingTime != null)
            btnGetNextFishingTime.onClick.AddListener(OnClickGetNextFishingTime);

        if (btnStartAutoFishing != null)
            btnStartAutoFishing.onClick.AddListener(OnClickStartAutoFishing);

        if (btnStopAutoFishing != null)
            btnStopAutoFishing.onClick.AddListener(OnClickStopAutoFishing);

        DontDestroyOnLoad(gameObject);

        UpdateOutput("服务器测试界面已就绪\n");
    }

    private void UpdateOutput(string text)
    {
        if (outputText != null)
        {
            outputText.text = text;
        }
        Debug.Log(text);
    }

    /// <summary>
    /// 获取玩家所有信息
    /// </summary>
    private void OnClickGetPlayerInfo()
    {
        Debug.Log("[ServerTestView] OnClickGetPlayerInfo - 点击获取玩家信息");
        StartCoroutine(GetPlayerInfoCoroutine());
    }

    private System.Collections.IEnumerator GetPlayerInfoCoroutine()
    {
        UpdateOutput("正在获取玩家信息...\n");

        // 获取玩家数据
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[玩家数据] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取玩家数据失败: {request.error}\n");
            }
        }

        // 获取玩家装备
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/equipment/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[玩家装备] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取玩家装备失败: {request.error}\n");
            }
        }

        // 获取玩家人物
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/character/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[玩家人物] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取玩家人物失败: {request.error}\n");
            }
        }

        // 获取玩家金币
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/gold/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[玩家金币] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取玩家金币失败: {request.error}\n");
            }
        }

        // 获取玩家背包
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/inventory/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[玩家背包] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取玩家背包失败: {request.error}\n");
            }
        }

        // 获取鱼篓
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/fish-inventory/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[鱼篓] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取鱼篓失败: {request.error}\n");
            }
        }

        // 获取鱼篓容量
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/fish-bag-capacity/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[鱼篓容量] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取鱼篓容量失败: {request.error}\n");
            }
        }

        UpdateOutput("\n[完成] 玩家信息获取完毕\n");
    }

    /// <summary>
    /// 获取下次钓鱼剩余时间
    /// </summary>
    private void OnClickGetNextFishingTime()
    {
        Debug.Log("[ServerTestView] OnClickGetNextFishingTime - 点击获取下次钓鱼时间");
        StartCoroutine(GetNextFishingTimeCoroutine());
    }

    private System.Collections.IEnumerator GetNextFishingTimeCoroutine()
    {
        UpdateOutput("正在获取钓鱼状态...\n");

        // 获取钓鱼状态
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/fishing/status?playerId=1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[钓鱼状态] {json}\n");

                // 尝试解析 JSON 获取下次钓鱼时间
                try
                {
                    // 简单的 JSON 解析（避免依赖第三方库）
                    var data = UnityEngine.JsonUtility.FromJson<FishingStatusResponse>(json);
                    if (data != null)
                    {
                        UpdateOutput($"[解析结果]\n");
                        UpdateOutput($"  - 是否在自动钓鱼: {data.isAutoFishing}\n");
                        UpdateOutput($"  - 金币: {data.gold}\n");
                        UpdateOutput($"  - 等级: {data.level}\n");
                        UpdateOutput($"  - 经验: {data.exp}\n");
                        UpdateOutput($"  - 耐久度: {data.durability}\n");
                        UpdateOutput($"  - 今日钓鱼数: {data.todayFishCount}\n");
                        UpdateOutput($"  - 连击数: {data.comboCount}\n");
                    }
                }
                catch (System.Exception ex)
                {
                    UpdateOutput($"[解析错误] {ex.Message}\n");
                }
            }
            else
            {
                UpdateOutput($"[错误] 获取钓鱼状态失败: {request.error}\n");
            }
        }

        // 获取连续钓鱼模式状态
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/game/continuous-mode/status"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[连续模式状态] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取连续模式状态失败: {request.error}\n");
            }
        }

        // 获取自动钓鱼状态
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/fishing/auto/status?playerId=1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                UpdateOutput($"[自动钓鱼状态] {json}\n");
            }
            else
            {
                UpdateOutput($"[错误] 获取自动钓鱼状态失败: {request.error}\n");
            }
        }

        UpdateOutput("\n[完成] 钓鱼状态获取完毕\n");
    }

    /// <summary>
    /// 开始自动钓鱼
    /// </summary>
    private void OnClickStartAutoFishing()
    {
        Debug.Log("[ServerTestView] OnClickStartAutoFishing - 点击开始自动钓鱼");
        StartCoroutine(StartAutoFishingCoroutine());
    }

    private System.Collections.IEnumerator StartAutoFishingCoroutine()
    {
        UpdateOutput("正在开始自动钓鱼...\n");

        string jsonData = "{\"playerId\":1,\"sceneId\":1,\"baitId\":2501,\"intervalMs\":3000}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (var request = new UnityEngine.Networking.UnityWebRequest(serverUrl + "/api/fishing/auto/start", "POST"))
        {
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                UpdateOutput($"[自动钓鱼开始] {response}\n");
            }
            else
            {
                UpdateOutput($"[错误] 开始自动钓鱼失败: {request.error}\n");
            }
        }
    }

    /// <summary>
    /// 停止自动钓鱼
    /// </summary>
    private void OnClickStopAutoFishing()
    {
        Debug.Log("[ServerTestView] OnClickStopAutoFishing - 点击停止自动钓鱼");
        StartCoroutine(StopAutoFishingCoroutine());
    }

    private System.Collections.IEnumerator StopAutoFishingCoroutine()
    {
        UpdateOutput("正在停止自动钓鱼...\n");

        string jsonData = "{\"playerId\":1}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (var request = new UnityEngine.Networking.UnityWebRequest(serverUrl + "/api/fishing/auto/stop", "POST"))
        {
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                UpdateOutput($"[自动钓鱼停止] {response}\n");
            }
            else
            {
                UpdateOutput($"[错误] 停止自动钓鱼失败: {request.error}\n");
            }
        }
    }

    [System.Serializable]
    private class FishingStatusResponse
    {
        public bool success;
        public int level;
        public int gold;
        public int diamonds;
        public int exp;
        public int durability;
        public int todayFishCount;
        public int comboCount;
        public bool isAutoFishing;
        public int fishingMode; // 钓鱼模式（0=Normal, 1=Continuous）
    }
}
