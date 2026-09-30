using System;

/// <summary>
/// 宠物栏状态（服务器 → 客户端）
/// </summary>
[Serializable]
public class PetStorageStatusData
{
    public bool success;
    public string message = string.Empty;

    /// <summary>当前等级</summary>
    public int level;

    /// <summary>当前容量（格子数）</summary>
    public int capacity;

    /// <summary>已使用格子数</summary>
    public int used;

    /// <summary>剩余可用格子数</summary>
    public int remaining;

    /// <summary>是否已满</summary>
    public bool isFull;

    /// <summary>最大等级</summary>
    public int maxLevel;
}
