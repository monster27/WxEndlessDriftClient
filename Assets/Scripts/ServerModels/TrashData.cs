using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class TrashData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
    }

    [System.Serializable]
    public class TrashListWrapper
    {
        public List<TrashData> trash = new List<TrashData>();
    }
}