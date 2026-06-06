using System;

namespace ServerModels
{
    [System.Serializable]
    public class BaitData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
    }

    [System.Serializable]
    public class BaitListWrapper
    {
        public BaitData[] baits;
    }
}