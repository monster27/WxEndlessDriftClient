using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class CommunicateEvent
{
    private static Dictionary<string, Dictionary<Type, Delegate>> eventTable = new Dictionary<string, Dictionary<Type, Delegate>>();

    // === 无参数注册 ===
    public static void Register(string eventName, Action callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Register - 注册事件: {eventName} (无参数)");

        if (!eventTable.ContainsKey(eventName))
            eventTable[eventName] = new Dictionary<Type, Delegate>();

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Action);

        if (!typeTable.ContainsKey(dataType))
            typeTable[dataType] = null;

        typeTable[dataType] = (Action)typeTable[dataType] + callback;

        Z_Logger.Log($"[CommunicateEvent] Register - 事件 {eventName} 注册完成");
    }

    // === 1个参数注册 ===
    public static void Register<T>(string eventName, Action<T> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Register - 注册事件: {eventName}, 类型: {typeof(T).Name}");

        if (!eventTable.ContainsKey(eventName))
            eventTable[eventName] = new Dictionary<Type, Delegate>();

        var typeTable = eventTable[eventName];
        Type dataType = typeof(T);

        if (!typeTable.ContainsKey(dataType))
            typeTable[dataType] = null;

        typeTable[dataType] = (Action<T>)typeTable[dataType] + callback;

        Z_Logger.Log($"[CommunicateEvent] Register - 事件 {eventName} 注册完成");
    }

    // === 2个参数注册 ===
    public static void Register<T1, T2>(string eventName, Action<T1, T2> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Register - 注册事件: {eventName}, 类型: {typeof(T1).Name}, {typeof(T2).Name}");

        if (!eventTable.ContainsKey(eventName))
            eventTable[eventName] = new Dictionary<Type, Delegate>();

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Tuple<T1, T2>);

        if (!typeTable.ContainsKey(dataType))
            typeTable[dataType] = null;

        typeTable[dataType] = (Action<T1, T2>)typeTable[dataType] + callback;

        Z_Logger.Log($"[CommunicateEvent] Register - 事件 {eventName} 注册完成");
    }

    // === 3个参数注册 ===
    public static void Register<T1, T2, T3>(string eventName, Action<T1, T2, T3> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Register - 注册事件: {eventName}, 类型: {typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}");

        if (!eventTable.ContainsKey(eventName))
            eventTable[eventName] = new Dictionary<Type, Delegate>();

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Tuple<T1, T2, T3>);

        if (!typeTable.ContainsKey(dataType))
            typeTable[dataType] = null;

        typeTable[dataType] = (Action<T1, T2, T3>)typeTable[dataType] + callback;

        Z_Logger.Log($"[CommunicateEvent] Register - 事件 {eventName} 注册完成");
    }

    // === 无参数取消注册 ===
    public static void Unregister(string eventName, Action callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Unregister - 取消注册事件: {eventName} (无参数)");

        if (eventTable.ContainsKey(eventName))
        {
            var typeTable = eventTable[eventName];
            Type dataType = typeof(Action);

            if (typeTable.ContainsKey(dataType))
            {
                typeTable[dataType] = (Action)typeTable[dataType] - callback;

                if (typeTable[dataType] == null)
                    typeTable.Remove(dataType);

                if (typeTable.Count == 0)
                    eventTable.Remove(eventName);
            }
        }
    }

    // === 1个参数取消注册 ===
    public static void Unregister<T>(string eventName, Action<T> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Unregister - 取消注册事件: {eventName}, 类型: {typeof(T).Name}");

        if (eventTable.ContainsKey(eventName))
        {
            var typeTable = eventTable[eventName];
            Type dataType = typeof(T);

            if (typeTable.ContainsKey(dataType))
            {
                typeTable[dataType] = (Action<T>)typeTable[dataType] - callback;

                if (typeTable[dataType] == null)
                    typeTable.Remove(dataType);

                if (typeTable.Count == 0)
                    eventTable.Remove(eventName);
            }
        }
    }

    // === 2个参数取消注册 ===
    public static void Unregister<T1, T2>(string eventName, Action<T1, T2> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Unregister - 取消注册事件: {eventName}, 类型: {typeof(T1).Name}, {typeof(T2).Name}");

        if (eventTable.ContainsKey(eventName))
        {
            var typeTable = eventTable[eventName];
            Type dataType = typeof(Tuple<T1, T2>);

            if (typeTable.ContainsKey(dataType))
            {
                typeTable[dataType] = (Action<T1, T2>)typeTable[dataType] - callback;

                if (typeTable[dataType] == null)
                    typeTable.Remove(dataType);

                if (typeTable.Count == 0)
                    eventTable.Remove(eventName);
            }
        }
    }

    // === 3个参数取消注册 ===
    public static void Unregister<T1, T2, T3>(string eventName, Action<T1, T2, T3> callback)
    {
        Z_Logger.Log($"[CommunicateEvent] Unregister - 取消注册事件: {eventName}, 类型: {typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}");

        if (eventTable.ContainsKey(eventName))
        {
            var typeTable = eventTable[eventName];
            Type dataType = typeof(Tuple<T1, T2, T3>);

            if (typeTable.ContainsKey(dataType))
            {
                typeTable[dataType] = (Action<T1, T2, T3>)typeTable[dataType] - callback;

                if (typeTable[dataType] == null)
                    typeTable.Remove(dataType);

                if (typeTable.Count == 0)
                    eventTable.Remove(eventName);
            }
        }
    }

    // === 无参数触发 ===
    public static void Modify(string eventName)
    {
        Z_Logger.Log($"[CommunicateEvent] 触发事件: {eventName} (无参数)");

        if (!eventTable.ContainsKey(eventName))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册");
            return;
        }

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Action);

        if (!typeTable.ContainsKey(dataType))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册无参数类型");
            return;
        }

        if (typeTable[dataType] == null)
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 的监听器为空");
            return;
        }

        ((Action)typeTable[dataType])?.Invoke();
    }

    // === 1个参数触发 ===
    public static void Modify<T>(string eventName, T data)
    {
        Z_Logger.Log($"[CommunicateEvent] 触发事件: {eventName}, 数据: {data}");

        if (!eventTable.ContainsKey(eventName))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册");
            return;
        }

        var typeTable = eventTable[eventName];
        Type dataType = typeof(T);

        if (!typeTable.ContainsKey(dataType))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册类型 {dataType.Name}");
            return;
        }

        if (typeTable[dataType] == null)
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 的监听器为空");
            return;
        }

        ((Action<T>)typeTable[dataType])?.Invoke(data);
    }

    // === 2个参数触发 ===
    public static void Modify<T1, T2>(string eventName, T1 arg1, T2 arg2)
    {
        Z_Logger.Log($"[CommunicateEvent] 触发事件: {eventName}, 参数1: {arg1}, 参数2: {arg2}");

        if (!eventTable.ContainsKey(eventName))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册");
            return;
        }

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Tuple<T1, T2>);

        if (!typeTable.ContainsKey(dataType))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册类型 {dataType.Name}");
            return;
        }

        if (typeTable[dataType] == null)
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 的监听器为空");
            return;
        }

        ((Action<T1, T2>)typeTable[dataType])?.Invoke(arg1, arg2);
    }

    // === 3个参数触发 ===
    public static void Modify<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3)
    {
        Z_Logger.Log($"[CommunicateEvent] 触发事件: {eventName}, 参数1: {arg1}, 参数2: {arg2}, 参数3: {arg3}");

        if (!eventTable.ContainsKey(eventName))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册");
            return;
        }

        var typeTable = eventTable[eventName];
        Type dataType = typeof(Tuple<T1, T2, T3>);

        if (!typeTable.ContainsKey(dataType))
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册类型 {dataType.Name}");
            return;
        }

        if (typeTable[dataType] == null)
        {
            Z_Logger.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 的监听器为空");
            return;
        }

        ((Action<T1, T2, T3>)typeTable[dataType])?.Invoke(arg1, arg2, arg3);
    }

    // === 请求-响应机制 ===
    private static Dictionary<string, Delegate> requestHandlers = new Dictionary<string, Delegate>();

    public static void RegisterRequest<TRequest, TResponse>(string requestName, Func<TRequest, TResponse> handler)
    {
        requestHandlers[requestName] = handler;
    }

    public static TResponse Request<TRequest, TResponse>(string requestName, TRequest request)
    {
        if (requestHandlers.ContainsKey(requestName))
        {
            var handler = requestHandlers[requestName] as Func<TRequest, TResponse>;
            if (handler != null)
            {
                TResponse response = handler(request);
                return response;
            }
        }
        Z_Logger.LogWarning($"[CommunicateEvent] Request - requestName={requestName} 未找到处理器");
        return default(TResponse);
    }

    // === 回调机制（用于异步UI操作） ===
    private static Dictionary<string, Action> callbacks = new Dictionary<string, Action>();
    private static Dictionary<string, Action<bool>> boolCallbacks = new Dictionary<string, Action<bool>>();
    private static int callbackIdCounter = 0;

    public static string RegisterCallback(Action callback)
    {
        string callbackId = $"callback_{callbackIdCounter++}";
        callbacks[callbackId] = callback;
        return callbackId;
    }

    public static string RegisterCallback(Action<bool> callback)
    {
        string callbackId = $"callback_{callbackIdCounter++}";
        boolCallbacks[callbackId] = callback;
        return callbackId;
    }

    public static void OnCallback(string callbackId)
    {
        if (callbacks.ContainsKey(callbackId))
        {
            callbacks[callbackId]?.Invoke();
            callbacks.Remove(callbackId);
        }
        else
        {
            Z_Logger.LogWarning($"[CommunicateEvent] OnCallback - id={callbackId} 未找到");
        }
    }

    public static void OnCallback(string callbackId, bool result)
    {
        if (boolCallbacks.ContainsKey(callbackId))
        {
            boolCallbacks[callbackId]?.Invoke(result);
            boolCallbacks.Remove(callbackId);
        }
        else if (callbacks.ContainsKey(callbackId))
        {
            callbacks[callbackId]?.Invoke();
            callbacks.Remove(callbackId);
        }
        else
        {
            Z_Logger.LogWarning($"[CommunicateEvent] OnCallback - id={callbackId} 未找到");
        }
    }

    public static void ClearAll()
    {
        eventTable.Clear();
        requestHandlers.Clear();
        callbacks.Clear();
        boolCallbacks.Clear();
    }
}
