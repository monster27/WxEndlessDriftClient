using System;
using System.Collections.Generic;

/// <summary>宠物自动喂食等级配置（单级）</summary>
[Serializable]
public class PetFeedLevelData
{
    /// <summary>等级（1~10）</summary>
    public int level;

    /// <summary>该等级的自动喂食间隔（秒）</summary>
    public int interval;

    /// <summary>升级到下一等级消耗金币（0=满级）</summary>
    public int upgradeCost;

    /// <summary>升级描述</summary>
    public string upgradeDescription = "";
}

/// <summary>宠物自动喂食配置列表包装器（petFeedConfig.json 顶层）</summary>
[Serializable]
public class PetFeedConfigWrapper
{
    /// <summary>未升级时的基础间隔（秒）</summary>
    public int baseInterval = 7200;

    /// <summary>所有等级配置</summary>
    public List<PetFeedLevelData> levels = new List<PetFeedLevelData>();

    /// <summary>备注</summary>
    public List<string> notes = new List<string>();
}
