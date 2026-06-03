// ==================== RWJsonData.cs ====================
using UnityEngine;
using System.IO;

public static class RWJsonData
{
    public static string LoadJsonFromResources(string filePath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(filePath);
        if (jsonFile == null) { Debug.LogError($"未找到JSON文件: {filePath}"); return null; }
        return jsonFile.text;
    }

    public static string LoadJsonFromPath(string filePath)
    {
        if (!File.Exists(filePath)) { Debug.LogError($"文件不存在: {filePath}"); return null; }
        return File.ReadAllText(filePath);
    }

    public static T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) { Debug.LogError("JSON内容为空"); return null; }
        try { return JsonUtility.FromJson<T>(json); }
        catch (System.Exception e) { Debug.LogError($"JSON解析异常: {e.Message}"); return null; }
    }
}