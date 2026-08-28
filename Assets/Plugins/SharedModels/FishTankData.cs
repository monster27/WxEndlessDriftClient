using System.Collections.Generic;

[System.Serializable]
public class FishTankData
{
    public int id;
    public string name;
    public string type;      // normal / special
    public int purchaseCost;
}

[System.Serializable]
public class FishTankLevelData
{
    public int level;
    public int maxCount;
    public int upgradeCost;
    public float bonus;
}

[System.Serializable]
public class FishTankConfigWrapper
{
    public List<FishTankData> fishTanks = new List<FishTankData>();
    public List<FishTankLevelData> fishTankLevels = new List<FishTankLevelData>();
    public float baseEarningRate = 0.2f;
}
