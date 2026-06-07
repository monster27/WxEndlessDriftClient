using System;
using System.Collections.Generic;

namespace SharedModels
{
    [Serializable]
    public class TrashData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public class TrashListWrapper
    {
        public List<TrashData> trash = new List<TrashData>();
    }
}