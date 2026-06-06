using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class FishData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int islandId;
        public int rarityId;
        public List<int> preferredIslandIds = new List<int>();
        public List<int> preferredTimeIds = new List<int>();
        public List<int> preferredBaitIds = new List<int>();
        public List<int> preferredWeatherIds = new List<int>();
        public int fishSpeciesId;
        public int struggleTime;
        public float flashProbability;
        public float baseWeight;
        public int baseExp;
    }

    [System.Serializable]
    public class FishListWrapper
    {
        public List<FishData> fishes = new List<FishData>();
    }
}