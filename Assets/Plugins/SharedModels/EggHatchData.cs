using System;

/// <summary>
/// 孵化槽位数据（服务器 ↔ 客户端传输）
/// </summary>
[Serializable]
public class EggHatchSlotData
{
    /// <summary>槽位索引 0~5</summary>
    public int slotIndex;

    /// <summary>是否占用（有蛋在孵化/已完成未领取）</summary>
    public bool isOccupied;

    /// <summary>孵化中的蛋ID（0=空槽位）</summary>
    public int eggId;

    /// <summary>蛋的稀有度</summary>
    public int rarityId;

    /// <summary>开始孵化时间（Unix秒）</summary>
    public long startTime;

    /// <summary>预计完成时间（Unix秒）</summary>
    public long endTime;

    /// <summary>剩余秒数（-1=空槽位）</summary>
    public int remainingSeconds = -1;

    /// <summary>是否已孵化完成（可领取）</summary>
    public bool isCompleted;

    /// <summary>是否已领取</summary>
    public bool isClaimed;
}
