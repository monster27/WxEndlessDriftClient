using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 室外装饰皮肤数据
    /// </summary>
    [Serializable]
    public class OutdoorSkinData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int categoryId;
    }

    [Serializable]
    public class OutdoorSkinListWrapper
    {
        public List<OutdoorSkinData> decorations = new List<OutdoorSkinData>();
    }

}
