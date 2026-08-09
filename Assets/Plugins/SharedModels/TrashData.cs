using System;
using System.Collections.Generic;


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


[Serializable]
public class TrashListWrapper
{
    public List<TrashData> trash = new List<TrashData>();
}
