using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// 网络工具类
/// 提供所有网络相关的基础功能和工具方法
/// </summary>
public static class NetUtils
{
    #region 常量定义

    /// <summary>心跳间隔（秒）</summary>
    public const float HEARTBEAT_INTERVAL = 5f;

    /// <summary>最大心跳丢失次数</summary>
    public const int MAX_MISSED_HEARTBEATS = 2;

    /// <summary>网络超时时间（秒）</summary>
    public const float NETWORK_TIMEOUT = 10f;

    /// <summary>服务器地址</summary>
    public const string SERVER_HOST = "127.0.0.1";

    /// <summary>服务器端口</summary>
    public const int SERVER_PORT = 8888;

    #endregion

    #region 网络状态枚举

    /// <summary>网络连接状态</summary>
    public enum NetworkState
    {
        Disconnected,  // 未连接
        Connecting,    // 连接中
        Connected,     // 已连接
        Reconnecting   // 重连中
    }

    #endregion

    #region 数据序列化与反序列化

    /// <summary>
    /// 将对象序列化为Dictionary（用于网络传输）
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>序列化后的Dictionary</returns>
    public static Dictionary<string, object> SerializeToDict(object obj)
    {
        var dict = new Dictionary<string, object>();
        
        if (obj == null)
            return dict;

        var type = obj.GetType();
        var properties = type.GetProperties();
        
        foreach (var prop in properties)
        {
            object value = prop.GetValue(obj, null);
            if (value != null)
            {
                dict[prop.Name] = value;
            }
        }

        return dict;
    }

    /// <summary>
    /// 从Dictionary反序列化为指定类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="dict">包含数据的Dictionary</param>
    /// <returns>反序列化后的对象</returns>
    public static T DeserializeFromDict<T>(Dictionary<string, object> dict) where T : new()
    {
        T obj = new T();
        var type = typeof(T);
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            if (dict.TryGetValue(prop.Name, out object value))
            {
                try
                {
                    object convertedValue = Convert.ChangeType(value, prop.PropertyType);
                    prop.SetValue(obj, convertedValue, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetUtils] 反序列化属性失败: {prop.Name}, 错误: {ex.Message}");
                }
            }
        }

