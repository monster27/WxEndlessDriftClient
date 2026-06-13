using System;
using System.Collections.Generic;
using UnityEngine;
public static partial class CommunicateEvent
{
    private static Dictionary<string, Dictionary<Type, Delegate>> eventTable = new Dictionary<string, Dictionary<Type, Delegate>>();

    // === 带参数注册 ===
    public static void Register<T>(string eventName, Action<T> callback)
    {
        Debug.Log($"[CommunicateEvent] Register - 注册事件: {eventName}, 类型: {typeof(T).Name}");
        
        if (!eventTable.ContainsKey(eventName))
            eventTable[eventName] = new Dictionary<Type, Delegate>();

        var typeTable = eventTable[eventName];
        Type dataType = typeof(T);

        if (!typeTable.ContainsKey(dataType))
            typeTable[dataType] = null;

        typeTable[dataType] = (Action<T>)typeTable[dataType] + callback;
        
        Debug.Log($"[CommunicateEvent] Register - 事件 {eventName} 注册完成");
    }

    // === 无参数注册 ===
    public static void Register(string eventName, Action callback) { Register<Action>(eventName, (_) => callback()); }

    // === 带参数取消注册 ===
    public static void Unregister<T>(string eventName, Action<T> callback)
    {
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

    // === 无参数取消注册 ===
    public static void Unregister(string eventName, Action callback) { Unregister<Action>(eventName, (_) => callback()); }

    // === 带参数触发 ===
    public static void Modify<T>(string eventName, T newData)
    {
        if (!eventTable.ContainsKey(eventName))
        {
            Debug.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册任何监听器");
            return;
        }
        
        var typeTable = eventTable[eventName];
        Type dataType = typeof(T);

        if (!typeTable.ContainsKey(dataType))
        {
            Debug.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 未注册类型 {dataType.Name} 的监听器");
            return;
        }
        
        if (typeTable[dataType] == null)
        {
            Debug.LogWarning($"[CommunicateEvent] Modify - 事件 {eventName} 的 {dataType.Name} 监听器为空");
            return;
        }
        
        Debug.Log($"[CommunicateEvent] 触发事件: {eventName}, 数据: {newData}");
        ((Action<T>)typeTable[dataType])(newData);
    }

    // === 无参数触发 ===
    public static void Modify(string eventName) { Modify<Action>(eventName, null); }

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
        Debug.LogWarning($"[CommunicateEvent] Request - requestName={requestName} 未找到处理器");
        return default(TResponse);
    }

    // === 回调机制（用于异步UI操作） ===
    private static Dictionary<string, System.Action> callbacks = new Dictionary<string, System.Action>();
    private static Dictionary<string, System.Action<bool>> boolCallbacks = new Dictionary<string, System.Action<bool>>();
    private static int callbackIdCounter = 0;

    public static string RegisterCallback(System.Action callback)
    {
        string callbackId = $"callback_{callbackIdCounter++}";
        callbacks[callbackId] = callback;
        return callbackId;
    }

    public static string RegisterCallback(System.Action<bool> callback)
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
            Debug.LogWarning($"[CommunicateEvent] OnCallback - id={callbackId} 未找到");
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
            Debug.LogWarning($"[CommunicateEvent] OnCallback - id={callbackId} 未找到");
        }
    }

    public static void ClearAll() { eventTable.Clear(); requestHandlers.Clear(); callbacks.Clear(); boolCallbacks.Clear(); }
}