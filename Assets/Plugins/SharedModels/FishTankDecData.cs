using System.Collections.Generic;

[System.Serializable]
public class FishTankDecData
{
    public int id;
    public string name;
    public string description;
    public int categoryId;  // 80=摆设, 81=挂饰, 82=边框, 83=底面, 84=背景
    public float bonus;
    public int width;   // 摆设/挂饰 有效
    public int height;  // 摆设/挂饰 有效
}

[System.Serializable]
public class FishTankDecListWrapper
{
    public List<FishTankDecData> items;
}
