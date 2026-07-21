using System;

namespace SharedModels
{
    [Serializable]
    public class NestBaitData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int applicableScene;
    }

    [Serializable]
    public class NestBaitListWrapper
    {
        public NestBaitData[] nestBaits;
    }
}