        return obj;
    }

    /// <summary>
    /// 将Dictionary序列化为JSON字符串
    /// </summary>
    public static string SerializeToJson(Dictionary<string, object> dict)
    {
        if (dict == null || dict.Count == 0)
            return "{}";

        var sb = new System.Text.StringBuilder();
        sb.Append("{");
        bool first = true;
        foreach (var kvp in dict)
        {
            if (!first)
                sb.Append(",");
            sb.Append($"\"{kvp.Key}\":");
            sb.Append(ValueToJson(kvp.Value));
            first = false;
        }
        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// 将值转换为JSON字符串
    /// </summary>
    private static string ValueToJson(object value)
    {
        if (value == null)
            return "null";
        
        if (value is string)
            return $"\"{value}\"";
        
        if (value is int || value is long || value is float || value is double)
            return value.ToString();
        
        if (value is bool)
            return value.ToString().ToLower();
        
        if (value is Dictionary<string, object>)
            return SerializeToJson((Dictionary<string, object>)value);

        // 【修复】支持 List<Dictionary<string, object>>
        if (value is List<Dictionary<string, object>>)
        {
            var list = (List<Dictionary<string, object>>)value;
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.Append(SerializeToJson(list[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }

        // 【修复】支持 List<object>
        if (value is List<object>)
        {
            var list = (List<object>)value;
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.Append(ValueToJson(list[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }
        
        // 【修复】支持 List<int>
        if (value is List<int>)
        {
            var list = (List<int>)value;
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.Append(list[i].ToString());
            }
            sb.Append("]");
            return sb.ToString();
        }
        
        return $"\"{value}\"";
    }

    /// <summary>
    /// 简单的JSON解析（支持基本类型和嵌套对象）
    /// </summary>
    public static T? ParseJson<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        if (typeof(T) == typeof(Dictionary<string, object>))
        {
            var result = new Dictionary<string, object>();
            json = json.Trim();
            
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                json = json.Substring(1, json.Length - 2);
                ParseJsonObject(json, result);
            }
            
            return (T?)(object?)result;
        }

        try
        {
            var dict = new Dictionary<string, object>();
            json = json.Trim();
            
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                json = json.Substring(1, json.Length - 2);
                ParseJsonObject(json, dict);
            }

            var obj = System.Activator.CreateInstance<T>();
            foreach (var prop in typeof(T).GetFields())
            {
                if (dict.TryGetValue(prop.Name, out object? value))
                {
                    try
                    {
                        var convertedValue = System.Convert.ChangeType(value, prop.FieldType);
                        prop.SetValue(obj, convertedValue);
                    }
                    catch
                    {
                    }
                }
            }
            
            return obj;
        }
        catch
        {
            return default;
        }
    }
    
    public static Dictionary<string, object> ParseJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var result = new Dictionary<string, object>();
        json = json.Trim();
        
        if (json.StartsWith("{") && json.EndsWith("}"))
        {
            json = json.Substring(1, json.Length - 2);
            ParseJsonObject(json, result);
        }
        
        return result;
    }

    private static void ParseJsonObject(string content, Dictionary<string, object> result)
    {
        int depth = 0;
        int start = 0;
        
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            
            if (c == '{' || c == '[')
                depth++;
            else if (c == '}' || c == ']')
                depth--;
            else if (c == ',' && depth == 0)
            {
                ParseJsonPair(content.Substring(start, i - start), result);
                start = i + 1;
            }
        }
        
        if (start < content.Length)
        {
            ParseJsonPair(content.Substring(start), result);
        }
    }

    private static void ParseJsonPair(string pair, Dictionary<string, object> result)
    {
        int colonIndex = pair.IndexOf(':');
        if (colonIndex < 0)
            return;

        string key = pair.Substring(0, colonIndex).Trim().Trim('"');
        string valueStr = pair.Substring(colonIndex + 1).Trim();

        object value = ParseJsonValue(valueStr);
        result[key] = value;
    }

    private static object ParseJsonValue(string valueStr)
    {
        valueStr = valueStr.Trim();

        if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
        {
            return valueStr.Substring(1, valueStr.Length - 2);
        }
        
        if (valueStr == "null")
            return null;
        
        if (valueStr == "true")
            return true;
        
        if (valueStr == "false")
            return false;
        
        if (valueStr.StartsWith("{") && valueStr.EndsWith("}"))
        {
            var nested = new Dictionary<string, object>();
            ParseJsonObject(valueStr.Substring(1, valueStr.Length - 2), nested);
            return nested;
        }
        
        if (valueStr.StartsWith("[") && valueStr.EndsWith("]"))
        {
            return ParseJsonArray(valueStr);
        }
        
        if (valueStr.Contains("."))
        {
            if (double.TryParse(valueStr, out double d))
                return d;
        }
        
        if (long.TryParse(valueStr, out long l))
            return l;
        
        if (int.TryParse(valueStr, out int i))
            return i;

        return valueStr;
    }

    private static List<object> ParseJsonArray(string arrayStr)
    {
        var result = new List<object>();
        arrayStr = arrayStr.Trim();
        
        if (arrayStr.StartsWith("[") && arrayStr.EndsWith("]"))
        {
            arrayStr = arrayStr.Substring(1, arrayStr.Length - 2);
        }
        
        if (string.IsNullOrEmpty(arrayStr))
            return result;

        int depth = 0;
        int start = 0;
        
        for (int i = 0; i < arrayStr.Length; i++)
        {
            char c = arrayStr[i];
            
            if (c == '{' || c == '[')
                depth++;
            else if (c == '}' || c == ']')
                depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(ParseJsonValue(arrayStr.Substring(start, i - start)));
                start = i + 1;
            }
        }
        
        if (start < arrayStr.Length)
        {
            result.Add(ParseJsonValue(arrayStr.Substring(start)));
        }

        return result;
    }

    #endregion

    #region 数据验证

    /// <summary>
    /// 验证Dictionary是否包含指定的键
    /// </summary>
    /// <param name="data">数据字典</param>
    /// <param name="keys">需要验证的键列表</param>
    /// <returns>是否包含所有指定键</returns>
    public static bool ValidateData(Dictionary<string, object> data, params string[] keys)
    {
        if (data == null)
            return false;

        foreach (string key in keys)
        {
            if (!data.ContainsKey(key))
            {
                Debug.LogWarning($"[NetUtils] 数据验证失败：缺少键 '{key}'");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 安全获取Dictionary中的int值
    /// </summary>
    public static int GetIntValue(Dictionary<string, object> data, string key, int defaultValue = 0)
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取Dictionary中的float值
    /// </summary>
    public static float GetFloatValue(Dictionary<string, object> data, string key, float defaultValue = 0f)
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            return Convert.ToSingle(value);
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取Dictionary中的string值
    /// </summary>
    public static string GetStringValue(Dictionary<string, object> data, string key, string defaultValue = "")
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            return value.ToString();
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取Dictionary中的bool值
    /// </summary>
    public static bool GetBoolValue(Dictionary<string, object> data, string key, bool defaultValue = false)
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            return Convert.ToBoolean(value);
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取Dictionary中的long值
    /// </summary>
    public static long GetLongValue(Dictionary<string, object> data, string key, long defaultValue = 0)
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            return Convert.ToInt64(value);
        }
        return defaultValue;
    }

    #endregion

    #region 时间戳工具

    /// <summary>
    /// 获取当前UTC时间戳（毫秒）
    /// </summary>
    /// <returns>时间戳</returns>
    public static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 获取当前UTC时间戳（秒）
    /// </summary>
    /// <returns>时间戳</returns>
    public static long GetCurrentTimestampSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// 将时间戳转换为DateTime
    /// </summary>
    /// <param name="timestamp">时间戳（毫秒）</param>
    /// <returns>DateTime对象</returns>
    public static DateTime TimestampToDateTime(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    }

    #endregion

    #region 网络请求构建

    /// <summary>
    /// 构建心跳数据包
    /// </summary>
    /// <returns>心跳数据包</returns>
    public static Dictionary<string, object> BuildHeartbeatData()
    {
        return new Dictionary<string, object>
        {
            { "clientTime", GetCurrentTimestamp() },
            { "type", "heartbeat" }
        };
    }

    /// <summary>
    /// 构建通用请求数据包
    /// </summary>
    /// <param name="action">请求动作</param>
    /// <param name="data">请求数据</param>
    /// <returns>请求数据包</returns>
    public static Dictionary<string, object> BuildRequestData(string action, Dictionary<string, object> data = null)
    {
        var request = new Dictionary<string, object>
        {
            { "action", action },
            { "timestamp", GetCurrentTimestamp() }
        };

        if (data != null)
        {
            foreach (var kvp in data)
            {
                request[kvp.Key] = kvp.Value;
            }
        }

        return request;
    }

    #endregion

    #region 日志工具

    /// <summary>
    /// 打印网络请求日志
    /// </summary>
    public static void LogRequest(string action, Dictionary<string, object> data = null)
    {
        string dataStr = data != null ? string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "无数据";
        Debug.Log($"<color=blue>[NetUtils] 发送请求: {action}, 数据: {dataStr}</color>");
    }

    /// <summary>
    /// 打印网络响应日志
    /// </summary>
    public static void LogResponse(string action, Dictionary<string, object> data = null)
    {
        string dataStr = data != null ? string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "无数据";
        Debug.Log($"<color=green>[NetUtils] 收到响应: {action}, 数据: {dataStr}</color>");
    }

    /// <summary>
    /// 打印网络错误日志
    /// </summary>
    public static void LogError(string message, Exception ex = null)
    {
        string errorStr = ex != null ? $"{message}, 异常: {ex.Message}" : message;
        Debug.LogError($"<color=red>[NetUtils] 网络错误: {errorStr}</color>");
    }

    #endregion

    #region 连接状态判断

    /// <summary>
    /// 判断是否网络超时
    /// </summary>
    /// <param name="lastHeartbeatTime">上次心跳时间</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <returns>是否超时</returns>
    public static bool IsTimeout(long lastHeartbeatTime, float timeoutSeconds = NETWORK_TIMEOUT)
    {
        long currentTime = GetCurrentTimestamp();
        return (currentTime - lastHeartbeatTime) > (long)(timeoutSeconds * 1000);
    }

    #endregion
}