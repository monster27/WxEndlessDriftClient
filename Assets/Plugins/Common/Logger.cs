using UnityEngine;

public static class Z_Logger
{
    // Unity 标签常量
    public const string UNITY_TAG = "[UnityLog]";

    private static string GetTimestamp()
    {
        return System.DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]");
    }

    private static string FormatMessage(string message)
    {
        return $"{UNITY_TAG}{GetTimestamp()} {message}";
    }

    // ==================== Log ====================

    public static void Log(string message)
    {
        Debug.Log(FormatMessage(message));  // ✅ 直接调用 Debug.Log
    }

    public static void Log(string format, params object[] args)
    {
        Debug.Log(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.Log
    }

    // ==================== LogFormat ====================

    public static void LogFormat(string format, params object[] args)
    {
        Debug.Log(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.Log
    }

    // ==================== LogWarning ====================

    public static void LogWarning(string message)
    {
        Debug.LogWarning(FormatMessage(message));  // ✅ 直接调用 Debug.LogWarning
    }

    public static void LogWarning(string format, params object[] args)
    {
        Debug.LogWarning(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.LogWarning
    }

    public static void LogWarningFormat(string format, params object[] args)
    {
        Debug.LogWarning(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.LogWarning
    }

    // ==================== LogError ====================

    public static void LogError(string message)
    {
        Debug.LogError(FormatMessage(message));  // ✅ 直接调用 Debug.LogError
    }

    public static void LogError(string format, params object[] args)
    {
        Debug.LogError(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.LogError
    }

    public static void LogError(System.Exception ex)
    {
        Debug.LogError(FormatMessage($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));  // ✅ 直接调用 Debug.LogError
    }

    public static void LogErrorFormat(string format, params object[] args)
    {
        Debug.LogError(FormatMessage(string.Format(format, args)));  // ✅ 直接调用 Debug.LogError
    }

    // ==================== LogColor ====================

    public static void LogColor(string message, string color)
    {
        Debug.Log(FormatMessage($"<color={color}>{message}</color>"));  // ✅ 直接调用 Debug.Log
    }

    public static void LogColor(string format, string color, params object[] args)
    {
        Debug.Log(FormatMessage($"<color={color}>{string.Format(format, args)}</color>"));  // ✅ 直接调用 Debug.Log
    }

    // ==================== LogColorFormat ====================

    public static void LogColorFormat(string color, string format, params object[] args)
    {
        Debug.Log(FormatMessage($"<color={color}>{string.Format(format, args)}</color>"));  // ✅ 直接调用 Debug.Log
    }

    // ==================== LogException ====================

    public static void LogException(System.Exception ex)
    {
        Debug.LogException(ex);  // ✅ 直接调用 Debug.LogException
        Debug.LogError(FormatMessage($"[Exception] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));  // ✅ 直接调用 Debug.LogError
    }

    public static void LogException(string message, System.Exception ex)
    {
        Debug.LogException(ex);  // ✅ 直接调用 Debug.LogException
        Debug.LogError(FormatMessage($"[Exception] {message}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));  // ✅ 直接调用 Debug.LogError
    }
}
