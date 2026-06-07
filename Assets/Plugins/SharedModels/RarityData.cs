using System;
using System.Collections.Generic;

namespace SharedModels
{
    [Serializable]
    public class RarityData
    {
        public int id;
        public string name = string.Empty;
        public string color = string.Empty;
        public int weight;
        public int exp;
    }

    [Serializable]
    public class RarityListWrapper
    {
        public List<RarityData> rarities = new List<RarityData>();
    }
}