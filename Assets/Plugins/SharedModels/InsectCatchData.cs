using System;

/// <summary>
/// 抓昆虫结果（服务器 → 客户端）
/// </summary>
[Serializable]
public class InsectCatchResult
{
    public bool success;
    public string message = string.Empty;

    /// <summary>昆虫实例唯一ID</summary>
    public int insectInstanceId;

    /// <summary>昆虫种类ID</summary>
    public int insectId;

    public string insectName = string.Empty;

    public int rarityId;

    /// <summary>下次可抓的冷却秒数</summary>
    public int nextCooldownSeconds;
}

/// <summary>
/// 玩家昆虫数据（服务器 → 客户端）
/// </summary>
[Serializable]
public class PlayerInsectData
{
    /// <summary>昆虫实例唯一ID</summary>
    public int insectInstanceId;

    /// <summary>昆虫种类ID</summary>
    public int insectId;

    public string name = string.Empty;

    public int rarityId;

    /// <summary>捕获时间（Unix秒）</summary>
    public long caughtTimestamp;

    /// <summary>由哪只宠物抓到</summary>
    public int caughtByPetInstanceId;
}
