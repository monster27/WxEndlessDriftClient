// ==================== TrashData.cs ====================
using System;

[Serializable]
public class TrashData
{
    public int id;
    public string name;
    public float weight;
    public int weightValue;
    public int experience;

    public TrashData()
    {
        id = 0;
        name = "";
        weight = 0f;
        weightValue = 0;
        experience = 0;
    }
}
