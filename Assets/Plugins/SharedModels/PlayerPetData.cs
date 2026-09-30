using System;

/// <summary>
/// 宠物实例数据（服务器 ↔ 客户端传输）
/// </summary>
[Serializable]
public class PlayerPetData
{
    /// <summary>宠物实例唯一ID（自增）</summary>
    public int petInstanceId;

    /// <summary>宠物种类ID（对应 PetData.id）</summary>
    public int petId;

    /// <summary>稀有度ID</summary>
    public int rarityId;

    /// <summary>种类名称（如"小猫"）</summary>
    public string name = string.Empty;

    /// <summary>玩家自定义昵称（可空）</summary>
    public string nickname = string.Empty;

    public int level = 1;

    public int exp = 0;

    /// <summary>是否出战</summary>
    public bool isActive = false;

    /// <summary>获得时间（Unix秒）</summary>
    public long obtainedAt;

    /// <summary>上次抓昆虫时间（Unix秒）</summary>
    public long lastInsectCatchTime;

    /// <summary>下次可抓昆虫时间（Unix秒）</summary>
    /// 
    public long nextInsectCatchTime;
    /// <summary>当前饥饿度</summary>
    public int hunger;

    /// <summary>饥饿度上限</summary>
    public int maxHunger;

    /// <summary>剩余可捕捉昆虫的秒数（饥饿度耗尽倒计时）</summary>
    public int hungerRemainingSeconds;

    /// <summary>是否默认宠物（第一只，不可出售）</summary>
    public bool isDefault { get; set; }

    /// <summary>是否锁定（锁定后不可出售）</summary>
    public bool isLocked = false;
}
