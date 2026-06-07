using System;

namespace SharedModels
{
    [Serializable]
    public class BaitData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public class BaitListWrapper
    {
        public BaitData[] baits;
    }
}