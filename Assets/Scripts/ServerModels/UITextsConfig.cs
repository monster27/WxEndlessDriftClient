using System;

[Serializable]
public class UITextsConfig
{
    public EquipmentUITexts equipment;
}

[Serializable]
public class EquipmentUITexts
{
    public string notEquipped = "未放置";
    public string equipped = "已装备";
    public string equipSuccess = "装备成功！";
    public string equipFailed = "装备失败！";
    public string maxLevel = "已满级";
    public string upgradeSuccess = "升级成功！";
    public string upgradeFailed = "升级失败！";
    public string notEnoughGold = "金币不足！";
    public string currentLevel = "当前等级";
    public string nextLevelEffect = "升级后效果提升";
}