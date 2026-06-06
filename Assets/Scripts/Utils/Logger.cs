using UnityEngine;

namespace Utils
{
    public static class Logger
    {
        private static string GetTimestamp()
        {
            return System.DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]");
        }

        public static void Log(string message)
        {
            Debug.Log($"{GetTimestamp()} {message}");
        }

        public static void Log(string format, params object[] args)
        {
            Debug.Log($"{GetTimestamp()} {string.Format(format, args)}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{GetTimestamp()} {message}");
        }

        public static void LogWarning(string format, params object[] args)
        {
            Debug.LogWarning($"{GetTimestamp()} {string.Format(format, args)}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"{GetTimestamp()} {message}");
        }

        public static void LogError(string format, params object[] args)
        {
            Debug.LogError($"{GetTimestamp()} {string.Format(format, args)}");
        }

        public static void LogColor(string message, string color)
        {
            Debug.Log($"{GetTimestamp()} <color={color}>{message}</color>");
        }

        public static void LogColor(string format, string color, params object[] args)
        {
            Debug.Log($"{GetTimestamp()} <color={color}>{string.Format(format, args)}</color>");
        }
    }
}
