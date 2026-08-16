// ==================== RWJsonData.cs ====================
using UnityEngine;
using System.IO;
using System.Threading.Tasks;

public static class RWJsonData
{
    // ============================================================
    //  📦 运行时异步加载（从 Addressables）
    //  给游戏运行时使用，全部异步
    // ============================================================

    /// <summary>
    /// 异步从 Addressables 加载 JSON（运行时使用）
    /// </summary>
    public static async Task<string> LoadJson(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[RWJsonData] 文件路径为空");
            return null;
        }

        var (jsonFile, handle) = await AssetManager.LoadFromAddressablesAsync<TextAsset>(filePath);
        if (jsonFile == null)
        {
            Debug.LogError($"[RWJsonData] 未找到JSON文件: {filePath}");
            return null;
        }
        return jsonFile.text;
    }

    // ============================================================
    //  🛠️ 编辑器同步加载（从 Resources / 文件系统）
    //  只给编辑器工具使用，不依赖 Addressables
    // ============================================================

    /// <summary>
    /// 同步从 Resources 加载 JSON（仅编辑器工具使用）
    /// </summary>
    public static string LoadJsonSync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[RWJsonData] 文件路径为空");
            return null;
        }

        TextAsset jsonFile = Resources.Load<TextAsset>(filePath);
        if (jsonFile == null)
        {
            Debug.LogError($"[RWJsonData] 未找到JSON文件: {filePath}");
            return null;
        }
        return jsonFile.text;
    }

    /// <summary>
    /// 从文件系统加载 JSON（仅编辑器工具使用）
    /// </summary>
    public static string LoadJsonFromPath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[RWJsonData] 文件不存在: {filePath}");
            return null;
        }
        return File.ReadAllText(filePath);
    }

    // ============================================================
    //  🔧 通用 JSON 解析
    // ============================================================

    public static T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[RWJsonData] JSON内容为空");
            return null;
        }
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RWJsonData] JSON解析异常: {e.Message}");
            return null;
        }
    }
}
