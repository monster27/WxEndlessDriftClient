using System;
using System.Collections.Generic;

/// <summary>
/// 宠物栏等级配置（单级）
/// </summary>
[Serializable]
public class PetStorageLevelData
{
    /// <summary>等级（1~10）</summary>
    public int level;

    /// <summary>该等级下的宠物栏容量（格子数）</summary>
    public int capacity;

    /// <summary>升级到下一级消耗的金币；0=已满级</summary>
    public int upgradeCost;

    /// <summary>升级描述（客户端展示用）</summary>
    public string upgradeDescription = "";
}

/// <summary>
/// 宠物栏配置列表包装器（petStorage.json 的顶层结构）
/// </summary>
[Serializable]
public class PetStorageListWrapper
{
    /// <summary>初始容量（未升级时的格子数，一般=levels[0].capacity）</summary>
    public int baseCapacity = 20;

    /// <summary>所有等级配置</summary>
    public List<PetStorageLevelData> levels = new();

    /// <summary>备注</summary>
    public List<string> notes = new();
}
