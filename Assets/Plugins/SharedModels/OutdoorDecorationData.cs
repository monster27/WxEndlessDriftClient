using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 室外装饰数据
    /// </summary>
    [Serializable]
    public class OutdoorDecorationData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int categoryId;
    }

    [Serializable]
    public class OutdoorDecorationListWrapper
    {
        public List<OutdoorDecorationData> decorations = new List<OutdoorDecorationData>();
    }

}
